# Workflow library and draft version creation

Scope: canonical API-WF-01, API-WF-02's initial draft metadata, API-WF-03 empty next-version creation, API-WF-05, FR-WF-001 (partial), SSS §5 Workflow read, BR-001/002/009/020, UI-03 and the version-library portion of UI-11. This does not complete workflow authoring, publication or execution.

## Contracts

| Endpoint | Permission | Contract |
|---|---|---|
| GET `/api/v1/workflows` | `workflows.read`, TENANT | `page` (default 1), `pageSize` (default 25, max 100), `search` (max 200, literal case-insensitive name substring), `businessType` (TASK/REQUEST), `status` (DRAFT/ACTIVE/INACTIVE). Stable name/ID order; scoped total; latest version ID/number/status. Workflow status is not version status. |
| POST `/api/v1/workflows` | `workflows.configure`, TENANT | `{ "name": "Access requests", "businessType": "REQUEST" }`; type defaults to REQUEST when omitted. Name trimmed, 1–200 characters; duplicates allowed by Appendix C. Creates a logical DRAFT and empty version 1 in DRAFT. Returns 201 with row and version-read Location. |
| GET `/api/v1/workflow-versions/{id}` | `workflows.read`, TENANT | Workflow/name/type, version identity/number/status/publication time, object-shaped definition, ordered steps/configuration and ordered transitions/guards. Repeatable-read snapshot keeps the graph consistent. Other-tenant and missing IDs both return 404. |
| POST `/api/v1/workflows/{id}/versions` | `workflows.configure`, TENANT | Required body `{}` creates an empty DRAFT with the next server-assigned version number. Returns 201 with workflow row and version-read Location. Existing workflow status, versions, steps and transitions are unchanged. This is not a clone operation; unknown fields, including source version, number, status and graph data, return 422. |

Initial creation accepts metadata only. Unknown JSON fields are rejected with 422, including status, tenantId, steps and transitions; supplied graph data is never silently discarded. The API cannot activate, publish or execute the draft. Empty drafts are allowed for setup, but are not valid publishable workflows. The complete graph-authoring payload remains an acceptance gate.

All four endpoints require authentication and emit no-store responses. Tenant context comes from the authenticated session, not request parameters/headers. Company/account/tenant eligibility and effective grants are resolved live through the Application authorizer. Role names grant no authority. Initial creation commits root, version and `WORKFLOW.DRAFT_CREATED` audit together; an injected database failure test verifies all three roll back. The audit records the real actor, tenant, root, initial version, type, name and draft status.

Next-version creation locks the tenant-owned workflow row, reads the highest committed version number and rechecks live authority after lock acquisition. The Application layer creates the draft and `WORKFLOW.VERSION_DRAFT_CREATED` audit; Infrastructure commits them together. Concurrent requests serialize to distinct increasing numbers. Number exhaustion returns 409 `WORKFLOW.VERSION_LIMIT`. No existing aggregate data is replaced, so this append operation does not need an If-Match header. It is not idempotent: separate successful calls create separate drafts. Clients do not automatically retry an uncertain response. No new schema, state, permission or default grant is introduced.

Reference-only migration `20261001092159_WorkflowLibraryPermissions` seeds `workflows.read` to COMPANY_ADMIN, MANAGER and EMPLOYEE under SSS §5 and derived-catalog decision A-10. Only COMPANY_ADMIN receives `workflows.configure`. Custom roles may configure either explicit tenant permission. No accounts, user-role assignments, tables, published definitions or production demo data are seeded. Rollback refuses to remove custom-role grants.

Platform-plane accounts do not bypass tenant isolation. The SSS's platform Workflow read capability still needs an explicitly authorized platform inspection design; this tenant library is not that interface.

## UI

- Settings → Workflows (`/settings/workflows`) provides name/type/status filters, pagination, latest-version links and loading/empty/error/denial states.
- Authorized configuration users can create a name/type and empty draft version. Basic metadata entry is usable on desktop/mobile; this is not the desktop-optimized graph designer. An uncertain network outcome requires reloading and checking the library before another create attempt.
- `/settings/workflows/:id/versions/:v` reads the actual version, steps, transitions and stored JSON as escaped text. Existing configuration is read-only on every screen size. Configuration users can create an explicitly empty next version, then follow the returned link; the historical view is not replaced. Pending requests disable creation, a success receipt prevents duplicate clicks, and uncertain failures direct users to check the library before retrying. No unimplemented edit/publish/runtime buttons are shown.
- Settings navigation accepts role-configuration or workflow-read permission and exposes only the corresponding links. Individual feature guards and server authorization remain independent. Workflow navigation is not added to the operational top level.
- Existing GRID tokens and Material controls are reused. No visual assets, new theme or design studio are involved.

## Remaining acceptance gates

FR-WF-001 still requires graph editing, typed definition/configuration/guard schemas, canonical state/edge checks, valid start/end path, reference/approval-rule validation, cycle policy, next-version authoring, transactional authorized publication/audit, service selection and full designer acceptance. The missing draft-update endpoint is explicitly awaiting owner confirmation in [pending decisions](pending-owner-decisions.md). No such endpoint has been silently invented.

FR-WF-002 runtime integration, approvals, SLA, Task/Request persistence, workflow transition audits and business side effects remain incomplete. Stored JSON is not executed. ADR-0003's exact task transition graph remains authoritative.

Validation results are recorded in [implementation status](implementation-status.md). Backend evidence includes live revocation, non-authoritative role names, read-versus-configure separation, tenant/ID isolation, draft/audit atomicity, validation/overposting, failed-transaction rollback, suspended-tenant denial and safe migration rollback/reapply. Frontend tests and real desktop/mobile flows verify the UI, not substitutes for these backend checks.
