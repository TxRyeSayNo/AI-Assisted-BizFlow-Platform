# AI-Assisted BizFlow Platform — Architecture Report

Source: SSS §17 (System Architecture), §18 (Technology Stack), §19 (Security), §12–13 (Database), §14/Appendix D (API), §20 (Logging/Errors), §28 (Implementation Contract).

---

## 1. Architecture Decision

**Modular monolith.** One deployable ASP.NET Core 10 Web API hosting all 13 modules as internal modules with explicit boundaries, plus one Angular 22 SPA. No microservices, no Kubernetes (§17, §2.7, §28.1).

**Why:** the SSS fixes it. It is also the technically correct choice here — the modules share one transactional boundary (a task is created, assigned, SLA-timed and audited in one unit of work), so splitting them would replace in-process transactions with eventual consistency for no benefit at this scale.

### 1.1 Layers

| Layer | Project | Contains | Must not contain |
|---|---|---|---|
| Presentation | `/frontend` (Angular 22) | Screens, forms, state, API clients | Business rules, authorization decisions |
| API | `BizFlow.Api` | Controllers, DTOs, model binding, auth wiring, SignalR hubs, error mapping | Business logic, EF Core usage |
| Application | `BizFlow.Application` | Use-case services, commands/queries, validation orchestration, **transaction boundaries**, **audit emission**, **authorization calls**, **SLA/notification dispatch** | Provider-specific infrastructure, SQL |
| Domain | `BizFlow.Domain` | Entities, value objects, **state machines**, workflow guards, permission policy, business rules | EF Core, HTTP, OpenAI |
| Infrastructure | `BizFlow.Infrastructure` | EF Core DbContext + migrations, object storage, email, OpenAI client, Hangfire jobs, Redis cache | Business decisions |

**Hard rule (§28.2 rule 44):** controllers never touch `DbContext`. AI tools never touch `DbContext`. Both go through Application services. This single rule is what makes the tenant/authorization/audit guarantees enforceable rather than aspirational.

### 1.2 Cross-cutting components

| Component | Lives in | Responsibility |
|---|---|---|
| `ITenantContext` | Application (impl. Api) | Resolves current `TenantId`/`UserId` from the authenticated principal; every tenant-owned query and write is asserted against it (BR-001) |
| `IResourceAuthorizer` | Application + Domain policy | Single authorization path used by **both** human and AI callers; evaluates permission code + scope type (PLATFORM/TENANT/DEPARTMENT/SELF/ASSIGNED) + manager scope (BR-002, BR-003, BR-006) |
| `IWorkflowEngine` | Domain + Application | The **only** component that mutates `Task.Status` / `Request.Status`; validates transition against the pinned `WorkflowVersion` and guard JSON (FR-WF-002, C-04) |
| `ISlaService` | Application | Starts/pauses/resumes/evaluates SLA clocks using the tenant business calendar and the pinned `SLAVersion` (FR-SLA-002/004) |
| `IAuditWriter` | Application | Append-only audit written at the mutation boundary; called by use cases, never by controllers (BR-020) |
| `INotificationDispatcher` | Application | Resolves recipients, enforces `IdempotencyKey`, writes in-app notification, queues email (BR-019) |
| `IUnitOfWork` | Application | Transaction scope for multi-record lifecycle changes (§28.2 rule 51) |

---

## 2. Technology Stack (fixed by §28.1, verified against this machine)

