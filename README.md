# DocManagement API

> The full README (overview, how to run, API walkthrough) is still to be written. This section records the deliberate trade-offs.

## Design decisions and trade-offs

- **`POST /api/documents` returns `200 OK` with the created document**, not `201 Created` + `Location`. It is a conscious simplification; switching to `CreatedAtAction(nameof(GetById), …)` is a one-line change.
- **Optimistic concurrency is internal only.** A SQL Server `rowversion` protects the read-modify-write window inside one request (conflicts map to `409 Conflict`). It is not exposed to clients, so two clients editing the same document sequentially follow last-write-wins. The production answer would be an `ETag` built from the row version plus `If-Match` on `PUT`/`DELETE` (`412 Precondition Failed`).
- **Transactional outbox is at-least-once and single-instance.** The document update and the outbox message are saved in one `SaveChanges`. The publisher polls without row locking, so several API replicas could publish the same message twice. This is safe because preview generation is idempotent (a document that is no longer `Pending` is skipped). For scale-out, rows would be claimed with `UPDLOCK, READPAST` or a lease column.
- **Development-only conveniences.** EF migrations are applied on startup, Scalar/OpenAPI is exposed, and `POST /api/auth/token` issues a JWT for any user name only in the `Development` environment. Outside it the token endpoint returns `404` and tokens must come from a real identity provider. `docker-compose.yml` therefore runs the API in `Development`. In production, migrations would run as a separate step (migration bundle or job) rather than on startup of every replica.
