# BizFlow Requirements Register

Version 0.1 · 2026-10-16 · Draft, pending stakeholder review

This is the companion register for [`SRS.md`](SRS.md). SSS FR IDs are preserved. “Confirmed in SSS” means present in the source baseline; implementation is **not verified**. Proposed NFRs remain proposed. Release scope is carried from SSS priority plus the user-confirmed workspace decision.

## Summary


|Requirement class|Count|
|---|---|
|Functional Requirements|62|
|Non-Functional Requirements|11|
|Business Rules|22|
|Security Requirements|12|

## 1. Functional requirements (62 canonical IDs)


### FR-AUTH-001 — Authenticate with Employee Code


|Field|Value|
|---|---|
|Requirement ID|FR-AUTH-001|
|Title|Authenticate with Employee Code|
|Requirement statement|The system shall Allow users to log in using their employee code and password. Main behavior: Validate credentials; create authenticated session/token; resolve tenant|
|Rationale|Allow users to log in using their employee code and password.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager/Company Admin|
|Preconditions|Active user; valid tenant/company context|
|Trigger|Login form submit|
|Main behavior|Validate credentials; create authenticated session/token; resolve tenant|
|Alternate behavior|If code is ambiguous globally, require tenant/company context or scoped login|
|Exception/failure behavior|Invalid credentials, inactive account, locked account|
|Input|EmployeeCode, Password, Tenant/Company context|
|Output|Access token + refresh token/session context|
|Validation|Required, code must match user and password hash|
|Business rule|EmployeeCode unique within tenant|
|Permission|Authentication only within current tenant|
|Data impact|User/session/auth metadata|
|API impact|POST /api/v1/auth/login|
|Notification|Login success; failed login if security policy requires|
|AI involvement|No AI|
|Dependencies|M01|
|Acceptance criteria|User can log in and receive correct tenant context.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AUTH-002 — Authenticate with Company Email


|Field|Value|
|---|---|
|Requirement ID|FR-AUTH-002|
|Title|Authenticate with Company Email|
|Requirement statement|The system shall Allow company-provided email + password login. Main behavior: Validate email and password; create session/token|
|Rationale|Allow company-provided email + password login.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager/Company Admin|
|Preconditions|Active account with unique company email|
|Trigger|Login form submit|
|Main behavior|Validate email and password; create session/token|
|Alternate behavior|Employee can use either employee code or email|
|Exception/failure behavior|Invalid credentials/inactive/locked|
|Input|Email, Password, tenant/company context|
|Output|Access token + refresh context|
|Validation|Email format; unique within tenant|
|Business rule|Email belongs to one user in tenant|
|Permission|Same as login policy|
|Data impact|User/session/auth metadata|
|API impact|POST /api/v1/auth/login|
|Notification|Optional security notification for repeated failures|
|AI involvement|No AI|
|Dependencies|M01|
|Acceptance criteria|Valid company email credentials authenticate successfully.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AUTH-003 — Refresh Access Token


|Field|Value|
|---|---|
|Requirement ID|FR-AUTH-003|
|Title|Refresh Access Token|
|Requirement statement|The system shall Renew access token without re-entering credentials. Main behavior: Validate and rotate refresh token; return access token|
|Rationale|Renew access token without re-entering credentials.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Authenticated user|
|Preconditions|Valid refresh token|
|Trigger|Token refresh request|
|Main behavior|Validate and rotate refresh token; return access token|
|Alternate behavior|Expired refresh token requires login|
|Exception/failure behavior|Revoked/invalid token|
|Input|Refresh token|
|Output|New access token|
|Validation|Token signature, expiry, revocation|
|Business rule|Refresh token rotation|
|Permission|Auth only|
|Data impact|User/session/auth metadata|
|API impact|POST /api/v1/auth/refresh|
|Notification|None|
|AI involvement|No AI|
|Dependencies|M01|
|Acceptance criteria|Valid refresh token yields a new access token.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AUTH-004 — Reset Password


|Field|Value|
|---|---|
|Requirement ID|FR-AUTH-004|
|Title|Reset Password|
|Requirement statement|The system shall Reset or administratively issue a new password. Main behavior: Issue reset token via company email; or admin triggers temporary password flow|
|Rationale|Reset or administratively issue a new password.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|User / Company Admin|
|Preconditions|User identity known; email available or admin authority|
|Trigger|Forgot password / admin reset|
|Main behavior|Issue reset token via company email; or admin triggers temporary password flow|
|Alternate behavior|Employee without email uses admin-issued reset|
|Exception/failure behavior|Expired token, invalid code|
|Input|Identifier + reset token + new password|
|Output|Password reset confirmation|
|Validation|Password policy; reset token one-time|
|Business rule|Old password invalidated after reset|
|Permission|Auth permission|
|Data impact|User/session/auth metadata|
|API impact|POST /api/v1/auth/forgot-password; POST /api/v1/auth/reset-password|
|Notification|Email reset|
|AI involvement|No AI|
|Dependencies|M01|
|Acceptance criteria|Password can be changed through an authorized flow.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TEN-001 — Register Company


|Field|Value|
|---|---|
|Requirement ID|FR-TEN-001|
|Title|Register Company|
|Requirement statement|The system shall Create a registration request for a new company. Main behavior: Validate company data; create pending company registration|
|Rationale|Create a registration request for a new company.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Public/Platform onboarding flow|
|Preconditions|No active tenant with same company identity|
|Trigger|Registration submit|
|Main behavior|Validate company data; create pending company registration|
|Alternate behavior|Duplicate company details return review message|
|Exception/failure behavior|Validation/duplicate failure|
|Input|Company name, legal/contact data|
|Output|Pending company registration|
|Validation|Required fields; uniqueness rules|
|Business rule|Registration starts in Pending|
|Permission|Public form or authenticated onboarding|
|Data impact|Company/Tenant records|
|API impact|POST /api/v1/platform/company-registrations|
|Notification|Platform Admin notification|
|AI involvement|No AI|
|Dependencies|M02|
|Acceptance criteria|A valid registration is created in Pending state.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TEN-002 — Create Tenant for Approved Company


|Field|Value|
|---|---|
|Requirement ID|FR-TEN-002|
|Title|Create Tenant for Approved Company|
|Requirement statement|The system shall Create isolated tenant/workspace after approval. Main behavior: Create tenant; initialize default settings; create initial Company Admin|
|Rationale|Create isolated tenant/workspace after approval.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Platform Administrator|
|Preconditions|Company registration approved|
|Trigger|Approval action|
|Main behavior|Create tenant; initialize default settings; create initial Company Admin|
|Alternate behavior|Tenant creation retries safely|
|Exception/failure behavior|Provisioning failure|
|Input|CompanyId, tenant key, settings|
|Output|Active tenant/workspace|
|Validation|Tenant key unique|
|Business rule|One primary tenant per company in MVP|
|Permission|Platform Admin only|
|Data impact|Company/Tenant records|
|API impact|POST /api/v1/platform/tenants|
|Notification|Company Admin onboarding notification|
|AI involvement|No AI|
|Dependencies|M01/M02|
|Acceptance criteria|Approved company receives an active isolated tenant.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TEN-003 — Change Company Status


|Field|Value|
|---|---|
|Requirement ID|FR-TEN-003|
|Title|Change Company Status|
|Requirement statement|The system shall Activate, suspend or deactivate company operations. Main behavior: Validate transition; update company/tenant status; preserve history|
|Rationale|Activate, suspend or deactivate company operations.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Platform Administrator|
|Preconditions|Company exists|
|Trigger|Status action|
|Main behavior|Validate transition; update company/tenant status; preserve history|
|Alternate behavior|Already-target-status is idempotent|
|Exception/failure behavior|Invalid transition|
|Input|CompanyId, target status, reason|
|Output|New company status|
|Validation|Reason required for suspend/deactivate|
|Business rule|Suspended tenant blocks normal business actions|
|Permission|Platform Admin|
|Data impact|Company/Tenant records|
|API impact|PATCH /api/v1/platform/companies/{id}/status|
|Notification|Tenant admin alert|
|AI involvement|No AI|
|Dependencies|M02/M13|
|Acceptance criteria|Status changes are enforced and audited.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TEN-004 — Enforce Tenant Data Isolation


|Field|Value|
|---|---|
|Requirement ID|FR-TEN-004|
|Title|Enforce Tenant Data Isolation|
|Requirement statement|The system shall Restrict every business query/action to current tenant. Main behavior: Resolve tenant; authorize resource; apply tenant filter; reject cross-tenant access|
|Rationale|Restrict every business query/action to current tenant.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|System / all actors|
|Preconditions|Authenticated user with tenant context|
|Trigger|Every business request|
|Main behavior|Resolve tenant; authorize resource; apply tenant filter; reject cross-tenant access|
|Alternate behavior|Platform Admin has explicit platform scope only|
|Exception/failure behavior|Missing tenant, cross-tenant resource|
|Input|TenantId + resource ID|
|Output|Authorized resource or 403/404|
|Validation|TenantId required on tenant-owned data|
|Business rule|No cross-tenant read/write|
|Permission|Applied across API/data layer|
|Data impact|Company/Tenant records|
|API impact|All endpoints|
|Notification|Security alert/audit on suspicious attempts|
|AI involvement|AI inherits current tenant context|
|Dependencies|All modules|
|Acceptance criteria|Cross-tenant integration tests pass.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-ORG-001 — Create User Account


|Field|Value|
|---|---|
|Requirement ID|FR-ORG-001|
|Title|Create User Account|
|Requirement statement|The system shall Create a user inside a company. Main behavior: Validate identity; create user; assign department/role; activate or invite|
|Rationale|Create a user inside a company.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Tenant active; admin permission|
|Trigger|Save user|
|Main behavior|Validate identity; create user; assign department/role; activate or invite|
|Alternate behavior|Invite pending activation|
|Exception/failure behavior|Duplicate employee code/email|
|Input|EmployeeCode, Name, Email, Department, Role, Status|
|Output|User record|
|Validation|EmployeeCode and email unique within tenant|
|Business rule|User belongs to one tenant|
|Permission|users.create|
|Data impact|User/Department/Role records|
|API impact|POST /api/v1/users|
|Notification|Invite notification|
|AI involvement|No AI|
|Dependencies|M01-M03|
|Acceptance criteria|New user is visible only in its tenant and has correct permissions.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-ORG-002 — Update User Profile


