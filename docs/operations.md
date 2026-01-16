# Operations Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide covers testing, monitoring, error handling, troubleshooting, and development workflows for the Guild Progress Service Extension.

## Testing

### Test in Local Development Environment

#### Quick Start with Swagger UI

The recommended way to test this service is using the Swagger UI interface.

1. **Run the service**:

   ```shell
   docker compose up --build
   ```

2. **Get an access token**:
   
   Use [demo/get-access-token.postman_collection.json](../demo/get-access-token.postman_collection.json) to obtain an access token.

   Required Postman environment variables:
   - `AB_BASE_URL`: https://test.accelbyte.io
   - `AB_CLIENT_ID`: Your OAuth client ID
   - `AB_CLIENT_SECRET`: Your OAuth client secret
   - `AB_USERNAME`: Test user email (for user token)
   - `AB_PASSWORD`: Test user password (for user token)

3. **Access Swagger UI**:
   
   Open `http://localhost:8000/guild/apidocs/`
   
   > :information_source: The URL path depends on your `BASE_PATH` setting. Format: `http://localhost:8000{BASE_PATH}/apidocs/`

   ![swagger-interface](./images/swagger-interface.png)

4. **Authorize Swagger UI**:

   Click "Authorize" button and enter:
   ```
   Bearer <your_access_token>
   ```

   ![swagger-interface](./images/swagger-authorize.png)

5. **Test Endpoints**:
   - `POST /v1/admin/namespace/{namespace}/progress` - Create or update guild progress
   - `GET /v1/admin/namespace/{namespace}/progress/{guild_id}` - Get guild progress

---

## Observability

### Overview

The service includes built-in observability features:

- **Metrics**: Prometheus metrics available at `:8080/metrics`
- **Tracing**: OpenTelemetry distributed tracing
- **Logging**: Structured JSON logs with configurable levels

### Local Development Setup

To see how observability works in local development, follow these steps:

1. **Uncomment loki logging driver** in [docker-compose.yaml](../docker-compose.yaml):

   ```yaml
   # logging:
   #   driver: loki
   #   options:
   #     loki-url: http://host.docker.internal:3100/loki/api/v1/push
   #     mode: non-blocking
   #     max-buffer-size: 4m
   #     loki-retries: "3"
   ```

   > :warning: **Make sure to install docker loki plugin beforehand**: Otherwise,
   this app will not be able to run. This is required so that container 
   logs can flow to the `loki` service within `grpc-plugin-dependencies` stack. 
   Use this command to install docker loki plugin: 
   `docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions`.

