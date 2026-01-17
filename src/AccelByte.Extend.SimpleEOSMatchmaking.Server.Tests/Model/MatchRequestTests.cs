using System;
using System.Collections.Generic;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using Xunit;

using ModelMatchRequestStatus = AccelByte.Extend.SimpleEOSMatchmaking.Server.Model.MatchRequestStatus;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Model
{
    public class MatchRequestTests
    {
        [Fact]
        public void Constructor_GeneratesUniqueRequestId()
        {
            // Arrange & Act
            var request1 = new MatchRequest("user1");
            var request2 = new MatchRequest("user2");

            // Assert
            Assert.NotNull(request1.RequestId);
            Assert.NotNull(request2.RequestId);
            Assert.NotEqual(request1.RequestId, request2.RequestId);
        }

        [Fact]
        public void Constructor_SetsUserIdCorrectly()
        {
            // Arrange
            var userId = "test-user-123";

            // Act
            var request = new MatchRequest(userId);

            // Assert
            Assert.Equal(userId, request.UserId);
        }

        [Fact]
        public void Constructor_InitializesStatusAsPending()
        {
            // Arrange & Act
            var request = new MatchRequest("user1");

            // Assert
            Assert.Equal(ModelMatchRequestStatus.Pending, request.Status);
        }

        [Fact]
        public void Constructor_SetsCreatedAtToCurrentTime()
        {
            // Arrange
            var beforeCreation = DateTime.UtcNow;

            // Act
            var request = new MatchRequest("user1");

            // Assert
            var afterCreation = DateTime.UtcNow;
            Assert.InRange(request.CreatedAt, beforeCreation, afterCreation);
        }

        [Fact]
        public void Constructor_InitializesMatchedAtAsNull()
        {
            // Arrange & Act
            var request = new MatchRequest("user1");

            // Assert
            Assert.Null(request.MatchedAt);
        }

        [Fact]
        public void Constructor_InitializesSessionIdAsNull()
        {
            // Arrange & Act
            var request = new MatchRequest("user1");

            // Assert
            Assert.Null(request.SessionId);
        }

        [Fact]
        public void Constructor_WithMetadata_StoresMetadataCorrectly()
        {
            // Arrange
            var metadata = new Dictionary<string, string>
            {
                { "region", "us-west" },
                { "skill", "1500" }
            };

            // Act
            var request = new MatchRequest("user1", metadata);

            // Assert
            Assert.NotNull(request.Metadata);
            Assert.Equal(2, request.Metadata.Count);
            Assert.Equal("us-west", request.Metadata["region"]);
            Assert.Equal("1500", request.Metadata["skill"]);
        }

        [Fact]
        public void Constructor_WithoutMetadata_InitializesMetadataAsNull()
        {
            // Arrange & Act
            var request = new MatchRequest("user1");

            // Assert
            Assert.Null(request.Metadata);
        }

        [Fact]
        public void Status_CanBeUpdatedToMatched()
        {
            // Arrange
            var request = new MatchRequest("user1");

            // Act
            request.Status = ModelMatchRequestStatus.Matched;

            // Assert
            Assert.Equal(ModelMatchRequestStatus.Matched, request.Status);
        }

        [Fact]
        public void MatchedAt_CanBeSet()
        {
            // Arrange
            var request = new MatchRequest("user1");
            var matchTime = DateTime.UtcNow;

            // Act
            request.MatchedAt = matchTime;

            // Assert
            Assert.Equal(matchTime, request.MatchedAt);
        }

        [Fact]
        public void SessionId_CanBeSet()
        {
            // Arrange
            var request = new MatchRequest("user1");
            var sessionId = "session-123";

            // Act
            request.SessionId = sessionId;

            // Assert
            Assert.Equal(sessionId, request.SessionId);
        }
    }
}
