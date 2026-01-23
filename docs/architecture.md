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

## Session Provider Modes

The matchmaking service supports two session provider modes to accommodate different deployment patterns. Both implementations use the same `ISessionCreator` interface, allowing operators to switch between patterns via configuration without code changes.

### Overview

| Mode | Purpose | Session Creation | Use Cases |
|------|---------|------------------|-----------|
| **Create** | Matchmaker creates sessions | New session per match | P2P gameplay, dedicated server provider integration |
| **Find** | Matchmaker finds sessions | Existing available sessions | Player-hosted servers, pre-allocated dedicated servers |

### Create Mode (Default)

In "create" mode, the matchmaker creates new EOS sessions for each match.

**Architecture:**
```
Players → MatchMaker → Create Match → EOSSessionCreator → New EOS Session
```

**Use Cases:**
- **Peer-to-peer gameplay**: Matched players connect directly to each other
- **Dedicated server provider integration**: Matchmaker creates session, then allocates server from provider

**Flow:**
1. Players submit match requests
2. MatchMaker creates a match from pending requests
3. EOSSessionCreator creates a new EOS session
4. Session details returned to matched players
5. Players join the session and connect (P2P or via allocated server)

**Configuration:**
```json
{
  "SessionProvider": {
    "Mode": "create"
  }
}
```

**Implementation:** `EOSSessionCreator` class creates sessions using EOS SDK's `UpdateSession` API.

### Find Mode

In "find" mode, the matchmaker finds existing available EOS sessions created by game servers and notifies the session owner.

**Architecture:**
```
Game Servers → Create EOS Sessions (available)
Players → MatchMaker → Create Match → EOSSessionFinder → Find & Claim Session → Notify Owner
```

**Use Cases:**
- **Player-hosted servers**: Players create sessions and wait for matchmaker to assign players
- **Pre-allocated dedicated servers**: Dedicated servers create their own sessions and register with EOS

**Flow:**
1. Game servers create EOS sessions and mark them as available (0 players, not started)
2. Players submit match requests
3. MatchMaker creates a match from pending requests
4. EOSSessionFinder searches for available sessions
5. Finder claims a session (adds to local cache)
6. Finder notifies session owner with match details
7. Session owner updates session to "started" state in EOS
8. Players join the claimed session

**Configuration:**
```json
{
  "SessionProvider": {
    "Mode": "find"
  },
  "SessionFinder": {
    "BucketId": "default",
    "MaxSearchResults": 10,
    "ClaimedSessionExpirationSeconds": 300
  }
}
```

**Configuration Options:**
- `BucketId`: Session bucket identifier to filter search results (default: "default")
- `MaxSearchResults`: Maximum number of search results to retrieve (default: 10)
- `ClaimedSessionExpirationSeconds`: Expiration time in seconds for claimed session cache entries (default: 300)

**Implementation:** `EOSSessionFinder` class searches for sessions using EOS SDK's `SessionSearch` API.

### Session Claiming and Concurrency

In "find" mode, the session finder uses a local claimed sessions cache to prevent concurrent matches from claiming the same session:

**Claimed Sessions Cache:**
- Thread-safe in-memory cache (`ConcurrentDictionary`)
- Tracks recently claimed sessions with timestamps
- Automatic expiration after configured time (default: 5 minutes)
- Prevents race conditions when multiple matches are created simultaneously

**Claiming Process:**
1. Search for available sessions (0 players, not started, not in cache)
2. Select first unclaimed session from results
3. Add session to claimed cache with current timestamp
4. Notify session owner
5. Return session info to matchmaker

**Why Local Cache?**
- EOS session state updates are not instantaneous
- Multiple concurrent matches might see the same "available" session
- Local cache provides immediate consistency within the matchmaker
- Session owner is responsible for updating EOS state to "started"

### Session Owner Notification

In "find" mode, the session owner must be notified when their session is claimed so they can prepare for players.

**Default Implementation:**

The service includes a stub implementation (`StubSessionOwnerNotifier`) that logs notifications:

```csharp
public class StubSessionOwnerNotifier : ISessionOwnerNotifier
{
    public Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo)
    {
        _logger.LogInformation(
            "STUB: Session claimed - SessionId={SessionId}, MatchId={MatchId}, " +
            "ConnectionInfo={ConnectionInfo}, UserIds={UserIds}",
            sessionId, match.MatchId, connectionInfo, string.Join(",", match.UserIds));
        
        // TODO: Implement actual notification mechanism
        return Task.CompletedTask;
    }
}
```

**Notification Payload:**

The notification includes complete match information:
- `sessionId`: The EOS session ID that was claimed
- `match.MatchId`: Unique identifier for the match
- `match.UserIds`: List of all matched player IDs
- `match.RequestIds`: List of all match request IDs
- `connectionInfo`: Connection information from the EOS session

**Custom Implementation Example:**

Developers should implement their own notification mechanism based on their infrastructure:

