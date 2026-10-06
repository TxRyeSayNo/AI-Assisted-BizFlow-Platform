# Task creation and scoped detail — implementation contract

Approved before implementation under ADR-0012 / TD-01–04. Requirement scope: FR-TASK-001, API-TASK-02, UI-04/05, SSS §9.1, BR-001/002/020 and the existing ADR-0011 read policy. This contract does not remove the remaining assignment, workflow/SLA selection, Request-to-Task or lifecycle requirements.

## API

- `POST /api/v1/tasks`: live tenant `tasks.create`; initial supported use case creates an independent DRAFT with `title`, optional `description`, optional canonical uppercase `priority` (default MEDIUM), optional absolute `deadline`, and optional ordered `checklist` title strings. Tenant, creator, identity, state, order and timestamps are server-owned. No assignment is silently fabricated. Assignment and Request-linked creation remain their separate authorized use cases. Unknown input fields are rejected.
- Creation returns 201 with `{taskId,title,status,createdAt}` and Location `/api/v1/tasks/{id}`. Replays return the original logical creation result, even if a later authorized operation changes that Task. Clients reread detail for current state.
- Optional `Idempotency-Key` is a case-sensitive opaque key of 1–128 visible ASCII characters. Only its SHA-256 hash is retained. Its scope is authenticated tenant + actor + `TASK.CREATE`. Same key and normalized semantic input replay; differing input returns 409. Client property ordering and equivalent UTC offsets do not change intent. The UI generates one key per submission attempt and retains it while an outcome is uncertain. New keys deliberately represent new operations.
- `GET /api/v1/tasks/{id}` returns Task fields, creator display name, initial/current checklist, current assignment and paginated progress/result history. It exposes linked Request identifiers only, not linked Request contents. Checklist, report and result pages use independent positive page parameters and a shared pageSize (1–100, default 25), stable ordering and totals. Missing, foreign and authenticated out-of-scope resources return the same 404 envelope; unauthenticated calls retain 401.

## Application and persistence rules

1. Resolve current live authority, never client-supplied actor/tenant/role names. Creation requires `tasks.create`; detail uses the unchanged union from ADR-0011, with denial audit before non-revealing failure.
2. Normalize and validate title/description, priority, ordered checklist and explicit-offset deadline syntax. UTC timestamps use database microsecond precision. The request fingerprint is SHA-256 over a deterministic versioned representation of normalized input, excluding generated identifiers and server time.
3. For keyed creation, begin a database transaction and acquire a database-backed lock scoped to tenant/actor/operation/key hash. Look up immutable audit evidence only within that scope. Recheck live authorization after waiting for the lock. An identical replay is resolved before checking whether its original deadline has now passed; replay must not create new work or extend the deadline.
4. For new execution, compare the normalized supplied deadline with current server UTC now. Create Task, checklist and truthful TASK.CREATED audit in one transaction. The immutable audit contains the fingerprint and key hash plus the original creation snapshot used for replay. A unique partial database index is defense in depth. Audit failure must roll back every new business row. No automatic retry after an uncertain database commit.
5. No key means ordinary non-idempotent creation; the UI always supplies one. Current permission revocation or tenant suspension is enforced on replay too. Keys from another user, tenant or operation cannot retrieve or collide with this actor's result. No plaintext key, credential or authentication token is stored in audit.
6. An unpinned Task retains the exact canonical Domain graph, not an alternative workflow state machine. Creation alone changes no existing state. Published workflow/SLA snapshots and Request history remain untouched. Later lifecycle commands must use the Domain policy and their own authorization, evidence, audit and replay contracts.

## Verification before acceptance

The executable tests must implement [the TD acceptance cases](../../tests/task-decision-acceptance.md). Current implementation/results are recorded in [implementation status](implementation-status.md). The contract is not a claim that endpoints or UI are already available.

## Implementation links

- Domain: `WorkTask`, `TaskChecklistItem`, unchanged canonical `TaskLifecyclePolicy`, `AuditLog.TaskCreated`.
- Application: `TaskCreation`, `TaskCreationRules`, `TaskDetailQuery` and typed DTO/store ports. Controllers contain only binding/delegation/HTTP response handling.
- Persistence: `TaskCreationStore`, shared `TaskReadQueries.Visible`, `TaskDetailReader`, migration `20261006033038_TaskCreationReplayProtection`. No table or column added.
- API: `TasksController` POST and GET-by-id alongside the existing scoped list.
- UI: `/tasks/new` with `tasks.create` guard and `/tasks/:id` with the approved read union; GRID forms and paginated detail.
- Tests: `TaskCreationRulesTests`, `TaskCreationAuthorizationTests`, `TaskCreationAuditTests`, `TaskCreationApiTests` (partial `TaskListApiTests`), Angular creation/detail tests, real `task-create.spec.ts` and existing list regression.

Creation currently supports independent drafts. This is an incremental implementation, not removal of assignee-at-creation, workflow/SLA binding, Request-derived Tasks, assignment/acceptance/execution, progress/submission/review or notifications. Full platform completion remains unproven.
