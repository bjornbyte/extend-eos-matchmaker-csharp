# Setup Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide covers everything you need to set up, configure, and deploy the Guild Progress Service Extension.

## Prerequisites

### 1. Development Tools

You'll need the following tools installed on Windows 11 WSL2, Linux Ubuntu 22.04, or macOS 14+:

#### a. Bash

- On Windows WSL2 or Linux Ubuntu:

  ```
  bash --version

  GNU bash, version 5.1.16(1)-release (x86_64-pc-linux-gnu)
  ...
  ```

- On macOS:

  ```
  bash --version

  GNU bash, version 3.2.57(1)-release (arm64-apple-darwin23)
  ...
  ```

#### b. Make

- On Windows WSL2 or Linux Ubuntu:

  To install from the Ubuntu repository, run `sudo apt update && sudo apt install make`.

  ```
  make --version

  GNU Make 4.3
  ...
  ```

- On macOS:

  ```
  make --version

  GNU Make 3.81
  ...
  ```

#### c. Docker (Docker Desktop 4.30+/Docker Engine v23.0+)

- On Linux Ubuntu:

  1. To install from the Ubuntu repository, run `sudo apt update && sudo apt install docker.io docker-buildx docker-compose-v2`.
  2. Add your user to the `docker` group: `sudo usermod -aG docker $USER`.
  3. Log out and log back in to allow the changes to take effect.

- On Windows or macOS:

  Follow Docker's documentation on installing the Docker Desktop on [Windows](https://docs.docker.com/desktop/install/windows-install/) or [macOS](https://docs.docker.com/desktop/install/mac-install/).

  ```
  docker version

  ...
  Server: Docker Desktop
     Engine:
     Version:          24.0.5
  ...
  ```

#### d. .NET 8 SDK