|Field|Value|
|---|---|
|Requirement ID|FR-ORG-002|
|Title|Update User Profile|
|Requirement statement|The system shall Update editable user information. Main behavior: Validate allowed fields; persist; audit sensitive changes|
|Rationale|Update editable user information.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator / authorized user for own profile|
|Preconditions|User exists and in same tenant|
|Trigger|Save changes|
|Main behavior|Validate allowed fields; persist; audit sensitive changes|
|Alternate behavior|Email/employee code change may require re-verification|
|Exception/failure behavior|Invalid format or immutable field|
|Input|Profile fields|
|Output|Updated user|
|Validation|Field-specific validation|
|Business rule|EmployeeCode may be immutable after activation unless admin policy allows|
|Permission|users.update / profile.update|
|Data impact|User/Department/Role records|
|API impact|PUT /api/v1/users/{id}|
|Notification|Optional profile update notification|
|AI involvement|No AI|
|Dependencies|M03|
|Acceptance criteria|Authorized changes are persisted and audited.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-ORG-003 — Deactivate User Account


|Field|Value|
|---|---|
|Requirement ID|FR-ORG-003|
|Title|Deactivate User Account|
|Requirement statement|The system shall Disable a user without losing history. Main behavior: Set inactive; revoke sessions; preserve historical ownership|
|Rationale|Disable a user without losing history.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|User exists; no hard-delete rule|
|Trigger|Deactivate action|
|Main behavior|Set inactive; revoke sessions; preserve historical ownership|
|Alternate behavior|Blocked if user is required as sole active approver unless reassigned|
|Exception/failure behavior|Invalid state / critical dependency|
|Input|UserId, reason|
|Output|Inactive user|
|Validation|Reason required|
|Business rule|Historical records remain|
|Permission|users.deactivate|
|Data impact|User/Department/Role records|
|API impact|POST /api/v1/users/{id}/deactivate|
|Notification|Optional notification|
|AI involvement|No AI|
|Dependencies|M01/M03/M07|
|Acceptance criteria|User can no longer authenticate while history remains intact.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-ORG-004 — Create Department


|Field|Value|
|---|---|
|Requirement ID|FR-ORG-004|
|Title|Create Department|
|Requirement statement|The system shall Create department within tenant. Main behavior: Validate name/code; create; optionally assign manager|
|Rationale|Create department within tenant.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Tenant active|
|Trigger|Save department|
|Main behavior|Validate name/code; create; optionally assign manager|
|Alternate behavior|Duplicate code rejected|
|Exception/failure behavior|Validation|
|Input|Name, Code, ParentDepartment optional|
|Output|Department|
|Validation|Name/code unique within tenant|
|Business rule|No cross-tenant reference|
|Permission|departments.create|
|Data impact|User/Department/Role records|
|API impact|POST /api/v1/departments|
|Notification|None|
|AI involvement|No AI|
|Dependencies|M03|
|Acceptance criteria|Department appears in tenant organization.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-ORG-005 — Configure Role Permissions


|Field|Value|
|---|---|
|Requirement ID|FR-ORG-005|
|Title|Configure Role Permissions|
|Requirement statement|The system shall Create/update custom roles and permission assignments. Main behavior: Validate permissions against allowed catalog; save version|
|Rationale|Create/update custom roles and permission assignments.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Tenant active|
|Trigger|Save role permissions|
|Main behavior|Validate permissions against allowed catalog; save version|
|Alternate behavior|Cannot remove permission required by system protection|
|Exception/failure behavior|Unauthorized permission elevation|
|Input|Role, permission set|
|Output|Updated role|
|Validation|Permission IDs valid; role unique within tenant|
|Business rule|Custom roles cannot exceed tenant/platform restrictions|
|Permission|roles.configure|
|Data impact|User/Department/Role records|
|API impact|PUT /api/v1/roles/{id}/permissions|
|Notification|Security/config audit|
|AI involvement|No AI|
|Dependencies|M03/M13|
|Acceptance criteria|Role permissions apply to users after policy evaluation.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SVC-001 — Create Internal Service


|Field|Value|
|---|---|
|Requirement ID|FR-SVC-001|
|Title|Create Internal Service|
|Requirement statement|The system shall Define a service offered to employees inside the company. Main behavior: Create service and category metadata|
|Rationale|Define a service offered to employees inside the company.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Tenant active|
|Trigger|Save service|
|Main behavior|Create service and category metadata|
|Alternate behavior|Duplicate code rejected|
|Exception/failure behavior|Validation|
|Input|Name, code, description, categories, active flag|
|Output|Service|
|Validation|Code unique within tenant|
|Business rule|Service belongs to one tenant|
|Permission|service.create|
|Data impact|Service and routing/configuration records|
|API impact|POST /api/v1/services|
|Notification|None|
|AI involvement|Optional AI can use service catalog as context|
|Dependencies|M04|
|Acceptance criteria|Service can be selected for future requests.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SVC-002 — Configure Service Routing


|Field|Value|
|---|---|
|Requirement ID|FR-SVC-002|
|Title|Configure Service Routing|
|Requirement statement|The system shall Define routing defaults/constraints for a service. Main behavior: Set target department rules, allowed assignee scopes and fallback|
|Rationale|Define routing defaults/constraints for a service.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Service exists|
|Trigger|Save routing configuration|
|Main behavior|Set target department rules, allowed assignee scopes and fallback|
|Alternate behavior|Rules may overlap; system resolves by priority|
|Exception/failure behavior|Invalid department/person references|
|Input|Routing rules|
|Output|Versioned routing config|
|Validation|References same tenant; no cyclic rules|
|Business rule|Fallback to manual routing|
|Permission|service.routing.configure|
|Data impact|Service and routing/configuration records|
|API impact|PUT /api/v1/services/{id}/routing|
|Notification|Config audit|
|AI involvement|AI uses routing rules as constraints|
|Dependencies|M04/M12|
|Acceptance criteria|Valid service routing guides request assignment.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SVC-003 — Configure Workflow for Service


|Field|Value|
|---|---|
|Requirement ID|FR-SVC-003|
|Title|Configure Workflow for Service|
|Requirement statement|The system shall Associate a workflow version with a service. Main behavior: Create/activate workflow version association|
|Rationale|Associate a workflow version with a service.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Service + workflow exist|
|Trigger|Save association|
|Main behavior|Create/activate workflow version association|
|Alternate behavior|Active workflow cannot be modified in place|
|Exception/failure behavior|Version conflict|
|Input|ServiceId, WorkflowVersionId|
|Output|New active association|
|Validation|Only compatible workflow|
|Business rule|Existing in-flight objects retain old version|
|Permission|service.workflow.configure|
|Data impact|Service and routing/configuration records|
|API impact|PUT /api/v1/services/{id}/workflow|
|Notification|Config audit|
|AI involvement|AI can recommend but cannot change association without authority|
|Dependencies|M04/M07|
|Acceptance criteria|New service transactions use the configured workflow version.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SVC-004 — Configure Approval Rule for Service


