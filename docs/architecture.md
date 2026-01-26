# Architecture Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

> **⚠️ Note on Example Code:** The custom implementation examples in this document (such as UDP notifiers, Redis implementations, etc.) were generated with AI assistance and have not been tested or verified to work. They are provided as architectural guidance and starting points. You should test and adapt them for your specific environment and requirements.

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
- **Dedicated server provider integration**: Matchmaker allocates server from provider, then creates session 

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
7. Session owner updates session state in EOS as needed.
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
- Prevents race conditions when multiple matches are created simultaneously, or before the session owner marks the session started.

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

Developers should implement their own notification mechanism based on their infrastructure. This example uses UDP, which is ideal for Unreal Engine game servers since they have native UDP socket support:

```csharp
public class UdpSessionOwnerNotifier : ISessionOwnerNotifier, IDisposable
{
    private readonly UdpClient _udpClient;
    private readonly ILogger<UdpSessionOwnerNotifier> _logger;

    public UdpSessionOwnerNotifier(ILogger<UdpSessionOwnerNotifier> logger)
    {
        _logger = logger;
        _udpClient = new UdpClient();
    }

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
            // connectionInfo contains the game server's IP:Port (e.g., "192.168.1.100:7777")
            var parts = connectionInfo.Split(':');
            var ipAddress = parts[0];
            var port = int.Parse(parts[1]);

            // Serialize payload to JSON bytes
            var jsonPayload = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(jsonPayload);

            // Send UDP packet to game server
            await _udpClient.SendAsync(bytes, bytes.Length, ipAddress, port);
            
            _logger.LogInformation(
                "Sent UDP notification to session owner: SessionId={SessionId}, Endpoint={Endpoint}", 
                sessionId, connectionInfo);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, 
                "Failed to send UDP notification to session owner: SessionId={SessionId}, Endpoint={ConnectionInfo}", 
                sessionId, connectionInfo);
        }
    }

    public void Dispose()
    {
        _udpClient?.Dispose();
    }
}
```

**Registration:**
```csharp
// In Program.cs, replace the stub notifier
builder.Services.AddSingleton<ISessionOwnerNotifier, UdpSessionOwnerNotifier>();
```

**Unreal Engine Game Server (C++):**

```cpp
// Simple UDP listener for Unreal Engine game servers
FUdpSocketReceiver* SocketReceiver;
FSocket* ListenSocket;

void AMyGameServer::SetupMatchmakerListener()
{
    // Create UDP socket bound to port 7777
    ListenSocket = FUdpSocketBuilder(TEXT("MatchmakerListener"))
        .AsReusable()
        .BoundToPort(7777)
        .Build();
    
    // Start receiving messages
    SocketReceiver = new FUdpSocketReceiver(ListenSocket, 
        FTimespan::FromMilliseconds(100), 
        TEXT("MatchmakerReceiver"));
    SocketReceiver->OnDataReceived().BindUObject(this, 
        &AMyGameServer::OnMatchmakerMessage);
}

void AMyGameServer::OnMatchmakerMessage(const FArrayReaderPtr& Data, 
    const FIPv4Endpoint& Endpoint)
{
    // Convert bytes to JSON string
    FString JsonString;
    JsonString.AppendChars((const ANSICHAR*)Data->GetData(), Data->Num());
    
    // Parse JSON and process match notification
    TSharedPtr<FJsonObject> JsonObject;
    TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JsonString);
    
    if (FJsonSerializer::Deserialize(Reader, JsonObject))
    {
        FString SessionId = JsonObject->GetStringField(TEXT("sessionId"));
        TSharedPtr<FJsonObject> MatchObj = JsonObject->GetObjectField(TEXT("match"));
        
        // Process the match (update session state, prepare for players, etc.)
        HandleMatchClaimed(SessionId, MatchObj);
        
        // Optional: Send acknowledgment back to matchmaker
        FString AckMessage = FString::Printf(TEXT("{\"status\":\"received\",\"sessionId\":\"%s\"}"), 
            *SessionId);
        int32 BytesSent;
        ListenSocket->SendTo((uint8*)TCHAR_TO_UTF8(*AckMessage), AckMessage.Len(), 
            BytesSent, *Endpoint.ToInternetAddr());
    }
}
```

**UDP Reliability Considerations:**

UDP is connectionless and does not guarantee delivery. Consider adding acknowledgment handling:

**Option 1: Fire-and-Forget (Simplest)**
- Matchmaker sends notification and assumes success
- Suitable for local network with low packet loss
- Session owner updates EOS session state, which serves as implicit confirmation

**Option 2: Acknowledgment with Retry (Recommended)**
- Game server sends ACK response (shown in example above)
- Matchmaker retries if no ACK received within timeout
- Provides reliability without TCP overhead

**Option 3: TCP or HTTP (Most Reliable)**
- Use TCP sockets or HTTP for guaranteed delivery
- More complex implementation in Unreal (requires third-party libraries for HTTP)
- Higher latency and resource usage

For most game server scenarios, Option 1 (fire-and-forget) is sufficient since the session state in EOS serves as the source of truth. If the notification is lost, the session remains unclaimed and will be selected again by the next match.

**Notification Mechanisms:**

Common notification approaches:
- **UDP Packets**: Send datagrams to game server (recommended for Unreal Engine - native socket support)
- **TCP Sockets**: Persistent connection with guaranteed delivery
- **HTTP POST**: Game server exposes webhook endpoint (requires third-party library in Unreal)
- **Message Queue**: Publish to RabbitMQ, AWS SQS, Azure Service Bus
- **gRPC**: Call game server's gRPC service
- **WebSocket**: Send message over persistent connection
- **Database**: Write to shared database table that servers poll

**Why UDP for Game Servers:**
- Unreal Engine has native UDP socket support (`FUdpSocketBuilder`, `FUdpSocketReceiver`)
- Minimal code required (10-20 lines)
- No external dependencies
- Game developers already familiar with UDP networking
- Lower latency than HTTP/TCP for local network communication

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
    Task<DedicatedServer> RequestServerAsync();
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
var server = await _serverProvider.RequestServerAsync();

