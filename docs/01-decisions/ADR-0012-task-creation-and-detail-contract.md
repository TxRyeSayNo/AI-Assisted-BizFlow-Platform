# ADR-0012 — Approved Task creation, replay and detail decisions

Status: accepted by the owner on 2026-10-06 as TD-01 through TD-04, before implementation.

- **TD-01:** An unpinned Task uses exactly SSS §9.1 through the Domain state machine. A missing WorkflowVersionId never permits extra transitions or bypasses Domain rules. ADR-0003 remains authoritative.
- **TD-02:** A supplied creation deadline must be an absolute ISO datetime with explicit `Z` or UTC offset, normalized to UTC, and strictly later than server UTC now. Omission remains valid unless an applicable workflow/SLA/business policy requires a deadline.
- **TD-03:** Supported Task mutations accept optional `Idempotency-Key`. Immutable audit metadata stores a request fingerprint; identical retries replay the same logical result, changed-payload reuse conflicts, and database-backed concurrency prevents duplicate execution. No new table. Each implemented mutation must explicitly document its replay operation scope; this does not claim replay support for unimplemented endpoints.
- **TD-04:** Add `GET /api/v1/tasks/{id}` with the existing scoped Task-read capabilities and tenant isolation. Authenticated unauthorized, foreign and missing resources use non-revealing 404. Existing authentication rejection still applies before the use case.

The owner required API contracts, Application rules, tests and traceability to be aligned before implementation. Those artifacts are [Task creation/detail contract](../02-traceability/task-creation-and-detail.md), [test acceptance plan](../../tests/task-decision-acceptance.md), and the TD rows in [implementation traceability](../02-traceability/implementation-status.md). Accepted decisions are not proof of completed code or passing tests.
