# Operations Guide

**📚 Documentation:** [README](../README.md) | [Setup](setup.md) | [Architecture](architecture.md) | [Operations](operations.md) | [Testing](testing_guide.md)

---

This guide covers monitoring, error handling, troubleshooting, and deployment considerations for the Simple EOS Matchmaking Service.

## Testing

For comprehensive testing instructions, see the **[Testing Guide](testing_guide.md)**.

Quick reference:
- **Swagger UI**: `http://localhost:8000/matchmaking/apidocs/`
- **Unit Tests**: `dotnet test src/extend-service-extension-server.sln`
- **Get Access Token**: Use `demo/get-access-token.postman_collection.json`

---

## Observability

The service includes comprehensive built-in observability features for monitoring, debugging, and troubleshooting in both development and production environments.

### Observability Overview

The matchmaking service provides three pillars of observability:

- **Metrics**: Prometheus metrics for monitoring service health and performance
- **Tracing**: OpenTelemetry distributed tracing for debugging request flows
- **Logging**: Structured JSON logs for operational insights

### Basic Logging (Always Enabled)

The service uses structured logging with Microsoft.Extensions.Logging, which is enabled by default with no additional setup required.

#### Log Levels

Configure the log level via environment variable:

```bash
LOG_LEVEL=Information  # Options: Debug, Information, Warning, Error
```

**Log Levels Explained:**
- `Debug` - Verbose logging for development (includes request details, pool state)
- `Information` - Normal operations (match creation, request submission, expiration)
- `Warning` - Recoverable issues (notification failures, session creation retries)
- `Error` - Failures requiring attention (EOS errors, authorization failures)

#### Key Log Events

The service logs these important events:

**Match Request Lifecycle:**
- Request submission with user ID and metadata
- Request cancellation
- Request expiration (with count)
- Match creation with match ID and player count

**Session Management:**
- EOS session creation success/failure
- Session finder results (find mode)
- Session owner notification (find mode)

**Background Operations:**
- MatchMaker tick start/end
- Pool size at each tick
- Expired request cleanup

#### Viewing Logs

**Docker Compose:**
```bash
# View live logs
docker compose logs -f

# View logs for specific service
docker compose logs -f matchmaking-service

# View last 100 lines
docker compose logs --tail=100
```

#### Log Format

Logs are output in JSON format for easy parsing:

```json
{
  "timestamp": "2024-01-15T10:30:45.123Z",
  "level": "Information",
  "message": "Match created",
  "matchId": "abc123",
  "playerCount": 4,
  "sessionId": "session-xyz"
}
```

---

### Advanced Observability Setup

For production deployments, set up metrics and tracing to gain deeper insights into service behavior.

#### Metrics with Prometheus

**What You Get:**
- Request counts per endpoint
- Request duration histograms (p50, p95, p99)
- Error rates and status codes
- Active request counts
- Match pool size gauge
- Match creation rate
- EOS session creation success/failure rates

**Step 1: Access Metrics Endpoint**

The service exposes Prometheus metrics at `:8080/metrics`:

```bash
curl http://localhost:8080/metrics
```

**Step 2: Set Up Prometheus Server**

Create `prometheus.yml`:

```yaml
global:
  scrape_interval: 15s
  evaluation_interval: 15s

scrape_configs:
  - job_name: 'matchmaking-service'
    static_configs:
      - targets: ['matchmaking-service:8080']
    metrics_path: '/metrics'
```

**Step 3: Run Prometheus**

**Docker Compose:**

Add to your `docker-compose.yaml`:

```yaml
services:
  prometheus:
    image: prom/prometheus:latest
    ports:
      - "9090:9090"
    volumes:
      - ./prometheus.yml:/etc/prometheus/prometheus.yml
    command:
      - '--config.file=/etc/prometheus/prometheus.yml'
```

**Step 4: Access Prometheus UI**

Open http://localhost:9090 and query metrics:

