# Guild Progress Service - Testing Guide

This guide provides instructions for manual end-to-end testing of the Guild Progress Service Extension using Swagger UI and Postman.

## Prerequisites

### 1. Service Setup
- **Service Running**: Ensure the service is running locally
  ```bash
  docker compose up --build
  ```
- **Service URL**: `http://localhost:8000`
- **Swagger UI**: `http://localhost:8000/guild/apidocs/`

### 2. AccelByte Setup
You need the following AccelByte resources:

- **Client Credentials** (for authentication)
  - `client_id` - OAuth client ID with password grant or client credentials
  - `client_secret` - OAuth client secret
  - Required permissions:
    - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE]` - for creating/updating guild progress
    - `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [READ]` - for reading guild progress

- **Test User Account** (if using password grant)
  - `user_email` - Email of test user
  - `user_password` - Password of test user

- **Test Resources**
  - `namespace` - Your AccelByte namespace ID

## Postman Setup

### Import Collection

1. **Import Collection**
   - Open Postman
   - Click **Import**
   - Select `demo/get-access-token.postman_collection.json`

2. **Configure Environment Variables**
   - Create a new environment in Postman
   - Add the following variables:

   ```
   AB_BASE_URL: https://test.accelbyte.io (or your AGS URL)
   AB_CLIENT_ID: <YOUR_CLIENT_ID>
   AB_CLIENT_SECRET: <YOUR_CLIENT_SECRET>
   AB_USERNAME: <YOUR_TEST_USER_EMAIL>
   AB_PASSWORD: <YOUR_TEST_USER_PASSWORD>
   ```

## Test Flow

### Step 1: Authentication

**Request:** `Get User Access Token` or `Get Client Access Token`

- **Method:** POST
- **Endpoint:** `{{AB_BASE_URL}}/iam/v3/oauth/token`
- **Auth:** Basic Auth (AB_CLIENT_ID:AB_CLIENT_SECRET)
- **Body:**
  ```
  grant_type: password (for user token)
  username: {{AB_USERNAME}}
  password: {{AB_PASSWORD}}
  ```
  OR
  ```
  grant_type: client_credentials (for client token)
  ```

**Expected Response:**
```json
{
  "access_token": "eyJhbGciOiJSUzI1NiIsInR5cCI...",
  "user_id": "a1b2c3d4e5f6...",
  "namespace": "your-namespace"
}
```

**Save the access_token** for use in subsequent requests.

---

### Step 2: Create or Update Guild Progress

**Request:** `POST /v1/admin/namespace/{namespace}/progress`

- **Method:** POST
- **Endpoint:** `http://localhost:8000/guild/v1/admin/namespace/{namespace}/progress`
- **Auth:** Bearer Token (access_token from Step 1)
- **Body:**
  ```json
  {
    "guild_progress": {
      "guild_id": "test-guild-123",
      "namespace": "your-namespace",
      "objectives": {
        "boss_kills": 10,
        "quests_completed": 25,
        "members_recruited": 5
      }
    }
  }
  ```

**Expected Response:**
```json
{
  "guild_progress": {
    "guild_id": "test-guild-123",
    "namespace": "your-namespace",
    "objectives": {
      "boss_kills": 10,
      "quests_completed": 25,
      "members_recruited": 5
    }
  }
}
```

**Notes:**
- If `guild_id` is empty, a new GUID will be generated automatically
- The `objectives` map can contain any key-value pairs you need

---

### Step 3: Get Guild Progress

**Request:** `GET /v1/admin/namespace/{namespace}/progress/{guild_id}`

- **Method:** GET
- **Endpoint:** `http://localhost:8000/guild/v1/admin/namespace/{namespace}/progress/test-guild-123`
- **Auth:** Bearer Token (access_token from Step 1)

**Expected Response:**
```json
{
  "guild_progress": {
    "guild_id": "test-guild-123",
    "namespace": "your-namespace",
    "objectives": {
      "boss_kills": 10,
      "quests_completed": 25,
      "members_recruited": 5
    }
  }
}
```

**Possible Errors:**
- `404 Not Found`: Guild progress doesn't exist
- `403 Permission Denied`: Token doesn't have required permissions
- `401 Unauthorized`: Invalid or expired token

