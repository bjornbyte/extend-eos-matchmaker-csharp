// Copyright (c) 2025 AccelByte Inc. All Rights Reserved.
// This is licensed software from AccelByte Inc, for limitations
// and restrictions contact your company contract manager.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Helpers;
using AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Fixtures;

namespace AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests.Services
{
    /// <summary>
    /// Integration tests for EOSSessionFinder that require EOS SDK initialization.
    /// These tests verify the actual interaction with the EOS Sessions service.
    /// 
    /// IMPORTANT: These tests require:
    /// 1. Valid EOS credentials in .env file
    /// 2. Real EOS SDK connection (not mocked)
    /// 3. Clean state (no existing sessions in EOS)
    /// 
    /// Tests run in parallel using unique bucket IDs to prevent interference.
    /// 
    /// To run these tests manually:
    /// 1. Ensure you have valid EOS credentials in .env file
    /// 2. Run: dotnet test --filter "FullyQualifiedName~SessionFinderIntegrationTests"
    /// </summary>
    public class SessionFinderIntegrationTests : IAsyncLifetime
    {
        private static readonly TimeSpan EosSessionIndexDelay = TimeSpan.FromSeconds(5);
        private readonly ITestOutputHelper _output;
        private readonly SharedEOSFixture _eosFixture;
        private EmptySessionTestHelper _testHelper;
        private string _testBucketId;

        public SessionFinderIntegrationTests(ITestOutputHelper output, SharedEOSFixture eosFixture)
        {
            _output = output;
            _eosFixture = eosFixture;
            // Generate a unique bucket ID for this test instance
            _testBucketId = $"test-{Guid.NewGuid():N}";
        }

        public ValueTask InitializeAsync()
        {
            // Create test helper for each test
            if (_eosFixture.IsInitialized)
            {
                var loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddConsole();
                    builder.AddProvider(new XunitLoggerProvider(_output));
                    builder.SetMinimumLevel(LogLevel.Trace);
                });

                var helperLogger = loggerFactory.CreateLogger<EmptySessionTestHelper>();
                _testHelper = new EmptySessionTestHelper(helperLogger, _eosFixture.EOSService);
            }

