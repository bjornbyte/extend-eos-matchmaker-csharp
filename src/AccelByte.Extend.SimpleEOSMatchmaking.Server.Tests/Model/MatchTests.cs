using System;
using System.Collections.Generic;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Model
{
    public class MatchTests
    {
        [Fact]
        public void Constructor_GeneratesUniqueMatchId()
        {
            // Arrange
            var requests1 = new List<MatchRequest> { new MatchRequest("user1") };
            var requests2 = new List<MatchRequest> { new MatchRequest("user2") };

            // Act
            var match1 = new Match(requests1);
            var match2 = new Match(requests2);

            // Assert
            Assert.NotNull(match1.MatchId);
            Assert.NotNull(match2.MatchId);
            Assert.NotEqual(match1.MatchId, match2.MatchId);
        }

        [Fact]
        public void Constructor_StoresRequestsCorrectly()
        {
            // Arrange
            var requests = new List<MatchRequest>
            {
                new MatchRequest("user1"),
                new MatchRequest("user2")
            };

            // Act
            var match = new Match(requests);

            // Assert
            Assert.Equal(2, match.Requests.Count);
            Assert.Equal("user1", match.Requests[0].UserId);
            Assert.Equal("user2", match.Requests[1].UserId);
        }

        [Fact]
        public void Constructor_WithNullRequests_InitializesEmptyList()
        {
            // Arrange & Act
            var match = new Match(null);

            // Assert
            Assert.NotNull(match.Requests);
            Assert.Empty(match.Requests);
        }

        [Fact]
        public void Constructor_SetsCreatedAtToCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;
            var requests = new List<MatchRequest> { new MatchRequest("user1") };

            // Act
            var match = new Match(requests);

            // Assert
            var afterCreation = DateTime.UtcNow;
            Assert.InRange(match.CreatedAt, beforeCreation, afterCreation);
        }

        [Fact]
        public void Requests_CanBeModified()
        {
            // Arrange
            var requests = new List<MatchRequest> { new MatchRequest("user1") };
            var match = new Match(requests);

            // Act
            match.Requests.Add(new MatchRequest("user2"));

            // Assert
            Assert.Equal(2, match.Requests.Count);
        }
    }
}
