using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Interface for managing a pool of pending match requests
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
    /// </summary>
    public class MatchPool : IMatchPool
    {
        private readonly object _lock = new object();
        private readonly Dictionary<string, MatchRequest> _requestsById = new Dictionary<string, MatchRequest>();
        private readonly Dictionary<string, MatchRequest> _requestsByUserId = new Dictionary<string, MatchRequest>();
        private readonly List<MatchRequest> _requestsInOrder = new List<MatchRequest>();

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
                _requestsByUserId[request.UserId] = request;
                _requestsInOrder.Add(request);
            }
        }

        public MatchRequest? Remove(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
                return null;

            lock (_lock)
            {
                if (!_requestsById.TryGetValue(requestId, out var request))
                    return null;

                _requestsById.Remove(requestId);
                _requestsByUserId.Remove(request.UserId);
                _requestsInOrder.Remove(request);

                return request;
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

        public MatchRequest? GetByUserId(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                return null;

            lock (_lock)
            {
                _requestsByUserId.TryGetValue(userId, out var request);
                return request;
            }
        }

        public IReadOnlyList<MatchRequest> GetOldest(int count)
        {
            if (count <= 0)
                return new List<MatchRequest>();

            lock (_lock)
            {
                return _requestsInOrder
                    .OrderBy(r => r.CreatedAt)
                    .Take(count)
                    .ToList();
            }
        }

        public IReadOnlyList<MatchRequest> RemoveExpired(TimeSpan timeout)
        {
            var expiredRequests = new List<MatchRequest>();
            var cutoffTime = DateTime.UtcNow - timeout;

            lock (_lock)
            {
                // Find expired requests
                var toRemove = _requestsInOrder
                    .Where(r => r.CreatedAt < cutoffTime)
                    .ToList();

                // Remove and update status
                foreach (var request in toRemove)
                {
                    request.Status = Model.MatchRequestStatus.Expired;
                    _requestsById.Remove(request.RequestId);
                    _requestsByUserId.Remove(request.UserId);
                    _requestsInOrder.Remove(request);
                    expiredRequests.Add(request);
                }
            }

            return expiredRequests;
        }
    }
}
