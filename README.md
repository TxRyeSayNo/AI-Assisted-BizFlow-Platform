# AI-Assisted BizFlow Platform

An internal, multi-tenant work and request orchestration platform. The approved v1.2 specification is [the project source of truth](AI-Assisted_BizFlow_Software_System_Specification.md). Architecture and development stages are in [docs/00-analysis](docs/00-analysis).

## Current implementation

Development is in DEV-D4, with selected DEV-D5 prerequisites implemented. The initial ten-table identity migration, tenant safeguards, permission resolver, append-only audit, employee-code/email login, JWT access tokens, rotating refresh sessions and email password recovery are implemented. Angular GRID authentication screens connect to the real API. **Administrator-issued temporary passwords, forced-change login, provisioning and business workflows remain incomplete.** No production/demo credentials or fabricated business data are shipped; tests seed only disposable databases.

The approved session-persistence amendment is recorded in [ADR-0002](docs/01-decisions/ADR-0002-authentication-persistence.md). This increases the target model from 42 to 43 application tables.

The platform company-registration queue is available at `/platform/companies` for the explicit platform read permission. It supports live authorization, search/status filters and pagination; approval and provisioning are not implemented yet. [Queue contract and traceability](docs/02-traceability/platform-registration-queue.md).

The tenant-scoped department directory API and read-only GRID screen are implemented at `/settings/organization/departments`, reachable from the workspace. Search, status filters and pagination use the real tenant-authorized API. Department mutations, hierarchy management and the full organization setup UI remain pending. See [directory contract](docs/02-traceability/department-directory-api.md) and [business decisions awaiting confirmation](docs/02-traceability/pending-owner-decisions.md).

The people directory at `/settings/organization/users` provides tenant-authorized name/code/email search, account-status filtering and pagination, backed by `GET /api/v1/users`. It requires the explicit `users.read` grant seeded for the three tenant system roles. User creation, profile updates and deactivation are not implemented by this read-only screen. [People-directory contract and security evidence](docs/02-traceability/people-directory.md).

Authorized tenant administrators can use Settings → Roles and permissions to create custom roles and configure the currently available permission catalog. The API enforces tenant scope, protects system roles, rejects stale edits and audits changes. User-role assignment and the full permission matrix remain incomplete. [Role-configuration contract and traceability](docs/02-traceability/role-configuration.md).

The approved management-scope table and live same-tenant descendant resolver are implemented as a security prerequisite. Scope never substitutes for an action permission. Administrator scope configuration and default seeding on user creation/movement remain incomplete; no scope-administration UI or configuration endpoint is exposed. Task assignment uses existing configured scope. [Scope boundary and evidence](docs/02-traceability/management-scope-foundation.md).

The task lifecycle has a tested Domain policy for the canonical transitions and permission/evidence gates. The owner confirmed exact §9.1 precedence in [ADR-0003](docs/01-decisions/ADR-0003-task-transition-precedence.md): resume overdue work before submission and use IN_PROGRESS for result rework. [Task draft/checklist persistence](docs/02-traceability/task-draft-foundation.md) supports independent or Request-linked drafts with tenant and historical-reference safeguards. [Progress/result history](docs/02-traceability/task-evidence-history.md) adds tenant-scoped append-only evidence storage. [Assignment history](docs/02-traceability/task-assignment-history.md) preserves original targets and receipts and implements the owner-approved one-time department claim rule. Workspace → [Tasks](docs/02-traceability/task-list.md) provides the scoped read list, with server-authorized tenant/managed/own/assigned permissions, search, status/priority filters, paging, current responsibility and deadlines. [Independent draft creation and scoped details](docs/02-traceability/task-creation-and-detail.md) add future absolute deadlines, ordered checklists and database-backed audit replay; validation evidence is recorded in implementation status. The [assignment command and GRID drawer](docs/02-traceability/task-assignment-command.md) implement DRAFT/REJECTED assignment with live target scope, atomic history/audit/in-app notifications and safe keyed retries. Request-linked creation, acceptance, active reassignment, progress/submission orchestration and executable workflows remain incomplete. [Task-policy scope and integration gates](docs/02-traceability/task-lifecycle-foundation.md).

