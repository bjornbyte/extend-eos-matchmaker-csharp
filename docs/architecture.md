# Architecture Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

> **⚠️ Note:** Code examples in this document were generated with AI assistance and have not been tested. They are provided as architectural guidance. See [Implementation Examples](examples.md) for complete code samples.

---

This guide explains the technical architecture, design decisions, and key concepts of the Simple EOS Matchmaking Service Extension.

## Architecture Overview

The service provides automatic player matching with EOS session creation, following a clean architecture pattern with clear separation of concerns.

### Request Flow

```
Client → gRPC Gateway → Auth Interceptor → MatchmakingService → MatchPool
→ Background MatchMaker → SessionCreator → EOS Session → Notifier → Client
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
│  └──────────────────────┬───────────────────────────────┘   │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │         MatchmakingService                           │   │
│  └──────────────────────┬───────────────────────────────┘   │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              MatchPool                               │   │
│  └──────────────────────┬───────────────────────────────┘   │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              MatchMaker                              │   │
│  └──────────────────────┬───────────────────────────────┘   │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │          SessionCreator                              │   │
│  └──────────────────────┬───────────────────────────────┘   │
│  ┌──────────────────────▼───────────────────────────────┐   │
│  │              Notifier                                │   │
│  └──────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   EOS SDK Platform   │
              └──────────────────────┘
```

---

## Session Provider Modes

The service supports two modes for session management, configured via `SessionProvider.Mode`.

### Mode Comparison

| Mode | Session Creation | Use Cases | Implementation |
|------|------------------|-----------|----------------|
| **Create** (default) | Matchmaker creates new sessions | P2P gameplay, on-demand servers | `EOSSessionCreator` |
| **Find** | Matchmaker finds existing sessions | Pre-allocated servers, player-hosted | `EOSSessionFinder` |

### Create Mode

**Flow:** Players → MatchMaker → Create Match → Create EOS Session → Players Connect

**Configuration:**
```json
{
  "SessionProvider": { "Mode": "create" }
}
```

**Use when:**
- Players connect peer-to-peer
- Matchmaker orchestrates server allocation
- Sessions are ephemeral and created on-demand

**Extension:** Can be extended with `IDedicatedServerProvider` to allocate servers from providers (GameLift, custom allocator, etc.)

### Find Mode

**Flow:** Servers Create Sessions → Players → MatchMaker → Find Available Session → Notify Server → Players Connect

**Configuration:**
```json
{
  "SessionProvider": { "Mode": "find" },
  "SessionFinder": {
    "BucketId": "default",
    "MaxSearchResults": 10,
    "ClaimedSessionExpirationSeconds": 300
  }
}
```

**Use when:**
- Game servers create their own EOS sessions
- Pre-allocated or player-hosted servers
- Servers control session lifecycle

**Session Claiming:**
- Uses local cache to prevent race conditions
- Claimed sessions expire after configured time (default: 5 minutes)
- Session owner updates EOS state after notification

**Session Owner Notification:**

Implement `ISessionOwnerNotifier` to notify servers when their session is claimed.

**Default:** `StubSessionOwnerNotifier` (logs only - replace for production)

**Recommended for Unreal Engine:** UDP notifications (native socket support, minimal code)

**See:** [Implementation Examples](examples.md) for UDP, HTTP, and other notification mechanisms

---

## Core Components

### MatchmakingService

**Location:** `Services/MatchmakingService.cs`

**Purpose:** gRPC service handling client requests (submit, status, cancel)

**Key Methods:**
- `SubmitMatchRequest()` - Creates new match request, returns request ID
- `GetMatchStatus()` - Returns current status and session details if matched
- `CancelMatchRequest()` - Cancels pending request

**Error Handling:**
- `FailedPrecondition` - User already has pending request or cannot cancel
- `NotFound` - Request ID doesn't exist
- `Unauthenticated` - Missing or invalid authorization

### MatchPool

**Location:** `Services/MatchPool.cs`

**Purpose:** Thread-safe in-memory storage for pending match requests

