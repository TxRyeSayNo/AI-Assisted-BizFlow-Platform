# Task lifecycle policy foundation

Date: 2026-10-01. Domain-only prerequisite for the approved workflow engine. No task persistence, API or UI was implemented by this increment; subsequent [Task draft/checklist persistence](task-draft-foundation.md) adds the two approved storage tables. The task module is not complete.

## Authority and implemented rules

`TaskLifecyclePolicy` implements the edges of SSS §9.1 using exactly ten task states, with owner-confirmed precedence in ADR-0003. It does not introduce workflow-instance or other entities. `TaskTransitionDenial` describes technical validation failures, not additional task business states.

The policy uses explicit permission grants through `ResourcePolicy`, not actor/role-name equality. User-driven edges enforce active identity/tenant, matching resource tenant and the required `tasks.assign`, `tasks.accept`, `tasks.execute`, `tasks.submit`, `tasks.confirm` or `tasks.cancel` permission. Narrow permission scopes continue to be evaluated by the existing resource policy.

- DRAFT/REJECTED → ASSIGNED requires validated target evidence and a target department in configured management scope (FR-TASK-002, BR-003).
- ASSIGNED → ACCEPTED/REJECTED requires the assigned-target relationship; rejection needs a nonblank reason (FR-TASK-003).
- ACCEPTED/OVERDUE → IN_PROGRESS requires the assigned-target relationship (FR-TASK-004).
- IN_PROGRESS → SUBMITTED requires an authorized assigned performer and submitted-result evidence (FR-TASK-007).
- SUBMITTED → CONFIRMED requires the authorized reviewer, submitted result and accepted-result decision. SUBMITTED → IN_PROGRESS is reviewer-driven rework allowed by workflow (FR-TASK-008).
- CONFIRMED → COMPLETED is a separate manager/system step requiring submitted result and satisfied confirmation (BR-012/016).
- Nonterminal states may be cancelled with explicit permission, nonblank reason and workflow authorization. COMPLETED/CANCELLED have no outgoing transitions; cancellation does not reopen a task (FR-TASK-010).
- Only the system entry point can mark IN_PROGRESS/SUBMITTED → OVERDUE, with evidence that the deadline/SLA threshold was crossed. System callers cannot use other employee/manager edges (BR-018).
- Every allowed edge additionally requires the pinned/default workflow and all configured guards to permit it. Evidence defaults to false; callers cannot rely on an omitted precondition. Unknown enum values and unsupported edges fail closed.

## Owner-resolved specification conflict

FR-TASK-007 includes OVERDUE as a result-submission precondition, while §9.1 has no OVERDUE → SUBMITTED edge. FR-TASK-008 permits rejecting a result into REJECTED or IN_PROGRESS according to workflow, while §9.1 only lists the latter. On 2026-10-01 the owner chose exact §9.1 precedence: overdue work resumes to IN_PROGRESS before submission; submitted-result rejection uses IN_PROGRESS rework. Both excluded direct edges now return `InvalidTransition`, including when workflow evidence is otherwise permissive. Existing assignment rejection into REJECTED is preserved. See [accepted ADR-0003](../01-decisions/ADR-0003-task-transition-precedence.md). This resolves the graph conflict, not the remaining task integration work.

## Integration requirements and evidence limits

`TaskTransitionEvidence` is a server-resolved Domain input, **not** an HTTP or AI tool DTO. Application services must load authoritative assignment/reviewer, result, confirmation, target and pinned workflow facts. The future workflow engine must be the sole state mutator and must call this gate inside authorized, concurrency-protected use cases that persist lifecycle changes, immutable history, notifications/SLA effects and audit atomically where required. A caller-provided boolean must never stand in for those validations.

The separate system entry point is for trusted background/Application services after resolving the task tenant and its live eligibility, not a client-selected actor flag. AI tools must use the initiating user's ordinary authorized Application path and its human-confirmation/policy gates; they cannot call the system path to acquire authority.

`TaskLifecyclePolicyTests` checks all 100 state pairs for user and system contexts, exact permission mapping on each canonical user edge, active tenant/user and cross-tenant constraints, no role-name/wildcard authority, assignment scope, assigned/reviewer relationships, required reasons/results/confirmation, system-only overdue, unknown enum values and the owner-confirmed resume/rework paths. These are policy-unit tests, not proof of task workflow integration or TST-TASK-001…004 acceptance.

Still required: Service/Workflow dependencies, Task and related physical entities, Application transactions and audit, exact deadline/workflow/SLA validation, task endpoints/screens, assignment history, progress/result revisions, concurrency/idempotency, notifications and real API/browser tests. Task and Request remain distinct; no Request state or revision behavior is changed here.
