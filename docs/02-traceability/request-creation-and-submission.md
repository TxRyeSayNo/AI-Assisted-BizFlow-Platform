# Request Creation, Submission, and Scoped List/Detail — Implementation Contract

Scope: FR-REQ-001, FR-REQ-002, POST `/api/v1/requests`, POST `/api/v1/requests/{id}/submit`, GET `/api/v1/requests`, GET `/api/v1/requests/{id}`, UI-07, UI-08, BR-001/002/003/006/009, SSS §9.2, ADR-0003, ADR-0009.

- **Request Creation (requester)**:
  - Input: `{ serviceId: UUID, categoryId: UUID, title: string, description?: string, priority?: "LOW"|"MEDIUM"|"HIGH"|"CRITICAL", parentRequestId?: UUID, submitImmediately?: boolean }`.
  - Header: Optional `Idempotency-Key` (1–128 visible ASCII chars).
  - Permission: `requests.create` (Employee, Manager, Company Admin). If `submitImmediately` is true, also requires `requests.submit`.
  - Initial States:
    - Normal draft: `DRAFT` status.
    - Direct submission: `SUBMITTED` status.
  - Preconditions: Active tenant actor, active internal service, active category belonging to that service, canonical priority, optional active parent request.
  - Persistence & Concurrency: Transactional save via `IRequestCreationStore` with PostgreSQL advisory lock `pg_advisory_xact_lock(hashtextextended(tenant/actor/REQUEST.CREATE/keyHash))` and replay detection.
  - Audit & Idempotency: Append-only `AuditLog` action `REQUEST.CREATED`, verified SHA-256 idempotency key hash and request payload fingerprint, check constraint `CK_AuditLog_RequestReplay`, unique replay index `UX_AuditLog_RequestCreationReplay`.

- **Request Submission (requester)**:
  - Endpoint: `POST /api/v1/requests/{id}/submit`.
  - Header: Optional `Idempotency-Key`.
  - Permission: `requests.submit` (Self scope: requester ID matching current actor).
  - Preconditions: Active tenant actor, `DRAFT` request state, unpinned workflow/SLA.
  - Lifecycle Transition: `DRAFT` → `SUBMITTED` with optimistic locking on `UpdatedAt`.
  - Concurrency: `SELECT FOR UPDATE` row lock on `WorkRequest` and transaction advisory lock on `REQUEST.SUBMIT`.
  - Audit & Notification: Append-only `AuditLog` action `REQUEST.SUBMITTED`, unique index `UX_AuditLog_RequestSubmissionReplay`, in-app `Notification` of event `RequestSubmitted` published to requester.

- **Scoped Request List & Detail Queries**:
  - `GET /api/v1/requests`:
    - Evaluates caller grants (`requests.read.tenant` → Tenant, `requests.read.managed` → Managed departments, `requests.read.own` → Requester self).
    - Supports paging (`page`, `pageSize` 1–100), case-insensitive search on title/description, status filter (12 canonical states from ADR-0009), priority filter, and service filter.
    - Joins Service, Category, and Requester names.
  - `GET /api/v1/requests/{id}`:
    - Scoped access evaluation with non-revealing 404 (returns 404 rather than 403 when request exists in another scope or tenant, preventing ID enumeration).
    - Returns full details including service, category, requester profile, timestamps, and parent/revision request links.

- **Frontend Features**:
  - `Requests` (`frontend/src/app/features/requests/requests.ts`, `requests.html`, `requests.scss`):
    - Scoped request list with search, status filter (12 states), priority filter, and dynamic active services filter.
    - Status badges, priority indicators, accessible tabular layout, and pagination controls.
    - "New request" button guarded by `requests.create`.
  - `RequestCreate` (`frontend/src/app/features/requests/request-create.ts`, `request-create.html`):
    - Fetches active services and dynamically cascades active categories.
    - Validation on title, priority, and categories.
    - "Submit immediately" toggle for direct submission.
    - Retries and idempotency key handling across network outcomes.
    - Navigates to `/requests/:id` upon creation.
  - `RequestDetail` (`frontend/src/app/features/requests/request-detail.ts`, `request-detail.html`, `request-detail.scss`):
    - Displays full request properties, metadata, and description.
    - "Submit request" button enabled for draft requests when caller has `requests.submit`.
    - Non-revealing 404 error handling.
  - Routes registered in `frontend/src/app/app.routes.ts`: `/requests`, `/requests/new`, `/requests/:id`.
  - Navigation link added in `frontend/src/app/features/auth/workspace.ts`.

- **Verification Evidence**:
  - Unit tests: `RequestLifecyclePolicyTests.cs`, `RequestReadPolicyTests.cs`, `RequestCreationAuditTests.cs` (313 passed).
  - Integration tests: `RequestApiTests.cs` (276 passed against real PostgreSQL 18 Testcontainers).
  - Frontend unit tests: `requests.spec.ts`, `request-create.spec.ts`, `request-detail.spec.ts`, `app.routes.spec.ts` (163 passed).
  - Production build: Angular build clean with all lazy chunk files emitted.
  - Prettier formatting: `npm run format:check` clean.