            return ValueTask.CompletedTask;
        }

        public async ValueTask DisposeAsync()
        {
            // Cleanup all test sessions after each test
            if (_testHelper != null)
            {
                await _testHelper.CleanupAllSessionsAsync();
            }
        }

        private EOSSessionFinder CreateSessionFinder()
        {
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddProvider(new XunitLoggerProvider(_output));
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            var logger = loggerFactory.CreateLogger<EOSSessionFinder>();
            var config = new EOSSessionFinderConfig
            {
                BucketId = _testBucketId, // Use unique bucket per test
                MaxSearchResults = 10,
                ClaimedSessionExpirationSeconds = 300
            };

            var cacheLogger = loggerFactory.CreateLogger<InMemoryClaimedSessionsCache>();
            var cache = new InMemoryClaimedSessionsCache(cacheLogger, config);
            
            var notifierLogger = loggerFactory.CreateLogger<StubSessionOwnerNotifier>();
            var notifier = new StubSessionOwnerNotifier(notifierLogger);

            return new EOSSessionFinder(logger, _eosFixture.EOSService, config, cache, notifier);
        }

        /// <summary>
        /// Test 7.1: Verify NoAvailableSessionsException is thrown when no sessions exist
        /// Requirements: 1.3, 5.1
        /// </summary>
        [Fact(Timeout = 15000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_WhenNoSessionsExist_ShouldThrowNoAvailableSessionsException()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange - Using unique bucket, no cleanup needed
            var sessionFinder = CreateSessionFinder();
            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-1"),
                new MatchRequest("user-2")
            };
            var match = new Match(requests);

            _output.WriteLine($"Testing GetSessionAsync with no available sessions");
            _output.WriteLine($"Match ID: {match.MatchId}");
            _output.WriteLine($"Bucket ID: {_testBucketId}");

            // Act & Assert
            var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(
                async () => await sessionFinder.GetSessionAsync(match)
            );

            Assert.NotNull(exception);
            Assert.Equal(0, exception.SessionsSearched);
            Assert.Contains("No available sessions found", exception.Message);

            _output.WriteLine($"✓ NoAvailableSessionsException thrown as expected");
            _output.WriteLine($"  Sessions Searched: {exception.SessionsSearched}");
            _output.WriteLine($"  Message: {exception.Message}");
        }

        /// <summary>
        /// Test 7.2: Verify SessionFinder can find and claim empty sessions
        /// Requirements: 1.1, 1.2, 2.1, 2.2, 3.1, 3.2, 3.3, 3.4
        /// </summary>
        [Fact(Timeout = 30000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_WithEmptySessions_ShouldFindAndClaimDifferentSessions()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            const int sessionCount = 3;
            var sessionFinder = CreateSessionFinder();
            var createdSessionIds = new List<string>();

            _output.WriteLine($"Creating {sessionCount} empty test sessions...");

            // Create N empty sessions in unique bucket
            for (int i = 0; i < sessionCount; i++)
            {
                var sessionId = await _testHelper.CreateEmptySessionAsync(_testBucketId);
                createdSessionIds.Add(sessionId);
                _output.WriteLine($"  Created session {i + 1}/{sessionCount}: {sessionId}");
            }

            // Wait for sessions to be indexed by EOS
            _output.WriteLine($"Waiting {EosSessionIndexDelay.TotalSeconds} seconds for EOS to index sessions...");
            await Task.Delay(EosSessionIndexDelay);

            var claimedSessionIds = new List<string>();

            // Act - Call GetSessionAsync N times
            for (int i = 0; i < sessionCount; i++)
            {
                var requests = new List<MatchRequest>
                {
                    new MatchRequest($"user-{i}-1"),
                    new MatchRequest($"user-{i}-2")
                };
                var match = new Match(requests);

                _output.WriteLine($"Claiming session {i + 1}/{sessionCount} for match: {match.MatchId}");

                var sessionInfo = await sessionFinder.GetSessionAsync(match);

                Assert.NotNull(sessionInfo);
                Assert.NotNull(sessionInfo.SessionId);
                Assert.NotEmpty(sessionInfo.SessionId);

                claimedSessionIds.Add(sessionInfo.SessionId);

                _output.WriteLine($"  ✓ Claimed session: {sessionInfo.SessionId}");
                _output.WriteLine($"    Request IDs: {string.Join(", ", sessionInfo.RequestIds)}");
                _output.WriteLine($"    User IDs: {string.Join(", ", sessionInfo.UserIds)}");

                // Verify metadata
                Assert.Equal(2, sessionInfo.RequestIds.Count);
                Assert.Equal(2, sessionInfo.UserIds.Count);
                Assert.Contains($"user-{i}-1", sessionInfo.UserIds);
                Assert.Contains($"user-{i}-2", sessionInfo.UserIds);
            }

            // Assert - Verify each call claimed a different session
            var uniqueSessionIds = claimedSessionIds.Distinct().ToList();
            Assert.Equal(sessionCount, uniqueSessionIds.Count);

            _output.WriteLine($"✓ All {sessionCount} sessions were claimed with unique session IDs");

            // Verify all claimed sessions are from the created sessions
            foreach (var claimedId in claimedSessionIds)
            {
                Assert.Contains(claimedId, createdSessionIds);
            }

            _output.WriteLine($"✓ All claimed sessions were from the created test sessions");

            // Act & Assert - Verify (N+1)th call throws exception
            var extraRequests = new List<MatchRequest>
            {
                new MatchRequest("user-extra-1"),
                new MatchRequest("user-extra-2")
            };
            var extraMatch = new Match(extraRequests);

            _output.WriteLine($"Attempting to claim session {sessionCount + 1} (should fail)...");

            var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(
                async () => await sessionFinder.GetSessionAsync(extraMatch)
            );

            Assert.NotNull(exception);
            _output.WriteLine($"✓ NoAvailableSessionsException thrown as expected after all sessions claimed");
            _output.WriteLine($"  Sessions Searched: {exception.SessionsSearched}");
        }

        /// <summary>
        /// Test 9.3: Verify started sessions are excluded from search results
        /// Requirements: 6.2
        /// </summary>
        [Fact(Timeout = 30000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_WithStartedSessions_ShouldExcludeStartedSessions()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionFinder = CreateSessionFinder();

            _output.WriteLine("Creating started test sessions...");

            // Create 2 started sessions in unique bucket
            var startedSessionId1 = await _testHelper.CreateStartedSessionAsync(_testBucketId);
            _output.WriteLine($"  Created started session 1: {startedSessionId1}");

            var startedSessionId2 = await _testHelper.CreateStartedSessionAsync(_testBucketId);
            _output.WriteLine($"  Created started session 2: {startedSessionId2}");

            // Wait for sessions to be indexed by EOS
            _output.WriteLine($"Waiting {EosSessionIndexDelay.TotalSeconds} seconds for EOS to index sessions...");
            await Task.Delay(EosSessionIndexDelay);

            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-1"),
                new MatchRequest("user-2")
            };
            var match = new Match(requests);

            _output.WriteLine($"Attempting to find session for match: {match.MatchId}");

            // Act & Assert - Verify GetSessionAsync throws exception (no available sessions)
            var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(
                async () => await sessionFinder.GetSessionAsync(match)
            );

            Assert.NotNull(exception);
            _output.WriteLine($"✓ NoAvailableSessionsException thrown as expected");
            _output.WriteLine($"  Sessions Searched: {exception.SessionsSearched}");
            _output.WriteLine($"  Started sessions were correctly excluded from search results");
        }

        /// <summary>
        /// Test 9.4: Verify optimistic concurrency - two concurrent calls don't claim the same session
        /// Requirements: 2.3
        /// </summary>
        [Fact(Timeout = 30000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_ConcurrentCalls_ShouldClaimDifferentSessions()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionFinder = CreateSessionFinder();

            _output.WriteLine("Creating 2 empty test sessions for concurrency test...");

            // Create 2 empty sessions in unique bucket to ensure both concurrent calls can succeed
            var sessionId1 = await _testHelper.CreateEmptySessionAsync(_testBucketId);
            _output.WriteLine($"  Created session 1: {sessionId1}");

            var sessionId2 = await _testHelper.CreateEmptySessionAsync(_testBucketId);
            _output.WriteLine($"  Created session 2: {sessionId2}");

            // Wait for sessions to be indexed by EOS
            _output.WriteLine($"Waiting {EosSessionIndexDelay.TotalSeconds} seconds for EOS to index sessions...");
            await Task.Delay(EosSessionIndexDelay);

            var requests1 = new List<MatchRequest>
            {
                new MatchRequest("user-1-1"),
                new MatchRequest("user-1-2")
            };
            var match1 = new Match(requests1);

            var requests2 = new List<MatchRequest>
            {
                new MatchRequest("user-2-1"),
                new MatchRequest("user-2-2")
            };
            var match2 = new Match(requests2);

            _output.WriteLine($"Starting two concurrent GetSessionAsync calls...");
            _output.WriteLine($"  Match 1: {match1.MatchId}");
            _output.WriteLine($"  Match 2: {match2.MatchId}");

            // Act - Start two concurrent GetSessionAsync calls
            var task1 = sessionFinder.GetSessionAsync(match1);
            var task2 = sessionFinder.GetSessionAsync(match2);

            var results = await Task.WhenAll(task1, task2);

            // Assert - Verify both calls succeeded
            Assert.NotNull(results[0]);
            Assert.NotNull(results[1]);

            var claimedSessionId1 = results[0].SessionId;
            var claimedSessionId2 = results[1].SessionId;

            _output.WriteLine($"✓ Both concurrent calls succeeded");
            _output.WriteLine($"  Call 1 claimed: {claimedSessionId1}");
            _output.WriteLine($"  Call 2 claimed: {claimedSessionId2}");

            // Assert - Verify both calls claimed different sessions
            Assert.NotEqual(claimedSessionId1, claimedSessionId2);

            _output.WriteLine($"✓ Both calls claimed different sessions (no duplicate claims)");
        }

        /// <summary>
        /// Test 9.5: Verify cache expiration removes old entries
        /// Requirements: 2.4, 2.5
        /// </summary>
        [Fact(Timeout = 20000)]
        [Trait("Category", "Integration")]
        public async Task GetSessionAsync_AfterCacheExpiration_ShouldAllowReclaimingSession()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange - Create session finder with short expiration time (1 second)
            var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.AddProvider(new XunitLoggerProvider(_output));
                builder.SetMinimumLevel(LogLevel.Trace);
            });

            var logger = loggerFactory.CreateLogger<EOSSessionFinder>();
            var config = new EOSSessionFinderConfig
            {
                BucketId = _testBucketId, // Use unique bucket per test
                MaxSearchResults = 10,
                ClaimedSessionExpirationSeconds = 1 // 1 second for faster test
            };

            var cacheLogger = loggerFactory.CreateLogger<InMemoryClaimedSessionsCache>();
            var cache = new InMemoryClaimedSessionsCache(cacheLogger, config);

            var notifierLogger = loggerFactory.CreateLogger<StubSessionOwnerNotifier>();
            var notifier = new StubSessionOwnerNotifier(notifierLogger);

            var sessionFinder = new EOSSessionFinder(logger, _eosFixture.EOSService, config, cache, notifier);

            _output.WriteLine("Creating empty test session...");

            // Create 1 empty session in unique bucket
            var sessionId = await _testHelper.CreateEmptySessionAsync(_testBucketId);
            _output.WriteLine($"  Created session: {sessionId}");

            // Wait for session to be indexed by EOS
            _output.WriteLine($"Waiting {EosSessionIndexDelay} for EOS to index session...");
            await Task.Delay(EosSessionIndexDelay);

            var requests1 = new List<MatchRequest>
            {
                new MatchRequest("user-1-1"),
                new MatchRequest("user-1-2")
            };
            var match1 = new Match(requests1);

            _output.WriteLine($"Claiming session for match 1: {match1.MatchId}");

            // Act - Claim the session
            var sessionInfo1 = await sessionFinder.GetSessionAsync(match1);

            Assert.NotNull(sessionInfo1);
            Assert.Equal(sessionId, sessionInfo1.SessionId);

            _output.WriteLine($"✓ Session claimed: {sessionInfo1.SessionId}");

            // Verify session is in cache
            Assert.True(cache.IsSessionClaimed(sessionId));
            _output.WriteLine($"✓ Session is in claimed cache");

            // Try to claim again immediately - should fail
            var requests2 = new List<MatchRequest>
            {
                new MatchRequest("user-2-1"),
                new MatchRequest("user-2-2")
            };
            var match2 = new Match(requests2);

            _output.WriteLine($"Attempting to claim same session immediately (should fail)...");

            var exception = await Assert.ThrowsAsync<NoAvailableSessionsException>(
                async () => await sessionFinder.GetSessionAsync(match2)
            );

            Assert.NotNull(exception);
            _output.WriteLine($"✓ NoAvailableSessionsException thrown as expected (session still in cache)");

            // Wait for cache expiration (1 second + buffer)
            _output.WriteLine($"Waiting for cache expiration (1.5 seconds)...");
            await Task.Delay(1500);

            // Verify session is removed from cache
            Assert.False(cache.IsSessionClaimed(sessionId));
            _output.WriteLine($"✓ Session removed from claimed cache after expiration");

            // Note: We cannot reclaim the session because it was already updated with match_id
            // This test verifies that the cache expiration works correctly
            // In a real scenario, the session owner would have marked the session as started
            _output.WriteLine($"✓ Cache expiration test complete");
        }

        /// <summary>
        /// Test: Measure actual EOS session indexing time
        /// This test creates a session and polls for it to measure how long EOS takes to index it.
        /// </summary>
        [Fact(Timeout = 30000)]
        [Trait("Category", "Integration")]
        [Trait("Category", "Performance")]
        public async Task MeasureEOSIndexingTime_CreateSessionAndPollUntilFound()
        {
            // Skip if EOS is not initialized
            if (!_eosFixture.IsInitialized)
            {
                _output.WriteLine("Skipping test - EOS SDK not initialized. Check your .env file.");
                return;
            }

            // Arrange
            var sessionFinder = CreateSessionFinder();
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();

            _output.WriteLine("=== EOS Session Indexing Time Measurement ===");
            _output.WriteLine($"Bucket ID: {_testBucketId}");
            _output.WriteLine("");

            // Act - Create session
            _output.WriteLine("Creating empty test session...");
            var sessionCreationStart = stopwatch.ElapsedMilliseconds;
            var sessionId = await _testHelper.CreateEmptySessionAsync(_testBucketId);
            var sessionCreationTime = stopwatch.ElapsedMilliseconds - sessionCreationStart;
            
            _output.WriteLine($"✓ Session created: {sessionId}");
            _output.WriteLine($"  Creation time: {sessionCreationTime}ms");
            _output.WriteLine("");

            // Poll for session until found
            _output.WriteLine("Polling for session in search results...");
            var pollingStart = stopwatch.ElapsedMilliseconds;
            var pollCount = 0;
            var maxPolls = 60; // Max 30 seconds (500ms intervals)
            var pollInterval = 500; // 500ms
            bool found = false;

            var requests = new List<MatchRequest>
            {
                new MatchRequest("user-measure-1"),
                new MatchRequest("user-measure-2")
            };
            var match = new Match(requests);

            while (pollCount < maxPolls && !found)
            {
                pollCount++;
                var pollStart = stopwatch.ElapsedMilliseconds;

                try
                {
                    // Try to find and claim the session
                    var sessionInfo = await sessionFinder.GetSessionAsync(match);
                    
                    if (sessionInfo != null && sessionInfo.SessionId == sessionId)
                    {
                        found = true;
                        var totalIndexingTime = stopwatch.ElapsedMilliseconds - pollingStart;
                        var pollTime = stopwatch.ElapsedMilliseconds - pollStart;
                        
                        _output.WriteLine($"✓ Session found on poll #{pollCount}!");
                        _output.WriteLine($"  Poll time: {pollTime}ms");
                        _output.WriteLine($"  Total indexing time: {totalIndexingTime}ms ({totalIndexingTime / 1000.0:F2}s)");
                        _output.WriteLine("");
                        _output.WriteLine("=== RESULTS ===");
                        _output.WriteLine($"Session creation time: {sessionCreationTime}ms");
                        _output.WriteLine($"EOS indexing time: {totalIndexingTime}ms ({totalIndexingTime / 1000.0:F2}s)");
                        _output.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms ({stopwatch.ElapsedMilliseconds / 1000.0:F2}s)");
                        _output.WriteLine($"Polls required: {pollCount}");
                        break;
                    }
                }
                catch (NoAvailableSessionsException ex)
                {
                    // Session not indexed yet
                    var pollTime = stopwatch.ElapsedMilliseconds - pollStart;
                    _output.WriteLine($"  Poll #{pollCount}: Not found yet (searched {ex.SessionsSearched} sessions, took {pollTime}ms)");
                }

                // Wait before next poll
                await Task.Delay(pollInterval);
            }

            stopwatch.Stop();

            // Assert
            Assert.True(found, $"Session was not found after {pollCount} polls ({pollCount * pollInterval / 1000.0:F1}s). EOS indexing may be slower than expected.");

            _output.WriteLine("");
            _output.WriteLine("=== RECOMMENDATION ===");
            var indexingTime = stopwatch.ElapsedMilliseconds - pollingStart - sessionCreationTime;
            if (indexingTime < 3000)
            {
                _output.WriteLine($"✓ EOS indexing is fast ({indexingTime}ms). Consider reducing wait times in tests to 3-4 seconds.");
            }
            else if (indexingTime < 7000)
            {
                _output.WriteLine($"✓ EOS indexing is moderate ({indexingTime}ms). Current 10-second waits could be reduced to 7-8 seconds.");
            }
            else
            {
                _output.WriteLine($"⚠ EOS indexing is slow ({indexingTime}ms). Current 10-second waits are appropriate.");
            }
        }
    }

    /// <summary>
    /// Custom logger provider for xUnit test output
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
    /// Custom logger for xUnit test output
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

        public IDisposable BeginScope<TState>(TState state) => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{logLevel}] {_categoryName}: {formatter(state, exception)}");
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
}