|Field|Value|
|---|---|
|Requirement ID|FR-SVC-004|
|Title|Configure Approval Rule for Service|
|Requirement statement|The system shall Attach approval behavior to a service. Main behavior: Define approver sequence/parallel groups and decision rules|
|Rationale|Attach approval behavior to a service.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Service exists|
|Trigger|Save rule|
|Main behavior|Define approver sequence/parallel groups and decision rules|
|Alternate behavior|Invalid approver reference rejected|
|Exception/failure behavior|No approver available|
|Input|Approval configuration|
|Output|Approval rule version|
|Validation|Approver same tenant and active|
|Business rule|At least one valid approval step when required|
|Permission|service.approval.configure|
|Data impact|Service and routing/configuration records|
|API impact|PUT /api/v1/services/{id}/approval-rules|
|Notification|Config audit|
|AI involvement|AI may summarize, not approve|
|Dependencies|M04/M07|
|Acceptance criteria|Approval instances use the active rule version.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-001 — Create Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-001|
|Title|Create Task|
|Requirement statement|The system shall Create an internal task from structured or natural-language input. Main behavior: Validate fields; create DRAFT or ASSIGNED task; optionally invoke AI assistance|
|Rationale|Create an internal task from structured or natural-language input.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager|
|Preconditions|Manager has create permission|
|Trigger|Create action|
|Main behavior|Validate fields; create DRAFT or ASSIGNED task; optionally invoke AI assistance|
|Alternate behavior|AI draft can be edited before save|
|Exception/failure behavior|Validation, duplicate submit|
|Input|Title, description, assignee/department optional, deadline, priority, checklist|
|Output|Task record|
|Validation|Title required; deadline valid; references same tenant|
|Business rule|Task creator becomes audit owner; task belongs to tenant|
|Permission|tasks.create|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks|
|Notification|Assignee notification after assignment|
|AI involvement|AI may extract/suggest fields|
|Dependencies|M05/M12|
|Acceptance criteria|Manager can create a valid task and see it in own scope.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-002 — Assign Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-002|
|Title|Assign Task|
|Requirement statement|The system shall Assign a task to employee or department. Main behavior: Validate target scope; create assignment; change state to ASSIGNED|
|Rationale|Assign a task to employee or department.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager|
|Preconditions|Task DRAFT/REJECTED; target within manager scope|
|Trigger|Assign action|
|Main behavior|Validate target scope; create assignment; change state to ASSIGNED|
|Alternate behavior|Department assignment can trigger AI suggested assignee|
|Exception/failure behavior|Unauthorized target|
|Input|TaskId, target type/id, note|
|Output|Assignment + state change|
|Validation|Manager scope; target active|
|Business rule|Assignment history immutable|
|Permission|tasks.assign|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/assign|
|Notification|Assignee/department notified|
|AI involvement|AI may recommend assignee; human confirmation required for final assignment unless explicit authorized auto-action|
|Dependencies|M05/M12|
|Acceptance criteria|Assigned target can view the task.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-003 — Accept Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-003|
|Title|Accept Task|
|Requirement statement|The system shall Employee confirms receipt of assigned task. Main behavior: Create acceptance record; state ACCEPTED|
|Rationale|Employee confirms receipt of assigned task.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|Task ASSIGNED and user is target|
|Trigger|Accept action|
|Main behavior|Create acceptance record; state ACCEPTED|
|Alternate behavior|Employee can reject assignment with reason -> REJECTED|
|Exception/failure behavior|Already accepted/cancelled|
|Input|TaskId, acceptance/rejection reason|
|Output|Acceptance record + state|
|Validation|Required reason on rejection|
|Business rule|Rejecting assignment does not delete task|
|Permission|tasks.accept|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/accept|
|Notification|Manager notification|
|AI involvement|AI none|
|Dependencies|M05/M09|
|Acceptance criteria|Employee can accept; rejected assignment is auditable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-004 — Execute Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-004|
|Title|Execute Task|
|Requirement statement|The system shall Perform assigned task and record work. Main behavior: Set IN_PROGRESS; allow progress actions|
|Rationale|Perform assigned task and record work.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|Task ACCEPTED/IN_PROGRESS|
|Trigger|Start work|
|Main behavior|Set IN_PROGRESS; allow progress actions|
|Alternate behavior|Task may return from OVERDUE to IN_PROGRESS|
|Exception/failure behavior|Unauthorized/invalid state|
|Input|TaskId|
|Output|Updated state/history|
|Validation|Only assigned/authorized employee can execute|
|Business rule|State transitions enforced|
|Permission|tasks.execute|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/start|
|Notification|Optional realtime update|
|AI involvement|AI can summarize current task context|
|Dependencies|M05|
|Acceptance criteria|Authorized employee can start work.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-005 — Update Task Progress


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-005|
|Title|Update Task Progress|
|Requirement statement|The system shall Record percentage/status/progress note. Main behavior: Validate progress; store report; keep state IN_PROGRESS|
|Rationale|Record percentage/status/progress note.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|Task active|
|Trigger|Progress submit|
|Main behavior|Validate progress; store report; keep state IN_PROGRESS|
|Alternate behavior|Progress can be submitted with evidence|
|Exception/failure behavior|Invalid percent/state|
|Input|TaskId, percent, note, attachments|
|Output|Progress report|
|Validation|0-100 numeric; non-decreasing unless correction reason|
|Business rule|Every progress update timestamped|
|Permission|tasks.progress.update|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/progress|
|Notification|Manager notification according to preferences|
|AI involvement|AI can summarize progress/risk|
|Dependencies|M05/M10|
|Acceptance criteria|Manager can see latest progress and history.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-006 — Submit Progress Report


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-006|
|Title|Submit Progress Report|
|Requirement statement|The system shall Submit a formal progress report. Main behavior: Create report; notify manager; optionally attach evidence|
|Rationale|Submit a formal progress report.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|Task active|
|Trigger|Report submit|
|Main behavior|Create report; notify manager; optionally attach evidence|
|Alternate behavior|Can be repeated until final result|
|Exception/failure behavior|File validation / state rejection|
|Input|TaskId, content, attachments|
|Output|Progress report|
|Validation|Content required; files within policy|
|Business rule|Report immutable after submission except correction record|
|Permission|tasks.progress-report|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/progress-reports|
|Notification|Manager notification|
|AI involvement|AI may summarize report before submit|
|Dependencies|M05/M09/M10/M12|
|Acceptance criteria|Formal report is stored and traceable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-007 — Submit Task Result


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-007|
|Title|Submit Task Result|
|Requirement statement|The system shall Submit final result/evidence for review. Main behavior: Store result; state SUBMITTED; start review milestone|
|Rationale|Submit final result/evidence for review.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|Task IN_PROGRESS/OVERDUE|
|Trigger|Submit result|
|Main behavior|Store result; state SUBMITTED; start review milestone|
|Alternate behavior|Manager may request rework -> task can return to IN_PROGRESS|
|Exception/failure behavior|Invalid state/file|
|Input|TaskId, result text, attachments|
|Output|Submitted result|
|Validation|Result required; attachments validated|
|Business rule|Submit freezes current result revision|
|Permission|tasks.submit|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/result|
|Notification|Manager reviewer notified|
|AI involvement|AI may check completeness and summarize|
|Dependencies|M05/M10/M12|
|Acceptance criteria|Manager receives result for confirmation.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-008 — Confirm Task Result


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-008|
|Title|Confirm Task Result|
|Requirement statement|The system shall Manager confirms final submitted result. Main behavior: Validate evidence; record confirmation; complete task|
|Rationale|Manager confirms final submitted result.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager|
|Preconditions|Task SUBMITTED; manager is reviewer|
|Trigger|Confirm action|
|Main behavior|Validate evidence; record confirmation; complete task|
|Alternate behavior|Reject result -> REJECTED or back to IN_PROGRESS according to configured workflow|
|Exception/failure behavior|Unauthorized reviewer / missing evidence|
|Input|TaskId, decision, note|
|Output|Confirmation record; task state|
|Validation|Reviewer permission; decision required|
|Business rule|Critical milestone requires reviewer confirmation|
|Permission|tasks.confirm|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/confirmation|
|Notification|Employee notified|
|AI involvement|AI can summarize evidence but cannot final-confirm without authorized action policy|
|Dependencies|M05/M07/M10|
|Acceptance criteria|Confirmed result causes task completion according to workflow.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-009 — Reassign Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-009|
|Title|Reassign Task|
|Requirement statement|The system shall Move responsibility to another employee/department. Main behavior: Validate target; close old assignment; create new assignment; preserve history|
|Rationale|Move responsibility to another employee/department.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager|
|Preconditions|Task not completed/cancelled|
|Trigger|Reassign action|
|Main behavior|Validate target; close old assignment; create new assignment; preserve history|
|Alternate behavior|Can use AI recommendation first|
|Exception/failure behavior|Invalid target|
|Input|TaskId, target, reason|
|Output|New active assignment|
|Validation|Target within manager scope|
|Business rule|Reason required; audit old/new assignee|
|Permission|tasks.reassign|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/reassign|
|Notification|Old/new target notifications|
|AI involvement|AI may recommend new assignee based on workload|
|Dependencies|M05/M12/M13|
|Acceptance criteria|Task history shows full assignment chain.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-TASK-010 — Cancel Task


|Field|Value|
|---|---|
|Requirement ID|FR-TASK-010|
|Title|Cancel Task|
|Requirement statement|The system shall Cancel an active task with reason. Main behavior: Validate authority; set CANCELLED; record reason|
|Rationale|Cancel an active task with reason.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager / authorized Company Admin|
|Preconditions|Task not completed|
|Trigger|Cancel action|
|Main behavior|Validate authority; set CANCELLED; record reason|
|Alternate behavior|Completed tasks require special correction/void flow, not cancel|
|Exception/failure behavior|Invalid state|
|Input|TaskId, reason|
|Output|CANCELLED task|
|Validation|Reason required|
|Business rule|Cancelled task cannot be re-opened directly; create new task if needed|
|Permission|tasks.cancel|
|Data impact|Task, assignment/progress/result records|
|API impact|POST /api/v1/tasks/{id}/cancel|
|Notification|Target notification|
|AI involvement|AI cannot autonomously cancel unless explicitly allowed|
|Dependencies|M05/M13|
|Acceptance criteria|Cancelled task is excluded from active workload and retained for audit.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-001 — Create Internal Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-001|
|Title|Create Internal Request|
|Requirement statement|The system shall Create a request using structured form or natural language. Main behavior: Validate service/category and content; create DRAFT or SUBMITTED|
|Rationale|Create a request using structured form or natural language.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee|
|Preconditions|User active in tenant|
|Trigger|Submit draft/request|
|Main behavior|Validate service/category and content; create DRAFT or SUBMITTED|
|Alternate behavior|AI can draft structured fields before submit|
|Exception/failure behavior|Missing required info|
|Input|Service, title, description, attachments, optional priority|
|Output|Request record|
|Validation|Service required unless tenant permits generic request; content required|
|Business rule|Request belongs to tenant and requester|
|Permission|requests.create|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests|
|Notification|Submission notification|
|AI involvement|AI assistance available before/after draft|
|Dependencies|M06/M10/M12|
|Acceptance criteria|Valid request can be submitted and tracked.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-002 — Analyze Request for Routing


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-002|
|Title|Analyze Request for Routing|
|Requirement statement|The system shall Analyze request content for classification/routing. Main behavior: Extract entities; classify service/category; detect missing information; recommend routing|
|Rationale|Analyze request content for classification/routing.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|AI Agent|
|Preconditions|Submitted request; AI feature enabled|
|Trigger|Request submitted or manual AI request|
|Main behavior|Extract entities; classify service/category; detect missing information; recommend routing|
|Alternate behavior|Low confidence -> manual review; missing data -> WAITING_FOR_INFORMATION|
|Exception/failure behavior|AI timeout/provider failure|
|Input|Request content + service catalog + org context|
|Output|Structured AI recommendation|
|Validation|Schema validation; confidence range; references must exist|
|Business rule|No direct DB mutation by model output|
|Permission|request.002|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/ai/requests/analyze|
|Notification|AI processing status|
|AI involvement|Core AI function|
|Dependencies|M12/M04/M06|
|Acceptance criteria|Valid structured recommendation is produced or safe fallback occurs.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-003 — Route Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-003|
|Title|Route Request|
|Requirement statement|The system shall Route request to department/person. Main behavior: Validate target; create routing record; move ROUTED|
|Rationale|Route request to department/person.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager / authorized workflow / AI through approved tool|
|Preconditions|Request SUBMITTED; routing recommendation available or manual routing|
|Trigger|Route action|
|Main behavior|Validate target; create routing record; move ROUTED|
|Alternate behavior|Low-confidence AI output requires human selection|
|Exception/failure behavior|Invalid target / policy|
|Input|RequestId, target, reason|
|Output|Routing record + state|
|Validation|Tenant/permission/routing rule|
|Business rule|Final route stored separately from AI suggestion|
|Permission|requests.route|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/route|
|Notification|Department/person notified|
|AI involvement|AI may recommend; authorized tool can execute with required confirmation|
|Dependencies|M06/M07/M12|
|Acceptance criteria|Request reaches the intended queue/person.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-004 — Receive Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-004|
|Title|Receive Request|
|Requirement statement|The system shall Assigned department/person accepts responsibility. Main behavior: Record receipt; set RECEIVED; begin SLA if configured|
|Rationale|Assigned department/person accepts responsibility.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager assigned recipient|
|Preconditions|Request ROUTED|
|Trigger|Receive/accept action|
|Main behavior|Record receipt; set RECEIVED; begin SLA if configured|
|Alternate behavior|Reject intake with reason can return to ROUTED/manual triage|
|Exception/failure behavior|Already received/invalid assignee|
|Input|RequestId|
|Output|Receipt record/state|
|Validation|Recipient permission; request active|
|Business rule|Receipt timestamp starts processing milestone|
|Permission|requests.receive|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/receive|
|Notification|Requester notified|
|AI involvement|AI none|
|Dependencies|M06/M08|
|Acceptance criteria|Request becomes visibly owned and active.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-005 — Process Assigned Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-005|
|Title|Process Assigned Request|
|Requirement statement|The system shall Work on a request directly or through generated tasks. Main behavior: Create comments/progress; optionally create task; update state|
|Rationale|Work on a request directly or through generated tasks.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager assigned recipient|
|Preconditions|Request RECEIVED/IN_PROGRESS|
|Trigger|Start processing|
|Main behavior|Create comments/progress; optionally create task; update state|
|Alternate behavior|If information missing -> WAITING_FOR_INFORMATION|
|Exception/failure behavior|Unauthorized update/state|
|Input|RequestId, note, task link|
|Output|Updated request/process history|
|Validation|State transition and assignment checks|
|Business rule|Request may have zero or more child tasks|
|Permission|requests.process|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/process-actions|
|Notification|Requester may receive progress notification|
|AI involvement|AI can summarize/recommend next step|
|Dependencies|M06/M05/M09|
|Acceptance criteria|Authorized Employee/Manager can progress request and preserve history.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-006 — Create Task from Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-006|
|Title|Create Task from Request|
|Requirement statement|The system shall Create a task linked to a request. Main behavior: Create task with RequestId; route/assign; preserve parent relation|
|Rationale|Create a task linked to a request.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Manager / authorized Employee/Manager|
|Preconditions|Request processing needs actionable work|
|Trigger|Create-task action|
|Main behavior|Create task with RequestId; route/assign; preserve parent relation|
|Alternate behavior|Multiple tasks can be created|
|Exception/failure behavior|Invalid request state|
|Input|RequestId, task fields|
|Output|Task linked to request|
|Validation|Task inherits tenant/service context|
|Business rule|Request remains open until workflow resolves it|
|Permission|tasks.from-request|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/tasks|
|Notification|Assignee notified|
|AI involvement|AI may draft task fields|
|Dependencies|M05/M06/M12|
|Acceptance criteria|Task appears in request timeline and can be tracked independently.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-007 — Resolve Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-007|
|Title|Resolve Request|
|Requirement statement|The system shall Mark request resolution ready for requester confirmation. Main behavior: Validate completion criteria; set RESOLVED; record resolution|
|Rationale|Mark request resolution ready for requester confirmation.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Assigned Employee/Manager / Manager|
|Preconditions|Request IN_PROGRESS; resolution evidence present when required|
|Trigger|Resolve action|
|Main behavior|Validate completion criteria; set RESOLVED; record resolution|
|Alternate behavior|Workflow may require approval before resolve|
|Exception/failure behavior|Missing evidence/failed validation|
|Input|RequestId, resolution, attachments|
|Output|RESOLVED request|
|Validation|Resolution required; workflow conditions|
|Business rule|Resolution does not equal final close until confirmation|
|Permission|requests.resolve|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/resolve|
|Notification|Requester notified|
|AI involvement|AI can summarize/check completeness|
|Dependencies|M06/M07/M10|
|Acceptance criteria|Requester can review the resolution.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-008 — Confirm Request Resolution


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-008|
|Title|Confirm Request Resolution|
|Requirement statement|The system shall Requester confirms or rejects resolution. Main behavior: Confirm -> CLOSED; reject -> configured rework path/IN_PROGRESS with reason|
|Rationale|Requester confirms or rejects resolution.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee requester|
|Preconditions|Request RESOLVED and requester owns request|
|Trigger|Confirm action|
|Main behavior|Confirm -> CLOSED; reject -> configured rework path/IN_PROGRESS with reason|
|Alternate behavior|Rejection must preserve prior resolution history|
|Exception/failure behavior|Unauthorized requester|
|Input|RequestId, decision, note|
|Output|Confirmation + final state|
|Validation|Requester identity; decision required|
|Business rule|Critical milestone confirmation recorded|
|Permission|requests.confirm|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/confirmation|
|Notification|Assigned Employee/Manager notified|
|AI involvement|AI may summarize resolution but not confirm on behalf of requester without explicit policy|
|Dependencies|M06/M13|
|Acceptance criteria|Confirmed resolution closes the request.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REQ-009 — Create Revised Request from Rejected Request


