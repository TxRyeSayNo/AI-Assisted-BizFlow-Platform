# Request draft and revision persistence foundation

Scope: DEV-D5 prerequisite for FR-REQ-001/009, Appendix C Request, BR-001/008/010/015. This is not an exposed Request API or a complete Request workflow.

## Model and domain

`WorkRequest` maps to the approved `Request` table, preserving all eighteen specified business fields, required service/category references, nullable parent/revision links, nullable applied workflow/SLA versions and UTC lifecycle timestamps. UUIDv7 identity, native twelve-state and four-priority enums, xmin concurrency, foreign-key indexes and tenant/requester/status indexes are implementation details. This increment brought the model to 23 of the approved 43 application tables; subsequent [resolution history](request-resolution-history.md) brings it to 24. No convenience entity is added.

`CreateDraft` validates required identifiers, a trimmed 1–300 character title, nonblank plain-text description and known priority. Literal markup is data, not trusted HTML; consumers must render it with contextual escaping. A draft does not infer a service's runtime configuration or submit itself. `ReviseRejected` requires a rejected source, derives its tenant, creates a new independent DRAFT identity and stores RevisedFromRequestId. Edited fields are revalidated; runtime versions, lifecycle timestamps, resolution/evidence and parent linkage are not copied implicitly. The original object is never changed. Requester authority and safe optional evidence reuse still belong to the future Application use case.

## Persistence boundaries

EF default reads require authenticated tenant context, restrict rows to that tenant and exclude soft-deleted rows. Added drafts require persisted same-tenant requester/service/category/parent records and a persisted rejected revision source. Forged attached objects cannot stand in for persisted ownership. Existing-row edits, lifecycle transitions, configuration pinning and deletion are not enabled through EF by this prerequisite.

PostgreSQL independently checks requester/service tenancy, category ownership, parent/revision tenancy and valid revision sources. Parent references must point to already-existing records and remain immutable, preventing cycles; revision links and request identity also remain immutable. Applied versions, once non-null, cannot be changed or cleared. Workflow references must be published REQUEST versions in the same tenant; SLA references must belong to that tenant.

Every UPDATE to an already REJECTED row is rejected, including status reopening, description/title edits, soft deletion, timestamp-only and no-op updates. Hard deletion is denied for every Request. Migration rollback refuses nonempty Request history; an empty rollback/reapplication is supported. These integrity guards are not a workflow engine, a resource authorization layer or an audit boundary. Test-only SQL constructs historical source fixtures; it is not a production transition implementation.

## Remaining gates

- Authorized/audited Application orchestration, canonical APIs and GRID request creation/list/detail/revision UI.
- Request lifecycle implementation: the owner-approved rework and resume edges are recorded in [ADR-0009](../01-decisions/ADR-0009-request-transition-clarification.md). No Request transition policy or execution is implemented by this persistence prerequisite.
- Generic-request handling must reconcile FR-REQ-001's tenant-policy exception with required ServiceId/CategoryId; this explicit-service persistence path does not claim generic-request support.
- Workflow selection/publication, assignment/routing/resolution and confirmation records, task linkage, evidence selection, SLA runtime, notification delivery, AI constraints and end-to-end acceptance.

The complete backend suite passed 202 unit and 182 PostgreSQL integration tests, and EF reported no pending model changes. The explicit-priority test first reproduced LOW being replaced by the MEDIUM database default; setting the EF sentinel to MEDIUM corrected this without changing the approved enum or database default. All four priorities now round-trip unchanged. Migration checks verify empty rollback/reapplication and refusal to discard existing Request history.

Further validation results are recorded in [implementation status](implementation-status.md). Domain/persistence tests do not prove Request API authorization, transition correctness or user-facing feature completion.
