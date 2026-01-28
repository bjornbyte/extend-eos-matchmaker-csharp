using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// INFRASTRUCTURE-LEVEL EXTENSION POINT: Interface for managing a pool of pending match requests.
    /// 
    /// IMPLEMENTATION CONSIDERATIONS:
    /// - Must maintain FIFO ordering for fair matching
    /// - Must support fast lookups by request ID and user ID
    /// - Must be thread-safe for concurrent access
    /// - Must support atomic operations (add, remove, get oldest)
    /// 
    /// See docs/architecture.md#infrastructure-extension-points for Redis and database examples.
    /// </summary>
    public interface IMatchPool
    {
        /// <summary>
        /// Add a new match request to the pool
        /// </summary>
        void Add(MatchRequest request);

        /// <summary>
        /// Remove a match request by ID
        /// </summary>
        MatchRequest? Remove(string requestId);

        /// <summary>
        /// Get a match request by ID
        /// </summary>
        MatchRequest? Get(string requestId);

        /// <summary>
        /// Get a match request by user ID
        /// </summary>
        MatchRequest? GetByUserId(string userId);

        /// <summary>
        /// Get oldest N requests for matching
        /// </summary>
        IReadOnlyList<MatchRequest> GetOldest(int count);

        /// <summary>
        /// Remove expired requests and return them
        /// </summary>
        IReadOnlyList<MatchRequest> RemoveExpired(TimeSpan timeout);

        /// <summary>
        /// Get count of pending requests
        /// </summary>
        int Count { get; }
    }

    /// <summary>
    /// Thread-safe in-memory storage for pending match requests
    /// This is suitable for single-instance deployments but has limitations:
    /// 
    /// LIMITATIONS OF DEFAULT IN-MEMORY IMPLEMENTATION:
    /// - Single-instance only: Cannot share state across multiple service instances
    /// - No durability: All pending requests lost on service restart
    /// - Memory-bound: Limited by available RAM
    /// </summary>
    public class MatchPool : IMatchPool
    {
        private readonly object Lock = new();
        private readonly Dictionary<string, MatchRequest> RequestsById = new();
        private readonly Dictionary<string, MatchRequest> RequestsByUserId = new();
        private readonly List<MatchRequest> RequestsInOrder = [];

        public int Count
        {
            get
            {
                lock (Lock)
                {
                    return RequestsById.Count;
                }
            }
        }

        public void Add(MatchRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            lock (Lock)
            {
                RequestsById[request.RequestId] = request;
                RequestsByUserId[request.UserId] = request;
                RequestsInOrder.Add(request);
            }
        }

        public MatchRequest? Remove(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            lock (Lock)
            {
                if (!RequestsById.TryGetValue(requestId, out var request))
                    return null;

                RequestsById.Remove(requestId);
                RequestsByUserId.Remove(request.UserId);
                RequestsInOrder.Remove(request);

                return request;
            }
        }

        public MatchRequest? Get(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            lock (Lock)
            {
                RequestsById.TryGetValue(requestId, out var request);
                return request;
            }
        }

        public MatchRequest? GetByUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            lock (Lock)
            {
                RequestsByUserId.TryGetValue(userId, out var request);
                return request;
            }
        }

        public IReadOnlyList<MatchRequest> GetOldest(int count)
        {
            if (count <= 0)
                return new List<MatchRequest>();

            lock (Lock)
            {
                return RequestsInOrder
                    .OrderBy(r => r.CreatedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public IReadOnlyList<MatchRequest> RemoveExpired(TimeSpan timeout)
        {
            var expiredRequests = new List<MatchRequest>();
            var cutoffTime = DateTime.UtcNow - timeout;

            lock (Lock)
            {
                // Find expired requests
                var toRemove = RequestsInOrder
                    .Where(r => r.CreatedAt < cutoffTime)
                    .ToList();

                // Remove and update the status
                foreach (var request in toRemove)
                {
                    request.Status = Model.MatchRequestStatus.Expired;
                    RequestsById.Remove(request.RequestId);
                    RequestsByUserId.Remove(request.UserId);
                    RequestsInOrder.Remove(request);
                    expiredRequests.Add(request);
                }
            }

            return expiredRequests;
        }
    }
}
