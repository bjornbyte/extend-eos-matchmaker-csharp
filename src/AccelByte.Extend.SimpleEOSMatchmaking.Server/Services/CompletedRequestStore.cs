using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// INFRASTRUCTURE-LEVEL EXTENSION POINT: Interface for managing completed match requests with retention period.
    /// 
    /// The default implementation (CompletedRequestStore) uses in-memory storage with thread-safe operations.
    /// This allows players to query the status of their completed requests (matched, expired, cancelled)
    /// for a configurable retention period (default: 2 minutes).
    /// 
    /// LIMITATIONS OF DEFAULT IN-MEMORY IMPLEMENTATION:
    /// - No durability: All completed requests lost on service restart
    /// - Single-instance only: Cannot share state across multiple service instances
    /// - Memory-bound: Limited retention period due to RAM constraints
    /// 
    /// WHEN TO IMPLEMENT CUSTOM STORAGE:
    /// 
    /// Service Restart Durability:
    /// - Use database (SQL Server, PostgreSQL, MongoDB, etc.) for persistence
    /// - Players can query request status even after service restarts
    /// - Useful for debugging and analytics
    /// 
    /// Multi-Instance Deployments:
    /// - Use Redis or distributed cache for shared state
    /// - All instances can serve status queries for any request
    /// - Consistent experience across load-balanced instances
    /// 
    /// Long Retention Periods:
    /// - Use database with indexing for efficient queries
    /// - Support retention periods of hours or days
    /// - Enable historical analysis and player support
    /// 
    /// IMPLEMENTATION CONSIDERATIONS:
    /// - Must support fast lookups by request ID
    /// - Must support automatic expiration/cleanup of old requests
    /// - Must be thread-safe for concurrent access
    /// - Consider indexing on CompletedAt for efficient expiration queries
    /// 
    /// RETENTION STRATEGY:
    /// Default retention period is 2 minutes (configurable via MatchMaker:RetentionPeriodSeconds).
    /// After this period, requests are removed to prevent unbounded memory growth.
    /// 
    /// See docs/architecture.md#infrastructure-extension-points for database and Redis examples.
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

        /// <summary>
        /// Get count of completed requests
        /// </summary>
        int Count { get; }
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
                // Find expired requests
                var toRemove = _requestsById.Values
                    .Where(r => r.CompletedAt.HasValue && r.CompletedAt.Value < cutoffTime)
                    .ToList();

                // Remove them
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
