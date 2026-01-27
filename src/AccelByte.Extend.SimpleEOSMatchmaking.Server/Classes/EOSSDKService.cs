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
        private readonly ILogger<EOSSDKService> Logger;
        private readonly EOSConfig Config;
        private readonly object TickLock = new object();
        private PlatformInterface? PlatformInterface;
        private bool Disposed;

        public EOSSDKService(ILogger<EOSSDKService> logger, IOptions<EOSConfig> config)
        {
            Logger = logger;
            Config = config.Value;
            
            // Override with environment variables if present
            Config.ReadEnvironmentVariables();
        }

        /// <summary>
        /// Gets the EOS Platform Interface instance
        /// </summary>
        public PlatformInterface? Platform => PlatformInterface;

        /// <summary>
        /// Thread-safe wrapper for Platform.Tick().
        /// EOS SDK's Tick() is not thread-safe and must be called from only one thread at a time.
        /// </summary>
        public void Tick()
        {
            lock (TickLock)
            {
                PlatformInterface?.Tick();
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                Logger.LogInformation("Initializing EOS SDK...");
                Logger.LogInformation("EOS Configuration: ProductId={ProductId}, SandboxId={SandboxId}, DeploymentId={DeploymentId}", 
                    Config.ProductId, Config.SandboxId, Config.DeploymentId);

                // Initialize EOS SDK
                var initializeOptions = new InitializeOptions
                {
                    ProductName = "SimpleEOSMatchmaking",
                    ProductVersion = "1.0.0"
                };

                // Set up logging callback to suppress message boxes and redirect to our logger
                Epic.OnlineServices.Logging.LoggingInterface.SetCallback((ref Epic.OnlineServices.Logging.LogMessage message) =>
                {
                    var logMessage = $"[EOS SDK] {message.Category}: {message.Message}";
                    switch (message.Level)
                    {
                        case Epic.OnlineServices.Logging.LogLevel.Fatal:
                        case Epic.OnlineServices.Logging.LogLevel.Error:
                            Logger.LogError(logMessage);
                            break;
                        case Epic.OnlineServices.Logging.LogLevel.Warning:
                            Logger.LogWarning(logMessage);
                            break;
                        case Epic.OnlineServices.Logging.LogLevel.Info:
                            Logger.LogInformation(logMessage);
                            break;
                        case Epic.OnlineServices.Logging.LogLevel.Verbose:
                        case Epic.OnlineServices.Logging.LogLevel.VeryVerbose:
                            Logger.LogDebug(logMessage);
                            break;
                    }
                });

                var initializeResult = PlatformInterface.Initialize(ref initializeOptions);
                if (initializeResult != Result.Success)
                {
                    Logger.LogError("Failed to initialize EOS SDK: {Result}", initializeResult);
                    throw new Exception($"EOS SDK initialization failed: {initializeResult}");
                }

                Logger.LogInformation("EOS SDK initialized successfully");

                var platformOptions = new Epic.OnlineServices.Platform.Options
                {
                    ProductId = Config.ProductId,
                    SandboxId = Config.SandboxId,
                    DeploymentId = Config.DeploymentId,
                    ClientCredentials = new ClientCredentials
                    {
                        ClientId = Config.ClientId,
                        ClientSecret = Config.ClientSecret
                    },
                    IsServer = true,
                    Flags = PlatformFlags.DisableOverlay | PlatformFlags.DisableSocialOverlay,
                    TickBudgetInMilliseconds = 0
                };

                PlatformInterface = PlatformInterface.Create(ref platformOptions);
                if (PlatformInterface == null)
                {
                    Logger.LogError("Failed to create EOS Platform Interface");
                    throw new Exception("Failed to create EOS Platform Interface");
                }

                Logger.LogInformation("EOS Platform Interface created successfully");
                Logger.LogInformation("EOS SDK is ready for use");
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error initializing EOS SDK");
                throw;
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Shutting down EOS SDK...");
            
            Dispose();
            
            Logger.LogInformation("EOS SDK shut down successfully");
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            if (Disposed)
                return;

            PlatformInterface?.Release();
            PlatformInterface = null;

            PlatformInterface.Shutdown();

            Disposed = true;
            GC.SuppressFinalize(this);
        }
    }
}