| Layer | Technology | Local status |
|---|---|---|
| Frontend | Angular 22 + TypeScript | Node **v22.17.0**, npm **10.9.2** — satisfies Angular 22's Node range (^20.19 / ^22.12 / >=24) |
| UI | Angular Material + Tailwind CSS | to install |
| Backend | ASP.NET Core 10 Web API, C# | .NET SDK **10.0.401** installed |
| ORM | EF Core 10 + Npgsql 10 | to install |
| Database | PostgreSQL 18 | via Docker (`postgres:18`) — no local `psql` |
| Auth | ASP.NET Core Identity + JWT (access + rotating refresh) | to implement |
| Authorization | Policy-based + resource + tenant scope | to implement |
| Realtime | SignalR | to implement |
| Background jobs | Hangfire (SLA, reminders, escalation, orphan cleanup) | to implement |
| Cache | Redis 8 | via Docker — never the source of truth |
| Object storage | MinIO / S3-compatible, signed direct upload | via Docker |
| AI | OpenAI Responses API + official .NET SDK; model name from environment | to implement behind an interface |
| AI output | Structured Outputs / JSON Schema + tool calling | to implement |
| Logging | Serilog (structured, correlated) | to implement |
| Observability | OpenTelemetry (traces/metrics) | to implement |
| Containers | Docker + Docker Compose | Docker **27.4.0**, Compose **v2.31** |
| Testing | xUnit (unit) + integration tests (Testcontainers or compose) + Playwright (E2E) | to implement |
| API docs | OpenAPI/Swagger generated from controllers | to implement |

**Deviation:** none. The toolchain on this machine supports the entire fixed stack as written. `psql` is absent but irrelevant — the SSS already mandates Docker Compose as the reference environment.

**Risk note:** the SSS pins pre-release-era versions (Angular 22, .NET 10, PostgreSQL 18, Npgsql 10). Exact patch versions will be pinned in `package.json` / `.csproj` / `docker-compose.yml` at repository initialisation as §33 requires. If any pinned major is unavailable from the registries at install time, I will report it rather than silently downgrade.

---

## 3. Repository Structure (§28.3, concretised)

```
/repo
  /frontend                     Angular 22 SPA
    /src/app
      /core                     auth, http interceptors, guards, tenant context, error handling
      /shared                   design-system components, pipes, directives, layout shells
      /features/auth            login, forgot/reset password
      /features/platform        platform admin console (companies, tenants)
      /features/organization    users, departments, roles/permissions
      /features/service         services, categories, routing, workflow/SLA binding
      /features/workflow        workflow version designer, approval center
      /features/sla             SLA profiles, SLA monitor
      /features/task            list, detail, create/AI assistant
      /features/request         list, create, detail, AI analysis
      /features/reports         dashboards and reports
      /features/ai              AI proposal review components (shared by task/request)
      /features/audit           audit viewer
      /features/notification    notifications
  /backend
    BizFlow.Api
    BizFlow.Application
    BizFlow.Domain
    BizFlow.Infrastructure
  /tests
    BizFlow.UnitTests
    BizFlow.IntegrationTests
    BizFlow.E2ETests
  /docs
    00-analysis                  this planning set
    01-decisions                 ADR-style decision log
  /deploy
    docker-compose.yml           postgres 18, minio, redis 8
    .env.example
```

**Module organisation in code:** each Application module is a folder (`Application/Tasks`, `Application/Requests`, …) exposing one public service interface per use case group plus internal handlers. Domain is organised by aggregate (`Domain/Tasks`, `Domain/Requests`, `Domain/Workflow`, `Domain/Sla`, `Domain/Organization`, `Domain/Ai`). No module reaches into another module's internals — cross-module interaction is via application interfaces and domain events.

---

## 4. Database Approach

### 4.1 Baseline

PostgreSQL 18, EF Core 10 + Npgsql 10, code-first migrations, UUIDv7 primary keys, `TIMESTAMPTZ` in **UTC** (§28.2 rule 49), `JSONB` for definition/config payloads.

### 4.2 Rules

