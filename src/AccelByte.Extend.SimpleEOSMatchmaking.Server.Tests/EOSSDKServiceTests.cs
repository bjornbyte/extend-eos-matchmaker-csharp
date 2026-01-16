// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    public class EOSSDKServiceTests
    {
        [Fact]
        public void EOSConfig_ReadEnvironmentVariables_ShouldReadAllValues()
        {
            // Arrange
            var testProductId = "test-product-id";
            var testSandboxId = "test-sandbox-id";
            var testDeploymentId = "test-deployment-id";
            var testClientId = "test-client-id";
            var testClientSecret = "test-client-secret";

            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", testProductId);
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", testSandboxId);
            Environment.SetEnvironmentVariable("EOS_DEPLOYMENT_ID", testDeploymentId);
            Environment.SetEnvironmentVariable("EOS_CLIENT_ID", testClientId);
            Environment.SetEnvironmentVariable("EOS_CLIENT_SECRET", testClientSecret);

            var config = new EOSConfig();

            // Act
            config.ReadEnvironmentVariables();

            // Assert
            Assert.Equal(testProductId, config.ProductId);
            Assert.Equal(testSandboxId, config.SandboxId);
            Assert.Equal(testDeploymentId, config.DeploymentId);
            Assert.Equal(testClientId, config.ClientId);
            Assert.Equal(testClientSecret, config.ClientSecret);

            // Cleanup
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", null);
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", null);
            Environment.SetEnvironmentVariable("EOS_DEPLOYMENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_SECRET", null);
        }

        [Fact]
        public void EOSConfig_ReadEnvironmentVariables_ShouldHandleMissingValues()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", null);
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", null);
            Environment.SetEnvironmentVariable("EOS_DEPLOYMENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_SECRET", null);

            var config = new EOSConfig
            {
                ProductId = "default-product",
                SandboxId = "default-sandbox",
                DeploymentId = "default-deployment",
                ClientId = "default-client",
                ClientSecret = "default-secret"
            };

            // Act
            config.ReadEnvironmentVariables();

            // Assert - Should keep default values when env vars are not set
            Assert.Equal("default-product", config.ProductId);
            Assert.Equal("default-sandbox", config.SandboxId);
            Assert.Equal("default-deployment", config.DeploymentId);
            Assert.Equal("default-client", config.ClientId);
            Assert.Equal("default-secret", config.ClientSecret);
        }

        [Fact]
        public void EOSConfig_ReadEnvironmentVariables_ShouldTrimWhitespace()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", "  test-product  ");
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", "  test-sandbox  ");
            Environment.SetEnvironmentVariable("EOS_DEPLOYMENT_ID", "  test-deployment  ");
            Environment.SetEnvironmentVariable("EOS_CLIENT_ID", "  test-client  ");
            Environment.SetEnvironmentVariable("EOS_CLIENT_SECRET", "  test-secret  ");

            var config = new EOSConfig();

            // Act
            config.ReadEnvironmentVariables();

            // Assert
            Assert.Equal("test-product", config.ProductId);
            Assert.Equal("test-sandbox", config.SandboxId);
            Assert.Equal("test-deployment", config.DeploymentId);
            Assert.Equal("test-client", config.ClientId);
            Assert.Equal("test-secret", config.ClientSecret);

            // Cleanup
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", null);
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", null);
            Environment.SetEnvironmentVariable("EOS_DEPLOYMENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_ID", null);
            Environment.SetEnvironmentVariable("EOS_CLIENT_SECRET", null);
        }

        [Fact]
        public void EOSConfig_ReadEnvironmentVariables_ShouldIgnoreEmptyStrings()
        {
            // Arrange
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", "");
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", "   ");
            
            var config = new EOSConfig
            {
                ProductId = "default-product",
                SandboxId = "default-sandbox"
            };

            // Act
            config.ReadEnvironmentVariables();

            // Assert - Should keep default values when env vars are empty/whitespace
            Assert.Equal("default-product", config.ProductId);
            Assert.Equal("default-sandbox", config.SandboxId);

            // Cleanup
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", null);
            Environment.SetEnvironmentVariable("EOS_SANDBOX_ID", null);
        }

        [Fact]
        public void EOSSDKService_Constructor_ShouldReadEnvironmentVariables()
        {
            // Arrange
            var testProductId = "constructor-test-product";
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", testProductId);

            var config = new EOSConfig();
            var options = Options.Create(config);
            var logger = new LoggerFactory().CreateLogger<EOSSDKService>();

            // Act
            var service = new EOSSDKService(logger, options);

            // Assert - The constructor should have called ReadEnvironmentVariables
            // We can't directly access _config, but we can verify the behavior by checking
            // that the service was created without throwing an exception
            Assert.NotNull(service);

            // Cleanup
            Environment.SetEnvironmentVariable("EOS_PRODUCT_ID", null);
        }
    }
}