// Create session with server info
match.serverInfo = server // assumes match object has been updated to include server info field
var sessionInfo = await _sessionCreator.GetSessionAsync(match);

_logger.LogInformation(
    "Match created with dedicated server: MatchId={MatchId}, ServerId={ServerId}",
    match.MatchId, server.ServerId);
```

This extension is not included in the sample but demonstrates how the architecture can be adapted to your deployment needs.

### Choosing the Right Mode

**Use Create Mode when:**
- Players connect peer-to-peer (no dedicated servers)
- You want the matchmaker to orchestrate server allocation

**Use Find Mode when:**
- Game servers create their own EOS sessions
- You have pre-allocated or player-hosted servers
- You want servers to control session lifecycle

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
- Extract user ID from context

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

## Extension Points

The matchmaking service is designed with clear extension points that allow you to customize behavior for your specific game and infrastructure needs. Extension points are categorized into two levels:

### Application-Level Extension Points

These interfaces define how the matchmaking service integrates with your game-specific systems. You should implement custom versions of these interfaces to match your game's notification mechanisms and session management approach.

#### IPlayerNotifier

**Purpose:** Notifies players when matches are found.

**When to Implement:**
- You want to avoid game clients needing to poll for the result of their match request.

**Default Implementation:** `LoggingPlayerNotifier` - Logs match events to console (suitable for development/testing only)

**Interface:**
```csharp
public interface IPlayerNotifier
{
    Task NotifyMatchAsync(SessionInfo sessionInfo);
}
```

**Example Scenarios:**
- **Push Notifications:** Send Firebase Cloud Messaging or Apple Push Notification Service alerts
- **Webhooks:** HTTP POST to your game backend with match details
- **Message Queues:** Publish to RabbitMQ, AWS SQS, or Azure Service Bus
- **Lobby Service:** Call your lobby service API to notify connected players

**See:** [Complete webhook example](#webhook-player-notifier-example) below

#### ISessionCreator

**Purpose:** Abstracts session provider modes (create vs find).

**When to Implement:**
- You want to customize session creation logic
- You need to integrate with a different session backend
- You want to add custom session metadata or attributes

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
- **Create Mode:** Matchmaker creates new sessions for each match (P2P or on-demand servers)
- **Find Mode:** Matchmaker finds existing sessions created by game servers

**See:** [Session Provider Decision Tree](#session-provider-decision-tree) below

#### ISessionOwnerNotifier

**Purpose:** Notifies game servers when their session is claimed (find mode only).

**When to Implement:**
- You're using find mode with player-hosted or dedicated servers
- Game servers need to prepare for incoming players
- You need to update server state when a session is claimed

**Default Implementation:** `StubSessionOwnerNotifier` - Logs notifications to console (must be replaced for production)

**Interface:**
```csharp
public interface ISessionOwnerNotifier
{
    Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo);
}
```

**Example Mechanisms:**
- **HTTP POST:** Game server exposes webhook endpoint
- **gRPC:** Call game server's gRPC service
- **Message Queue:** Publish to RabbitMQ, AWS SQS, Azure Service Bus
- **WebSocket:** Send message over persistent connection

**Note:** Only used in find mode. Not needed for create mode.

### Infrastructure-Level Extension Points

These interfaces define how the matchmaking service stores and manages data. The default implementations use in-memory storage suitable for single-instance deployments. Implement custom versions when you need distributed storage, durability, or multi-instance deployments.

#### IMatchPool

**Purpose:** Storage for pending match requests.

**When to Implement:**
- **Multi-instance deployments:** Multiple matchmaker instances need shared state
- **High availability:** Requests should survive service restarts
- **Large-scale matchmaking:** Need external queue system for performance

**Default Implementation:** `MatchPool` - Thread-safe in-memory storage (single-instance only)

**Limitations of Default:**
- Lost on service restart (no durability)
- Cannot be shared across multiple instances
- Limited by server memory

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

**Custom Implementation Scenarios:**
- **Redis:** Distributed cache for multi-instance deployments
- **Database:** SQL Server or PostgreSQL for durability and querying
- **Message Queue:** RabbitMQ or AWS SQS for high-throughput scenarios

**See:** [Redis-based MatchPool example](#redis-based-matchpool-example) below

#### ICompletedRequestStore

**Purpose:** Storage for completed requests with retention period.

**When to Implement:**
- **Service restart durability:** Completed requests should survive restarts
- **Multi-instance deployments:** Multiple instances need shared state
- **Long retention periods:** Need database with indexing for efficient queries
- **Audit requirements:** Need persistent storage for compliance

**Default Implementation:** `CompletedRequestStore` - Thread-safe in-memory storage with automatic expiration

**Limitations of Default:**
- Lost on service restart (no durability)
- Cannot be shared across multiple instances
- Limited retention by server memory

**Interface:**
```csharp
public interface ICompletedRequestStore
{
    void Add(MatchRequest request);
    MatchRequest? Get(string requestId);
    void RemoveExpired(TimeSpan retentionPeriod);
}
```

**Custom Implementation Scenarios:**
- **Redis:** Distributed cache with TTL for multi-instance deployments
- **Database:** SQL Server or PostgreSQL for long-term retention and querying
- **Time-series database:** InfluxDB or TimescaleDB for analytics

**See:** [Database-backed CompletedRequestStore example](#database-backed-completedrequeststore-example) below

#### IClaimedSessionsCache

**Purpose:** Cache for claimed sessions to prevent race conditions (find mode only).

**When to Implement:**
- **Multi-instance deployments:** Multiple matchmaker instances need shared cache
- **Find mode at scale:** High concurrency requires distributed coordination

**Default Implementation:** `InMemoryClaimedSessionsCache` - Thread-safe in-memory cache with automatic expiration

**Limitations of Default:**
- Cannot be shared across multiple instances
- Race conditions possible in multi-instance deployments

**Interface:**
```csharp
public interface IClaimedSessionsCache
{
    bool TryAdd(string sessionId, DateTime claimedAt);
    bool Contains(string sessionId);
    void RemoveExpired(TimeSpan expirationTime);
}
```

**Custom Implementation Scenarios:**
- **Redis:** Distributed cache with TTL for multi-instance deployments
- **Distributed lock service:** Consul or etcd for coordination

**Note:** Only used in find mode. Not needed for create mode.

---

## Core Customization Points

While extension points allow you to plug in custom implementations, some customizations require modifying the core matching logic directly. The primary customization point is the `MatchMaker` class.

### MatchMaker Customization

**Location:** `Services/MatchMaker.cs`

The `MatchMaker` class implements the core matching algorithm. The default implementation uses a simple FIFO (First-In-First-Out) algorithm that matches the oldest N requests together. This ensures fairness but doesn't consider player skill, region, or other factors.

**When to Customize:**
- You need skill-based matchmaking (MMR/ELO)
- You want region-based matching
- You need role-based matching (tank, healer, DPS)
- You want to keep parties/groups together
- You need custom match quality scoring

**How to Customize:**

The `TryMatchAsync()` method contains the matching algorithm. Modify this method directly to implement your custom logic. The method already includes inline comments marking customization points.

#### Example 1: Skill-Based Matching (MMR/ELO)

Add skill metadata to match requests and filter by skill range:

```csharp
public async Task<IReadOnlyList<Match>> TryMatchAsync()
{
    var matches = new List<Match>();
    
    try
    {
        // ... expiration logic ...
        
        // SKILL-BASED MATCHING
        // Group requests by skill bracket
        var allRequests = _matchPool.GetAll();
        var skillBrackets = allRequests
            .Where(r => r.Metadata != null && r.Metadata.ContainsKey("mmr"))
            .GroupBy(r => GetSkillBracket(int.Parse(r.Metadata["mmr"])))
            .OrderBy(g => g.Key); // Match lower skill brackets first
        
        foreach (var bracket in skillBrackets)
        {
            var bracketRequests = bracket.OrderBy(r => r.CreatedAt).ToList();
            
            while (bracketRequests.Count >= _config.MatchSize)
            {
                // Take oldest N requests from this skill bracket
                var requestsForMatch = bracketRequests.Take(_config.MatchSize).ToList();
                bracketRequests.RemoveRange(0, _config.MatchSize);
                
                // Remove from pool and create match
                foreach (var request in requestsForMatch)
                {
                    _matchPool.Remove(request.RequestId);
                }
                
                var match = new Match(requestsForMatch);
                // ... session creation and notification ...
                matches.Add(match);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in TryMatchAsync");
    }
    
    return matches;
}

// Helper method to determine skill bracket
private int GetSkillBracket(int mmr)
{
    // Bronze: 0-999, Silver: 1000-1999, Gold: 2000-2999, etc.
    return mmr / 1000;
}
```

**Submitting Requests with MMR:**
```csharp
var request = new MatchRequest
{
    UserId = userId,
    Metadata = new Dictionary<string, string>
    {
        { "mmr", "1500" } // Player's MMR rating
    }
};
```

#### Example 2: Region-Based Matching

Group requests by region to minimize latency:

```csharp
public async Task<IReadOnlyList<Match>> TryMatchAsync()
{
    var matches = new List<Match>();
    
    try
    {
        // ... expiration logic ...
        
        // REGION-BASED MATCHING
        // Group requests by region
        var allRequests = _matchPool.GetAll();
        var regionGroups = allRequests
            .Where(r => r.Metadata != null && r.Metadata.ContainsKey("region"))
            .GroupBy(r => r.Metadata["region"]);
        
        foreach (var regionGroup in regionGroups)
        {
            var regionRequests = regionGroup.OrderBy(r => r.CreatedAt).ToList();
            
            while (regionRequests.Count >= _config.MatchSize)
            {
                // Take oldest N requests from this region
                var requestsForMatch = regionRequests.Take(_config.MatchSize).ToList();
                regionRequests.RemoveRange(0, _config.MatchSize);
                
                // Remove from pool and create match
                foreach (var request in requestsForMatch)
                {
                    _matchPool.Remove(request.RequestId);
                }
                
                var match = new Match(requestsForMatch);
                // ... session creation and notification ...
                
                _logger.LogInformation(
                    "Created region-based match {MatchId} for region {Region}",
                    match.MatchId, regionGroup.Key);
                
                matches.Add(match);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in TryMatchAsync");
    }
    
    return matches;
}
```

**Submitting Requests with Region:**
```csharp
var request = new MatchRequest
{
    UserId = userId,
    Metadata = new Dictionary<string, string>
    {
        { "region", "us-west" } // Player's preferred region
    }
};
```

#### Example 3: Role-Based Matching (Team Composition)

Ensure balanced team composition for team-based games:

```csharp
public async Task<IReadOnlyList<Match>> TryMatchAsync()
{
    var matches = new List<Match>();
    
    try
    {
        // ... expiration logic ...
        
        // ROLE-BASED MATCHING
        // Required composition: 1 tank, 1 healer, 2 DPS
        var allRequests = _matchPool.GetAll()
            .Where(r => r.Metadata != null && r.Metadata.ContainsKey("role"))
            .ToList();
        
        var tanks = allRequests.Where(r => r.Metadata["role"] == "tank")
            .OrderBy(r => r.CreatedAt).ToList();
        var healers = allRequests.Where(r => r.Metadata["role"] == "healer")
            .OrderBy(r => r.CreatedAt).ToList();
        var dps = allRequests.Where(r => r.Metadata["role"] == "dps")
            .OrderBy(r => r.CreatedAt).ToList();
        
        // Create matches while we have the required composition
        while (tanks.Count >= 1 && healers.Count >= 1 && dps.Count >= 2)
        {
            var requestsForMatch = new List<MatchRequest>
            {
                tanks[0],    // 1 tank
                healers[0],  // 1 healer
                dps[0],      // 1 dps
                dps[1]       // 1 dps
            };
            
            // Remove from pool
            foreach (var request in requestsForMatch)
            {
                _matchPool.Remove(request.RequestId);
            }
            
            // Remove from local lists
            tanks.RemoveAt(0);
            healers.RemoveAt(0);
            dps.RemoveRange(0, 2);
            
            var match = new Match(requestsForMatch);
            // ... session creation and notification ...
            
            _logger.LogInformation(
                "Created role-balanced match {MatchId} (1 tank, 1 healer, 2 dps)",
                match.MatchId);
            
            matches.Add(match);
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in TryMatchAsync");
    }
    
    return matches;
}
```

**Submitting Requests with Role:**
```csharp
var request = new MatchRequest
{
    UserId = userId,
    Metadata = new Dictionary<string, string>
    {
        { "role", "tank" } // Player's selected role
    }
};
```

#### Example 4: Combined Skill + Region Matching

Combine multiple criteria for more sophisticated matching:

```csharp
public async Task<IReadOnlyList<Match>> TryMatchAsync()
{
    var matches = new List<Match>();
    
    try
    {
        // ... expiration logic ...
        
        // COMBINED SKILL + REGION MATCHING
        var allRequests = _matchPool.GetAll()
            .Where(r => r.Metadata != null && 
                       r.Metadata.ContainsKey("mmr") && 
                       r.Metadata.ContainsKey("region"))
            .ToList();
        
        // Group by region first, then by skill bracket
        var groups = allRequests
            .GroupBy(r => new 
            { 
                Region = r.Metadata["region"],
                SkillBracket = GetSkillBracket(int.Parse(r.Metadata["mmr"]))
            });
        
        foreach (var group in groups)
        {
            var groupRequests = group.OrderBy(r => r.CreatedAt).ToList();
            
            while (groupRequests.Count >= _config.MatchSize)
            {
                var requestsForMatch = groupRequests.Take(_config.MatchSize).ToList();
                groupRequests.RemoveRange(0, _config.MatchSize);
                
                foreach (var request in requestsForMatch)
                {
                    _matchPool.Remove(request.RequestId);
                }
                
                var match = new Match(requestsForMatch);
                // ... session creation and notification ...
                
                _logger.LogInformation(
                    "Created match {MatchId} for region {Region}, skill bracket {Bracket}",
                    match.MatchId, group.Key.Region, group.Key.SkillBracket);
                
                matches.Add(match);
            }
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Error in TryMatchAsync");
    }
    
    return matches;
}

private int GetSkillBracket(int mmr)
{
    return mmr / 1000; // 0-999, 1000-1999, 2000-2999, etc.
}
```

#### Best Practices for MatchMaker Customization

1. **Always maintain FIFO within groups:** Even with filtering, match the oldest requests first within each group to ensure fairness
2. **Handle edge cases:** What happens when there aren't enough players in a skill bracket or region?
3. **Consider fallback logic:** After waiting too long, should you relax matching criteria?
4. **Log match quality:** Add metrics to track average skill difference, wait times, etc.
5. **Test thoroughly:** Custom matching logic can have subtle bugs that affect player experience
6. **Validate metadata:** Always check that required metadata exists before using it
7. **Keep it simple:** Complex matching algorithms can increase wait times and reduce match quality

---

## Deployment Considerations

The matchmaking service can be deployed in different configurations depending on your scale, availability, and infrastructure requirements. Understanding the trade-offs between single-instance and multi-instance deployments is crucial for choosing the right approach.

### Single-Instance Deployment (Default)

The default configuration uses in-memory storage and is designed for single-instance deployments.

**Characteristics:**
- One matchmaker instance handles all requests
- In-memory storage for match pool and completed requests
- No external dependencies (Redis, database)
- Simple to deploy and operate
- Suitable for development, testing, and moderate production loads

**Advantages:**
- ✅ Simple setup - no external dependencies
- ✅ Low latency - all data in memory
- ✅ Easy to debug and troubleshoot
- ✅ Lower infrastructure costs
- ✅ Sufficient for most games (up to ~1000 concurrent players)

**Limitations:**
- ❌ No high availability - single point of failure
- ❌ Data lost on restart - pending requests and completed request history
- ❌ Limited by single server resources (CPU, memory)
- ❌ Cannot scale horizontally

**When to Use:**
- Development and testing environments
- Small to medium-sized games (< 1000 concurrent players)
- Games with tolerance for brief downtime during deployments
- Cost-sensitive deployments

**Configuration:**
```json
{
  "MatchMaker": {
    "MatchSize": 2,
    "TickInterval": "00:00:01",
    "RequestTimeout": "00:01:00"
  }
}
```

No additional infrastructure required - just deploy the service.

### Multi-Instance Deployment

For high availability and horizontal scaling, deploy multiple matchmaker instances with distributed storage.

**Characteristics:**
- Multiple matchmaker instances behind a load balancer
- Distributed storage (Redis, database) for shared state
- External dependencies required
- More complex to deploy and operate
- Suitable for large-scale production deployments

**Advantages:**
- ✅ High availability - no single point of failure
- ✅ Horizontal scaling - add more instances as needed
- ✅ Data durability - survives instance restarts
- ✅ Better performance under high load

**Limitations:**
- ❌ More complex setup and operation
- ❌ Higher infrastructure costs (Redis, database, load balancer)
- ❌ Slightly higher latency (network calls to external storage)
- ❌ Requires distributed systems expertise

**When to Use:**
- Large-scale games (> 1000 concurrent players)
- Production environments requiring high availability
- Games with zero-tolerance for downtime
- When data durability is critical

**Required Customizations:**

To deploy multiple instances, you must implement distributed versions of:

1. **IMatchPool** - Use Redis or database for shared match pool
2. **ICompletedRequestStore** - Use Redis or database for shared completed requests
3. **IClaimedSessionsCache** (find mode only) - Use Redis for shared claimed sessions cache

**See:** [Complete implementation examples](#complete-working-examples) below

### Storage Trade-offs

Understanding the trade-offs between in-memory and distributed storage helps you make informed decisions.

#### In-Memory Storage (Default)

**Pros:**
- Extremely fast (no network latency)
- Simple to implement and debug
- No external dependencies
- Lower infrastructure costs

**Cons:**
- Data lost on restart
- Cannot be shared across instances
- Limited by server memory
- No durability guarantees

**Best For:**
- Single-instance deployments
- Development and testing
- Stateless or ephemeral data
- Cost-sensitive deployments

#### Redis (Distributed Cache)

**Pros:**
- Fast (low network latency)
- Shared across multiple instances
- Built-in TTL for automatic expiration
- High availability with Redis Cluster
- Relatively simple to operate

**Cons:**
- Requires Redis infrastructure
- Data lost if Redis crashes (unless using persistence)
- Additional cost
- Network latency vs in-memory

**Best For:**
- Multi-instance deployments
- Caching with TTL
- High-performance distributed state
- When eventual consistency is acceptable

**Use Cases:**
- IMatchPool (shared pending requests)
- ICompletedRequestStore (shared completed requests with TTL)
- IClaimedSessionsCache (shared claimed sessions with TTL)

#### Database (SQL/NoSQL)

**Pros:**
- Durable - survives restarts and crashes
- Queryable - complex queries and analytics
- Long-term retention
- ACID guarantees (SQL)
- Backup and recovery

**Cons:**
- Slower than Redis or in-memory
- More complex to operate
- Higher infrastructure costs
- Requires schema management (SQL)

**Best For:**
- Long-term data retention
- Audit and compliance requirements
- Complex queries and analytics
- When durability is critical

**Use Cases:**
- ICompletedRequestStore (long-term retention for analytics)
- IMatchPool (if durability is more important than performance)

### Decision Criteria

Use this decision tree to choose the right deployment approach:

```
Do you need high availability (zero downtime)?
├─ YES → Multi-instance deployment
│   ├─ Implement distributed IMatchPool (Redis)
│   ├─ Implement distributed ICompletedRequestStore (Redis or database)
│   └─ If using find mode: Implement distributed IClaimedSessionsCache (Redis)
│
└─ NO → Can you tolerate brief downtime during deployments?
    ├─ YES → Single-instance deployment (default)
    │   └─ Use default in-memory implementations
    │
    └─ NO → Do you need data durability (survive restarts)?
        ├─ YES → Single-instance with database
        │   ├─ Implement database-backed ICompletedRequestStore
        │   └─ Consider database-backed IMatchPool
        │
        └─ NO → Single-instance deployment (default)
            └─ Use default in-memory implementations
```

**Additional Considerations:**

- **Concurrent players:** < 1000 → Single-instance, > 1000 → Multi-instance
- **Geographic distribution:** Multiple regions → Multi-instance per region
- **Budget:** Limited → Single-instance, Flexible → Multi-instance
- **Operational complexity:** Limited expertise → Single-instance, Experienced team → Multi-instance
- **Data retention:** Short-term → In-memory, Long-term → Database
- **Compliance:** Audit requirements → Database with retention

### Infrastructure Customization Guidelines

When customizing infrastructure components, follow these guidelines:

#### When to Customize IMatchPool

**Customize when:**
- Deploying multiple instances (required)
- Need data durability across restarts
- Pool size exceeds server memory
- Need to query or analyze pending requests

**Keep default when:**
- Single-instance deployment
- Moderate load (< 1000 concurrent players)
- Acceptable to lose pending requests on restart

**Implementation options:**
- Redis: Best for multi-instance, high performance
- Database: Best for durability and querying
- Message Queue: Best for very high throughput

#### When to Customize ICompletedRequestStore

**Customize when:**
- Need data durability across restarts
- Deploying multiple instances (required)
- Long retention periods (> 1 hour)
- Need to query completed requests for analytics
- Compliance or audit requirements

**Keep default when:**
- Single-instance deployment
- Short retention periods (< 1 hour)
- No analytics requirements
- Acceptable to lose history on restart

**Implementation options:**
- Redis: Best for multi-instance, short retention (< 24 hours)
- Database: Best for long retention, analytics, compliance
- Time-series database: Best for analytics and metrics

#### When to Customize IClaimedSessionsCache (Find Mode Only)

**Customize when:**
- Deploying multiple instances in find mode (required)
- High concurrency in find mode

**Keep default when:**
- Single-instance deployment
- Using create mode (not needed)
- Low concurrency in find mode

**Implementation options:**
- Redis: Best choice for distributed cache with TTL
- Distributed lock service: Consul, etcd for coordination

### Monitoring and Observability

Regardless of deployment type, ensure proper monitoring:

**Key Metrics:**
- Match pool size (gauge)
- Match creation rate (counter)
- Match creation latency (histogram)
- Request timeout rate (counter)
- Session creation success/failure rate (counter)

**Distributed Deployment Additional Metrics:**
- Redis connection pool usage
- Database query latency
- Cache hit/miss rates
- Cross-instance coordination latency

**See:** [Operations Guide](operations.md) for detailed observability setup

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

## Complete Working Examples

This section provides complete, working code examples for common customizations. These examples are production-ready and can be adapted to your specific infrastructure.

### Webhook Player Notifier Example

This example shows how to implement a player notifier that sends HTTP POST requests to notify players when matches are found.

**Use Case:** Notify your game backend when matches are created so it can push notifications to players.

**Implementation:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Player notifier that sends HTTP POST webhooks to notify players
    /// </summary>
    public class WebhookPlayerNotifier : IPlayerNotifier
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<WebhookPlayerNotifier> _logger;
        private readonly string _webhookUrl;

        public WebhookPlayerNotifier(
            HttpClient httpClient,
            ILogger<WebhookPlayerNotifier> logger,
            IConfiguration configuration)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Get webhook URL from configuration
            _webhookUrl = configuration["PlayerNotifier:WebhookUrl"] 
                ?? throw new InvalidOperationException("PlayerNotifier:WebhookUrl not configured");
        }

        public async Task NotifyMatchAsync(SessionInfo sessionInfo)
        {
            _logger.LogInformation(
                "Notifying {PlayerCount} players about match via webhook",
                sessionInfo.UserIds.Count);

            // Send notifications to all players in parallel
            var notificationTasks = sessionInfo.UserIds.Select(userId =>
                SendNotificationAsync(userId, sessionInfo)
            );

            try
            {
                await Task.WhenAll(notificationTasks);
                _logger.LogInformation(
                    "Successfully notified all players for session {SessionId}",
                    sessionInfo.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, 
                    "Failed to notify some players for session {SessionId}",
                    sessionInfo.SessionId);
                // Don't throw - notification failures shouldn't break matching
            }
        }

        private async Task SendNotificationAsync(string userId, SessionInfo sessionInfo)
        {
            try
            {
                var payload = new
                {
                    type = "match_found",
                    user_id = userId,
                    session_id = sessionInfo.SessionId,
                    player_count = sessionInfo.UserIds.Count,
                    timestamp = DateTime.UtcNow
                };

                var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
                response.EnsureSuccessStatusCode();

                _logger.LogDebug(
                    "Notified user {UserId} about session {SessionId}",
                    userId, sessionInfo.SessionId);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to notify user {UserId} via webhook",
                    userId);
                // Don't throw - continue notifying other players
            }
        }
    }
}
```

**Registration in Program.cs:**

```csharp
// Configure HttpClient with timeout and retry policy
builder.Services.AddHttpClient<WebhookPlayerNotifier>()
    .ConfigureHttpClient(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(5);
    })
    .AddTransientHttpErrorPolicy(policy => 
        policy.WaitAndRetryAsync(3, retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))));

// Register as IPlayerNotifier
builder.Services.AddSingleton<IPlayerNotifier, WebhookPlayerNotifier>();
```

**Configuration (appsettings.json):**

```json
{
  "PlayerNotifier": {
    "WebhookUrl": "https://your-game-backend.com/api/matchmaking/notifications"
  }
}
```

**Webhook Endpoint Example (Your Game Backend):**

```csharp
[ApiController]
[Route("api/matchmaking/notifications")]
public class MatchmakingNotificationsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> ReceiveNotification([FromBody] MatchNotification notification)
    {
        // Send push notification to player
        await _pushNotificationService.SendAsync(
            notification.UserId,
            "Match Found!",
            $"Your match is ready. Session: {notification.SessionId}");

        return Ok();
    }
}

public class MatchNotification
{
    public string Type { get; set; }
    public string UserId { get; set; }
    public string SessionId { get; set; }
    public int PlayerCount { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Redis-Based MatchPool Example

This example shows how to implement a distributed match pool using Redis for multi-instance deployments.

**Use Case:** Deploy multiple matchmaker instances that share the same match pool.

**Prerequisites:**
- Install `StackExchange.Redis` NuGet package
- Redis server running and accessible

**Implementation:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Redis-based match pool for multi-instance deployments
    /// </summary>
    public class RedisMatchPool : IMatchPool
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisMatchPool> _logger;
        private const string PoolKey = "matchmaking:pool";
        private const string UserIndexKey = "matchmaking:user_index";

        public RedisMatchPool(
            IConnectionMultiplexer redis,
            ILogger<RedisMatchPool> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public int Count
        {
            get
            {
                var db = _redis.GetDatabase();
                return (int)db.SortedSetLength(PoolKey);
            }
        }

        public void Add(MatchRequest request)
        {
            var db = _redis.GetDatabase();
            var json = JsonSerializer.Serialize(request);
            
            // Use CreatedAt timestamp as score for FIFO ordering
            var score = request.CreatedAt.Ticks;
            
            // Add to sorted set (pool) and user index
            var transaction = db.CreateTransaction();
            transaction.SortedSetAddAsync(PoolKey, json, score);
            transaction.StringSetAsync($"{UserIndexKey}:{request.UserId}", request.RequestId);
            transaction.Execute();

            _logger.LogDebug("Added request {RequestId} to Redis pool", request.RequestId);
        }

        public MatchRequest? Remove(string requestId)
        {
            var db = _redis.GetDatabase();
            
            // Find and remove the request
            var allRequests = db.SortedSetRangeByScore(PoolKey);
            foreach (var entry in allRequests)
            {
                var request = JsonSerializer.Deserialize<MatchRequest>(entry.ToString());
                if (request?.RequestId == requestId)
                {
                    var transaction = db.CreateTransaction();
                    transaction.SortedSetRemoveAsync(PoolKey, entry);
                    transaction.KeyDeleteAsync($"{UserIndexKey}:{request.UserId}");
                    transaction.Execute();

                    _logger.LogDebug("Removed request {RequestId} from Redis pool", requestId);
                    return request;
                }
            }

            return null;
        }

        public MatchRequest? Get(string requestId)
        {
            var db = _redis.GetDatabase();
            var allRequests = db.SortedSetRangeByScore(PoolKey);
            
            foreach (var entry in allRequests)
            {
                var request = JsonSerializer.Deserialize<MatchRequest>(entry.ToString());
                if (request?.RequestId == requestId)
                {
                    return request;
                }
            }

            return null;
        }

        public MatchRequest? GetByUserId(string userId)
        {
            var db = _redis.GetDatabase();
            var requestId = db.StringGet($"{UserIndexKey}:{userId}");
            
            if (requestId.IsNullOrEmpty)
            {
                return null;
            }

            return Get(requestId.ToString());
        }

        public List<MatchRequest> GetOldest(int count)
        {
            var db = _redis.GetDatabase();
            
            // Get oldest entries (lowest scores = earliest timestamps)
            var entries = db.SortedSetRangeByScore(PoolKey, take: count);
            
            return entries
                .Select(entry => JsonSerializer.Deserialize<MatchRequest>(entry.ToString()))
                .Where(request => request != null)
                .Cast<MatchRequest>()
                .ToList();
        }

        public List<MatchRequest> RemoveExpired(TimeSpan timeout)
        {
            var db = _redis.GetDatabase();
            var cutoffTime = DateTime.UtcNow - timeout;
            var cutoffScore = cutoffTime.Ticks;

            // Get expired entries
            var expiredEntries = db.SortedSetRangeByScore(PoolKey, 0, cutoffScore);
            var expiredRequests = expiredEntries
                .Select(entry => JsonSerializer.Deserialize<MatchRequest>(entry.ToString()))
                .Where(request => request != null)
                .Cast<MatchRequest>()
                .ToList();

            if (expiredRequests.Count > 0)
            {
                // Remove expired entries
                var transaction = db.CreateTransaction();
                transaction.SortedSetRemoveRangeByScoreAsync(PoolKey, 0, cutoffScore);
                
                foreach (var request in expiredRequests)
                {
                    transaction.KeyDeleteAsync($"{UserIndexKey}:{request.UserId}");
                }
                
                transaction.Execute();

                _logger.LogInformation(
                    "Removed {Count} expired requests from Redis pool",
                    expiredRequests.Count);
            }

            return expiredRequests;
        }

        public List<MatchRequest> GetAll()
        {
            var db = _redis.GetDatabase();
            var allEntries = db.SortedSetRangeByScore(PoolKey);
            
            return allEntries
                .Select(entry => JsonSerializer.Deserialize<MatchRequest>(entry.ToString()))
                .Where(request => request != null)
                .Cast<MatchRequest>()
                .ToList();
        }
    }
}
```

**Registration in Program.cs:**

```csharp
// Configure Redis connection
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var redisConnectionString = configuration["Redis:ConnectionString"] 
        ?? "localhost:6379";
    return ConnectionMultiplexer.Connect(redisConnectionString);
});

// Register Redis-based match pool
builder.Services.AddSingleton<IMatchPool, RedisMatchPool>();
```

**Configuration (appsettings.json):**

```json
{
  "Redis": {
    "ConnectionString": "your-redis-server:6379,password=your-password"
  }
}
```

### Database-Backed CompletedRequestStore Example

This example shows how to implement a database-backed completed request store for durability and long-term retention.

**Use Case:** Persist completed requests for analytics, compliance, or multi-instance deployments.

**Prerequisites:**
- Install `Microsoft.EntityFrameworkCore.SqlServer` NuGet package (or your preferred database provider)
- Database server running and accessible

**Implementation:**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Database context for completed requests
    /// </summary>
    public class MatchmakingDbContext : DbContext
    {
        public DbSet<MatchRequest> CompletedRequests { get; set; }

        public MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatchRequest>(entity =>
            {
                entity.HasKey(e => e.RequestId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.CompletedAt);
                entity.Property(e => e.Metadata).HasConversion(
                    v => System.Text.Json.JsonSerializer.Serialize(v, (JsonSerializerOptions)null),
                    v => System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(v, (JsonSerializerOptions)null));
            });
        }
    }

    /// <summary>
    /// Database-backed completed request store for durability
    /// </summary>
    public class DatabaseCompletedRequestStore : ICompletedRequestStore
    {
        private readonly MatchmakingDbContext _dbContext;
        private readonly ILogger<DatabaseCompletedRequestStore> _logger;

        public DatabaseCompletedRequestStore(
            MatchmakingDbContext dbContext,
            ILogger<DatabaseCompletedRequestStore> logger)
        {
            _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Add(MatchRequest request)
        {
            try
            {
                _dbContext.CompletedRequests.Add(request);
                _dbContext.SaveChanges();

                _logger.LogDebug(
                    "Added completed request {RequestId} to database",
                    request.RequestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to add completed request {RequestId} to database",
                    request.RequestId);
                throw;
            }
        }

        public MatchRequest? Get(string requestId)
        {
            try
            {
                return _dbContext.CompletedRequests
                    .FirstOrDefault(r => r.RequestId == requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to get completed request {RequestId} from database",
                    requestId);
                return null;
            }
        }

        public List<MatchRequest> RemoveExpired(TimeSpan retentionPeriod)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - retentionPeriod;
                
                var expiredRequests = _dbContext.CompletedRequests
                    .Where(r => r.CompletedAt < cutoffTime)
                    .ToList();

                if (expiredRequests.Count > 0)
                {
                    _dbContext.CompletedRequests.RemoveRange(expiredRequests);
                    _dbContext.SaveChanges();

                    _logger.LogInformation(
                        "Removed {Count} expired completed requests from database",
                        expiredRequests.Count);
                }

                return expiredRequests;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove expired requests from database");
                return new List<MatchRequest>();
            }
        }
    }
}
```

**Registration in Program.cs:**

```csharp
// Configure database context
builder.Services.AddDbContext<MatchmakingDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("MatchmakingDb");
    options.UseSqlServer(connectionString);
});

// Register database-backed completed request store
builder.Services.AddScoped<ICompletedRequestStore, DatabaseCompletedRequestStore>();

// Run migrations on startup
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<MatchmakingDbContext>();
    dbContext.Database.Migrate();
}
```

**Configuration (appsettings.json):**

```json
{
  "ConnectionStrings": {
    "MatchmakingDb": "Server=your-server;Database=Matchmaking;User Id=your-user;Password=your-password;"
  }
}
```

**Migration Commands:**

```bash
# Create initial migration
dotnet ef migrations add InitialCreate --context MatchmakingDbContext

