using System;
using System.Collections.Generic;
using System.Linq;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using Xunit;

using ModelMatchRequestStatus = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    public class MatchPoolTests
    {
        [Fact]
        public void Add_AddsRequestToPool()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");

            // Act
            pool.Add(request);

            // Assert
            Assert.Equal(1, pool.Count);
            var retrieved = pool.Get(request.RequestId);
            Assert.NotNull(retrieved);
            Assert.Equal(request.RequestId, retrieved.RequestId);
        }

        [Fact]
        public void Add_MultipleRequests_IncreasesCount()
        {
            // Arrange
            var pool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");

            // Act
            pool.Add(request1);
            pool.Add(request2);

            // Assert
            Assert.Equal(2, pool.Count);
        }

        [Fact]
        public void Get_WithValidRequestId_ReturnsRequest()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");
            pool.Add(request);

            // Act
            var retrieved = pool.Get(request.RequestId);

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(request.RequestId, retrieved.RequestId);
            Assert.Equal(request.UserId, retrieved.UserId);
        }

        [Fact]
        public void Get_WithInvalidRequestId_ReturnsNull()
        {
            // Arrange
            var pool = new MatchPool();

            // Act
            var retrieved = pool.Get("non-existent-id");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void GetByUserId_WithValidUserId_ReturnsRequest()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");
            pool.Add(request);

            // Act
            var retrieved = pool.GetByUserId("user1");

            // Assert
            Assert.NotNull(retrieved);
            Assert.Equal(request.RequestId, retrieved.RequestId);
            Assert.Equal("user1", retrieved.UserId);
        }

        [Fact]
        public void GetByUserId_WithInvalidUserId_ReturnsNull()
        {
            // Arrange
            var pool = new MatchPool();

            // Act
            var retrieved = pool.GetByUserId("non-existent-user");

            // Assert
            Assert.Null(retrieved);
        }

        [Fact]
        public void Remove_WithValidRequestId_RemovesAndReturnsRequest()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");
            pool.Add(request);

            // Act
            var removed = pool.Remove(request.RequestId);

            // Assert
            Assert.NotNull(removed);
            Assert.Equal(request.RequestId, removed.RequestId);
            Assert.Equal(0, pool.Count);
            Assert.Null(pool.Get(request.RequestId));
        }

        [Fact]
        public void Remove_WithInvalidRequestId_ReturnsNull()
        {
            // Arrange
            var pool = new MatchPool();

            // Act
            var removed = pool.Remove("non-existent-id");

            // Assert
            Assert.Null(removed);
        }

        [Fact]
        public void Remove_UpdatesUserIdIndex()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");
            pool.Add(request);

            // Act
            pool.Remove(request.RequestId);

            // Assert
            Assert.Null(pool.GetByUserId("user1"));
        }

        [Fact]
        public void GetOldest_ReturnsRequestsInFIFOOrder()
        {
            // Arrange
            var pool = new MatchPool();
            var request1 = new MatchRequest("user1");
            System.Threading.Thread.Sleep(10); // Ensure different timestamps
            var request2 = new MatchRequest("user2");
            System.Threading.Thread.Sleep(10);
            var request3 = new MatchRequest("user3");

            pool.Add(request1);
            pool.Add(request2);
            pool.Add(request3);

            // Act
            var oldest = pool.GetOldest(2);

            // Assert
            Assert.Equal(2, oldest.Count);
            Assert.Equal(request1.RequestId, oldest[0].RequestId);
            Assert.Equal(request2.RequestId, oldest[1].RequestId);
        }

        [Fact]
        public void GetOldest_WithCountGreaterThanPoolSize_ReturnsAllRequests()
        {
            // Arrange
            var pool = new MatchPool();
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");

            pool.Add(request1);
            pool.Add(request2);

            // Act
            var oldest = pool.GetOldest(10);

            // Assert
            Assert.Equal(2, oldest.Count);
        }

        [Fact]
        public void GetOldest_WithEmptyPool_ReturnsEmptyList()
        {
            // Arrange
            var pool = new MatchPool();

            // Act
            var oldest = pool.GetOldest(5);

            // Assert
            Assert.Empty(oldest);
        }

        [Fact]
        public void RemoveExpired_RemovesAndReturnsExpiredRequests()
        {
            // Arrange
            var pool = new MatchPool();
            var oldRequest = new MatchRequest("user1");
            oldRequest.CreatedAt = DateTime.UtcNow.AddSeconds(-70); // 70 seconds ago
            
            var recentRequest = new MatchRequest("user2");
            recentRequest.CreatedAt = DateTime.UtcNow.AddSeconds(-30); // 30 seconds ago

            pool.Add(oldRequest);
            pool.Add(recentRequest);

            // Act
            var expired = pool.RemoveExpired(TimeSpan.FromSeconds(60));

            // Assert
            Assert.Single(expired);
            Assert.Equal(oldRequest.RequestId, expired[0].RequestId);
            Assert.Equal(1, pool.Count);
            Assert.Null(pool.Get(oldRequest.RequestId));
            Assert.NotNull(pool.Get(recentRequest.RequestId));
        }

        [Fact]
        public void RemoveExpired_WithNoExpiredRequests_ReturnsEmptyList()
        {
            // Arrange
            var pool = new MatchPool();
            var request = new MatchRequest("user1");
            pool.Add(request);

            // Act
            var expired = pool.RemoveExpired(TimeSpan.FromSeconds(60));

            // Assert
            Assert.Empty(expired);
            Assert.Equal(1, pool.Count);
        }

        [Fact]
        public void RemoveExpired_UpdatesStatusToExpired()
        {
            // Arrange
            var pool = new MatchPool();
            var oldRequest = new MatchRequest("user1");
            oldRequest.CreatedAt = DateTime.UtcNow.AddSeconds(-70);
            pool.Add(oldRequest);

            // Act
            var expired = pool.RemoveExpired(TimeSpan.FromSeconds(60));

            // Assert
            Assert.Single(expired);
            Assert.Equal(ModelMatchRequestStatus.Expired, expired[0].Status);
        }

        [Fact]
        public void Count_ReturnsCorrectCount()
        {
            // Arrange
            var pool = new MatchPool();

            // Act & Assert
            Assert.Equal(0, pool.Count);

            pool.Add(new MatchRequest("user1"));
            Assert.Equal(1, pool.Count);

            pool.Add(new MatchRequest("user2"));
            Assert.Equal(2, pool.Count);

            pool.Remove(pool.GetOldest(1)[0].RequestId);
            Assert.Equal(1, pool.Count);
        }

        [Fact]
        public void ThreadSafety_ConcurrentAdds_AllRequestsAdded()
        {
            // Arrange
            var pool = new MatchPool();
            var tasks = new List<System.Threading.Tasks.Task>();
            var requestCount = 100;

            // Act
            for (int i = 0; i < requestCount; i++)
            {
                var userId = $"user{i}";
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    var request = new MatchRequest(userId);
                    pool.Add(request);
                }));
            }

            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());

            // Assert
            Assert.Equal(requestCount, pool.Count);
        }

        [Fact]
        public void ThreadSafety_ConcurrentReadsAndWrites_NoExceptions()
        {
            // Arrange
            var pool = new MatchPool();
            var tasks = new List<System.Threading.Tasks.Task>();
            var operationCount = 50;

            // Add some initial requests
            for (int i = 0; i < 10; i++)
            {
                pool.Add(new MatchRequest($"user{i}"));
            }

            // Act - Mix of reads and writes
            for (int i = 0; i < operationCount; i++)
            {
                var index = i;
                tasks.Add(System.Threading.Tasks.Task.Run(() =>
                {
                    if (index % 3 == 0)
                    {
                        pool.Add(new MatchRequest($"user{index + 100}"));
                    }
                    else if (index % 3 == 1)
                    {
                        pool.GetOldest(5);
                    }
                    else
                    {
                        var oldest = pool.GetOldest(1);
                        if (oldest.Count > 0)
                        {
                            pool.Remove(oldest[0].RequestId);
                        }
                    }
                }));
            }

            // Assert - Should complete without exceptions
            System.Threading.Tasks.Task.WaitAll(tasks.ToArray());
            Assert.True(pool.Count >= 0); // Just verify pool is in valid state
        }
    }
}
