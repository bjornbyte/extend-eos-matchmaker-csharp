# Testing Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide provides comprehensive instructions for manual end-to-end testing of the Simple EOS Matchmaking Service using Swagger UI and Postman.

## Prerequisites

### 1. Service Setup

- **Service Running**: Ensure the service is running locally
  ```bash
  docker compose up --build
  ```
- **Service URL**: `http://localhost:8000`
- **Swagger UI**: `http://localhost:8000/matchmaking/apidocs/`

### 2. AccelByte Setup

You need the following AccelByte resources:

- **Client Credentials** (for authentication)
  - `client_id` - OAuth client ID
  - `client_secret` - OAuth client secret

- **Test User Account** (if using password grant)
  - `user_email` - Email of test user
  - `user_password` - Password of test user

- **Test Resources**
  - `namespace` - Your AccelByte namespace ID

### 3. EOS Setup

- EOS credentials configured in `.env` file
- Valid EOS Product, Sandbox, and Deployment

---

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
   AB_BASE_URL: https://test.accelbyte.io
   AB_CLIENT_ID: <YOUR_CLIENT_ID>
   AB_CLIENT_SECRET: <YOUR_CLIENT_SECRET>
   AB_USERNAME: <YOUR_TEST_USER_EMAIL>
   AB_PASSWORD: <YOUR_TEST_USER_PASSWORD>
   ```

---

## Test Flow

### Step 1: Authentication

**Request:** `Get User Access Token` or `Get Client Access Token`

- **Method:** POST
- **Endpoint:** `{{AB_BASE_URL}}/iam/v3/oauth/token`
- **Auth:** Basic Auth (AB_CLIENT_ID:AB_CLIENT_SECRET)
- **Body:**
  ```
  grant_type: password
  username: {{AB_USERNAME}}
  password: {{AB_PASSWORD}}
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

### Step 2: Submit Match Request

**Request:** `POST /matchmaking/v1/request`

- **Method:** POST
- **Endpoint:** `http://localhost:8000/matchmaking/v1/request`
- **Auth:** Bearer Token (access_token from Step 1)
- **Headers:** Use one of the following:
  - `Authorization: Bearer <access_token>`
  - `user-id: test-user-1` (for testing without token parsing)
- **Body:**
  ```json
  {
    "metadata": {
      "region": "us-west",
      "skill_level": "intermediate"
    }
  }
  ```

**Expected Response:**
```json
{
  "request_id": "550e8400-e29b-41d4-a716-446655440000"
}
```

**Notes:**
- `metadata` is optional and can contain any key-value pairs
- Each user can only have one pending request at a time
- Save the `request_id` for status queries

---

### Step 3: Check Match Status

**Request:** `GET /matchmaking/v1/request/{request_id}`

- **Method:** GET
- **Endpoint:** `http://localhost:8000/matchmaking/v1/request/{request_id}`
- **Auth:** Bearer Token
- **Headers:**
  - `Authorization: Bearer <access_token>`

**Expected Response (Pending):**
```json
{
  "request_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "PENDING",
  "session_id": "",
  "matched_user_ids": [],
  "matched_request_ids": []
}
```

**Expected Response (Matched):**
```json
{
  "request_id": "550e8400-e29b-41d4-a716-446655440000",
  "status": "MATCHED",
  "session_id": "a1b2c3d4-e5f6-7890-abcd-ef1234567890",
  "matched_user_ids": ["test-user-1", "test-user-2"],
  "matched_request_ids": ["550e8400-...", "660e8400-..."]
}
```

**Status Values:**
- `PENDING` - Waiting for match
- `MATCHED` - Successfully matched
- `EXPIRED` - Timed out (default: 60 seconds)
- `CANCELLED` - User cancelled

---

### Step 4: Cancel Match Request (Optional)

**Request:** `DELETE /matchmaking/v1/request/{request_id}`