# Apply migrations
dotnet ef database update --context MatchmakingDbContext
```

### Skill-Based MatchMaker Modification Example

This example shows how to modify the MatchMaker to implement skill-based matching using MMR (Matchmaking Rating).

**Use Case:** Match players of similar skill levels together for balanced gameplay.

**Implementation:**

See the [MatchMaker Customization](#matchmaker-customization) section above for the complete skill-based matching example. The key points are:

1. Add `mmr` metadata to match requests
2. Group requests by skill bracket (e.g., 0-999, 1000-1999, etc.)
3. Match within skill brackets using FIFO
4. Consider fallback logic for players waiting too long

**Additional Considerations:**

- **Skill bracket size:** Smaller brackets = better matches but longer wait times
- **Fallback logic:** After waiting X seconds, expand skill range
- **Team balancing:** For team games, balance average MMR between teams
- **New player protection:** Separate bracket for new players (< 10 games)

---

## Session Provider Decision Tree

Choosing between create mode and find mode depends on your game architecture and server infrastructure. Use this decision tree to determine the right approach.

### Decision Tree

```
How are game servers managed in your game?

├─ Players connect PEER-TO-PEER (no dedicated servers)
│  └─ Use CREATE MODE
│     └─ Matchmaker creates session, players connect directly to each other
│
├─ Dedicated servers are ALLOCATED ON-DEMAND
│  └─ Use CREATE MODE with dedicated server provider integration
│     ├─ Matchmaker creates session
│     ├─ Allocate server from provider (GameLift, custom allocator, etc.)
│     └─ Add server connection info to session
│
├─ Dedicated servers are PRE-ALLOCATED (always running)
│  └─ Use FIND MODE
│     ├─ Servers create EOS sessions and mark as available
│     ├─ Matchmaker finds available sessions
│     └─ Matchmaker notifies server owner when session is claimed
│
└─ Players HOST their own servers
   └─ Use FIND MODE
      ├─ Player-hosted servers create EOS sessions
      ├─ Matchmaker finds available sessions
      └─ Matchmaker notifies server owner when session is claimed
