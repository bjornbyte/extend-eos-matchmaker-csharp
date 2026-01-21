using System;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Xunit;
using ModelMatchRequestStatus = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    public class CompletedRequestStoreTests
    {
        [Fact]
        public void Add_ShouldStoreCompletedRequest()
        {
            // Arrange
            var store = new CompletedRequestStore();
            var request = new MatchRequest("user1");
            request.Status = ModelMatchRequestStatus.Matched;

            // Act
            store.Add(request);

            // Assert
            Assert.Equal(1, store.Count);
        }

        [Fact]
        public void Get_ShouldReturnStoredRequest()
        {
            // Arrange
            var store = new CompletedRequestStore();
            var request = new MatchRequest("user1");
            request.Status = ModelMatchRequestStatus.Matched;
            store.Add(request);

            // Act
            var retrieved = store.Get(request.RequestId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(request.RequestId, retrieved.RequestId);
            Assert.Equal(request.UserId, retrieved.UserId);
        }

        [Fact]
        public void Get_WithInvalidId_ShouldReturnNull()
        {
            // Arrange
            var store = new CompletedRequestStore();

            // Act
            var retrieved = store.Get("invalid-id");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void RemoveExpired_ShouldRemoveRequestsOlderThanRetentionPeriod()
        {
            // Arrange
            var store = new CompletedRequestStore();
            var oldRequest = new MatchRequest("user1");
            oldRequest.Status = ModelMatchRequestStatus.Matched;
            oldRequest.CompletedAt = DateTime.UtcNow.AddSeconds(-130); // 130 seconds ago
            
            var recentRequest = new MatchRequest("user2");
            recentRequest.Status = ModelMatchRequestStatus.Matched;
            recentRequest.CompletedAt = DateTime.UtcNow.AddSeconds(-60); // 60 seconds ago

            store.Add(oldRequest);
            store.Add(recentRequest);

            // Act
            var removed = store.RemoveExpired(TimeSpan.FromSeconds(120));

            // Assert
            Assert.Single(removed);
            Assert.Equal(oldRequest.RequestId, removed[0].RequestId);
            Assert.Equal(1, store.Count);
            Assert.NotNull(store.Get(recentRequest.RequestId));
        }

        [Fact]
        public void RemoveExpired_WithNoExpiredRequests_ShouldReturnEmptyList()
        {
            // Arrange
            var store = new CompletedRequestStore();
            var request = new MatchRequest("user1");
            request.Status = ModelMatchRequestStatus.Matched;
            request.CompletedAt = DateTime.UtcNow.AddSeconds(-60);
            store.Add(request);

            // Act
            var removed = store.RemoveExpired(TimeSpan.FromSeconds(120));

            // Assert
            Assert.Empty(removed);
            Assert.Equal(1, store.Count);
        }

        [Fact]
        public void Count_ShouldReturnCorrectNumberOfRequests()
        {
            // Arrange
            var store = new CompletedRequestStore();

            // Act & Assert
            Assert.Equal(0, store.Count);

            store.Add(new MatchRequest("user1"));
            Assert.Equal(1, store.Count);

            store.Add(new MatchRequest("user2"));
            Assert.Equal(2, store.Count);
        }

        [Fact]
        public void Add_WithNullRequest_ShouldThrowArgumentNullException()
        {
            // Arrange
            var store = new CompletedRequestStore();

            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => store.Add(null!));
        }

        [Fact]
        public void Get_WithNullOrEmptyId_ShouldReturnNull()
        {
            // Arrange
            var store = new CompletedRequestStore();

            // Act & Assert
            Assert.Null(store.Get(null!));
            Assert.Null(store.Get(string.Empty));
        }
    }
}
