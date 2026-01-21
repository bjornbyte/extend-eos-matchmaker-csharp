// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using Xunit;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    public class MatchmakingExceptionsTests
    {
        [Fact]
        public void DuplicateRequestException_ShouldStoreExistingRequestId()
        {
            // Arrange
            var existingRequestId = "request-123";

            // Act
            var exception = new DuplicateRequestException(existingRequestId);

            // Assert
            Assert.Equal(existingRequestId, exception.ExistingRequestId);
            Assert.Contains(existingRequestId, exception.Message);
        }

        [Fact]
        public void MatchRequestNotFoundException_ShouldHaveMessage()
        {
            // Arrange & Act
            var exception = new MatchRequestNotFoundException();

            // Assert
            Assert.NotNull(exception.Message);
            Assert.NotEmpty(exception.Message);
        }

        [Fact]
        public void RequestAlreadyMatchedException_ShouldHaveMessage()
        {
            // Arrange & Act
            var exception = new RequestAlreadyMatchedException();

            // Assert
            Assert.NotNull(exception.Message);
            Assert.NotEmpty(exception.Message);
        }

        [Fact]
        public void SessionCreationException_ShouldHaveMessage()
        {
            // Arrange & Act
            var exception = new SessionCreationException();

            // Assert
            Assert.NotNull(exception.Message);
            Assert.NotEmpty(exception.Message);
        }

        [Fact]
        public void SessionCreationException_WithInnerException_ShouldStoreInnerException()
        {
            // Arrange
            var innerException = new InvalidOperationException("Inner error");

            // Act
            var exception = new SessionCreationException("Session failed", innerException);

            // Assert
            Assert.Equal(innerException, exception.InnerException);
            Assert.Contains("Session failed", exception.Message);
        }

        [Fact]
        public void NoAvailableSessionsException_ShouldStoreSessionsSearched()
        {
            // Arrange
            var sessionsSearched = 5;

            // Act
            var exception = new NoAvailableSessionsException(sessionsSearched);

            // Assert
            Assert.Equal(sessionsSearched, exception.SessionsSearched);
        }

        [Fact]
        public void NoAvailableSessionsException_MessageShouldIncludeSessionsSearched()
        {
            // Arrange
            var sessionsSearched = 10;

            // Act
            var exception = new NoAvailableSessionsException(sessionsSearched);

            // Assert
            Assert.Contains("No available sessions found", exception.Message);
            Assert.Contains(sessionsSearched.ToString(), exception.Message);
        }

        [Fact]
        public void SessionClaimFailedException_ShouldStoreRetryAttempts()
        {
            // Arrange
            var retryAttempts = 3;

            // Act
            var exception = new SessionClaimFailedException(retryAttempts);

            // Assert
            Assert.Equal(retryAttempts, exception.RetryAttempts);
        }

        [Fact]
        public void SessionClaimFailedException_MessageShouldIncludeRetryAttempts()
        {
            // Arrange
            var retryAttempts = 5;

            // Act
            var exception = new SessionClaimFailedException(retryAttempts);

            // Assert
            Assert.Contains("Failed to claim session", exception.Message);
            Assert.Contains(retryAttempts.ToString(), exception.Message);
        }
    }
}
