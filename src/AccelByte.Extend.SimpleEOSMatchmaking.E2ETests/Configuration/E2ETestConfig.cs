namespace AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Configuration;

/// <summary>
/// Configuration for E2E tests
/// </summary>
public class E2ETestConfig
{
    /// <summary>
    /// Base URL of the matchmaking service (e.g., http://127.0.0.1:8000/eos-matchmaking)
    /// Note: Using 127.0.0.1 instead of localhost because Docker port mapping only exposes IPv4
    /// </summary>
    public string ServiceBaseUrl { get; set; } = "http://127.0.0.1:8000/eos-matchmaking";

    /// <summary>
    /// Authentication mode: "Bearer" or "UserId"
    /// </summary>
    public AuthMode AuthenticationMode { get; set; } = AuthMode.UserId;

    /// <summary>
    /// Bearer token to use when AuthenticationMode is Bearer
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// Default user ID to use for tests
    /// </summary>
    public string DefaultUserId { get; set; } = "test-user";

    /// <summary>
    /// Timeout for waiting for matches to be created
    /// </summary>
    public TimeSpan MatcherWaitTime { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Load configuration from environment variables or use defaults
    /// </summary>
    public static E2ETestConfig FromEnvironment()
    {
        var config = new E2ETestConfig();

        var serviceUrl = Environment.GetEnvironmentVariable("E2E_SERVICE_URL");
        if (!string.IsNullOrEmpty(serviceUrl))
        {
            config.ServiceBaseUrl = serviceUrl;
        }

        var authMode = Environment.GetEnvironmentVariable("E2E_AUTH_MODE");
        if (!string.IsNullOrEmpty(authMode) && Enum.TryParse<AuthMode>(authMode, true, out var mode))
        {
            config.AuthenticationMode = mode;
        }

        var bearerToken = Environment.GetEnvironmentVariable("E2E_BEARER_TOKEN");
        if (!string.IsNullOrEmpty(bearerToken))
        {
            config.BearerToken = bearerToken;
        }

        var userId = Environment.GetEnvironmentVariable("E2E_USER_ID");
        if (!string.IsNullOrEmpty(userId))
        {
            config.DefaultUserId = userId;
        }

        return config;
    }
}

/// <summary>
/// Authentication mode for E2E tests
/// </summary>
public enum AuthMode
{
    /// <summary>
    /// Use user-id header (for testing with auth disabled)
    /// </summary>
    UserId,

    /// <summary>
    /// Use Authorization: Bearer token header (for testing with auth enabled)
    /// </summary>
    Bearer
}
