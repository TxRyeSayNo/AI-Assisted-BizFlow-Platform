# Dashboard & Operational Reporting — Implementation Contract

Scope: FR-REP-001, FR-REP-002, FR-REP-003, FR-REP-004, SSS §7.10, §20.1, §29 Appendix C, UI-16.
Endpoints:
- `GET /api/v1/dashboard/manager`
- `GET /api/v1/reports/company`
- `GET /api/v1/reports/workload`
- `GET /api/v1/reports/sla`

---

## 1. Architectural and Business Rules (SSS §7.10, §20.1, §29 Appendix C)

- **Manager Dashboard (FR-REP-001)**:
  - Purpose: Operational overview of tasks and requests within the tenant.
  - Query parameters: `departmentId` (Guid? optional), `from` (DateTimeOffset? optional), `to` (DateTimeOffset? optional).
  - Main flow: Aggregates total tasks, active tasks (`Assigned`, `Accepted`, `InProgress`, `Submitted`, `Confirmed`), completed tasks, overdue tasks (explicit `Overdue` or active with `Deadline < UtcNow`), and cancelled tasks.
  - Request aggregation: Total requests, pending requests (`Submitted`, `InProgress`, `Resolved`, `Confirmed`), closed requests, rejected requests, and cancelled requests.
  - Distributions: Status and priority breakdowns formatted in uppercase key mappings.
  - Department summaries: Active, completed, overdue, and request counts per department.
  - Authorization: Requires `reports.manager` permission (granted to `COMPANY_ADMIN` and `MANAGER`).

- **Company Report (FR-REP-002)**:
  - Purpose: Executive-level company operational report across the entire tenant.
  - Query parameters: `from` (DateTimeOffset? optional), `to` (DateTimeOffset? optional).
  - Main flow:
    - Task completion rate: `(totalCompleted / totalCreated) * 100`.
    - Average task turnaround time: Mean duration in hours from `CreatedAt` to `CompletedAt`.
    - Request resolution rate: `(totalClosed / totalSubmitted) * 100`.
    - Average request resolution time: Mean duration in hours from `CreatedAt` to `ClosedAt`.
    - Department performance metrics: Tasks assigned, completed, requests received, and closed.
    - Tenant audit activity count within the specified window.
  - Authorization: Requires `reports.company` permission (granted to `COMPANY_ADMIN`).

- **Workload Report (FR-REP-003)**:
  - Purpose: User and department workload allocation.
  - Query parameters: `departmentId` (Guid? optional), `from` (DateTimeOffset? optional), `to` (DateTimeOffset? optional).
  - Main flow:
    - Active workload strictly excludes `Cancelled` tasks.
    - User workload item view: Active tasks, completed tasks, overdue tasks, and total assigned tasks per active user.
    - Department capacity breakdown: Total active tasks, total members, and average tasks per member.
    - Range limit validation: Date range cannot exceed configurable maximum `report.max_range_days` = 366 days. Throws `ApplicationFault(FaultKind.Validation, "REPORT.RANGE_EXCEEDS_MAX")`.
  - Authorization: Requires `reports.workload` permission (granted to `COMPANY_ADMIN` and `MANAGER`).

- **SLA Performance Report (FR-REP-004)**:
  - Purpose: Compliance reporting against SLA targets.
  - Query parameters: `serviceId` (Guid? optional), `from` (DateTimeOffset? optional), `to` (DateTimeOffset? optional).
  - Main flow:
    - Overall SLA compliance rate: `(compliantCount / totalTracked) * 100`.
    - Breached requests: Explicitly rejected requests or active requests exceeding the 48-hour standard SLA threshold.
    - Average elapsed turnaround time for closed requests.
    - Service-by-service breakdown: Total requests, compliant count, breached count, and compliance rate percentage.
  - Authorization: Requires `reports.sla` permission (granted to `COMPANY_ADMIN` and `MANAGER`).

- **Multi-Tenant Boundaries**:
  - All queries strictly enforce `TenantId == Caller.TenantId`.
  - Task assignments joined through tenant tasks to ensure 100% tenant isolation.

- **Security & Permissions**:
  - Migration `20261008010000_DashboardAndReportingFoundation`:
    - `reports.manager` (`01a14000-0000-7000-8000-000000000008`): module `reports`, action `manager`, scope `TENANT`.
    - `reports.company` (`01a14000-0000-7000-8000-000000000009`): module `reports`, action `company`, scope `TENANT`.
    - `reports.workload` (`01a14000-0000-7000-8000-00000000000a`): module `reports`, action `workload`, scope `TENANT`.
    - `reports.sla` (`01a14000-0000-7000-8000-00000000000b`): module `reports`, action `sla`, scope `TENANT`.
  - Role assignments:
    - `COMPANY_ADMIN`: granted all four permissions.
    - `MANAGER`: granted `reports.manager`, `reports.workload`, and `reports.sla`.

---

## 2. API Contracts

