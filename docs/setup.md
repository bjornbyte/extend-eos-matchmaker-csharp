# Setup Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide provides complete instructions for setting up, configuring, and deploying the Simple EOS Matchmaking Service Extension.

## Prerequisites

### Development Tools

- **Bash** - Command-line shell (Git Bash on Windows, native on Linux/Mac)
- **Make** - Build automation tool
- **Docker** - Container platform (Docker Desktop recommended)
- **Docker Compose** - Multi-container orchestration
- **.NET 8 SDK** - For local development and testing
- **Postman** - API testing (optional, but recommended)
- **extend-helper-cli** - AccelByte deployment tool

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
# AccelByte Configuration
AB_BASE_URL=https://test.accelbyte.io
AB_CLIENT_ID=your-client-id
AB_CLIENT_SECRET=your-client-secret
AB_NAMESPACE=your-namespace

# Service Configuration
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true
BASE_PATH=/eos-matchmaking

# EOS Configuration
EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-eos-client-id
EOS_CLIENT_SECRET=your-eos-client-secret

# Matchmaker Configuration (Optional)
MATCHMAKER__MATCHSIZE=2
MATCHMAKER__TICKINTERVALSECONDS=1
MATCHMAKER__REQUESTTIMEOUTSECONDS=60
```

### 4. Build and Run with Docker

```bash
docker compose up --build
```

The service will be available at:
- **REST API**: `http://localhost:8000/eos-matchmaking`
- **Swagger UI**: `http://localhost:8000/eos-matchmaking/apidocs/`
- **Metrics**: `http://localhost:8080/metrics`

### 5. Verify Service is Running

```bash
curl http://localhost:8000/eos-matchmaking/apidocs/
```

You should see the Swagger UI HTML response.

---

## Configuration Options

### Matchmaker Settings

Configure matchmaking behavior via environment variables or `appsettings.json`:

```json
{
  "MatchMaker": {
    "MatchSize": 2,
    "TickIntervalSeconds": 1,
    "RequestTimeoutSeconds": 60
  }
}
```

**Environment Variable Format:**
```bash
MATCHMAKER__MATCHSIZE=4
MATCHMAKER__TICKINTERVALSECONDS=2
MATCHMAKER__REQUESTTIMEOUTSECONDS=120
```

**Parameters:**
- `MatchSize` - Number of players per match (default: 2)
- `TickIntervalSeconds` - How often the matcher runs (default: 1 second)
- `RequestTimeoutSeconds` - Request expiration time (default: 60 seconds)

### EOS SDK Settings

Configure EOS integration via environment variables:

```bash
EOS_PRODUCT_ID=your-product-id
EOS_SANDBOX_ID=your-sandbox-id
EOS_DEPLOYMENT_ID=your-deployment-id
EOS_CLIENT_ID=your-eos-client-id
EOS_CLIENT_SECRET=your-eos-client-secret
```

These are required for session creation. The service will fail to start if EOS credentials are missing or invalid.

### Authorization Settings

**Enable Authorization** (Production):
```bash
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true
```

**Disable Authorization** (Local Development Only):
```bash
PLUGIN_GRPC_SERVER_AUTH_ENABLED=false
```

⚠️ **Warning:** Never disable authorization in production environments.

### Service Path

Configure the base path for the service:

```bash
BASE_PATH=/eos-matchmaking
```

This affects all endpoint URLs:
- `/eos-matchmaking/matchmaking/v1/request`
- `/eos-matchmaking/apidocs/`

---

## Local Development Without Docker

### 1. Install .NET 8 SDK

Download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/8.0)

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

1. **Install extend-helper-cli**:
   ```bash
   npm install -g @accelbyte/extend-helper-cli
   ```

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

**Environment Variables:**
- `AB_CLIENT_ID` - Your OAuth client ID
- `AB_CLIENT_SECRET` - Your OAuth client secret (mark as secret)
- `EOS_PRODUCT_ID` - Your EOS product ID
- `EOS_SANDBOX_ID` - Your EOS sandbox ID
- `EOS_DEPLOYMENT_ID` - Your EOS deployment ID
- `EOS_CLIENT_ID` - Your EOS client ID
- `EOS_CLIENT_SECRET` - Your EOS client secret (mark as secret)

**Optional Configuration:**
- `MATCHMAKER__MATCHSIZE` - Match size (default: 2)
- `MATCHMAKER__TICKINTERVALSECONDS` - Tick interval (default: 1)
- `MATCHMAKER__REQUESTTIMEOUTSECONDS` - Timeout (default: 60)

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
