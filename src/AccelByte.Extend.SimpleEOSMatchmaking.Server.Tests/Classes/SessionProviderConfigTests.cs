// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using Xunit;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Classes
{
    public class SessionProviderConfigTests
    {
        [Fact]
        public void Validate_WithCreateMode_DoesNotThrow()
        {
            // Arrange
            var config = new SessionProviderConfig { Mode = "create" };

            // Act & Assert
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Validate_WithFindMode_DoesNotThrow()
        {
            // Arrange
            var config = new SessionProviderConfig { Mode = "find" };

            // Act & Assert
            var exception = Record.Exception(() => config.Validate());
            Assert.Null(exception);
        }

        [Fact]
        public void Validate_WithInvalidMode_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new SessionProviderConfig { Mode = "invalid" };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("Invalid SessionProviderMode", exception.Message);
            Assert.Contains("invalid", exception.Message);
            Assert.Contains("create", exception.Message);
            Assert.Contains("find", exception.Message);
        }

        [Fact]
        public void Validate_WithEmptyMode_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new SessionProviderConfig { Mode = "" };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("Invalid SessionProviderMode", exception.Message);
        }

        [Fact]
        public void Validate_WithNullMode_ThrowsInvalidOperationException()
        {
            // Arrange
            var config = new SessionProviderConfig { Mode = null! };

            // Act & Assert
            var exception = Assert.Throws<InvalidOperationException>(() => config.Validate());
            Assert.Contains("Invalid SessionProviderMode", exception.Message);
        }
    }
}
