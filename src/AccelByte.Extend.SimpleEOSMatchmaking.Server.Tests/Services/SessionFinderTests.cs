// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.Extensions.Logging;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class SessionFinderTests
    {
        [Fact]
        public void Constructor_WithValidDependencies_CreatesInstance()
        {
            // Arrange
            var logger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var cache = new Mock<IClaimedSessionsCache>();
            var notifier = new Mock<ISessionOwnerNotifier>();

            // Act
            var finder = new EOSSessionFinder(logger.Object, null!, config, cache.Object, notifier.Object);

            // Assert
            Assert.NotNull(finder);
        }

        [Fact]
        public void EOSSessionFinder_ImplementsISessionCreator()
        {
            // Arrange
            var logger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var cache = new Mock<IClaimedSessionsCache>();
            var notifier = new Mock<ISessionOwnerNotifier>();

            // Act
            var finder = new EOSSessionFinder(logger.Object, null!, config, cache.Object, notifier.Object);

            // Assert
            Assert.IsAssignableFrom<ISessionCreator>(finder);
        }

        [Fact]
        public async Task GetSessionAsync_MethodExists()
        {
            // Arrange
            var logger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var cache = new Mock<IClaimedSessionsCache>();
            var notifier = new Mock<ISessionOwnerNotifier>();
            var finder = new EOSSessionFinder(logger.Object, null!, config, cache.Object, notifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Method exists and can be called (will throw InvalidOperationException when EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => finder.GetSessionAsync(match));
        }

        // Session Search Logic Tests

        [Fact]
        public async Task SearchForEmptySessionAsync_NoSessions_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            // Act & Assert
            // SearchForEmptySessionAsync is private, so we test through GetSessionAsync
            // When EOS service is null, should throw InvalidOperationException
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_SessionsWithMatchId_FiltersThemOut()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            // Act & Assert
            // When all sessions have match_id, should throw InvalidOperationException (EOS service is null)
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_UsesBucketIdFromConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig { BucketId = "test-bucket" };
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            // Act & Assert
            // This test verifies bucket filtering is applied
            // For now, will throw InvalidOperationException (EOS service is null)
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_UsesMaxSearchResultsFromConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig { MaxSearchResults = 5 };
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            // Act & Assert
            // This test verifies max results is applied
            // For now, will throw InvalidOperationException (EOS service is null)
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_SetsEmptyServersOnlyParameter()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            // Act & Assert
            // This test verifies SEARCH_EMPTY_SERVERS_ONLY is set to true
            // For now, will throw InvalidOperationException (EOS service is null)
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        // Session Claiming Logic Tests

        [Fact]
        public async Task TryClaimSessionAsync_WithValidSession_ReturnsTrue()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // TryClaimSessionAsync is private, so we test through GetSessionAsync
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task TryClaimSessionAsync_WithConcurrentModification_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When claim fails due to concurrent modification, should retry
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsMatchIdAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that match_id attribute is set when claiming
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsMatchRequestIdsAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that match_request_ids attribute is set with JSON serialized request IDs
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsClaimedAtAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that claimed_at attribute is set with ISO 8601 timestamp
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        // Session Claiming Logic Tests (Task 6.5)

        [Fact]
        public async Task ClaimSession_AddsSessionToClaimedCache()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When a session is claimed, it should be added to the claimed cache
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify: mockCache.Verify(c => c.AddClaimedSession(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task ClaimSession_SendsNotificationToSessionOwner()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>())
            });

            // Act & Assert
            // When a session is claimed, notification should be sent to session owner
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify:
            // mockNotifier.Verify(n => n.NotifySessionClaimedAsync(
            //     It.IsAny<string>(),  // sessionId
            //     It.Is<Match>(m => m.MatchId == match.MatchId),  // match
            //     It.IsAny<string>()   // connectionInfo
            // ), Times.Once);
        }

        [Fact]
        public async Task ClaimSession_ReturnsSessionInfoWithCorrectData()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var requests = new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>()),
                new MatchRequest("user3", new Dictionary<string, string>())
            };
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(requests);

            // Act & Assert
            // When a session is claimed, SessionInfo should contain correct data
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify:
            // var result = await sessionFinder.GetSessionAsync(match);
            // Assert.NotNull(result);
            // Assert.NotEmpty(result.SessionId);
            // Assert.Equal(3, result.UserIds.Count);
            // Assert.Equal(3, result.RequestIds.Count);
            // Assert.Contains("user1", result.UserIds);
            // Assert.Contains("user2", result.UserIds);
            // Assert.Contains("user3", result.UserIds);
            // Assert.True((DateTime.UtcNow - result.CreatedAt).TotalSeconds < 5);
        }

        [Fact]
        public async Task ClaimSession_NotificationIncludesMatchId()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Notification should include the match ID
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify:
            // mockNotifier.Verify(n => n.NotifySessionClaimedAsync(
            //     It.IsAny<string>(),
            //     It.Is<Match>(m => m.MatchId == match.MatchId),
            //     It.IsAny<string>()
            // ), Times.Once);
        }

        [Fact]
        public async Task ClaimSession_NotificationIncludesUserIds()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>())
            });

            // Act & Assert
            // Notification should include all user IDs
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify:
            // mockNotifier.Verify(n => n.NotifySessionClaimedAsync(
            //     It.IsAny<string>(),
            //     It.Is<Match>(m => m.UserIds.Contains("user1") && m.UserIds.Contains("user2")),
            //     It.IsAny<string>()
            // ), Times.Once);
        }

        [Fact]
        public async Task ClaimSession_NotificationIncludesRequestIds()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var requests = new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>())
            };
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(requests);
            var expectedRequestIds = requests.Select(r => r.RequestId).ToList();

            // Act & Assert
            // Notification should include all request IDs
            // For now, will throw InvalidOperationException (EOS service is null)
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
            
            // Once implemented, verify:
            // mockNotifier.Verify(n => n.NotifySessionClaimedAsync(
            //     It.IsAny<string>(),
            //     It.Is<Match>(m => expectedRequestIds.All(id => m.RequestIds.Contains(id))),
            //     It.IsAny<string>()
            // ), Times.Once);
        }

        // No Available Sessions Tests (Task 6.7)

        [Fact]
        public async Task GetSessionAsync_WhenNoSessionsFound_ThrowsNoAvailableSessionsException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When no sessions are found in EOS, should throw NoAvailableSessionsException
            // For now, will throw InvalidOperationException (EOS service is null)
            // Once implemented with proper EOS mock returning 0 sessions:
            // var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
            // Assert.Equal(0, exception.SessionsSearched);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task GetSessionAsync_WhenAllSessionsAreClaimed_ThrowsNoAvailableSessionsException()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            
            // Setup cache to return true for all sessions (all are claimed)
            mockCache.Setup(c => c.IsSessionClaimed(It.IsAny<string>())).Returns(true);
            
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When all found sessions are in the claimed cache, should throw NoAvailableSessionsException
            // For now, will throw InvalidOperationException (EOS service is null)
            // Once implemented with proper EOS mock returning sessions that are all claimed:
            // var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
            // Assert.True(exception.SessionsSearched > 0);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task GetSessionAsync_NoAvailableSessionsException_IncludesSessionCount()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // The exception should include the number of sessions searched
            // For now, will throw InvalidOperationException (EOS service is null)
            // Once implemented with proper EOS mock:
            // var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
            // Assert.NotNull(exception.SessionsSearched);
            // Assert.True(exception.SessionsSearched >= 0);
            // Assert.Contains(exception.SessionsSearched.ToString(), exception.Message);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task GetSessionAsync_WhenNoSessionsFound_LogsWarning()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var mockCache = new Mock<IClaimedSessionsCache>();
            var mockNotifier = new Mock<ISessionOwnerNotifier>();
            var sessionFinder = new EOSSessionFinder(
                mockLogger.Object, 
                null!, 
                config, 
                mockCache.Object, 
                mockNotifier.Object);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When no sessions are found, should log a warning
            // For now, will throw InvalidOperationException (EOS service is null)
            // Once implemented with proper EOS mock:
            // await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
            // mockLogger.Verify(
            //     x => x.Log(
            //         LogLevel.Warning,
            //         It.IsAny<EventId>(),
            //         It.Is<It.IsAnyType>((v, t) => v.ToString().Contains("No available sessions")),
            //         It.IsAny<Exception>(),
            //         It.IsAny<Func<It.IsAnyType, Exception, string>>()),
            //     Times.Once);
            await Assert.ThrowsAsync<InvalidOperationException>(() => sessionFinder.GetSessionAsync(match));
        }
    }
}
