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

### AccelByte Account

You need an AccelByte Gaming Services (AGS) account with:

1. **AGS Environment** - Test or production environment
2. **Namespace** - Your game namespace
3. **OAuth Client** - Confidential client with required permissions

#### Required Permissions

**For AGS Private Cloud:**
- `ADMIN:NAMESPACE:{namespace}:MATCHMAKING [CREATE,READ,DELETE]`

**For AGS Shared Cloud:**
- Matchmaking (Create, Read, Delete)

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

Edit `.env` with your credentials:

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

# Service Configuration (Optional - defaults shown)
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true
BASE_PATH=/matchmaking

# Matchmaker Configuration (Optional - defaults shown)
MATCHMAKER__MATCHSIZE=2
MATCHMAKER__TICKINTERVALSECONDS=1
MATCHMAKER__REQUESTTIMEOUTSECONDS=60
```

> See [Configuration](#configuration) section below for detailed explanation of all parameters.

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

### Required Configuration

These credentials are **required** for the service to function. The service will fail to start if any are missing or invalid.

#### AccelByte Credentials

```bash
AB_BASE_URL=https://test.accelbyte.io
AB_CLIENT_ID=your-client-id
AB_CLIENT_SECRET=your-client-secret
AB_NAMESPACE=your-namespace
```

**Where to get these:**
- Login to AGS Admin Portal
- Navigate to **Admin** → **Namespace** → **Integration** → **OAuth Clients**
- Create or use existing confidential client with matchmaking permissions

#### Epic Online Services (EOS) Credentials

```bash
EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-eos-client-id
EOS_CLIENT_SECRET=your-eos-client-secret
```

**Where to get these:**
- Login to [Epic Games Developer Portal](https://dev.epicgames.com/)
- Navigate to your Product → **Product Settings**
- Copy Product ID, Sandbox ID, Deployment ID, Client ID, and Client Secret

---

### Optional Configuration

These parameters have sensible defaults and only need to be changed for specific use cases.

#### Matchmaker Settings

Configure matchmaking behavior via environment variables or `appsettings.json`:

```bash
MATCHMAKER__MATCHSIZE=2                    # Default: 2
MATCHMAKER__TICKINTERVALSECONDS=1          # Default: 1
MATCHMAKER__REQUESTTIMEOUTSECONDS=60       # Default: 60
```

**Parameters:**

| Parameter | Default | Description | When to Change |
|-----------|---------|-------------|----------------|
| `MatchSize` | `2` | Number of players per match | Change based on your game mode (e.g., 4 for squad, 10 for team deathmatch) |
| `TickIntervalSeconds` | `1` | How often the matcher runs (in seconds) | Increase to reduce CPU usage in low-traffic scenarios; decrease for faster matching in high-traffic scenarios |
| `RequestTimeoutSeconds` | `60` | How long requests stay in pool before expiring | Increase for games with longer expected wait times; decrease for fast-paced games where players expect quick matches |

**JSON Format:**
```json
{
  "MatchMaker": {
    "MatchSize": 2,
    "TickIntervalSeconds": 1,
    "RequestTimeoutSeconds": 60
  }
}
```

#### Session Provider Mode

**Default:** `create` mode (service creates new EOS sessions for each match)

```bash
SESSIONPROVIDER__MODE=create               # Default: create
```

**When to use Find mode:**
- Your game servers create EOS sessions and register them
- You want the matchmaker to find and claim existing sessions
- You need more control over session lifecycle

**Find Mode Configuration:**
```bash
SESSIONPROVIDER__MODE=find
SESSIONFINDER__BUCKETID=default                          # Default: default
SESSIONFINDER__MAXSEARCHRESULTS=10                       # Default: 10
SESSIONFINDER__CLAIMEDSESSIONEXPIRATIONSECONDS=300       # Default: 300
```

| Parameter | Default | Description | When to Change |
|-----------|---------|-------------|----------------|
| `BucketId` | `default` | EOS bucket to search for sessions | Use different buckets for different game modes or regions |
| `MaxSearchResults` | `10` | Maximum sessions to retrieve per search | Increase if you have many available sessions; decrease to reduce API calls |
| `ClaimedSessionExpirationSeconds` | `300` | How long to cache claimed sessions (5 minutes) | Increase for longer session setup times; decrease to allow faster re-claiming |

> See [Architecture Guide](architecture.md#session-provider-modes) for detailed information on both modes and when to use each.

#### Authorization Settings

**Default:** `true` (authorization enabled)

```bash
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true       # Default: true
```

**When to disable:**
- Local development and testing only
- ⚠️ **Never disable in production environments**

#### Service Path

**Default:** `/matchmaking`

```bash
BASE_PATH=/matchmaking                     # Default: /matchmaking
```

**When to change:**
- You need a different URL path for routing or organizational purposes
- Affects all endpoint URLs: `/{BASE_PATH}/v1/request`, `/{BASE_PATH}/apidocs/`

---

## Deployment Scenarios

### Single-Instance Deployment (Default)

**Use case:** Development, testing, small-scale production (< 1000 concurrent players)

**Configuration:** Use default settings with required credentials only.

**What's included:**
- In-memory match pool (thread-safe, single process)
- In-memory completed request store (lost on restart)
- In-memory claimed sessions cache (find mode only)
- Built-in logging to console

**Limitations:**
- No horizontal scaling (single instance only)
- Match pool and request history lost on restart
- No durability across deployments

**Setup:**
```bash
# .env file - only required credentials needed
AB_BASE_URL=https://test.accelbyte.io
AB_CLIENT_ID=your-client-id
AB_CLIENT_SECRET=your-client-secret
AB_NAMESPACE=your-namespace

EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-eos-client-id
EOS_CLIENT_SECRET=your-eos-client-secret

# All other settings use defaults
```

### Multi-Instance Deployment (Production Scale)

**Use case:** Production environments with high availability and horizontal scaling needs

**Configuration:** Requires custom implementations of infrastructure extension points.

**What to customize:**

1. **IMatchPool** - Use distributed storage (Redis, database)
   - Allows multiple instances to share the same match pool
   - Provides durability across restarts
   - See [Architecture Guide - Redis MatchPool Example](architecture.md#redis-matchpool-example)

2. **ICompletedRequestStore** - Use persistent storage (Redis, database)
   - Maintains request history across restarts
   - Enables multi-instance deployments
   - See [Architecture Guide - Database CompletedRequestStore Example](architecture.md#database-completedrequestore-example)

3. **IClaimedSessionsCache** (Find mode only) - Use distributed cache (Redis)
   - Prevents race conditions across multiple instances
   - Required for multi-instance find mode deployments
   - See [Architecture Guide - Redis ClaimedSessionsCache Example](architecture.md#redis-claimedsessionscache-example)

4. **IPlayerNotifier** - Use production notification mechanism
   - Replace logging with webhooks, push notifications, or message queues
   - See [Architecture Guide - Webhook PlayerNotifier Example](architecture.md#webhook-playernotifier-example)

**Setup:**
```bash
# Same required credentials as single-instance
# Plus any configuration needed for your custom implementations

# Example: Redis connection string for distributed storage
REDIS_CONNECTION_STRING=redis:6379

# Example: Database connection for persistent storage
DATABASE_CONNECTION_STRING=Server=db;Database=matchmaking;...
```

**Deployment considerations:**
- Load balancer required for multiple instances
- Shared storage (Redis/database) must be highly available
- Monitor distributed storage performance and capacity
- Consider regional deployments for global games

> See [Architecture Guide - Extension Points](architecture.md#extension-points) for complete implementation examples.

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

Edit `src/AccelByte.Extend.SimpleEOSMatchmaking.Server/appsettings.json`:

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
  }
}
```