**Data Structures:**
- `Dictionary<string, MatchRequest>` - Fast lookup by request ID
- `Dictionary<string, MatchRequest>` - Fast lookup by user ID (prevents duplicates)
- `List<MatchRequest>` - Maintains insertion order for FIFO

**Key Methods:**
- `Add()` - Add new request
- `Remove()` - Remove request by ID
- `GetOldest(count)` - Get oldest N requests for matching
- `RemoveExpired(timeout)` - Remove and return expired requests

### MatchMaker

**Location:** `Services/MatchMaker.cs`

**Purpose:** Background service that periodically creates matches from the pool

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
7. Update request statuses to MATCHED
8. Call Notifier to log match event
9. On failure: return requests to pool

### SessionCreator

**Location:** `Services/SessionCreator.cs`

**Purpose:** Creates EOS sessions for matched players using the EOS SDK

**Session Configuration:**
- Session name: `match-{matchId}`
- Session ID: Generated GUID
- Bucket ID: "default"
- Max players: Match size
- Attributes: match_id, match_request_ids

**Timeout:** 10 seconds for session creation

### Notifier

**Location:** `Services/Notifier.cs`

**Purpose:** Extensibility point for notifying players when matches are found

**Current Implementation:** `LoggingNotifier` - Logs match events to console

**Extensibility:** Implement `INotifier` for custom notification mechanisms (webhooks, push notifications, message queues)

---

## Extension Points

The service is designed with clear extension points for customization. Extension points are categorized into two levels:

### Application-Level Extension Points

These interfaces define how the service integrates with your game-specific systems.

#### IPlayerNotifier

**Purpose:** Notifies players when matches are found

**Default:** `LoggingPlayerNotifier` - Logs to console

**When to Implement:**
- Send push notifications to mobile devices
- Notify players via webhooks
- Integrate with message queue system
- Send in-game notifications through lobby service

**Interface:**
```csharp
public interface IPlayerNotifier
{
    Task NotifyMatchAsync(SessionInfo sessionInfo);
}
```

