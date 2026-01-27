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
    public class EOSSessionFinder : ISessionCreator
    {
        private readonly ILogger<EOSSessionFinder> _logger;
        private readonly EOSSDKService _eosService;
        private readonly EOSSessionFinderConfig _config;
        private readonly IClaimedSessionsCache _claimedSessionsCache;
        private readonly ISessionOwnerNotifier _sessionOwnerNotifier;
        private readonly object _claimLock = new object();

        public EOSSessionFinder(
            ILogger<EOSSessionFinder> logger,
            EOSSDKService eosService,
            EOSSessionFinderConfig config,
            IClaimedSessionsCache claimedSessionsCache,
            ISessionOwnerNotifier sessionOwnerNotifier)
        {
            _logger = logger;
            _eosService = eosService;
            _config = config;
            _claimedSessionsCache = claimedSessionsCache;
            _sessionOwnerNotifier = sessionOwnerNotifier;
        }

        public async Task<SessionInfo> GetSessionAsync(Match match)
        {
            // Search for an available session
            var (sessionDetails, sessionsSearched) = await SearchForAvailableSessionAsync();
            
            if (sessionDetails == null)
            {
                _logger.LogWarning("No available sessions found: SessionsSearched={SessionsSearched}, MatchId={MatchId}",
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
                _logger.LogError("Failed to copy session info for claiming: {Result}", infoResult);
                throw new InvalidOperationException($"Failed to copy session info: {infoResult}");
            }

            var sessionId = sessionInfo.Value.SessionId?.ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                _logger.LogError("Session has no session ID");
                throw new InvalidOperationException("Session has no session ID");
            }

            // Session was already added to claimed cache in SearchForAvailableSessionAsync
            _logger.LogDebug("Processing claimed session: SessionId={SessionId}, MatchId={MatchId}",
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
                await _sessionOwnerNotifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);
                _logger.LogInformation("Notified session owner: SessionId={SessionId}, MatchId={MatchId}, ConnectionInfo={ConnectionInfo}",
                    sessionId, match.MatchId, connectionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to notify session owner: SessionId={SessionId}, MatchId={MatchId}",
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

            _logger.LogInformation("Successfully claimed session: SessionId={SessionId}, MatchId={MatchId}",
                sessionId, match.MatchId);

            return result;
        }

        private async Task<(SessionDetails? sessionDetails, int sessionsSearched)> SearchForAvailableSessionAsync()
        {
            if (_eosService == null)
            {
                _logger.LogError("EOS SDK Service is not initialized");
                throw new InvalidOperationException("EOS SDK Service is not initialized");
            }

            var platform = _eosService.Platform;
            if (platform == null)
            {
                _logger.LogError("EOS Platform is not initialized");
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                _logger.LogError("Failed to get EOS Sessions interface");
                throw new InvalidOperationException("EOS Sessions interface not available");
            }

            // Create session search handle
            var createSearchOptions = new CreateSessionSearchOptions
            {
                MaxSearchResults = (uint)_config.MaxSearchResults
            };

            var createSearchResult = sessionsInterface.CreateSessionSearch(ref createSearchOptions, out var sessionSearch);
            if (createSearchResult != Result.Success || sessionSearch == null)
            {
                _logger.LogError("Failed to create session search: {Result}", createSearchResult);
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
                        Value = new AttributeDataValue { AsUtf8 = _config.BucketId }
                    },
                    ComparisonOp = ComparisonOp.Equal
                };

                var setParamResult = sessionSearch.SetParameter(ref setParameterOptions);
                if (setParamResult != Result.Success)
                {
                    _logger.LogError("Failed to set bucket ID search parameter: {Result}", setParamResult);
                    throw new InvalidOperationException($"Failed to set search parameter: {setParamResult}");
                }

                _logger.LogDebug("Set search parameter: BucketId={BucketId}", _config.BucketId);

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
                    _eosService.Tick();
                    await Task.Delay(1); // Minimal delay - just yield to other tasks
                }

                if (!tcs.Task.IsCompleted)
                {
                    _logger.LogError("Session search timed out after {Timeout} seconds", timeout.TotalSeconds);
                    throw new TimeoutException($"Session search timed out after {timeout.TotalSeconds} seconds");
                }

                var findResult = await tcs.Task;
                if (findResult != Result.Success)
                {
                    _logger.LogWarning("Session search returned no results: {Result}", findResult);
                    return (null, 0);
                }

                // Iterate through results and find first unclaimed session
                var getCountOptions = new SessionSearchGetSearchResultCountOptions();
                var resultCount = sessionSearch.GetSearchResultCount(ref getCountOptions);

                _logger.LogDebug("Found {Count} sessions in search results", resultCount);

                // Lock the entire check-and-claim operation to prevent race conditions
                // This ensures that concurrent calls don't claim the same session
                lock (_claimLock)
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
                            _logger.LogWarning("Failed to copy search result at index {Index}: {Result}", i, copyResult);
                            continue;
                        }

                        // Get session ID
                        var copyInfoOptions = new SessionDetailsCopyInfoOptions();
                        var infoResult = details.CopyInfo(ref copyInfoOptions, out var sessionInfo);
                        
                        if (infoResult != Result.Success || sessionInfo == null)
                        {
                            _logger.LogWarning("Failed to copy session info at index {Index}: {Result}", i, infoResult);
                            continue;
                        }

                        var sessionId = sessionInfo.Value.SessionId?.ToString();
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            _logger.LogWarning("Session at index {Index} has no session ID", i);
                            continue;
                        }

                        // Check if session is in claimed cache
                        if (_claimedSessionsCache.IsSessionClaimed(sessionId))
                        {
                            _logger.LogDebug("Session {SessionId} is already claimed, skipping", sessionId);
                            continue;
                        }

                        // Mark session as claimed immediately to prevent concurrent claims
                        _claimedSessionsCache.AddClaimedSession(sessionId);
                        _logger.LogInformation("Found and claimed available session: {SessionId}", sessionId);
                        return (details, (int)resultCount);
                    }

                    // All sessions were claimed
                    _logger.LogWarning("All {Count} found sessions are already claimed", resultCount);
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