2. **Clone and run grpc-plugin-dependencies** stack alongside this app:

   ```bash
   git clone https://github.com/AccelByte/grpc-plugin-dependencies.git
   cd grpc-plugin-dependencies
   docker compose up
   ```

   After this, Grafana will be accessible at http://localhost:3000.

   > :exclamation: More information about [grpc-plugin-dependencies](https://github.com/AccelByte/grpc-plugin-dependencies) is available [here](https://github.com/AccelByte/grpc-plugin-dependencies/blob/main/README.md).

3. **Perform testing** to generate logs and metrics. For example, by following [Test in Local Development Environment](#test-in-local-development-environment).

### Production Observability

For production deployments, configure the following environment variables:

- `OTEL_EXPORTER_ZIPKIN_ENDPOINT` - Zipkin endpoint for distributed tracing
- `OTEL_SERVICE_NAME` - Service name for tracing (default: `eos-voice-rtc`)
- `LOG_LEVEL` - Log level: `debug`, `info`, `warn`, `error` (default: `info`)

---

## API Error Codes

### Understanding Error Responses

The service returns gRPC errors with the following structure:

```json
{
    "code": 5,                           // Standard gRPC code (0-16)
    "message": "Guild not found.",       // Human-readable message
    "details": []                        // Additional context (optional)
}
```

### Common Error Scenarios

**Scenario 1: Invalid Namespace**

```json
{
    "code": 3,  // gRPC InvalidArgument
    "message": "Invalid namespace"
}
```

→ Namespace format is invalid or doesn't exist. Client should fix the input.

**Scenario 2: Guild Not Found**

```json
{
    "code": 5,  // gRPC NotFound
    "message": "Guild progress not found"
}
```

→ Guild ID is valid but no progress record exists. Client should create one first.

**Scenario 3: Permission Denied**

```json
{
    "code": 7,  // gRPC PermissionDenied
    "message": "Insufficient permissions"
}
```

→ Access token doesn't have required CLOUDSAVE:RECORD permissions.

### gRPC Code to HTTP Status Mapping

| gRPC Code | gRPC Name | HTTP Equiv | When to Retry |
|-----------|-----------|------------|---------------|
| 3 | InvalidArgument | 400 | ❌ Never |
| 5 | NotFound | 404 | ❌ Never |
| 7 | PermissionDenied | 403 | ❌ Never |
| 13 | Internal | 500 | 🔄 Yes |
| 16 | Unauthenticated | 401 | 🔑 Refresh token |

### Client Retry Behavior

**DO NOT RETRY (4xx errors)**: These are client errors indicating bad input or missing resources.
- **gRPC code 3** (InvalidArgument) - Invalid input format, fix the request
- **gRPC code 5** (NotFound) - Resource doesn't exist
- **gRPC code 7** (PermissionDenied) - Fix permissions or use correct token

**SAFE TO RETRY (5xx errors)**: These are server errors that may be temporary.
- **gRPC code 13** (Internal) - Use exponential backoff with max retries

---

## Troubleshooting

### Service Won't Start

**Symptom:**
```
Error: missing required environment variable
```

**Solution**: Check `.env` file contains all required variables from the template.

Ensure the following variables are set:
- `AB_BASE_URL`, `AB_CLIENT_ID`, `AB_CLIENT_SECRET`, `AB_NAMESPACE`
- `BASE_PATH` (must start with `/`)

---

### Permission Denied

**Symptom:**
```
403 Forbidden - insufficient permissions
```

**Solution**:

This error occurs when the **OAuth client calling the service** doesn't have the required CLOUDSAVE:RECORD permissions.

#### For AGS Private Cloud

1. **Identify which OAuth client is calling the service** (your game server or game client)
2. **Add the required permissions to that OAuth client**:
   - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE]` - for creating/updating guild progress
   - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [READ]` - for reading guild progress
3. **Regenerate the access token** after adding permissions
4. **Use the new token** when calling endpoints

#### For AGS Shared Cloud

- Add the following permissions to your OAuth client:
  - Cloud Save -> Game Records (Create, Read, Update, Delete)

---

### Guild Not Found

**Symptom:**
```
404 Not Found - Guild progress not found
```

**Solution**:
- Verify the `guild_id` is correct
- The guild progress must be created before it can be retrieved
- Use the create/update endpoint first to initialize guild progress

---

### Invalid Namespace

**Symptom:**
```
400 Bad Request - Invalid namespace
```

**Solution**:
- Verify the namespace exists in your AccelByte environment
- Check that the OAuth client has access to this namespace
- Ensure the namespace is in active status

---

## Development

### Running Tests

```shell
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Code Linting

The project uses standard .NET code analysis. Configure analysis rules in the `.csproj` file or via `.editorconfig`.

### Regenerate Protocol Buffers

After modifying `Protos/service.proto`:

```shell
dotnet build
```

This regenerates:
- C# gRPC service stubs
- Gateway integration code
- Swagger/OpenAPI specification in `gateway/apidocs/service.swagger.json`

---

## Additional Resources

- **Testing Guide**: [testing_guide.md](testing_guide.md) - Comprehensive testing instructions
- **AccelByte Docs**: [Extend Service Extension](https://docs.accelbyte.io/gaming-services/services/extend/service-extension/)
- **gRPC Gateway**: [grpc-ecosystem/grpc-gateway](https://github.com/grpc-ecosystem/grpc-gateway)
