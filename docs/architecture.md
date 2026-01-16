# Architecture Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide explains the technical architecture, design decisions, and key concepts of the Guild Progress Service Extension.

## Architecture Overview

### Request Flow

1. Client calls REST endpoint on the gateway (HTTP).
2. Gateway forwards to gRPC handler.
3. Auth interceptor validates bearer token and permission metadata.
4. Handler processes the request and interacts with AccelByte Cloud Save service.
5. Handler returns the response through the gateway.

### Core Components

The service consists of the following core components:

- **Gateway**: `gateway/main.go` wires REST to gRPC, and serves Swagger JSON/UI.
- **Auth**: `Classes/AuthorizationInterceptor.cs` enforces token + permission checks declared in proto.
- **Guild Service**: `Services/MyService.cs` implements guild progress creation, update, and retrieval.
- **Cloud Save Integration**: Uses AccelByte SDK to store and retrieve guild progress data.
- **Observability**: `Program.cs` sets up Prometheus metrics, OpenTelemetry tracing, and health checks.

---

## Data Model

The service manages guild progress data with the following structure:

### GuildProgress

- **guild_id**: Unique identifier for the guild (auto-generated if not provided)
- **namespace**: AccelByte namespace where the guild exists
- **objectives**: Map of objective names to progress values (key-value pairs)

### Storage

Guild progress data is stored in AccelByte Cloud Save service using game records:
- **Record Key**: `guildProgress_{guild_id}`
- **Record Type**: Game Record (admin-managed)
- **Namespace**: Scoped to the requesting namespace

---

## Authorization & Permissions

The service uses AccelByte IAM's permission-based authorization to secure endpoints:

### Endpoints

- `POST /v1/admin/namespace/{namespace}/progress` - Create or update guild progress
  - Requires: `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE]`
  
- `GET /v1/admin/namespace/{namespace}/progress/{guild_id}` - Get guild progress
  - Requires: `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [READ]`

### How Authorization Works

1. **Token Validation**: The auth interceptor validates the Bearer token with AccelByte IAM
2. **Permission Check**: The interceptor verifies the token has the required permission declared in the proto file
3. **Namespace Validation**: Ensures the token has access to the requested namespace
4. **Request Processing**: If authorized, the service processes the request

### Security Notes

- All endpoints require valid Bearer tokens
- Permissions are enforced based on proto file declarations
- Token validation is handled by AccelByte SDK
- Authorization can be disabled for development by setting `PLUGIN_GRPC_SERVER_AUTH_ENABLED=false`

---

## Guild ID Generation

When creating or updating guild progress:

- If a `guild_id` is provided in the request, it will be used as-is
- If `guild_id` is empty or not provided, a new GUID will be generated automatically
- The generated GUID has hyphens removed for consistency

This ensures every guild progress record has a unique identifier.

---

## Architecture Decisions

### Cloud Save Integration

The service uses AccelByte Cloud Save's Game Record feature to store guild progress:

- **Admin-managed records**: Only accessible via admin endpoints with proper permissions
- **Namespace isolation**: Each namespace has its own set of guild records
- **Key-based access**: Fast retrieval using predictable key pattern `guildProgress_{guild_id}`
- **JSON storage**: Flexible schema for objectives map

### gRPC with REST Gateway

The service implements a dual-protocol approach:

- **gRPC Server**: High-performance binary protocol for internal services
- **REST Gateway**: HTTP/JSON API for game clients and external integrations
- **Single Implementation**: Business logic written once in C#, exposed via both protocols
- **Swagger Documentation**: Auto-generated API documentation from proto definitions

### Interceptor-Based Authorization

Authorization is implemented as a gRPC interceptor:

- **Declarative Permissions**: Defined in proto file using custom options
- **Automatic Enforcement**: No need to check permissions in business logic
- **Consistent Security**: All endpoints protected uniformly
- **Development Mode**: Can be disabled for local testing

---

## Next Steps

- **Set up the service**: See [Setup Guide](setup.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
- **Test the service**: See [Testing Guide](testing_guide.md)
