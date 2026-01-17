// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Model
{
    /// <summary>
    /// Status of a match request
    /// </summary>
    public enum MatchRequestStatus
    {
        Pending = 0,
        Matched = 1,
        Expired = 2,
        Cancelled = 3
    }

    /// <summary>
    /// Represents a player's request to be matched
    /// </summary>
    public class MatchRequest
    {
        public string RequestId { get; set; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = string.Empty;
        public MatchRequestStatus Status { get; set; } = MatchRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? MatchedAt { get; set; }
        public string? SessionId { get; set; }
        public Dictionary<string, string> Metadata { get; set; } = new();
    }
}