### `GET /api/v1/dashboard/manager`
- **Query**: `departmentId` (UUID, optional), `from` (ISO 8601, optional), `to` (ISO 8601, optional).
- **Response `200 OK`**:
  ```json
  {
    "taskMetrics": {
      "totalTasks": 12,
      "activeTasks": 6,
      "completedTasks": 4,
      "overdueTasks": 1,
      "cancelledTasks": 1,
      "byStatus": { "IN_PROGRESS": 4, "COMPLETED": 4 },
      "byPriority": { "HIGH": 5, "MEDIUM": 7 }
    },
    "requestMetrics": {
      "totalRequests": 10,
      "pendingRequests": 5,
      "closedRequests": 3,
      "rejectedRequests": 1,
      "cancelledRequests": 1,
      "byStatus": { "SUBMITTED": 2, "CLOSED": 3 },
      "byPriority": { "HIGH": 4 }
    },
    "departmentSummaries": [
      {
        "departmentId": "019f7f8a-0000-7000-8000-000000000001",
        "departmentName": "Engineering",
        "activeTasks": 4,
        "completedTasks": 2,
        "overdueTasks": 1,
        "totalRequests": 5
      }
    ],
    "generatedAt": "2026-10-07T12:00:00Z"
  }
  ```

### `GET /api/v1/reports/company`
- **Query**: `from` (optional), `to` (optional).
- **Response `200 OK`**:
  ```json
  {
    "totalTasksCreated": 100,
    "totalTasksCompleted": 80,
    "taskCompletionRatePercent": 80.0,
    "averageTaskCompletionTimeHours": 12.5,
    "totalRequestsSubmitted": 50,
    "totalRequestsClosed": 45,
    "requestResolutionRatePercent": 90.0,
    "averageRequestResolutionTimeHours": 24.0,
    "departmentMetrics": [
      {
        "departmentId": "019f7f8a-0000-7000-8000-000000000001",
        "departmentName": "Engineering",
        "totalTasks": 60,
        "completedTasks": 50,
        "totalRequests": 30,
        "closedRequests": 28
      }
    ],
    "recentAuditActivityCount": 120,
    "generatedAt": "2026-10-07T12:00:00Z"
  }
  ```

### `GET /api/v1/reports/workload`
- **Query**: `departmentId` (optional), `from` (optional), `to` (optional).
- **Response `200 OK`**:
  ```json
  {
    "items": [
      {
        "userId": "019f7f8a-0000-7000-8000-000000000010",
        "fullName": "Alice Engineer",
        "employeeCode": "EMP001",
        "departmentId": "019f7f8a-0000-7000-8000-000000000001",
        "departmentName": "Engineering",
        "activeTasks": 5,
        "completedTasks": 10,
        "overdueTasks": 0,
        "totalAssigned": 15
      }
    ],
    "departmentBreakdown": [
      {
        "departmentId": "019f7f8a-0000-7000-8000-000000000001",
        "departmentName": "Engineering",
        "activeTasks": 5,
        "totalMembers": 3,
        "avgTasksPerMember": 1.7
      }
    ],
    "generatedAt": "2026-10-07T12:00:00Z"
  }
  ```

### `GET /api/v1/reports/sla`
- **Query**: `serviceId` (optional), `from` (optional), `to` (optional).
- **Response `200 OK`**:
  ```json
  {
    "totalTracked": 40,
    "compliantCount": 38,
    "breachedCount": 2,
    "complianceRatePercent": 95.0,
    "avgElapsedHours": 16.2,
    "serviceBreakdown": [
      {
        "serviceId": "019f7f8a-0000-7000-8000-000000000020",
        "serviceName": "Hardware Setup",
        "totalRequests": 20,
        "compliantRequests": 19,
        "breachedRequests": 1,
        "complianceRatePercent": 95.0
      }
    ],
    "generatedAt": "2026-10-07T12:00:00Z"
  }
  ```

---

## 3. Frontend Implementation (UI-16)

- **Route**: `/reports` guarded by `tenantGuard` and `permissionGuard` with any of `['reports.manager', 'reports.company', 'reports.workload', 'reports.sla']`.
- **Component**: `Reports` (`bf-reports`):
  - 4 interactive views: Manager Dashboard, Company Report, Workload Analysis, SLA Performance.
  - Dynamic tab visibility based on authenticated actor's permissions.
  - Interactive filter controls: Department dropdown, date range inputs, quick preset buttons (Last 7d, Last 30d, Reset).
  - Modern card KPI grid with status colors, completion rate gauges, and turnaround time metrics.
  - Responsive tables for department summaries, user workload distribution, and SLA service breakdowns.
  - Navigation integrated into `Workspace` home menu.

---

## 4. Verification Evidence

- **Backend Unit Tests**: 388 passed, 0 failed (`tests/BizFlow.UnitTests`).
- **Frontend Unit Tests**: 194 passed across 36 test files, 0 failed (`npm test -- --watch=false`).
- **Production Build**: `npm run build` completed with zero errors. Initial bundle: 345.05 kB.