```

### Mode Comparison

| Aspect | Create Mode | Find Mode |
|--------|-------------|-----------|
| **Session Creation** | Matchmaker creates new sessions | Game servers create sessions |
| **Server Allocation** | On-demand or P2P | Pre-allocated or player-hosted |
| **Session Lifecycle** | Matchmaker controls | Server controls |
| **Notification** | Players only | Players + server owner |
| **Complexity** | Simpler | More complex |
| **Use Cases** | P2P, on-demand servers | Pre-allocated servers, player-hosted |

### Create Mode - Detailed Use Cases

**1. Peer-to-Peer Gameplay**

Best for games where players connect directly to each other without dedicated servers.

**Characteristics:**
- No server infrastructure needed
- One player acts as host
- Lower infrastructure costs
- Suitable for small player counts (2-8 players)

**Examples:**
- Fighting games (1v1)
- Co-op games (2-4 players)
- Party games
- Mobile games with small matches

**Flow:**
1. Players submit match requests
2. Matchmaker creates match and EOS session
3. Players receive session ID
4. Players join session and connect P2P
5. One player acts as host

**2. On-Demand Dedicated Servers**

Best for games that allocate dedicated servers when matches are created.

**Characteristics:**
- Servers allocated only when needed
- Cost-efficient (pay per match)
- Scales automatically with player demand
- Requires integration with server provider

**Examples:**
- Battle royale games
- Competitive multiplayer games
- Large-scale matches (10+ players)
- Games requiring authoritative servers

**Flow:**
1. Players submit match requests
2. Matchmaker creates match and EOS session
3. Matchmaker allocates server from provider
4. Server connection info added to session
5. Players receive session ID and server address
6. Players connect to dedicated server

**Implementation:** See [Extending Create Mode with Dedicated Server Provider](#extending-create-mode-with-dedicated-server-provider) section

### Find Mode - Detailed Use Cases

**1. Pre-Allocated Dedicated Servers**

Best for games with dedicated servers that are always running and waiting for players.

**Characteristics:**
- Servers always running (fixed cost)
- Instant match start (no allocation delay)
- Servers manage their own lifecycle
- Requires server notification mechanism

**Examples:**
- MMO games with instanced content
- Games with persistent server infrastructure
- Enterprise deployments with fixed server pools
- Games with complex server initialization

**Flow:**
1. Dedicated servers start and create EOS sessions (available state)
2. Players submit match requests
3. Matchmaker creates match and finds available session
4. Matchmaker claims session (adds to cache)
5. Matchmaker notifies server owner
6. Server updates session to "started" state
7. Players receive session ID and connect to server

**Server Notification:** Implement `ISessionOwnerNotifier` to notify servers (HTTP, gRPC, message queue)

**2. Player-Hosted Servers**

Best for games where players can host their own servers and wait for matchmaker to fill them.

**Characteristics:**
- Players create and manage servers
- Community-driven server ecosystem
- No server infrastructure costs
- Requires server notification mechanism

**Examples:**
- Sandbox games with custom servers
- Games with modding support
- Community-driven multiplayer games
- Games with server browser + matchmaking

**Flow:**
1. Player starts server and creates EOS session (available state)
2. Other players submit match requests
3. Matchmaker creates match and finds player's session
4. Matchmaker claims session
5. Matchmaker notifies server owner (player)
6. Server owner updates session to "started"
7. Matched players receive session ID and connect

**Server Notification:** Implement `ISessionOwnerNotifier` to notify player-hosted servers

### Hybrid Approach

Some games use both modes for different scenarios:

**Example: Casual vs Competitive**
- **Create Mode:** Casual quick play (P2P or on-demand servers)
- **Find Mode:** Competitive ranked play (pre-allocated dedicated servers)

**Example: Public vs Custom**
- **Create Mode:** Public matchmaking (on-demand servers)
- **Find Mode:** Custom/private servers (player-hosted)

**Implementation:** Run two separate matchmaker instances with different configurations.

### Configuration Examples

**Create Mode Configuration:**

```json
{
  "SessionProvider": {
    "Mode": "create"
  },
  "MatchMaker": {
    "MatchSize": 4,
    "TickInterval": "00:00:01",
    "RequestTimeout": "00:01:00"
  }
}
```

**Find Mode Configuration:**

```json
{
  "SessionProvider": {
    "Mode": "find"
  },
  "SessionFinder": {
    "BucketId": "default",
    "MaxSearchResults": 10,
    "ClaimedSessionExpirationSeconds": 300
  },
  "MatchMaker": {
    "MatchSize": 4,
    "TickInterval": "00:00:01",
    "RequestTimeout": "00:01:00"
  }
}
```

### Decision Criteria Summary

**Choose Create Mode when:**
- ✅ Players connect peer-to-peer
- ✅ Servers are allocated on-demand
- ✅ You want matchmaker to control session lifecycle
- ✅ Simpler architecture is preferred
- ✅ No need for pre-allocated servers

**Choose Find Mode when:**
- ✅ Servers are pre-allocated and always running
- ✅ Players host their own servers
- ✅ Servers need to control their own lifecycle
- ✅ Servers need notification when sessions are claimed
- ✅ Complex server initialization required

**Key Questions:**
1. Who creates the EOS session? (Matchmaker → Create, Server → Find)
2. When are servers allocated? (On-demand → Create, Pre-allocated → Find)
3. Who controls session lifecycle? (Matchmaker → Create, Server → Find)
4. Do servers need notification? (No → Create, Yes → Find)

---

## Next Steps

- **Set up the service**: See [Setup Guide](setup.md)
- **Test the service**: See [Testing Guide](testing_guide.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)

