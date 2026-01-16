// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Epic.OnlineServices;
using Epic.OnlineServices.Platform;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes
{
    /// <summary>
    /// Service responsible for initializing and managing the EOS SDK lifecycle
    /// </summary>
    public class EOSSDKService : IHostedService, IDisposable
    {
        private readonly ILogger<EOSSDKService> _logger;
        private readonly EOSConfig _config;
        private PlatformInterface? _platformInterface;
        private bool _disposed;

        public EOSSDKService(ILogger<EOSSDKService> logger, IOptions<EOSConfig> config)
        {
            _logger = logger;
            _config = config.Value;
            
            // Override with environment variables if present
            _config.ReadEnvironmentVariables();
        }

        /// <summary>
        /// Gets the EOS Platform Interface instance
        /// </summary>
        public PlatformInterface? Platform => _platformInterface;

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _logger.LogInformation("Initializing EOS SDK...");
                _logger.LogInformation("EOS Configuration: ProductId={ProductId}, SandboxId={SandboxId}, DeploymentId={DeploymentId}", 
                    _config.ProductId, _config.SandboxId, _config.DeploymentId);

                // Initialize EOS SDK
                var initializeOptions = new InitializeOptions
                {
                    ProductName = "SimpleEOSMatchmaking",
                    ProductVersion = "1.0.0"
                };

                var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
                if (initializeResult != Result.Success)
                {
                    _logger.LogError("Failed to initialize EOS SDK: {Result}", initializeResult);
                    throw new Exception($"EOS SDK initialization failed: {initializeResult}");
                }

                _logger.LogInformation("EOS SDK initialized successfully");

                // Create platform interface
                var platformOptions = new Epic.OnlineServices.Platform.Options
                {
                    ProductId = _config.ProductId,
                    SandboxId = _config.SandboxId,
                    DeploymentId = _config.DeploymentId,
                    ClientCredentials = new ClientCredentials
                    {
                        ClientId = _config.ClientId,
                        ClientSecret = _config.ClientSecret
                    },
                    IsServer = true,
                    EncryptionKey = null, // Optional: Add encryption key if needed
                    OverrideCountryCode = null,
                    OverrideLocaleCode = null,
                    Flags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay,
                    CacheDirectory = null,
                    TickBudgetInMilliseconds = 0
                };

                _platformInterface = PlatformInterface.Create(ref platformOptions);
                if (_platformInterface == null)
                {
                    _logger.LogError("Failed to create EOS Platform Interface");
                    throw new Exception("Failed to create EOS Platform Interface");
                }

                _logger.LogInformation("EOS Platform Interface created successfully");
                _logger.LogInformation("EOS SDK is ready for use");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initializing EOS SDK");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Shutting down EOS SDK...");
            
            Dispose();
            
            _logger.LogInformation("EOS SDK shut down successfully");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _platformInterface?.Release();
            _platformInterface = null;

            PlatformInterface.Shutdown();

            _disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