|Field|Value|
|---|---|
|Requirement ID|FR-REQ-009|
|Title|Create Revised Request from Rejected Request|
|Requirement statement|The system shall Reuse a rejected request as the basis for a new submission. Main behavior: Create new DRAFT request; copy safe fields; link RevisedFromRequestId; allow edits; submit separately|
|Rationale|Reuse a rejected request as the basis for a new submission.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Employee requester / authorized user|
|Preconditions|Original request REJECTED|
|Trigger|Select “Create Revised Request”|
|Main behavior|Create new DRAFT request; copy safe fields; link RevisedFromRequestId; allow edits; submit separately|
|Alternate behavior|User may choose attachments to reuse|
|Exception/failure behavior|Original not rejected / access denied|
|Input|Original RequestId + edited fields|
|Output|New RequestId in DRAFT or SUBMITTED|
|Validation|Original remains immutable; copied fields revalidated|
|Business rule|New request has independent lifecycle and audit|
|Permission|request.009|
|Data impact|Request, routing/resolution/relationship records|
|API impact|POST /api/v1/requests/{id}/revise|
|Notification|New request notification after submit|
|AI involvement|AI may help rewrite/satisfy missing data|
|Dependencies|M06/M10/M13|
|Acceptance criteria|Rejected original remains REJECTED and new request is independently traceable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-WF-001 — Define Workflow Version


|Field|Value|
|---|---|
|Requirement ID|FR-WF-001|
|Title|Define Workflow Version|
|Requirement statement|The system shall Create a versioned workflow with ordered steps/transitions. Main behavior: Validate graph; create immutable version on publish|
|Rationale|Create a versioned workflow with ordered steps/transitions.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Company Administrator|
|Preconditions|Tenant active|
|Trigger|Save/publish workflow|
|Main behavior|Validate graph; create immutable version on publish|
|Alternate behavior|Draft version can be edited before publish|
|Exception/failure behavior|Cycle/invalid transition|
|Input|Workflow metadata, steps, transitions|
|Output|Published workflow version|
|Validation|At least one entry/exit; valid references|
|Business rule|Published versions immutable|
|Permission|workflows.configure|
|Data impact|Workflow/approval runtime and version records|
|API impact|POST /api/v1/workflows; POST /api/v1/workflows/{id}/publish|
|Notification|Config audit|
|AI involvement|AI can recommend but not publish|
|Dependencies|M07|
|Acceptance criteria|Valid workflow version can be selected by a service.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-WF-002 — Execute Workflow Transition


|Field|Value|
|---|---|
|Requirement ID|FR-WF-002|
|Title|Execute Workflow Transition|
|Requirement statement|The system shall Move Task/Request to next valid state according to workflow. Main behavior: Evaluate guards; record transition; trigger side effects|
|Rationale|Move Task/Request to next valid state according to workflow.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|System + authorized actor|
|Preconditions|Object active; transition exists|
|Trigger|Action/event|
|Main behavior|Evaluate guards; record transition; trigger side effects|
|Alternate behavior|Rejected transition returns validation error|
|Exception/failure behavior|Guard failure|
|Input|ObjectId, transition, actor|
|Output|New state + transition log|
|Validation|Current state must match source; actor authorized|
|Business rule|No direct status update bypassing workflow for workflow-controlled objects|
|Permission|workflow.002|
|Data impact|Workflow/approval runtime and version records|
|API impact|POST /api/v1/workflow-instances/{id}/transition|
|Notification|Notifications based on transition|
|AI involvement|AI may request transition via tool but business engine validates|
|Dependencies|M07/M05/M06|
|Acceptance criteria|Invalid transitions are blocked.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-WF-003 — Create Approval Instance


|Field|Value|
|---|---|
|Requirement ID|FR-WF-003|
|Title|Create Approval Instance|
|Requirement statement|The system shall Start approval process for object. Main behavior: Resolve approvers; create approval instance/steps; notify|
|Rationale|Start approval process for object.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|System / Manager|
|Preconditions|Workflow step requires approval|
|Trigger|Enter approval step|
|Main behavior|Resolve approvers; create approval instance/steps; notify|
|Alternate behavior|If no approver found -> blocked/manual escalation|
|Exception/failure behavior|Unavailable approver|
|Input|ObjectId, rule version|
|Output|Approval instance|
|Validation|Approver active and in scope|
|Business rule|Approval rule snapshot preserved on instance|
|Permission|workflow.003|
|Data impact|Workflow/approval runtime and version records|
|API impact|POST /api/v1/approvals|
|Notification|Approver notification|
|AI involvement|AI may recommend but not approve|
|Dependencies|M07/M09|
|Acceptance criteria|Approvers receive an actionable approval request.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-WF-004 — Record Approval Decision


|Field|Value|
|---|---|
|Requirement ID|FR-WF-004|
|Title|Record Approval Decision|
|Requirement statement|The system shall Approver approves or rejects an approval step. Main behavior: Validate decision; record; advance or reject workflow|
|Rationale|Approver approves or rejects an approval step.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager / authorized approver|
|Preconditions|Pending approval; user in approver list|
|Trigger|Decision submit|
|Main behavior|Validate decision; record; advance or reject workflow|
|Alternate behavior|Reject sends object to REJECTED or revision path per workflow|
|Exception/failure behavior|Duplicate decision/expired approval|
|Input|ApprovalId, decision, reason|
|Output|Approval action + workflow result|
|Validation|Reason required on reject; idempotency|
|Business rule|Decision cannot be overwritten; correction is new action|
|Permission|workflow.004|
|Data impact|Workflow/approval runtime and version records|
|API impact|POST /api/v1/approvals/{id}/decision|
|Notification|Requester/next approver notified|
|AI involvement|AI can summarize context only|
|Dependencies|M07/M13|
|Acceptance criteria|Approval decision changes workflow correctly and is auditable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SLA-001 — Configure SLA Profile


|Field|Value|
|---|---|
|Requirement ID|FR-SLA-001|
|Title|Configure SLA Profile|
|Requirement statement|The system shall Define processing target and warning/escalation rules. Main behavior: Set duration, calendar, warning thresholds, escalation actions|
|Rationale|Define processing target and warning/escalation rules.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Company Administrator|
|Preconditions|Tenant active|
|Trigger|Save SLA|
|Main behavior|Set duration, calendar, warning thresholds, escalation actions|
|Alternate behavior|Version when used by active objects|
|Exception/failure behavior|Invalid duration/calendar|
|Input|Service/Request type, target, warning, escalation|
|Output|SLA profile version|
|Validation|Nonnegative duration; valid calendar|
|Business rule|Version immutable for active transactions|
|Permission|sla.configure|
|Data impact|SLA profile/runtime/escalation records|
|API impact|POST /api/v1/sla-profiles|
|Notification|Config audit|
|AI involvement|AI can recommend SLA but cannot publish automatically|
|Dependencies|M08|
|Acceptance criteria|Published SLA profile is selectable by service/workflow.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SLA-002 — Monitor SLA Clock


