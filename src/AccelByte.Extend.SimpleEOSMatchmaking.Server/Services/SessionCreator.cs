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
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Creates EOS sessions for matched players using the EOS SDK
    /// </summary>
    public class EosSessionProvider : ISessionProvider
    {
        private readonly ILogger<EosSessionProvider> Logger;
        private readonly EOSSDKService EosService;

        public EosSessionProvider(ILogger<EosSessionProvider> logger, EOSSDKService eosService)
        {
            Logger = logger;
            EosService = eosService;
        }

        public async Task<SessionInfo> GetSessionAsync(Match match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            if (match.Requests == null || match.Requests.Count == 0)
                throw new ArgumentException("Match must contain at least one request", nameof(match));

            var platform = EosService.Platform;
            if (platform == null)
            {
                Logger.LogError("EOS Platform is not initialized");
                throw new InvalidOperationException("EOS Platform is not initialized");
            }

            var sessionsInterface = platform.GetSessionsInterface();
            if (sessionsInterface == null)
            {
                Logger.LogError("Failed to get EOS Sessions Interface");
                throw new InvalidOperationException("Failed to get EOS Sessions Interface");
            }

            try
            {
                // Generate a unique session name
                var sessionName = $"match-{match.MatchId}";
                var sessionId = Guid.NewGuid().ToString();

                Logger.LogInformation("Creating EOS session: SessionName={SessionName}, SessionId={SessionId}, PlayerCount={PlayerCount}",
                    sessionName, sessionId, match.Requests.Count);
                
                // Create session modification handle
                var createOptions = new CreateSessionModificationOptions
                {
                    SessionName = sessionName,
                    BucketId = "default",
                    MaxPlayers = (uint)match.Requests.Count,
                    PresenceEnabled = false,
                    SessionId = sessionId,
                    SanctionsEnabled = false,
                    AllowedPlatformIds = null
                };

                var createResult = sessionsInterface.CreateSessionModification(ref createOptions, out var sessionModification);
                if (createResult != Result.Success)
                {
                    Logger.LogError("Failed to create session modification: {Result}", createResult);
                    throw new InvalidOperationException($"Failed to create session modification: {createResult}");
                }

                try
                {
                    // Add match request IDs as session attributes
                    var requestIds = match.Requests.Select(r => r.RequestId).ToList();
                    var requestIdsJson = System.Text.Json.JsonSerializer.Serialize(requestIds);

                    var addAttributeOptions = new SessionModificationAddAttributeOptions
                    {
                        SessionAttribute = new AttributeData
                        {
                            Key = "match_request_ids",
                            Value = new AttributeDataValue { AsUtf8 = requestIdsJson }
                        },
                        AdvertisementType = SessionAttributeAdvertisementType.DontAdvertise
                    };

                    var addAttributeResult = sessionModification.AddAttribute(ref addAttributeOptions);
                    if (addAttributeResult != Result.Success)
                    {
                        Logger.LogWarning("Failed to add match_request_ids attribute: {Result}", addAttributeResult);
                    }

                    // Add match ID as an attribute
                    var matchIdAttributeOptions = new SessionModificationAddAttributeOptions
                    {
                        SessionAttribute = new AttributeData
                        {
                            Key = "match_id",
                            Value = new AttributeDataValue { AsUtf8 = match.MatchId }
                        },
                        AdvertisementType = SessionAttributeAdvertisementType.DontAdvertise
                    };

                    var matchIdResult = sessionModification.AddAttribute(ref matchIdAttributeOptions);
                    if (matchIdResult != Result.Success)
                    {
                        Logger.LogWarning("Failed to add match_id attribute: {Result}", matchIdResult);
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
                    // EOS SDK requires regular Tick() calls to process callbacks
                    var timeout = TimeSpan.FromSeconds(10);
                    var startTime = DateTime.UtcNow;
                    
                    while (!tcs.Task.IsCompleted && (DateTime.UtcNow - startTime) < timeout)
                    {
                        EosService.Tick();
                        await Task.Delay(1); // Minimal delay - just yield to other tasks
                    }

                    if (!tcs.Task.IsCompleted)
                    {
                        Logger.LogError("Session creation timed out after {Timeout} seconds", timeout.TotalSeconds);
                        throw new TimeoutException($"Session creation timed out after {timeout.TotalSeconds} seconds");
                    }

                    var updateResult = await tcs.Task;
                    
                    if (updateResult != Result.Success)
                    {
                        Logger.LogError("Failed to update/create session: {Result}", updateResult);
                        throw new InvalidOperationException($"Failed to create session: {updateResult}");
                    }

                    Logger.LogInformation("Successfully created EOS session: SessionId={SessionId}", sessionId);

                    // Build and return session info
                    var sessionInfo = new SessionInfo
                    {
                        SessionId = sessionId,
                        RequestIds = requestIds,
                        UserIds = match.Requests.Select(r => r.UserId).ToList(),
                        CreatedAt = DateTime.UtcNow
                    };

                    return sessionInfo;
                }
                finally
                {
                    // Release the session modification handle
                    sessionModification?.Release();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error creating EOS session for match {MatchId}", match.MatchId);
                throw;
            }
        }
    }
}
