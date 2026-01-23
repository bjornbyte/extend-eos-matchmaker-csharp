// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using Xunit;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Classes
{
    public class EOSSessionFinderConfigTests
    {
        [Fact]
        public void Constructor_SetsDefaultBucketId()
        {
            // Arrange & Act
            var config = new EOSSessionFinderConfig();

            // Assert
            Assert.Equal("default", config.BucketId);
        }

        [Fact]
        public void Constructor_SetsDefaultMaxSearchResults()
        {
            // Arrange & Act
            var config = new EOSSessionFinderConfig();

            // Assert
            Assert.Equal(10, config.MaxSearchResults);
        }

        [Fact]
        public void Constructor_SetsDefaultClaimedSessionExpirationSeconds()
        {
            // Arrange & Act
            var config = new EOSSessionFinderConfig();

            // Assert
            Assert.Equal(300, config.ClaimedSessionExpirationSeconds);
        }

        [Fact]
        public void BucketId_CanBeSet()
        {
            // Arrange
            var config = new EOSSessionFinderConfig();

            // Act
            config.BucketId = "custom-bucket";

            // Assert
            Assert.Equal("custom-bucket", config.BucketId);
        }

        [Fact]
        public void MaxSearchResults_CanBeSet()
        {
            // Arrange
            var config = new EOSSessionFinderConfig();

            // Act
            config.MaxSearchResults = 20;

            // Assert
            Assert.Equal(20, config.MaxSearchResults);
        }

        [Fact]
        public void ClaimedSessionExpirationSeconds_CanBeSet()
        {
            // Arrange
            var config = new EOSSessionFinderConfig();

            // Act
            config.ClaimedSessionExpirationSeconds = 600;

            // Assert
            Assert.Equal(600, config.ClaimedSessionExpirationSeconds);
        }
    }
}
