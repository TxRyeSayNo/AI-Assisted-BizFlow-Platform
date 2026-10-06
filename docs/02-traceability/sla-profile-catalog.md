# SLA profile catalog

Scope: API-SLA-01/02 and the metadata portion of FR-SLA-001 / UI-03 configuration. This increment lists and creates named DRAFT profiles only. It does not complete FR-SLA-001: targets, warnings, calendars, version creation, escalation rules, service binding and runtime clocks are still required.

## Contract and traceability

- Domain `SlaProfile` preserves Appendix C's four fields: SLAProfileId (server UUIDv7), TenantId, Name (1–200 characters) and Status (DRAFT/ACTIVE/INACTIVE). Creation trims the name and uses DRAFT. No extra business timestamp, uniqueness restriction or lifecycle state is invented.
- Application `SlaProfileCatalog` resolves live `sla.read` or `sla.configure` authority before accessing the store. These are separate TENANT-scoped permissions, seeded only into the existing COMPANY_ADMIN system role. Explicit custom-role grants work; matching an administrator role name is not authority. Platform accounts do not gain tenant configuration access.
- `GET /api/v1/sla-profiles` accepts page (default 1), pageSize (default 25, maximum 100), literal case-insensitive name search and canonical status. It returns `{items:[{slaProfileId,name,status}],page,pageSize,total}`. Both count and rows are tenant-filtered. Count and page are separate read-committed reads, not a transactional catalog snapshot.
- `POST /api/v1/sla-profiles` accepts only `{name}` and returns 201 with `{slaProfileId,name,status}`. Unknown fields—including tenant, target, calendar, status and version configuration—are rejected with 422 rather than silently discarded. Duplicate names are allowed by the approved dictionary. No item GET endpoint or Location URI is invented.
- The thin controller delegates to Application; Infrastructure saves the profile and actor-attributed `SLA.PROFILE_CREATED` AuditLog in one transaction. Failed audit persistence rolls back the profile. Responses use no-store and the existing safe error envelope.
- Migration `20261002125924_SlaProfileCatalog` adds the approved SLAProfile table, native status enum, tenant FK, name constraint, tenant/status/name index, xmin concurrency and immutable identity/tenant trigger. The application model is now 18 of the approved 43 tables. Application/EF do not yet permit profile mutation, activation or deletion. Empty rollback/reapply is supported; rollback refuses populated profiles or configured non-default grants.
- Settings → SLA profiles uses the GRID/Material foundation and explicit frontend grants. It supports loading/empty/error/denied states, paging, filters and named draft creation. Unknown mutation outcomes retain input and require retrieval before another attempt; no POST is automatically retried. Browser guards/hiding are usability only; backend authorization is mandatory.

## Owner-approved SLA version behavior

[ADR-0004](../01-decisions/ADR-0004-sla-snapshot-immutability.md) records the owner's approval: every saved SLA version is immutable from creation, and its referenced calendar is frozen. Changes require a new version and, when calendar configuration changes, a new calendar record. No SLAVersion publication status/timestamp fields are added. The later [snapshot persistence increment](sla-snapshot-foundation.md) implements these storage guarantees and approved calendar/escalation schemas; the profile catalog still exposes named draft metadata only.

## Validation and remaining work

Coverage is in `SlaProfileTests`, `SlaProfileCatalogTests`, Angular `sla-profiles.spec.ts`, the settings guard tests and real-browser `sla-profiles.spec.ts`. Backend coverage exercises real authorization, tenant filtering, unsupported inputs, audit rollback, persistence constraints and migration preservation. Browser cases use real Kestrel and disposable PostgreSQL, not fabricated catalog responses. Current run results are recorded in [implementation status](implementation-status.md).

Subsequent increments implement [authorized audited version creation](sla-version-creation.md) with serialized numbering and [read-only version history](sla-version-history.md). Next: default calendar administration and full configuration UI; service selection and version pinning; SLA runtime/pause/warning/overdue/escalation processing. No runtime or full SLA acceptance is implied by a named draft or stored snapshot.