|Field|Value|
|---|---|
|Requirement ID|FR-SLA-002|
|Title|Monitor SLA Clock|
|Requirement statement|The system shall Track elapsed processing time. Main behavior: Calculate elapsed time under tenant calendar; update SLA state|
|Rationale|Track elapsed processing time.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|System|
|Preconditions|Object has active SLA|
|Trigger|Scheduler tick/event|
|Main behavior|Calculate elapsed time under tenant calendar; update SLA state|
|Alternate behavior|Paused when configured waiting state|
|Exception/failure behavior|Clock calculation failure|
|Input|ObjectId, timestamps, calendar|
|Output|SLA metrics/state|
|Validation|Consistent timezone/calendar|
|Business rule|One authoritative server time source|
|Permission|sla.002|
|Data impact|SLA profile/runtime/escalation records|
|API impact|Background job/internal service|
|Notification|Warning notification when threshold reached|
|AI involvement|AI receives risk context|
|Dependencies|M08|
|Acceptance criteria|SLA state is updated predictably.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SLA-003 — Escalate Overdue Work


|Field|Value|
|---|---|
|Requirement ID|FR-SLA-003|
|Title|Escalate Overdue Work|
|Requirement statement|The system shall Escalate when SLA/deadline is violated. Main behavior: Create escalation event; notify target; preserve previous owner|
|Rationale|Escalate when SLA/deadline is violated.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|System|
|Preconditions|Object overdue and escalation configured|
|Trigger|Scheduler detects overdue|
|Main behavior|Create escalation event; notify target; preserve previous owner|
|Alternate behavior|Repeated scheduler run must not duplicate escalation|
|Exception/failure behavior|Notification failure|
|Input|ObjectId, escalation rule|
|Output|Escalation event|
|Validation|Idempotent event key|
|Business rule|At most one escalation per configured level|
|Permission|sla.003|
|Data impact|SLA profile/runtime/escalation records|
|API impact|Internal job|
|Notification|Manager/Admin notification|
|AI involvement|AI may recommend follow-up, not replace rule engine|
|Dependencies|M08/M09/M13|
|Acceptance criteria|Overdue item is escalated exactly once per level.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-SLA-004 — Pause SLA on Waiting for Information


|Field|Value|
|---|---|
|Requirement ID|FR-SLA-004|
|Title|Pause SLA on Waiting for Information|
|Requirement statement|The system shall Pause SLA when workflow allows requester clarification state. Main behavior: Freeze SLA clock; record pause reason/time; resume on response|
|Rationale|Pause SLA when workflow allows requester clarification state.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|System|
|Preconditions|Request WAITING_FOR_INFORMATION and policy says pause|
|Trigger|State change|
|Main behavior|Freeze SLA clock; record pause reason/time; resume on response|
|Alternate behavior|If policy says no pause, clock continues|
|Exception/failure behavior|Invalid state/policy|
|Input|ObjectId, reason|
|Output|SLA pause record|
|Validation|State must be configured as pausable|
|Business rule|Total paused duration traceable|
|Permission|sla.004|
|Data impact|SLA profile/runtime/escalation records|
|API impact|Internal SLA service|
|Notification|Requester prompt/owner notification|
|AI involvement|AI may detect missing info|
|Dependencies|M08/M06|
|Acceptance criteria|Configured pause policy is honored.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-COL-001 — Create Comment


|Field|Value|
|---|---|
|Requirement ID|FR-COL-001|
|Title|Create Comment|
|Requirement statement|The system shall Add collaboration comment to Task/Request. Main behavior: Store comment; attach optional files; notify relevant users|
|Rationale|Add collaboration comment to Task/Request.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager/Company Admin|
|Preconditions|Authorized access|
|Trigger|Comment submit|
|Main behavior|Store comment; attach optional files; notify relevant users|
|Alternate behavior|Edit/delete only within comment policy|
|Exception/failure behavior|Empty comment|
|Input|ObjectId, content, attachments|
|Output|Comment|
|Validation|Content required|
|Business rule|Comment history retained|
|Permission|comments.create|
|Data impact|Comment/Attachment/archive metadata|
|API impact|POST /api/v1/{tasks\|requests}/{id}/comments|
|Notification|Relevant users notified|
|AI involvement|AI may draft/rewrite but user submits|
|Dependencies|M09/M10|
|Acceptance criteria|Comment appears in timeline and is auditable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-COL-002 — Upload Attachment


|Field|Value|
|---|---|
|Requirement ID|FR-COL-002|
|Title|Upload Attachment|
|Requirement statement|The system shall Upload evidence file up to 500 MB. Main behavior: Request signed upload; upload direct to object storage; finalize metadata|
|Rationale|Upload evidence file up to 500 MB.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager/Company Admin|
|Preconditions|Authorized object access|
|Trigger|Upload initiation|
|Main behavior|Request signed upload; upload direct to object storage; finalize metadata|
|Alternate behavior|Large files use multipart/resumable upload|
|Exception/failure behavior|Type/size/storage failure|
|Input|File metadata + binary|
|Output|Attachment record|
|Validation|Allowlist; max 500 MB; malware/content validation hook|
|Business rule|Tenant-prefixed object key; metadata in DB|
|Permission|collaboration.002|
|Data impact|Comment/Attachment/archive metadata|
|API impact|POST /api/v1/attachments/upload-session; POST /finalize|
|Notification|Upload complete notification where needed|
|AI involvement|AI can inspect metadata/content only if feature enabled|
|Dependencies|M10|
|Acceptance criteria|500 MB file can be uploaded without proxying entire binary through API.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-COL-003 — Archive Completed Record


|Field|Value|
|---|---|
|Requirement ID|FR-COL-003|
|Title|Archive Completed Record|
|Requirement statement|The system shall Move closed/completed records out of active views. Main behavior: Set archived timestamp; remove from active lists; retain history|
|Rationale|Move closed/completed records out of active views.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Authorized Manager/Company Admin|
|Preconditions|Task COMPLETED or Request CLOSED|
|Trigger|Archive action/retention job|
|Main behavior|Set archived timestamp; remove from active lists; retain history|
|Alternate behavior|Cannot archive while pending approval/confirmation|
|Exception/failure behavior|Invalid state|
|Input|ObjectId|
|Output|Archived record|
|Validation|Only closed states|
|Business rule|Archive is not hard delete|
|Permission|collaboration.003|
|Data impact|Comment/Attachment/archive metadata|
|API impact|POST /api/v1/{tasks\|requests}/{id}/archive|
|Notification|None|
|AI involvement|AI summaries may exclude archived data by default|
|Dependencies|M05/M06/M13|
|Acceptance criteria|Archived records remain searchable according to permission.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-COL-004 — Search and Filter Records


|Field|Value|
|---|---|
|Requirement ID|FR-COL-004|
|Title|Search and Filter Records|
|Requirement statement|The system shall Search/filter Task and Request data in authorized scope. Main behavior: Build scoped query; filter by status, service, department, assignee, dates, priority|
|Rationale|Search/filter Task and Request data in authorized scope.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P0 / Must|
|Release scope|MVP|
|Actor|Employee/Manager/Company Admin|
|Preconditions|Authenticated|
|Trigger|Search submit|
|Main behavior|Build scoped query; filter by status, service, department, assignee, dates, priority|
|Alternate behavior|No results returns empty set|
|Exception/failure behavior|Invalid filter|
|Input|Query, filters, paging|
|Output|Paged results|
|Validation|All filters tenant-scoped; pagination bounds|
|Business rule|Search never bypasses authorization|
|Permission|collaboration.004|
|Data impact|Comment/Attachment/archive metadata|
|API impact|GET /api/v1/tasks; GET /api/v1/requests|
|Notification|None|
|AI involvement|AI can translate natural-language search to filters as P2|
|Dependencies|M05/M06|
|Acceptance criteria|Search returns only authorized records with stable pagination.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REP-001 — View Manager Dashboard


|Field|Value|
|---|---|
|Requirement ID|FR-REP-001|
|Title|View Manager Dashboard|
|Requirement statement|The system shall Show workload and task/request operational metrics. Main behavior: Aggregate counts, workload, overdue, completion, SLA metrics|
|Rationale|Show workload and task/request operational metrics.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager|
|Preconditions|Authenticated|
|Trigger|Open dashboard|
|Main behavior|Aggregate counts, workload, overdue, completion, SLA metrics|
|Alternate behavior|Drill-down only to authorized data|
|Exception/failure behavior|Aggregation failure|
|Input|Date range + department filters|
|Output|Dashboard metrics|
|Validation|Tenant scope; time range limits|
|Business rule|Metrics use a documented definition|
|Permission|report.001|
|Data impact|Read-only aggregation/report queries|
|API impact|GET /api/v1/dashboard/manager|
|Notification|None|
|AI involvement|AI summary optional|
|Dependencies|M11|
|Acceptance criteria|Manager dashboard metrics match underlying data.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REP-002 — View Company Report


|Field|Value|
|---|---|
|Requirement ID|FR-REP-002|
|Title|View Company Report|
|Requirement statement|The system shall View company-level operational report. Main behavior: Aggregate tenant-wide metrics within role|
|Rationale|View company-level operational report.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Company Administrator|
|Preconditions|Authenticated|
|Trigger|Open report|
|Main behavior|Aggregate tenant-wide metrics within role|
|Alternate behavior|Large range may use async export|
|Exception/failure behavior|Aggregation failure|
|Input|Date range, filters|
|Output|Report data|
|Validation|Tenant scope; metric definition|
|Business rule|No cross-tenant aggregation|
|Permission|report.002|
|Data impact|Read-only aggregation/report queries|
|API impact|GET /api/v1/reports/company|
|Notification|Optional report notification|
|AI involvement|AI can summarize|
|Dependencies|M11/M13|
|Acceptance criteria|Company Admin sees tenant-wide report only.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REP-003 — Generate Workload Report


|Field|Value|
|---|---|
|Requirement ID|FR-REP-003|
|Title|Generate Workload Report|
|Requirement statement|The system shall Calculate workload by user/department. Main behavior: Aggregate active and recently completed tasks; configurable period|
|Rationale|Calculate workload by user/department.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager/Company Admin|
|Preconditions|Authorized report scope|
|Trigger|Generate report|
|Main behavior|Aggregate active and recently completed tasks; configurable period|
|Alternate behavior|If no historical data, return zero/empty|
|Exception/failure behavior|Invalid dates|
|Input|Date range, department|
|Output|Workload report|
|Validation|Range <= configurable max|
|Business rule|Exclude cancelled from active workload|
|Permission|report.003|
|Data impact|Read-only aggregation/report queries|
|API impact|GET /api/v1/reports/workload|
|Notification|None|
|AI involvement|AI can explain outliers|
|Dependencies|M11|
|Acceptance criteria|Workload report matches task assignments and statuses.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-REP-004 — Generate SLA Performance Report


