using System;
using System.Collections.Generic;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Model
{
    /// <summary>
    /// Represents the status of a match request
    /// </summary>
    public enum MatchRequestStatus
    {
        Pending,
        Matched,
        Expired,
        Cancelled
    }

    /// <summary>
    /// Represents a matchmaking request from a user
    /// </summary>
    public class MatchRequest(string userId, Dictionary<string, string>? metadata = null)
    {
        public string RequestId { get; } = Guid.NewGuid().ToString();
        public string UserId { get; set; } = userId;
        public MatchRequestStatus Status { get; set; } = MatchRequestStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? MatchedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? SessionId { get; set; }
        public Dictionary<string, string>? Metadata { get; set; } = metadata;
    }
}
