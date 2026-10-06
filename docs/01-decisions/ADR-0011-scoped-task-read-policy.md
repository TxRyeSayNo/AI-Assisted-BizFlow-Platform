# ADR-0011: Scoped Task-read permissions

Status: Accepted by the project owner on 2026-10-04.

## Decision

The Task list combines explicitly granted read capabilities, always within the authenticated tenant:

- `tasks.read.tenant`: all tenant Tasks; default COMPANY_ADMIN grant.
- `tasks.read.managed`: Tasks whose current target department lies within configured management scope; default MANAGER grant.
- `tasks.read.own`: Tasks created by the current user; default MANAGER and EMPLOYEE grants.
- `tasks.read.assigned`: Tasks currently targeted to the user, or unclaimed department queues in the user's own department; default MANAGER and EMPLOYEE grants.

Granted capabilities form a union. Ended assignments are not current targets. After a department assignment is claimed under ADR-0010, it is not an unclaimed queue. Role display names confer no authority; custom-role capabilities are resolved through the same explicit grants. Tenant isolation and soft-delete filtering remain mandatory. Platform inspection will be handled separately, not by bypassing tenant filters in this list.

This approval resolves the read policy and default grants, not completion of its API/UI implementation. Mutations still need their own action permissions, scope, workflow validation and audit.

Implementation mapping: TENANT / DEPARTMENT / SELF / ASSIGNED catalog scopes respectively, interpreted by the Task-specific read policy. DEPARTMENT on `tasks.read.managed` requires configured management scope, not ordinary membership. Current explicit assignment DepartmentId identifies a department target; a user-only target resolves through that user's current tenant-owned department. See [Task list contract](../02-traceability/task-list.md) for API/UI and verification boundaries.
