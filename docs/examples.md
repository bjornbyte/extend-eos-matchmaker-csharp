# Implementation Examples

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md) | **Examples**

---

> **⚠️ Note:** These code examples were generated with AI assistance and have not been tested or verified to work. They are provided as starting points. You should test and adapt them for your specific environment and requirements.

---

## Table of Contents

- [Webhook Player Notifier](#webhook-player-notifier)
- [Key-Value Store-Based MatchPool](#key-value-store-based-matchpool)
- [Database-Backed CompletedRequestStore](#database-backed-completedrequeststore)
- [Skill-Based MatchMaker](#skill-based-matchmaker)

---

## Webhook Player Notifier

**Use Case:** Notify your game backend when matches are created so it can push notifications to players.

**Extension Point:** `IPlayerNotifier`

### Implementation

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
            _webhookUrl = configuration["PlayerNotifier:WebhookUrl"] 
                ?? throw new InvalidOperationException("PlayerNotifier:WebhookUrl not configured");
        }
        public async Task NotifyMatchAsync(SessionInfo sessionInfo)
        {
            try
            {
                var payload = new
                {
                    session_id = sessionInfo.SessionId,
                    user_ids = sessionInfo.UserIds,
                    request_ids = sessionInfo.RequestIds,
                    created_at = sessionInfo.CreatedAt
                };

                var response = await _httpClient.PostAsJsonAsync(_webhookUrl, payload);
                response.EnsureSuccessStatusCode();

                _logger.LogInformation(
                    "Notified players via webhook for session {SessionId}",
                    sessionInfo.SessionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to notify players via webhook for session {SessionId}",
                    sessionInfo.SessionId);
                throw;
            }
        }
    }
}
```

### Registration

```csharp
// In Program.cs
builder.Services.AddHttpClient<IPlayerNotifier, WebhookPlayerNotifier>();
```

### Configuration

```bash
# Environment variable
PLAYERNOTIFIER__WEBHOOKURL=https://your-backend.com/api/match-notifications

# Or in appsettings.json
{
  "PlayerNotifier": {
    "WebhookUrl": "https://your-backend.com/api/match-notifications"
  }
}
```

### Webhook Payload

```json
{
  "session_id": "550e8400-e29b-41d4-a716-446655440000",
  "user_ids": ["user1", "user2"],
  "request_ids": ["req1", "req2"],
  "created_at": "2026-01-26T10:30:00Z"
}
```

---

## Key-Value Store-Based MatchPool

**Use Case:** Multi-instance deployment requiring shared match pool state across multiple matchmaker instances.

**Extension Point:** `IMatchPool`

**Recommended Store:** AccelByte Managed Key-Value Store (Valkey/Redis-compatible)

### Implementation

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
    public class RedisMatchPool : IMatchPool
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly ILogger<RedisMatchPool> _logger;
        private readonly string _keyPrefix = "matchpool:";

        public RedisMatchPool(
            IConnectionMultiplexer redis,
            ILogger<RedisMatchPool> logger)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Add(MatchRequest request)
        {
            var db = _redis.GetDatabase();
            var key = $"{_keyPrefix}{request.RequestId}";
            var userKey = $"{_keyPrefix}user:{request.UserId}";
            var listKey = $"{_keyPrefix}list";

            var json = JsonSerializer.Serialize(request);

            // Use transaction to ensure atomicity
            var transaction = db.CreateTransaction();
            transaction.StringSetAsync(key, json);
            transaction.StringSetAsync(userKey, request.RequestId);
            transaction.ListRightPushAsync(listKey, request.RequestId);
            
            if (!transaction.Execute())
            {
                throw new InvalidOperationException("Failed to add request to Redis");
            }

            _logger.LogDebug("Added request {RequestId} to Redis pool", request.RequestId);
        }

        public bool Remove(string requestId)
        {
            var db = _redis.GetDatabase();
            var key = $"{_keyPrefix}{requestId}";
            var listKey = $"{_keyPrefix}list";

            // Get request to find user ID
            var json = db.StringGet(key);
            if (json.IsNullOrEmpty)
                return false;

            var request = JsonSerializer.Deserialize<MatchRequest>(json!);
            if (request == null)
                return false;

            var userKey = $"{_keyPrefix}user:{request.UserId}";

            // Use transaction
            var transaction = db.CreateTransaction();
            transaction.KeyDeleteAsync(key);
            transaction.KeyDeleteAsync(userKey);
            transaction.ListRemoveAsync(listKey, requestId);

            var success = transaction.Execute();
            
            if (success)
            {
                _logger.LogDebug("Removed request {RequestId} from Redis pool", requestId);
            }

            return success;
        }

        public MatchRequest? Get(string requestId)
        {
            var db = _redis.GetDatabase();
            var key = $"{_keyPrefix}{requestId}";
            var json = db.StringGet(key);

            if (json.IsNullOrEmpty)
                return null;

            return JsonSerializer.Deserialize<MatchRequest>(json!);
        }

        public MatchRequest? GetByUserId(string userId)
        {
            var db = _redis.GetDatabase();
            var userKey = $"{_keyPrefix}user:{userId}";
            var requestId = db.StringGet(userKey);

            if (requestId.IsNullOrEmpty)
                return null;

            return Get(requestId!);
        }

        public List<MatchRequest> GetOldest(int count)
        {
            var db = _redis.GetDatabase();
            var listKey = $"{_keyPrefix}list";

            // Get oldest N request IDs from list
            var requestIds = db.ListRange(listKey, 0, count - 1);

            var requests = new List<MatchRequest>();
            foreach (var requestId in requestIds)
            {
                var request = Get(requestId!);
                if (request != null)
                {
                    requests.Add(request);
                }
            }

            return requests.OrderBy(r => r.CreatedAt).ToList();
        }

        public List<MatchRequest> RemoveExpired(TimeSpan timeout)
        {
            var db = _redis.GetDatabase();
            var listKey = $"{_keyPrefix}list";
            var cutoffTime = DateTime.UtcNow - timeout;

            // Get all request IDs
            var requestIds = db.ListRange(listKey);
            var expiredRequests = new List<MatchRequest>();

            foreach (var requestId in requestIds)
            {
                var request = Get(requestId!);
                if (request != null && request.CreatedAt < cutoffTime)
                {
                    expiredRequests.Add(request);
                    Remove(request.RequestId);
                }
            }

            _logger.LogInformation("Removed {Count} expired requests from Redis pool", expiredRequests.Count);
            return expiredRequests;
        }
    }
}
```

### Registration

```csharp
// In Program.cs
builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var configuration = sp.GetRequiredService<IConfiguration>();
    var connectionString = configuration["Redis:ConnectionString"] 
        ?? throw new InvalidOperationException("Redis:ConnectionString not configured");
    return ConnectionMultiplexer.Connect(connectionString);
});

builder.Services.AddSingleton<IMatchPool, RedisMatchPool>();
```

### Configuration

```bash
# Environment variable
REDIS__CONNECTIONSTRING=your-redis-host:6379,password=your-password

# Or in appsettings.json
{
  "Redis": {
    "ConnectionString": "your-redis-host:6379,password=your-password"
  }
}
```

### Dependencies

Add to your `.csproj`:

```xml
<PackageReference Include="StackExchange.Redis" Version="2.7.10" />
```

---

## Database-Backed CompletedRequestStore

**Use Case:** Persist completed requests across service restarts, support long retention periods, enable audit trails.

**Extension Point:** `ICompletedRequestStore`

**Recommended Database:** PostgreSQL, MySQL, or SQL Server

### Implementation

```csharp
using System;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    // DbContext
    public class MatchmakingDbContext : DbContext
    {
        public MatchmakingDbContext(DbContextOptions<MatchmakingDbContext> options)
            : base(options)
        {
        }

        public DbSet<MatchRequest> CompletedRequests { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<MatchRequest>(entity =>
            {
                entity.HasKey(e => e.RequestId);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.MatchedAt);
                entity.Property(e => e.Status).HasConversion<string>();
            });
        }
    }

    // Store implementation
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
                throw;
            }
        }

        public void RemoveExpired(TimeSpan retentionPeriod)
        {
            try
            {
                var cutoffTime = DateTime.UtcNow - retentionPeriod;

                var expiredRequests = _dbContext.CompletedRequests
                    .Where(r => r.MatchedAt != null && r.MatchedAt < cutoffTime)
                    .ToList();

                if (expiredRequests.Any())
                {
                    _dbContext.CompletedRequests.RemoveRange(expiredRequests);
                    _dbContext.SaveChanges();

                    _logger.LogInformation(
                        "Removed {Count} expired requests from database",
                        expiredRequests.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to remove expired requests from database");
                throw;
            }
        }
    }
}
```

### Registration

```csharp
// In Program.cs
builder.Services.AddDbContext<MatchmakingDbContext>(options =>
{
    var connectionString = builder.Configuration["Database:ConnectionString"]
        ?? throw new InvalidOperationException("Database:ConnectionString not configured");
    options.UseNpgsql(connectionString); // Or UseSqlServer, UseMySql
});

builder.Services.AddScoped<ICompletedRequestStore, DatabaseCompletedRequestStore>();
```

### Configuration

```bash
# Environment variable
DATABASE__CONNECTIONSTRING=Host=localhost;Database=matchmaking;Username=user;Password=pass

# Or in appsettings.json
{
  "Database": {
    "ConnectionString": "Host=localhost;Database=matchmaking;Username=user;Password=pass"
  }
}
```

### Database Migration

Create initial migration:

```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

### Dependencies

Add to your `.csproj`:

```xml
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Microsoft.EntityFrameworkCore.Tools" Version="8.0.0" />
```

---

## Skill-Based MatchMaker

**Use Case:** Match players based on skill rating (MMR/ELO) instead of simple FIFO.

**Customization Point:** `MatchMaker.TryMatchAsync()` method

**Note:** This requires modifying the core MatchMaker class, not implementing an interface.

### Implementation

Modify `Services/MatchMaker.cs`:

```csharp
private async Task<bool> TryMatchAsync()
{
    // 1. Remove expired requests
    var expiredRequests = _matchPool.RemoveExpired(_requestTimeout);
    foreach (var expired in expiredRequests)
    {
        expired.Status = MatchRequestStatus.Expired;
        _completedRequestStore.Add(expired);
        _logger.LogInformation(
            "Request {RequestId} expired after {Timeout}",
            expired.RequestId,
            _requestTimeout);
    }

    // 2. Get all pending requests
    var allRequests = _matchPool.GetOldest(1000); // Get large batch for skill matching

    if (allRequests.Count < _matchSize)
    {
        _logger.LogDebug(
            "Not enough requests for match. Pool size: {PoolSize}, Required: {MatchSize}",
            allRequests.Count,
            _matchSize);
        return false;
    }

    // 3. CUSTOMIZATION: Group by skill brackets
    var skillBrackets = allRequests
        .GroupBy(r => GetSkillBracket(r))
        .OrderBy(g => g.Key); // Match lower skill brackets first

    foreach (var bracket in skillBrackets)
    {
        var requests = bracket.OrderBy(r => r.CreatedAt).ToList();

        // Try to create matches within this bracket
        while (requests.Count >= _matchSize)
        {
            var matchRequests = requests.Take(_matchSize).ToList();
            requests = requests.Skip(_matchSize).ToList();

            // Verify skill compatibility
            if (!AreSkillsCompatible(matchRequests))
            {
                _logger.LogDebug("Skipping match due to skill incompatibility");
                continue;
            }

            // Remove from pool
            foreach (var req in matchRequests)
            {
                _matchPool.Remove(req.RequestId);
            }

            // Create match
            var match = new Match
            {
                MatchId = Guid.NewGuid().ToString(),
                Requests = matchRequests,
                CreatedAt = DateTime.UtcNow
            };

            try
            {
                // Create session
                var sessionInfo = await _sessionCreator.GetSessionAsync(match);

                // Update requests
                foreach (var req in matchRequests)
                {
                    req.Status = MatchRequestStatus.Matched;
                    req.MatchedAt = DateTime.UtcNow;
                    req.SessionId = sessionInfo.SessionId;
                    _completedRequestStore.Add(req);
                }

                // Notify players
                await _playerNotifier.NotifyMatchAsync(sessionInfo);

                _logger.LogInformation(
                    "Created match {MatchId} with {PlayerCount} players in skill bracket {Bracket}",
                    match.MatchId,
                    matchRequests.Count,
                    GetSkillBracket(matchRequests[0]));

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create match {MatchId}", match.MatchId);

                // Return requests to pool
                foreach (var req in matchRequests)
                {
                    _matchPool.Add(req);
                }

                return false;
            }
        }
    }

    return false;
}

// Helper: Extract MMR from metadata and determine bracket
private int GetSkillBracket(MatchRequest request)
{
    if (request.Metadata?.TryGetValue("mmr", out var mmrStr) == true &&
        int.TryParse(mmrStr, out var mmr))
    {
        return mmr / 500; // Bracket size of 500 MMR
    }
    return 0; // Default bracket for players without MMR
}

// Helper: Verify all players in match are within acceptable skill range
private bool AreSkillsCompatible(List<MatchRequest> requests)
{
    var mmrs = requests
        .Select(r => r.Metadata?.TryGetValue("mmr", out var mmrStr) == true &&
                     int.TryParse(mmrStr, out var mmr) ? mmr : 0)
        .ToList();

    if (mmrs.All(m => m == 0))
        return true; // All unranked players

    var minMmr = mmrs.Min();
    var maxMmr = mmrs.Max();
    var maxDifference = 1000; // Max 1000 MMR difference

    return (maxMmr - minMmr) <= maxDifference;
}
```

### Client Usage

Submit match request with MMR metadata:

```bash
curl -X POST "http://localhost:8000/matchmaking/v1/match/submit" \
  -H "Authorization: Bearer $ACCESS_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "metadata": {
      "mmr": "2500"
    }
  }'
```

### Configuration

Adjust bracket size and max difference in the helper methods:

```csharp
// Bracket size (group players into skill ranges)
return mmr / 500; // 500 MMR per bracket

// Max skill difference within a match
var maxDifference = 1000; // Max 1000 MMR difference
```

### Considerations

- **Wait times**: Skill-based matching may increase wait times for high/low skill players
- **Bracket tuning**: Adjust bracket size based on player population
- **Fallback**: Consider falling back to FIFO after timeout for better wait times
- **Dynamic brackets**: Adjust bracket size based on time of day or queue length

---

## Additional Examples

For more examples and patterns, see:

- **[Architecture Guide](architecture.md)** - Extension points and design patterns
- **[Setup Guide](setup.md)** - Configuration and deployment
- **[Operations Guide](operations.md)** - Monitoring and troubleshooting
