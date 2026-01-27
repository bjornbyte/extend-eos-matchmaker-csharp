# Architecture Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

> **⚠️ Note on Example Code:** The custom implementation examples in this document (such as UDP notifiers, Key-Value Store implementations, etc.) were generated with AI assistance and have not been tested or verified to work. They are provided as architectural guidance and starting points. You should test and adapt them for your specific environment and requirements.

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
- **UDP Packets:** Send datagrams to game server (recommended for Unreal Engine - native socket support)
- **TCP Sockets:** Persistent connection with guaranteed delivery
- **HTTP POST:** Game server exposes webhook endpoint (requires third-party library in Unreal)
- **gRPC:** Call game server's gRPC service
- **Message Queue:** Publish to RabbitMQ, AWS SQS, Azure Service Bus
- **WebSocket:** Send message over persistent connection

**Note:** Only used in find mode. Not needed for create mode.

### Infrastructure-Level Extension Points

These interfaces define how the matchmaking service stores and manages data. The default implementations use in-memory storage suitable for single-instance deployments. Implement custom versions when you need distributed storage, durability, or multi-instance deployments.

**All default implementations share these limitations:**
- Lost on service restart (no durability)
- Cannot be shared across multiple instances
- Limited by server memory

**Common implementation options:**
- **AccelByte Managed Key-Value Store:** Best for multi-instance deployments (Valkey/Redis-compatible, fully managed)
- **Database:** Best for durability, long-term retention, and complex queries
- **Specialized stores:** Message queues for high throughput, time-series databases for analytics

#### IMatchPool

**Purpose:** Storage for pending match requests.

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

**See:** [Key-Value Store-based MatchPool example](#key-value-store-based-matchpool-example) below

#### ICompletedRequestStore

**Purpose:** Storage for completed requests with retention period.

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

**See:** [Database-backed CompletedRequestStore example](#database-backed-completedrequeststore-example) below

#### IClaimedSessionsCache

**Purpose:** Cache for claimed sessions to prevent race conditions (find mode only).

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
- No external dependencies (Key-Value Store, database)
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
- Distributed storage (AccelByte Managed Key-Value Store, database) for shared state
- External dependencies required
- Suitable for large-scale production deployments

**Advantages:**
- ✅ High availability - no single point of failure
- ✅ Horizontal scaling - add more instances as needed
- ✅ Data durability - survives instance restarts
- ✅ Better performance under high load

**Limitations:**
- ❌ More complex to reason about
- ❌ Higher infrastructure costs (Key-Value Store, database)
- ❌ Slightly higher latency (network calls to external storage)
- ❌ Requires distributed systems understanding

**When to Use:**
- Large-scale games (> 1000 concurrent players)
- Production environments requiring high availability
- Games with zero-tolerance for downtime
- When data durability is critical

**Required Customizations:**

To deploy multiple instances, you must implement distributed versions of:

1. **IMatchPool** - Use AccelByte Managed Key-Value Store or database for shared match pool
2. **ICompletedRequestStore** - Use AccelByte Managed Key-Value Store or database for shared completed requests
3. **IClaimedSessionsCache** (find mode only) - Use AccelByte Managed Key-Value Store for shared claimed sessions cache

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

#### AccelByte Managed Key-Value Store (Distributed Cache)

**Pros:**
- Fully managed by AccelByte (no infrastructure to maintain)
- Shared across multiple instances
- Built-in TTL for automatic expiration
- High availability with Valkey (Redis-compatible)
- Integrated with AccelByte Extend platform
- Relatively simple to operate
- Fast access with low latency

**Cons:**
- Additional cost
- Network latency vs in-memory

**Best For:**
- Multi-instance deployments on AccelByte Extend
- Caching with TTL
- High-performance distributed state
- When eventual consistency is acceptable
- Teams already using AccelByte platform

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
- Slower than Key-Value Store or in-memory
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
│   ├─ Implement distributed IMatchPool (AccelByte Managed Key-Value Store)
│   ├─ Implement distributed ICompletedRequestStore (AccelByte Managed Key-Value Store or database)
│   └─ If using find mode: Implement distributed IClaimedSessionsCache (AccelByte Managed Key-Value Store)
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

### Monitoring and Observability

Regardless of deployment type, ensure proper monitoring:

**Key Metrics:**
- Match pool size (gauge)
- Match creation rate (counter)
- Match creation latency (histogram)
- Request timeout rate (counter)
- Session creation success/failure rate (counter)

**Distributed Deployment Additional Metrics:**
- Key-Value Store connection pool usage
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

## Implementation Examples

For complete, working code examples of common customizations, see the **[Implementation Examples Guide](examples.md)**.

Available examples:
- **Webhook Player Notifier** - Send HTTP notifications when matches are found
- **Key-Value Store-Based MatchPool** - Distributed match pool for multi-instance deployments
- **Database-Backed CompletedRequestStore** - Persistent storage for completed requests
- **Skill-Based MatchMaker** - Custom matching algorithm with MMR/ELO

---

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

