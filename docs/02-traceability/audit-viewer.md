# Scoped audit viewer

Date: 2026-10-01. The normal approval path recovered after an initial usage-limit rejection. The scoped read slice now has passing backend, Angular and real-browser checks recorded below. Full-system audit coverage remains incomplete until every required business mutation is implemented and audited.

## Requirement and scope

Canonical API-AUD-01, UI-15, NFR-DATA-001, SSS §20.1, Appendix C AuditLog and architecture §6. The existing append-only audit model and write protections are retained; no business states or tables are added.

- Tenant-plane calls require live `audit.read` TENANT authority and read only the current tenant's events.
- Platform-plane calls require live `platform.audit.read` PLATFORM authority and read only NULL-TenantId events, matching `/platform/audit`'s approved platform-level scope. This is not a cross-company inspection interface.
- The reference-only migration seeds the tenant permission to COMPANY_ADMIN and the platform permission to PLATFORM_ADMIN. No role name itself grants authority. MANAGER and EMPLOYEE receive no tenant-wide audit grant by default; explicit custom-role delegation uses the existing permission configuration boundary. Own-resource/department audit views are not introduced.
- Query scope is derived exclusively from authenticated context. Caller-supplied tenant/scope headers or query values cannot select another stream. The platform query's local `IgnoreQueryFilters` is immediately restricted to `TenantId IS NULL`; normal tenant filtering remains enabled elsewhere.

## GET `/api/v1/audit-logs`

Optional filters: `page` (default 1), `pageSize` (default 25, max 100), `actorId` UUID, `actorType` USER/SYSTEM/AI_AGENT, exact `action` (max 100), exact `objectType` (max 60), `objectId` UUID, `from` inclusive and `until` exclusive. Time bounds must be ISO timestamps with explicit offsets and are normalized to UTC. A supplied start must precede a supplied end. Empty GUIDs, invalid enums, oversized values and invalid/overflowing pages are rejected.

Response: `{ items, page, pageSize, total }`. Rows contain auditLogId, actorType, actorId, action, objectType, objectId, before, after, metadata and createdAt. Nullable system attribution remains nullable; human attribution is never invented. JSON is the existing writer-selected audit data, not a serialization of live User entities or technical logs. Existing writers exclude credentials. Future mutation writers must maintain that allowlisting rule.

Records sort newest `(CreatedAt, AuditLogId)` first. Count and page are separate read-committed queries; concurrent events can change later pages. No frozen paging snapshot is promised. All responses are no-store. The controller delegates to Application authorization/validation and never accesses DbContext. There is no audit update/delete API.

## GRID UI

`/audit` and `/platform/audit` provide the corresponding permission-gated views, reachable from the appropriate workspace landing page. Filters cover actor, action, object and UTC dates. The UI's inclusive “through date” is sent as the next UTC midnight in the exclusive `until` API field. Applied filters are retained during paging; stale requests are cancelled and old data is cleared on failure/denial.

The read-only detail drawer uses Angular Material's modal dialog, keyboard dismissal and focus restoration, displaying before/after/metadata as escaped JSON text. Actor/object IDs are retained as historical references without inventing current names. Desktop/mobile browser tests exercise these interactions; final visual evidence is recorded in implementation status.

## Validation evidence and remaining checks

1. Full backend suite: 92 unit and 108 integration tests passed, including authorization/date-boundary cases, PostgreSQL/API scope and revocation cases, and permission migration rollback/reapply.
2. EF built successfully and reported no pending model changes after the reference migration.
3. Angular production build passed (321.34 kB initial bundle); all 55 Angular tests and source formatting passed. A Material dialog mock initially resolved through the wrong injector; its explicit component-level test override corrected the unit test without changing production dialog behavior.
4. The final real desktop/mobile run passed all 22 flows. New audit flows create real role changes, inspect before/after evidence and verify separate platform history and Escape/focus behavior. The harness removed its disposable PostgreSQL container after shutdown.
5. Initial screenshots caught dialog transition frames. Capture now waits for full opacity/removal, uses viewport screenshots for the modal, and verifies the last metadata content and close button are reachable. Final desktop/mobile screenshots were inspected; a wrapped mobile View label was corrected and verified with visible-label/button/cell geometry rather than Material's decorative overflow. No horizontal page overflow was found. The final CSS fix passed production build, Angular tests and formatting again.
6. All six supplemental mocked-browser regressions passed. Packaged-image rebuilding and hosted CI remain separate, unverified gates. See [implementation status](implementation-status.md) for the complete evidence and remaining project scope.

The earlier approval-service usage error reported availability at 9:04 PM and did not execute the requested command. The normal approval path was retried after availability returned; no bypass or substituted database was used. The verified read slice does not prove complete BR-020 event coverage for modules that are still missing.
