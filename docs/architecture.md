# Architecture Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide explains the technical architecture, design decisions, and key concepts of the Simple EOS Matchmaking Service Extension.

## Architecture Overview

The Simple EOS Matchmaking Service is an Extend Service Extension that provides automatic player matching with EOS session creation. The service follows a clean architecture pattern with clear separation of concerns between request handling, match storage, background matching, and session creation.

### Request Flow

```
1. Client submits match request → gRPC Gateway (HTTP/JSON)
2. Gateway forwards to gRPC Server
3. Auth interceptor validates bearer token and permissions
4. MatchmakingService handles request
5. Request stored in MatchPool (in-memory)
6. Background MatchMaker periodically checks pool
7. When enough players available, MatchMaker creates Match
8. SessionCreator creates EOS session
9. Notifier logs match event (extensible)
10. Request status updated to MATCHED with session details
11. Client polls GetMatchStatus to retrieve session info
```

### High-Level Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      gRPC Gateway                            │
│                   (HTTP/JSON → gRPC)                         │
└────────────────────────┬────────────────────────────────────┘
                         │
┌────────────────────────▼────────────────────────────────────┐
│                  gRPC Server (C#)                            │
│  ┌──────────────────────────────────────────────────────┐   │
│  │         AuthorizationInterceptor                     │   │
│  │    (Token validation & permission checks)            │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │         MatchmakingService                           │   │
│  │  (Submit, GetStatus, Cancel endpoints)               │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              MatchPool                               │   │
│  │  (Thread-safe in-memory request storage)             │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              MatchMaker                              │   │
│  │  (Background service - periodic matching)            │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │          SessionCreator                              │   │
│  │  (Creates EOS sessions for matches)                  │   │
│  └──────────────────────┬───────────────────────────────┘   │
│                         │                                    │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              Notifier                                │   │
│  │  (Logs match events - extensible)                    │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   EOS SDK Platform   │
              │  (Session creation)  │
              └──────────────────────┘
```

---

## Core Components

### MatchmakingService

**Location:** `Services/MatchmakingService.cs`

**Purpose:** gRPC service implementation that handles client requests for matchmaking operations.

**Responsibilities:**
- Accept match requests from authenticated users
- Validate and prevent duplicate requests per user
- Store requests in MatchPool
- Provide status queries for pending/matched requests
- Handle cancellation of pending requests
- Extract user ID from authorization context

**Key Methods:**
- `SubmitMatchRequest()` - Creates new match request, returns request ID
- `GetMatchStatus()` - Returns current status and session details if matched
- `CancelMatchRequest()` - Cancels pending request (only if status is PENDING)

**Error Handling:**
- Returns `FailedPrecondition` if user already has pending request
- Returns `NotFound` if request ID doesn't exist
- Returns `FailedPrecondition` if trying to cancel non-pending request
- Returns `Unauthenticated` if authorization header missing or invalid

### MatchPool

**Location:** `Services/MatchPool.cs`

**Purpose:** Thread-safe in-memory storage for pending match requests.

**Responsibilities:**
- Store match requests indexed by request ID and user ID
- Maintain insertion order for FIFO matching
- Provide thread-safe access to request data
- Remove expired requests based on timeout
- Support retrieval of oldest N requests for matching

**Data Structures:**
- `Dictionary<string, MatchRequest>` - Fast lookup by request ID
- `Dictionary<string, MatchRequest>` - Fast lookup by user ID (prevents duplicates)
- `List<MatchRequest>` - Maintains insertion order for FIFO

**Thread Safety:**
- All operations protected by lock
- Ensures consistency across concurrent access from gRPC handlers and background matcher

**Key Methods:**
- `Add()` - Add new request to pool
- `Remove()` - Remove request by ID
- `Get()` - Retrieve request by ID
- `GetByUserId()` - Check if user has pending request
- `GetOldest(count)` - Get oldest N requests for matching
- `RemoveExpired(timeout)` - Remove and return expired requests

### MatchMaker

**Location:** `Services/MatchMaker.cs`

**Purpose:** Background service that periodically creates matches from the pool.

**Responsibilities:**
- Run as hosted service (IHostedService)
- Periodically check pool for matching opportunities
- Remove expired requests
- Create matches when enough players available
- Coordinate session creation
- Handle failures gracefully (return requests to pool)

**Configuration:**
- `MatchSize` - Number of players per match (default: 2)
- `TickInterval` - How often to check for matches (default: 1 second)
- `RequestTimeout` - How long before requests expire (default: 60 seconds)

**Matching Algorithm:**
1. Remove expired requests from pool
2. Check if pool has >= MatchSize requests
3. Get oldest MatchSize requests (FIFO)
4. Remove requests from pool
5. Create Match object
6. Call SessionCreator to create EOS session
7. Update request statuses to MATCHED with session ID
8. Call Notifier to log match event
9. On failure: return requests to pool and break

**Error Handling:**
- Logs errors but continues running
- Returns requests to pool if session creation fails
- Breaks out of matching loop on failure to avoid infinite retry

### SessionCreator

**Location:** `Services/SessionCreator.cs`

**Purpose:** Creates EOS sessions for matched players using the EOS SDK.

**Responsibilities:**
- Create EOS session with unique session ID
- Configure session with match metadata
- Add session attributes (match ID, request IDs)
- Handle EOS SDK callbacks asynchronously
- Tick EOS platform while waiting for callbacks

**EOS Integration:**
- Uses `Epic.OnlineServices.Sessions` interface
- Creates session modification handle
- Sets session name, bucket ID, max players
- Adds custom attributes for match tracking
- Calls `UpdateSession()` to create the session
- Polls platform with `Tick()` until callback completes

**Session Configuration:**
- Session name: `match-{matchId}`
- Session ID: Generated GUID
- Bucket ID: "default"
- Max players: Match size
- Presence: Disabled
- Sanctions: Disabled

**Attributes:**
- `match_id` - The match GUID
- `match_request_ids` - JSON array of request IDs

**Timeout:**
- 10 second timeout for session creation
- Throws TimeoutException if EOS doesn't respond

### Notifier

**Location:** `Services/Notifier.cs`

**Purpose:** Extensibility point for notifying players when matches are found.

**Current Implementation:**
- `LoggingNotifier` - Simple implementation that logs match events to console
- Logs session ID and list of matched user IDs

**Extensibility:**

The `INotifier` interface allows game developers to implement custom notification mechanisms:

```csharp
public interface INotifier
{
    Task NotifyMatchAsync(SessionInfo sessionInfo);
}
```

**Possible Integration Services:**
- Push notification services (Firebase Cloud Messaging, Apple Push Notification Service)
- WebSocket servers for real-time client notifications
- AccelByte Lobby service for in-game notifications
- Message queues (RabbitMQ, Azure Service Bus) for asynchronous processing
- Webhook endpoints for custom game server integration
- Discord/Slack bots for community notifications

**Example: Webhook Implementation**

A webhook implementation could look something like this:

```csharp
public class WebhookNotifier : INotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<WebhookNotifier> _logger;
    private readonly string _webhookUrl;

    public async Task NotifyMatchAsync(SessionInfo sessionInfo)
    {
        // Send notifications to all players in parallel
        var notificationTasks = sessionInfo.UserIds.Select(userId =>
            SendNotificationAsync(userId, sessionInfo.SessionId)
        );

        await Task.WhenAll(notificationTasks);
    }

    private async Task SendNotificationAsync(string userId, string sessionId)
    {
        try
        {
            var payload = new
            {
                type = "match_found",
                user_id = userId,
                session_id = sessionId,
                timestamp = DateTime.UtcNow
            };

            await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to notify user {UserId}", userId);
        }
    }
}
```

**Registration:**

To use a custom notifier, register it in `Program.cs`:

```csharp
// Replace LoggingNotifier with your implementation
builder.Services.AddSingleton<INotifier, YourCustomNotifier>();
```

---

## Data Models

### MatchRequest

**Location:** `Model/MatchRequest.cs`

**Purpose:** Represents a matchmaking request from a single user.

**Fields:**
- `RequestId` (string) - Unique GUID for the request
- `UserId` (string) - User who submitted the request
- `Status` (enum) - Current status (Pending, Matched, Expired, Cancelled)
- `CreatedAt` (DateTime) - When request was created (UTC)
- `MatchedAt` (DateTime?) - When request was matched (UTC, nullable)
- `SessionId` (string?) - EOS session ID if matched (nullable)
- `Metadata` (Dictionary<string, string>?) - Optional custom metadata

**Status Values:**
- `Pending` - Waiting in pool for match
- `Matched` - Successfully matched with other players
- `Expired` - Timed out before match found
- `Cancelled` - User cancelled the request

### Match

**Location:** `Model/Match.cs`

**Purpose:** Represents a completed match with multiple match requests.

**Fields:**
- `MatchId` (string) - Unique GUID for the match
- `Requests` (List<MatchRequest>) - All requests in this match
- `CreatedAt` (DateTime) - When match was created (UTC)

### SessionInfo

**Location:** `Model/SessionInfo.cs`

**Purpose:** Information about a created EOS session.

**Fields:**
- `SessionId` (string) - EOS session ID
- `RequestIds` (List<string>) - All request IDs in the session
- `UserIds` (List<string>) - All user IDs in the session
- `CreatedAt` (DateTime) - When session was created (UTC)

---

## Matching Algorithm

The matcher uses a **FIFO (First-In-First-Out)** algorithm to ensure fairness:

### Algorithm Steps

1. **Expiration Check**
   - Remove requests older than `RequestTimeout`
   - Update their status to `Expired`
   - Log count of expired requests

2. **Match Availability Check**
   - Check if pool has >= `MatchSize` requests
   - If not, wait until next tick

3. **Request Selection**
   - Get oldest `MatchSize` requests from pool
   - Requests are ordered by `CreatedAt` timestamp
   - Ensures players who waited longest get matched first

4. **Request Removal**
   - Remove selected requests from pool
   - Verify all requests were successfully removed
   - If removal fails, return requests to pool and abort

5. **Match Creation**
   - Create `Match` object with selected requests
   - Generate unique match ID

6. **Session Creation**
   - Call `SessionCreator.CreateSessionAsync()`
   - Create EOS session with match metadata
   - Wait for EOS callback (with timeout)

7. **Status Update**
   - Update all request statuses to `Matched`
   - Set `MatchedAt` timestamp
   - Set `SessionId` from EOS

8. **Notification**
   - Call `Notifier.NotifyMatchAsync()`
   - Log or send notifications to players

9. **Error Handling**
   - If session creation fails, return requests to pool
   - Break out of matching loop to avoid infinite retry
   - Log error for investigation

### Algorithm Properties

- **Fairness:** FIFO ensures players who wait longest get matched first
- **Simplicity:** No complex skill-based matching or ranking
- **Reliability:** Requests returned to pool on failure
- **Scalability:** In-memory pool suitable for moderate load
- **Configurability:** Match size, tick interval, and timeout are configurable

### Performance Characteristics

- **Time Complexity:** O(n) for getting oldest requests, O(1) for lookups
- **Space Complexity:** O(n) where n is number of pending requests
- **Throughput:** Limited by EOS session creation latency (~100-500ms)
- **Concurrency:** Thread-safe pool supports concurrent access

---

## Request Lifecycle

### State Diagram

```
                    SubmitMatchRequest
                           │
                           ▼
                    ┌─────────────┐
                    │   PENDING   │◄──┐
                    └─────────────┘   │
                           │          │
                ┌──────────┼──────────┴─────────┐
                │          │                    │
         CancelMatchRequest│              Session
                │          │              Creation
                │          │               Failed
                ▼          ▼                    │
         ┌──────────┐  ┌─────────┐             │
         │CANCELLED │  │ MATCHED │             │
         └──────────┘  └─────────┘             │
                │                               │
         RequestTimeout                         │
                │                               │
                ▼                               │
         ┌──────────┐                           │
         │ EXPIRED  │◄──────────────────────────┘
         └──────────┘
