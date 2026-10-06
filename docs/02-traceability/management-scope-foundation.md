# Management-scope persistence and resolution

Date: 2026-10-01. Partial organization/security prerequisite, not a complete manager-administration or task-assignment feature.

## Requirement and boundary

BR-003 requires assignment/reassignment targets to remain inside configured management scope. Approved architecture §4.3 and A-02 specify `ManagementScope(ManagementScopeId, TenantId, UserId, DepartmentId, IncludeDescendants, CreatedBy, CreatedAt)`, with effective scope equal to the union of listed departments and optionally their descendants. This is one of the four already-approved additive tables, not a new scope amendment. The implemented model now has 11 application tables toward the approved 43-table target (including ADR-0002); provider-owned Hangfire tables are separate.

`ManagementScope` configuration does not itself grant `tasks.assign`, department membership or company-wide access. Role names do not confer scope. `ResourcePolicy` still requires the exact permission, active account/tenant, matching resource scope and the configured management target. Out-of-scope targets map through Application authorization to `TASK.TARGET_OUT_OF_SCOPE`/403, with a separate tenant-scoped denial audit that does not disclose target identifiers.

## Implemented

- Domain configuration with validated ownership/creator IDs and UTC creation time. Iterative descendant expansion unions overlapping roots, excludes unknown roots, does not add unlisted ancestors/siblings and terminates on malformed cycles or deep hierarchies.
- EF migration `20261001012648_ManagementScopeFoundation`: UUID key, tenant/user/creator FKs, composite same-tenant department FK, unique tenant/user/department configuration, FK indexes and `xmin`. No existing user receives a scope or new permission from this migration.
- Default queries require authenticated tenant context. Writes validate tenant, subject, department and actual creator; no scope edit/delete use case is exposed yet. SQL triggers independently prevent cross-tenant/platform subject or creator references and ownership/attribution changes. Configuration updates/removal remain unavailable through EF until an audited administration use case is implemented.
- The live identity resolver reads roots using both the verified user and tenant, and loads hierarchy only from that tenant. Its narrow filter bypass supports pre-session authentication; it does not trust request-supplied scope IDs. No cached/token-carried scope is used. Empty configuration remains empty; own department is not an implicit fallback.
- Hierarchy expansion describes configured scope, not target business eligibility. It does not silently redefine the subtree based on department status. Future assignment services must also enforce the applicable target/user/department and workflow preconditions.
- Migration rollback succeeds for an empty configuration table but refuses to discard populated authorization configuration. Fresh application and repeated migration application are exercised against disposable PostgreSQL.

## Evidence and remaining work

`ManagementScopeTests` verifies identity/time fields, root/descendant semantics, overlap, unknown roots and a 20,000-node cycle. `ManagementScopePersistenceTests` verifies scoped/fail-closed queries, EF and SQL boundaries, attribution, duplicate configuration, live expansion/revocation, pre-session resolution, actual Application denial/audit, permission-plus-scope enforcement and inactive-account denial. `ManagementScopeMigrationTests` checks rollback/reapply and preserves data/history/triggers after refused rollback. Exact test runs are recorded in [implementation status](implementation-status.md).

Still required: authorized Company Administrator configuration on user detail, atomic configuration-change audit, default own-department-subtree seeding for users with `tasks.assign` on creation/department movement, user-role administration and integration into actual task assignment/reassignment. No configuration endpoint or UI contract was invented here. Current test fixture SQL is not a supported administration mechanism. Full BR-003 and TST-SEC-001 acceptance remain open until those workflows are implemented and tested.