|Field|Value|
|---|---|
|Requirement ID|FR-REP-004|
|Title|Generate SLA Performance Report|
|Requirement statement|The system shall Report on SLA compliance. Main behavior: Compute compliance, overdue count, average elapsed/paused time|
|Rationale|Report on SLA compliance.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager/Company Admin|
|Preconditions|Authorized report scope|
|Trigger|Generate report|
|Main behavior|Compute compliance, overdue count, average elapsed/paused time|
|Alternate behavior|Incomplete SLA records flagged|
|Exception/failure behavior|Missing timestamps|
|Input|Date range, service|
|Output|SLA report|
|Validation|Well-defined metric formulas|
|Business rule|Uses server timestamps and SLA snapshots|
|Permission|report.004|
|Data impact|Read-only aggregation/report queries|
|API impact|GET /api/v1/reports/sla|
|Notification|None|
|AI involvement|AI can summarize trends|
|Dependencies|M08/M11|
|Acceptance criteria|SLA metrics are reproducible from stored records.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-001 — Assist Task Creation


|Field|Value|
|---|---|
|Requirement ID|FR-AI-001|
|Title|Assist Task Creation|
|Requirement statement|The system shall Turn natural-language manager instruction into a structured draft. Main behavior: Gather tenant context; call LLM; validate schema; show draft fields|
|Rationale|Turn natural-language manager instruction into a structured draft.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager + AI Agent|
|Preconditions|AI enabled|
|Trigger|User requests AI assistance|
|Main behavior|Gather tenant context; call LLM; validate schema; show draft fields|
|Alternate behavior|Missing details highlighted for user|
|Exception/failure behavior|LLM failure/schema mismatch|
|Input|Natural-language task instruction + allowed context|
|Output|TaskDraftSuggestion|
|Validation|Strict JSON schema; field/reference validation|
|Business rule|AI output never directly persists task without policy|
|Permission|ai.001|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-assistance|
|Notification|AI activity status|
|AI involvement|Core AI|
|Dependencies|M12/M05|
|Acceptance criteria|AI returns editable task draft with structured fields or safe failure.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-002 — Recommend Task Assignment


|Field|Value|
|---|---|
|Requirement ID|FR-AI-002|
|Title|Recommend Task Assignment|
|Requirement statement|The system shall Recommend department/assignee for a task. Main behavior: Use task content + org responsibilities + workload + history; score candidates|
|Rationale|Recommend department/assignee for a task.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager + AI Agent|
|Preconditions|Task draft/context available|
|Trigger|Request recommendation|
|Main behavior|Use task content + org responsibilities + workload + history; score candidates|
|Alternate behavior|No suitable candidate -> manual selection|
|Exception/failure behavior|Data unavailable/low confidence|
|Input|Task draft + candidate context|
|Output|Recommendation with confidence + reasons|
|Validation|Referenced users/departments valid; confidence 0-1|
|Business rule|Recommendation not final assignment by default|
|Permission|ai.002|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-assignment-recommendation|
|Notification|Manager notification only if configured|
|AI involvement|Core AI|
|Dependencies|M12/M03/M05|
|Acceptance criteria|Manager can accept/override recommendation and final assignment is logged.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-003 — Recommend Task Parameters


|Field|Value|
|---|---|
|Requirement ID|FR-AI-003|
|Title|Recommend Task Parameters|
|Requirement statement|The system shall Suggest priority, deadline, checklist, workflow and SLA. Main behavior: Return structured suggestions grounded in service/workflow catalog|
|Rationale|Suggest priority, deadline, checklist, workflow and SLA.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager + AI Agent|
|Preconditions|Task content/service context|
|Trigger|AI assistance|
|Main behavior|Return structured suggestions grounded in service/workflow catalog|
|Alternate behavior|Omit uncertain fields rather than invent|
|Exception/failure behavior|Invalid enum/date|
|Input|Task text + service catalog + workflow metadata|
|Output|Recommendation payload|
|Validation|Schema + catalog validation|
|Business rule|AI may suggest only options that exist in tenant|
|Permission|ai.003|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-parameters|
|Notification|None|
|AI involvement|Core AI|
|Dependencies|M12/M04/M07/M08|
|Acceptance criteria|Every recommendation references a valid tenant-configured option or is marked unavailable.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-004 — Break Down Task


|Field|Value|
|---|---|
|Requirement ID|FR-AI-004|
|Title|Break Down Task|
|Requirement statement|The system shall Propose subtasks for a complex task. Main behavior: Generate ordered subtask draft list with dependencies|
|Rationale|Propose subtasks for a complex task.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager + AI Agent|
|Preconditions|Task draft/active and feature enabled|
|Trigger|Ask AI to break down|
|Main behavior|Generate ordered subtask draft list with dependencies|
|Alternate behavior|Single task if decomposition not justified|
|Exception/failure behavior|Too many/duplicate subtasks|
|Input|Task description/context|
|Output|Subtask plan|
|Validation|Max subtask count; no cyclic dependencies|
|Business rule|User reviews before creation|
|Permission|ai.004|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-breakdown|
|Notification|None|
|AI involvement|Core AI|
|Dependencies|M12/M05|
|Acceptance criteria|Subtasks can be edited and approved before creation.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-005 — Monitor Task Risk


|Field|Value|
|---|---|
|Requirement ID|FR-AI-005|
|Title|Monitor Task Risk|
|Requirement statement|The system shall Identify tasks at risk of missing deadline/SLA. Main behavior: Analyze current progress against time remaining; produce risk and reasons|
|Rationale|Identify tasks at risk of missing deadline/SLA.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|AI Agent / Manager|
|Preconditions|Task active; progress/SLA data available|
|Trigger|Scheduled evaluation/dashboard query|
|Main behavior|Analyze current progress against time remaining; produce risk and reasons|
|Alternate behavior|Insufficient data -> unknown risk|
|Exception/failure behavior|AI timeout|
|Input|Task progress + deadline/SLA context|
|Output|Risk recommendation|
|Validation|Schema; bounded scores; no state mutation|
|Business rule|Rule engine remains authoritative for overdue state|
|Permission|ai.005|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-risk|
|Notification|Optional manager alert|
|AI involvement|Core AI|
|Dependencies|M12/M08|
|Acceptance criteria|Risk result is informative and does not override official SLA state.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-006 — Summarize Task Progress


|Field|Value|
|---|---|
|Requirement ID|FR-AI-006|
|Title|Summarize Task Progress|
|Requirement statement|The system shall Summarize progress/history into a concise update. Main behavior: Gather task events/reports; summarize facts; cite relevant dates/actors|
|Rationale|Summarize progress/history into a concise update.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Manager/Employee + AI Agent|
|Preconditions|User authorized to view task|
|Trigger|Summary request|
|Main behavior|Gather task events/reports; summarize facts; cite relevant dates/actors|
|Alternate behavior|If no history, state insufficient data|
|Exception/failure behavior|AI failure|
|Input|Task timeline|
|Output|Summary text/structured bullets|
|Validation|No fabricated events; output based on retrieved context|
|Business rule|Only authorized task data included|
|Permission|ai.006|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/task-summary|
|Notification|None|
|AI involvement|Core AI|
|Dependencies|M05/M13|
|Acceptance criteria|Summary matches stored timeline within tested factual checks.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-007 — Analyze Multi-intent Request


|Field|Value|
|---|---|
|Requirement ID|FR-AI-007|
|Title|Analyze Multi-intent Request|
|Requirement statement|The system shall Detect multiple distinct business intents in one request. Main behavior: Detect intents; propose split; preserve parent relation|
|Rationale|Detect multiple distinct business intents in one request.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Employee/Manager + AI Agent|
|Preconditions|Request submitted|
|Trigger|AI analysis|
|Main behavior|Detect intents; propose split; preserve parent relation|
|Alternate behavior|Single intent -> no split recommendation|
|Exception/failure behavior|AI failure/low confidence|
|Input|Request content + service catalog|
|Output|Intent list + confidence|
|Validation|Schema; min/max intent count; no invented data|
|Business rule|Original request remains intact|
|Permission|ai.007|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/request-multi-intent|
|Notification|Employee/manager review prompt|
|AI involvement|Core AI|
|Dependencies|M12/M06|
|Acceptance criteria|Complex request is split only after validation/review.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-008 — Split Request into Child Requests


|Field|Value|
|---|---|
|Requirement ID|FR-AI-008|
|Title|Split Request into Child Requests|
|Requirement statement|The system shall Create proposed child requests for each intent. Main behavior: Create child drafts linked to parent; copy only safe context|
|Rationale|Create proposed child requests for each intent.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|AI Agent + authorized human|
|Preconditions|Multi-intent recommendation exists|
|Trigger|Confirm split|
|Main behavior|Create child drafts linked to parent; copy only safe context|
|Alternate behavior|Human can merge/edit before submit|
|Exception/failure behavior|Duplicate child/conflicting routing|
|Input|ParentId + intent data|
|Output|Child request drafts|
|Validation|Each child has valid service/category or marked for manual routing|
|Business rule|Children have independent lifecycle; parent remains traceable|
|Permission|ai.008|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/requests/{id}/split|
|Notification|Child draft notification optional|
|AI involvement|Core AI|
|Dependencies|M06/M12|
|Acceptance criteria|No child is created without parent relation and audit record.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-009 — Recommend Request Routing


|Field|Value|
|---|---|
|Requirement ID|FR-AI-009|
|Title|Recommend Request Routing|
|Requirement statement|The system shall Recommend service/category/department/person/priority/workflow/SLA. Main behavior: Retrieve allowed candidates; generate structured recommendation; validate|
|Rationale|Recommend service/category/department/person/priority/workflow/SLA.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|Employee/Manager + AI Agent|
|Preconditions|Request content and tenant configuration available|
|Trigger|AI analysis|
|Main behavior|Retrieve allowed candidates; generate structured recommendation; validate|
|Alternate behavior|Low confidence -> manual routing|
|Exception/failure behavior|Invalid recommendation|
|Input|Request + service/org/workflow/SLA context|
|Output|Recommendation payload|
|Validation|All IDs same tenant; confidence bounded|
|Business rule|Final routing requires human/workflow authority|
|Permission|ai.009|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/request-routing|
|Notification|Manual review notification|
|AI involvement|Core AI|
|Dependencies|M04/M06/M07/M08/M12|
|Acceptance criteria|Recommendation is actionable but does not bypass authorization.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


