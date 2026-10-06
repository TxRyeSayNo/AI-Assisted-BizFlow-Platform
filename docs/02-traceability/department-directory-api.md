# Department directory slice

Updated: 2026-10-01. DEV-D4 implementation of Appendix D `API-ORG-05` (GET `/api/v1/departments`), supporting FR-ORG-004 and BR-001 tenant isolation, with a read-only GRID directory at the approved `/settings/organization/departments` route. This is not a completed department-management feature or full UI-03 setup screen.

## Contract

The canonical catalog authorizes this read for **Authenticated** callers. The Application layer requires a current active user inside an active tenant/company, but deliberately does not invent a Company Admin role or extra permission requirement. A tenantless Platform Administrator cannot select or bypass into a tenant's directory. Department mutations and other permission-gated operations must continue to use `IResourceAuthorizer`; the membership-only authorizer is limited to catalog reads whose approved access is authenticated tenant membership.

Query parameters are `page` (default 1), `pageSize` (default 25, maximum 100), optional `status` (ACTIVE or INACTIVE), and optional `search` (maximum 200 trimmed characters, literal case-insensitive substring of code/name). The default includes both canonical statuses. Rows are ordered by `(CreatedAt, DepartmentId)` ascending. The result is `{ items, page, pageSize, total }`; each row contains DepartmentId, Code, Name, ParentDepartmentId, Status and CreatedAt. Pagination is read-committed, not a frozen snapshot across requests.

Tenant identity comes exclusively from the validated session. No tenant selector is accepted as authority. A forged `X-Tenant-Id` header or `tenantId` query parameter cannot override it. Infrastructure keeps the existing fail-closed global filters and adds the Application-resolved tenant predicate; no `IgnoreQueryFilters` is used. Search, count, paging and parent IDs remain tenant-scoped. Responses are no-store.

## Implementation evidence

- `TenantMembershipAuthorizer`: current identity/tenant checks and denial audit; no role-name authority.
- `DepartmentQuery`: authorization before query validation/storage; bounded paging and canonical-status validation.
- `DepartmentReader`: read-only tenant-filtered projection from the existing Department table.
- `DepartmentsController`: authenticated thin API controller; no DbContext access.
- `TenantMembershipTests`: active member without grants, inactive user/tenant denial, tenantless platform denial.
- `DepartmentQueryTests`: real PostgreSQL/API checks for anonymous/platform denial, tenant A/B isolation with forged inputs, scoped parent IDs, filtering/paging/validation and rejection of stale sessions after deactivation/suspension.

No table, column, state, role grant or migration is added. No department is created or modified by this endpoint. Organization configuration and create/update APIs remain incomplete.

Backend validation (2026-09-30): the full run passed 49 unit and 65 integration tests with real PostgreSQL 18. The frontend increment does not change the API, persistence model or business authorization.

## Read-only UI (2026-10-01)

The workspace links tenant members to the directory. Tenantless platform accounts receive the access-denied route; anonymous visitors go to login. This navigation guard is a usability mechanism only: the existing API authorizer remains authoritative. No Company Admin role is invented for this authenticated catalog read, and this link does not expose configuration mutations to employees.

The screen uses approved GRID tokens and Material controls, displays department names/codes and canonical status labels, and supports literal search, status filtering and 25-row paging. It does not pretend that paginated rows form a complete department hierarchy. Parent management and manager assignment remain unfinished.

Loading, useful empty, sanitized failure/retry and permission-denied states are implemented. Applying filters resets pagination, obsolete requests are cancelled, and previous rows are cleared before loading or failure. Company/department text uses Angular interpolation, never raw HTML. The client sends no tenant selector. No create/edit controls or fictitious mutation endpoints are shipped.

Verification includes four directory component tests and three tenant navigation guard tests. The real browser harness seeds 26 departments and separate-tenant sentinel records only in its disposable PostgreSQL database. Desktop/mobile flows exercise workspace navigation, first/last pages, inactive filtering, exact code search and exclusion of the other tenant's records. These are real API calls, not mocked data. Desktop/mobile screenshots were visually inspected without overflow or overlapping controls. Full frontend build/test evidence is maintained in [implementation status](implementation-status.md).

## Pending business-rule decision

FR-ORG-004 says **name/code unique within tenant**, while Appendix C explicitly says Department.Name is **not unique**. The owner has been asked whether to enforce both unique names/codes or codes only. Neither interpretation has been silently implemented. Existing code uniqueness and non-unique names remain unchanged until confirmation; department creation/update work is stopped at that conflict. Optional manager assignment also depends on the approved organization/management-scope implementation and is not represented by an invented Department.ManagerId column.
