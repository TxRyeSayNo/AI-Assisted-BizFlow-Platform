# Record Archival & Unified Search — Implementation Contract

Scope: FR-COL-003, FR-COL-004, SSS §7.9, §20.1, §29 Appendix C (Table 33 of 43 foundation).
Endpoints:
- `POST /api/v1/records/{type}/{id}/archive`
- `GET /api/v1/records/search`

---

## 1. Architectural and Business Rules (SSS §7.9, §20.1, §29 Appendix C)

- **Record Archival (FR-COL-003)**:
  - Supports archiving completed and terminal records (`type`: `task` or `request`).
  - Terminal state validation:
    - `Task`: Can only be archived if `Status` is `Completed` or `Cancelled`. Archiving active or pending tasks throws `400 Bad Request` (`RECORD.INVALID_STATE`).
    - `Request`: Can only be archived if `Status` is `Closed` or `Cancelled`. Archiving non-closed requests throws `400 Bad Request` (`RECORD.INVALID_STATE`).
  - Multi-tenant boundary: Target record must strictly belong to the caller's active tenant (`TenantId`). Cross-tenant access returns `404 Not Found`.
  - Soft-delete semantics:
    - Sets `DeletedAt = DateTimeOffset.UtcNow`.
    - Updates `UpdatedAt = DateTimeOffset.UtcNow`.
    - Preserves all workflow fields, checklists, confirmation decisions, attachments, and comments without data destruction.
  - Audit logging:
    - Emits structured `AuditLog` event with action `RECORD.ARCHIVED`.
    - Includes `recordType`, `recordId`, `terminalState`, and `archivedAt` timestamp.
  - EF Core & Database guards:
    - `BizFlowDbContext.SaveChangesAsync` explicitly allows updating `DeletedAt` and `UpdatedAt` on terminal tasks/requests without triggering workflow mutation guards.
    - PostgreSQL indexes `IX_Task_TenantId_DeletedAt` and `IX_Request_TenantId_DeletedAt` ensure instant soft-delete filtering.

- **Unified Search & Filter (FR-COL-004)**:
  - Query parameters:
    - `q` / query text: Case-insensitive search matching against `Title` and `Description`.
    - `type`: `ALL`, `TASK`, or `REQUEST`.
    - `status`: Case-insensitive status filter matching domain enum values.
    - `priority`: Case-insensitive priority filter matching domain enum values.
    - `from`: Optional ISO 8601 UTC date filter (`CreatedAt >= from`).
    - `to`: Optional ISO 8601 UTC date filter (`CreatedAt <= to`).
    - `includeArchived`: Boolean flag (default `false`). When `false`, excludes records where `DeletedAt != null`.
    - `page` (default 1) & `pageSize` (default 20, max 100).
  - Multi-tenant isolation:
    - Searches strictly within `TenantId == Caller.TenantId`.
  - Read query architecture:
    - `RecordSearchReader` executes database projection queries against `WorkTasks` and `WorkRequests`, ordering by `CreatedAt DESC`.
    - In-memory formatting of enum string values avoids EF Core Npgsql SQL translation issues.

- **Security & Authorization**:
  - Permissions seeded in migration `20261007230000_RecordArchivalAndSearchFoundation`:
    - `records.archive` (`01a14000-0000-7000-8000-000000000006`): Module `collaboration`, granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.
    - `records.search` (`01a14000-0000-7000-8000-000000000007`): Module `collaboration`, granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.

---

## 2. API Contracts

### `POST /api/v1/records/{type}/{id}/archive`
- **Parameters**: `type` (`task` | `request`), `id` (UUIDv7 record ID).
- **Responses**:
  - `200 OK`:
    ```json
    {
      "recordType": "TASK",
      "recordId": "019f7f8a-0000-7000-8000-000000000010",
      "terminalState": "COMPLETED",
      "archivedAt": "2026-10-07T14:30:00Z"
    }
    ```
  - `400 Bad Request`: `RECORD.INVALID_STATE` (if record is not in a terminal state).
  - `404 Not Found`: If record does not exist or belongs to another tenant.
  - `403 Forbidden`: If user lacks `records.archive` permission.

### `GET /api/v1/records/search`
- **Query parameters**: `q`, `type`, `status`, `priority`, `from`, `to`, `includeArchived`, `page`, `pageSize`.
- **Response `200 OK`**:
  ```json
  {
    "items": [
      {
        "id": "019f7f8a-0000-7000-8000-000000000010",
        "recordType": "TASK",
        "title": "Complete ISO 27001 Audit",
        "description": "Prepare deliverable evidence documents",
        "status": "COMPLETED",
        "priority": "HIGH",
        "creatorOrRequesterId": "019f7f8a-0000-7000-8000-000000000001",
        "creatorOrRequesterName": "Alice Auditor",
        "createdAt": "2026-10-07T10:00:00Z",
        "updatedAt": "2026-10-07T12:00:00Z",
        "deletedAt": null,
        "isArchived": false
      }
    ],
    "totalCount": 1,
    "page": 1,
    "pageSize": 20,
    "totalPages": 1
  }
  ```

---

## 3. Frontend Implementation

- **Route**: `/records/search` guarded by `tenantGuard` and `records.search` permission.
- **Component**: `RecordSearch` (`bf-record-search`):
  - Form controls for search keyword, record type dropdown (`All`, `Tasks`, `Requests`), status dropdown, priority dropdown, and `includeArchived` checkbox.
  - Responsive table rendering:
    - Type badge (TASK / REQUEST) with distinct color coding.
    - Status badge.
    - Priority badge.
    - Archived marker badge.
    - Formatted dates via `DatePipe`.
    - Direct action links navigating to `/tasks/{id}` or `/requests/{id}`.
  - Synchronous URL query parameter reflection (`q`, `type`, `status`, `priority`, `includeArchived`, `page`) for bookmarking and sharing.
- **Task & Request Details Integration**:
  - `TaskDetail`: Shows "Archive task" button when status is `COMPLETED` or `CANCELLED` and `DeletedAt == null`, guarded by `records.archive` permission.
  - `RequestDetail`: Shows "Archive request" button when status is `CLOSED` or `CANCELLED` and `DeletedAt == null`, guarded by `records.archive` permission.
- **Navigation**:
  - Global navigation in `Workspace` toolbar ("Search records").
  - Quick-search buttons on `Tasks` and `Requests` list pages.

---

## 4. Verification Evidence

- **Backend Unit Tests**: 381 passing in `tests/BizFlow.UnitTests` (100% green).
- **PostgreSQL 18 Integration Tests**: `RecordArchivalAndSearchApiTests` validating multi-tenant isolation, state guards, and query filtering.
- **Frontend Unit Tests**: 188 tests passing across 35 test files (100% green).
- **Production Build**: `npm run build` completed with zero errors. Initial bundle size: 330.56 kB.
