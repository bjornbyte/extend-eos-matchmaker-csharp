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
    public class MatchRequest
    {
        public string RequestId { get; }
        public string UserId { get; set; }
        public MatchRequestStatus Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? MatchedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? SessionId { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }

        public MatchRequest(string userId, Dictionary<string, string>? metadata = null)
        {
            RequestId = Guid.NewGuid().ToString();
            UserId = userId;
            Status = MatchRequestStatus.Pending;
            CreatedAt = DateTime.UtcNow;
            Metadata = metadata;
        }
    }
}
