// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Concurrent;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// In-memory implementation of claimed sessions cache
    /// </summary>
    public class InMemoryClaimedSessionsCache : IClaimedSessionsCache
    {
        private readonly ILogger<InMemoryClaimedSessionsCache> _logger;
        private readonly EOSSessionFinderConfig _config;
        private readonly ConcurrentDictionary<string, DateTime> _claimedSessions = new();

        public InMemoryClaimedSessionsCache(
            ILogger<InMemoryClaimedSessionsCache> logger,
            EOSSessionFinderConfig config)
        {
            _logger = logger;
            _config = config;
        }

        public bool IsSessionClaimed(string sessionId)
        {
            // Remove expired entries before checking
            RemoveExpiredEntries();
            
            return _claimedSessions.ContainsKey(sessionId);
        }

        public void AddClaimedSession(string sessionId)
        {
            _claimedSessions[sessionId] = DateTime.UtcNow;
            _logger.LogDebug("Added session to claimed cache: SessionId={SessionId}", sessionId);
        }

        public void RemoveExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expirationTime = TimeSpan.FromSeconds(_config.ClaimedSessionExpirationSeconds);
            
            var expiredKeys = _claimedSessions
                .Where(kvp => now - kvp.Value > expirationTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _claimedSessions.TryRemove(key, out _);
                _logger.LogDebug("Removed expired session from claimed cache: SessionId={SessionId}", key);
            }
        }
    }
}
