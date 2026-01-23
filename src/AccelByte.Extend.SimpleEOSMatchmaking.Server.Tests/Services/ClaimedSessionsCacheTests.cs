// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class ClaimedSessionsCacheTests
    {
        private readonly Mock<ILogger<InMemoryClaimedSessionsCache>> _mockLogger;
        private readonly EOSSessionFinderConfig _config;

        public ClaimedSessionsCacheTests()
        {
            _mockLogger = new Mock<ILogger<InMemoryClaimedSessionsCache>>();
            _config = new EOSSessionFinderConfig
            {
                ClaimedSessionExpirationSeconds = 2 // Short expiration for testing
            };
        }

        [Fact]
        public void IsSessionClaimed_ReturnsFalse_ForUnclaimedSession()
        {
            // Arrange
            var cache = new InMemoryClaimedSessionsCache(_mockLogger.Object, _config);
            var sessionId = "unclaimed-session-123";

            // Act
            var result = cache.IsSessionClaimed(sessionId);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void IsSessionClaimed_ReturnsTrue_ForClaimedSession()
        {
            // Arrange
            var cache = new InMemoryClaimedSessionsCache(_mockLogger.Object, _config);
            var sessionId = "claimed-session-456";

            // Act
            cache.AddClaimedSession(sessionId);
            var result = cache.IsSessionClaimed(sessionId);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void AddClaimedSession_AddsSessionToCache()
        {
            // Arrange
            var cache = new InMemoryClaimedSessionsCache(_mockLogger.Object, _config);
            var sessionId = "new-session-789";

            // Act
            cache.AddClaimedSession(sessionId);

            // Assert
            Assert.True(cache.IsSessionClaimed(sessionId));
        }

        [Fact]
        public async Task RemoveExpiredEntries_RemovesOldEntries()
        {
            // Arrange
            var cache = new InMemoryClaimedSessionsCache(_mockLogger.Object, _config);
            var sessionId = "expired-session-101";

            // Act
            cache.AddClaimedSession(sessionId);
            Assert.True(cache.IsSessionClaimed(sessionId)); // Should be claimed initially

            // Wait for expiration (config is set to 2 seconds)
            await Task.Delay(TimeSpan.FromSeconds(2.5));

            // RemoveExpiredEntries is called automatically by IsSessionClaimed
            var result = cache.IsSessionClaimed(sessionId);

            // Assert
            Assert.False(result); // Should be expired and removed
        }

        [Fact]
        public void AddClaimedSession_UpdatesTimestamp_ForExistingSession()
        {
            // Arrange
            var cache = new InMemoryClaimedSessionsCache(_mockLogger.Object, _config);
            var sessionId = "existing-session-202";

            // Act
            cache.AddClaimedSession(sessionId);
            cache.AddClaimedSession(sessionId); // Add again to update timestamp

            // Assert
            Assert.True(cache.IsSessionClaimed(sessionId));
        }
    }
}
