// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
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

        public EOSSessionFinder(
            ILogger<EOSSessionFinder> logger,
            EOSSDKService eosService,
            EOSSessionFinderConfig config)
        {
            _logger = logger;
            _eosService = eosService;
            _config = config;
        }

        public async Task<SessionInfo> GetSessionAsync(Match match)
        {
            if (match == null)
                throw new ArgumentNullException(nameof(match));

            if (match.Requests == null || match.Requests.Count == 0)
                throw new ArgumentException("Match must contain at least one request", nameof(match));

            // For now, always throw NoAvailableSessionsException
            // Full implementation will be added in next iteration
            throw new NoAvailableSessionsException(0);
        }

        // TODO: Implement SearchForEmptySessionAsync in next iteration
        // This method will search for empty sessions without match_id attribute
    }
}
