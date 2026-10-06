# Workflow persistence foundation

Scope: DEV-D5 prerequisite for FR-WF-001/002 and Appendix C's Workflow, WorkflowVersion, WorkflowStep and WorkflowTransition. This records the persistence increment; the subsequent [workflow library](workflow-library.md) adds initial draft creation and read APIs/UI. Neither increment completes workflow authoring or execution.

## Implemented boundaries

- Four approved entities and physical tables; no WorkflowInstance or convenience table. The implemented application model now contains 15 tables toward the approved 43-table target (including ADR-0002).
- Draft factories validate identifiers, field lengths, positive version/order numbers, defined enum values, step-code allowlisting and JSON object structure. Native PostgreSQL enums, foreign keys, unique version numbers, unique per-version step codes/order, JSON checks and row-version concurrency protect storage.
- Workflow owns TenantId. Version, step and transition query filters derive tenancy through the approved parent relationships. Anonymous and platform-plane contexts do not implicitly receive tenant configuration access.
- EF writes validate fresh parent ownership/status, reject reparenting and deny published configuration edits. Attached parent objects cannot replace persisted ownership evidence; parent IDs are concurrency predicates on detached updates. These guards supplement, not replace, the Application layer's future `workflows.configure` authorization.
- Database triggers prohibit edits/deletion of published/retired versions and insertion, editing or deletion of their children. Child writes lock the parent version so a write waiting behind publication rechecks its committed status. Truncation is rejected. Rollback refuses to drop populated workflow configuration.
- EF currently permits only draft saves and prohibits hard deletion. There is no EF publishing path. Direct SQL publication in tests is disposable fixture setup, not a supported production use case.

## Traceability and remaining gates

| Requirement | Current evidence | Still required |
|---|---|---|
| Appendix C workflow definitions | Domain factories, EF mappings, migration `20261001090500_WorkflowPersistenceFoundation` | Typed semantic configuration schemas and full authoring operations |
| FR-WF-001 immutable published versions | PostgreSQL triggers, fresh-status EF guard, stale-write/concurrency tests | Authorized transactional publish service, graph entry/exit/reference checks, invalid-cycle/edge checks, audit, service selection, API and UI |
| BR-001/002 tenant isolation | Four filtered entity sets, fresh parent resolution, ownership/concurrency protections | All forthcoming public workflow endpoints and final TST-SEC-001 audit |
| FR-WF-002 execution | No runtime implementation in this increment | Canonical Task/Request transition integration, guard evaluation, transaction/transition log, side effects and human confirmation where required |

JSON validation currently proves only an object-shaped storage value. It does not validate a guard language, approval configuration, executable graph or permission. Drafts are not selectable/executable workflows. No arbitrary JSON evaluator or AI database writer is introduced.

Published rows are completely immutable; no retirement endpoint or lifecycle mutation is exposed. Version retirement behavior must be reconciled with the immutable-version contract before implementing such an operation. The owner-approved exact task state graph remains governed by ADR-0003.

## Test scope

`WorkflowDefinitionTests` checks draft fields and structural rejection. `WorkflowPersistenceTests` uses PostgreSQL 18 to check tenant-filtered graph reads, anonymous/platform rejection, forged attached parents and detached children, unique/check constraints, draft editing, stale published edits, direct-SQL immutability and a lock-observed publication race. `WorkflowMigrationTests` independently checks empty rollback/reapply, populated rollback refusal and retention of data/history/triggers.

Run results and regression evidence are recorded in [implementation status](implementation-status.md). No new public API or frontend changed in this increment. Workflow authoring, publication, runtime execution, audit integration and user-visible acceptance remain incomplete.
