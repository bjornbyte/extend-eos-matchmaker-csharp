// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

using ModelMatch = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.Match;
using ModelMatchRequestStatus = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class MatchMakerTests
    {
        private readonly Mock<ISessionCreator> _mockSessionCreator;
        private readonly Mock<INotifier> _mockNotifier;
        private readonly Mock<ILogger<MatchMaker>> _mockLogger;

        public MatchMakerTests()
        {
            _mockSessionCreator = new Mock<ISessionCreator>();
            _mockNotifier = new Mock<INotifier>();
            _mockLogger = new Mock<ILogger<MatchMaker>>();
        }

        [Fact]
        public async Task TryMatchAsync_WithEnoughRequests_CreatesMatch()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");
            matchPool.Add(request1);
            matchPool.Add(request2);

            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { request1.RequestId, request2.RequestId },
                UserIds = new List<string> { "user1", "user2" },
                CreatedAt = DateTime.UtcNow
            };

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ReturnsAsync(sessionInfo);

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Single(matches);
            Assert.Equal(2, matches[0].Requests.Count);
            Assert.Equal(0, matchPool.Count); // Pool should be empty after match
            _mockSessionCreator.Verify(s => s.GetSessionAsync(It.IsAny<ModelMatch>()), Times.Once);
            _mockNotifier.Verify(n => n.NotifyMatchAsync(sessionInfo), Times.Once);
        }

        [Fact]
        public async Task TryMatchAsync_WithNotEnoughRequests_CreatesNoMatch()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            matchPool.Add(new MatchRequest("user1"));

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Empty(matches);
            Assert.Equal(1, matchPool.Count); // Request should still be in pool
            _mockSessionCreator.Verify(s => s.GetSessionAsync(It.IsAny<ModelMatch>()), Times.Never);
        }

        [Fact]
        public async Task TryMatchAsync_SessionCreationFails_ReturnsRequestsToPool()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");
            matchPool.Add(request1);
            matchPool.Add(request2);

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ThrowsAsync(new InvalidOperationException("Session creation failed"));

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Empty(matches);
            Assert.Equal(2, matchPool.Count); // Requests should be returned to pool
            _mockNotifier.Verify(n => n.NotifyMatchAsync(It.IsAny<SessionInfo>()), Times.Never);
        }

        [Fact]
        public async Task TryMatchAsync_RemovesExpiredRequests()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromMilliseconds(50) // 50ms timeout
            };

            var matchPool = new MatchPool();
            var expiredRequest = new MatchRequest("user1");
            matchPool.Add(expiredRequest);

            // Wait for request to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Empty(matches);
            Assert.Equal(0, matchPool.Count); // Expired request should be removed
        }

        [Fact]
        public async Task TryMatchAsync_UpdatesMatchRequestStatus()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");
            matchPool.Add(request1);
            matchPool.Add(request2);

            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { request1.RequestId, request2.RequestId },
                UserIds = new List<string> { "user1", "user2" },
                CreatedAt = DateTime.UtcNow
            };

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ReturnsAsync(sessionInfo);

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Equal(AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus.Matched, request1.Status);
            Assert.Equal(AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus.Matched, request2.Status);
            Assert.NotNull(request1.MatchedAt);
            Assert.NotNull(request2.MatchedAt);
            Assert.Equal("session-123", request1.SessionId);
            Assert.Equal("session-123", request2.SessionId);
        }

        [Fact]
        public async Task TryMatchAsync_SelectsOldestRequests()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            var request1 = new MatchRequest("user1");
            await Task.Delay(2); // Ensure different timestamps
            var request2 = new MatchRequest("user2");
            await Task.Delay(2);
            var request3 = new MatchRequest("user3");
            
            matchPool.Add(request1);
            matchPool.Add(request2);
            matchPool.Add(request3);

            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { request1.RequestId, request2.RequestId },
                UserIds = new List<string> { "user1", "user2" },
                CreatedAt = DateTime.UtcNow
            };

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ReturnsAsync(sessionInfo);

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store for this test
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Single(matches);
            Assert.Contains(matches[0].Requests, r => r.UserId == "user1"); // Oldest
            Assert.Contains(matches[0].Requests, r => r.UserId == "user2"); // Second oldest
            Assert.Equal(1, matchPool.Count); // user3 should remain in pool
        }

        [Fact]
        public async Task TryMatchAsync_MovesMatchedRequestsToCompletedStore()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60),
                RetentionPeriod = TimeSpan.FromSeconds(120)
            };

            var matchPool = new MatchPool();
            var completedStore = new CompletedRequestStore();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");
            matchPool.Add(request1);
            matchPool.Add(request2);

            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { request1.RequestId, request2.RequestId },
                UserIds = new List<string> { "user1", "user2" },
                CreatedAt = DateTime.UtcNow
            };

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ReturnsAsync(sessionInfo);

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                completedStore,
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Single(matches);
            Assert.Equal(0, matchPool.Count); // Pool should be empty
            Assert.Equal(2, completedStore.Count); // Both requests should be in completed store
            
            var completedRequest1 = completedStore.Get(request1.RequestId);
            var completedRequest2 = completedStore.Get(request2.RequestId);
            
            Assert.NotNull(completedRequest1);
            Assert.NotNull(completedRequest2);
            Assert.Equal(ModelMatchRequestStatus.Matched, completedRequest1.Status);
            Assert.Equal(ModelMatchRequestStatus.Matched, completedRequest2.Status);
            Assert.NotNull(completedRequest1.CompletedAt);
            Assert.NotNull(completedRequest2.CompletedAt);
        }

        [Fact]
        public async Task TryMatchAsync_MovesExpiredRequestsToCompletedStore()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromMilliseconds(50),
                RetentionPeriod = TimeSpan.FromSeconds(120)
            };

            var matchPool = new MatchPool();
            var completedStore = new CompletedRequestStore();
            var expiredRequest = new MatchRequest("user1");
            matchPool.Add(expiredRequest);

            // Wait for request to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                completedStore,
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Empty(matches);
            Assert.Equal(0, matchPool.Count); // Pool should be empty
            Assert.Equal(1, completedStore.Count); // Expired request should be in completed store
            
            var completedRequest = completedStore.Get(expiredRequest.RequestId);
            Assert.NotNull(completedRequest);
            Assert.Equal(ModelMatchRequestStatus.Expired, completedRequest.Status);
            Assert.NotNull(completedRequest.CompletedAt);
        }

        [Fact]
        public async Task TryMatchAsync_CleansUpExpiredCompletedRequests()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60),
                RetentionPeriod = TimeSpan.FromMilliseconds(50) // 50ms retention
            };

            var matchPool = new MatchPool();
            var completedStore = new CompletedRequestStore();
            
            // Add an old completed request
            var oldRequest = new MatchRequest("user1");
            oldRequest.Status = ModelMatchRequestStatus.Matched;
            oldRequest.CompletedAt = DateTime.UtcNow.AddSeconds(-1); // 1 second ago
            completedStore.Add(oldRequest);

            // Wait for retention period to expire
            await Task.Delay(TimeSpan.FromMilliseconds(100));

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                completedStore,
                config,
                _mockLogger.Object);

            // Act
            await matchMaker.TryMatchAsync();

            // Assert
            Assert.Equal(0, completedStore.Count); // Old completed request should be removed
            Assert.Null(completedStore.Get(oldRequest.RequestId));
        }

        [Fact]
        public async Task TryMatchAsync_WithNullCompletedStore_StillWorks()
        {
            // Arrange
            var config = new MatchMakerConfig
            {
                MatchSize = 2,
                TickInterval = TimeSpan.FromSeconds(1),
                RequestTimeout = TimeSpan.FromSeconds(60)
            };

            var matchPool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");
            matchPool.Add(request1);
            matchPool.Add(request2);

            var sessionInfo = new SessionInfo
            {
                SessionId = "session-123",
                RequestIds = new List<string> { request1.RequestId, request2.RequestId },
                UserIds = new List<string> { "user1", "user2" },
                CreatedAt = DateTime.UtcNow
            };

            _mockSessionCreator.Setup(s => s.GetSessionAsync(It.IsAny<ModelMatch>()))
                .ReturnsAsync(sessionInfo);

            var matchMaker = new MatchMaker(
                matchPool,
                _mockSessionCreator.Object,
                _mockNotifier.Object,
                null, // No completed store
                config,
                _mockLogger.Object);

            // Act
            var matches = await matchMaker.TryMatchAsync();

            // Assert
            Assert.Single(matches);
            Assert.Equal(0, matchPool.Count);
        }
    }
}
