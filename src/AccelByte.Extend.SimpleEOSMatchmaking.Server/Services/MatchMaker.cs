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
    /// Configuration for the matchmaker
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
    public class MatchMaker(
        IMatchPool matchPool,
        ISessionCreator sessionCreator,
        IPlayerNotifier notifier,
        ICompletedRequestStore completedRequestStore,
        MatchMakerConfig config,
        ILogger<MatchMaker> logger)
        : IHostedService
    {
        private readonly IMatchPool MatchPool = matchPool ?? throw new ArgumentNullException(nameof(matchPool));
        private readonly ISessionCreator SessionCreator = sessionCreator ?? throw new ArgumentNullException(nameof(sessionCreator));
        private readonly IPlayerNotifier Notifier = notifier ?? throw new ArgumentNullException(nameof(notifier));
        private readonly ICompletedRequestStore CompletedRequestStore = completedRequestStore ?? throw new ArgumentNullException(nameof(completedRequestStore));
        private readonly MatchMakerConfig Config = config ?? throw new ArgumentNullException(nameof(config));
        private readonly ILogger<MatchMaker> Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        private Timer? Timer;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("MatchMaker starting with MatchSize={MatchSize}, TickInterval={TickInterval}, RequestTimeout={RequestTimeout}",
                Config.MatchSize, Config.TickInterval, Config.RequestTimeout);

            Timer = new Timer(
                DoMatchmaking,
                null,
                TimeSpan.Zero,
                Config.TickInterval);

            return Task.CompletedTask;
        }

        private async void DoMatchmaking(object? _)
        {
            try
            {
                await TryMatchAsync();
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Error in MatchMaker");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("MatchMaker stopping");
            Timer?.Change(Timeout.Infinite, 0);
            Timer?.Dispose();
            return Task.CompletedTask;
        }

        /// <summary>
        /// Attempt to create matches from the current pool.
        /// 
        /// CUSTOMIZATION POINT: This method implements the core matching algorithm.
        /// The default implementation uses a simple FIFO (First-In-First-Out) algorithm
        /// that matches the oldest N requests together.
        /// 
        /// You can customize this method to implement different matching strategies such as
        /// skill-based matching, region-based matching, role-based matching, or party/group matching.
        /// 
        /// See docs/examples.md for complete implementation example starting points.
        /// </summary>
        public async Task<IReadOnlyList<Match>> TryMatchAsync()
        {
            var matches = new List<Match>();

            try
            {
                var expiredCompleted = CompletedRequestStore.RemoveExpired(Config.RetentionPeriod);
                if (expiredCompleted.Count > 0)
                {
                    Logger.LogInformation("Removed {Count} expired completed requests from retention store", expiredCompleted.Count);
                }

                var expiredRequests = MatchPool.RemoveExpired(Config.RequestTimeout);
                if (expiredRequests.Count > 0)
                {
                    Logger.LogInformation("Removed {Count} expired requests", expiredRequests.Count);
                    
                    foreach (var expiredRequest in expiredRequests)
                    {
                        expiredRequest.Status = Model.MatchRequestStatus.Expired;
                        expiredRequest.CompletedAt = DateTime.UtcNow;
                        CompletedRequestStore.Add(expiredRequest);
                    }
                }

                // MATCHING ALGORITHM: Simple FIFO (First-In-First-Out)
                // This ensures fairness - players who waited the longest get matched first.
                
                while (MatchPool.Count >= Config.MatchSize)
                {
                    var oldestRequests = MatchPool.GetOldest(Config.MatchSize);
                    if (oldestRequests.Count < Config.MatchSize)
                    {
                        break;
                    }

                    var requestsForMatch = new List<MatchRequest>();
                    foreach (var request in oldestRequests)
                    {
                        var removed = MatchPool.Remove(request.RequestId);
                        if (removed != null)
                        {
                            requestsForMatch.Add(removed);
                        }
                    }

                    if (requestsForMatch.Count != Config.MatchSize)
                    {
                        Logger.LogWarning("Failed to remove all requests for match, expected {Expected}, got {Actual}",
                            Config.MatchSize, requestsForMatch.Count);
                        
                        foreach (var request in requestsForMatch)
                        {
                            MatchPool.Add(request);
                        }
                        break;
                    }

                    var match = new Match(requestsForMatch);
                    
                    try
                    {
                        var sessionInfo = await SessionCreator.GetSessionAsync(match);

                        foreach (var request in requestsForMatch)
                        {
                            request.Status = Model.MatchRequestStatus.Matched;
                            request.MatchedAt = DateTime.UtcNow;
                            request.SessionId = sessionInfo.SessionId;
                            request.CompletedAt = DateTime.UtcNow;
                        }

                        foreach (var request in requestsForMatch)
                        {
                            CompletedRequestStore.Add(request);
                        }

                        await Notifier.NotifyMatchAsync(sessionInfo);

                        matches.Add(match);

                        Logger.LogInformation("Created match {MatchId} with {PlayerCount} players, SessionId={SessionId}",
                            match.MatchId, requestsForMatch.Count, sessionInfo.SessionId);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError(ex, "Failed to create session for match {MatchId}, returning requests to pool", match.MatchId);
                        
                        foreach (var request in requestsForMatch)
                        {
                            MatchPool.Add(request);
                        }
                        
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error in TryMatchAsync");
            }

            return matches;
        }
    }
}
