// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Epic.OnlineServices;
using Epic.OnlineServices.Sessions;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Helpers
{
    /// <summary>
    /// Test helper for creating and managing empty EOS sessions for integration tests.
    /// This helper creates sessions without match_id attributes to simulate pre-created
    /// sessions that are available for the SessionFinder to claim.
    /// </summary>
    public class EmptySessionTestHelper
    {
        private readonly ILogger<EmptySessionTestHelper> _logger;
        private readonly EOSSDKService _eosService;
        private readonly List<string> _createdSessionIds = new();

        public EmptySessionTestHelper(ILogger<EmptySessionTestHelper> logger, EOSSDKService eosService)
        {
            _logger = logger;
            _eosService = eosService;
        }

        /// <summary>
        /// Creates an empty EOS session without a match_id attribute.
        /// This simulates a session created by a game server that is waiting for players.
        /// </summary>
        /// <param name="bucketId">The bucket ID for the session (default: "default")</param>
        /// <param name="maxPlayers">Maximum number of players (default: 4)</param>
        /// <returns>The session ID of the created session</returns>
        public async Task<string> CreateEmptySessionAsync(string bucketId = "default", uint maxPlayers = 4)
        {
            var platform = _eosService.Platform;
            if (platform == null)
            {
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                throw new InvalidOperationException("Failed to get EOS Sessions Interface");
            }

            var sessionId = Guid.NewGuid().ToString();
            var sessionName = $"test-empty-{sessionId}";

            _logger.LogInformation("Creating empty test session: SessionId={SessionId}, BucketId={BucketId}", 
                sessionId, bucketId);

            // Create session modification handle
            var createOptions = new CreateSessionModificationOptions
            {
                SessionName = sessionName,
                BucketId = bucketId,
                MaxPlayers = maxPlayers,
                PresenceEnabled = false,
                SessionId = sessionId,
                SanctionsEnabled = false,
                AllowedPlatformIds = null
            };

            var createResult = sessionsInterface.CreateSessionModification(ref createOptions, out var sessionModification);
            if (createResult != Result.Success)
            {
                throw new InvalidOperationException($"Failed to create session modification: {createResult}");
            }

            try
            {
                // Set permission level to PublicAdvertised so the session appears in search results
                var permissionOptions = new SessionModificationSetPermissionLevelOptions
                {
                    PermissionLevel = OnlineSessionPermissionLevel.PublicAdvertised
                };

                var permissionResult = sessionModification.SetPermissionLevel(ref permissionOptions);
                if (permissionResult != Result.Success)
                {
                    _logger.LogWarning("Failed to set permission level: {Result}", permissionResult);
                }

                // Update the session (create it) - WITHOUT adding match_id attribute
                var updateOptions = new UpdateSessionOptions
                {
                    SessionModificationHandle = sessionModification
                };

                var tcs = new TaskCompletionSource<Result>();

                sessionsInterface.UpdateSession(ref updateOptions, null, (ref UpdateSessionCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                // Tick the platform while waiting for the callback
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;

                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    _eosService.Tick();
                    await Task.Delay(100);
                }

                if (!tcs.Task.IsCompleted)
                {
                    throw new TimeoutException($"Session creation timed out after {timeout.TotalSeconds} seconds");
                }

                var updateResult = await tcs.Task;

                if (updateResult != Result.Success)
                {
                    throw new InvalidOperationException($"Failed to create empty session: {updateResult}");
                }

                _createdSessionIds.Add(sessionId);
                _logger.LogInformation("Successfully created empty test session: SessionId={SessionId}", sessionId);

                return sessionId;
            }
            finally
            {
                sessionModification?.Release();
            }
        }

        /// <summary>
        /// Creates an empty EOS session WITH a match_id attribute.
        /// This is used for negative tests to verify that the SessionFinder
        /// correctly excludes sessions that are already claimed.
        /// </summary>
        /// <param name="matchId">The match ID to set on the session</param>
        /// <param name="bucketId">The bucket ID for the session (default: "default")</param>
        /// <param name="maxPlayers">Maximum number of players (default: 4)</param>
        /// <returns>The session ID of the created session</returns>
        public async Task<string> CreateClaimedSessionAsync(string matchId, string bucketId = "default", uint maxPlayers = 4)
        {
            var platform = _eosService.Platform;
            if (platform == null)
            {
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                throw new InvalidOperationException("Failed to get EOS Sessions Interface");
            }

            var sessionId = Guid.NewGuid().ToString();
            var sessionName = $"test-claimed-{sessionId}";

            _logger.LogInformation("Creating claimed test session: SessionId={SessionId}, MatchId={MatchId}, BucketId={BucketId}",
                sessionId, matchId, bucketId);

            // Create session modification handle
            var createOptions = new CreateSessionModificationOptions
            {
                SessionName = sessionName,
                BucketId = bucketId,
                MaxPlayers = maxPlayers,
                PresenceEnabled = false,
                SessionId = sessionId,
                SanctionsEnabled = false,
                AllowedPlatformIds = null
            };

            var createResult = sessionsInterface.CreateSessionModification(ref createOptions, out var sessionModification);
            if (createResult != Result.Success)
            {
                throw new InvalidOperationException($"Failed to create session modification: {createResult}");
            }

            try
            {
                // Set permission level to PublicAdvertised so the session appears in search results
                var permissionOptions = new SessionModificationSetPermissionLevelOptions
                {
                    PermissionLevel = OnlineSessionPermissionLevel.PublicAdvertised
                };

                var permissionResult = sessionModification.SetPermissionLevel(ref permissionOptions);
                if (permissionResult != Result.Success)
                {
                    _logger.LogWarning("Failed to set permission level: {Result}", permissionResult);
                }

                // Add match_id attribute to mark session as claimed
                var matchIdAttributeOptions = new SessionModificationAddAttributeOptions
                {
                    SessionAttribute = new AttributeData
                    {
                        Key = "match_id",
                        Value = new AttributeDataValue { AsUtf8 = matchId }
                    },
                    AdvertisementType = SessionAttributeAdvertisementType.DontAdvertise
                };

                var matchIdResult = sessionModification.AddAttribute(ref matchIdAttributeOptions);
                if (matchIdResult != Result.Success)
                {
                    throw new InvalidOperationException($"Failed to add match_id attribute: {matchIdResult}");
                }

                // Update the session (create it)
                var updateOptions = new UpdateSessionOptions
                {
                    SessionModificationHandle = sessionModification
                };

                var tcs = new TaskCompletionSource<Result>();

                sessionsInterface.UpdateSession(ref updateOptions, null, (ref UpdateSessionCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                // Tick the platform while waiting for the callback
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;

                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    _eosService.Tick();
                    await Task.Delay(100);
                }

                if (!tcs.Task.IsCompleted)
                {
                    throw new TimeoutException($"Session creation timed out after {timeout.TotalSeconds} seconds");
                }

                var updateResult = await tcs.Task;

                if (updateResult != Result.Success)
                {
                    throw new InvalidOperationException($"Failed to create claimed session: {updateResult}");
                }

                _createdSessionIds.Add(sessionId);
                _logger.LogInformation("Successfully created claimed test session: SessionId={SessionId}, MatchId={MatchId}",
                    sessionId, matchId);

                return sessionId;
            }
            finally
            {
                sessionModification?.Release();
            }
        }

        /// <summary>
        /// Creates an empty EOS session and starts it (marks it as InProgress).
        /// This is used for negative tests to verify that the SessionFinder
        /// correctly excludes sessions that are already started.
        /// </summary>
        /// <param name="bucketId">The bucket ID for the session (default: "default")</param>
        /// <param name="maxPlayers">Maximum number of players (default: 4)</param>
        /// <returns>The session ID of the created and started session</returns>
        public async Task<string> CreateStartedSessionAsync(string bucketId = "default", uint maxPlayers = 4)
        {
            // First create an empty session
            var sessionId = await CreateEmptySessionAsync(bucketId, maxPlayers);

            var platform = _eosService.Platform;
            if (platform == null)
            {
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                throw new InvalidOperationException("Failed to get EOS Sessions Interface");
            }

            var sessionName = $"test-empty-{sessionId}";

            _logger.LogInformation("Starting test session: SessionId={SessionId}", sessionId);

            try
            {
                // Start the session to mark it as InProgress
                var startOptions = new StartSessionOptions
                {
                    SessionName = sessionName
                };

                var tcs = new TaskCompletionSource<Result>();

                sessionsInterface.StartSession(ref startOptions, null, (ref StartSessionCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                // Tick the platform while waiting for the callback
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;

                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    _eosService.Tick();
                    await Task.Delay(100);
                }

                if (!tcs.Task.IsCompleted)
                {
                    throw new TimeoutException($"Session start timed out after {timeout.TotalSeconds} seconds");
                }

                var startResult = await tcs.Task;

                if (startResult != Result.Success)
                {
                    throw new InvalidOperationException($"Failed to start session: {startResult}");
                }

                _logger.LogInformation("Successfully started test session: SessionId={SessionId}", sessionId);

                return sessionId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error starting test session: SessionId={SessionId}", sessionId);
                // Try to cleanup the session if start failed
                await DestroySessionAsync(sessionId);
                throw;
            }
        }

        /// <summary>
        /// Destroys a specific test session by session ID.
        /// </summary>
        /// <param name="sessionId">The session ID to destroy</param>
        public async Task DestroySessionAsync(string sessionId)
        {
            var platform = _eosService.Platform;
            if (platform == null)
            {
                _logger.LogWarning("EOS Platform is not initialized, cannot destroy session: {SessionId}", sessionId);
                return;
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                _logger.LogWarning("Failed to get EOS Sessions Interface, cannot destroy session: {SessionId}", sessionId);
                return;
            }

            try
            {
                _logger.LogInformation("Destroying test session: SessionId={SessionId}", sessionId);

                var destroyOptions = new DestroySessionOptions
                {
                    SessionName = $"test-empty-{sessionId}" // Try empty session name first
                };

                var tcs = new TaskCompletionSource<Result>();

                sessionsInterface.DestroySession(ref destroyOptions, null, (ref DestroySessionCallbackInfo callbackInfo) =>
                {
                    tcs.SetResult(callbackInfo.ResultCode);
                });

                // Tick the platform while waiting for the callback
                var timeout = TimeSpan.FromSeconds(10);
                var startTime = DateTime.UtcNow;

                while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                {
                    _eosService.Tick();
                    await Task.Delay(100);
                }

                if (!tcs.Task.IsCompleted)
                {
                    _logger.LogWarning("Session destruction timed out: SessionId={SessionId}", sessionId);
                    return;
                }

                var destroyResult = await tcs.Task;

                if (destroyResult == Result.Success)
                {
                    _logger.LogInformation("Successfully destroyed test session: SessionId={SessionId}", sessionId);
                    _createdSessionIds.Remove(sessionId);
                }
                else if (destroyResult == Result.NotFound)
                {
                    // Try with claimed session name
                    destroyOptions = new DestroySessionOptions
                    {
                        SessionName = $"test-claimed-{sessionId}"
                    };

                    tcs = new TaskCompletionSource<Result>();

                    sessionsInterface.DestroySession(ref destroyOptions, null, (ref DestroySessionCallbackInfo callbackInfo) =>
                    {
                        tcs.SetResult(callbackInfo.ResultCode);
                    });

                    startTime = DateTime.UtcNow;

                    while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                    {
                        _eosService.Tick();
                        await Task.Delay(100);
                    }

                    if (tcs.Task.IsCompleted)
                    {
                        destroyResult = await tcs.Task;
                        if (destroyResult == Result.Success)
                        {
                            _logger.LogInformation("Successfully destroyed test session: SessionId={SessionId}", sessionId);
                            _createdSessionIds.Remove(sessionId);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to destroy test session: SessionId={SessionId}, Result={Result}",
                                sessionId, destroyResult);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("Failed to destroy test session: SessionId={SessionId}, Result={Result}",
                        sessionId, destroyResult);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error destroying test session: SessionId={SessionId}", sessionId);
            }
        }

        /// <summary>
        /// Destroys all test sessions created by this helper.
        /// Should be called in test cleanup to ensure no sessions are left behind.
        /// </summary>
        public async Task CleanupAllSessionsAsync()
        {
            _logger.LogInformation("Cleaning up {Count} test sessions", _createdSessionIds.Count);

            var sessionIdsToCleanup = _createdSessionIds.ToList();
            foreach (var sessionId in sessionIdsToCleanup)
            {
                await DestroySessionAsync(sessionId);
            }

            _createdSessionIds.Clear();
            _logger.LogInformation("Test session cleanup complete");
        }

        /// <summary>
        /// Gets the list of session IDs created by this helper.
        /// </summary>
        public IReadOnlyList<string> CreatedSessionIds => _createdSessionIds.AsReadOnly();

        /// <summary>
        /// Searches for and destroys ALL sessions in the specified bucket.
        /// This is useful for cleaning up leftover sessions from previous test runs.
        /// </summary>
        /// <param name="bucketId">The bucket ID to clean up (default: "default")</param>
        public async Task CleanupAllSessionsInBucketAsync(string bucketId = "default")
        {
            var platform = _eosService.Platform;
            if (platform == null)
            {
                _logger.LogWarning("EOS Platform is not initialized, cannot cleanup sessions");
                return;
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                _logger.LogWarning("Failed to get EOS Sessions Interface, cannot cleanup sessions");
                return;
            }

            try
            {
                _logger.LogInformation("Searching for all sessions in bucket: {BucketId}", bucketId);

                // Create search handle
                var createSearchOptions = new CreateSessionSearchOptions
                {
                    MaxSearchResults = 100
                };

                var createResult = sessionsInterface.CreateSessionSearch(ref createSearchOptions, out var searchHandle);
                if (createResult != Result.Success)
                {
                    _logger.LogWarning("Failed to create session search: {Result}", createResult);
                    return;
                }

                try
                {
                    // Set bucket ID parameter
                    var bucketIdParam = new SessionSearchSetParameterOptions
                    {
                        Parameter = new AttributeData
                        {
                            Key = "bucket",
                            Value = new AttributeDataValue { AsUtf8 = bucketId }
                        },
                        ComparisonOp = ComparisonOp.Equal
                    };

                    var paramResult = searchHandle.SetParameter(ref bucketIdParam);
                    if (paramResult != Result.Success)
                    {
                        _logger.LogWarning("Failed to set bucket parameter: {Result}", paramResult);
                        return;
                    }

                    // Execute search
                    var findOptions = new SessionSearchFindOptions();
                    var tcs = new TaskCompletionSource<Result>();

                    searchHandle.Find(ref findOptions, null, (ref SessionSearchFindCallbackInfo callbackInfo) =>
                    {
                        tcs.SetResult(callbackInfo.ResultCode);
                    });

                    // Tick the platform while waiting for the callback
                    var timeout = TimeSpan.FromSeconds(10);
                    var startTime = DateTime.UtcNow;

                    while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                    {
                        _eosService.Tick();
                        await Task.Delay(100);
                    }

                    if (!tcs.Task.IsCompleted)
                    {
                        _logger.LogWarning("Session search timed out");
                        return;
                    }

                    var findResult = await tcs.Task;
                    if (findResult != Result.Success)
                    {
                        _logger.LogWarning("Session search failed: {Result}", findResult);
                        return;
                    }

                    // Get search results
                    var countOptions = new SessionSearchGetSearchResultCountOptions();
                    var resultCount = searchHandle.GetSearchResultCount(ref countOptions);

                    _logger.LogInformation("Found {Count} sessions to cleanup", resultCount);

                    // Destroy each session
                    for (uint i = 0; i < resultCount; i++)
                    {
                        var copyOptions = new SessionSearchCopySearchResultByIndexOptions
                        {
                            SessionIndex = i
                        };

                        var copyResult = searchHandle.CopySearchResultByIndex(ref copyOptions, out var sessionDetails);
                        if (copyResult != Result.Success)
                        {
                            _logger.LogWarning("Failed to copy search result {Index}: {Result}", i, copyResult);
                            continue;
                        }

                        try
                        {
                            if (sessionDetails != null)
                            {
                                var copyInfoOptions = new SessionDetailsCopyInfoOptions();
                                var infoResult = sessionDetails.CopyInfo(ref copyInfoOptions, out var sessionInfo);
                                
                                if (infoResult == Result.Success && sessionInfo != null)
                                {
                                    var sessionId = sessionInfo.Value.SessionId;
                                    var sessionName = $"test-empty-{sessionId}";

                                    _logger.LogInformation("Destroying session: SessionId={SessionId}", sessionId);

                                    var destroyOptions = new DestroySessionOptions
                                    {
                                        SessionName = sessionName
                                    };

                                    var destroyTcs = new TaskCompletionSource<Result>();

                                    sessionsInterface.DestroySession(ref destroyOptions, null, (ref DestroySessionCallbackInfo callbackInfo) =>
                                    {
                                        destroyTcs.SetResult(callbackInfo.ResultCode);
                                    });

                                    startTime = DateTime.UtcNow;

                                    while (!destroyTcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                                    {
                                        _eosService.Tick();
                                        await Task.Delay(100);
                                    }

                                    if (destroyTcs.Task.IsCompleted)
                                    {
                                        var destroyResult = await destroyTcs.Task;
                                        if (destroyResult == Result.Success)
                                        {
                                            _logger.LogInformation("Successfully destroyed session: SessionId={SessionId}", sessionId);
                                        }
                                        else if (destroyResult == Result.NotFound)
                                        {
                                            // Try with claimed session name
                                            sessionName = $"test-claimed-{sessionId}";
                                            destroyOptions = new DestroySessionOptions
                                            {
                                                SessionName = sessionName
                                            };

                                            destroyTcs = new TaskCompletionSource<Result>();

                                            sessionsInterface.DestroySession(ref destroyOptions, null, (ref DestroySessionCallbackInfo callbackInfo) =>
                                            {
                                                destroyTcs.SetResult(callbackInfo.ResultCode);
                                            });

                                            startTime = DateTime.UtcNow;

                                            while (!destroyTcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                                            {
                                                _eosService.Tick();
                                                await Task.Delay(100);
                                            }

                                            if (destroyTcs.Task.IsCompleted)
                                            {
                                                destroyResult = await destroyTcs.Task;
                                                if (destroyResult == Result.Success)
                                                {
                                                    _logger.LogInformation("Successfully destroyed session: SessionId={SessionId}", sessionId);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            sessionDetails?.Release();
                        }
                    }

                    _logger.LogInformation("Bucket cleanup complete");
                }
                finally
                {
                    searchHandle?.Release();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cleaning up sessions in bucket: {BucketId}", bucketId);
            }
        }
    }
}
