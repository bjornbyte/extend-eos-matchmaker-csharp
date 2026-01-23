// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Configuration for the match maker
    /// </summary>
    public class MatchMakerConfig
    {
        /// <summary>
        /// Number of players per match
        /// </summary>
        public int MatchSize { get; set; } = 2;

        /// <summary>
        /// How often to check for matches
        /// </summary>
        public TimeSpan TickInterval { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Request expiration timeout
        /// </summary>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(60);

        /// <summary>
        /// Retention period for completed requests
        /// </summary>
        public TimeSpan RetentionPeriod { get; set; } = TimeSpan.FromSeconds(120);
    }

    /// <summary>
    /// Background service that periodically checks the pool and creates matches
    /// </summary>
    public class MatchMaker : IHostedService
    {
        private readonly IMatchPool _matchPool;
        private readonly ISessionCreator _sessionCreator;
        private readonly IPlayerNotifier _notifier;
        private readonly ICompletedRequestStore _completedRequestStore;
        private readonly MatchMakerConfig _config;
        private readonly ILogger<MatchMaker> _logger;
        private Timer? _timer;

        public MatchMaker(
            IMatchPool matchPool,
            ISessionCreator sessionCreator,
            IPlayerNotifier notifier,
            ICompletedRequestStore completedRequestStore,
            MatchMakerConfig config,
            ILogger<MatchMaker> logger)
        {
            _matchPool = matchPool ?? throw new ArgumentNullException(nameof(matchPool));
            _sessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
            _notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
            _completedRequestStore = completedRequestStore ?? throw new ArgumentNullException(nameof(completedRequestStore));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MatchMaker starting with MatchSize={MatchSize}, TickInterval={TickInterval}, RequestTimeout={RequestTimeout}",
                _config.MatchSize, _config.TickInterval, _config.RequestTimeout);

            _timer = new Timer(
                async _ => await TryMatchAsync(),
                null,
                TimeSpan.Zero,
                _config.TickInterval);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("MatchMaker stopping");
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Attempt to create matches from the current pool.
        /// 
        /// CUSTOMIZATION POINT: This method implements the core matching algorithm.
        /// The default implementation uses a simple FIFO (First-In-First-Out) algorithm
        /// that matches the oldest N requests together.
        /// 
        /// COMMON CUSTOMIZATIONS:
        /// 
        /// 1. SKILL-BASED MATCHING:
        ///    - Add skill/MMR metadata to MatchRequest
        ///    - Filter requests by skill range before matching
        ///    - Example: var skillFiltered = _matchPool.GetAll().Where(r => Math.Abs(r.Metadata["mmr"] - avgMmr) < 200);
        /// 
        /// 2. REGION-BASED MATCHING:
        ///    - Add region metadata to MatchRequest
        ///    - Group requests by region before matching
        ///    - Example: var regionRequests = _matchPool.GetAll().Where(r => r.Metadata["region"] == "us-west");
        /// 
        /// 3. ROLE-BASED MATCHING (team games):
        ///    - Add role metadata (tank, healer, dps)
        ///    - Ensure balanced team composition
        ///    - Example: Select 1 tank, 1 healer, 2 dps
        /// 
        /// 4. PARTY/GROUP MATCHING:
        ///    - Add party_id metadata to keep friends together
        ///    - Match parties as units rather than individual players
        /// 
        /// See docs/architecture.md#matchmaker-customization for complete examples.
        /// </summary>
        public async Task<IReadOnlyList<Match>> TryMatchAsync()
        {
            var matches = new List<Match>();

            try
            {
                // Clean up expired completed requests
                var expiredCompleted = _completedRequestStore.RemoveExpired(_config.RetentionPeriod);
                if (expiredCompleted.Count > 0)
                {
                    _logger.LogInformation("Removed {Count} expired completed requests from retention store", expiredCompleted.Count);
                }

                // Remove expired requests first
                var expiredRequests = _matchPool.RemoveExpired(_config.RequestTimeout);
                if (expiredRequests.Count > 0)
                {
                    _logger.LogInformation("Removed {Count} expired requests", expiredRequests.Count);
                    
                    // Move expired requests to completed store
                    foreach (var expiredRequest in expiredRequests)
                    {
                        expiredRequest.Status = Model.MatchRequestStatus.Expired;
                        expiredRequest.CompletedAt = DateTime.UtcNow;
                        _completedRequestStore.Add(expiredRequest);
                    }
                }

                // MATCHING ALGORITHM: Simple FIFO (First-In-First-Out)
                // This ensures fairness - players who waited longest get matched first.
                //
                // CUSTOMIZATION POINT: Replace this section for custom matching logic.
                // Examples:
                // - Skill-based: Filter by MMR/ELO before selecting oldest
                // - Region-based: Group by region metadata
                // - Role-based: Ensure team composition (tank, healer, dps)
                //
                // Check if we have enough requests to make a match
                while (_matchPool.Count >= _config.MatchSize)
                {
                    // CUSTOMIZATION POINT: Add filtering logic here
                    // Example: var eligibleRequests = _matchPool.GetAll().Where(r => r.Metadata["region"] == targetRegion);
                    
                    // Get the oldest requests (FIFO)
                    var oldestRequests = _matchPool.GetOldest(_config.MatchSize);
                    if (oldestRequests.Count < _config.MatchSize)
                    {
                        break;
                    }

                    // Remove requests from pool
                    var requestsForMatch = new List<MatchRequest>();
                    foreach (var request in oldestRequests)
                    {
                        var removed = _matchPool.Remove(request.RequestId);
                        if (removed != null)
                        {
                            requestsForMatch.Add(removed);
                        }
                    }

                    // Verify we got all the requests
                    if (requestsForMatch.Count != _config.MatchSize)
                    {
                        _logger.LogWarning("Failed to remove all requests for match, expected {Expected}, got {Actual}",
                            _config.MatchSize, requestsForMatch.Count);
                        
                        // Return requests to pool
                        foreach (var request in requestsForMatch)
                        {
                            _matchPool.Add(request);
                        }
                        break;
                    }

                    // Create match
                    var match = new Match(requestsForMatch);
                    
                    try
                    {
                        // Get session
                        var sessionInfo = await _sessionCreator.GetSessionAsync(match);

                        // Update request statuses
                        foreach (var request in requestsForMatch)
                        {
                            request.Status = Model.MatchRequestStatus.Matched;
                            request.MatchedAt = DateTime.UtcNow;
                            request.SessionId = sessionInfo.SessionId;
                            request.CompletedAt = DateTime.UtcNow;
                        }

                        // Move matched requests to completed store
                        foreach (var request in requestsForMatch)
                        {
                            _completedRequestStore.Add(request);
                        }

                        // Notify
                        await _notifier.NotifyMatchAsync(sessionInfo);

                        matches.Add(match);

                        _logger.LogInformation("Created match {MatchId} with {PlayerCount} players, SessionId={SessionId}",
                            match.MatchId, requestsForMatch.Count, sessionInfo.SessionId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to create session for match {MatchId}, returning requests to pool", match.MatchId);
                        
                        // Return requests to pool on failure
                        foreach (var request in requestsForMatch)
                        {
                            _matchPool.Add(request);
                        }
                        
                        // Break out of loop to avoid infinite retry
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TryMatchAsync");
            }

            return matches;
        }
    }
}