- **Method:** DELETE
- **Endpoint:** `http://localhost:8000/matchmaking/v1/request/{request_id}`
- **Auth:** Bearer Token
- **Headers:**
  - `Authorization: Bearer <access_token>`

**Expected Response:**
```json
{
  "success": true
}
```

**Notes:**
- Only PENDING requests can be cancelled
- Returns error if request is already MATCHED, EXPIRED, or CANCELLED

---

## Testing with Swagger UI

### Step 1: Open Swagger UI

Navigate to `http://localhost:8000/matchmaking/apidocs/`

### Step 2: Authorize

1. Click the **Authorize** button
2. Enter: `Bearer <your_access_token>`
3. Click **Authorize**

### Step 3: Test Submit Match Request

1. Expand `POST /matchmaking/v1/request`
2. Click **Try it out**
3. Fill in request body:
   ```json
   {
     "metadata": {
       "region": "us-west"
     }
   }
   ```
4. Click **Execute**
5. Review the response and save the `request_id`

### Step 4: Test Get Match Status

1. Expand `GET /matchmaking/v1/request/{request_id}`
2. Click **Try it out**
3. Fill in `request_id` from previous step
4. Click **Execute**
5. Review the status

### Step 5: Test Cancel Match Request

1. Expand `DELETE /matchmaking/v1/request/{request_id}`
2. Click **Try it out**
3. Fill in `request_id`
4. Click **Execute**
5. Verify cancellation success

---

## Complete Matchmaking Scenario

### Scenario: Two Players Match

1. **Player 1 submits request:**
   ```bash
   POST /matchmaking/v1/request
   Headers: user-id: player-1
   Response: { "request_id": "req-1" }
   ```

2. **Player 2 submits request:**
   ```bash
   POST /matchmaking/v1/request
   Headers: user-id: player-2
   Response: { "request_id": "req-2" }
   ```

3. **Wait 1-2 seconds** (for MatchMaker tick)

4. **Player 1 checks status:**
   ```bash
   GET /matchmaking/v1/request/req-1
   Response: {
     "status": "MATCHED",
     "session_id": "session-123",
     "matched_user_ids": ["player-1", "player-2"]
   }
   ```

5. **Player 2 checks status:**
   ```bash
   GET /matchmaking/v1/request/req-2
   Response: {
     "status": "MATCHED",
     "session_id": "session-123",
     "matched_user_ids": ["player-1", "player-2"]
   }
   ```

## Troubleshooting

### Service Not Running

**Symptom:**
```
Error: connect ECONNREFUSED 127.0.0.1:8000
```

**Solution:** Start the service with `docker compose up --build`

### Authentication Failed

**Symptom:**
```
401 Unauthorized
```

**Solution:**
- Check client_id and client_secret are correct
- Ensure OAuth client has required permissions
- Verify user credentials (if using password grant)
- Check token hasn't expired

### Match Not Created

**Symptom:** Requests stay PENDING indefinitely

**Solution:**
- Ensure at least MatchSize (default: 2) requests are submitted
- Check MatchMaker is running (view service logs)
- Verify EOS credentials are valid
- Check service logs for errors

### Permission Denied

**Symptom:**
```
403 Forbidden
```

**Solution:**
- Verify test user has `NAMESPACE:{namespace}:MATCHMAKING [CREATE,READ,DELETE]` permissions
- Check namespace matches your namespace
- Regenerate access token after adding permissions

---

## Next Steps

After successful testing:

1. **Integration Testing**: Test with real game client
2. **Load Testing**: Test with multiple concurrent requests
3. **Monitoring**: Set up Grafana dashboards
4. **Deployment**: Deploy to AGS using [Setup Guide](setup.md)

---

## Support

For issues or questions:
- Check service logs: `docker compose logs -f`
- Review Swagger UI: `http://localhost:8000/matchmaking/apidocs/`
- Check documentation: [README](../README.md)
