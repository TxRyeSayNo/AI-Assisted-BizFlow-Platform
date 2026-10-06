# Tenant people directory

Date: 2026-10-01. Implements canonical API-ORG-01 (`GET /api/v1/users`) and the read portion of UI-03 organization setup. This is not FR-ORG-001/002/003 user creation, editing or deactivation.

## Authorization and data contract

The approved SSS §5 grants Organization read capability to Company Administrator, Manager and Employee. A-10 derives atomic catalog codes from those actions. The reference-only `20261001013618_UserDirectoryRead` migration adds TENANT-scoped `users.read` to COMPANY_ADMIN, MANAGER and EMPLOYEE system roles. No account, role assignment, management scope or user mutation permission is created. Custom roles require an explicit grant through existing role configuration. Role names are never authorization evidence.

Application authorization requires a live tenant-wide `users.read` grant and active account/tenant/company context. This is a company directory read, not task assignment authority: configured manager target scope remains a separate guard for assignment/reassignment. A narrower unrelated grant cannot be treated as tenant-wide directory permission. Tenantless platform users have no directory bypass. Forged tenant headers/query parameters cannot override signed context.

The endpoint returns `{ items, page, pageSize, total }`. Each item contains only `userId`, `employeeCode`, `fullName`, `email`, nullable `departmentId`/`departmentName`, and `status` (`ACTIVE`, `INACTIVE`, `LOCKED`). It never selects or serializes password hashes, security stamps, lockout counters, refresh sessions, reset data or effective grants. Deleted accounts are excluded by the normal tenant query filter. Department names and counts use the same tenant boundary. Responses are no-store.

Filters: `search` (trimmed, maximum 200 characters; literal case-insensitive name/code/email substring), `status`, optional `departmentId`, `page` (default 1) and `pageSize` (default 25, maximum 100). Foreign/nonexistent department filters yield no matches without an existence lookup. Empty identifiers, invalid states and overflowing paging are rejected. Ordering is stable by creation time and user ID. No mutation is exposed by this controller.

## UI

`/settings/organization/users` is a GRID/Material read-only People screen with search, account-status filtering, paging and department/contact information. Loading, empty, recoverable failure and denied states are explicit. Obsolete requests are cancelled and old data is cleared before reload or denial; server exception text is not rendered. Nullable department is displayed as “No department.” Names and contact values use escaped text, not HTML.

The workspace exposes “Browse people directory” only with the read grant. Settings also links it for administrators with that grant. Both tenant and permission route guards are usability controls; backend authorization remains mandatory. No create/edit/deactivate buttons or unfinished workflow placeholders are shown.

## Verification and remaining gates

`UserDirectoryTests` uses real HTTP authentication and PostgreSQL to verify no role-name authority, live grant removal, all three explicit system-role read grants, cross-tenant forged input, soft-delete exclusion, same-tenant department lookup, exact safe response keys, paging, literal search, canonical statuses, invalid filters, platform denial and revoked tenant eligibility. `UserDirectoryMigrationTests` verifies rollback/reapply and refusal to remove custom-role configuration.

Angular tests cover loading/empty/ready/denied/failure states, escaping, nullable departments, request cancellation, applied versus edited filters and permission navigation. Real desktop/mobile browser flows use 27 tenant-owned accounts and verify paging, status/email search, empty states and overflow. See [implementation status](implementation-status.md) for completed validation runs and screenshot review.

Remaining: user create/invite/update/deactivate flows, temporary-password completion contract, user-role assignment, manager-scope administration/default seeding, full permission matrix, full organization shell and accessibility/final security audit. The full physical-model target stays 43 application tables; this migration adds no tables or columns.