---

## Testing with Swagger UI

### Step 1: Open Swagger UI

Navigate to `http://localhost:8000/guild/apidocs/`

### Step 2: Authorize

1. Click the **Authorize** button
2. Enter: `Bearer <your_access_token>`
3. Click **Authorize**

### Step 3: Test Create/Update Endpoint

1. Expand `POST /v1/admin/namespace/{namespace}/progress`
2. Click **Try it out**
3. Fill in:
   - `namespace`: Your namespace ID
   - Request body with guild progress data
4. Click **Execute**
5. Review the response

### Step 4: Test Get Endpoint

1. Expand `GET /v1/admin/namespace/{namespace}/progress/{guild_id}`
2. Click **Try it out**
3. Fill in:
   - `namespace`: Your namespace ID
   - `guild_id`: The guild ID from previous step
4. Click **Execute**
5. Review the response

---

## Error Scenario Testing

### Test 1: Invalid Namespace
Try accessing with a non-existent namespace.
- Expected: `400 Bad Request` or `403 Permission Denied`

### Test 2: Guild Not Found
Try getting a guild that doesn't exist.
- Expected: `404 Not Found`

### Test 3: Missing Authorization
Try accessing without Bearer token.
- Expected: `401 Unauthorized`

### Test 4: Insufficient Permissions
Try accessing with a token that doesn't have CLOUDSAVE:RECORD permissions.
- Expected: `403 Permission Denied`

---

## Sample Test Data

### Sample Guild Progress Data

```json
{
  "guild_progress": {
    "guild_id": "guild-alpha",
    "namespace": "mygame",
    "objectives": {
      "total_xp": 50000,
      "level": 15,
      "dungeons_cleared": 42,
      "pvp_wins": 18,
      "guild_bank_gold": 100000
    }
  }
}
```

### Sample Update (Increment Progress)

```json
{
  "guild_progress": {
    "guild_id": "guild-alpha",
    "namespace": "mygame",
    "objectives": {
      "total_xp": 55000,
      "level": 16,
      "dungeons_cleared": 45,
      "pvp_wins": 20,
      "guild_bank_gold": 125000
    }
  }
}
```

---

## API Response Reference

### Success Response Structure

**Guild Progress Response:**
```json
{
  "guild_progress": {
    "guild_id": "string",
    "namespace": "string",
    "objectives": {
      "key1": 0,
      "key2": 0
    }
  }
}
```

### Error Response Structure

**gRPC Error Response:**
```json
{
  "code": 5,
  "message": "Guild progress not found",
  "details": []
}
```

**Common Error Codes:**
- `3` (InvalidArgument): Invalid input data
- `5` (NotFound): Guild progress not found
- `7` (PermissionDenied): Insufficient permissions
- `13` (Internal): Server error
- `16` (Unauthenticated): Invalid or missing token

---

## Troubleshooting

### Service Not Running
```
Error: connect ECONNREFUSED 127.0.0.1:8000
```
**Solution:** Start the service with `docker compose up --build`

---

### Authentication Failed
```
401 Unauthorized
```
**Solution:** 
- Check client_id and client_secret are correct
- Ensure OAuth client has password grant or client_credentials enabled
- Verify user credentials (if using password grant)

---

### Guild Not Found
```
404 Not Found
```
**Solution:**
- Guild progress must be created before it can be retrieved
- Use the create/update endpoint first
- Verify the guild_id is correct

---

### Permission Denied
```
403 Forbidden
```
**Solution:**
- Verify OAuth client has `ADMIN:NAMESPACE:{namespace}:CLOUDSAVE:RECORD [CREATE,READ]` permissions
- Check namespace matches your namespace
- Use correct client_id/secret

---

## Next Steps

After successful testing:

1. **Integration Testing**: Test with real game client
2. **Load Testing**: Test with multiple concurrent requests
3. **Data Validation**: Test with various objective data structures
4. **Error Handling**: Test edge cases and error scenarios

---

## Support

For issues or questions:
- Check service logs: `docker compose logs -f`
- Review Swagger UI: `http://localhost:8000/guild/apidocs/`
- Check documentation: [README](../README.md)
