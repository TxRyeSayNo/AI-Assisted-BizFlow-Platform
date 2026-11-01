# AI-Assisted BizFlow Platform

An internal, multi-tenant work and request orchestration platform. The approved v1.2 specification is [the project source of truth](AI-Assisted_BizFlow_Software_System_Specification.md). Architecture and development stages are in [docs/00-analysis](docs/00-analysis).

## Current implementation

The implementation baseline as of 1 November 2026 is documented in [CURRENT_IMPLEMENTATION_BASELINE.md](docs/project-audit/CURRENT_IMPLEMENTATION_BASELINE.md), including source links, known gaps and validation limits. The system is a multi-tenant modular monolith with .NET 10 and an Angular 22 SPA. Authentication, tenant/organization management, Task and Request workflows, notifications, reporting and AI assistance have working code, with important limitations recorded in that baseline. Presence of an endpoint or test is not proof that a requirement is complete.

Robot chat provides task-decomposition proposals and single/batch confirmation. AI context authorization, proactive-worker tenant context, fabricated client fallback content and reminder delivery require further verification. The SRS requirements remain separate from current code behavior; proposed decisions are not silently approved.

The repository includes an optional [enterprise simulation seed](deploy/seed_enterprise_simulation.sql) and its [generator](scripts/seed_enterprise_simulation.py). They modify business data and are intended only for an explicitly selected disposable or simulation database. A fresh database does not automatically get these accounts.

The specification entry points are [SRS](docs/requirements/srs/SRS_v1.0_DRAFT.md), [SSS](docs/specifications/sss/SSS_v1.0_DRAFT.md), [architecture decisions](docs/specifications/sss/ARCHITECTURE_DECISIONS.md) and the [project audit](docs/project-audit/PROJECT_AUDIT_REPORT.md).

## Prerequisites

- .NET SDK 10.0.401 (see `global.json`).
- Node 22.22.3 or a compatible version declared in `frontend/package.json`; npm 11.6.2. The earlier planning document's Node 22.17.0 claim is superseded by the actual Angular 22.2 CLI engine requirements.
- Docker with Linux containers for PostgreSQL 18 integration and local development.

## Run and validate

From the repository root:

```powershell
dotnet restore BizFlow.slnx
dotnet test BizFlow.slnx --no-restore
```

Integration tests start a disposable PostgreSQL 18 container and apply migrations; Docker must be running in Linux-container mode. They never use the local development database. If Windows Testcontainers cannot locate Docker Desktop's engine, inspect `docker context inspect` and set the matching endpoint for that terminal, for example `$env:DOCKER_HOST='npipe://./pipe/dockerDesktopLinuxEngine'`. The .NET Docker client requires that URI form, not Docker CLI's four-slash form. Do not disable the database tests to obtain a green build.

The development API listens on `http://localhost:5154`. `/health/live` checks process liveness only; `/health/ready` checks database access and matching migration history without applying migrations, returning a safe 503 when not ready. `/openapi/v1.json` is available in Development. These endpoints do not prove business-feature completion or detect arbitrary manual schema drift. [Readiness contract](docs/02-traceability/operational-readiness.md).

In a second terminal:

```powershell
cd frontend
npm ci
npm start
```

The Angular development server proxies `/api` to the API; open `http://localhost:4200`. Use `npm run build` and `npm test` to validate the frontend. `npm run format:check` checks project source formatting (the approved token assets are maintained verbatim).

Browser tests live in `tests/BizFlow.E2ETests`. Run `npm ci`, `npx playwright install chromium`, then `npm test` from that folder using the compatible Node runtime. These UI failure tests use mocked responses on port 4280. `npm run test:real` instead starts disposable PostgreSQL, applies migrations, seeds ephemeral accounts, runs the real API on a dynamic loopback port and tests authentication/recovery, the platform registry, tenant directories, roles, workflow drafts, SLA profile drafts, audit viewing and recipient notifications on port 4281. Real case starts are paced because they share one loopback client; production authentication rate limits remain enabled and unchanged. Reset links are issued through the real API and Hangfire worker; only email delivery is captured in memory. The SMTP adapter has a separate loopback delivery test. The harness owns and cleans up its resources. Real-auth network traces are disabled to avoid persisting credentials.

For PostgreSQL, copy `deploy/.env.example` to `deploy/.env`, replace the password placeholder, and run:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```

Compose binds PostgreSQL, Redis and the S3-compatible service to loopback by default and uses named volumes. See [deploy/docker-compose.yml](deploy/docker-compose.yml) for the actual port defaults and configuration.

For packaged API/frontend containers, use the optional `deploy/docker-compose.app.yml` overlay. It builds the real Angular SPA and API plus an explicit migration image. [Container setup, smoke checks and production gates](deploy/README.md). It does not create login accounts or automatically migrate a development database.

Before running the API, set `ConnectionStrings__BizFlow` to your PostgreSQL connection string and configure `Jwt__Issuer`, `Jwt__Audience`, and `Jwt__SigningKeyBase64` via environment variables or a secret provider. The key must be cryptographically random, base64 encoded, and at least 32 bytes; never commit it. Startup rejects missing JWT settings. Then run:

```powershell
dotnet tool restore
dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure
dotnet run --project backend/BizFlow.Api
```

Provisioning is still pending; a fresh development database has no login accounts. Use the isolated real-browser harness to validate authentication without adding production seed credentials. [Authentication configuration and security behavior](docs/02-traceability/authentication-slice.md).

Email password recovery is disabled until an operator configures TLS SMTP and the frontend origin. See [password-recovery configuration and remaining requirements](docs/02-traceability/password-reset-slice.md). Enabling it starts Hangfire's PostgreSQL-backed email worker; no public job dashboard is exposed.

## Boundaries and evidence

`frontend` → `BizFlow.Api` → `BizFlow.Application` → `BizFlow.Domain`, with provider code in `BizFlow.Infrastructure`. Controllers and AI tools must use application services. Role names do not grant authority; the server resolves explicit permissions and tenant/resource scope. Client route guards are usability controls only.

Current tests cover policy decisions, audit, identity constraints, login, JWT validation, current account/tenant checks, rate limits and refresh rotation/replay through the real API and PostgreSQL. Authenticated test-only probes do **not** replace the full business-endpoint tenant-isolation test `TST-SEC-001`.

Progress and remaining gates: [implementation status](docs/02-traceability/implementation-status.md).

The local [CI workflow](.github/workflows/ci.yml) defines locked Release builds, database/migration tests, Angular/browser checks and isolated packaged-app validation. It has not been pushed or executed on GitHub. [CI scope, security boundaries and activation gates](docs/02-traceability/continuous-integration.md).
