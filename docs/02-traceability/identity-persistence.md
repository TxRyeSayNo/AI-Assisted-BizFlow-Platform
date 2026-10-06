# Identity persistence increment

Requirements: FR-AUTH-001/002/003, FR-TEN-004, FR-ORG-001/004/005; BR-001/002/015/020; SSS Appendix C; ADR-0002.

The first migration contains ten of the target 43 application tables: Company, Tenant, User, Department, Role, Permission, UserRole, RolePermission, AuthenticationSession and AuditLog. Other required entities will be added in their dependent slices; they have not been removed from scope.

## Physical mapping

Domain `Id` maps to the SSS's named primary key (for example `UserAccount.Id` → `User.UserId`). All primary keys are generated UUIDv7. Enum values use the uppercase canonical vocabulary through native PostgreSQL enum mappings. DateTimeOffset columns map to TIMESTAMPTZ; factories normalize timestamps to UTC. Department hierarchy and user-department foreign keys include TenantId, so PostgreSQL rejects cross-tenant references.

Identity operational columns on User: normalized employee code/email, security stamp, failed-access count, lockout end, MustChangePassword and a platform-account discriminator. Normalized identity indexes enforce case-insensitive uniqueness per tenant; identifiers remain reusable in another tenant. The platform discriminator allows null TenantId only for the approved platform-account plane and does not grant permissions. A tenantless user cannot belong to a department. Accounts still require explicit role-permission grants.

Company, Tenant, Department, User, Role and AuthenticationSession use PostgreSQL xmin for concurrency. TenantId/UserId are additional concurrency predicates on tenant-owned records and sessions; this blocks a forged detached entity from changing another tenant's row even if its primary key and xmin are known.

Default tenant-owned reads fail closed without authenticated user and tenant context. Platform data access and authentication lookups must use explicit, narrowly scoped queries after the relevant Application validation. The DbContext's platform-account check is a persistence-plane safeguard, **not** a replacement for action-specific Application authorization. No request DTO or tenant header changes the context.

## Storage invariants

- AuditLog rejects UPDATE, DELETE and TRUNCATE through PostgreSQL triggers, in addition to EF save guards.
- Tenant ownership is immutable for User, Department and Role.
- UserRole and RolePermission triggers prohibit cross-tenant custom roles and platform permission elevation into tenant roles/accounts.
- Sessions store only a unique SHA-256 token hash; successor and consumed-session changes commit in one EF transaction with xmin checking. Concurrent losers must roll back their inserted successor.
- Sync SaveChanges is rejected; all saves pass through the asynchronous scope guards.

The migration alone does not prove a complete FR. Identity password verification, JWT issuance and refresh/replay-family revocation are now implemented in the [authentication slice](authentication-slice.md). User lifecycle services, password reset, system-role seeds and the full business API isolation suite remain required.

## Provider references

EF's [global filters](https://learn.microsoft.com/en-us/ef/core/querying/filters), Npgsql's [enum mapping](https://www.npgsql.org/efcore/mapping/enum.html) and [xmin concurrency mapping](https://www.npgsql.org/efcore/modeling/concurrency.html) informed the provider configuration. The integration suite applies migrations to a disposable PostgreSQL 18.3 container and tests storage behavior directly.