### FR-AI-010 — Execute Authorized AI Action


|Field|Value|
|---|---|
|Requirement ID|FR-AI-010|
|Title|Execute Authorized AI Action|
|Requirement statement|The system shall Allow AI to invoke a controlled business tool. Main behavior: Validate tool schema; authorize current user/tenant; execute application service; log action|
|Rationale|Allow AI to invoke a controlled business tool.|
|Source|SSS v1.2 FINAL, Section 7|
|Status|Confirmed in SSS v1.2; implementation not verified|
|Priority|P1 / Should|
|Release scope|MVP (priority P1)|
|Actor|AI Agent|
|Preconditions|Tool enabled; current user has authority; action policy allows|
|Trigger|Tool call|
|Main behavior|Validate tool schema; authorize current user/tenant; execute application service; log action|
|Alternate behavior|Requires confirmation -> pause and request human confirmation|
|Exception/failure behavior|Tool denied, business-rule failure, stale data|
|Input|Tool name + structured args + user/tenant context|
|Output|Tool result|
|Validation|Strict schema; permission; tenant; business rules; idempotency|
|Business rule|AI never accesses DB directly|
|Permission|ai.010|
|Data impact|AIInteraction/Recommendation/AIAgentAction records|
|API impact|POST /api/v1/ai/actions/execute|
|Notification|Action result + audit|
|AI involvement|Core AI|
|Dependencies|M12 + target module|
|Acceptance criteria|Unauthorized or invalid actions are rejected and logged.|
|Verification method|Test + inspection/demo where applicable; actual test execution not verified|


## 2. Non-functional requirements



### NFR-PERF-001 — API responsiveness


|Field|Value|
|---|---|
|Requirement ID|NFR-PERF-001|
|Requirement statement|[PROPOSED] 95% of standard read/write API calls should complete within 2 seconds under demo-scale load, excluding external AI/file upload latency.|
|Source|SSS v1.2, Section 21|
|Status|Proposed in SSS v1.2; approval not evidenced|
|Priority|Should|
|Release scope|MVP proposal pending approval|
|Acceptance / verification|Verify by test/inspection/analysis. Numerical thresholds in [PROPOSED] requirements require stakeholder approval and a defined workload/environment before they can be release gates.|


### NFR-PERF-002 — AI latency


|Field|Value|
|---|---|
|Requirement ID|NFR-PERF-002|
|Requirement statement|[PROPOSED] Show processing state for AI calls; define timeout and fallback rather than blocking indefinitely.|
|Source|SSS v1.2, Section 21|
|Status|Proposed in SSS v1.2; approval not evidenced|
|Priority|Should|
|Release scope|MVP proposal pending approval|
|Acceptance / verification|Verify by test/inspection/analysis. Numerical thresholds in [PROPOSED] requirements require stakeholder approval and a defined workload/environment before they can be release gates.|


### NFR-SCAL-001 — Tenant growth


|Field|Value|
|---|---|
|Requirement ID|NFR-SCAL-001|
|Requirement statement|[PROPOSED] Schema and services should support more tenants without tenant-specific code branches.|
|Source|SSS v1.2, Section 21|
|Status|Proposed in SSS v1.2; approval not evidenced|
|Priority|Should|
|Release scope|MVP proposal pending approval|
|Acceptance / verification|Verify by test/inspection/analysis. Numerical thresholds in [PROPOSED] requirements require stakeholder approval and a defined workload/environment before they can be release gates.|


### NFR-SEC-001 — Tenant isolation


|Field|Value|
|---|---|
|Requirement ID|NFR-SEC-001|
|Requirement statement|Mandatory; no cross-tenant data access.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Must|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-SEC-002 — Least privilege


|Field|Value|
|---|---|
|Requirement ID|NFR-SEC-002|
|Requirement statement|Mandatory; every sensitive action requires explicit permission.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Must|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-REL-001 — Idempotency


|Field|Value|
|---|---|
|Requirement ID|NFR-REL-001|
|Requirement statement|Required for finalization, confirmations, scheduler-triggered notifications and AI actions where duplicate execution is possible.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Must|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-MAINT-001 — Modularity


|Field|Value|
|---|---|
|Requirement ID|NFR-MAINT-001|
|Requirement statement|Modules must have clear application/domain boundaries even in monolith.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Should|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-OBS-001 — Observability


|Field|Value|
|---|---|
|Requirement ID|NFR-OBS-001|
|Requirement statement|Structured logs with correlation IDs required; traces/metrics recommended.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Must|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-USAB-001 — Usability


|Field|Value|
|---|---|
|Requirement ID|NFR-USAB-001|
|Requirement statement|Core task/request flows should be understandable without training documentation.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Should|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-DATA-001 — Auditability


|Field|Value|
|---|---|
|Requirement ID|NFR-DATA-001|
|Requirement statement|Critical lifecycle/security events are queryable and immutable.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Should|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


### NFR-FILE-001 — Attachment


|Field|Value|
|---|---|
|Requirement ID|NFR-FILE-001|
|Requirement statement|Support files up to 500 MB using direct object storage upload.|
|Source|SSS v1.2, Section 21|
|Status|Confirmed in SSS v1.2|
|Priority|Should|
|Release scope|MVP|
|Acceptance / verification|Derive a test from the stated control; execution status is not verified.|


## 3. Security requirements



### SEC-001 — Enforce tenant scope on business access


|Field|Value|
|---|---|
|Requirement ID|SEC-001|
|Requirement statement|Every tenant-owned read or mutation shall resolve the authenticated user’s current tenant scope and reject cross-tenant access.|
|Source|BR-001; FR-TEN-004; NFR-SEC-001|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-002 — Authorize each protected action on the server


|Field|Value|
|---|---|
|Requirement ID|SEC-002|
|Requirement statement|The backend shall evaluate action-level and resource-level permission for every protected operation; frontend visibility alone shall not authorize access.|
|Source|BR-002; Role & Permission Matrix; SSS Security Specification|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-003 — Constrain relational references to the same tenant


|Field|Value|
|---|---|
|Requirement ID|SEC-003|
|Requirement statement|Tenant-owned foreign references shall be validated to belong to the same tenant unless the relationship is explicitly platform-global.|
|Source|SSS Validation Rules; BR-001|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-004 — Restrict platform-administration scope


|Field|Value|
|---|---|
|Requirement ID|SEC-004|
|Requirement statement|Platform-wide access shall be explicitly scoped to platform-admin capabilities; tenant administrators shall remain tenant-scoped and administrative access shall be auditable.|
|Source|Actor model; FR-TEN-003; BR-020|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-005 — Protect attachment objects and downloads


|Field|Value|
|---|---|
|Requirement ID|SEC-005|
|Requirement statement|Attachment metadata and object access shall be authorized against the associated tenant and business record before upload finalization or download.|
|Source|BR-013; FR-COL-002; SSS Security Specification|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-006 — Isolate AI context by tenant and user permissions


|Field|Value|
|---|---|
|Requirement ID|SEC-006|
|Requirement statement|AI retrieval and prompts shall include only context the initiating user is authorized to access within the active tenant.|
|Source|BR-006; AI Context; AI Guardrails|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-007 — Execute AI actions through allowlisted application services


|Field|Value|
|---|---|
|Requirement ID|SEC-007|
|Requirement statement|AI shall not access or write directly to the database; all actions shall pass through registered tools/application services and normal authorization/business validation.|
|Source|BR-007; FR-AI-010; Implementation Contract|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-008 — Validate AI-generated values before use


|Field|Value|
|---|---|
|Requirement ID|SEC-008|
|Requirement statement|Server-side handlers shall validate model-generated IDs, enums, dates, states and permission-sensitive arguments against current tenant data and business rules.|
|Source|AI Input/Output Rules; BR-005/006/021|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-009 — Preserve audit history integrity


|Field|Value|
|---|---|
|Requirement ID|SEC-009|
|Requirement statement|Critical lifecycle and security events shall be queryable within authorized scope; audit records shall be append-only through normal business APIs.|
|Source|BR-015; BR-020; NFR-DATA-001|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-010 — Protect identity and session lifecycle


|Field|Value|
|---|---|
|Requirement ID|SEC-010|
|Requirement statement|Authentication shall reject inactive/locked accounts; refresh/reset credentials shall be validated, revocable where specified, and one-time reset tokens shall not be reusable.|
|Source|FR-AUTH-001..004; SSS Security Specification|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-011 — Validate and isolate uploaded files


|Field|Value|
|---|---|
|Requirement ID|SEC-011|
|Requirement statement|Uploads shall enforce the documented 500 MB per-file limit, allowed content types and tenant-scoped object keys; failures shall not expose unauthorized stored objects.|
|Source|BR-013/014; FR-COL-002; NFR-FILE-001|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


### SEC-012 — Prevent ID-based access-control bypass


|Field|Value|
|---|---|
|Requirement ID|SEC-012|
|Requirement statement|A client-supplied resource identifier shall not, by itself, grant access; the server shall verify tenant ownership and the actor’s authorization for the identified resource.|
|Source|FR-TEN-004; Security Specification; TST-SEC-001|
|Status|Derived from explicitly stated baseline security rules; implementation not verified|
|Priority|Must|
|Release scope|MVP|
|Acceptance criteria|Test allowed and denied cases, including negative cross-tenant/resource-access cases; assert no unauthorized data or side effect.|
|Verification method|Integration/security test + code inspection|


## 4. Business rules (22)


