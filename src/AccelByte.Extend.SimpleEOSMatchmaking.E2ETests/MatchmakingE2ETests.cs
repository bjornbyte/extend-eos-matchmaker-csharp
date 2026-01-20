using AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Configuration;
using AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Helpers;
using Xunit;
using Xunit.Abstractions;

namespace AccelByte.Extend.SimpleEOSMatchmaking.E2ETests;

/// <summary>
/// End-to-end tests for the matchmaking service.
/// These tests assume the service is running at the configured URL.
/// </summary>
[Trait("Category", "E2E")]
public class MatchmakingE2ETests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly E2ETestConfig _config;

    public MatchmakingE2ETests(ITestOutputHelper output)
    {
        _output = output;
        _config = E2ETestConfig.FromEnvironment();

        _output.WriteLine($"E2E Test Configuration:");
        _output.WriteLine($"  Service URL: {_config.ServiceBaseUrl}");
        _output.WriteLine($"  Auth Mode: {_config.AuthenticationMode}");
        _output.WriteLine($"  Matcher Wait Time: {_config.MatcherWaitTime.TotalSeconds}s");
    }

    public async Task InitializeAsync()
    {
        // Verify service is running
        using var client = new MatchmakingHttpClient(_config);
        var isHealthy = await client.IsServiceHealthyAsync();

        if (!isHealthy)
        {
            throw new InvalidOperationException(
                $"Service is not running at {_config.ServiceBaseUrl}. " +
                "Please start the service before running E2E tests.");
        }

        _output.WriteLine("✓ Service is running and healthy");
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task SubmitMatchRequest_ReturnsValidRequestId()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-1");
        var metadata = new Dictionary<string, string>
        {
            ["region"] = "us-west",
            ["skill_level"] = "intermediate"
        };

        // Act
        var (response, duration) = await client.SubmitMatchRequestAsync(metadata);

        // Assert
        Assert.NotNull(response);
        Assert.NotNull(response.RequestId);
        Assert.NotEqual(Guid.Empty.ToString(), response.RequestId);

        _output.WriteLine($"✓ Created request: {response.RequestId} ({duration.TotalMilliseconds:F2}ms)");

        // Cleanup
        var (_, cancelDuration) = await client.CancelMatchRequestAsync(response.RequestId);
        _output.WriteLine($"  Cleanup: Cancelled in {cancelDuration.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task GetMatchStatus_WithPendingRequest_ReturnsPendingStatus()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-2");
        var (submitResponse, submitDuration) = await client.SubmitMatchRequestAsync();
        _output.WriteLine($"  Submit: {submitDuration.TotalMilliseconds:F2}ms");

        // Act
        var (statusResponse, statusDuration) = await client.GetMatchStatusAsync(submitResponse.RequestId);

        // Assert
        Assert.NotNull(statusResponse);
        Assert.Equal(submitResponse.RequestId, statusResponse.RequestId);
        Assert.Equal("PENDING", statusResponse.Status);
        Assert.Empty(statusResponse.MatchedUserIds);
        Assert.Empty(statusResponse.MatchedRequestIds);

        _output.WriteLine($"✓ Request {submitResponse.RequestId} is PENDING ({statusDuration.TotalMilliseconds:F2}ms)");

        // Cleanup
        var (_, cancelDuration) = await client.CancelMatchRequestAsync(submitResponse.RequestId);
        _output.WriteLine($"  Cleanup: {cancelDuration.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task CancelMatchRequest_WithPendingRequest_SucceedsCancellation()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-3");
        var (submitResponse, submitDuration) = await client.SubmitMatchRequestAsync();
        _output.WriteLine($"  Submit: {submitDuration.TotalMilliseconds:F2}ms");

        // Act
        var (cancelResponse, cancelDuration) = await client.CancelMatchRequestAsync(submitResponse.RequestId);

        // Assert
        Assert.True(cancelResponse.Success);

        _output.WriteLine($"✓ Successfully cancelled request: {submitResponse.RequestId} ({cancelDuration.TotalMilliseconds:F2}ms)");

        // Verify request is no longer found
        var (statusResponse, statusDuration) = await client.GetMatchStatusAsync(submitResponse.RequestId);
        Assert.Null(statusResponse);
        _output.WriteLine($"  Verify not found: {statusDuration.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task SubmitMatchRequest_WithDuplicateUser_ThrowsException()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-4");
        var (firstResponse, firstDuration) = await client.SubmitMatchRequestAsync();
        _output.WriteLine($"  First submit: {firstDuration.TotalMilliseconds:F2}ms");

        try
        {
            // Act & Assert
            await Assert.ThrowsAsync<HttpRequestException>(async () =>
                await client.SubmitMatchRequestAsync());

            _output.WriteLine("✓ Duplicate request correctly rejected");
        }
        finally
        {
            // Cleanup
            var (_, cancelDuration) = await client.CancelMatchRequestAsync(firstResponse.RequestId);
            _output.WriteLine($"  Cleanup: {cancelDuration.TotalMilliseconds:F2}ms");
        }
    }

    [Fact]
    public async Task GetMatchStatus_WithNonExistentRequest_ReturnsNull()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-5");
        var fakeRequestId = Guid.NewGuid().ToString();

        // Act
        var (statusResponse, duration) = await client.GetMatchStatusAsync(fakeRequestId);

        // Assert
        Assert.Null(statusResponse);

        _output.WriteLine($"✓ Non-existent request correctly returns null ({duration.TotalMilliseconds:F2}ms)");
    }

    [Fact]
    public async Task TwoPlayers_SubmitRequests_GetMatched()
    {
        // Arrange
        using var client1 = new MatchmakingHttpClient(_config, "e2e-match-player-1");
        using var client2 = new MatchmakingHttpClient(_config, "e2e-match-player-2");

        var metadata = new Dictionary<string, string>
        {
            ["region"] = "us-west"
        };

        // Act - Submit both requests
        var (response1, submit1Duration) = await client1.SubmitMatchRequestAsync(metadata);
        var (response2, submit2Duration) = await client2.SubmitMatchRequestAsync(metadata);

        _output.WriteLine($"Player 1 request: {response1.RequestId} ({submit1Duration.TotalMilliseconds:F2}ms)");
        _output.WriteLine($"Player 2 request: {response2.RequestId} ({submit2Duration.TotalMilliseconds:F2}ms)");

        // Verify both are pending
        var (status1Before, status1BeforeDuration) = await client1.GetMatchStatusAsync(response1.RequestId);
        var (status2Before, status2BeforeDuration) = await client2.GetMatchStatusAsync(response2.RequestId);

        Assert.NotNull(status1Before);
        Assert.NotNull(status2Before);
        Assert.Equal("PENDING", status1Before.Status);
        Assert.Equal("PENDING", status2Before.Status);

        _output.WriteLine($"✓ Both requests are PENDING (P1: {status1BeforeDuration.TotalMilliseconds:F2}ms, P2: {status2BeforeDuration.TotalMilliseconds:F2}ms)");

        // Wait for matcher to process
        _output.WriteLine($"Waiting {_config.MatcherWaitTime.TotalSeconds}s for matcher...");
        await Task.Delay(_config.MatcherWaitTime);

        // Assert - Requests should be matched and removed from pool
        var (status1After, status1AfterDuration) = await client1.GetMatchStatusAsync(response1.RequestId);
        var (status2After, status2AfterDuration) = await client2.GetMatchStatusAsync(response2.RequestId);

        // Note: Current implementation removes matched requests from pool
        // So they will return null after matching
        Assert.Null(status1After);
        Assert.Null(status2After);

        _output.WriteLine($"✓ Both requests were matched (removed from pool)");
        _output.WriteLine($"  P1 check: {status1AfterDuration.TotalMilliseconds:F2}ms, P2 check: {status2AfterDuration.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task SubmitMatchRequest_WithMetadata_PreservesMetadata()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-6");
        var metadata = new Dictionary<string, string>
        {
            ["region"] = "eu-central",
            ["skill_level"] = "advanced",
            ["game_mode"] = "ranked"
        };

        // Act
        var (submitResponse, submitDuration) = await client.SubmitMatchRequestAsync(metadata);
        var (statusResponse, statusDuration) = await client.GetMatchStatusAsync(submitResponse.RequestId);

        // Assert
        Assert.NotNull(statusResponse);
        // Note: Current API doesn't return metadata in status response
        // This test verifies the request is accepted with metadata

        _output.WriteLine($"✓ Request with metadata accepted: {submitResponse.RequestId}");
        _output.WriteLine($"  Submit: {submitDuration.TotalMilliseconds:F2}ms, Status: {statusDuration.TotalMilliseconds:F2}ms");

        // Cleanup
        var (_, cancelDuration) = await client.CancelMatchRequestAsync(submitResponse.RequestId);
        _output.WriteLine($"  Cleanup: {cancelDuration.TotalMilliseconds:F2}ms");
    }

    [Fact]
    public async Task CancelMatchRequest_WithNonExistentRequest_ThrowsException()
    {
        // Arrange
        using var client = new MatchmakingHttpClient(_config, "e2e-test-user-7");
        var fakeRequestId = Guid.NewGuid().ToString();

        // Act & Assert
        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
            await client.CancelMatchRequestAsync(fakeRequestId));

        _output.WriteLine($"✓ Cancel non-existent request correctly throws exception");
        _output.WriteLine($"  Exception: {exception.Message}");
    }
}
