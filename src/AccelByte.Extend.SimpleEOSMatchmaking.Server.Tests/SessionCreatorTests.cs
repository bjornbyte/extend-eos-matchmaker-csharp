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
using Xunit.Sdk;
using DotNetEnv;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Classes;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Model;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Services;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Fixtures;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests
{
    /// <summary>
    /// Logger provider that routes log messages to xUnit test output
    /// </summary>
    public class XunitLoggerProvider : ILoggerProvider
    {
        private readonly ITestOutputHelper _output;

        public XunitLoggerProvider(ITestOutputHelper output)
        {
            _output = output;
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new XunitLogger(_output, categoryName);
        }

        public void Dispose() { }
    }

    /// <summary>
    /// Logger that writes to xUnit test output
    /// </summary>
    public class XunitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;

        public XunitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            try
            {
                var message = formatter(state, exception);
                _output.WriteLine($"[{logLevel}] {_categoryName}: {message}");
                if (exception != null)
                {
                    _output.WriteLine($"Exception: {exception}");
                }
            }
            catch
            {
                // Ignore errors writing to test output
            }
        }
    }

    /// <summary>
    /// Unit tests for SessionCreator that don't require EOS SDK initialization
    /// NOTE: These tests are currently skipped because creating EOSSDKService instances
    /// can trigger EOS SDK DLL loading even without calling StartAsync.
    /// 
    /// The validation logic tested here should be covered by integration tests
    /// or by mocking ISessionCreator in other component tests.
    /// </summary>
    public class SessionCreatorTests
    {
        [Fact(Timeout = 5000)] //, Skip = "Skipped - creating EOSSDKService triggers EOS SDK loading")]
        public async Task GetSessionAsync_WithNullMatch_ShouldThrowArgumentNullException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EosSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EosSessionCreator(logger, eosService);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                async () => await sessionCreator.GetSessionAsync(null!)
            );
        }

        [Fact(Timeout = 5000)] //, Skip = "Skipped - creating EOSSDKService triggers EOS SDK loading")]
        public async Task GetSessionAsync_WithEmptyRequests_ShouldThrowArgumentException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EosSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EosSessionCreator(logger, eosService);

            var match = new Match(new List<MatchRequest>());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await sessionCreator.GetSessionAsync(match)
            );
        }

        [Fact(Timeout = 5000)] //, Skip = "Skipped - creating EOSSDKService triggers EOS SDK loading")]
        public async Task GetSessionAsync_WithNullRequests_ShouldThrowArgumentException()
        {
            // Arrange - Create a session creator without needing EOS initialized
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<EosSessionCreator>();
            var eosConfig = new EOSConfig();
            var eosOptions = Options.Create(eosConfig);
            var eosLogger = loggerFactory.CreateLogger<EOSSDKService>();
            var eosService = new EOSSDKService(eosLogger, eosOptions);
            var sessionCreator = new EosSessionCreator(logger, eosService);

            var match = new Match(null!);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(
                async () => await sessionCreator.GetSessionAsync(match)
            );
        }
    }

    /// <summary>
    /// Integration tests for SessionCreator that require EOS SDK initialization
    /// These tests are SKIPPED by default to avoid slow test runs.
    /// 
    /// IMPORTANT: Sessions created during tests will disappear from the EOS portal after the test completes.
    /// This is expected behavior - when the EOS Platform is disposed (via EOSFixture.Dispose), 
    /// all sessions created by that platform instance are automatically destroyed by EOS.
    /// In production, sessions persist as long as the matchmaking service is running.
    /// 
    /// Tests run in parallel using the shared assembly fixture.
    /// 
    /// To run these tests manually:
    /// 1. Ensure you have valid EOS credentials in .env file
    /// 2. Uncomment the tests below
    /// 3. Run: dotnet test --filter "FullyQualifiedName~SessionCreatorIntegrationTests"
    /// </summary>
    public class SessionCreatorIntegrationTests
    {
        // Integration tests are commented out to prevent EOS SDK initialization during normal test runs
        // Uncomment these tests when you want to run integration tests manually
        
        private readonly ITestOutputHelper _output;
        private readonly SharedEOSFixture _eosFixture;

        public SessionCreatorIntegrationTests(ITestOutputHelper output, SharedEOSFixture eosFixture)
        {
            _output = output;
            _eosFixture = eosFixture;
        }

        private EosSessionCreator CreateSessionCreator()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddProvider(new XunitLoggerProvider(_output));
                builder.SetMinimumLevel(LogLevel.Trace); // Capture all log levels
            });

            var logger = loggerFactory.CreateLogger<EosSessionCreator>();
            return new EosSessionCreator(logger, _eosFixture.EOSService);
        }

        [Fact(Timeout = 10000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_WithValidMatch_ShouldCreateSession()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionCreator = CreateSessionCreator();
            
            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-1"),
                new MatchRequest("user-2")
            };
            var match = new Match(requests);

            _output.WriteLine($"Creating session for match: {match.MatchId}");
            _output.WriteLine($"Request IDs: {string.Join(", ", match.Requests.ConvertAll(r => r.RequestId))}");
            _output.WriteLine($"User IDs: {string.Join(", ", match.Requests.ConvertAll(r => r.UserId))}");

            // Act
            var sessionInfo = await sessionCreator.GetSessionAsync(match);

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

        [Fact(Timeout = 10000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_WithMultiplePlayers_ShouldCreateSession()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionCreator = CreateSessionCreator();
            
            var requests = new List<MatchRequest>
            {
                new MatchRequest("player-1"),
                new MatchRequest("player-2"),
                new MatchRequest("player-3"),
                new MatchRequest("player-4")
            };
            var match = new Match(requests);

            _output.WriteLine($"Creating 4-player session for match: {match.MatchId}");

            // Act
            var sessionInfo = await sessionCreator.GetSessionAsync(match);

            // Assert
            Assert.NotNull(sessionInfo);
            Assert.Equal(4, sessionInfo.RequestIds.Count);
            Assert.Equal(4, sessionInfo.UserIds.Count);

            _output.WriteLine($"✓ 4-player session created successfully!");
            _output.WriteLine($"  Session ID: {sessionInfo.SessionId}");
        }
        
    }
}