|Rule ID|Rule|Statement|
|---|---|---|
|BR-001|Tenant isolation|Every tenant-owned record has TenantId and every business query/action is evaluated against current tenant scope.|
|BR-002|Role boundary|System roles establish baseline capabilities; custom tenant roles can only grant allowed permission catalog items.|
|BR-003|Manager scope|A Manager can assign/reassign only within configured management scope.|
|BR-004|Department assignment|A task/request assigned to a department represents a department queue/team responsibility until a user accepts or is assigned.|
|BR-005|AI recommendation|AI recommendations are not final business decisions unless a tenant-configured policy explicitly permits automatic action.|
|BR-006|AI action authorization|Every AI tool call is evaluated using current user identity, tenant, permission, resource scope and business rules.|
|BR-007|AI direct DB access|AI never writes to the database directly; all mutations go through application services/tools.|
|BR-008|Request revision|REJECTED Request is immutable as a historical transaction; revision creates a new Request linked through RevisedFromRequestId.|
|BR-009|Workflow immutability|Published workflow versions cannot be edited in place when referenced by active transactions.|
|BR-010|SLA versioning|Active transactions use an SLA snapshot/version, so later configuration changes do not alter historical calculations.|
|BR-011|Approval audit|Approval decisions are append-only actions; no in-place overwrite.|
|BR-012|Critical confirmation|Two-sided confirmation is required only at configured critical milestones such as acceptance, result confirmation or request resolution confirmation.|
|BR-013|Evidence|Evidence attachment metadata is tenant-scoped; object storage keys include tenant context.|
|BR-014|File size|No uploaded file may exceed 500 MB.|
|BR-015|Soft delete|Business records use soft delete/deactivation where deletion is allowed; audit logs are immutable.|
|BR-016|Completion|A task cannot be COMPLETED before required result submission and configured confirmation.|
|BR-017|Request closure|A request cannot be CLOSED until resolution confirmation and required workflow conditions are satisfied.|
|BR-018|Overdue|OVERDUE is system-detected; users cannot manually mark a record overdue.|
|BR-019|Notification idempotency|Repeated scheduler runs must not duplicate the same notification/escalation event.|
|BR-020|Audit coverage|Security-sensitive and lifecycle-changing actions must create audit records.|
|BR-021|AI confidence|Confidence is advisory metadata; it cannot by itself override business rules.|
|BR-022|Missing information|When required data is missing and the workflow supports waiting, request may enter WAITING_FOR_INFORMATION; SLA pause follows tenant SLA policy.|

## 5. API inventory extracted from SSS v1.2

> API entries below are the canonical SSS contract inventory, not a claim that routes exist in the current source code.


|API ID|Method|Path|Purpose|Actor/scope|Domain|
|---|---|---|---|---|---|
|API-AUTH-01|POST|/api/v1/auth/login|Login with employee code OR company email + password|Anonymous|Auth|
|API-AUTH-02|POST|/api/v1/auth/refresh|Rotate refresh token and issue access token|Authenticated|Auth|
|API-AUTH-03|POST|/api/v1/auth/forgot-password|Start password reset|Anonymous|Auth|
|API-AUTH-04|POST|/api/v1/auth/reset-password|Complete password reset|Reset token|Auth|
|API-TEN-01|POST|/api/v1/company-registrations|Submit company registration|Anonymous/Onboarding|Tenant|
|API-TEN-02|GET|/api/v1/platform/company-registrations|List registrations|Platform Admin|Tenant|
|API-TEN-03|POST|/api/v1/platform/companies/{id}/approve|Approve registration|Platform Admin|Tenant|
|API-TEN-04|POST|/api/v1/platform/tenants|Provision tenant|Platform Admin|Tenant|
|API-TEN-05|POST|/api/v1/platform/companies/{id}/status|Change company status|Platform Admin|Tenant|
|API-ORG-01|GET|/api/v1/users|List scoped users|Company Admin/authorized|Organization|
|API-ORG-02|POST|/api/v1/users|Create user|Company Admin|Organization|
|API-ORG-03|PUT|/api/v1/users/{id}|Update user|Company Admin/authorized|Organization|
|API-ORG-04|POST|/api/v1/users/{id}/deactivate|Deactivate user|Company Admin|Organization|
|API-ORG-05|GET|/api/v1/departments|List departments|Authenticated|Organization|
|API-ORG-06|POST|/api/v1/departments|Create department|Company Admin|Organization|
|API-ORG-07|PUT|/api/v1/departments/{id}|Update department|Company Admin|Organization|
|API-RBAC-01|GET|/api/v1/roles|List roles|Company Admin|Security|
|API-RBAC-02|POST|/api/v1/roles|Create custom role|Company Admin|Security|
|API-RBAC-03|PUT|/api/v1/roles/{id}/permissions|Set role permissions|Company Admin|Security|
|API-SVC-01|GET|/api/v1/services|List services|Authenticated|Service|
|API-SVC-02|POST|/api/v1/services|Create service|Company Admin|Service|
|API-SVC-03|PUT|/api/v1/services/{id}|Update service|Company Admin|Service|
|API-SVC-04|PUT|/api/v1/services/{id}/routing|Configure routing rules|Company Admin|Service|
|API-SVC-05|POST|/api/v1/services/{id}/categories|Create service category|Company Admin|Service|
|API-WF-01|GET|/api/v1/workflows|List workflows|Company Admin/authorized|Workflow|
|API-WF-02|POST|/api/v1/workflows|Create workflow draft|Company Admin|Workflow|
|API-WF-03|POST|/api/v1/workflows/{id}/versions|Create next version|Company Admin|Workflow|
|API-WF-04|POST|/api/v1/workflow-versions/{id}/publish|Publish version|Company Admin|Workflow|
|API-WF-05|GET|/api/v1/workflow-versions/{id}|Read workflow version|Authorized|Workflow|
|API-WF-06|POST|/api/v1/workflows/{id}/transition|Validate/execute transition|Authorized runtime actor|Workflow|
|API-APP-01|POST|/api/v1/approval-instances|Create approval instance|System/authorized|Approval|
|API-APP-02|POST|/api/v1/approval-instances/{id}/decision|Approve/reject|Authorized approver|Approval|
|API-TASK-01|GET|/api/v1/tasks|List/filter tasks|Scoped user|Task|
|API-TASK-02|POST|/api/v1/tasks|Create task|Manager/authorized|Task|
|API-TASK-03|PUT|/api/v1/tasks/{id}|Update task editable fields|Manager/authorized|Task|
|API-TASK-04|POST|/api/v1/tasks/{id}/assign|Assign task|Manager|Task|
|API-TASK-05|POST|/api/v1/tasks/{id}/accept|Accept task|Assigned Employee|Task|
|API-TASK-06|POST|/api/v1/tasks/{id}/reject-assignment|Reject assignment|Assigned Employee|Task|
|API-TASK-07|POST|/api/v1/tasks/{id}/progress|Update progress|Assigned Employee/authorized|Task|
|API-TASK-08|POST|/api/v1/tasks/{id}/progress-reports|Submit progress report|Assigned Employee/authorized|Task|
|API-TASK-09|POST|/api/v1/tasks/{id}/result|Submit final result|Assigned Employee/authorized|Task|
|API-TASK-10|POST|/api/v1/tasks/{id}/confirmation|Confirm/rework result|Manager/reviewer|Task|
|API-TASK-11|POST|/api/v1/tasks/{id}/reassign|Reassign task|Manager|Task|
|API-TASK-12|POST|/api/v1/tasks/{id}/cancel|Cancel task|Authorized Manager/Admin|Task|
|API-REQ-01|GET|/api/v1/requests|List/filter requests|Scoped user|Request|
|API-REQ-02|POST|/api/v1/requests|Create request|Employee|Request|
|API-REQ-03|PUT|/api/v1/requests/{id}|Update draft/waiting request|Requester/authorized|Request|
|API-REQ-04|POST|/api/v1/requests/{id}/route|Route request|Manager/authorized/system|Request|
|API-REQ-05|POST|/api/v1/requests/{id}/receive|Receive request|Assigned user/manager|Request|
|API-REQ-06|POST|/api/v1/requests/{id}/information|Provide additional information|Requester/authorized|Request|
|API-REQ-07|POST|/api/v1/requests/{id}/tasks|Create task from request|Authorized Manager/Employee|Request|
|API-REQ-08|POST|/api/v1/requests/{id}/resolve|Resolve request|Assigned Employee/Manager|Request|
|API-REQ-09|POST|/api/v1/requests/{id}/confirmation|Confirm resolution|Requester|Request|
|API-REQ-10|POST|/api/v1/requests/{id}/reject|Reject request|Authorized approver/manager|Request|
|API-REQ-11|POST|/api/v1/requests/{id}/revise|Create revised request|Requester/authorized|Request|
|API-SLA-01|GET|/api/v1/sla-profiles|List SLA profiles|Company Admin/authorized|SLA|
|API-SLA-02|POST|/api/v1/sla-profiles|Create SLA profile|Company Admin|SLA|
|API-SLA-03|POST|/api/v1/sla-profiles/{id}/versions|Create SLA version|Company Admin|SLA|
|API-SLA-04|GET|/api/v1/sla/{objectType}/{id}|View SLA status|Authorized|SLA|
|API-SLA-05|POST|/api/v1/sla/{objectType}/{id}/pause|Pause SLA|Authorized workflow actor|SLA|
|API-COL-01|POST|/api/v1/comments|Create comment|Authorized participant|Collaboration|
|API-COL-02|PUT|/api/v1/comments/{id}|Edit own comment|Author/authorized|Collaboration|
|API-COL-03|POST|/api/v1/attachments/upload-session|Create signed/multipart upload session|Authorized|Storage|
|API-COL-04|POST|/api/v1/attachments/finalize|Finalize attachment|Uploader/backend|Storage|
|API-COL-05|POST|/api/v1/records/{type}/{id}/archive|Archive record|Authorized|Archive|
|API-COL-06|GET|/api/v1/records/search|Search/filter scoped records|Authenticated|Search|
|API-NOTIF-01|GET|/api/v1/notifications|List notifications|Authenticated|Notification|
|API-NOTIF-02|PATCH|/api/v1/notifications/{id}/read|Mark notification read|Recipient|Notification|
|API-REP-01|GET|/api/v1/dashboard/manager|Manager dashboard|Manager|Reporting|
|API-REP-02|GET|/api/v1/reports/company|Company report|Company Admin|Reporting|
|API-REP-03|GET|/api/v1/reports/workload|Workload report|Manager/Company Admin|Reporting|
|API-REP-04|GET|/api/v1/reports/sla|SLA performance report|Manager/Company Admin|Reporting|
|API-AI-01|POST|/api/v1/ai/task-assistance|AI task drafting/recommendations|Manager|AI|
|API-AI-02|POST|/api/v1/ai/request-routing|AI request analysis/routing|Employee/Manager|AI|
|API-AI-03|POST|/api/v1/ai/actions/execute|Execute allowlisted AI tool action|AI Agent internal|AI|
|API-AUD-01|GET|/api/v1/audit-logs|Read scoped audit events|Platform/Company Admin|Audit|

## 6. Release interpretation

- P0 maps to Must/MVP.
- P1 maps to Should/important MVP scope, subject to explicit demo/release selection.
- P2 maps to Post-MVP.
- Multiple independent workspaces per company maps to Future Enterprise and is deferred; pricing/billing/quotas remain unspecified.
- Priority, release scope, approval state and implementation status are separate dimensions.
