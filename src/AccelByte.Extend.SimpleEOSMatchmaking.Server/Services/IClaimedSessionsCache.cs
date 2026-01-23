// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Interface for tracking recently claimed sessions to prevent concurrent claims
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