| Concern | Approach |
|---|---|
| Tenant isolation | Every tenant-owned entity carries `TenantId`; a **global query filter** keyed on `ITenantContext` is applied in `DbContext.OnModelCreating`. Platform-scoped reads use an explicit `IgnoreQueryFilters()` + policy check, never a bypass. FR-TEN-004 integration tests are mandatory (CHK-010). |
| Soft delete | `DeletedAt`/`Status` per entity; `AuditLog` has no update or delete path (BR-015) |
| Optimistic concurrency | `RowVersion`/`xmin` concurrency token on `Task`, `Request`, `WorkflowVersion`, `SLAVersion`, `ApprovalInstance`, `Attachment` (GAP-025) |
| Immutability | Published `WorkflowVersion` / `SLAVersion` / `ApprovalRuleVersion` rows are never updated after publish — a change creates `VersionNo + 1` (BR-009/BR-010) |
| Approval | `ApprovalStepInstance` decisions are insert-only; no decision overwrite (BR-011) |
| Enum storage | Native PG enums for stable status sets; `VARCHAR` + check constraint for allowlisted extensible sets (notification types, AI features) |
| Indexes | Every FK; `(TenantId, Status, Deadline)` on Task; `(TenantId, Status, CreatedAt)` on Request; unique `(TenantId, EmployeeCode)`, `(TenantId, Email)`, `(TenantId, Code)` on Service/Department/TenantKey; unique `IdempotencyKey` |
| Polymorphic references | `Comment`, `Attachment`, `Confirmation`, `SLAState`, `SLAEvent` use `(ObjectType, ObjectId)` with **no FK** — integrity enforced in the application layer plus a consistency-check job |

### 4.3 Entities: baseline plus gap-resolving additions

The dictionary (Appendix C) defines **38** entities and is adopted verbatim. Four High-severity findings (C-05, C-06, C-07) are omissions in the physical model, not missing business capability, so they are closed by **four additive tables** that store configuration/runtime data the SSS already requires but never modelled. No existing entity, field, relationship or rule is changed.

| New table | Closes | Purpose |
|---|---|---|
| `TenantSetting` | C-07 | Key/value tenant policy store: AI auto-action enablement, AI confidence threshold, SLA pause policy, allowed attachment types, max upload size, report range limits, AI rate limits |
| `ManagementScope` | C-06 | Explicit, auditable manager assignable-scope: `(UserId, DepartmentId, IncludeDescendants)`; default-seeded to the manager's own department subtree |
| `SLAState` | C-05 | Per-object SLA runtime snapshot: pinned `SLAVersionId`, start/warning/target instants, paused total, status, escalation level, last evaluated |
| `SLAEvent` | C-05 | Append-only SLA timeline: STARTED / WARNING / PAUSED / RESUMED / OVERDUE / ESCALATED with unique idempotency key |

**Workflow runtime (C-04) is resolved without a new table.** `WorkflowInstance` does not exist in the dictionary and is not needed: the applied version is already pinned on the object via `WorkflowVersionId`, the transition is validated by the engine, and FR-WF-002's "record transition" + BR-020 audit coverage are satisfied by an append-only `AuditLog` entry (`Action = WORKFLOW.TRANSITION`, before/after state in `BeforeJson`/`AfterJson`). This avoids inventing an aggregate the SSS never asked for.

Resulting physical model: **38 baseline + 4 gap-resolving = 42 tables.**

### 4.4 Seed / demo data (§3.2 permits simulated data)

Two demo tenants (Company A, Company B) with distinct users, departments, services and workflows — required by Demo D (multi-tenant isolation). Each: 1 Company Admin, 2 Managers, 6 Employees, 3–4 departments, 3 services with routing, 1 published workflow per business type, 1 approval rule, 1 SLA profile with warning+escalation, 1 business calendar (Asia/Ho_Chi_Minh, Mon–Fri 08:00–17:30), permission catalog, and 22 business rules' worth of state coverage.

---

## 5. API Architecture

