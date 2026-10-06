# Task acceptance — implementation contract

FR-TASK-003, API-TASK-05, BR-001/002/004/012/020, SSS §9.1 and ADR-0010.

- `POST /api/v1/tasks/{id}/accept` accepts `{ note?: string }`; unknown fields are rejected. The live `tasks.accept` tenant capability is configured for EMPLOYEE and MANAGER system roles, and never substitutes for the current assignment relationship. No role-name authorization.
- Only an active same-tenant direct assignee, or an active current member of an unclaimed department queue, may accept an undecided current assignment on an ASSIGNED Task. Other/foreign targets return non-revealing 404. Existing queue eligibility follows current membership, not department activation status.
- The default canonical ASSIGNED → ACCEPTED edge applies only without workflow/SLA pins. Pinned execution remains fail-closed until its runtime is implemented; configured critical milestone rules must not be bypassed.
- Acceptance claims a null UserId exactly once, retaining DepartmentId, assignment identity and original metadata. The approved Confirmation table records TASK/RECEIVE/CONFIRMED, actual ActorId, optional plain-text note and UTC time. Claim, receipt, state, confirmation, immutable audit and TaskAccepted notification to AssignedBy commit together under the Task row lock and active-member lock.
- Optional Idempotency-Key uses the existing visible-ASCII key contract, tenant+actor+TASK.ACCEPT namespace, versioned normalized TaskId/note fingerprint, immutable audit result, database serialization and unique index. Identical retries replay after current permission and recipient checks; changed input conflicts. A different operation/key cannot accept an already-decided assignment. No idempotency table.
- The UI must prevent duplicate submissions, freeze unknown-outcome payload/key for identical retries, and refresh detail after success or uncertain closure. Notification links remain subject to scoped Task reads.
- Required tests: direct and department acceptance; concurrent queue claims; identity/tenant/permission/membership denial; immutable origin/receipt/confirmation; exact edge and pinned denial; keyed replay/conflicts; audit failure rolls back all effects; manager notification and real desktop/mobile acceptance.

Department-queue rejection authority is awaiting an owner decision. Rejection and execution remain required, not removed from scope. This document is a contract, not completion evidence; see implementation status for executed validation.
