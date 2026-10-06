# Local containers and packaged-app validation

This is a **loopback-only development/demo deployment**, not production readiness or a complete BizFlow demo. The modular monolith remains one API/application/domain deployment; Nginx serves the compiled Angular SPA and proxies API requests. PostgreSQL remains the transactional database. Redis and S3-compatible storage are deferred until their consuming features land, not replaced with other technologies.

## Start the packaged application

Run from the repository root with Docker Desktop in Linux-container mode (Compose v2). Copy `deploy/.env.example` to the Git-ignored `deploy/.env` and replace the placeholder secrets before startup. Set:

- `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` for database initialization.
- `BIZFLOW_DATABASE_CONNECTION` for API/migrations: use `Host=postgres` and the same database/user/password. This is a complete Npgsql connection string so passwords requiring quoting can be represented without unsafe string concatenation. Do not reuse the host-run `Host=localhost` value inside containers.
- `JWT_SIGNING_KEY_BASE64`: base64 encoding of at least 32 cryptographically random bytes; do not use a memorable password or the committed placeholder.
- Optional `JWT_ISSUER`, `JWT_AUDIENCE`, `WEB_PORT` (default 8080), `POSTGRES_PORT` (default 5432).

```powershell
$compose = @('compose', '--env-file', 'deploy/.env', '-f', 'deploy/docker-compose.yml', '-f', 'deploy/docker-compose.app.yml')
docker @compose --profile tools build api web migrate
docker @compose up -d --wait postgres
docker @compose run --rm migrate
docker @compose up -d --wait api web
```

Open `http://localhost:8080` (or WEB_PORT). Migrations are an **explicit, one-shot operator command**, not an API startup action. Repeating the migration command is safe when the database is already current. The migration image uses the pinned repository EF tool and compiled migrations. Normal startup does not run its `tools` profile service.

A fresh database has **no login account**. These images deliberately contain no demo/admin password, automatic platform bootstrap or seeded business objects. Provisioning/bootstrap remains unfinished; use the existing disposable real-browser harness to demonstrate working login/reset/registry behavior with test-only accounts. Do not mistake a running login screen for a complete onboarding implementation.

For normal shutdown, `docker @compose down` retains the development PostgreSQL volume. Do not add `--volumes` unless you intentionally want to destroy that database. Changing POSTGRES_PASSWORD in `.env` does not change credentials inside an already-initialized PostgreSQL volume; rotate database credentials deliberately rather than deleting data.

## Isolated smoke check

```powershell
./deploy/Test-Containers.ps1 -Build
```

The script builds local `bizflow/api:local`, `bizflow/web:local` and `bizflow/migrate:local` images, creates a uniquely named `bizflow-smoke-<guid>` project, chooses available loopback ports and supplies random process-scoped secrets. With images already built, omit `-Build`. It does not read or edit your local `deploy/.env`.

Add `-Browser` after installing the existing browser-test dependencies and Chromium (see the root README) to verify the **production-built** Angular login, credential failure, recovery navigation and all currently implemented protected screen routes at desktop/mobile widths through the container proxy. These include Tasks, SLA history, configuration, inbox and both audit planes. No Angular development server or mocked API is used for this check.

It starts fresh PostgreSQL, applies migrations twice, starts the packaged API/SPA, and verifies SPA deep links, API liveness/readiness, all currently implemented protected read endpoints' 401 JSON contracts, JSON 404 rather than HTML fallback, and unknown-user login against the migrated database. It stops only its disposable PostgreSQL service to verify readiness returns 503 while liveness remains 200, then restarts the database and verifies recovery. It also inspects non-root users and read-only root filesystems. Its `finally` cleanup removes **only that invocation's** test containers, network and disposable database volume. Images and build cache remain for reuse. No real account or external email is used. These HTTP checks complement—not replace—the Playwright business-flow tests; anonymous rejection alone does not prove authenticated resource authorization.

## Boundaries and remaining production gates

- Runtime API and web containers are non-root, have read-only root filesystems, writable `/tmp`, dropped Linux capabilities and `no-new-privileges`. The build stages use SDK/toolchain images; SDKs and source are not copied into runtime images.
- `.dockerignore` excludes local secrets, Git/agent metadata, tests, docs, host build artifacts and dependency caches. Application build/runtime base images are pinned by verified registry manifest digest. NuGet restore uses lock enforcement; Node/npm versions are pinned and the SPA uses `npm ci`. Refresh image digests deliberately after security review and rerun validation; digest pinning does not replace vulnerability monitoring.
- Only the web and PostgreSQL ports bind to `127.0.0.1`; API has no published host port. Do not expose this plaintext demo configuration to a network. Production needs TLS, host validation, trusted-proxy configuration, secret provisioning, database least-privilege roles, backups, image/dependency scanning, readiness checks and operational monitoring.
- Forwarded client headers are cleared by this demo proxy and not trusted by the API. Therefore the backend IP quota is shared by callers behind this proxy; user/tenant application quotas still apply. Production must explicitly configure trusted proxies and coordinated rate limits before scale-out. Do not enable unrestricted forwarded-header trust.
- `/health/live` proves process liveness only. The web health check uses `/health/ready`, which checks database access and exact migration history without applying migrations. Missing/unknown migration IDs return a safe 503; it does not prove manual schema integrity, every external integration or full feature availability. [Readiness contract and limits](../docs/02-traceability/operational-readiness.md).
- Recovery email is disabled in this overlay. Configure TLS SMTP through an operator-owned Compose override and the settings in `docs/02-traceability/password-reset-slice.md` before enabling it. AI/provider/storage credentials are not embedded in images.
- The local demo connection explicitly disables optional GSS negotiation because Kerberos is not configured. Npgsql documents this setting for the harmless missing-library warning in minimal Linux images. This setting does not replace production database TLS configuration; configure certificate-verified transport for a network deployment. [Npgsql security documentation](https://www.npgsql.org/doc/security.html#gss-session-encryption-gss-api).
- The current proxy serves JSON APIs and the SPA. Attachment direct-upload and SignalR configuration must be verified with those features; this deployment does not claim the 500 MB attachment requirement is implemented.
- Containers have been targeted for the local Linux Docker environment. The [CI workflow](../.github/workflows/ci.yml) now invokes the isolated smoke check; its first hosted execution remains unverified. Multi-architecture publishing, a remote registry and production rollout are not implemented.
