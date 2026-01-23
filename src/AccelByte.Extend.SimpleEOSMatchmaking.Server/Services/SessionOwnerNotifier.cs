// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System.Linq;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Interface for notifying session owners that their session has been claimed
    /// </summary>
    public interface ISessionOwnerNotifier
    {
        /// <summary>
        /// Notify the session owner that their session has been claimed for a match
        /// </summary>
        /// <param name="sessionId">The ID of the claimed session</param>
        /// <param name="match">The match information including user IDs and request IDs</param>
        /// <param name="connectionInfo">Connection information for the session owner</param>
        Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo);
    }

    /// <summary>
    /// Stub implementation of session owner notifier
    /// Developers should extend this for their game's specific notification mechanism
    /// </summary>
    public class StubSessionOwnerNotifier : ISessionOwnerNotifier
    {
        private readonly ILogger<StubSessionOwnerNotifier> _logger;

        public StubSessionOwnerNotifier(ILogger<StubSessionOwnerNotifier> logger)
        {
            _logger = logger;
        }

        public Task NotifySessionClaimedAsync(string sessionId, Match match, string connectionInfo)
        {
            var userIds = string.Join(",", match.Requests.Select(r => r.UserId));
            var requestIds = string.Join(",", match.Requests.Select(r => r.RequestId));

            _logger.LogInformation(
                "STUB: Session claimed notification - SessionId={SessionId}, MatchId={MatchId}, " +
                "ConnectionInfo={ConnectionInfo}, UserIds={UserIds}, RequestIds={RequestIds}",
                sessionId,
                match.MatchId,
                connectionInfo,
                userIds,
                requestIds);

            // TODO: Implement actual notification mechanism for your game
            // Examples:
            // - HTTP POST to game server using connectionInfo
            // - Message queue (RabbitMQ, AWS SQS, etc.)
            // - gRPC call to game server
            // - WebSocket message

            return Task.CompletedTask;
        }
    }
}