| Aspect | Convention |
|---|---|
| Base | `/api/v1`, REST/JSON, bearer access token (Appendix D) |
| Tenant | Derived from the authenticated session; never accepted as a client-supplied authority. Route/resource IDs are cross-checked against it. |
| Validation | FluentValidation-style DTO validators → `422`; domain rule failures → `409`/`422` with a stable `code` |
| Error envelope | `{ code, message, details, traceId, timestamp }` exactly as §20.2 |
| Idempotency | `Idempotency-Key` header required on finalize, confirmation, scheduler-driven notification and AI action endpoints (NFR-REL-001) |
| Concurrency | `If-Match`/`ETag` (RowVersion) on mutable aggregates; mismatch → `409` with current state (§29 concurrency row) |
| Pagination | `page`/`pageSize` (max 100), stable ordering by `(CreatedAt, Id)`; response carries `page`, `pageSize`, `total` |
| Filtering | Allowlisted fields only; all filters authorization-scoped; free text parameterised (FR-COL-004) |
| Rate limiting | ASP.NET Core rate limiter on `/auth/*` and `/ai/*`, per user + per tenant |
| Errors | 400/422 validation, 401 auth, 403 authz, 404 non-revealing (cross-tenant and non-existent are indistinguishable), 409 conflict, 429 rate, 502/503 external, 500 with traceId only |
| AI | AI endpoints are ordinary authenticated endpoints; `/ai/actions/execute` is internal-only and is never exposed to the browser |

The Canonical API Catalog is authoritative where it conflicts with inline §7 references (finding C-02). Enumerated directly, the catalogue defines **76** endpoints (Auth 4 · Tenant 5 · Org 7 · RBAC 3 · Service 5 · Workflow 6 · Approval 2 · Task 12 · Request 11 · SLA 5 · Collaboration 6 · Notification 2 · Reporting 4 · AI 3 · Audit 1); all 76 are implemented and none is dropped.

To that baseline, the FR-required additions are implemented (findings C-03 and C-13, decisions A-07 and A-14) — the FR text and Appendix E define qualifications the catalogue names only partially:

| Addition | Required by |
|---|---|
| `POST /api/v1/ai/task-assignment-recommendation` | FR-AI-002 |
| `POST /api/v1/ai/task-parameters` | FR-AI-003 |
| `POST /api/v1/ai/task-breakdown` | FR-AI-004 |
| `POST /api/v1/ai/task-risk` | FR-AI-005 |
| `POST /api/v1/ai/task-summary` | FR-AI-006 |
| `POST /api/v1/ai/request-multi-intent` | FR-AI-007 |
| `POST /api/v1/requests/{id}/split` | FR-AI-008 |
| `POST /api/v1/tasks/{id}/start` | FR-TASK-004 |
| `POST /api/v1/requests/{id}/process-actions` | FR-REQ-005 |
| `POST /api/v1/users/{id}/reset-password` | FR-AUTH-004 |
| `PUT /api/v1/services/{id}/workflow` | FR-SVC-003 |
| `PUT /api/v1/services/{id}/approval-rules` | FR-SVC-004 |
| `POST /api/v1/workflow-transitions` | FR-WF-002 (replaces `workflow-instances/{id}/transition`; decision A-05) |

Resulting surface: **89** endpoints, every one traceable to an FR in `06-requirement-traceability.md`.

---

## 6. Security Architecture (§19)

| Control | Implementation |
|---|---|
| Authentication | ASP.NET Core Identity password hashing; JWT access token + rotating refresh token persisted with revocation flag; login accepts employee code **or** company email (FR-AUTH-001/002) |
| Authorization | Permission-catalog-driven policies + resource/scope checks + manager scope; enforced server-side for every mutation — hiding a button is never the control (task §14) |
| Tenant isolation | Tenant context from token claims; global query filter; tenant assertion on write; cross-tenant attempts produce 404/403 **and** an audit security event |
| Input security | DTO validation, EF parameterisation (no string SQL), output encoding, file allowlist |
| File security | Signed upload session, size ≤ 500 MB enforced at session creation *and* finalize, tenant-prefixed object keys, content-type allowlist, malware-scan hook point, orphan-cleanup job |
| Secrets | Environment variables / user-secrets only — DB credentials, JWT signing key, object-storage keys, OpenAI API key, SMTP credentials. `.env.example` committed; `.env` git-ignored. **No secret in source, ever** |
| AI security | Data minimisation, tool whitelist with permission metadata, prompt-injection defence (retrieved business text is untrusted; system policy outranks it), server-side revalidation of every ID/enum/date/permission, per-tenant call/token budget |
| Transport | HTTPS outside local development |
| Audit | Append-only, tenant-scoped, queryable by A01/A02 only |

