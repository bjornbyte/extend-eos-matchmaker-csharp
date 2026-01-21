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
        public void Constructor_SetsDefaultMaxRetryAttempts()
        {
            // Arrange & Act
            var config = new EOSSessionFinderConfig();

            // Assert
            Assert.Equal(3, config.MaxRetryAttempts);
        }

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
        public void MaxRetryAttempts_CanBeSet()
        {
            // Arrange
            var config = new EOSSessionFinderConfig();

            // Act
            config.MaxRetryAttempts = 5;

            // Assert
            Assert.Equal(5, config.MaxRetryAttempts);
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
    }
}
