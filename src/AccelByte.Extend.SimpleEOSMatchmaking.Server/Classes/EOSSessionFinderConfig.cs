// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    /// <summary>
    /// Configuration for EOSSessionFinder
    /// </summary>
    public class EOSSessionFinderConfig
    {
        /// <summary>
        /// Session bucket identifier to filter search results
        /// </summary>
        public string BucketId { get; set; } = "default";

        /// <summary>
        /// Maximum number of search results to retrieve
        /// </summary>
        public int MaxSearchResults { get; set; } = 10;

        /// <summary>
        /// Expiration time in seconds for claimed session cache entries
        /// </summary>
        public int ClaimedSessionExpirationSeconds { get; set; } = 300; // 5 minutes
    }
}
