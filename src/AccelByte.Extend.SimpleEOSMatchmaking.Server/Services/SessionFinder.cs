// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Finds and claims existing empty EOS sessions for matched players.
    /// Suitable for player-hosted servers or dedicated servers that create their own sessions.
    /// </summary>
    public class EosSessionFinder : ISessionCreator
    {
        private readonly ILogger<EosSessionFinder> Logger;
        private readonly EOSSDKService EosService;
        private readonly EOSSessionFinderConfig Config;
        private readonly IClaimedSessionsCache ClaimedSessionsCache;
        private readonly ISessionOwnerNotifier SessionOwnerNotifier;
        private readonly object ClaimLock = new();

        public EosSessionFinder(
            ILogger<EosSessionFinder> logger,
            EOSSDKService eosService,
            EOSSessionFinderConfig config,
            IClaimedSessionsCache claimedSessionsCache,
            ISessionOwnerNotifier sessionOwnerNotifier)
        {
            Logger = logger;
            EosService = eosService;
            Config = config;
            ClaimedSessionsCache = claimedSessionsCache;
            SessionOwnerNotifier = sessionOwnerNotifier;
        }

        public async Task<SessionInfo> GetSessionAsync(Match match)
        {
            // Search for an available session
            var (sessionDetails, sessionsSearched) = await SearchForAvailableSessionAsync();
            
            if (sessionDetails == null)
            {
                Logger.LogWarning("No available sessions found: SessionsSearched={SessionsSearched}, MatchId={MatchId}",
                    sessionsSearched, match.MatchId);
                throw new NoAvailableSessionsException(sessionsSearched);
            }
            
            // Claim the session
            return await ClaimSessionAsync(sessionDetails, match);
        }

        private async Task<SessionInfo> ClaimSessionAsync(SessionDetails sessionDetails, Match match)
        {
            // Get session info
            var copyInfoOptions = new SessionDetailsCopyInfoOptions();
            var infoResult = sessionDetails.CopyInfo(ref copyInfoOptions, out var sessionInfo);
            
            if (infoResult != Result.Success || sessionInfo == null)
            {
                Logger.LogError("Failed to copy session info for claiming: {Result}", infoResult);
                throw new InvalidOperationException($"Failed to copy session info: {infoResult}");
            }

            var sessionId = sessionInfo.Value.SessionId?.ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                Logger.LogError("Session has no session ID");
                throw new InvalidOperationException("Session has no session ID");
            }

            // Session was already added to claimed cache in SearchForAvailableSessionAsync
            Logger.LogDebug("Processing claimed session: SessionId={SessionId}, MatchId={MatchId}",
                sessionId, match.MatchId);

            // Extract connection info from session settings (if available)
            var connectionInfo = "unknown";
            if (sessionInfo.Value.Settings != null)
            {
                // Try to get host address or other connection info
                // Note: EOS SessionDetailsSettings doesn't have HostAddress property
                // Connection info would typically be stored in session attributes
                connectionInfo = sessionInfo.Value.SessionId?.ToString() ?? "unknown";
            }

            // Send notification to session owner
            try
            {
                await SessionOwnerNotifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);
                Logger.LogInformation("Notified session owner: SessionId={SessionId}, MatchId={MatchId}, ConnectionInfo={ConnectionInfo}",
                    sessionId, match.MatchId, connectionInfo);
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "Failed to notify session owner: SessionId={SessionId}, MatchId={MatchId}",
                    sessionId, match.MatchId);
                // Continue - notification is fire and forget
            }

            // Extract request IDs and user IDs from match requests
            var requestIds = match.Requests.Select(r => r.RequestId).ToList();
            var userIds = match.Requests.Select(r => r.UserId).ToList();

            // Create and return SessionInfo
            var result = new SessionInfo
            {
                SessionId = sessionId,
                RequestIds = requestIds,
                UserIds = userIds,
                CreatedAt = DateTime.UtcNow
            };

            Logger.LogInformation("Successfully claimed session: SessionId={SessionId}, MatchId={MatchId}",
                sessionId, match.MatchId);

            return result;
        }

        private async Task<(SessionDetails? sessionDetails, int sessionsSearched)> SearchForAvailableSessionAsync()
        {
            if (EosService == null)
            {
                Logger.LogError("EOS SDK Service is not initialized");
                throw new InvalidOperationException("EOS SDK Service is not initialized");
            }

            var platform = EosService.Platform;
            if (platform == null)
            {
                Logger.LogError("EOS Platform is not initialized");
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                Logger.LogError("Failed to get EOS Sessions interface");
                throw new InvalidOperationException("EOS Sessions interface not available");
            }

            // Create session search handle
            var createSearchOptions = new CreateSessionSearchOptions
            {
                MaxSearchResults = (uint)Config.MaxSearchResults
            };

            var createSearchResult = sessionsInterface.CreateSessionSearch(ref createSearchOptions, out var sessionSearch);
            if (createSearchResult != Result.Success || sessionSearch == null)
            {
                Logger.LogError("Failed to create session search: {Result}", createSearchResult);
                throw new InvalidOperationException($"Failed to create session search: {createSearchResult}");
            }

            try
            {
                // Set bucket ID parameter for search
                // EOS SDK requires at least one search parameter
                var setParameterOptions = new SessionSearchSetParameterOptions
                {
                    Parameter = new AttributeData
                    {
                        Key = SessionsInterface.SEARCH_BUCKET_ID,
                        Value = new AttributeDataValue { AsUtf8 = Config.BucketId }
                    },
                    ComparisonOp = ComparisonOp.Equal
                };

                var setParamResult = sessionSearch.SetParameter(ref setParameterOptions);
                if (setParamResult != Result.Success)
                {
                    Logger.LogError("Failed to set bucket ID search parameter: {Result}", setParamResult);
                    throw new InvalidOperationException($"Failed to set search parameter: {setParamResult}");
                }

                Logger.LogDebug("Set search parameter: BucketId={BucketId}", Config.BucketId);

                // Execute search
                var findOptions = new SessionSearchFindOptions
                {
                    LocalUserId = null // Server-side search doesn't require a specific user ID
                };

                var tcs = new TaskCompletionSource<Result>();
                sessionSearch.Find(ref findOptions, null, (ref SessionSearchFindCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                // Tick the platform while waiting for the callback
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;
                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    EosService.Tick();
                    await Task.Delay(1); // Minimal delay - just yield to other tasks
                }

                if (!tcs.Task.IsCompleted)
                {
                    Logger.LogError("Session search timed out after {Timeout} seconds", timeout.TotalSeconds);
                    throw new TimeoutException($"Session search timed out after {timeout.TotalSeconds} seconds");
                }

                var findResult = await tcs.Task;
                if (findResult != Result.Success)
                {
                    Logger.LogWarning("Session search returned no results: {Result}", findResult);
                    return (null, 0);
                }

                // Iterate through results and find first unclaimed session
                var getCountOptions = new SessionSearchGetSearchResultCountOptions();
                var resultCount = sessionSearch.GetSearchResultCount(ref getCountOptions);

                Logger.LogDebug("Found {Count} sessions in search results", resultCount);

                // Lock the entire check-and-claim operation to prevent race conditions
                // This ensures that concurrent calls don't claim the same session
                lock (ClaimLock)
                {
                    for (uint i = 0; i < resultCount; i++)
                    {
                        var copyOptions = new SessionSearchCopySearchResultByIndexOptions
                        {
                            SessionIndex = i
                        };

                        var copyResult = sessionSearch.CopySearchResultByIndex(ref copyOptions, out var details);
                        if (copyResult != Result.Success || details == null)
                        {
                            Logger.LogWarning("Failed to copy search result at index {Index}: {Result}", i, copyResult);
                            continue;
                        }

                        // Get session ID
                        var copyInfoOptions = new SessionDetailsCopyInfoOptions();
                        var infoResult = details.CopyInfo(ref copyInfoOptions, out var sessionInfo);
                        
                        if (infoResult != Result.Success || sessionInfo == null)
                        {
                            Logger.LogWarning("Failed to copy session info at index {Index}: {Result}", i, infoResult);
                            continue;
                        }

                        var sessionId = sessionInfo.Value.SessionId?.ToString();
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            Logger.LogWarning("Session at index {Index} has no session ID", i);
                            continue;
                        }

                        // Check if session is in claimed cache
                        if (ClaimedSessionsCache.IsSessionClaimed(sessionId))
                        {
                            Logger.LogDebug("Session {SessionId} is already claimed, skipping", sessionId);
                            continue;
                        }

                        // Mark session as claimed immediately to prevent concurrent claims
                        ClaimedSessionsCache.AddClaimedSession(sessionId);
                        Logger.LogInformation("Found and claimed available session: {SessionId}", sessionId);
                        return (details, (int)resultCount);
                    }

                    // All sessions were claimed
                    Logger.LogWarning("All {Count} found sessions are already claimed", resultCount);
                    return (null, (int)resultCount);
                }
            }
            finally
            {
                // Clean up search handle
                sessionSearch?.Release();
            }
        }


    }
}
