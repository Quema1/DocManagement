# DocManagement API

REST API for managing documents (`id`, `name`, `description`) with a simulated, asynchronously generated preview URL.

Stack: .NET 10, ASP.NET Core, EF Core + SQL Server, RabbitMQ, xUnit + Testcontainers.

## Running

```bash
docker compose up --build
```

- API: http://localhost:8080, Scalar UI: http://localhost:8080/scalar
- SQL Server on host port `14333`, RabbitMQ on `5672` (management UI on `15672`)
- Migrations are applied on startup.

To run the API from the IDE instead, start only the dependencies with `docker compose up -d sqlserver rabbitmq`.

All endpoints require a JWT. In Development you can get one for any user name:

```bash
curl -X POST http://localhost:8080/api/auth/token -H "Content-Type: application/json" -d '{"userName":"alice"}'
```

`src/DocManagement.Api/DocManagement.Api.http` contains the full request flow.

## API

| Method   | Route                          | Description |
|----------|--------------------------------|-------------|
| `GET`    | `/api/documents?page=&pageSize=` | List documents (paged) |
| `GET`    | `/api/documents/{id}`          | Get a document |
| `POST`   | `/api/documents`               | Create a document |
| `PUT`    | `/api/documents/{id}`          | Update a document |
| `DELETE` | `/api/documents/{id}`          | Delete a document |
| `POST`   | `/api/documents/{id}/preview`  | Request a preview (`202 Accepted`, or `200` if already ready) |
| `GET`    | `/api/documents/{id}/preview`  | Get preview status and URL |
| `POST`   | `/api/auth/token`              | Issue a dev token (Development only) |
| `GET`    | `/health`                      | Health check (SQL Server, RabbitMQ) |

Preview status goes `None` → `Pending` → `Ready` (or `Failed`). Editing a document resets it to `None`.

The preview URL is deterministic: `{BaseUrl}/previews/{hash}.png`, where `hash` is the first 16 hex chars of `SHA-256(Id, Name, Description)`.
Identical content gives the same URL and any edit gives a new one, so caches invalidate naturally;
the document Id is part of the hash so equal documents don't share a URL, without exposing the Id itself.

## Architecture

Clean Architecture with four projects:

- **Domain**: entities, enums, constants.
- **Application**: services, DTOs, repository and queue interfaces.
- **Infrastructure**: EF Core persistence and messaging.
- **Api**: controllers, auth, rate limiting, health checks, error handling.

## Preview processing

1. `POST /preview` sets the document to `Pending` and saves an outbox message in the same transaction.
2. A background publisher sends outbox messages to RabbitMQ (durable queue `document-previews`). Failures are retried with exponential backoff; after 10 attempts the message is dead-lettered and the preview is marked `Failed`.
3. A RabbitMQ consumer generates the URL after a simulated delay and sets `Ready`.

## Other features

- **Auth**: JWT bearer; every endpoint requires an authenticated user unless marked anonymous.
- **Rate limiting**: global token bucket per user (or IP), plus a stricter limit on preview requests. Returns `429` with `Retry-After`.
- **Logging**: structured logs (`DocumentId`, `OutboxMessageId`, …), JSON outside Development.

## Tests

```bash
dotnet test
```

Unit tests cover the services and helpers. Integration tests run the API against real SQL Server and RabbitMQ containers (Testcontainers), so Docker must be running.

## Design decisions and trade-offs

- **Optimistic concurrency is internal only.** A SQL Server `rowversion` protects the read-modify-write window inside one request (conflicts map to `409 Conflict`). It is not exposed to clients, so two clients editing the same document sequentially follow last-write-wins. The production answer would be an `ETag` built from the row version plus `If-Match` on `PUT`/`DELETE` (`412 Precondition Failed`).
- **Transactional outbox is at-least-once and single-instance.** The document update and the outbox message are saved in one `SaveChanges`. The publisher polls without row locking, so several API replicas could publish the same message twice. This is safe because preview generation is idempotent (a document that is no longer `Pending` is skipped). For scale-out, rows would be claimed with `UPDLOCK, READPAST` or a lease column.
- **Development-only conveniences.** EF migrations are applied on startup, Scalar/OpenAPI is exposed, and `POST /api/auth/token` issues a JWT for any user name only in the `Development` environment. Outside it the token endpoint returns `404` and tokens must come from a real identity provider. `docker-compose.yml` therefore runs the API in `Development`. In production, migrations would run as a separate step (migration bundle or job) rather than on startup of every replica.
- **API and workers run in one process.** Simpler for a small service; the consumer could move to a separate worker service.

## What's next

- **Real document upload**: store files in blob storage (Azure Blob / S3), render actual previews from their content and return signed URLs.
- **Redis**: cache for document reads and a distributed rate limiter, so limits hold across several API instances.
- **Prometheus + Grafana**: `/metrics` endpoint (request rates, outbox backlog, preview latency) and dashboards.
- **OpenTelemetry**: tracing from the HTTP request through the outbox and RabbitMQ to preview generation.
- **Azure DevOps pipeline**: build, tests and Docker image publish; deploy as a Docker Swarm stack.
- `ETag` / `If-Match` for client-visible concurrency; a separate worker service for preview generation.
