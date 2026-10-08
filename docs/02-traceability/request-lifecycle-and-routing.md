# Request Lifecycle and Routing — Implementation Contract

Scope: FR-REQ-003, FR-REQ-004, FR-REQ-005, POST `/api/v1/requests/{id}/route`, POST `/api/v1/requests/{id}/receive`, POST `/api/v1/requests/{id}/start`, POST `/api/v1/requests/{id}/reject`, POST `/api/v1/requests/{id}/cancel`, SSS §9.2, ADR-0009.

- **Request Routing (Manager / Triage)**:
  - Endpoint: `POST /api/v1/requests/{id}/route`.
  - Input: `{ toDepartmentId: UUID, toUserId?: UUID, reason?: string }`.
  - Header: Optional `Idempotency-Key` (1–128 visible ASCII chars).
  - Permission: `requests.route` (Tenant scope; managers filtered by target department scope).
  - Preconditions: Active tenant actor, request in `SUBMITTED` or `ROUTED` state, target department active, target user (if provided) active and in target department.
  - Lifecycle Transition: `SUBMITTED` / `ROUTED` → `ROUTED`.
  - Persistence & History: Inserts immutable `RequestRouting` record (table 31 of 43), source `MANUAL`, serializes via row lock `SELECT FOR UPDATE` and transaction advisory lock on `REQUEST.ROUTE`.
  - Audit & Idempotency: Append-only `AuditLog` action `REQUEST.ROUTED`, verified SHA-256 idempotency key hash and payload fingerprint, unique index `UX_AuditLog_RequestRoutingReplay`.
  - Notification: In-app `Notification` of event `RequestUpdated` dispatched to the routed user (or department).

- **Request Intake Receipt (Assigned Department / User)**:
  - Endpoint: `POST /api/v1/requests/{id}/receive`.
  - Input: `{ note?: string }`.
  - Header: Optional `Idempotency-Key`.
  - Permission: `requests.receive`.
  - Preconditions: Active tenant actor, request in `ROUTED` state, actor matches routing target user or belongs to target department.
  - Lifecycle Transition: `ROUTED` → `RECEIVED`.
  - Confirmation Milestone: Records `Confirmation` milestone record with `ObjectType = "REQUEST"`, `MilestoneType = "RECEIVE"`, `Decision = "CONFIRMED"`.
  - Audit & Idempotency: Append-only `AuditLog` action `REQUEST.RECEIVED`, unique index `UX_AuditLog_RequestReceiptReplay`.
  - Notification: In-app `Notification` of event `RequestUpdated` dispatched to requester.

- **Request Execution Start (Assigned Actor)**:
  - Endpoint: `POST /api/v1/requests/{id}/start`.
  - Header: Optional `Idempotency-Key`.
  - Permission: `requests.process`.
  - Preconditions: Request in `RECEIVED`, `WAITING_FOR_INFORMATION`, `OVERDUE`, or `RESOLVED` (rework).
  - Lifecycle Transition: `RECEIVED` → `IN_PROGRESS` (optimistic locking on `UpdatedAt`).
  - Audit & Idempotency: Append-only `AuditLog` action `REQUEST.STARTED`, unique index `UX_AuditLog_RequestExecutionReplay`.

- **Request Rejection (Manager / Reviewer)**:
  - Endpoint: `POST /api/v1/requests/{id}/reject`.
  - Input: `{ reason: string }` (mandatory plain text).
  - Header: Optional `Idempotency-Key`.
  - Permission: `requests.reject`.
  - Preconditions: Request in `SUBMITTED` or `ROUTED` state.
  - Lifecycle Transition: `SUBMITTED` / `ROUTED` → `REJECTED`.
  - Audit: Append-only `AuditLog` action `REQUEST.REJECTED` preserving rejection reason.
  - Notification: In-app `Notification` dispatched to requester.

- **Request Cancellation (Requester / Admin)**:
  - Endpoint: `POST /api/v1/requests/{id}/cancel`.
  - Header: Optional `Idempotency-Key`.
  - Permission: `requests.cancel` (Self scope for requester, Tenant scope for Admin).
  - Preconditions: Request in any active non-terminal state (`DRAFT`, `SUBMITTED`, `ROUTED`, `RECEIVED`, `IN_PROGRESS`, etc.).
  - Lifecycle Transition: `*` → `CANCELLED`.
  - Audit: Append-only `AuditLog` action `REQUEST.CANCELLED`.

- **Detail Query Enrichment**:
  - `GET /api/v1/requests/{id}` enriched with `Routing` (`ToDepartmentId`, `ToDepartmentName`, `ToUserId`, `ToUserName`, `RoutedAt`, `Reason`, `Source`) and `RejectionReason` (from rejection audit).

- **Frontend Features**:
  - `RequestDetail` (`frontend/src/app/features/requests/request-detail.ts`, `request-detail.html`, `request-detail.scss`):
    - Action buttons conditionally enabled based on permissions and state machine:
      - "Route request" (visible in `SUBMITTED` / `ROUTED` with `requests.route`).
      - "Acknowledge receipt" (visible in `ROUTED` with `requests.receive`).
      - "Start work" (visible in `RECEIVED` with `requests.process`).
      - "Reject request" (visible in `SUBMITTED` / `ROUTED` with `requests.reject`).
      - "Cancel request" (visible in active states with `requests.cancel`).
    - Modals for routing (with department and user lookups), receipt note, and rejection reason.
    - Information cards displaying current routing recipient and department.
    - Rejection banner with formatted reason for rejected requests.

- **Verification Evidence**:
  - Unit tests: `RequestRoutingTests.cs`, `RequestLifecyclePolicyTests.cs`, `RequestConfirmationTests.cs` (319 passed).
  - Integration tests: `RequestRoutingAndLifecycleApiTests.cs` (280 passed against real PostgreSQL 18 Testcontainers).
  - Frontend unit tests: `request-detail.spec.ts` (167 passed).
  - Production build: Angular build clean with all lazy chunk files emitted.
  - Prettier formatting: `npm run format:check` clean.
