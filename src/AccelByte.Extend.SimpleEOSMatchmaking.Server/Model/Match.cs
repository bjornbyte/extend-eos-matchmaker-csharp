using System;
using System.Collections.Generic;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Model
{
    /// <summary>
    /// Represents a completed match with multiple match requests
    /// </summary>
    public class Match
    {
        public string MatchId { get; }
        public List<MatchRequest> Requests { get; set; }
        public DateTime CreatedAt { get; set; }

        public Match(List<MatchRequest> requests)
        {
            MatchId = Guid.NewGuid().ToString();
            Requests = requests ?? new List<MatchRequest>();
            CreatedAt = DateTime.UtcNow;
        }
    }
}
