# Matchmaking Service E2E Tests

End-to-end tests for the Simple EOS Matchmaking Service. These tests assume the service is running and test the full HTTP API.

## Prerequisites

The matchmaking service must be running before executing these tests:

```bash
docker compose up --build
```

## Running the Tests

### Run all E2E tests

```bash
dotnet test --filter "Category=E2E"
```

### Run from the E2E test project directory

```bash
cd src/AccelByte.Extend.SimpleEOSMatchmaking.E2ETests
dotnet test
```

### Run with verbose output

```bash
dotnet test --logger "console;verbosity=detailed"
```

## Configuration

Tests can be configured via environment variables:

| Variable | Description | Default |
|----------|-------------|---------|
| `E2E_SERVICE_URL` | Base URL of the matchmaking service | `http://localhost:8000/matchmaking` |
| `E2E_AUTH_MODE` | Authentication mode: `UserId` or `Bearer` | `UserId` |
| `E2E_BEARER_TOKEN` | Bearer token (required if `E2E_AUTH_MODE=Bearer`) | - |
| `E2E_USER_ID` | Default user ID for tests | `test-user` |

### Example: Testing with Auth Disabled (UserId mode)

```bash
# Default configuration - uses grpc-metadata-user-id header
dotnet test
```

### Example: Testing with Auth Enabled (Bearer mode)

```bash
# Set environment variables
$env:E2E_AUTH_MODE="Bearer"
$env:E2E_BEARER_TOKEN="your-access-token-here"

# Run tests
dotnet test
```

### Example: Testing against a remote service

```bash
$env:E2E_SERVICE_URL="https://your-service.example.com/matchmaking"
$env:E2E_AUTH_MODE="Bearer"
$env:E2E_BEARER_TOKEN="your-access-token"

dotnet test
```

## Authentication Modes

### UserId Mode (Auth Disabled)

When the service is running with `PLUGIN_GRPC_SERVER_AUTH_ENABLED=false`, tests use the `grpc-metadata-user-id` header:

```csharp
// Automatically configured by MatchmakingHttpClient
headers.Add("grpc-metadata-user-id", "test-user-1");
```

### Bearer Mode (Auth Enabled)

When the service is running with `PLUGIN_GRPC_SERVER_AUTH_ENABLED=true`, tests use the `Authorization` header:

```csharp
// Automatically configured by MatchmakingHttpClient
headers.Add("Authorization", "Bearer your-token-here");
```

## Test Coverage

The E2E test suite covers:

- ✅ Submit match request
- ✅ Get match status (PENDING)
- ✅ Cancel match request
- ✅ Duplicate request rejection
- ✅ Non-existent request handling
- ✅ Two-player matching scenario
- ✅ Metadata preservation
- ✅ Error scenarios

## CI/CD Integration

### GitHub Actions Example

```yaml
- name: Start Service
  run: docker compose up -d

- name: Wait for Service
  run: |
    timeout 30 bash -c 'until curl -f http://localhost:8000/matchmaking/apidocs/; do sleep 1; done'

- name: Run E2E Tests
  run: dotnet test --filter "Category=E2E"
  env:
    E2E_SERVICE_URL: http://localhost:8000/matchmaking
    E2E_AUTH_MODE: UserId

- name: Stop Service
  run: docker compose down
```

## Troubleshooting

### Service Not Running

```
InvalidOperationException: Service is not running at http://localhost:8000/matchmaking
```

**Solution:** Start the service with `docker compose up --build`

### Authentication Errors

```
HttpRequestException: Response status code does not indicate success: 401 (Unauthorized)
```

**Solution:** 
- If using Bearer mode, ensure `E2E_BEARER_TOKEN` is set and valid
- If using UserId mode, ensure `PLUGIN_GRPC_SERVER_AUTH_ENABLED=false` in service `.env`

### Tests Timing Out

If tests fail due to matcher timing, adjust the wait time:

```csharp
// In E2ETestConfig.cs
public TimeSpan MatcherWaitTime { get; set; } = TimeSpan.FromSeconds(5); // Increase if needed
```

## Architecture

```
E2ETests
├── Configuration/
│   └── E2ETestConfig.cs          # Test configuration
├── Helpers/
│   └── MatchmakingHttpClient.cs  # HTTP client wrapper
├── Models/
│   └── MatchmakingModels.cs      # Request/Response DTOs
└── MatchmakingE2ETests.cs        # Test cases
```

## Design Decisions

### Why Separate E2E Tests?

- **Isolation:** E2E tests require a running service, unit tests don't
- **Speed:** E2E tests are slower, can be run separately in CI/CD
- **Clarity:** Clear distinction between unit tests and integration tests

### Why Configurable Authentication?

- **Flexibility:** Test both auth-enabled and auth-disabled modes
- **Realism:** Test with real bearer tokens in staging/production
- **Development:** Easy testing with auth disabled locally

### Why Not Use WebApplicationFactory?

- E2E tests verify the full stack including the Go gateway
- Tests the actual HTTP-to-gRPC translation
- Tests real deployment configuration
- More realistic integration testing

## Related Documentation

- [Testing Guide](../../docs/testing_guide.md) - Manual testing with Postman
- [Setup Guide](../../docs/setup.md) - Service configuration
- [Unit Tests](../AccelByte.Extend.SimpleEOSMatchmaking.Server.Tests/) - Component-level tests
