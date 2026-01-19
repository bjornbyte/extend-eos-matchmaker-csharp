# Operations Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide covers testing, monitoring, error handling, troubleshooting, and development workflows for the Simple EOS Matchmaking Service.

## Testing

### Local Testing with Swagger UI

The recommended way to test the service is using Swagger UI.

1. **Run the service:**
   ```bash
   docker compose up --build
   ```

2. **Get an access token:**
   
   Use [demo/get-access-token.postman_collection.json](../demo/get-access-token.postman_collection.json) to obtain a token.
   
   Required Postman environment variables:
   - `AB_BASE_URL`: https://test.accelbyte.io
   - `AB_CLIENT_ID`: Your OAuth client ID
   - `AB_CLIENT_SECRET`: Your OAuth client secret
   - `AB_USERNAME`: Test user email
   - `AB_PASSWORD`: Test user password

3. **Access Swagger UI:**
   
   Open `http://localhost:8000/eos-matchmaking/apidocs/`
   
   > The URL path depends on your `BASE_PATH` setting.

4. **Authorize Swagger UI:**
   
   Click "Authorize" button and enter:
   ```
   Bearer <your_access_token>
   ```

5. **Test Endpoints:**
   - `POST /matchmaking/v1/request` - Submit match request
   - `GET /matchmaking/v1/request/{request_id}` - Get match status
   - `DELETE /matchmaking/v1/request/{request_id}` - Cancel match request

### Complete Matchmaking Flow

Here's a typical matchmaking flow to test:

1. **Submit Match Requests** - Have 2 or more players submit requests
   ```
   POST /eos-matchmaking/matchmaking/v1/request
   Body: { "metadata": { "region": "us-west" } }
   ```
   Each player receives a unique `request_id`

2. **Check Status** - Query the status
   ```
   GET /eos-matchmaking/matchmaking/v1/request/{request_id}
   ```
   Status will be "PENDING" initially

3. **Wait for Match** - The background matcher runs every second
   - When enough players are in the pool, they are automatically matched
   - An EOS session is created
   - Request status changes to "MATCHED"

4. **Get Match Details** - Query again to get session information
   ```
   GET /eos-matchmaking/matchmaking/v1/request/{request_id}
   ```
   Response includes `session_id` and matched player information

5. **Cancel Request** (Optional) - Cancel a pending request
   ```
   DELETE /eos-matchmaking/matchmaking/v1/request/{request_id}
   ```
   Only works for pending requests

### Running Unit Tests

The project includes comprehensive unit tests:

```bash
dotnet test src/extend-service-extension-server.sln
```

All tests should pass before deployment.

---

## Observability

### Overview

The service includes built-in observability features:

- **Metrics**: Prometheus metrics at `:8080/metrics`
- **Tracing**: OpenTelemetry distributed tracing
- **Logging**: Structured JSON logs

### Local Development Setup

To see observability in action locally:

1. **Install Docker Loki plugin:**
   ```bash
   docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions
   ```

2. **Uncomment loki logging driver** in [docker-compose.yaml](../docker-compose.yaml):
   ```yaml
   logging:
     driver: loki
     options:
       loki-url: http://host.docker.internal:3100/loki/api/v1/push
       mode: non-blocking
       max-buffer-size: 4m
       loki-retries: "3"
   ```

3. **Clone and run grpc-plugin-dependencies:**
   ```bash
   git clone https://github.com/AccelByte/grpc-plugin-dependencies.git
   cd grpc-plugin-dependencies
   docker compose up
   ```
   
   Grafana will be accessible at http://localhost:3000

4. **Perform testing** to generate logs and metrics

### Production Observability

Configure these environment variables:

- `OTEL_EXPORTER_ZIPKIN_ENDPOINT` - Zipkin endpoint for tracing
- `OTEL_SERVICE_NAME` - Service name for tracing
- `LOG_LEVEL` - Log level: debug, info, warn, error

---

## Error Codes

### gRPC Error Responses

The service returns gRPC errors with this structure:

```json
{
  "code": 5,
  "message": "Match request not found",
  "details": []
}
```

### Common Error Scenarios

**Match Request Not Found (404)**
```json
{
  "code": 5,
  "message": "Match request not found"
}
```
→ Request ID doesn't exist or was already processed

**User Already Has Pending Request (409)**
```json
{
  "code": 9,
  "message": "User already has a pending match request: {request_id}"
}
```
→ User must cancel existing request before submitting new one

**Cannot Cancel Non-Pending Request (412)**
```json
{
  "code": 9,
  "message": "Cannot cancel request with status: MATCHED"
}
```
→ Only PENDING requests can be cancelled

**Unauthenticated (401)**
```json
{
  "code": 16,
  "message": "Authorization required"
}
```
→ Missing or invalid Bearer token

### gRPC Code to HTTP Status Mapping

| gRPC Code | gRPC Name | HTTP Equiv | When to Retry |
|-----------|-----------|------------|---------------|
| 5 | NotFound | 404 | ❌ Never |
| 9 | FailedPrecondition | 412 | ❌ Never |
| 13 | Internal | 500 | 🔄 Yes |
| 16 | Unauthenticated | 401 | 🔑 Refresh token |

---

## Troubleshooting

### Service Won't Start

**Symptom:** Service fails to start with configuration error

**Solution:**
- Check `.env` file has all required variables
- Verify EOS credentials are correct
- Check Docker is running
- Review service logs: `docker compose logs -f`

### Match Requests Not Matching

**Symptom:** Requests stay in PENDING status

**Solution:**
- Check MatchMaker is running (should see tick logs)
- Verify enough requests in pool (need >= MatchSize)
- Check EOS credentials are valid
- Review MatchMaker logs for errors

### EOS Session Creation Fails

**Symptom:** Requests return to pool, no matches created

**Solution:**
- Verify EOS Product, Sandbox, Deployment exist
- Check EOS Client ID and Secret are valid
- Ensure EOS SDK initialized successfully
- Check service logs for EOS error codes

### Permission Denied

**Symptom:** 403 Forbidden errors

**Solution:**
- Verify OAuth client has required permissions
- Check token is valid and not expired
- Ensure namespace matches request
- Regenerate access token

---

## Development

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Regenerate Protocol Buffers

After modifying `Protos/matchmaking.proto`:

```bash
dotnet build
```

This regenerates:
- C# gRPC service stubs
- Gateway integration code
- Swagger/OpenAPI specification

---

## Next Steps

- [Testing Guide](testing_guide.md) - Comprehensive testing instructions
- [Architecture Guide](architecture.md) - Technical architecture details
- [AccelByte Docs](https://docs.accelbyte.io/gaming-services/services/extend/service-extension/) - Official documentation
