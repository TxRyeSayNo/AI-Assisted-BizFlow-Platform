# Service metadata updates

Scope: canonical API-SVC-03, the service dictionary, BR-001/002/020, architecture mutable-aggregate concurrency and accepted [ADR-0008](../01-decisions/ADR-0008-service-update-permission.md). This does not implement routing or version binding.

## Contract and boundaries

`PUT /api/v1/services/{id}` requires live tenant-scoped `service.update`; `service.create` does not imply it. The reference-data migration grants update only to the COMPANY_ADMIN system role by default. Custom roles may receive the permission through existing role configuration. Role display names, platform authority and forged tenant headers do not bypass this boundary.

The replacement body contains required `code`, `name`, `active`, plus optional nullable `description`. Omitted description clears it. The active flag maps explicitly to ACTIVE/INACTIVE; no new status is introduced. Codes/names and plain text use the existing catalog validation. Unknown fields, including categories, tenant, status and configuration-version IDs, are rejected. The metadata endpoint cannot replace categories, move tenants, change identity or edit workflow/SLA bindings.

A strong current `If-Match` is mandatory. Missing/malformed tags return 422; stale tags return `409 SERVICE.VERSION_CONFLICT` with authorized current metadata, ETag and category codes. A duplicate tenant-local code returns `409 SERVICE.DUPLICATE_CODE`. Success is 200 with the current complete service projection and ETag header, with no-store caching.

The Application use case authorizes before storage and again after acquiring the tenant-owned parent lock. Foreign and missing resources share safe 404 responses with caller-scoped denial evidence. Metadata updates and category creation now share `IServiceMutationStore`, the same aggregate lock and parent xmin version. Exactly one concurrent mutation can consume a given version. All metadata validation completes before the Domain changes any field.

Metadata, UpdatedAt/xmin and actor-attributed `SERVICE.UPDATED` before/after audit commit atomically. A failed audit or unique constraint rolls back all fields and the parent version. Frozen-clock and no-op timestamp cases still advance xmin. Existing category IDs, creation timestamp and version bindings remain unchanged. Permission rollback refuses to discard configured non-default grants; no additional table or business column is created.

## GRID editor

An update-only user can reach Settings/catalog and edit service metadata without seeing creation/category-add controls. The editor preloads current metadata, shows what will be preserved and sends an explicit active flag on save; for a DRAFT record it explains that saving sets that selected status. All mutation handlers block overlapping submissions. Duplicate/validation errors retain input. Stale or uncertain outcomes block another mutation until a successful catalog reload and explicit reselection; current ETags from error responses are never silently adopted and PUT is never automatically retried. A confirmed save refilters/reloads the catalog by the saved code so filter counts and visible rows remain server-derived.

## Evidence and remaining work

The expanded full backend run passed 203 unit and 190 integration tests, including audit rollback, post-lock revocation, tenant/employee/platform/anonymous denial and permission-migration rollback. All 99 Angular tests, production build, source formatting and six supplemental mocked browser regressions pass; EF reports no pending model changes. All 32 real-API desktop/mobile browser tests passed, including metadata persistence, category preservation and employee edit-control exclusion. Desktop and 375px mobile editor/catalog screenshots were visually reviewed; browser overflow checks passed. Current results are maintained in [implementation status](implementation-status.md), not inferred from this contract.

Still required: routing, compatible workflow/approval binding, SLA/service selection and actual Request consumers. This is not full service-module acceptance or production readiness.