```

### State Transitions

1. **PENDING → MATCHED**
   - Trigger: MatchMaker successfully creates match and EOS session
   - Actions: Set `MatchedAt`, set `SessionId`, notify players
   - Irreversible: Cannot cancel or expire after matching

2. **PENDING → EXPIRED**
   - Trigger: Request age exceeds `RequestTimeout`
   - Actions: Remove from pool, update status
   - Automatic: Checked every tick by MatchMaker

3. **PENDING → CANCELLED**
   - Trigger: User calls `CancelMatchRequest`
   - Actions: Remove from pool, update status
   - User-initiated: Only allowed while PENDING

4. **PENDING → PENDING (on failure)**
   - Trigger: Session creation fails
   - Actions: Return request to pool
   - Retry: Request remains eligible for future matches

### Terminal States

- **MATCHED** - Success, player can join session
- **EXPIRED** - Timeout, player must submit new request
- **CANCELLED** - User cancelled, player must submit new request

---

## Authorization & Permissions

The service uses AccelByte IAM's permission-based authorization to secure endpoints.

### Permission Model

All endpoints require:
- Valid Bearer token in Authorization header
- Appropriate permission for the operation
- Access to the specified namespace

### Endpoint Permissions

Defined in `Protos/matchmaking.proto`:

1. **SubmitMatchRequest**
   - Resource: `NAMESPACE:{namespace}:MATCHMAKING`
   - Action: `CREATE`

2. **GetMatchStatus**
   - Resource: `NAMESPACE:{namespace}:MATCHMAKING`
   - Action: `READ`

3. **CancelMatchRequest**
   - Resource: `NAMESPACE:{namespace}:MATCHMAKING`
   - Action: `DELETE`

### Authorization Flow

1. **Token Extraction**
   - `AuthorizationInterceptor` extracts Bearer token from header
   - Validates token format

2. **Token Validation**
   - Validates token with AccelByte IAM
   - Checks token expiration
   - Verifies token signature

3. **Permission Check**
   - Extracts required permission from proto annotation
   - Checks if token has required permission
   - Validates namespace access

4. **Request Processing**
   - If authorized, request proceeds to service handler
   - If not authorized, returns `PermissionDenied` error

### Development Mode

Authorization can be disabled for local development:

```bash
PLUGIN_GRPC_SERVER_AUTH_ENABLED=false
```

**Warning:** Only use this for local development. Never disable in production.

---

## Architecture Decisions

### In-Memory Storage

**Decision:** Use in-memory `MatchPool` instead of database.

**Rationale:**
- Matchmaking requests are ephemeral (expire after 60 seconds)
- High read/write frequency (every tick)
- Low latency requirements
- No need for persistence across restarts
- Simpler implementation and deployment

**Trade-offs:**
- Requests lost on service restart (acceptable for matchmaking)
- Limited to single instance (no horizontal scaling)
- Memory usage grows with pending requests

**Mitigation:**
- Request timeout prevents unbounded growth
- Suitable for moderate load (thousands of concurrent requests)
- For high scale, consider Redis or distributed cache

### FIFO Matching Algorithm

**Decision:** Use simple FIFO matching instead of skill-based matching.

**Rationale:**
- Simplicity and predictability
- Fairness (first come, first served)
- No need for player skill data
- Fast matching (no complex calculations)
- Easy to understand and debug

**Trade-offs:**
- No skill balancing
- No team composition
- No region matching

**Extensibility:**
- Can be extended with metadata-based matching
- Metadata field allows custom matching criteria
- MatchMaker can be replaced with custom implementation

### Background Matching Service

**Decision:** Use background service with periodic ticking instead of event-driven matching.

**Rationale:**
- Predictable resource usage
- Simple implementation
- Easy to configure (tick interval)
- Batches multiple matches per tick
- Reduces EOS API calls

**Trade-offs:**
- Slight delay (up to tick interval)
- Runs even when pool is empty

**Configuration:**
- Default 1 second tick interval
- Configurable via `TickIntervalSeconds`

### EOS Session Integration

**Decision:** Create EOS sessions for all matches.

**Rationale:**
- Provides session infrastructure for games
- Handles player connectivity
- Supports session attributes for match metadata
- Industry-standard solution

**Trade-offs:**
- Requires EOS account and configuration
- Adds latency to matching (100-500ms)
- Requires EOS SDK integration

**Alternative:**
- Could return match results without session creation
- Games could create their own sessions

### gRPC with REST Gateway

**Decision:** Implement gRPC service with HTTP/JSON gateway.

**Rationale:**
- High performance binary protocol (gRPC)
- Easy integration for web clients (REST)
- Single implementation, dual protocols
- Auto-generated Swagger documentation
- Type-safe contracts (protobuf)

**Trade-offs:**
- More complex build process
- Requires gateway layer
- Larger deployment size

---

## Observability

### Metrics

**Prometheus metrics available at `:8080/metrics`:**

- Request counts per endpoint
- Request duration histograms
- Error rates
- Active request counts
- Pool size gauge
- Match creation rate

### Tracing

**OpenTelemetry distributed tracing:**

- Traces requests through gateway → gRPC → services
- Includes EOS SDK calls
- Exports to Zipkin (configurable)
- Helps debug latency issues

### Logging

**Structured logging with Microsoft.Extensions.Logging:**

- Request submission and cancellation
- Match creation events
- Session creation success/failure
- Expired request cleanup
- Error conditions

**Log Levels:**
- `Information` - Normal operations
- `Warning` - Recoverable issues
- `Error` - Failures requiring attention

---

## Next Steps

- **Set up the service**: See [Setup Guide](setup.md)
- **Test the service**: See [Testing Guide](testing_guide.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
