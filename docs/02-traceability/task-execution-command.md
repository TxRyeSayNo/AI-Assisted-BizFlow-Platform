# Task start/resume — implementation contract

Scope: FR-TASK-004, its POST `/api/v1/tasks/{id}/start` addition, BR-001/002/004/020, SSS §9.1, ADR-0003/0012.

- Input is `{}`; actor, tenant, status, assignment and timestamps cannot be supplied by clients. Live `tasks.execute` is a tenant capability, default EMPLOYEE/MANAGER; the server separately requires the active current individual assignee and a preserved acceptance receipt. An unclaimed department queue cannot bypass acceptance.
- Only ACCEPTED → IN_PROGRESS and OVERDUE → IN_PROGRESS are permitted by the canonical policy. IN_PROGRESS → IN_PROGRESS is not a new edge: a retry replays its prior keyed result; an unrelated start attempt conflicts. Starting does not create a report/result, invent percent, change a deadline, clear overdue history, or complete work.
- Workflow/SLA-pinned Tasks fail closed until their runtime is implemented. Resume never silently resets SLA clocks or substitutes a default graph for pinned configuration.
- Task-row serialization and active-user locking, post-lock permission checks, Domain policy/sole workflow state mutation, xmin and persistence evidence guard the transaction. Only Task.Status/UpdatedAt and immutable TASK.STARTED audit change. Receipt/assignment origin and Confirmation remain intact. Optional realtime is not replaced with a new Notification event outside the approved catalog.
- Optional Idempotency-Key uses tenant+actor+TASK.START, normalized versioned TaskId fingerprint, immutable original `{taskId,assignmentId,status:"IN_PROGRESS",startedAt}` result, conflict on another payload/Task, advisory lock and unique index. Same-key replay still requires current permission and current assigned-user ownership, and must not start work again after a later state change.
- GRID detail offers Start work / Resume work only for the current individual target with the capability and corresponding state. Unknown outcomes keep the same key; closing or success refreshes authoritative detail. Backend authorization remains mandatory.
- Required evidence: both exact edges; all other edges denied; missing acceptance, ended/rejected/other-user/foreign targets, inactive actor and pinned configuration denied; identical/concurrent retries, changed Task/key conflicts, permission revocation, audit rollback, unchanged receipt/history; API/Angular/real desktop/mobile checks.

Progress, reports/results, assignment rejection/reassignment, workflow/SLA runtime and the rest of the Task lifecycle remain required. This contract is not a completion claim.