**Useful Queries:**
```promql
# Request rate per endpoint
rate(grpc_server_handled_total[5m])

# Request duration p95
histogram_quantile(0.95, rate(grpc_server_handling_seconds_bucket[5m]))

# Match pool size
matchmaking_pool_size

# Match creation rate
rate(matchmaking_matches_created_total[5m])

# Error rate
rate(grpc_server_handled_total{grpc_code!="OK"}[5m])
```

**Step 5: Set Up Grafana (Optional)**

Visualize metrics with Grafana:

```yaml
services:
  grafana:
    image: grafana/grafana:latest
    ports:
      - "3000:3000"
    environment:
      - GF_SECURITY_ADMIN_PASSWORD=admin
    volumes:
      - grafana-storage:/var/lib/grafana

volumes:
  grafana-storage:
```

1. Open http://localhost:3000 (admin/admin)
2. Add Prometheus data source: http://prometheus:9090
3. Import dashboard or create custom panels

**Key Metrics to Monitor:**

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `grpc_server_handled_total` | Total requests | - |
| `grpc_server_handling_seconds` | Request latency | p95 > 1s |
| `matchmaking_pool_size` | Pending requests | > 1000 |
| `matchmaking_matches_created_total` | Matches created | - |
| `matchmaking_expired_requests_total` | Expired requests | Rate > 10% |
| `eos_session_creation_failures_total` | EOS failures | Rate > 5% |

#### Distributed Tracing with Zipkin

**What You Get:**
- End-to-end request traces through gateway → gRPC → services
- EOS SDK call timing
- Latency breakdown by component
- Error traces with full context

**Step 1: Run Zipkin Server**

**Docker Compose:**

Add to your `docker-compose.yaml`:

```yaml
services:
  zipkin:
    image: openzipkin/zipkin:latest
    ports:
      - "9411:9411"
```

**Step 2: Configure Service**

Set environment variable to export traces to Zipkin:

```bash
OTEL_EXPORTER_ZIPKIN_ENDPOINT=http://zipkin:9411/api/v2/spans
OTEL_SERVICE_NAME=matchmaking-service
```

**Docker Compose Example:**

```yaml
services:
  matchmaking-service:
    environment:
      - OTEL_EXPORTER_ZIPKIN_ENDPOINT=http://zipkin:9411/api/v2/spans
      - OTEL_SERVICE_NAME=matchmaking-service
```

**Step 3: Access Zipkin UI**

Open http://localhost:9411 and search for traces.

**Step 4: Analyze Traces**

**Trace Components:**
- `SubmitMatchRequest` - Request submission span
- `MatchPool.Add` - Pool storage span
- `MatchMaker.TryMatchAsync` - Matching algorithm span
- `SessionCreator.CreateSession` - EOS session creation span
- `EOS.UpdateSession` - EOS SDK call span
- `Notifier.NotifyMatch` - Notification span

**Useful Filters:**
- Service name: `matchmaking-service`
- Min duration: `> 1s` (find slow requests)
- Tags: `error=true` (find failed requests)
- Operation: `SubmitMatchRequest` (specific endpoint)

**Debugging with Traces:**
1. Find slow requests by filtering duration > 1s
2. Identify bottlenecks (which span takes longest?)
3. Trace errors through the entire request flow
4. Correlate traces with logs using trace ID

#### Complete Observability Stack

For a full observability setup, use the AccelByte gRPC plugin dependencies:

**Step 1: Clone Dependencies Repository**

```bash
git clone https://github.com/AccelByte/grpc-plugin-dependencies.git
cd grpc-plugin-dependencies
```

**Step 2: Start Observability Stack**

```bash
docker compose up
```

