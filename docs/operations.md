# Operations Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide covers monitoring, troubleshooting, and deployment considerations for production operations.

## Testing

For comprehensive testing instructions, see the **[Testing Guide](testing_guide.md)**.

Quick reference:
- **Swagger UI**: `http://localhost:8000/matchmaking/apidocs/`
- **Unit Tests**: `dotnet test src/extend-service-extension-server.sln`
- **Get Access Token**: Use `demo/get-access-token.postman_collection.json`

---

## Observability

### Logging

Configure log level: `LOG_LEVEL=Information` (Debug, Information, Warning, Error)

View logs: `docker compose logs -f`

### Metrics (Prometheus)

Endpoint: `http://localhost:8080/metrics`

Key metrics:
- `grpc_server_handling_seconds` - Request latency (alert if p95 > 1s)
- `matchmaking_pool_size` - Pending requests (alert if > 1000)
- `matchmaking_expired_requests_total` - Expiration rate (alert if > 10%)

### Tracing (Zipkin)

Configure: `OTEL_EXPORTER_ZIPKIN_ENDPOINT=http://zipkin:9411/api/v2/spans`

UI: `http://localhost:9411`

### Full Stack

Use [AccelByte gRPC plugin dependencies](https://github.com/AccelByte/grpc-plugin-dependencies) for Prometheus, Grafana, Loki, and Zipkin.

---

## Error Codes

| gRPC Code | HTTP | Meaning | Retry? |
|-----------|------|---------|--------|
| 5 | 404 | Not Found | ❌ Never |
| 9 | 412 | Failed Precondition | ❌ Never |
| 13 | 500 | Internal Error | 🔄 Yes |
| 16 | 401 | Unauthenticated | 🔑 Refresh token |

Common errors:
- **Match request not found** - Request ID doesn't exist or was already processed
- **User already has pending request** - Cancel existing request first
- **Cannot cancel non-pending request** - Only PENDING requests can be cancelled

---

## Multi-Instance Deployment

**When needed:** High availability, > 1000 concurrent players, horizontal scaling

**Required:** Implement distributed versions of IMatchPool, ICompletedRequestStore, and IClaimedSessionsCache (find mode only)

**See:** [Implementation Examples](examples.md) for Key-Value Store and database implementations

---

## Troubleshooting

### Testing Issues

**401 Unauthorized:**
- Check client_id and client_secret are correct
- Ensure OAuth client has required permissions
- Verify user credentials (if using password grant)
- Check token hasn't expired

**403 Forbidden:**
- Verify test user has `NAMESPACE:{namespace}:MATCHMAKING [CREATE,READ,DELETE]` permissions
- Check namespace matches your namespace
- Regenerate access token after adding permissions

**Connection Refused:**
- Start the service: `docker compose up --build`
- Verify service is listening on port 8000

**Requests Not Matching:**
- Ensure at least MatchSize (default: 2) requests are submitted
- Check MatchMaker is running (logs show tick every second)
- Verify EOS credentials are valid in `.env` file

### Operational Issues

**Service Won't Start:** Check `.env` file, verify EOS credentials, review logs: `docker compose logs -f`

**EOS Session Creation Fails:** Verify EOS Product/Sandbox/Deployment exist, check credentials

**High Latency:** Check Zipkin traces for slow spans

**High Error Rate:** Check logs and Zipkin traces, monitor EOS failure rate

---

## Next Steps

- **Testing**: See [Testing Guide](testing_guide.md)
- **Architecture**: See [Architecture Guide](architecture.md)
- **AccelByte Docs**: https://docs.accelbyte.io/gaming-services/services/extend/service-extension/