- Follow [.NET's installation guide](https://dotnet.microsoft.com/download/dotnet/8.0).

  ```
  dotnet --version

  8.0.119
  ```

#### e. Postman

- Use binary available [here](https://www.postman.com/downloads/)

#### f. extend-helper-cli

- Use the available binary from [extend-helper-cli](https://github.com/AccelByte/extend-helper-cli/releases).

> :exclamation: In macOS, you may use [Homebrew](https://brew.sh/) to easily install some of the tools above.

---

### 2. AccelByte Gaming Services (AGS) Setup

#### a. Base URL

- Sample URL for AGS Shared Cloud customers: `https://spaceshooter.prod.gamingservices.accelbyte.io`
- Sample URL for AGS Private Cloud customers:  `https://dev.accelbyte.io`

#### b. Create a Game Namespace

[Create a Game Namespace](https://docs.accelbyte.io/gaming-services/services/access/reference/namespaces/manage-your-namespaces/) if you don't have one yet. Keep the `Namespace ID`. Make sure this namespace is in active status.

#### c. Create OAuth Client for the Extend Service

[Create an OAuth Client for the Extend Service](https://docs.accelbyte.io/gaming-services/services/access/authorization/manage-access-control-for-applications/#create-an-iam-client) with confidential client type. This client is used by the service itself to validate tokens and call AGS APIs. Keep the `Client ID` and `Client Secret`.

**Required Permissions** (for both Private and Shared Cloud):

- For AGS Private Cloud customers:
  - `ADMIN:ROLE [READ]` to validate access token and permissions
  - `ADMIN:NAMESPACE:{namespace}:NAMESPACE [READ]` to validate access namespace
  - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE,READ,UPDATE,DELETE]` to manage guild progress records

- For AGS Shared Cloud customers:
  - IAM -> Roles (Read)
  - Basic -> Namespace (Read)
  - Cloud Save -> Game Records (Create, Read, Update, Delete)

---

### 3. OAuth Client for Game Client/Server

Your game client or game server needs an OAuth client to call this service's endpoints.

#### Create OAuth Client

- Create an OAuth client with `password` grant type for user authentication OR `client_credentials` for server-to-server
- Add the following permissions:
  - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE]` - for creating/updating guild progress
  - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [READ]` - for reading guild progress

For AGS Shared Cloud:
- Cloud Save -> Game Records (Create, Read, Update, Delete)

---

## Environment Configuration

To be able to run this app, you will need to follow these setup steps.

### 1. Create Environment File

Create a docker compose `.env` file by copying the content of [.env.template](../.env.template) file.

> :warning: **The host OS environment variables have higher precedence compared to `.env` file variables**:
> If the variables in `.env` file do not seem to take effect properly, check if there are host OS environment variables with the same name. 
> See documentation about [docker compose environment variables precedence](https://docs.docker.com/compose/how-tos/environment-variables/envvars-precedence/) for more details.

### 2. Configure Environment Variables

Fill in the required environment variables in `.env` file as shown below.

```bash
# AccelByte Configuration
AB_BASE_URL=https://test.accelbyte.io         # Your AGS environment Base URL
AB_CLIENT_ID=xxxxxxxxxx                       # Service OAuth Client ID
AB_CLIENT_SECRET=xxxxxxxxxx                   # Service OAuth Client Secret
AB_NAMESPACE=xxxxxxxxxx                       # Namespace ID
PLUGIN_GRPC_SERVER_AUTH_ENABLED=true          # Enable auth validation (set false for dev only)
BASE_PATH=/guild                              # Base path for the service endpoints

# Optional Configuration
LOG_LEVEL=info                                # Log level: debug, info, warn, error
OTEL_EXPORTER_ZIPKIN_ENDPOINT=                # OpenTelemetry Zipkin endpoint (optional)
```

> :exclamation: **In this app, PLUGIN_GRPC_SERVER_AUTH_ENABLED is `true` by default**: If it is set to `false`, the endpoint `permission.action` and `permission.resource`  validation will be disabled and the endpoint can be accessed without a valid access token. This option is provided for development purpose only.

> :warning: **BASE_PATH must start with `/`**: The service uses this as the URL prefix for all endpoints. For example, `BASE_PATH=/guild` results in endpoints like `/guild/v1/admin/namespace/{namespace}/progress`.

---

## Building

To build this app, use the following command.

```shell
make build
```

The build output will be available in `.output` directory.

### What Gets Built

- .NET binary compiled from C# source
- Protocol buffer files generated from `Protos/*.proto`
- gRPC Gateway files (Go)
- Swagger/OpenAPI specification

---

## Running

To (build and) run this app in a container, use the following command.

```shell
docker compose up --build
```

### Service Endpoints

Once running, the service will be available at:

- **gRPC Server**: `:6565`
- **HTTP Gateway**: `:8000`
- **Prometheus Metrics**: `:8080/metrics`
- **Swagger UI**: `http://localhost:8000{BASE_PATH}/apidocs/`
- **Swagger JSON**: `http://localhost:8000{BASE_PATH}/apidocs/api.json`

For example, with `BASE_PATH=/guild`:
- Swagger UI: `http://localhost:8000/guild/apidocs/`
- Create/Update endpoint: `http://localhost:8000/guild/v1/admin/namespace/{namespace}/progress`

---

## Deployment

After completing testing, the next step is to deploy your app to `AccelByte Gaming Services`.

### 1. Create an Extend Service Extension App

If you do not already have one, create a new [Extend Service Extension App](https://docs.accelbyte.io/gaming-services/services/extend/service-extension/getting-started-service-extension/#create-the-extend-app).

On the **App Detail** page, take note of the following values.
- `Namespace`
- `App Name`

Under the **Environment Configuration** section, set the required secrets and/or variables.

**Secrets:**
- `AB_CLIENT_ID` - AccelByte service OAuth client ID
- `AB_CLIENT_SECRET` - AccelByte service OAuth client secret

**Variables:**
- `BASE_PATH` - Set to `/guild` (or your preferred path)
- `PLUGIN_GRPC_SERVER_AUTH_ENABLED` - Set to `true`

### 2. Build and Push the Container Image

Use [extend-helper-cli](https://github.com/AccelByte/extend-helper-cli) to build and upload the container image.

```
extend-helper-cli image-upload --login --namespace <namespace> --app <app-name> --image-tag v0.0.1
```

> :warning: Run this command from your project directory. If you are in a different directory, add the `--work-dir <project-dir>` option to specify the correct path.

### 3. Deploy the Image

On the **App Detail** page:
- Click **Image Version History**
- Select the image you just pushed
- Click **Deploy Image**

---

## Next Steps

- **Test the service**: See [Testing Guide](testing_guide.md)
- **Understand the architecture**: See [Architecture Guide](architecture.md)
- **Monitor and troubleshoot**: See [Operations Guide](operations.md)
