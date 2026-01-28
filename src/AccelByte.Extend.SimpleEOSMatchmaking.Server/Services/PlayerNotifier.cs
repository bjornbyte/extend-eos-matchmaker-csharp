// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// APPLICATION-LEVEL EXTENSION POINT: Interface for notifying players when matches are found.
    /// 
    /// This is an extension point that game developers should implement to provide custom notification
    /// mechanisms for their game. The default implementation (LoggingPlayerNotifier) only logs to console.
    /// 
    /// Common implementations:
    /// - HTTP webhooks to game backend services
    /// - Push notifications (Firebase Cloud Messaging, Apple Push Notification Service)
    /// - Message queues (RabbitMQ, AWS SQS, Azure Service Bus)
    /// - WebSocket connections for real-time client notifications
    /// - AccelByte Lobby service integration
    /// 
    /// When to implement:
    /// - When you need to notify players outside of polling GetMatchStatus
    /// - When you want to integrate with your existing notification infrastructure
    /// - When you need real-time match notifications
    /// 
    /// See docs/architecture.md for complete implementation examples.
    /// </summary>
    public interface IPlayerNotifier
    {
        /// <summary>
        /// Notify players that a match has been found
        /// </summary>
        /// <param name="sessionInfo">Information about the created session</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyMatchAsync(SessionInfo sessionInfo);
    }

    /// <summary>
    /// Default no-op notifier implementation that logs match events.
    /// 
    /// TODO: Replace this with your game-specific notification implementation.
    /// 
    /// This stub implementation only logs to console and does not actually notify players.
    /// For production use, implement IPlayerNotifier with your notification mechanism:
    /// - HTTP webhooks to your game backend
    /// - Push notifications (Firebase, APNS)
    /// - Message queues (RabbitMQ, AWS SQS)
    /// - WebSocket connections
    /// - AccelByte Lobby service
    /// 
    /// See docs/architecture.md for complete implementation examples.
    /// </summary>
    public class LoggingPlayerNotifier : IPlayerNotifier
    {
        private readonly ILogger<LoggingPlayerNotifier> Logger;

        public LoggingPlayerNotifier(ILogger<LoggingPlayerNotifier> logger)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task NotifyMatchAsync(SessionInfo sessionInfo)
        {
            if (sessionInfo == null)
                throw new ArgumentNullException(nameof(sessionInfo));

            Logger.LogInformation(
                "Match created: SessionId={SessionId}, Users={Users}",
                sessionInfo.SessionId,
                string.Join(",", sessionInfo.UserIds));

            return Task.CompletedTask;
        }
    }
}
