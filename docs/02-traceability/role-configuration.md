# Tenant role configuration

Date: 2026-10-01. DEV-D4 implementation of FR-ORG-005 and canonical API-RBAC-01/02/03, with the approved Settings → Organization → Roles route. Supports BR-001/002 tenant/permission boundaries and BR-020 audit. The full organization module and permission catalog remain incomplete.

## Contracts and boundaries

- `GET /api/v1/roles?page=1&pageSize=25&search=...`: returns `{ items, page, pageSize, total, availablePermissions }`. Page sizes are 1–100; search is at most 100 trimmed characters and a literal case-insensitive name substring. Rows are ordered by system flag descending, then name/ID. Each row contains `roleId`, `name`, `isSystem`, canonical `status`, `permissionIds` and `eTag`. The catalog contains ID/code/module/action/scope for non-platform reference permissions only.
- `POST /api/v1/roles` with `{ name }`: creates a tenant-owned ACTIVE custom role with no grants and no user assignments; returns 201, the role row and ETag. Names are trimmed, 1–100 characters. Uniqueness preserves the existing tenant/name database index (case-sensitive); a duplicate returns `ROLE.NAME_EXISTS`/409 and commits no audit or partial record.
- `PUT /api/v1/roles/{id}/permissions` with `{ permissionIds }` and a strong `If-Match` value from the role row: replaces the permission set. IDs must be distinct, nonempty and present in the allowed catalog; an empty set removes all grants. The request is bounded to 500 IDs. Missing/malformed ETags return `ROLE.ETAG_REQUIRED`/422; a stale version returns `ROLE.VERSION_CONFLICT`/409 with current ETag/permission IDs. No wildcard overwrite is accepted.
- All three operations require a current tenant context and the explicit TENANT-scoped `roles.configure` grant. Role names, frontend navigation and token-carried grant snapshots are not authority. Department/self/assigned grants cannot authorize company-wide role configuration. Platform accounts have no tenant-role bypass.
- Cross-tenant role IDs are not found, even with forged tenant headers/query parameters. Tenant results exclude platform system roles and platform permissions. All responses are no-store.

System roles remain migration-owned protected reference data; this API cannot modify or delete them. Custom-role changes only affect the current tenant and cannot add PLATFORM authority. Role assignment to users belongs to FR-ORG-001/002 and is not added through an invented role endpoint.

## Persistence, concurrency and audit

The existing Role aggregate's PostgreSQL `xmin` supplies the ETag. Permission mutations acquire a row lock, recheck caller authority after waiting, validate the expected version and update the role even when only RolePermission rows change. Competing edits from the same version produce one winner and one conflict, not a merged or silently overwritten set.

FR-ORG-005's saved configuration uses the approved mutable-aggregate concurrency convention plus immutable before/after AuditLog snapshots; it does not introduce a RoleVersion table or a published-workflow lifecycle. `ROLE.CREATED` and `ROLE.PERMISSIONS_CONFIGURED` events record the actual actor, tenant, role and sorted permission IDs. The role/junction changes and mutation audit commit together. Authorization denial, protected-system-role edits, inaccessible roles and invalid permission-set attempts generate separate safe denial events without foreign resource details.

`20260930175555_TenantRoleConfiguration` is reference-data only: it adds `roles.configure`, the COMPANY_ADMIN/MANAGER/EMPLOYEE system-role identities, and the COMPANY_ADMIN grant for this implemented capability. Existing PLATFORM_ADMIN reference data is preserved. No account, password or automatic user-role assignment is seeded. At this migration MANAGER/EMPLOYEE had no feature permissions; the later [people-directory migration](people-directory.md) grants all three tenant system roles `users.read` according to SSS §5. The system-role permission matrix must grow with the remaining approved modules. This is **not** a complete permission-catalog/default-role implementation. No physical table or column is added by either permission migration.

Rollback refuses to remove assigned reference roles or a permission used by other roles. A dedicated disposable-database test verifies clean rollback/reapply and refusal with an existing assignment, including preservation of migration history and grants after refusal.

## UI and verification

Authorized tenant users navigate through `/settings` to `/settings/organization/roles`. The GRID/Material screen lists protected system roles, creates custom roles and provides a permission editor. Loading, empty, failure/retry and permission-denied states are implemented. A conflict blocks further save until the user reloads; the client never automatically substitutes the current ETag. A denied mutation clears stale role/catalog/editor data. Server messages are locally allowlisted.

Domain tests cover tenant-compatible permission scopes. PostgreSQL/API tests cover current grants versus role names, live grant removal, tenant isolation, platform-permission rejection, system-role protection, audit attribution/before-after state, rollback, duplicate names and concurrent permission edits. Browser tests create, configure and clear a real custom role at desktop/mobile widths; assignments used to prove live permission changes are test-only fixtures. UI tests cover states, input shape, If-Match, conflicts, system protection and clearing denied data.

See [implementation status](implementation-status.md) for exact validation runs. Provisioning, production user-role assignment, all remaining catalog entries, full settings navigation and final security/accessibility audit remain open.