This starts:
- **Prometheus** (http://localhost:9090) - Metrics collection
- **Grafana** (http://localhost:3000) - Metrics visualization
- **Loki** (http://localhost:3100) - Log aggregation
- **Zipkin** (http://localhost:9411) - Distributed tracing

**Step 3: Configure Service**

Update your `docker-compose.yaml`:

```yaml
services:
  matchmaking-service:
    environment:
      - OTEL_EXPORTER_ZIPKIN_ENDPOINT=http://host.docker.internal:9411/api/v2/spans
      - OTEL_SERVICE_NAME=matchmaking-service
      - LOG_LEVEL=Information
    logging:
      driver: loki
      options:
        loki-url: http://host.docker.internal:3100/loki/api/v1/push
        mode: non-blocking
        max-buffer-size: 4m
        loki-retries: "3"
```

**Step 4: Install Loki Docker Plugin**

```bash
docker plugin install grafana/loki-docker-driver:latest --alias loki --grant-all-permissions
```

**Step 5: Access Grafana**

1. Open http://localhost:3000 (admin/admin)
2. Data sources are pre-configured:
   - Prometheus: http://prometheus:9090
   - Loki: http://loki:3100
   - Zipkin: http://zipkin:9411
3. Create dashboards or import existing ones

**Step 6: Generate Test Data**

Run matchmaking requests to generate metrics, logs, and traces:

```bash
# Submit multiple requests
for i in {1..10}; do
  curl -X POST http://localhost:8000/matchmaking/v1/request \
    -H "Authorization: Bearer $TOKEN" \
    -H "Content-Type: application/json" \
    -d '{"metadata": {"region": "us-west"}}'
done
```

#### Production Observability Configuration

For production deployments, configure these environment variables:

```bash
# Logging
LOG_LEVEL=Information

# Tracing
OTEL_EXPORTER_ZIPKIN_ENDPOINT=https://your-zipkin-server/api/v2/spans
OTEL_SERVICE_NAME=matchmaking-service

# Metrics (exposed at :8080/metrics)
# Configure Prometheus to scrape this endpoint
```

**Best Practices:**
- Use `Information` log level in production (not `Debug`)
- Set up alerts on key metrics (error rate, latency, pool size)
- Retain traces for at least 7 days for debugging
- Use log aggregation (Loki, CloudWatch, Stackdriver) for centralized logs
- Monitor EOS session creation success rate
- Track match creation rate and pool size trends

#### Troubleshooting with Observability

**High Latency:**
1. Check Zipkin traces to identify slow spans
2. Look for slow EOS SDK calls
3. Check if pool operations are slow (consider Redis)

**High Error Rate:**
1. Check logs for error messages
2. Use Zipkin to trace failed requests
3. Check EOS session creation failure rate metric

**Requests Not Matching:**
1. Check `matchmaking_pool_size` metric (is pool growing?)
2. Check logs for MatchMaker tick events
3. Verify MatchSize configuration
4. Check for EOS session creation failures

**Memory Issues:**
1. Check pool size metric (is it growing unbounded?)
2. Check completed request store size
3. Verify expiration is working (check logs)
4. Consider implementing Redis-based storage

---

## Error Codes

### gRPC Error Responses

The service returns gRPC errors with this structure:

```json
{
  "code": 5,
  "message": "Match request not found",
  "details": []
}
```

### Common Error Scenarios

**Match Request Not Found (404)**
```json
{
  "code": 5,
  "message": "Match request not found"
}
```
→ Request ID doesn't exist or was already processed

**User Already Has Pending Request (409)**
```json
{
  "code": 9,
  "message": "User already has a pending match request: {request_id}"
}
```
→ User must cancel existing request before submitting new one

**Cannot Cancel Non-Pending Request (412)**
```json
{
  "code": 9,
  "message": "Cannot cancel request with status: MATCHED"
}
```
→ Only PENDING requests can be cancelled

**Unauthenticated (401)**
```json
{
  "code": 16,
  "message": "Authorization required"
}
```
→ Missing or invalid Bearer token

### gRPC Code to HTTP Status Mapping

| gRPC Code | gRPC Name | HTTP Equiv | When to Retry |
|-----------|-----------|------------|---------------|
| 5 | NotFound | 404 | ❌ Never |
| 9 | FailedPrecondition | 412 | ❌ Never |
| 13 | Internal | 500 | 🔄 Yes |
| 16 | Unauthenticated | 401 | 🔑 Refresh token |

---

## Multi-Instance Deployment Considerations

For high availability and horizontal scaling, you can deploy multiple matchmaker instances. However, the default in-memory implementations are designed for single-instance deployments and must be replaced with distributed versions.

### When to Deploy Multiple Instances

**Deploy multiple instances when:**
- You need high availability (zero downtime during deployments)
- You have > 1000 concurrent players
- You need to scale horizontally
- You require geographic distribution (multiple regions)

**Single instance is sufficient when:**
- You have < 1000 concurrent players
- You can tolerate brief downtime during deployments
- You want to minimize infrastructure complexity and costs

### Required Customizations

To deploy multiple instances, you **must** implement distributed versions of these infrastructure components:

#### 1. IMatchPool - Distributed Match Pool

**Why:** The default `MatchPool` uses in-memory storage that cannot be shared across instances. Each instance would have its own isolated pool, preventing proper matching.

**Solution:** Implement Redis-based or database-backed match pool.

**See:** [Redis-based MatchPool example](architecture.md#redis-based-matchpool-example) in the Architecture Guide

**Key Considerations:**
- Use Redis sorted sets for FIFO ordering
- Ensure atomic operations for add/remove
- Handle connection failures gracefully
- Monitor Redis performance and memory usage

#### 2. ICompletedRequestStore - Distributed Completed Request Store

**Why:** The default `CompletedRequestStore` uses in-memory storage. In multi-instance deployments, a request completed on one instance won't be visible to other instances.

**Solution:** Implement Redis-based or database-backed completed request store.

**See:** [Database-backed CompletedRequestStore example](architecture.md#database-backed-completedrequeststore-example) in the Architecture Guide

**Key Considerations:**
- Use Redis with TTL for short retention (< 24 hours)
- Use database for long retention or compliance requirements
- Implement automatic expiration/cleanup
- Monitor storage size and query performance

#### 3. IClaimedSessionsCache - Distributed Claimed Sessions Cache (Find Mode Only)

**Why:** In find mode, the default `InMemoryClaimedSessionsCache` prevents race conditions within a single instance. In multi-instance deployments, multiple instances could claim the same session simultaneously.

**Solution:** Implement Redis-based claimed sessions cache with TTL.

**Note:** Only required if using find mode. Not needed for create mode.

**Key Considerations:**
- Use Redis with TTL matching `ClaimedSessionExpirationSeconds`
- Implement atomic check-and-set operations
- Handle Redis connection failures
- Monitor cache hit rates

### Load Balancing

When deploying multiple instances, AccelByte's platform handles load balancing automatically. The platform distributes traffic across your instances using its built-in load balancing capabilities.

**Load Balancing Considerations:**
- AccelByte platform uses round-robin distribution by default
- Health checks are automatically configured
- Connection pooling is managed by the platform
- Timeouts are configurable via environment variables

### Deployment Architecture

**Multi-Instance Architecture:**

```
                    ┌─────────────────┐
                    │  Load Balancer  │
                    └────────┬────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
    ┌───────▼──────┐  ┌─────▼──────┐  ┌─────▼──────┐
    │ Matchmaker 1 │  │Matchmaker 2│  │Matchmaker 3│
    └───────┬──────┘  └─────┬──────┘  └─────┬──────┘
            │                │                │
            └────────────────┼────────────────┘
                             │
            ┌────────────────┼────────────────┐
            │                │                │
    ┌───────▼──────┐  ┌─────▼──────┐  ┌─────▼──────┐
    │     Redis    │  │  Database  │  │  EOS SDK   │
    │  (Match Pool)│  │(Completed) │  │ (Sessions) │
    └──────────────┘  └────────────┘  └────────────┘
```

### Configuration Example

**Docker Compose (Multi-Instance):**

```yaml
version: '3.8'

services:
  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
    volumes:
      - redis-data:/data

  matchmaking-1:
    build: .
    environment:
      - Redis__ConnectionString=redis:6379
      - OTEL_SERVICE_NAME=matchmaking-service-1
    depends_on:
      - redis

  matchmaking-2:
    build: .
    environment:
      - Redis__ConnectionString=redis:6379
      - OTEL_SERVICE_NAME=matchmaking-service-2
    depends_on:
      - redis

  matchmaking-3:
    build: .
    environment:
      - Redis__ConnectionString=redis:6379
      - OTEL_SERVICE_NAME=matchmaking-service-3
    depends_on:
      - redis

  nginx:
    image: nginx:alpine
    ports:
      - "8000:80"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf
    depends_on:
      - matchmaking-1
      - matchmaking-2
      - matchmaking-3

volumes:
  redis-data:
```

**Note**: When deploying to AccelByte's platform, you configure the number of replicas through the Admin Portal. The platform handles the deployment, scaling, and load balancing automatically.

### Monitoring Multi-Instance Deployments

**Additional Metrics to Monitor:**

| Metric | Description | Why Important |
|--------|-------------|---------------|
| Instance count | Number of running instances | Ensure desired replicas are running |
| Redis connection pool | Active/idle connections | Detect connection exhaustion |
| Redis latency | Command execution time | Identify Redis performance issues |
| Database query latency | Query execution time | Identify database bottlenecks |
| Load balancer distribution | Requests per instance | Ensure even distribution |

**Distributed Tracing:**

With multiple instances, distributed tracing becomes essential:
- Trace requests across load balancer → instance → Redis/database
- Identify which instance handled each request
- Correlate errors across instances
- Measure cross-instance latency

**Centralized Logging:**

Use log aggregation to view logs from all instances:
- Loki (with Grafana)
- AWS CloudWatch Logs
- Google Cloud Logging
- Azure Monitor Logs
- Elasticsearch (ELK stack)

### Testing Multi-Instance Deployments

**Test Scenarios:**

1. **Concurrent Matching:**
   - Submit requests to different instances
   - Verify they match correctly across instances
   - Check for duplicate matches

2. **Instance Failure:**
   - Stop one instance
   - Verify requests continue to be processed
   - Check for lost requests

3. **Redis Failure:**
   - Stop Redis temporarily
   - Verify graceful degradation
   - Check error handling and recovery

4. **Load Distribution:**
   - Submit many requests
   - Verify even distribution across instances
   - Check for hot spots

### Troubleshooting Multi-Instance Issues

**Requests Not Matching Across Instances:**
- Verify all instances use the same Redis connection
- Check Redis connectivity from each instance
- Verify MatchPool implementation is truly distributed
- Check for network partitions

**Duplicate Matches:**
- Verify atomic operations in distributed MatchPool
- Check claimed sessions cache (find mode)
- Review Redis transaction handling
- Check for race conditions in matching logic

**Uneven Load Distribution:**
- Check load balancer configuration
- Verify health checks are working
- Review connection pooling settings
- Check for instance performance differences

**Redis Connection Issues:**
- Monitor Redis connection pool metrics
- Check for connection leaks
- Verify connection timeout settings
- Consider Redis Cluster for high availability

### Cost Considerations

**Infrastructure Costs:**
- Multiple matchmaker instances (compute)
- Redis or database (storage)
- Load balancer (networking)
- Increased monitoring and logging costs

**Cost Optimization:**
- Start with 2-3 instances, scale as needed
- Use Redis for short-term storage (cheaper than database)
- Implement auto-scaling based on pool size
- Use spot instances for non-critical environments

### Migration from Single to Multi-Instance

**Migration Steps:**

1. **Implement distributed storage:**
   - Implement Redis-based IMatchPool
   - Implement Redis-based ICompletedRequestStore
   - If using find mode: Implement Redis-based IClaimedSessionsCache

2. **Deploy Redis:**
   - Set up Redis server or cluster
   - Configure connection strings
   - Test connectivity from matchmaker

3. **Update service configuration:**
   - Register distributed implementations in Program.cs
   - Update environment variables
   - Test with single instance first

4. **Deploy multiple instances:**
   - Deploy 2-3 instances behind load balancer
   - Monitor for issues
   - Gradually increase instance count

5. **Validate:**
   - Run load tests
   - Verify matching works correctly
   - Check for duplicate matches or lost requests
   - Monitor metrics and logs

**Rollback Plan:**
- Keep single-instance configuration ready
- Document rollback procedure
- Test rollback in staging environment
- Monitor closely during migration

### Best Practices

1. **Start Simple:** Begin with single instance, migrate to multi-instance only when needed
2. **Test Thoroughly:** Test multi-instance setup in staging before production
3. **Monitor Everything:** Set up comprehensive monitoring before going multi-instance
4. **Implement Gradually:** Add instances one at a time, monitor between additions
5. **Plan for Failure:** Design for instance failures, Redis failures, network partitions
6. **Document Configuration:** Keep clear documentation of distributed setup
7. **Automate Deployment:** Use infrastructure-as-code for reproducible deployments

### Further Reading

- [Architecture Guide - Deployment Considerations](architecture.md#deployment-considerations)
- [Architecture Guide - Redis-based MatchPool Example](architecture.md#redis-based-matchpool-example)
- [Architecture Guide - Database-backed CompletedRequestStore Example](architecture.md#database-backed-completedrequeststore-example)

---

## Troubleshooting

### Common Issues

**Service Won't Start:**
- Check `.env` file has all required variables
- Verify EOS credentials are correct
- Review service logs: `docker compose logs -f`

**Match Requests Not Matching:**
- Verify enough requests in pool (need >= MatchSize)
- Check MatchMaker is running (should see tick logs every second)
- Review MatchMaker logs for errors

**EOS Session Creation Fails:**
- Verify EOS Product, Sandbox, Deployment exist
- Check EOS Client ID and Secret are valid
- Check service logs for EOS error codes

**Permission Denied (403):**
- Verify OAuth client has required permissions
- Check token is valid and not expired
- Regenerate access token

For detailed testing troubleshooting, see the **[Testing Guide](testing_guide.md#troubleshooting)**.

### Observability-Based Troubleshooting

**High Latency:**
1. Check Zipkin traces to identify slow spans
2. Look for slow EOS SDK calls
3. Check if pool operations are slow (consider distributed storage)

**High Error Rate:**
1. Check logs for error messages
2. Use Zipkin to trace failed requests
3. Check EOS session creation failure rate metric

**Requests Not Matching:**
1. Check `matchmaking_pool_size` metric (is pool growing?)
2. Check logs for MatchMaker tick events
3. Verify MatchSize configuration
4. Check for EOS session creation failures

**Memory Issues:**
1. Check pool size metric (is it growing unbounded?)
2. Check completed request store size
3. Verify expiration is working (check logs)
4. Consider implementing distributed storage

---

## Development

### Running Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test
dotnet test --filter "FullyQualifiedName~TestMethodName"
```

### Regenerate Protocol Buffers

After modifying `Protos/matchmaking.proto`:

```bash
dotnet build
```

This regenerates:
- C# gRPC service stubs
- Gateway integration code
- Swagger/OpenAPI specification

---

## Next Steps

- [Testing Guide](testing_guide.md) - Comprehensive testing instructions
- [Architecture Guide](architecture.md) - Technical architecture details
- [AccelByte Docs](https://docs.accelbyte.io/gaming-services/services/extend/service-extension/) - Official documentation
