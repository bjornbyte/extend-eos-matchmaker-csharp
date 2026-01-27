# Setup Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide provides complete instructions for setting up, configuring, and deploying the Simple EOS Matchmaking Service Extension.

## Prerequisites

### Development Tools

Windows 11 WSL2 or Linux Ubuntu 22.04 or macOS 14+ with the following tools installed:

a. Bash

b. Make

c. Docker (Docker Desktop 4.30+/Docker Engine v23.0+)

d. .NET 8.0+ SDK

e. Postman

f. extend-helper-cli

> ❗ In macOS, you may use Homebrew to easily install some of the tools above.

### Epic Online Services (EOS) SDK

The service requires the EOS SDK to be downloaded and placed in the project directory. The SDK is not included in the repository.

### AccelByte Account

You need an AccelByte Gaming Services (AGS) account with:

1. **AGS Environment** - Test or production environment
2. **Namespace** - Your game namespace
3. **OAuth Client** - Confidential client

### Epic Online Services (EOS) Account

You need an Epic Games Developer Portal account with:

1. **Product** - Your game product in EOS
2. **Sandbox** - Development or production sandbox
3. **Deployment** - Active deployment configuration
4. **Client Credentials** - Client ID and Client Secret

#### Getting EOS Credentials

1. Go to [Epic Games Developer Portal](https://dev.epicgames.com/)
2. Navigate to your Product
3. Go to **Product Settings** 

From there you can copy the product ID, client ID and secret, sandbox ID, and deployment ID.

#### Downloading the EOS SDK

1. Go to [Epic Games Developer Portal](https://dev.epicgames.com/portal)
2. Navigate to **Downloads & Release notes** 
3. Download the **EOS SDK for C#** (version 1.18.1.2 or later recommended)
4. Extract the downloaded archive
5. Copy the SDK contents to the `EOS-SDK` directory in your project root

**Expected directory structure after extraction:**

```
extend-eos-matchmaker-csharp/
├── EOS-SDK/
│   ├── Bin/
│   │   ├── EOSSDK-Win64-Shipping.dll
│   │   ├── libEOSSDK-Linux-Shipping.so
│   │   ├── libEOSSDK-LinuxArm64-Shipping.so
│   │   └── libEOSSDK-Mac-Shipping.dylib
│   ├── Source/
│   │   └── (C# source files)
│   └── Tools/
│       └── (SDK tools)
├── src/
├── gateway/
└── ...
```

> ⚠️ **Important:** The `EOS-SDK` directory is excluded from version control (`.gitignore`). Each developer must download and place the SDK locally. The build process requires the SDK to be present in this exact location.

---

## Local Development Setup

### 1. Clone the Repository

```bash
git clone <repository-url>
cd extend-eos-matchmaker-csharp
```

### 2. Create Environment File

```bash
cp .env.template .env
```

### 3. Configure Environment Variables

Edit `.env` with your credentials. See [Configuration](#configuration) section below for detailed explanation of all parameters.

```bash
# AccelByte Configuration (Required)
AB_BASE_URL=https://test.accelbyte.io
AB_CLIENT_ID=your-client-id
AB_CLIENT_SECRET=your-client-secret
AB_NAMESPACE=your-namespace

# EOS Configuration (Required)
EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-eos-client-id
EOS_CLIENT_SECRET=your-eos-client-secret

# Service Configuration (Required)
BASE_PATH=/matchmaking

# Optional - only set if changing defaults
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true

# Optional - can also configure in appsettings.json
# MATCHMAKER__MATCHSIZE=2
# SESSIONPROVIDER__MODE=create
```

> Environment variables override `appsettings.json` values. Use `__` (double underscore) for nested configuration. See [Configuration](#configuration) section for details.

### 4. Build and Run with Docker

```bash
docker compose up --build
```

The service will be available at:
- **REST API**: `http://localhost:8000/matchmaking`
- **Swagger UI**: `http://localhost:8000/matchmaking/apidocs/`
- **Metrics**: `http://localhost:8080/metrics`

### 5. Verify Service is Running

```bash
curl http://localhost:8000/matchmaking/apidocs/
```

You should see the Swagger UI HTML response.

---

## Configuration

Configuration can be set via environment variables or `appsettings.json`. Environment variables override `appsettings.json` values.

### How Environment Variables Work

ASP.NET Core's `WebApplication.CreateBuilder(args)` automatically loads environment variables into the configuration system. For hierarchical configuration (nested JSON), use double underscores (`__`) in environment variable names, which are automatically converted to colons (`:`) in the configuration hierarchy.

**Example:** `MATCHMAKER__MATCHSIZE=4` maps to `MatchMaker:MatchSize` in configuration.

> See [Microsoft's Configuration documentation](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/configuration/#environment-variables) for details on environment variable mapping.

### Environment Variables Reference

| Variable | Required | Default | Description | Where to Get / When to Change |
|----------|----------|---------|-------------|-------------------------------|
| **AccelByte Configuration** |||||
| `AB_BASE_URL` | ✅ Yes | - | AGS base URL (e.g., `https://test.accelbyte.io`) | AGS Admin Portal → Your environment URL |
| `AB_NAMESPACE` | ✅ Yes | - | Your game namespace | AGS Admin Portal → Namespace settings |
| `AB_CLIENT_ID` | ✅ Yes | - | OAuth client ID | AGS Admin Portal → Admin → Namespace → Integration → OAuth Clients |
| `AB_CLIENT_SECRET` | ✅ Yes | - | OAuth client secret | Same as above (use confidential client with matchmaking permissions) |
| **EOS SDK Configuration** |||||
| `EOS_PRODUCT_ID` | ✅ Yes | - | EOS product ID | [Epic Developer Portal](https://dev.epicgames.com/) → Product → Product Settings |
| `EOS_SANDBOX_ID` | ✅ Yes | - | EOS sandbox ID | Same as above |
| `EOS_DEPLOYMENT_ID` | ✅ Yes | - | EOS deployment ID | Same as above |
| `EOS_CLIENT_ID` | ✅ Yes | - | EOS client ID | Same as above |
| `EOS_CLIENT_SECRET` | ✅ Yes | - | EOS client secret | Same as above |
| **Service Configuration** |||||
| `PLUGIN_GRPC_SERVER_AUTH_ENABLED` | No | `true` | Enable/disable authorization | Set to `false` for local testing only. ⚠️ Never disable in production |
| `BASE_PATH` | ✅ Yes | - | Service URL path prefix (e.g., `/matchmaking`) | Used by gateway for routing. Must start with `/`. Affects all endpoints |
| **Observability Configuration** |||||
| `OTEL_SERVICE_NAME` | No | `extend-app-service-extension` | Service name for OpenTelemetry tracing | Used to identify this service in distributed traces. Automatically prefixed with `extend-app-` |
| `OTEL_EXPORTER_ZIPKIN_ENDPOINT` | No | - | Zipkin endpoint URL (e.g., `http://zipkin:9411/api/v2/spans`) | Required only if using Zipkin for distributed tracing. See [Operations Guide](operations.md#observability) |
| **Matchmaker Configuration** (optional - can also use appsettings.json) |||||
| `MATCHMAKER__MATCHSIZE` | No | `2` | Number of players per match | Change based on game mode (e.g., 4 for squad, 10 for team deathmatch) |
| `MATCHMAKER__TICKINTERVALSECONDS` | No | `1` | How often matcher runs (seconds) | Increase to reduce CPU in low-traffic; decrease for faster matching in high-traffic |
| `MATCHMAKER__REQUESTTIMEOUTSECONDS` | No | `60` | Request expiration time (seconds) | Increase for longer wait times; decrease for fast-paced games |
| **Session Provider Configuration** (optional - can also use appsettings.json) |||||
| `SESSIONPROVIDER__MODE` | No | `create` | Session provider mode: `create` or `find` | Use `find` if game servers create EOS sessions. See [Architecture Guide](architecture.md#session-provider-modes) |
| **Session Finder Configuration** (optional - can also use appsettings.json, only used when Mode=find) |||||
| `SESSIONFINDER__BUCKETID` | No | `default` | EOS bucket to search for sessions | Use different buckets for game modes or regions |
| `SESSIONFINDER__MAXSEARCHRESULTS` | No | `10` | Max sessions per search | Increase for many available sessions; decrease to reduce API calls |
| `SESSIONFINDER__CLAIMEDSESSIONEXPIRATIONSECONDS` | No | `300` | Claimed session cache time (seconds) | Increase for longer setup times; decrease for faster re-claiming |

### Application Settings (appsettings.json)

Alternatively, configure application behavior via `appsettings.json`:

For optional configuration like matchmaker behavior and session provider mode, edit `appsettings.json`:

```json
{
  "MatchMaker": {
    "MatchSize": 2,
    "TickIntervalSeconds": 1,
    "RequestTimeoutSeconds": 60
  },
  "SessionProvider": {
    "Mode": "create"
  },
  "SessionFinder": {
    "BucketId": "default",
    "MaxSearchResults": 10,
    "ClaimedSessionExpirationSeconds": 300
  }
}
```
---
## Local Development Without Docker

### 1. Install .NET 8.0+ SDK

**For Windows and macOS:**

Download from [Browse all .NET versions](https://dotnet.microsoft.com/download/dotnet)

**For Ubuntu:**

```bash
sudo apt-get update && sudo apt-get install -y dotnet-sdk-8.0
```

> Note: .NET 8.0 or later is required.

### 2. Restore Dependencies

```bash
cd src
dotnet restore
```

### 3. Configure Settings

Edit `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/appsettings.json` with your credentials and settings. See [Configuration](#configuration) section for detailed explanation of all parameters.

```json
{
  "AccelByte": {
    "BaseUrl": "https://test.accelbyte.io",
    "ClientId": "your-client-id",
    "ClientSecret": "your-client-secret",
    "Namespace": "your-namespace"
  },
  "EOS": {
    "ProductId": "your-product-id",
    "SandboxId": "your-sandbox-id",
    "DeploymentId": "your-deployment-id",
    "ClientId": "your-eos-client-id",
    "ClientSecret": "your-eos-client-secret"
  },
  "MatchMaker": {
    "MatchSize": 2,
    "TickIntervalSeconds": 1,
    "RequestTimeoutSeconds": 60
  },
  "SessionProvider": {
    "Mode": "create"
  }
}
```

### 4. Run the Service

```bash
cd src/AccelByte.Extend.SimpleEOSMatchmaking.Server
dotnet run
```

### 5. Run Tests

```bash
cd src
dotnet test
```

---

## Deployment to AccelByte

### Deployment Steps

#### 1. Create an Extend Service Extension App

If you do not already have one, create a new Extend Service Extension App in the AGS Admin Portal:
- Navigate to **Extend** → **Service Extension**
- Click **Create New App**
- Enter app name and description
- Note the app name for deployment

On the App Detail page, under the **Environment Configuration** section, set the required environment variables. See [Configuration](#configuration) section for detailed explanation of all parameters.

**Required:** All AccelByte and EOS credentials (mark secrets as secret)
**Optional:** Only set if changing defaults (MatchMaker settings, SessionProvider mode, etc.)

> See [Deployment Scenarios](#deployment-scenarios) for single-instance vs multi-instance deployment guidance.

#### 2. Build and Push the Container Image

Use extend-helper-cli to build and upload the container image:

```bash
extend-helper-cli image-upload --login --namespace <namespace> --app <app-name> --image-tag v0.0.1
```

> ⚠️ Run this command from your project directory.

#### 3. Deploy the Image

On the App Detail page:
1. Go to your app's **Versions** tab
2. Select the uploaded image version
3. Click **Deploy**
4. Wait for deployment to complete
5. Note the service URL

#### 4. Verify Deployment

```bash
curl https://<your-app-url>/apidocs/
```

---

## Testing the Deployment

For comprehensive testing instructions, see the **[Testing Guide](testing_guide.md)**.

Quick verification:
```bash
curl https://<your-app-url>/apidocs/
```

---

## Troubleshooting

For troubleshooting issues, see the **[Operations Guide](operations.md#troubleshooting)**.

Common setup issues:
- **Service won't start**: Check `.env` file has all required variables, verify EOS credentials
- **Port conflicts**: Change ports in `docker-compose.yaml`
- **EOS session creation fails**: Verify EOS Product/Sandbox/Deployment exist in Epic portal

---

## Next Steps

- **Test the service**: See [Testing Guide](testing_guide.md)
- **Understand the architecture**: See [Architecture Guide](architecture.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
- **Use Dev Containers**: See [Dev Container Guide](devcontainer.md)
