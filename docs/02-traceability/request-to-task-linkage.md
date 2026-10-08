# Request-to-Task Linkage & Orchestration — Implementation Contract

Scope: FR-REQ-006, SSS §9.2 / §9.3, ADR-0009.
Endpoints:
- `POST /api/v1/requests/{id}/tasks`
- `POST /api/v1/tasks` (with optional `requestId`)
- `GET /api/v1/requests/{id}/tasks`
- `GET /api/v1/requests/{id}` (enriched with `tasks: RequestLinkedTaskItemView[]`)

---

## 1. Architectural and Business Rules (ADR-0009 & SSS §9.2 / §9.3)

- **Request-to-Task Association (`POST /api/v1/requests/{id}/tasks` and `POST /api/v1/tasks`)**:
  - **Permission**: `tasks.create` (Manager / Supervisor scope).
  - **Preconditions**: Target request must exist in the caller's active tenant and must be active (cannot link to requests in `CANCELLED`, `CLOSED`, or `REJECTED` state).
  - **Foreign Key & DB Trigger Enforcement**:
    - PostgreSQL foreign key constraint `FK_Task_Request_RequestId` links `Task.RequestId` to `Request.Id`.
    - PostgreSQL trigger `bizflow_task_guard()` enforces that `Task.RequestId` belongs to the exact same `TenantId` as the task.
    - PostgreSQL trigger `bizflow_task_guard()` enforces that `Task.RequestId` is immutable once set and cannot be modified or unlinked on task update.
    - `BizFlowDbContext` enforces tenant-consistent reference validation on insert.
  - **Idempotency & Replay Protection**:
    - Optional `Idempotency-Key` header with SHA-256 fingerprinting incorporating `requestId`.
    - Idempotency replay returns the original `TaskCreatedView` with `requestId` and status intact.
  - **Audit Logging**:
    - Emits append-only `AuditLog` action `TASK.CREATED` with details capturing the linked `requestId`.

- **Linked Tasks Visibility (`GET /api/v1/requests/{id}` & `GET /api/v1/requests/{id}/tasks`)**:
  - **Permission**: `requests.read` for request detail; `tasks.read` for task listing.
  - **Scoped Querying**:
    - `GET /api/v1/requests/{id}` queries `WorkTasks` where `RequestId == id`, joining assignment history for current assignee display (`assignedUserName`, `assignedDepartmentName`), projecting into `RequestLinkedTaskItemView[]`.
    - `GET /api/v1/requests/{id}/tasks` supports paginated listing filtered by `filter.RequestId`.
  - **Bidirectional UI Navigation**:
    - In `RequestDetail`: A dedicated **Linked Tasks** section displays all associated tasks with title, status badge, priority indicator, assignee, deadline, and direct navigation links to `/tasks/:id`.
    - In `RequestDetail`: Header "+ Create task" button and inline action modal allow authorized users to spawn tasks directly from the request with title, description, priority, deadline, and checklist items.
    - In `TaskDetail`: Header property list displays clickable link back to the parent request: `<a routerLink="/requests/:requestId">`.
    - In `TaskCreate`: Supports query parameter `?requestId=:id` to pre-link new tasks when navigating from request flows.

---

## 2. API Contracts

### `POST /api/v1/requests/{id}/tasks`
- **Request Body**:
  ```json
  {
    "title": "Configure storage volume mount",
    "description": "Mount SAN storage volume to database instance",
    "priority": "HIGH",
    "deadline": "2026-10-15T12:00:00Z",
    "checklist": [
      "Check SAN connectivity",
      "Format XFS filesystem",
      "Mount to /var/lib/data"
    ]
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "taskId": "01a13000-0000-7000-8000-000000000001",
    "title": "Configure storage volume mount",
    "status": "DRAFT",
    "priority": "HIGH",
    "deadline": "2026-10-15T12:00:00Z",
    "createdAt": "2026-10-07T11:00:00Z",
    "requestId": "01a12000-0000-7000-8000-000000000055"
  }
  ```

### `GET /api/v1/requests/{id}/tasks`
- **Query Parameters**: `page=1&pageSize=25`
- **Response `200 OK`**:
  ```json
  {
    "items": [
      {
        "taskId": "01a13000-0000-7000-8000-000000000001",
        "title": "Configure storage volume mount",
        "status": "DRAFT",
        "priority": "HIGH",
        "deadline": "2026-10-15T12:00:00Z",
        "createdAt": "2026-10-07T11:00:00Z",
        "assignedTo": null,
        "isOverdue": false
      }
    ],
    "page": 1,
    "pageSize": 25,
    "totalCount": 1
  }
  ```

### `GET /api/v1/requests/{id}` (Enriched Response)
- **Response `200 OK`**: Includes `tasks: RequestLinkedTaskItemView[]`:
  ```json
  {
    "requestId": "01a12000-0000-7000-8000-000000000055",
    "title": "Server Migration Plan",
    "status": "IN_PROGRESS",
    "priority": "HIGH",
    "tasks": [
      {
        "taskId": "01a13000-0000-7000-8000-000000000001",
        "title": "Configure storage volume mount",
        "status": "DRAFT",
        "priority": "HIGH",
        "deadline": "2026-10-15T12:00:00Z",
        "createdAt": "2026-10-07T11:00:00Z",
        "assignedUserId": null,
        "assignedUserName": null,
        "assignedDepartmentId": null,
        "assignedDepartmentName": null
      }
    ]
  }
  ```

---

## 3. Verification & Evidence

1. **Backend Unit Tests**:
   - `tests/BizFlow.UnitTests/Tasks/TaskCreationRequestLinkTests.cs`:
     - `Creates_task_with_valid_request_link_and_persists_in_audit_and_result`
     - `Throws_when_request_is_inactive_or_not_found`
     - `Replays_idempotent_creation_with_request_id`
   - Result: **All 327 backend unit tests passing**.

2. **PostgreSQL 18 Integration Tests**:
   - `tests/BizFlow.IntegrationTests/Persistence/RequestTaskLinkageApiTests.cs`:
     - `Request_to_task_creation_listing_and_detail_linkage_flow`: Creates request, verifies linked task creation via `POST /api/v1/requests/{id}/tasks`, verifies task listing via `GET /api/v1/requests/{id}/tasks`, and verifies enriched request detail via `GET /api/v1/requests/{id}`.
     - `Creating_task_linked_to_nonexistent_request_fails`: Tests non-existent and cross-tenant failure handling.
   - Result: **Passed against real PostgreSQL 18 container**.

3. **Frontend Unit Tests**:
   - `frontend/src/app/features/requests/request-detail.spec.ts`:
     - `renders linked tasks in the request detail view`
     - `allows creating a linked task from request and refreshes details`
     - `prevents task creation when request is in inactive status (e.g., CANCELLED or CLOSED)`
   - Result: **All 174 Angular tests passing** (32 test files). Production build cleanly compiles under Angular SCSS budgets (7.42 kB, well under 8.00 kB maximum error budget).