> See [Configuration](#configuration) section for detailed explanation of all parameters and defaults.

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

### Prerequisites

1. **Download extend-helper-cli**:
   - Go to the [extend-helper-cli releases page](https://github.com/AccelByte/extend-helper-cli/releases)
   - Download the latest executable for your operating system
   - Add the executable to your PATH or note its location

   > ⚠️ We recommend to always use the latest version available.

2. **Create Extend App** in AGS Admin Portal:
   - Navigate to **Extend** → **Service Extension**
   - Click **Create New App**
   - Enter app name and description
   - Note the app name for deployment

### Deployment Steps

#### 1. Build Docker Image

```bash
docker build -t extend-eos-matchmaking:v0.0.1 .
```

#### 2. Login to AccelByte Registry

```bash
extend-helper-cli dockerlogin --namespace <your-namespace>
```

#### 3. Upload Image

```bash
extend-helper-cli image-upload \
  --namespace <your-namespace> \
  --app <your-app-name> \
  --image-tag v0.0.1
```

#### 4. Configure Secrets in Admin Portal

Navigate to your app in the Admin Portal and configure:

**Required Environment Variables:**
- `AB_CLIENT_ID` - Your OAuth client ID
- `AB_CLIENT_SECRET` - Your OAuth client secret (mark as secret)
- `EOS_PRODUCT_ID` - Your EOS product ID
- `EOS_SANDBOX_ID` - Your EOS sandbox ID
- `EOS_DEPLOYMENT_ID` - Your EOS deployment ID
- `EOS_CLIENT_ID` - Your EOS client ID
- `EOS_CLIENT_SECRET` - Your EOS client secret (mark as secret)

**Optional Configuration** (only if changing defaults):
- `MATCHMAKER__MATCHSIZE` - Match size (default: 2)
- `MATCHMAKER__TICKINTERVALSECONDS` - Tick interval (default: 1)
- `MATCHMAKER__REQUESTTIMEOUTSECONDS` - Timeout (default: 60)
- `SESSIONPROVIDER__MODE` - Session provider mode (default: create)

> See [Configuration](#configuration) section for complete list of optional parameters and when to change them.
> See [Deployment Scenarios](#deployment-scenarios) for single-instance vs multi-instance deployment guidance.

#### 5. Deploy Image

In the Admin Portal:
1. Go to your app's **Versions** tab
2. Select the uploaded image version
3. Click **Deploy**
4. Wait for deployment to complete
5. Note the service URL

#### 6. Verify Deployment

```bash
curl https://<your-app-url>/apidocs/
```

---

## Testing the Deployment

### Using Postman

1. **Import Collections**:
   - `demo/get-access-token.postman_collection.json`
   - `demo/matchmaking-service-demo.postman_collection.json`

2. **Configure Environment**:
   - `AB_BASE_URL` - Your AGS base URL
   - `AB_NAMESPACE` - Your namespace
   - `AB_CLIENT_ID` - Your OAuth client ID
   - `AB_CLIENT_SECRET` - Your OAuth client secret
   - `AB_USERNAME` - Test user username
   - `AB_PASSWORD` - Test user password
   - `EXTEND_APP_SERVICE_URL` - Your deployed service URL

3. **Run Tests**:
   - Get user access token
   - Submit match request
   - Check match status
   - Cancel request (optional)

### Using Swagger UI

1. Navigate to `https://<your-app-url>/apidocs/`
2. Click **Authorize**
3. Enter `Bearer <access_token>`
4. Test endpoints directly in the UI

For detailed testing scenarios, see the [Testing Guide](testing_guide.md).

---

## Troubleshooting

### Service Won't Start

**Check EOS Credentials:**
```bash
docker compose logs | grep "EOS"
```

Look for initialization errors. Common issues:
- Invalid Product ID, Sandbox ID, or Deployment ID
- Invalid Client ID or Client Secret
- Network connectivity to EOS services

**Check AccelByte Configuration:**
```bash
docker compose logs | grep "AccelByte"
```

Verify:
- Base URL is correct
- Client credentials are valid
- Namespace exists

### Authorization Errors

**Error: `Unauthenticated` or `PermissionDenied`**

1. Verify token is valid:
   ```bash
   curl -H "Authorization: Bearer <token>" \
     https://test.accelbyte.io/iam/v3/oauth/verify
   ```

2. Check client permissions in Admin Portal
3. Ensure `PLUGIN_GRPC_SERVER_AUTH_ENABLED=true`

### Session Creation Fails

**Error: `Failed to create EOS session`**

1. Check EOS SDK logs:
   ```bash
   docker compose logs | grep "EOS SDK"
   ```

2. Verify EOS credentials are correct
3. Check EOS service status
4. Ensure deployment is active in EOS portal

### Matches Not Being Created

**Requests stay PENDING:**

1. Check MatchMaker is running:
   ```bash
   docker compose logs | grep "MatchMaker"
   ```

2. Verify enough players in pool (need >= MatchSize)
3. Check tick interval configuration
4. Look for errors in matcher logs

### Port Conflicts

**Error: `port is already allocated`**

Change ports in `docker-compose.yaml`:
```yaml
ports:
  - "8001:8000"  # Change 8000 to 8001
  - "8081:8080"  # Change 8080 to 8081
```

---

## Next Steps

- **Test the service**: See [Testing Guide](testing_guide.md)
- **Understand the architecture**: See [Architecture Guide](architecture.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
- **Use Dev Containers**: See [Dev Container Guide](devcontainer.md)
