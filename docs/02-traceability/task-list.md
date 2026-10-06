# Scoped Task list

Traceability: API-TASK-01 (GET), FR-TASK-002 assigned-target visibility, SSS §5 / BR-001/002/003/006, UI-04, ADR-0010/0011. Development stage: DEV-D5 read slice, with DEV-D8 verification.

## Contract

`GET /api/v1/tasks` requires an active authenticated tenant account and at least one explicit Task-read grant. The Application query resolves current account, tenant, grants, department membership and management scope from storage on every request. Caller-supplied tenant or scope values never determine access. Denied application access is audited in the caller's scope.

| Permission | Catalog scope | Default system grants | Included Tasks |
|---|---|---|---|
| `tasks.read.tenant` | TENANT | COMPANY_ADMIN | All non-deleted Tasks in the current tenant |
| `tasks.read.managed` | DEPARTMENT | MANAGER | Current target department belongs to configured management scope |
| `tasks.read.own` | SELF | MANAGER, EMPLOYEE | Creator is the current user |
| `tasks.read.assigned` | ASSIGNED | MANAGER, EMPLOYEE | Current user target, or unclaimed queue in the user's current department |

`TaskReadPolicy` interprets these specific capabilities as a union; generic department membership is not substituted for management scope. For a department target, the assignment's explicit DepartmentId is used. For a user-only target, the targeted user's current department is resolved from tenant-filtered storage. When both IDs exist, the assignment's explicit department remains its department target. Configured descendant expansion is reused. Ended assignments provide no access. A queue with a UserId is claimed and does not grant access to other department members. Custom roles use exactly the same permission catalog, never their display names. Platform inspection is separate and is not implemented by this endpoint.

The infrastructure reader applies this union and the tenant/soft-delete predicates before **both count and pagination**. No client filtering substitutes for server authorization. The read projection contains Task identity/title/state/priority/deadline/creation/source/creator plus current target IDs and names, not user credentials, historical assignments, progress/result content or Request content. Missing/deleted target names have explicit UI fallbacks.

Queue membership is resolved from the current tenant-owned User.DepartmentId. Department inactivity prevents new assignment through write guards but does not erase existing queue visibility for an active member. Moving a user out of the department removes queue visibility on the next read. Read permissions are not assignment/acceptance permissions.

Query parameters:

- `page` (default 1), `pageSize` (default 25, maximum 100); invalid/overflowing offsets return the standard 422 envelope.
- `search`: trimmed literal case-insensitive title search, maximum 200 characters.
- `status`: one exact canonical uppercase Task state, including `IN_PROGRESS`.
- `priority`: `LOW`, `MEDIUM`, `HIGH`, `CRITICAL`.

Filters combine with AND after the access union. Results order by CreatedAt descending then TaskId ascending. Response: `{ items, page, pageSize, total }`, with no-store caching. Empty authorized scope returns an empty page, not a tenant-wide fallback.

## Implementation

Domain `TaskReadPolicy` → Application `TaskListQuery` / `ITaskListReader` → Infrastructure `TaskListReader` → thin API `TasksController` → lazy GRID `/tasks` screen and permission-aware workspace navigation.

Migration `20261005003438_ScopedTaskReadPermissions` adds four permission catalog entries and six approved role grants, no new table. Rollback refuses to discard non-default role grants. Application table count remains 29 of the approved 43.

The UI provides status/priority/title filters, responsibility and deadline display, paging and loading/empty/error/denied states. It cancels obsolete requests, preserves applied filters while paging and clears stale rows before reload. Text is escaped; dates display in browser local time. An elapsed deadline does not fabricate an OVERDUE state. The subsequent [creation/detail increment](task-creation-and-detail.md) adds a `tasks.create`-controlled New task link and real scoped detail links; no acceptance/edit action is presented before its use case exists.

## Validation and remaining gates

Coverage is in `TaskReadPolicyTests`, `TaskListQueryTests`, `TaskListApiTests`, `TaskReadMigrationTests`, Angular `tasks.spec.ts` and real Playwright `tasks.spec.ts` (desktop/mobile). The harness seeds disposable read fixtures; those fixtures do not prove production Task creation/assignment/lifecycle behavior. Current execution results are recorded in [implementation status](implementation-status.md).

For a focused real-browser rerun after the full suite, use the supported Node environment and run `dotnet run --project tests/BizFlow.BrowserHarness -- tasks.spec.ts` from the repository root. Omitting selectors retains the entire real suite; selectors are forwarded to Playwright as individual arguments, not a shell command. Both desktop and mobile projects remain selected by default.

The subsequent [assignment command](task-assignment-command.md) implements DRAFT/REJECTED assignment with configured management scope and atomic in-app notifications. Not complete: Request-linked or initially assigned creation, active reassignment/acceptance transactions, remaining lifecycle mutations, progress/result workflows, confirmations, SLA runtime, email/SignalR delivery and platform inspection. Independent drafts and scoped details are tracked separately above. The full Task module and TST-SEC-001 remain incomplete.
