using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// INFRASTRUCTURE-LEVEL EXTENSION POINT: Interface for managing completed match requests with a retention period.
    /// </summary>
    public interface ICompletedRequestStore
    {
        /// <summary>
        /// Add a completed request to the store
        /// </summary>
        void Add(MatchRequest request);

        /// <summary>
        /// Get a completed request by ID
        /// </summary>
        MatchRequest? Get(string requestId);

        /// <summary>
        /// Remove requests that have exceeded the retention period
        /// </summary>
        IReadOnlyList<MatchRequest> RemoveExpired(TimeSpan retentionPeriod);
    }

    /// <summary>
    /// Thread-safe in-memory storage for completed match requests
    /// </summary>
    public class CompletedRequestStore : ICompletedRequestStore
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, MatchRequest> _requestsById = new Dictionary<string, MatchRequest>();

        public int Count
        {
            get
            {
                lock (_lock)
                {
                    return _requestsById.Count;
                }
            }
        }

        public void Add(MatchRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            lock (_lock)
            {
                _requestsById[request.RequestId] = request;
            }
        }

        public MatchRequest? Get(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            lock (_lock)
            {
                _requestsById.TryGetValue(requestId, out var request);
                return request;
            }
        }

        public IReadOnlyList<MatchRequest> RemoveExpired(TimeSpan retentionPeriod)
        {
            var expiredRequests = new List<MatchRequest>();
            var cutoffTime = DateTime.UtcNow - retentionPeriod;

            lock (_lock)
            {
                var toRemove = _requestsById.Values
                    .Where(r => r.CompletedAt.HasValue && r.CompletedAt.Value < cutoffTime)
                    .ToList();

                foreach (var request in toRemove)
                {
                    _requestsById.Remove(request.RequestId);
                    expiredRequests.Add(request);
                }
            }

            return expiredRequests;
        }
    }
}
