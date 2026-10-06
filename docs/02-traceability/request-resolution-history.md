# Request resolution history

Scope: DEV-D5 persistence prerequisite for FR-REQ-007/008, Appendix C RequestResolution, BR-001/008/015/017 and ADR-0009. This does not expose a resolve/confirmation API or change Request state.

## Contract

The approved six fields map to `RequestResolution`: ResolutionId, RequestId, ResolverId, Content, RevisionNo and CreatedAt. No tenant column or convenience table is added; ownership follows the persisted Request. This brings the implemented model to 24 of the approved 43 application tables. Restrictive foreign keys protect Request and resolver references; the request/revision index supports history reads and the resolver FK is indexed. RevisionNo remains a positive integer with the approved default of 1; server-side revision allocation belongs to the forthcoming resolve use case.

The Domain factory validates identifiers, positive revision, nonblank plain text and UTC timestamps. Markup remains literal data for contextual escaping by future consumers. Rework creates another record rather than modifying old resolution content. It does not automatically transition or close the Request.

Default EF queries inherit authenticated tenant scope and soft-delete filtering from Request. Save guards require an existing visible IN_PROGRESS parent and same-tenant resolver; attached forged parent objects do not establish ownership. All existing-resolution edits and hard deletes are denied. These are persistence boundaries, not substitutes for live `requests.resolve` permission and participant checks.

PostgreSQL independently locks the parent before inserting evidence, requires IN_PROGRESS and a non-deleted Request, checks resolver tenancy, and validates content/revision. This ordering allows the future transaction to insert resolution evidence before the workflow engine sets RESOLVED. A concurrent parent change cannot be overlooked using an old snapshot. Every UPDATE, DELETE and TRUNCATE is rejected, including no-op updates. Prior resolutions remain intact after requester rework or later rejection. Empty migration rollback/reapplication is supported; rollback with resolution history is refused.

## Verification and remaining gates

Unit and PostgreSQL tests cover field validation, tenant/anonymous/platform read and write boundaries, forged ownership, raw-SQL reference checks, every nonprocessing parent state, retained evidence after rework, append-only protection, soft-deleted parents, migration rollback and lock-observed concurrency at READ COMMITTED, REPEATABLE READ and SERIALIZABLE. Final execution results are recorded in [implementation status](implementation-status.md).

The final full backend run passed 210 unit and 197 integration tests; EF reported no pending model changes. No frontend/public API changed, and no Request UI acceptance is claimed.

Still required: the authorized/audited resolve transaction, live assignment and workflow/evidence checks, server-allocated revision numbers, confirmation records, BR-017 closure, Request APIs/UI, runtime SLA/notifications and end-to-end acceptance. Test-only SQL builds lifecycle fixtures; it is not a production workflow engine.
