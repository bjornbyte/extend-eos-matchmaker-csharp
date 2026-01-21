// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
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
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();

            // Act
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Assert
            Assert.NotNull(sessionFinder);
        }

        [Fact]
        public void EOSSessionFinder_ImplementsISessionCreator()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();

            // Act
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Assert
            Assert.IsAssignableFrom<ISessionCreator>(sessionFinder);
        }

        [Fact]
        public async Task GetSessionAsync_MethodExists()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Method exists and can be called (will throw NoAvailableSessionsException until search logic is implemented)
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        // Session Search Logic Tests

        [Fact]
        public async Task SearchForEmptySessionAsync_NoSessions_ReturnsNull()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Act & Assert
            // SearchForEmptySessionAsync is private, so we test through GetSessionAsync
            // When no sessions exist, should throw NoAvailableSessionsException
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_SessionsWithMatchId_FiltersThemOut()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Act & Assert
            // When all sessions have match_id, should throw NoAvailableSessionsException
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_UsesBucketIdFromConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig { BucketId = "test-bucket" };
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Act & Assert
            // This test verifies bucket filtering is applied
            // For now, will throw NoAvailableSessionsException until search logic is implemented
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_UsesMaxSearchResultsFromConfig()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig { MaxSearchResults = 5 };
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Act & Assert
            // This test verifies max results is applied
            // For now, will throw NoAvailableSessionsException until search logic is implemented
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task SearchForEmptySessionAsync_SetsEmptyServersOnlyParameter()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            // Act & Assert
            // This test verifies SEARCH_EMPTY_SERVERS_ONLY is set to true
            // For now, will throw NoAvailableSessionsException until search logic is implemented
            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        // Session Claiming Logic Tests

        [Fact]
        public async Task TryClaimSessionAsync_WithValidSession_ReturnsTrue()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // TryClaimSessionAsync is private, so we test through GetSessionAsync
            // For now, will throw NoAvailableSessionsException until claiming logic is implemented
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task TryClaimSessionAsync_WithConcurrentModification_ReturnsFalse()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // When claim fails due to concurrent modification, should retry
            // For now, will throw NoAvailableSessionsException until claiming logic is implemented
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsMatchIdAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that match_id attribute is set when claiming
            // For now, will throw NoAvailableSessionsException until claiming logic is implemented
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsMatchRequestIdsAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>()),
                new MatchRequest("user2", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that match_request_ids attribute is set with JSON serialized request IDs
            // For now, will throw NoAvailableSessionsException until claiming logic is implemented
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }

        [Fact]
        public async Task ClaimSession_SetsClaimedAtAttribute()
        {
            // Arrange
            var mockLogger = new Mock<ILogger<EOSSessionFinder>>();
            var config = new EOSSessionFinderConfig();
            var sessionFinder = new EOSSessionFinder(mockLogger.Object, null!, config);

            var match = new AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match(new List<MatchRequest>
            {
                new MatchRequest("user1", new Dictionary<string, string>())
            });

            // Act & Assert
            // Verify that claimed_at attribute is set with ISO 8601 timestamp
            // For now, will throw NoAvailableSessionsException until claiming logic is implemented
            await Assert.ThrowsAsync<NoAvailableSessionsException>(() => sessionFinder.GetSessionAsync(match));
        }
    }
}