---

## 7. AI Subsystem Architecture (M12)

The AI Agent is a first-class module with **no privileged path**. All ten FR-AI features share one pipeline:

```
UI  →  POST /api/v1/ai/<feature>                      (authenticated, rate-limited)
        │
        ├─ 1. Resolve tenant + user + effective permissions        (ITenantContext, IResourceAuthorizer)
        ├─ 2. Retrieve ONLY permitted context (catalog, org, workload, object history)
        ├─ 3. Build minimal prompt + JSON schema / tool definitions
        ├─ 4. Call provider behind IAiModelClient (timeout, retry w/ backoff, budget)
        ├─ 5. Schema-validate structured output
        ├─ 6. Resolve every ID/enum/date against the tenant catalog   ← hallucination gate
        ├─ 7. Persist AIInteraction + AIRecommendation
        └─ 8. Return proposal to UI (never a mutation)
                     │
        Human reviews / edits / overrides  →  normal business endpoint performs the real operation
                     │
        (only if tenant policy permits AND the tool is allowlisted)
        POST /api/v1/ai/actions/execute  →  IToolHandler
                     ├─ re-authorize against current user + scope
                     ├─ revalidate state / workflow / SLA / business rules
                     ├─ require confirmation unless policy explicitly auto-allows
                     ├─ invoke the Application service (never a repository)
                     └─ persist AIAgentAction { AuthorizationResult, ExecutionStatus }
```

| Component | Responsibility |
|---|---|
| `IAiModelClient` | Wraps the OpenAI Responses API (.NET SDK); model name from configuration; returns raw structured payload + usage/latency |
| `IAiFeatureHandler<TRequest,TSuggestion>` | One implementation per AI feature; owns prompt construction, context retrieval limits and output validation for that feature |
| `IAiSuggestionValidator` | Generic + feature-specific validation: JSON schema, tenant entity existence, enum allowlist, date normalisation, confidence bounds |
| `IAiToolRegistry` / `IToolHandler` | Allowlisted tools with JSON schema, required permission code and `RequiresConfirmation` flag |
| `IAiBudgetGuard` | Per-tenant/per-user call + token limits; 429 on exhaustion |

**Guardrails enforced structurally, not by prompt:** no AI tool can write to `DbContext`; the tool registry is closed (only registered tools are callable); confidence is stored as advisory metadata and read by no authorization decision (BR-021); tenant-scoped retrieval means cross-tenant context is impossible to assemble.

---

## 8. Background Processing, Realtime and Observability

| Concern | Design |
|---|---|
| SLA monitoring (FR-SLA-002/003) | Hangfire recurring job, every minute: load active `SLAState` rows for non-suspended tenants, compute elapsed working minutes on the tenant calendar, emit WARNING once and OVERDUE/ESCALATED once per level via `IdempotencyKey` |
| Notification email | Hangfire queue with exponential-backoff retries; in-app notification remains the source of truth (§23) |
| Attachment cleanup | Recurring job removing expired upload sessions and orphaned objects |
| Realtime | SignalR hub authenticated with the bearer token; groups keyed by `tenant:{tenantId}` and `user:{userId}`; events for task/request status changes and notifications |
| Logging | Serilog structured logs with `CorrelationId`/`TraceId`, `TenantId`, `UserId`, `ObjectId` (never raw prompts or secrets) |
| Observability | OpenTelemetry traces/metrics; AI calls traced with feature, model, latency, tokens, status |
| Audit vs logs | `AuditLog` (business, queryable, immutable) is strictly separate from technical logs (§20.1) |

---

## 9. Deployment

`deploy/docker-compose.yml` runs PostgreSQL 18, MinIO and Redis 8. The API (`dotnet run`) and SPA (`ng serve`) run on the host for development; the same compose file plus built images supports the demo environment (§24, GAP-024). Configuration is entirely environment-driven; a committed `.env.example` documents every variable with no real secret values.
