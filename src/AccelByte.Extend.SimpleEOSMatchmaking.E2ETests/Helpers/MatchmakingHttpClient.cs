using System.Net.Http.Json;
using System.Text.Json;
using System.Diagnostics;
using AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Configuration;
using AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Models;

namespace AccelByte.Extend.SimpleEOSMatchmaking.E2ETests.Helpers;

/// <summary>
/// HTTP client wrapper for matchmaking service with configurable authentication
/// </summary>
public class MatchmakingHttpClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly E2ETestConfig _config;
    private readonly string _userId;

    public MatchmakingHttpClient(E2ETestConfig config, string? userId = null)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _userId = userId ?? config.DefaultUserId;

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(config.ServiceBaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(30) // Increase timeout for health checks
        };

        ConfigureAuthentication();
    }

    private void ConfigureAuthentication()
    {
        switch (_config.AuthenticationMode)
        {
            case AuthMode.UserId:
                // Use grpc-metadata-user-id header for auth-disabled mode
                _httpClient.DefaultRequestHeaders.Add("grpc-metadata-user-id", _userId);
                break;

            case AuthMode.Bearer:
                // Use Authorization Bearer token for auth-enabled mode
                if (string.IsNullOrEmpty(_config.BearerToken))
                {
                    throw new InvalidOperationException(
                        "BearerToken must be configured when using Bearer authentication mode");
                }
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.BearerToken}");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(_config.AuthenticationMode));
        }
    }

    /// <summary>
    /// Submit a match request
    /// </summary>
    public async Task<(SubmitMatchResponse Response, TimeSpan Duration)> SubmitMatchRequestAsync(
        Dictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var request = new SubmitMatchRequest
        {
            Metadata = metadata ?? new Dictionary<string, string>()
        };

        var response = await _httpClient.PostAsJsonAsync(
            "v1/request",
            request,
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<SubmitMatchResponse>(
            cancellationToken: cancellationToken);

        stopwatch.Stop();
        
        return (result ?? throw new InvalidOperationException("Failed to deserialize response"), stopwatch.Elapsed);
    }

    /// <summary>
    /// Get match status
    /// </summary>
    public async Task<(GetMatchStatusResponse? Response, TimeSpan Duration)> GetMatchStatusAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            var response = await _httpClient.GetAsync(
                $"v1/request/{requestId}",
                cancellationToken);

            // Service returns 500 for not found (should be 404, but that's the current behavior)
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound ||
                response.StatusCode == System.Net.HttpStatusCode.InternalServerError)
            {
                stopwatch.Stop();
                return (null, stopwatch.Elapsed);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<GetMatchStatusResponse>(
                cancellationToken: cancellationToken);
            
            stopwatch.Stop();
            return (result, stopwatch.Elapsed);
        }
        catch (HttpRequestException ex) when (
            ex.StatusCode == System.Net.HttpStatusCode.NotFound ||
            ex.StatusCode == System.Net.HttpStatusCode.InternalServerError)
        {
            stopwatch.Stop();
            return (null, stopwatch.Elapsed);
        }
    }

    /// <summary>
    /// Cancel a match request
    /// </summary>
    public async Task<(CancelMatchResponse Response, TimeSpan Duration)> CancelMatchRequestAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        
        var response = await _httpClient.DeleteAsync(
            $"v1/request/{requestId}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<CancelMatchResponse>(
            cancellationToken: cancellationToken);

        stopwatch.Stop();
        
        return (result ?? throw new InvalidOperationException("Failed to deserialize response"), stopwatch.Elapsed);
    }

    /// <summary>
    /// Check if the service is healthy
    /// </summary>
    public async Task<bool> IsServiceHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("apidocs/", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Health check failed: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
