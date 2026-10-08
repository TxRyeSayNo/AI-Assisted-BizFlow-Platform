# Request Resolution, Requester Confirmation, Rework, and Revision — Implementation Contract

Scope: FR-REQ-007, FR-REQ-008, FR-REQ-009, SSS §9.2, ADR-0009, BR-017. Endpoints: POST `/api/v1/requests/{id}/resolve`, POST `/api/v1/requests/{id}/confirm`, POST `/api/v1/requests/{id}/revise`.

---

## 1. Architectural and Business Rules (ADR-0009 & SSS §9.2)

- **Request Resolution (`POST /api/v1/requests/{id}/resolve`)**:
  - **Permission**: `requests.resolve` (Designated resolver or assigned department member).
  - **Preconditions**: Request must be in active processing state `IN_PROGRESS`.
  - **Persistence & Trigger Compatibility**: Inserts immutable `RequestResolution` (table 24 of 43) while database status is `IN_PROGRESS` to satisfy PostgreSQL trigger `bizflow_request_resolution_guard()`.
  - **Revision Tracking**: Automatic revision number allocation (`MAX(RevisionNo) + 1` for the request).
  - **State Transition**: `IN_PROGRESS` → `RESOLVED`, sets `ResolvedAt` timestamp.
  - **Audit & Replay Protection**: Records append-only `AuditLog` action `REQUEST.RESOLVED` with payload SHA-256 idempotency hash, guarded by unique index `UX_AuditLog_RequestResolutionReplay`.
  - **Notification**: In-app `Notification` dispatched to requester alerting that resolution has been submitted for review.

- **Requester Confirmation & Closure (`POST /api/v1/requests/{id}/confirm`)**:
  - **Permission**: Requester (`SELF` scope) or tenant supervisor with `requests.confirm`.
  - **Preconditions**: Request must be in `RESOLVED` state.
  - **Decision: `CONFIRMED`**:
    - Milestone confirmation record created: `Confirmation` with `ObjectType = "REQUEST"`, `MilestoneType = "RESOLUTION"`, `Decision = "CONFIRMED"`, optional satisfaction/feedback note.
    - Two-stage state transition: `RESOLVED` → `CONFIRMED` → `CLOSED` per ADR-0009 and BR-017. Both transitions validated against concurrency guards and audit logs recorded (`REQUEST.CONFIRMED` and `REQUEST.CLOSED`). Sets `ClosedAt` timestamp.
    - Dispatches in-app `Notification` to resolver/department indicating acceptance and final closure.
  - **Decision: `REWORK`**:
    - Note is mandatory detailing why resolution did not satisfy requirements.
    - Milestone confirmation record created: `Confirmation` with `ObjectType = "REQUEST"`, `MilestoneType = "RESOLUTION"`, `Decision = "REJECTED"`, preserving rework feedback.
    - State transition: `RESOLVED` → `IN_PROGRESS`.
    - Dispatches in-app `Notification` to resolver/department indicating rework has been requested.
    - Subsequent resolution increments `RevisionNo` to 2+ while preserving full history of prior resolutions and rework feedback.

- **Request Revision (`POST /api/v1/requests/{id}/revise`)**:
  - **Permission**: `requests.submit` / `requests.create` (Requester `SELF` scope).
  - **Preconditions**: Source request must be in `REJECTED` state.
  - **Lineage Preservation**: Creates a new request inheriting `ServiceId`, `CategoryId`, and requester tenancy, with `RevisedFromRequestId` explicitly pointing to the rejected source request.
  - **Immediate Submission**: Optional `SubmitImmediately: true` transitions new request immediately from `DRAFT` to `SUBMITTED`.
  - **Audit**: Emits `AuditLog` action `REQUEST.REVISED` linking source and new request IDs.

---

## 2. API Contracts

### `POST /api/v1/requests/{id}/resolve`
- **Request Body**:
  ```json
  {
    "content": "Detailed resolution report or deliverable description"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000001",
    "resolutionId": "01a12000-0000-7000-8000-000000000010",
    "revisionNo": 1,
    "status": "RESOLVED",
    "resolvedAt": "2026-10-07T10:30:00Z"
  }
  ```

### `POST /api/v1/requests/{id}/confirm`
- **Request Body**:
  ```json
  {
    "decision": "CONFIRMED", // or "REWORK"
    "note": "Optional satisfaction feedback or mandatory rework reasons"
  }
  ```
- **Response `200 OK`**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000001",
    "confirmationId": "01a12000-0000-7000-8000-000000000020",
    "decision": "CONFIRMED",
    "status": "CLOSED", // or "IN_PROGRESS" on rework
    "confirmedAt": "2026-10-07T10:35:00Z"
  }
  ```

### `POST /api/v1/requests/{id}/revise`
- **Request Body**:
  ```json
  {
    "title": "Revised: Hardware Provisioning",
    "description": "Updated requirements with complete specifications",
    "priority": "HIGH",
    "submitImmediately": true
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000099",
    "sourceRequestId": "01a12000-0000-7000-8000-000000000001",
    "title": "Revised: Hardware Provisioning",
    "status": "SUBMITTED",
    "createdAt": "2026-10-07T10:40:00Z"
  }
  ```

---

## 3. Frontend Implementation

- **Component**: `RequestDetail` (`frontend/src/app/features/requests/request-detail.ts`, `request-detail.html`, `request-detail.scss`)
- **Interactive Workflows**:
  - **Resolution Dialog**: Allows resolvers to record technical resolution report with markdown preview and submit.
  - **Review & Confirmation Dialog**: Radio-toggle group between "Confirm & Accept" (green highlight) and "Request Rework" (orange warning). Enforces mandatory rework reason when rework is selected.
  - **Revise Dialog**: Prefills title with `"Revised: {original title}"`, allows updating description and priority, and toggle to immediately submit.
  - **Resolution History Timeline**: Renders all historical revisions with `RevisionNo`, resolver identity, UTC timestamp, and formatted deliverable content.
  - **Requester Reviews & Decisions Timeline**: Displays all milestone confirmations and rework feedbacks with color-coded badges and actor names.
- **Budget Compliance**: Cleaned component styles to remain strictly within Angular's style budget limits.

---

## 4. Verification and Test Traceability

- **Unit Tests**:
  - `RequestResolutionAndConfirmationCommandTests.cs`: Resolution allocation, requester self-scope validation, sequential closure transitions, rework transitions, revision from rejected requests. All 324 backend unit tests pass.
  - `request-detail.spec.ts`: Resolution flow, confirmation & closure flow, rework flow with validation guard, revision flow, and timeline rendering. All 171 frontend unit tests pass.
- **Integration Tests (PostgreSQL 18)**:
  - `Request_resolution_confirmation_and_closure_flow`: Complete end-to-end resolution, confirmation, and closure with audit and notification assertions.
  - `Request_resolution_rework_flow_preserves_history`: Full rework cycle (`IN_PROGRESS` → `RESOLVED` → `IN_PROGRESS` → `RESOLVED` → `CLOSED`), asserting `RevisionNo = 2` and multiple resolution/confirmation records.
  - `Request_revision_from_rejected_creates_new_linked_request`: Revision from rejected parent, asserting `RevisedFromRequestId` linkage, new submission audit, and requester scoping.
