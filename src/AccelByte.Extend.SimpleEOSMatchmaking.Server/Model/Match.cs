using System;
using System.Collections.Generic;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Model
{
    /// <summary>
    /// Represents a completed match with multiple match requests
    /// </summary>
    public class Match(List<MatchRequest> requests)
    {
        public string MatchId { get; } = Guid.NewGuid().ToString();
        public List<MatchRequest> Requests { get; set; } = requests ?? new List<MatchRequest>();
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
