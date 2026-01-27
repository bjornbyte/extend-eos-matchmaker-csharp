// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.IO;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using DotNetEnv;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using Xunit;

[assembly: AssemblyFixture(typeof(AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Fixtures.SharedEOSFixture))]

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Fixtures
{
    /// <summary>
    /// Shared fixture for EOS SDK initialization across all integration tests.
    /// EOS SDK can only be initialized once per process, so this fixture is shared
    /// across all test classes using xUnit v3's assembly fixture feature.
    /// This allows tests to run in parallel while sharing the same EOS SDK instance.
    /// Each test class uses unique bucket IDs to prevent test interference.
    /// </summary>
    public class SharedEOSFixture : IDisposable
    {
        public EOSSDKService EOSService { get; }
        public bool IsInitialized { get; private set; }

        public SharedEOSFixture()
        {
            // Load .env file from project root
            var currentDir = Directory.GetCurrentDirectory();
            var projectRoot = Path.GetFullPath(Path.Combine(currentDir, "..", "..", "..", "..", ".."));
            var envPath = Path.Combine(projectRoot, ".env");

            if (File.Exists(envPath))
            {
                Console.WriteLine($"Loading environment variables from: {envPath}");
                Env.Load(envPath);
            }

            // Setup logging
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            // Initialize EOS SDK Service
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosConfig = new EOSConfig();
            eosConfig.ReadEnvironmentVariables();

            var eosOptions = Options.Create(eosConfig);
            EOSService = new EOSSDKService(eosLogger, eosOptions);

            try
            {
                // Start the EOS SDK with a timeout
                var startTask = EOSService.StartAsync(CancellationToken.None);
                if (!startTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    Console.WriteLine("EOS SDK initialization timed out after 10 seconds");
                    IsInitialized = false;
                    return;
                }
                IsInitialized = true;
                Console.WriteLine("SharedEOSFixture: EOS SDK initialized successfully for assembly");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize EOS SDK: {ex.Message}");
                IsInitialized = false;
            }
        }

        public void Dispose()
        {
            if (IsInitialized)
            {
                Console.WriteLine("SharedEOSFixture: Disposing EOS SDK");
                EOSService?.StopAsync(CancellationToken.None).Wait();
                EOSService?.Dispose();
            }
        }
    }
}
