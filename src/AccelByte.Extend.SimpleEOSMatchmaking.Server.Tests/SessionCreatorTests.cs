// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using Xunit.Abstractions;
using DotNetEnv;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    /// <summary>
    /// Shared fixture for EOS SDK initialization
    /// EOS SDK can only be initialized once per process, so we use a fixture
    /// </summary>
    public class EOSFixture : IDisposable
    {
        public EOSSDKService EOSService { get; }
        public bool IsInitialized { get; private set; }

        public EOSFixture()
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
                EOSService?.StopAsync(CancellationToken.None).Wait();
                EOSService?.Dispose();
            }
        }
    }

    /// <summary>
    /// Tests for SessionCreator
    /// Integration tests require valid EOS credentials in .env file
    /// Unit tests can run without EOS credentials
    /// </summary>
    public class SessionCreatorTests : IClassFixture<EOSFixture>
    {
        private readonly ITestOutputHelper _output;
        private readonly EOSFixture _eosFixture;

        public SessionCreatorTests(ITestOutputHelper output, EOSFixture eosFixture)
        {
            _output = output;
            _eosFixture = eosFixture;
        }

        private EOSSessionCreator CreateSessionCreator()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Debug);
            });

            var logger = loggerFactory.CreateLogger<EOSSessionCreator>();
            return new EOSSessionCreator(logger, _eosFixture.EOSService);
        }

        [Fact(Timeout = 10000)] // 10 second timeout
        public async Task CreateSessionAsync_WithValidMatch_ShouldCreateSession()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionCreator = CreateSessionCreator();
            
            var match = new Match
            {
                MatchId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Requests = new List<MatchRequest>
                {
                    new MatchRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        UserId = "user-1",
                        Status = MatchRequestStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    },
                    new MatchRequest
                    {
                        RequestId = Guid.NewGuid().ToString(),
                        UserId = "user-2",
                        Status = MatchRequestStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    }
                }
            };

            _output.WriteLine($"Creating session for match: {match.MatchId}");
            _output.WriteLine($"Request IDs: {string.Join(", ", match.Requests.ConvertAll(r => r.RequestId))}");
            _output.WriteLine($"User IDs: {string.Join(", ", match.Requests.ConvertAll(r => r.UserId))}");

            // Act
            var sessionInfo = await sessionCreator.CreateSessionAsync(match);

            // Assert
            Assert.NotNull(sessionInfo);
            Assert.NotNull(sessionInfo.SessionId);
            Assert.NotEmpty(sessionInfo.SessionId);
            Assert.Equal(2, sessionInfo.RequestIds.Count);
            Assert.Equal(2, sessionInfo.UserIds.Count);
            Assert.Contains("user-1", sessionInfo.UserIds);
            Assert.Contains("user-2", sessionInfo.UserIds);

            _output.WriteLine($"✓ Session created successfully!");
            _output.WriteLine($"  Session ID: {sessionInfo.SessionId}");
            _output.WriteLine($"  Request IDs: {string.Join(", ", sessionInfo.RequestIds)}");
            _output.WriteLine($"  User IDs: {string.Join(", ", sessionInfo.UserIds)}");
            _output.WriteLine($"  Created At: {sessionInfo.CreatedAt}");
            _output.WriteLine($"");
            _output.WriteLine($"You can verify this session in the EOS Developer Portal:");
            _output.WriteLine($"  https://dev.epicgames.com/portal/");
        }

        [Fact(Timeout = 10000)] // 10 second timeout
        public async Task CreateSessionAsync_WithMultiplePlayers_ShouldCreateSession()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionCreator = CreateSessionCreator();
            
            var match = new Match
            {
                MatchId = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow,
                Requests = new List<MatchRequest>
                {
                    new MatchRequest { RequestId = Guid.NewGuid().ToString(), UserId = "player-1", Status = MatchRequestStatus.Pending, CreatedAt = DateTime.UtcNow },
                    new MatchRequest { RequestId = Guid.NewGuid().ToString(), UserId = "player-2", Status = MatchRequestStatus.Pending, CreatedAt = DateTime.UtcNow },
                    new MatchRequest { RequestId = Guid.NewGuid().ToString(), UserId = "player-3", Status = MatchRequestStatus.Pending, CreatedAt = DateTime.UtcNow },
                    new MatchRequest { RequestId = Guid.NewGuid().ToString(), UserId = "player-4", Status = MatchRequestStatus.Pending, CreatedAt = DateTime.UtcNow }
                }
            };

            _output.WriteLine($"Creating 4-player session for match: {match.MatchId}");

            // Act
            var sessionInfo = await sessionCreator.CreateSessionAsync(match);

            // Assert
            Assert.NotNull(sessionInfo);
            Assert.Equal(4, sessionInfo.RequestIds.Count);
            Assert.Equal(4, sessionInfo.UserIds.Count);

            _output.WriteLine($"✓ 4-player session created successfully!");
            _output.WriteLine($"  Session ID: {sessionInfo.SessionId}");
        }

        [Fact(Timeout = 5000)] // 5 second timeout
        public async Task CreateSessionAsync_WithNullMatch_ShouldThrowArgumentNullException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EOSSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EOSSessionCreator(logger, eosService);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await sessionCreator.CreateSessionAsync(null!)
            );
        }

        [Fact(Timeout = 5000)] // 5 second timeout
        public async Task CreateSessionAsync_WithEmptyRequests_ShouldThrowArgumentException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EOSSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EOSSessionCreator(logger, eosService);

            var match = new Match
            {
                MatchId = Guid.NewGuid().ToString(),
                Requests = new List<MatchRequest>()
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await sessionCreator.CreateSessionAsync(match)
            );
        }

        [Fact(Timeout = 5000)] // 5 second timeout
        public async Task CreateSessionAsync_WithNullRequests_ShouldThrowArgumentException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EOSSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EOSSessionCreator(logger, eosService);

            var match = new Match
            {
                MatchId = Guid.NewGuid().ToString(),
                Requests = null!
            };

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await sessionCreator.CreateSessionAsync(match)
            );
        }
    }
}
