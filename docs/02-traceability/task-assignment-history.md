# Task assignment history

Scope: DEV-D5 prerequisite for FR-TASK-002/003/009, BR-001/003/004/015, Appendix C TaskAssignment and accepted ADR-0010. This adds assignment storage and acceptance rules, not an exposed assignment/acceptance/reassignment API.

## Approved model and history

TaskAssignment retains the ten approved fields: TaskAssignmentId, TaskId, nullable DepartmentId/UserId, AssignedBy, AssignedAt, nullable AcceptedAt/RejectedAt/RejectionReason/EndedAt. It adds no tenant column or convenience entity; ownership derives from Task. The physical model now contains 29 of the approved 43 application tables.

The initial-record factory requires a task, assigning actor, at least one target and valid identifiers. Assignment time is UTC; receipt/end fields start empty. Department-only, user-only and explicitly combined targets preserve the approved nullable-field model. The assign/reassign use case must additionally enforce exact permissions, configured management scope, workflow preconditions and reason/audit requirements.

The database enforces one current assignment per Task (`EndedAt IS NULL`) and preserves the original task/target/actor/time metadata. Reassignment closes the old record then inserts a new one; it does not overwrite the old target. Accepted/rejected decisions are mutually exclusive and cannot be rewritten or cleared once recorded. Rejection requires a nonblank plain-text reason. Milestones cannot precede assignment or follow the end timestamp. Ended records are fully immutable, including no-op updates. Hard deletion/TRUNCATE and rollback with assignment history are refused; empty rollback/reapplication is supported.

## One-time department claim

The owner approved [ADR-0010](../01-decisions/ADR-0010-department-task-acceptance-claim.md) during implementation. For a department-only assignment, acceptance atomically fills initially-null UserId with an active same-tenant user whose current DepartmentId matches the original target. DepartmentId, AssignedBy, AssignedAt and assignment identity do not change. A claim without acceptance, a user outside the department/tenant, a second claim or a changed acceptance timestamp is rejected.

`TaskAssignment.Accept` validates the server-loaded assigned parent, undecided/current assignment, live recipient identity/membership and timestamp before changing either UserId or AcceptedAt. An explicitly assigned user can only accept their own assignment. This method does not mutate Task status or persist anything. Database acceptance also rechecks active user/membership under a row lock. Actual accepting-actor audit, confirmation and Task transition remain mandatory parts of the forthcoming Application transaction; existing-row EF mutation remains disabled until that orchestration exists.

## Persistence boundaries

EF reads inherit authenticated tenant and soft-delete scope from Task. New assignments validate persisted task/actor/target ownership and active user/department targets; tracked forgeries cannot establish ownership. New records on COMPLETED/CANCELLED tasks are denied. Existing EF assignment writes are not enabled merely because SQL has milestone integrity guards.

PostgreSQL locks the parent Task and target rows before insertion. A concurrent target deactivation cannot be overlooked using an older transaction snapshot. Initial metadata, live-parent requirements, target tenancy and receipt-state checks are enforced independently of EF. Target inactivity after assignment does not destroy history or prevent closing the original record. These storage safeguards do not replace management-scope authorization.

## Evidence and remaining work

Tests cover all initial target shapes, missing identifiers, tenant/anonymous/platform isolation, forged parents, inactive/foreign targets, terminal tasks, receipt reason/time consistency, immutable decisions and ended history, single-current-assignment races, concurrent department claims, accepted Domain claim behavior, rollback and lock-observed user/department deactivation at three PostgreSQL isolation levels. Execution results are recorded in [implementation status](implementation-status.md).

The subsequent [assignment command](task-assignment-command.md) adds initial/rejected assignment with live target management scope, canonical unpinned assignment transition, parent locking/xmin protection, atomic state/history/audit/in-app events and a GRID action; current evidence is recorded in implementation status. The [acceptance command](task-acceptance-command.md) now orchestrates receipt/claim, Task transition, Confirmation, audit and manager in-app notification; its executed evidence is recorded separately. The [start/resume command](task-execution-command.md) subsequently adds current-performer execution while preserving these receipts. Still required: reassignment of active work, rejection, progress/results, full workflow/SLA runtime, email delivery and end-to-end lifecycle acceptance. The separate [scoped Task-read slice](task-list.md) uses current assignment history for visibility. The specification's complete Task module remains unfinished.
