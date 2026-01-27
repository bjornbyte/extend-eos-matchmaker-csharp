# Testing Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide provides step-by-step instructions for testing the matchmaking service using Swagger UI.

## Prerequisites

**Service:** `docker compose up --build`

**Access Token:** Use `demo/get-access-token.postman_collection.json` with Postman

Environment variables: `AB_BASE_URL`, `AB_CLIENT_ID`, `AB_CLIENT_SECRET`, `AB_USERNAME`, `AB_PASSWORD`

---

## Testing with Swagger UI

### 1. Get Access Token

Run the Postman collection to obtain an access token.

### 2. Open Swagger UI

Navigate to `http://localhost:8000/matchmaking/apidocs/`

### 3. Authorize

Click **Authorize**, enter `Bearer <your_access_token>`, click **Authorize**

### 4. Submit Match Request

**Endpoint:** `POST /matchmaking/v1/request`

**Body:**
```json
{
  "metadata": {
    "region": "us-west"
  }
}
```

**Response:** `{ "request_id": "550e8400-..." }`

### 5. Check Match Status

**Endpoint:** `GET /matchmaking/v1/request/{request_id}`

**Response (Pending):** `{ "status": "PENDING", ... }`

**Response (Matched):** `{ "status": "MATCHED", "session_id": "...", "matched_user_ids": [...] }`

**Status Values:** PENDING, MATCHED, EXPIRED, CANCELLED

### 6. Cancel Match Request (Optional)

**Endpoint:** `DELETE /matchmaking/v1/request/{request_id}`

**Note:** Only PENDING requests can be cancelled

---

## Complete Test Scenario

1. **Player 1 submits request** - Use Swagger UI, save `request_id_1`
2. **Player 2 submits request** - Use Swagger UI, save `request_id_2`
3. **Wait 1-2 seconds** (for MatchMaker tick)
4. **Both players check status** - Both should show MATCHED with same `session_id`

---

## Testing with cURL

### Submit Request
```bash
curl -X POST http://localhost:8000/matchmaking/v1/request \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"metadata": {"region": "us-west"}}'
```

### Check Status
```bash
curl http://localhost:8000/matchmaking/v1/request/$REQUEST_ID \
  -H "Authorization: Bearer $TOKEN"
```

### Cancel Request
```bash
curl -X DELETE http://localhost:8000/matchmaking/v1/request/$REQUEST_ID \
  -H "Authorization: Bearer $TOKEN"
```

---

## Unit Tests

Run all tests: `dotnet test src/extend-service-extension-server.sln`

---

## Troubleshooting

For troubleshooting issues during testing, see the **[Operations Guide](operations.md#troubleshooting)**.

Common issues:
- **401 Unauthorized**: Check token validity and OAuth client permissions
- **403 Forbidden**: Verify user has MATCHMAKING permissions for the namespace
- **Connection refused**: Ensure service is running with `docker compose up --build`
- **Requests not matching**: Verify at least MatchSize (default: 2) requests are submitted

---

## Next Steps

- **Monitoring**: Set up observability - see [Operations Guide](operations.md#observability)
- **Deployment**: Deploy to production - see [Setup Guide](setup.md)
- **Customization**: Extend the service - see [Architecture Guide](architecture.md#extension-points)