Workflow, version, step and transition persistence is implemented with tenant-scoped access and database-enforced published-version immutability. The implemented model contains 29 of the approved 43 application tables, including Notification, TenantSetting, three SLA/calendar configuration tables, Service/ServiceCategory, Request/RequestResolution and five Task/assignment/checklist/evidence tables. Settings → Workflows supports tenant-authorized listing, initial name/type draft creation, atomic empty next-version creation with audit, and read-only version details. Graph editing/validation, publishing and runtime execution remain incomplete. [Workflow library contracts and remaining gates](docs/02-traceability/workflow-library.md).

The [service catalog](docs/02-traceability/service-catalog.md) supports authenticated tenant-member browsing and permission-controlled creation of services with initial categories and atomic audit. Categories can also be added to existing services using ETag concurrency and atomic parent/category/audit persistence. The [metadata editor](docs/02-traceability/service-metadata-update.md) uses the separate `service.update` permission, shared ETag concurrency and atomic before/after audit while preserving categories and version bindings. The GRID UI includes filters, paging, safe details and explicit conflict recovery. Routing, workflow/approval binding and request submission remain incomplete.

The [Request draft/revision persistence foundation](docs/02-traceability/request-draft-foundation.md) preserves the approved data model, tenant-owned references, independent revisions and immutable rejected history. [Resolution-history persistence](docs/02-traceability/request-resolution-history.md) preserves earlier evidence across rework with tenant/reference checks and database-enforced append-only storage. These do not yet expose Request APIs, lifecycle transitions or UI; Application authorization/audit and complete Request workflows remain required.

Settings → SLA profiles provides a permission-controlled catalog and named draft creation with atomic audit. [Catalog contract](docs/02-traceability/sla-profile-catalog.md). Immutable SLA version/calendar storage, calendar schema validation and typed escalation configuration with tenant-recipient checks are implemented. The [version-creation API](docs/02-traceability/sla-version-creation.md) saves validated immutable versions with serialized numbering and atomic audit. [Paginated version history](docs/02-traceability/sla-version-history.md) and its read-only GRID detail drawer expose each saved snapshot's frozen calendar and escalation policy. The [timing editor](docs/02-traceability/sla-timing-editor.md) creates a new snapshot from an existing version while preserving that calendar and policy; validation evidence is tracked in implementation status. First-version/calendar/escalation authoring, default calendar administration and runtime clocks remain incomplete. [Snapshot storage and accepted contracts](docs/02-traceability/sla-snapshot-foundation.md).

The approved tenant-policy table now has typed value validation, tenant/actor persistence guards, unique keys, concurrency and protected rollback. It does not yet expose a policy API/UI or enable policy-controlled features. [Tenant-policy foundation and remaining gates](docs/02-traceability/tenant-setting-foundation.md).

Workspace → Notifications reads only the current tenant recipient's events, with unread count/filter, pagination and idempotent audited read receipts. No production events are fabricated. Task assignment produces recipient events atomically, including active department audiences, with links to scoped Task details. Remaining business event dispatch, email/SignalR delivery, Request/Approval links and platform notification support remain incomplete. [Inbox contracts and remaining gates](docs/02-traceability/notification-inbox.md).

The audit viewer at `/audit` and `/platform/audit` supports permission-scoped event filters and read-only before/after details. Company and platform event streams remain separate. Backend, Angular and real-browser checks pass; complete audit coverage still depends on the remaining business modules. [Audit contract and evidence](docs/02-traceability/audit-viewer.md).

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

The database binds to loopback only and persists in the `bizflow-local_postgres-data` volume. This initial compose file contains PostgreSQL only; Redis and S3-compatible storage will be added with the features that consume them.

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