**See:** [Webhook Player Notifier Example](examples.md#webhook-player-notifier)

#### ISessionCreator

**Purpose:** Abstracts session provider modes (create vs find)

**Default Implementations:**
- `EOSSessionCreator` - Creates new EOS sessions (create mode)
- `EOSSessionFinder` - Finds existing EOS sessions (find mode)

**Interface:**
```csharp
public interface ISessionCreator
{
    Task<SessionInfo> GetSessionAsync(Match match);
}
```

**Modes:**
- **Create Mode:** Matchmaker creates new sessions (P2P or on-demand servers)
- **Find Mode:** Matchmaker finds existing sessions (pre-allocated servers)

#### ISessionOwnerNotifier

**Purpose:** Notifies game servers when their session is claimed (find mode only)

**Default:** `StubSessionOwnerNotifier` - Logs to console (must replace for production)

**When to Implement:**
- Using find mode with player-hosted or dedicated servers
- Game servers need to prepare for incoming players

**Interface:**
```csharp
public interface ISessionOwnerNotifier
{
    Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo);
}
```

**Example Mechanisms:**
- UDP packets (recommended for Unreal Engine)
- TCP sockets, HTTP POST, gRPC
- Message queues, WebSocket

**Note:** Only used in find mode

### Infrastructure-Level Extension Points

These interfaces define how the service stores and manages data. Default implementations use in-memory storage suitable for single-instance deployments.

**All default implementations share these limitations:**
- Lost on service restart (no durability)
- Cannot be shared across multiple instances
- Limited by server memory

**Common implementation options:**
- **AccelByte Managed Key-Value Store:** Best for multi-instance deployments (Valkey/Redis-compatible, fully managed)
- **Database:** Best for durability, long-term retention, and complex queries
- **Specialized stores:** Message queues for high throughput, time-series databases for analytics

#### IMatchPool

**Purpose:** Storage for pending match requests

**Default:** `MatchPool` - Thread-safe in-memory storage

**Interface:**
```csharp
public interface IMatchPool
{
    void Add(MatchRequest request);
    bool Remove(string requestId);
    MatchRequest? Get(string requestId);
    MatchRequest? GetByUserId(string userId);
    List<MatchRequest> GetOldest(int count);
    List<MatchRequest> RemoveExpired(TimeSpan timeout);
}
```

**When to customize:** Multi-instance deployments, high availability, large-scale matchmaking

**See:** [Key-Value Store-based MatchPool Example](examples.md#key-value-store-based-matchpool)

#### ICompletedRequestStore

**Purpose:** Storage for completed requests with retention period

**Default:** `CompletedRequestStore` - Thread-safe in-memory storage with automatic expiration

**Interface:**
```csharp
public interface ICompletedRequestStore
{
    void Add(MatchRequest request);
    MatchRequest? Get(string requestId);
    void RemoveExpired(TimeSpan retentionPeriod);
}
```

**When to customize:** Service restart durability, multi-instance deployments, long retention periods, audit requirements

**See:** [Database-backed CompletedRequestStore Example](examples.md#database-backed-completedrequeststore)

#### IClaimedSessionsCache

**Purpose:** Cache for claimed sessions to prevent race conditions (find mode only)

**Default:** `InMemoryClaimedSessionsCache` - Thread-safe in-memory cache with automatic expiration

**Interface:**
```csharp
public interface IClaimedSessionsCache
{
    bool TryAdd(string sessionId, DateTime claimedAt);
    bool Contains(string sessionId);
    void RemoveExpired(TimeSpan expirationTime);
}
```

**When to customize:** Multi-instance deployments in find mode, high concurrency

**Note:** Only used in find mode

---

## Core Customization Points

While extension points allow you to plug in custom implementations, some customizations require modifying the core matching logic directly.

### MatchMaker Customization

**Location:** `Services/MatchMaker.cs`

The `MatchMaker` class implements the core matching algorithm. The default implementation uses a simple FIFO (First-In-First-Out) algorithm that matches the oldest N requests together.

**When to Customize:**
- Skill-based matchmaking (MMR/ELO)
- Region-based matching
- Role-based matching (tank, healer, DPS)
- Keep parties/groups together
- Custom match quality scoring

**How to Customize:**

Modify the `TryMatchAsync()` method directly. The method includes inline comments marking customization points.

**Example: Skill-Based Matching**

```csharp
// Group requests by skill bracket
var skillBrackets = allRequests
    .GroupBy(r => {
        if (r.Metadata?.TryGetValue("mmr", out var mmr) == true)
            return int.Parse(mmr) / 1000; // Bracket size of 1000 MMR
        return 0;
    });

// Match within each bracket
foreach (var bracket in skillBrackets)
{
    var requests = bracket.OrderBy(r => r.CreatedAt).ToList();
    // ... create matches from requests
}
```

**See:** [Skill-Based MatchMaker Example](examples.md#skill-based-matchmaker) for complete implementation

---

## Deployment Considerations

The service can be deployed in different configurations depending on scale, availability, and infrastructure requirements.

### Single-Instance Deployment (Default)

**Characteristics:**
- One matchmaker instance handles all requests
- In-memory storage for match pool and completed requests
- No external dependencies
- Simple to deploy and operate

**Suitable for:**
- Development and testing
- Small-scale production (< 1000 concurrent players)
- When brief downtime during deployments is acceptable

**Limitations:**
- No horizontal scaling
- Data lost on restart
- No high availability

### Multi-Instance Deployment

**Characteristics:**
- Multiple matchmaker instances behind a load balancer
- Distributed storage (AccelByte Managed Key-Value Store, database) for shared state
- External dependencies required
- Suitable for large-scale production deployments

**When needed:**
- High availability (zero downtime)
- > 1000 concurrent players
- Horizontal scaling
- Geographic distribution

**Required Customizations:**

To deploy multiple instances, you must implement distributed versions of:

1. **IMatchPool** - Use AccelByte Managed Key-Value Store or database
2. **ICompletedRequestStore** - Use AccelByte Managed Key-Value Store or database
3. **IClaimedSessionsCache** (find mode only) - Use AccelByte Managed Key-Value Store

**See:** [Implementation Examples](examples.md) for complete code samples

### Deployment Decision Tree

```
Do you need high availability (zero downtime)?
├─ YES → Multi-instance deployment
│   ├─ Implement distributed IMatchPool
│   ├─ Implement distributed ICompletedRequestStore
│   └─ If using find mode: Implement distributed IClaimedSessionsCache
│
└─ NO → Can you tolerate brief downtime during deployments?
    ├─ YES → Single-instance deployment (default)
    │   └─ Use default in-memory implementations
    │
    └─ NO → Multi-instance deployment required
```

### Infrastructure Customization Guidelines

**The key decision:** Are you deploying multiple instances?

**Multi-instance deployment (high availability):**
- **Required:** Implement distributed versions of IMatchPool and ICompletedRequestStore
- **Required (find mode only):** Implement distributed IClaimedSessionsCache
- **Recommended:** Use AccelByte Managed Key-Value Store for all three
- **Alternative:** Use database for IMatchPool and ICompletedRequestStore if you need durability or complex queries

**Single-instance deployment (default):**
- **Keep defaults** unless you have specific needs:
  - Data durability across restarts → Use database
  - Long retention (> 24 hours) → Use database for ICompletedRequestStore
  - Analytics/compliance → Use database or time-series database
  - Very high throughput → Use message queue for IMatchPool

---

## Data Models

### MatchRequest

**Location:** `Model/MatchRequest.cs`

**Fields:**
- `RequestId` (string) - Unique GUID
- `UserId` (string) - User who submitted the request
- `Status` (enum) - Pending, Matched, Expired, Cancelled
- `CreatedAt` (DateTime) - When request was created (UTC)
- `MatchedAt` (DateTime?) - When request was matched (UTC, nullable)
- `SessionId` (string?) - EOS session ID if matched (nullable)
- `Metadata` (Dictionary<string, string>?) - Optional custom metadata

### Match

**Location:** `Model/Match.cs`

**Fields:**
- `MatchId` (string) - Unique GUID
- `Requests` (List<MatchRequest>) - All requests in this match
- `CreatedAt` (DateTime) - When match was created (UTC)

### SessionInfo

**Location:** `Model/SessionInfo.cs`

**Fields:**
- `SessionId` (string) - EOS session ID
- `RequestIds` (List<string>) - All request IDs in the session
- `UserIds` (List<string>) - All user IDs in the session
- `CreatedAt` (DateTime) - When session was created (UTC)

---

## Matching Algorithm

The matcher uses a **FIFO (First-In-First-Out)** algorithm to ensure fairness:

1. **Remove Expired Requests** - Requests older than timeout are removed
2. **Check Pool Size** - If pool has < MatchSize requests, wait for next tick
3. **Get Oldest Requests** - Get oldest MatchSize requests from pool
4. **Create Match** - Remove requests from pool and create Match object
5. **Create Session** - Call SessionCreator to create EOS session
6. **Update Status** - Mark requests as MATCHED with session ID
7. **Notify Players** - Call PlayerNotifier with session info
8. **On Failure** - Return requests to pool and log error

**Configuration:**
- `MatchSize` - Number of players per match (default: 2)
- `TickInterval` - How often matcher runs (default: 1 second)
- `RequestTimeout` - How long before requests expire (default: 60 seconds)

**Customization:** Modify `MatchMaker.TryMatchAsync()` for skill-based, region-based, or role-based matching

---

## Request Lifecycle

### State Diagram

```
PENDING → MATCHED (success)
PENDING → EXPIRED (timeout)
PENDING → CANCELLED (user action)
```

### State Transitions

**PENDING:**
- Initial state when request is submitted
- Request is in the match pool waiting for match
- Can transition to: MATCHED, EXPIRED, CANCELLED

**MATCHED:**
- Request was successfully matched with other players
- EOS session created
- Session ID available
- Terminal state (no further transitions)

**EXPIRED:**
- Request timed out before match was found
- Removed from pool after RequestTimeout
- Terminal state

**CANCELLED:**
- User cancelled the request
- Only possible from PENDING state
- Terminal state

---

## Authorization & Permissions

The service uses AccelByte IAM's permission-based authorization to secure endpoints.

### Required Permissions

Users must have the following permission to access matchmaking endpoints:

```
NAMESPACE:{namespace}:MATCHMAKING [CREATE, READ, DELETE]
```

**Permission Breakdown:**
- `CREATE` - Submit match requests
- `READ` - Get match status
- `DELETE` - Cancel match requests

### Authorization Flow

1. Client sends request with Bearer token in Authorization header
2. AuthorizationInterceptor validates token with AccelByte IAM
3. Interceptor checks user has required permissions
4. If valid, request proceeds to MatchmakingService
5. If invalid, returns Unauthenticated or PermissionDenied error

### Error Responses

**401 Unauthenticated:**
- Missing Authorization header
- Invalid or expired token
- Token validation failed

**403 PermissionDenied:**
- Valid token but missing required permissions
- User doesn't have MATCHMAKING permission for namespace

---

## Observability

### Metrics

The service exposes Prometheus metrics at `:8080/metrics`:

**Request Metrics:**
- `grpc_server_handled_total` - Total requests by endpoint and status
- `grpc_server_handling_seconds` - Request duration histogram

**Matchmaking Metrics:**
- `matchmaking_pool_size` - Current number of pending requests
- `matchmaking_matches_created_total` - Total matches created
- `matchmaking_expired_requests_total` - Total expired requests
- `eos_session_creation_failures_total` - EOS session creation failures

**See:** [Operations Guide](operations.md#observability) for monitoring setup

### Tracing

The service supports OpenTelemetry distributed tracing with Zipkin.

**Configuration:**
```bash
OTEL_EXPORTER_ZIPKIN_ENDPOINT=http://zipkin:9411/api/v2/spans
OTEL_SERVICE_NAME=matchmaking-service
```

**Trace Components:**
- `SubmitMatchRequest` - Request submission
- `MatchPool.Add` - Pool storage
- `MatchMaker.TryMatchAsync` - Matching algorithm
- `SessionCreator.CreateSession` - EOS session creation
- `Notifier.NotifyMatch` - Notification

**See:** [Operations Guide](operations.md#observability) for tracing setup

### Logging

The service uses structured logging with configurable log levels:

**Log Levels:**
- `Debug` - Verbose logging for development
- `Information` - Normal operations (default)
- `Warning` - Recoverable issues
- `Error` - Failures requiring attention

**Configuration:**
```bash
LOG_LEVEL=Information
```

**See:** [Operations Guide](operations.md#observability) for logging details

---

## Implementation Examples

For complete, working code examples of common customizations, see the **[Implementation Examples Guide](examples.md)**.

Available examples:
- **Webhook Player Notifier** - Send HTTP notifications when matches are found
- **Key-Value Store-Based MatchPool** - Distributed match pool for multi-instance deployments
- **Database-Backed CompletedRequestStore** - Persistent storage for completed requests
- **Skill-Based MatchMaker** - Custom matching algorithm with MMR/ELO

---

## Choosing Session Provider Mode

The service supports two modes (configured via `SessionProvider.Mode`):

**Create Mode (Default):** Matchmaker creates new EOS sessions for each match. Use for P2P gameplay or on-demand server allocation.

**Find Mode:** Matchmaker finds existing EOS sessions created by game servers. Use for pre-allocated dedicated servers or player-hosted servers. Requires implementing `ISessionOwnerNotifier` to notify servers when their session is claimed.

**Key Decision:** Who creates the EOS session? If the matchmaker creates it, use create mode. If game servers create it, use find mode.

See [Session Provider Modes](#session-provider-modes) section above for detailed architecture and implementation guidance.

---

## Next Steps

- **Set up the service**: See [Setup Guide](setup.md)
- **Test the service**: See [Testing Guide](testing_guide.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
- **View code examples**: See [Implementation Examples](examples.md)