```csharp
public class HttpSessionOwnerNotifier : ISessionOwnerNotifier
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpSessionOwnerNotifier> _logger;

    public async Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo)
    {
        var payload = new
        {
            sessionId,
            match = new
            {
                matchId = match.MatchId,
                userIds = match.UserIds,
                requestIds = match.RequestIds,
                createdAt = match.CreatedAt
            }
        };

        try
        {
            // connectionInfo contains the game server's HTTP endpoint
            await _httpClient.PostAsJsonAsync($"{connectionInfo}/session-claimed", payload);
            _logger.LogInformation("Notified session owner: SessionId={SessionId}", sessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to notify session owner: SessionId={SessionId}", sessionId);
        }
    }
}
```

**Registration:**
```csharp
// In Program.cs, replace the stub notifier
builder.Services.AddHttpClient<HttpSessionOwnerNotifier>();
builder.Services.AddSingleton<ISessionOwnerNotifier, HttpSessionOwnerNotifier>();
```

**Notification Mechanisms:**

Common notification approaches:
- **HTTP POST**: Game server exposes webhook endpoint
- **Message Queue**: Publish to RabbitMQ, AWS SQS, Azure Service Bus
- **gRPC**: Call game server's gRPC service
- **WebSocket**: Send message over persistent connection
- **Database**: Write to shared database table that servers poll

### Extending Create Mode with Dedicated Server Provider

The "create" mode can be extended to support dedicated server allocation by adding a dedicated server provider.

**Architecture:**
```
Players → MatchMaker → Create Match → Allocate Server → Create Session → Add Server Info
```

**Example Interface:**
```csharp
public interface IDedicatedServerProvider
{
    Task<DedicatedServer> AllocateServerAsync();
    Task ReleaseServerAsync(string serverId);
}

public class DedicatedServer
{
    public string ServerId { get; set; }
    public string IpAddress { get; set; }
    public int Port { get; set; }
    public string ConnectionString => $"{IpAddress}:{Port}";
}
```

**Integration in MatchMaker:**
```csharp
// In MatchMaker.TryMatchAsync()
var match = new Match(requestsForMatch);

// Allocate a dedicated server
var server = await _serverProvider.AllocateServerAsync();

// Create session with server info
var sessionInfo = await _sessionCreator.GetSessionAsync(match);

// Add server connection info to session metadata
await _eosService.UpdateSessionAttribute(
    sessionInfo.SessionId, 
    "server_address", 
    server.ConnectionString);

_logger.LogInformation(
    "Match created with dedicated server: MatchId={MatchId}, ServerId={ServerId}",
    match.MatchId, server.ServerId);
```

**Example: AWS GameLift Integration**
```csharp
public class GameLiftServerProvider : IDedicatedServerProvider
{
    private readonly IAmazonGameLift _gameLiftClient;
    private readonly string _fleetId;

    public async Task<DedicatedServer> AllocateServerAsync()
    {
        var request = new CreateGameSessionRequest
        {
            FleetId = _fleetId,
            MaximumPlayerSessionCount = 4
        };

        var response = await _gameLiftClient.CreateGameSessionAsync(request);
        
        return new DedicatedServer
        {
            ServerId = response.GameSession.GameSessionId,
            IpAddress = response.GameSession.IpAddress,
            Port = response.GameSession.Port
        };
    }

    public async Task ReleaseServerAsync(string serverId)
    {
        // GameLift automatically terminates sessions when empty
        await Task.CompletedTask;
    }
}
```

**Example: Agones (Kubernetes) Integration**
```csharp
public class AgonesServerProvider : IDedicatedServerProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _allocatorEndpoint;

    public async Task<DedicatedServer> AllocateServerAsync()
    {
        var response = await _httpClient.PostAsync($"{_allocatorEndpoint}/allocate", null);
        var allocation = await response.Content.ReadFromJsonAsync<GameServerAllocation>();
        
        return new DedicatedServer
        {
            ServerId = allocation.Name,
            IpAddress = allocation.Status.Address,
            Port = allocation.Status.Ports[0].Port
        };
    }

    public async Task ReleaseServerAsync(string serverId)
    {
        // Mark server as ready for reallocation
        await _httpClient.PostAsync($"{_allocatorEndpoint}/ready/{serverId}", null);
    }
}
```

This extension is not included in the sample but demonstrates how the architecture can be adapted to your deployment needs.

### Choosing the Right Mode

**Use Create Mode when:**
- Players connect peer-to-peer (no dedicated servers)
- You want the matchmaker to orchestrate server allocation
- You're integrating with a dedicated server provider (GameLift, Agones, etc.)
- Sessions are ephemeral and created on-demand

**Use Find Mode when:**
- Game servers create their own sessions
- You have pre-allocated or player-hosted servers
- Servers register themselves with EOS
- You want servers to control session lifecycle

**Hybrid Approach:**

Some games may use both modes:
- **Create mode** for casual/quick play (P2P or on-demand servers)
- **Find mode** for custom/private servers (player-hosted or dedicated)

This requires running two separate matchmaker instances with different configurations.

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
         CancelMatchRequest│                 Session
                │          │                 Creation
                │          │                 Failed
                ▼          ▼                    │
         ┌──────────┐  ┌─────────┐              │
         │CANCELLED │  │ MATCHED │              │
         └──────────┘  └─────────┘              │
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
