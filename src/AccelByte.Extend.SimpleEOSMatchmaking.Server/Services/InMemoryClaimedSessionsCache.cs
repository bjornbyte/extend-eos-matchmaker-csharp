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
    /// In-memory implementation of the claimed sessions cache
    /// </summary>
    public class InMemoryClaimedSessionsCache(
        ILogger<InMemoryClaimedSessionsCache> logger,
        EOSSessionFinderConfig config)
        : IClaimedSessionsCache
    {
        private readonly ConcurrentDictionary<string, DateTime> ClaimedSessions = new();

        public bool IsSessionClaimed(string sessionId)
        {
            // Remove expired entries before checking
            RemoveExpiredEntries();
            
            return ClaimedSessions.ContainsKey(sessionId);
        }

        public void AddClaimedSession(string sessionId)
        {
            ClaimedSessions[sessionId] = DateTime.UtcNow;
            logger.LogDebug("Added session to claimed cache: SessionId={SessionId}", sessionId);
        }

        public void RemoveExpiredEntries()
        {
            var now = DateTime.UtcNow;
            var expirationTime = TimeSpan.FromSeconds(config.ClaimedSessionExpirationSeconds);
            
            var expiredKeys = ClaimedSessions
                .Where(kvp => now - kvp.Value > expirationTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                ClaimedSessions.TryRemove(key, out _);
                logger.LogDebug("Removed expired session from claimed cache: SessionId={SessionId}", key);
            }
        }
    }
}
