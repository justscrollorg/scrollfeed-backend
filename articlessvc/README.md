# Articles Service v2.0

An enhanced microservice for fetching and serving Wikipedia articles with NATS messaging and MongoDB persistence.

## Features

- **NATS Integration**: Uses NATS for distributed messaging and background job processing
- **MongoDB Storage**: Persistent storage with proper indexing and cleanup
- **Rate Limiting**: Respects Wikipedia's API rate limits (100 requests/sec)
- **Background Refresh**: Automatic periodic refresh of articles every 6 hours
- **Search Capability**: Full-text search across article titles and descriptions
- **Health Monitoring**: Built-in health checks and statistics endpoints
- **Kubernetes Ready**: Production-ready deployment configuration

## Architecture

```
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   Client API    │───▶│ Articles Service │───▶│   MongoDB       │
└─────────────────┘    └─────────────────┘    └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │   NATS Server   │
                       └─────────────────┘
                                │
                                ▼
                       ┌─────────────────┐
                       │ Background Jobs │
                       └─────────────────┘
```

## API Endpoints

### GET /articles-api
Get paginated articles with optional search
- Query params: `page`, `pageSize`, `search`

### GET /articles-api/{id}
Get a specific article by ID

### GET /articles-api/stats
Get service statistics and configuration

### POST /articles-api/refresh
Trigger manual refresh of articles
- Query params: `batchSize`

## Port Forward
```bash
kubectl port-forward svc/articles-service 8080:80
```

## Deployment

### Docker
```bash
docker build -t anurag2911/articles-service:latest .
docker push anurag2911/articles-service:latest
```

### Kubernetes
```bash
kubectl apply -f ../../../_infra/articles-service/deployment.yaml
```