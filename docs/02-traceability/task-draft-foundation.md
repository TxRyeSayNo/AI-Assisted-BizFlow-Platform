# Task draft and initial checklist persistence

Scope: DEV-D5 prerequisite for FR-TASK-001, FR-REQ-006, Appendix C Task/TaskChecklistItem, BR-001/010/015 and the approved modular-monolith workflow boundary. No Task API/UI or lifecycle execution is exposed by this increment.

## Model and behavior

`WorkTask` maps the fifteen approved Task fields; `TaskChecklistItem` maps its seven approved fields. Task has its own four-priority enum and reuses the exact ten-state `TaskState` already covered by the canonical lifecycle policy. No service column, extra business state or WorkflowInstance is introduced. This increment brought the physical model to 26 of the approved 43 application tables; subsequent [progress/result history](task-evidence-history.md) brings it to 28.

The draft factory requires tenant/creator identifiers and a trimmed 1–300 character plain-text title; description, deadline and RequestId are optional. Whitespace-only description becomes null. Provided timestamps are normalized to UTC. All priority values, including zero-valued LOW, are preserved by EF; MEDIUM remains the database default. Deadline compliance with the selected workflow/SLA is an Application gate still to be implemented, not guessed by the draft factory.

Drafts have no assignment, pinned runtime versions or completion metadata. A source Request may have zero, one or multiple Tasks; source state, timestamps and xmin are unchanged by creating linked Tasks. RequestId is historical traceability, not a merged Request/Task lifecycle. Initial checklist entries have positive order and no completion metadata. Future completion must retain same-tenant completing-user and timestamp consistency.

## Storage boundaries

Authenticated tenant context filters Task reads; checklist queries inherit their parent's tenant and soft-delete visibility. Draft saves validate creator and optional persisted Request ownership. Added checklist items require a tenant-owned draft parent, either a validated newly added Task or a fresh persisted row. A forged tracked/detached parent is not authoritative. Existing Task/checklist edits, assignment, completion and deletion remain denied through EF until their authorized use cases are implemented.

The database checks creator/Request tenancy, title/control-character rules, published same-tenant TASK workflow selection and same-tenant SLA selection. It preserves Task identity, creator, source Request and creation time; applied version references cannot be replaced or cleared. FK and tenant/status/deadline indexes support the approved query patterns; Task uses xmin optimistic concurrency.

Checklist inserts lock their parent and require a live DRAFT, preventing stale initial-checklist insertion after concurrent assignment. Checklist identity/parent are immutable, text/order are constrained, and completion flag/user/time must be consistent with a same-tenant completing user. SQL integrity guards are not an exposed checklist completion command or a replacement for workflow authorization.

Hard deletion and TRUNCATE of Task/checklist history are denied. Empty migration rollback/reapplication is supported; nonempty history blocks rollback. No new permissions, roles, public routes or API payloads are added.

## Evidence and remaining gates

Tests cover independent drafts, initial checklists, every priority and database defaults, zero/multiple linked Tasks without Request changes, anonymous/platform/foreign-tenant boundaries, forged parent ownership, prohibited existing-row writes, raw-SQL reference/version integrity, checklist completion consistency, soft-delete filtering, protected rollback and lock-observed draft-state races at three PostgreSQL isolation levels. Final run results are recorded in [implementation status](implementation-status.md).

Still required: creation/update/assignment and progress/result Application use cases with exact permissions/scopes, workflow/SLA deadline validation and state execution, aggregate concurrency/audit, checklist completion commands, notifications, canonical Task and Request-to-Task APIs, GRID screens and end-to-end acceptance. Progress/result and assignment storage were added by subsequent history increments. The existing [Task lifecycle policy](task-lifecycle-foundation.md) is not yet wired into a runtime engine.
