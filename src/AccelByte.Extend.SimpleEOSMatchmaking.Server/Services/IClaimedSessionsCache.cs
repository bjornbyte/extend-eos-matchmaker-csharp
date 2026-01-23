// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// INFRASTRUCTURE-LEVEL EXTENSION POINT: Interface for tracking recently claimed sessions to prevent concurrent claims.
    /// 
    /// This extension point is ONLY used in "find" mode where the matchmaker searches for existing
    /// available EOS sessions. The cache prevents race conditions when multiple matches are created
    /// simultaneously and might try to claim the same session.
    /// 
    /// WHY THIS IS NEEDED:
    /// - EOS session state updates are not instantaneous
    /// - Multiple concurrent matches might see the same "available" session
    /// - Local cache provides immediate consistency within the matchmaker
    /// - Session owner is responsible for updating EOS state to "started"
    /// 
    /// DEFAULT IMPLEMENTATION (InMemoryClaimedSessionsCache):
    /// - Thread-safe in-memory cache using ConcurrentDictionary
    /// - Tracks sessions with timestamps
    /// - Automatic expiration after configured time (default: 5 minutes)
    /// - Suitable for single-instance deployments
    /// 
    /// WHEN TO IMPLEMENT DISTRIBUTED CACHE:
    /// 
    /// Multi-Instance Deployments:
    /// - Use Redis with TTL (Time-To-Live) for shared state
    /// - All matchmaker instances see the same claimed sessions
    /// - Prevents different instances from claiming the same session
    /// - Essential for horizontal scaling
    /// 
    /// IMPLEMENTATION CONSIDERATIONS:
    /// - Must support fast lookups (IsSessionClaimed)
    /// - Must support automatic expiration (TTL or RemoveExpiredEntries)
    /// - Must be thread-safe for concurrent access
    /// - Expiration time should match session owner notification timeout
    /// 
    /// REDIS EXAMPLE:
    /// Use Redis SET with EX (expiration) for automatic TTL:
    /// SET claimed:session:{sessionId} "1" EX 300
    /// 
    /// NOT needed when:
    /// - Using "create" mode (matchmaker creates sessions)
    /// - Single-instance deployment with in-memory cache
    /// 
    /// See docs/architecture.md#infrastructure-extension-points for Redis implementation example.
    /// </summary>
    public interface IClaimedSessionsCache
    {
        /// <summary>
        /// Check if a session is in the claimed cache
        /// </summary>
        bool IsSessionClaimed(string sessionId);

        /// <summary>
        /// Add a session to the claimed cache with expiration
        /// </summary>
        void AddClaimedSession(string sessionId);

        /// <summary>
        /// Remove expired entries from the cache
        /// </summary>
        void RemoveExpiredEntries();
    }
}
