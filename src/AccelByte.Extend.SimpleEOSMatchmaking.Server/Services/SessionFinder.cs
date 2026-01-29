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
    public class EosSessionFinder(
        ILogger<EosSessionFinder> logger,
        EOSSDKService eosService,
        EOSSessionFinderConfig config,
        IClaimedSessionsCache claimedSessionsCache,
        ISessionOwnerNotifier sessionOwnerNotifier)
        : ISessionProvider
    {
        private readonly object ClaimLock = new();

        public async Task<SessionInfo> GetSessionAsync(Match match)
        {
            var (sessionDetails, sessionsSearched) = await SearchForAvailableSessionAsync();

            if (sessionDetails != null) return await ClaimSessionAsync(sessionDetails, match);
            
            logger.LogWarning("No available sessions found: SessionsSearched={SessionsSearched}, MatchId={MatchId}",
                sessionsSearched, match.MatchId);
            throw new NoAvailableSessionsException(sessionsSearched);
        }

        private async Task<SessionInfo> ClaimSessionAsync(SessionDetails sessionDetails, Match match)
        {
            var copyInfoOptions = new SessionDetailsCopyInfoOptions();
            var infoResult = sessionDetails.CopyInfo(ref copyInfoOptions, out var sessionInfo);
            
            if (infoResult != Result.Success || sessionInfo == null)
            {
                logger.LogError("Failed to copy session info for claiming: {Result}", infoResult);
                throw new InvalidOperationException($"Failed to copy session info: {infoResult}");
            }

            var sessionId = sessionInfo.Value.SessionId?.ToString();
            if (string.IsNullOrEmpty(sessionId))
            {
                logger.LogError("Session has no session ID");
                throw new InvalidOperationException("Session has no session ID");
            }

            logger.LogDebug("Processing claimed session: SessionId={SessionId}, MatchId={MatchId}",
                sessionId, match.MatchId);

            var connectionInfo = "unknown";
            if (sessionInfo.Value.Settings != null)
            {
                connectionInfo = sessionInfo.Value.SessionId?.ToString() ?? "unknown";
            }

            try
            {
                await sessionOwnerNotifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);
                logger.LogInformation("Notified session owner: SessionId={SessionId}, MatchId={MatchId}, ConnectionInfo={ConnectionInfo}",
                    sessionId, match.MatchId, connectionInfo);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify session owner: SessionId={SessionId}, MatchId={MatchId}",
                    sessionId, match.MatchId);
            }

            var requestIds = match.Requests.Select(r => r.RequestId).ToList();
            var userIds = match.Requests.Select(r => r.UserId).ToList();

            var result = new SessionInfo
            {
                SessionId = sessionId,
                RequestIds = requestIds,
                UserIds = userIds,
                CreatedAt = DateTime.UtcNow
            };

            logger.LogInformation("Successfully claimed session: SessionId={SessionId}, MatchId={MatchId}",
                sessionId, match.MatchId);

            return result;
        }

        private async Task<(SessionDetails? sessionDetails, int sessionsSearched)> SearchForAvailableSessionAsync()
        {
            var platform = eosService.Platform;
            if (platform == null)
            {
                logger.LogError("EOS Platform is not initialized");
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();

            // Create a session search handle
            var createSearchOptions = new CreateSessionSearchOptions
            {
                MaxSearchResults = (uint)config.MaxSearchResults
            };

            var createSearchResult = sessionsInterface.CreateSessionSearch(ref createSearchOptions, out var sessionSearch);
            if (createSearchResult != Result.Success || sessionSearch == null)
            {
                logger.LogError("Failed to create session search: {Result}", createSearchResult);
                throw new InvalidOperationException($"Failed to create session search: {createSearchResult}");
            }

            try
            {
                var setParameterOptions = new SessionSearchSetParameterOptions
                {
                    Parameter = new AttributeData
                    {
                        Key = SessionsInterface.SEARCH_BUCKET_ID,
                        Value = new AttributeDataValue { AsUtf8 = config.BucketId }
                    },
                    ComparisonOp = ComparisonOp.Equal
                };

                var setParamResult = sessionSearch.SetParameter(ref setParameterOptions);
                if (setParamResult != Result.Success)
                {
                    logger.LogError("Failed to set bucket ID search parameter: {Result}", setParamResult);
                    throw new InvalidOperationException($"Failed to set search parameter: {setParamResult}");
                }

                logger.LogDebug("Set search parameter: BucketId={BucketId}", config.BucketId);

                var findOptions = new SessionSearchFindOptions
                {
                    LocalUserId = null
                };

                var tcs = new TaskCompletionSource<Result>();
                sessionSearch.Find(ref findOptions, null, (ref SessionSearchFindCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;
                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    eosService.Tick();
                    await Task.Delay(1);
                }

                if (!tcs.Task.IsCompleted)
                {
                    logger.LogError("Session search timed out after {Timeout} seconds", timeout.TotalSeconds);
                    throw new TimeoutException($"Session search timed out after {timeout.TotalSeconds} seconds");
                }

                var findResult = await tcs.Task;
                if (findResult != Result.Success)
                {
                    logger.LogWarning("Session search returned no results: {Result}", findResult);
                    return (null, 0);
                }

                // Iterate through results and find first unclaimed session
                var getCountOptions = new SessionSearchGetSearchResultCountOptions();
                var resultCount = sessionSearch.GetSearchResultCount(ref getCountOptions);

                logger.LogDebug("Found {Count} sessions in search results", resultCount);

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
                            logger.LogWarning("Failed to copy search result at index {Index}: {Result}", i, copyResult);
                            continue;
                        }

                        var copyInfoOptions = new SessionDetailsCopyInfoOptions();
                        var infoResult = details.CopyInfo(ref copyInfoOptions, out var sessionInfo);
                        
                        if (infoResult != Result.Success || sessionInfo == null)
                        {
                            logger.LogWarning("Failed to copy session info at index {Index}: {Result}", i, infoResult);
                            continue;
                        }

                        var sessionId = sessionInfo.Value.SessionId?.ToString();
                        if (string.IsNullOrEmpty(sessionId))
                        {
                            logger.LogWarning("Session at index {Index} has no session ID", i);
                            continue;
                        }

                        if (claimedSessionsCache.IsSessionClaimed(sessionId))
                        {
                            logger.LogDebug("Session {SessionId} is already claimed, skipping", sessionId);
                            continue;
                        }

                        claimedSessionsCache.AddClaimedSession(sessionId);
                        logger.LogInformation("Found and claimed available session: {SessionId}", sessionId);
                        return (details, (int)resultCount);
                    }

                    logger.LogWarning("All {Count} found sessions are already claimed", resultCount);
                    return (null, (int)resultCount);
                }
            }
            finally
            {
                sessionSearch?.Release();
            }
        }


    }
}
