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
    /// APPLICATION-LEVEL EXTENSION POINT: Interface for notifying session owners that their session has been claimed.
    /// 
    /// This extension point is ONLY used in "find" mode where game servers create their own EOS sessions
    /// and wait for the matchmaker to assign players to them.
    /// 
    /// When the matchmaker finds an available session and assigns players to it, the session owner
    /// (game server) must be notified so they can:
    /// - Update the EOS session state to "started"
    /// - Prepare for incoming player connections
    /// - Load the appropriate game mode/map
    /// 
    /// Common implementations:
    /// - HTTP POST to game server webhook endpoint
    /// - Message queue (RabbitMQ, AWS SQS, Azure Service Bus)
    /// - gRPC call to game server
    /// - WebSocket message over persistent connection
    /// - Database write that servers poll
    /// 
    /// When to implement:
    /// - When using "find" mode (SessionProvider:Mode = "find")
    /// - When game servers need to know which players are joining
    /// - When servers need to prepare before players connect
    /// 
    /// NOT needed when:
    /// - Using "create" mode (matchmaker creates sessions)
    /// - P2P gameplay without dedicated servers
    /// 
    /// See docs/architecture.md#session-owner-notification for complete examples.
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
    /// Stub implementation of session owner notifier.
    /// 
    /// TODO: Replace this with your game server notification mechanism.
    /// 
    /// This stub implementation only logs to console and does not actually notify game servers.
    /// This is ONLY used in "find" mode where game servers create their own EOS sessions.
    /// 
    /// For production use in "find" mode, implement ISessionOwnerNotifier with your notification mechanism:
    /// - HTTP POST to game server webhook (using connectionInfo as endpoint)
    /// - Message queue (RabbitMQ, AWS SQS, Azure Service Bus)
    /// - gRPC call to game server
    /// - WebSocket message over persistent connection
    /// - Database write that servers poll
    /// 
    /// The notification should include:
    /// - sessionId: Which session was claimed
    /// - match.MatchId: Unique match identifier
    /// - match.UserIds: List of players joining
    /// - connectionInfo: Server's connection endpoint
    /// 
    /// See docs/architecture.md#session-owner-notification for complete examples.
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
