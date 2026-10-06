# Platform company-registration queue

Date: 2026-09-30. DEV-D4 incremental implementation of Appendix D `API-TEN-02`, A01 platform company visibility and the read-side of UI-02. Supports FR-TEN-001/002; it does **not** complete registration submission, approval, provisioning or lifecycle transitions.

## Contract and traceability

| Layer | Implementation |
|---|---|
| Domain/catalog | `PlatformPermissions.ReadCompanyRegistrations` = `platform.company-registrations.read`, PLATFORM scope |
| Application | `CompanyRegistrationQuery` authorizes the exact platform resource before validating filters or reading records |
| Infrastructure | `CompanyRegistrationReader` performs a narrow no-tracking Company projection; rejects tenant contexts; never exposes tenant work/user data or disables filters globally |
| API | Authenticated GET `/api/v1/platform/company-registrations`, no-store response; controller delegates to Application |
| Database | Existing Company records; migration `20260930055252_PlatformRegistrationRead` seeds one platform permission, the PLATFORM_ADMIN system-role configuration and its permission link; no table/column additions |
| UI | `/platform/companies`, reachable from the authenticated platform landing page with the permission; GRID top navigation, search/status filter, table, paging and four data states |
| Tests | Application authorization-before-storage and bounds; real API/PostgreSQL denial, live grant revocation, role-name nonauthority, literal substring filtering, stable paging and no state mutation; Angular state tests; desktop/mobile real-browser queue tests |

Query parameters: `page` (default 1), `pageSize` (default 25, maximum 100), optional `status` (PENDING/ACTIVE/SUSPENDED/INACTIVE), optional `search` (trimmed, maximum 200 characters). Search is a case-insensitive literal substring of company name/code, not a SQL wildcard expression. Ordering is newest `(CreatedAt, CompanyId)` first. The response is `{ items, page, pageSize, total }`; rows contain only CompanyId, Code, Name, ContactEmail, Status and CreatedAt. Count and page are separate read-committed queries; concurrent changes may alter the count between requests, and a frozen multi-page snapshot is not promised.

The API lists all registrations by default; the UI begins with PENDING selected. It displays actual canonical company status without introducing APPROVED or REJECTED company states. Invalid filters/paging return the standard 422 envelope. Anonymous callers get 401; callers without the live platform grant get 403 with denial audit. A tenant role named PLATFORM_ADMIN grants no authority, and platform account type alone is also insufficient. Role/grant revocation takes effect on the next API request even with an unexpired access token.

The reference-data migration creates no login account, secret, role assignment or tenant bypass. It is intentionally the first, partial system-role catalog seed; other approved permissions/roles remain to be implemented with their modules. Rollback refuses to remove the role if assigned to a user. A new application database still has no login accounts; test account creation and role assignment occur only inside disposable test containers. Production bootstrap and provisioning remain required.

UI dates are explicitly displayed in UTC because this is the tenantless platform plane. The scrollable table has a labelled keyboard-focusable region and semantic row/column headers; horizontal overflow stays inside that region. Failed or forbidden reloads clear old records before showing locally defined feedback. Older pending requests are cancelled on a new filter/page request. Client permission guards hide navigation for usability; the server remains authoritative.

## Remaining onboarding gates

- Public company registration, duplicate handling and platform-admin notifications.
- Separate approval and retry-safe provisioning, initial company admin, default tenant settings/calendar, onboarding notification.
- Company/tenant lifecycle transitions, reasons, concurrency, audit and notifications.
- Production platform bootstrap and remaining system-role permission catalog.
- Full UI-02 operational dashboard and lifecycle controls; the queue deliberately exposes no nonfunctional approve/provision buttons.
- End-to-end M1 / Demo D and the complete TST-SEC-001 business-endpoint suite.

Approval/provisioning must resolve the specification's approved-registration precondition using the canonical Company states and auditable history, without silently inventing a new state. Owner confirmation has been requested for an immutable COMPANY.APPROVED audit record while Company remains PENDING, changing to ACTIVE only when provisioning succeeds. This proposal is not approved yet, and this read-only slice does not implement it.

Validation: 45 unit and 60 integration tests passed across the backend; 20 Angular tests passed; production build succeeded; no EF model changes are pending. Eight real and six mocked desktop/mobile browser tests passed. The queue was visually inspected on desktop and mobile, including both ends of the horizontally scrollable mobile table.
