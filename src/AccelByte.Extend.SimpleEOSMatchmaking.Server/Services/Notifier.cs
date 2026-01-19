// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Services
{
    /// <summary>
    /// Interface for notifying players when matches are found
    /// </summary>
    public interface INotifier
    {
        /// <summary>
        /// Notify players that a match has been found
        /// </summary>
        /// <param name="sessionInfo">Information about the created session</param>
        /// <returns>Task representing the async operation</returns>
        Task NotifyMatchAsync(SessionInfo sessionInfo);
    }

    /// <summary>
    /// Default no-op notifier implementation that logs match events
    /// </summary>
    public class LoggingNotifier : INotifier
    {
        private readonly ILogger<LoggingNotifier> _logger;

        public LoggingNotifier(ILogger<LoggingNotifier> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task NotifyMatchAsync(SessionInfo sessionInfo)
        {
            if (sessionInfo == null)
                throw new ArgumentNullException(nameof(sessionInfo));

            _logger.LogInformation(
                "Match created: SessionId={SessionId}, Users={Users}",
                sessionInfo.SessionId,
                string.Join(",", sessionInfo.UserIds));

            return Task.CompletedTask;
        }
    }
}
