using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class NotifierTests
    {
        [Fact]
        public async Task LoggingNotifier_NotifyMatchAsync_LogsMatchInformation()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingNotifier>>();
            var notifier = new LoggingNotifier(mockLogger.Object);
            
            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { "req-1", "req-2" },
                UserIds = new List<string> { "user-1", "user-2" },
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await notifier.NotifyMatchAsync(sessionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Match created") && 
                                                   v.ToString().Contains("session-123")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LoggingNotifier_NotifyMatchAsync_WithNullSessionInfo_ThrowsArgumentNullException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingNotifier>>();
            var notifier = new LoggingNotifier(mockLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() => 
                notifier.NotifyMatchAsync(null));
        }

        [Fact]
        public async Task LoggingNotifier_NotifyMatchAsync_CompletesSuccessfully()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingNotifier>>();
            var notifier = new LoggingNotifier(mockLogger.Object);
            
            var sessionInfo = new SessionInfo
            {
                SessionId = "session-456",
                RequestIds = new List<string> { "req-3", "req-4" },
                UserIds = new List<string> { "user-3", "user-4" },
                CreatedAt = DateTime.UtcNow
            };

            // Act
            var task = notifier.NotifyMatchAsync(sessionInfo);

            // Assert
            Assert.True(task.IsCompleted);
            await task; // Should not throw
        }

        [Fact]
        public async Task INotifier_Interface_CanBeImplemented()
        {
            // Arrange
            var mockNotifier = new Mock<INotifier>();
            var sessionInfo = new SessionInfo
            {
                SessionId = "session-789",
                RequestIds = new List<string> { "req-5" },
                UserIds = new List<string> { "user-5" },
                CreatedAt = DateTime.UtcNow
            };

            mockNotifier
                .Setup(x => x.NotifyMatchAsync(It.IsAny<SessionInfo>()))
                .Returns(Task.CompletedTask);

            // Act
            await mockNotifier.Object.NotifyMatchAsync(sessionInfo);

            // Assert
            mockNotifier.Verify(x => x.NotifyMatchAsync(sessionInfo), Times.Once);
        }

        [Fact]
        public async Task LoggingNotifier_NotifyMatchAsync_LogsAllUserIds()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingNotifier>>();
            var notifier = new LoggingNotifier(mockLogger.Object);
            
            var sessionInfo = new SessionInfo
            {
                SessionId = "session-multi",
                RequestIds = new List<string> { "req-1", "req-2", "req-3" },
                UserIds = new List<string> { "user-1", "user-2", "user-3" },
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await notifier.NotifyMatchAsync(sessionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("user-1") && 
                                                   v.ToString().Contains("user-2") &&
                                                   v.ToString().Contains("user-3")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task LoggingNotifier_NotifyMatchAsync_WithEmptyUserIds_StillLogs()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<LoggingNotifier>>();
            var notifier = new LoggingNotifier(mockLogger.Object);
            
            var sessionInfo = new SessionInfo
            {
                SessionId = "session-empty",
                RequestIds = new List<string>(),
                UserIds = new List<string>(),
                CreatedAt = DateTime.UtcNow
            };

            // Act
            await notifier.NotifyMatchAsync(sessionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("Match created")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }
    }
}
