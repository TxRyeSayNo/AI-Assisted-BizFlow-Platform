# Operational readiness

Scope: necessary deployment/monitoring infrastructure under SSS §18/20 and DEV-D2/DEV-D8. This is not a new business module, permission, entity or claim of completed product functionality.

## Contract

- `GET /health/live` retains its existing anonymous `200 { status: "healthy" }` process-liveness contract. It does not depend on PostgreSQL.
- `GET /health/ready` is anonymous and non-cacheable. It returns `200 { status: "ready" }` when PostgreSQL is reachable and the applied EF migration IDs exactly match this executable's migrations. Otherwise it returns 503 with the standard safe error envelope, code `SERVICE.NOT_READY`, generic message, null details, trace ID and UTC timestamp.
- Missing history, missing migrations and unknown/newer migration IDs fail closed. Deployment order remains explicit: apply the migration bundle, then start/route the matching application version. This does not promise compatibility between different application/schema versions during a rolling upgrade.
- The probe does not migrate, seed, audit a business mutation, query tenant rows or reveal database/host names, migration IDs or exception details. It checks migration history, not manual schema drift, every database privilege, external integrations or full feature availability.

## Boundaries and timeout behavior

The API delegates through `IReadinessProbe` to Infrastructure. No controller or endpoint accesses DbContext directly. Readiness uses a separate, disposed context with no user/tenant identity and probe-only connection options built from operator configuration; ordinary business context/pool configuration is unchanged. It never rebuilds credentials from a previously opened connection's potentially redacted connection string.

The HTTP check supplies a three-second cancellation deadline. A stalled PostgreSQL handshake test proved that this token alone did not bound the configured 30-second connection timeout. Probe connections therefore also use a two-second connection timeout, two-second command timeout, one-second cancellation-response timeout and no pooling. The provider's bounded cleanup may extend beyond the nominal cancellation deadline. These are separate controls in the [official Npgsql timeout documentation](https://www.npgsql.org/doc/connection-string-parameters.html#timeouts-and-keepalive).

Operational probes expose only aggregate service availability. Production ingress should restrict probe traffic to its monitoring/load-balancing infrastructure; public internet exposure and production network policy are not supplied by the loopback demo deployment. SMTP/AI/storage/SignalR readiness must be considered when those production integrations exist; none is implicitly declared healthy here.

## Deployment and evidence

Nginx explicitly proxies both health endpoints rather than falling back to the SPA. The packaged web health check now uses readiness, so Compose health waits include API/database migration readiness. Liveness remains available independently for restart decisions.

The disposable container smoke test verifies ready after explicit migrations, 503 during a database stop, continued process liveness and recovery after the database restarts. Only that invocation's uniquely named test project is stopped or cleaned up.

`ReadinessTests` covers current migration history, caller-supplied tenant header non-authority, empty-schema non-mutation, unknown migration rejection/recovery, a nonresponsive real TCP peer, cancellation and safe/non-cacheable response fields. Latest execution results belong in [implementation status](implementation-status.md); this contract alone is not test-pass evidence.
