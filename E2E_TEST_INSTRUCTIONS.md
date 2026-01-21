# E2E Test Instructions

## Running E2E Tests After Code Changes

When you make changes to the service implementation, you need to restart the service before running E2E tests.

### Step 1: Stop the Current Service

```bash
docker compose down
```

### Step 2: Rebuild and Start the Service

```bash
docker compose up --build
```

This will:
- Rebuild the Docker image with your latest code changes
- Start the service on the configured ports (6565, 8080, 8000)
- Make the service available for E2E testing

### Step 3: Run E2E Tests

In a separate terminal, run:

```bash
dotnet test src/extend-service-extension-server.sln --filter "Category=E2E"
```

Or to run all tests including E2E:

```bash
dotnet test src/extend-service-extension-server.sln
```

## Important Notes

- **E2E tests are NOT backward compatible** - they test the current implementation
- Always rebuild the Docker image when testing new features
- E2E tests run against a live service at `http://127.0.0.1:8000/matchmaking`
- Make sure the service is healthy before running tests (the tests will check this automatically)

## Retention Feature E2E Tests

The retention feature adds two key E2E test verifications:

1. **Cancelled requests are queryable**: After cancelling a request, it should be queryable with `CANCELLED` status
2. **Matched requests are queryable**: After two players are matched, both requests should be queryable with `MATCHED` status and the same session ID

These tests verify that the completed request store is working correctly in the live service.
