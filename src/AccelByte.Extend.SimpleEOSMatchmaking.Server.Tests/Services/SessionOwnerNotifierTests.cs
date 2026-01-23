using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Match = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match;
using MatchRequest = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequest;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class SessionOwnerNotifierTests
    {
        [Fact]
        public async Task NotifySessionClaimedAsync_CallsWithCorrectSessionId()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-123";
            var connectionInfo = "http://gameserver:8080";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(sessionId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_CallsWithCorrectMatchId()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-456";
            var connectionInfo = "http://gameserver:8080";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(match.MatchId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_CallsWithCorrectConnectionInfo()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-789";
            var connectionInfo = "http://gameserver:9090";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains(connectionInfo)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_IncludesAllUserIds()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-1"),
                new MatchRequest("user-2"),
                new MatchRequest("user-3")
            };
            var match = new Match(requests);
            var sessionId = "test-session-multi";
            var connectionInfo = "http://gameserver:8080";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => 
                        v.ToString().Contains("user-1") &&
                        v.ToString().Contains("user-2") &&
                        v.ToString().Contains("user-3")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_IncludesAllRequestIds()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-requests";
            var connectionInfo = "http://gameserver:8080";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            // Verify that the log contains request IDs
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => 
                        v.ToString().Contains(match.Requests[0].RequestId) &&
                        v.ToString().Contains(match.Requests[1].RequestId)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_ReturnsCompletedTask()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-completed";
            var connectionInfo = "http://gameserver:8080";

            // Act
            var task = notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            Assert.True(task.IsCompleted);
            await task; // Should not throw
        }

        [Fact]
        public async Task ISessionOwnerNotifier_Interface_CanBeImplemented()
        {
            // Arrange
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var match = CreateTestMatch();
            var sessionId = "test-session-interface";
            var connectionInfo = "http://gameserver:8080";

            mockNotifier
                .Setup(x => x.NotifySessionClaimedAsync(
                    It.IsAny<string>(), 
                    It.IsAny<Match>(), 
                    It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            await mockNotifier.Object.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockNotifier.Verify(
                x => x.NotifySessionClaimedAsync(sessionId, match, connectionInfo), 
                Times.Once);
        }

        [Fact]
        public async Task NotifySessionClaimedAsync_LogsStubMessage()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<StubSessionOwnerNotifier>>();
            var notifier = new StubSessionOwnerNotifier(mockLogger.Object);
            
            var match = CreateTestMatch();
            var sessionId = "test-session-stub";
            var connectionInfo = "http://gameserver:8080";

            // Act
            await notifier.NotifySessionClaimedAsync(sessionId, match, connectionInfo);

            // Assert
            mockLogger.Verify(
                x => x.Log(
                    LogLevel.Information,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("STUB")),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception, string>>()),
                Times.Once);
        }

        private Match CreateTestMatch()
        {
            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-1"),
                new MatchRequest("user-2")
            };
            return new Match(requests);
        }
    }
}
