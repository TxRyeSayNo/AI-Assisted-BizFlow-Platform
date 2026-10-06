**AI-ASSISTED BIZFLOW PLATFORM**

**SOFTWARE SYSTEM SPECIFICATION**

Technical Product Specification & Implementation Contract

| **Item**         | **Specification**                                                                  |
|------------------|------------------------------------------------------------------------------------|
| Project          | AI-Assisted BizFlow Platform                                                       |
| Vietnamese name  | Nền tảng quản lý và điều phối công việc nội bộ doanh nghiệp                        |
| Document status  | Baseline v1.2 – Scope Locked / Implementation Ready                                |
| Primary audience | SWP391 project team, BA/SA/Architect/Developer/QA, AI coding agent                 |
| Source baseline  | Approved Group 3 SWP391 concise report + scope decisions confirmed in conversation |
| Last updated     | 28/09/2026                                                                         |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Document purpose<br />
</strong>This document is the working Single Source of Truth for business scope, system behavior, data model, APIs, UI, AI behavior, security, testing, and implementation constraints. Requirements are intentionally separated from technical decisions, and critical ambiguities are closed by explicit baseline decisions.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 0. Requirement Status & Decision Legend

| **Status**        | **Meaning**                                                                                                                                                                                                    |
|-------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| CONFIRMED         | Provided by the team, approved baseline, or explicitly confirmed in the latest discussion.                                                                                                                     |
| PROPOSED DECISION | Technical/business decision adopted because the team explicitly delegated remaining choices to the project lead. It is treated as the baseline for implementation unless later changed through change control. |
| OUT OF SCOPE      | Explicitly excluded from this project baseline.                                                                                                                                                                |
| OPTIONAL / P2     | May be implemented if time remains; must not change the core domain model.                                                                                                                                     |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Important<br />
</strong>The earlier intermediate count of 54 Use Cases is not the canonical baseline. Use Case models must follow the later approved diagram structure and this specification. No functionality from the old intermediate count should be reintroduced unless mapped here.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# 1. Executive Summary

AI-Assisted BizFlow Platform is a SaaS Multi-Tenant internal-work orchestration platform for multiple companies. Each company operates in an isolated tenant/workspace and can configure users, departments, roles/permissions, services, workflows, approval rules, and SLAs independently. The platform supports two core directions: manager-to-employee/department task assignment and employee-to-department/manager internal requests.

The AI Agent is a business assistant across both directions. For tasks, it can understand natural-language instructions, identify missing information, suggest assignees, priority, deadline, checklist, workflow/SLA, split large work into subtasks, and monitor delivery risk. For requests, it can classify, extract information, detect missing data, detect multiple intents, split complex requests, recommend routing, and summarize processing. AI actions are constrained by tenant, role/permission, business rules, and human approval requirements.

The system is intentionally not an ERP. It focuses on internal work and request orchestration, with evidence attachments, milestone confirmation, audit history, SLA monitoring, dashboards, and reporting.

# 2. Project Overview

## 2.1 Project Vision

Provide a configurable, multi-company platform that turns scattered internal work communication into traceable, role-controlled, workflow-driven business processes with AI-assisted coordination.

## 2.2 Problem Statement

Internal work arrives from multiple directions and is often handled through chat, email, spreadsheets, or direct conversations. This creates information fragmentation, unclear ownership, weak deadline visibility, and poor auditability.

## 2.3 Target Users

Platform administrators, company administrators, managers, employees, and the internal AI Agent.

## 2.4 Target Organizations

Primarily SMEs and other organizations that need configurable internal workflow/request handling without adopting a full ERP platform.

## 2.5 Main Objectives

Centralize work and requests; improve routing and ownership; make workflows configurable; enforce SLA/approval; preserve evidence and audit history; provide AI-assisted coordination without bypassing human authority.

## 2.6 Core Value

One platform, many independent companies; natural-language-to-business-process conversion; two-way internal workflow; traceability; controlled AI automation.

## 2.7 Scope

### In Scope

- Multi-tenant company onboarding and tenant isolation.

- Identity, employee code/email login, password management, roles and permissions.

- Users, departments and organizational structure.

- Configurable internal Services, Workflows, Approval Rules and SLA profiles.

- Task creation, assignment, acceptance, execution, progress reporting, result submission and milestone confirmation.

- Internal Request creation, routing, processing, resolution and requester confirmation.

- Request-to-Task conversion and parent/child request relationships for multi-intent submissions.

- Attachments/evidence with maximum file size 500 MB.

- Comments, notifications, audit history, search/filter, archive.

- Dashboards and operational reports.

- AI-assisted task creation/assignment and request analysis/routing.

- Human-in-the-loop AI review and permission-constrained tool execution.

### Out of Scope

- Full ERP capabilities: accounting, payroll, inventory, full CRM, sales management.

- Marketplace/B2B commerce.

- External customer-service/ticketing portal as a separate product domain.

- Autonomous AI decisions that bypass configured approvals or permission.

- Enterprise SSO as a required MVP feature.

- Kubernetes/microservices infrastructure for the student project.

## 2.8 Confirmed Decisions from Latest Team Input

| **Decision**                  | **Baseline**                                                                                                                                                                         |
|-------------------------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Rejected Request              | The original Request remains REJECTED. User may reuse it to create a new Request; the new Request is a distinct record linked by RevisedFromRequestId.                               |
| Task lifecycle                | Use proposed main states plus out-of-band states: DRAFT, ASSIGNED, ACCEPTED, IN_PROGRESS, SUBMITTED, CONFIRMED, COMPLETED; REJECTED, CANCELLED, OVERDUE.                             |
| Request lifecycle             | Do not use AI_ANALYZED as a state. Main states: DRAFT, SUBMITTED, ROUTED, RECEIVED, IN_PROGRESS, RESOLVED, CONFIRMED, CLOSED; REJECTED, CANCELLED, WAITING_FOR_INFORMATION, OVERDUE. |
| Login                         | Employee code + password OR company-provided email + password.                                                                                                                       |
| Attachment size               | Maximum 500 MB per file.                                                                                                                                                             |
| Remaining open design choices | Use the proposed technical/operational decisions from the prior Gap Analysis as the implementation baseline.                                                                         |

# 3. Assumptions, Constraints and Risks

## 3.1 Assumptions

- Each user belongs to one tenant/company for the MVP.

- Department is an organizational unit; employee assignment is to a user, while department assignment represents a queue or team scope.

- A company may define custom roles in addition to the three baseline business roles, but custom roles cannot exceed tenant boundaries or system security constraints.

- A workflow version already used by active transactions is immutable; changes create a new version.

- A business calendar exists per tenant; the MVP uses one configurable default calendar.

- AI is an external LLM dependency accessed through the backend AI Agent module.

## 3.2 Constraints

- Five-person student project team.

- Implementation should remain a modular monolith.

- Development/demo may use simulated company and employee data.

- External AI and object storage services create network and cost dependencies.

## 3.3 Risks

| **Risk**                                          | **Impact** | **Mitigation**                                                                                |
|---------------------------------------------------|------------|-----------------------------------------------------------------------------------------------|
| Scope sprawl                                      | High       | Freeze core modules; treat P2 functions as optional.                                          |
| AI hallucination/wrong routing                    | High       | Structured output, validation, confidence, human review, fallback to manual routing.          |
| Tenant data leakage                               | Critical   | Tenant context propagation, resource authorization, integration tests, optional DB RLS layer. |
| Large file upload failures at 500 MB              | High       | Direct object-storage upload, multipart/resumable upload, signed URLs.                        |
| Workflow configuration breaks active transactions | High       | Immutable versioning of workflow/SLA/approval definitions.                                    |
| Team integration complexity                       | High       | Modular monolith, common API conventions, branch strategy, contract tests.                    |

# 4. System Actors

| **Actor ID** | **Actor**              | **Purpose**                                                                                 | **Core permissions / scope**                                                                  |
|--------------|------------------------|---------------------------------------------------------------------------------------------|-----------------------------------------------------------------------------------------------|
| A01          | Platform Administrator | Operates the entire platform and company/tenant lifecycle.                                  | Platform-level company/tenant visibility; platform configuration; activity monitoring.        |
| A02          | Company Administrator  | Configures one company tenant.                                                              | Tenant-scoped organizational and workflow configuration; company reports.                     |
| A03          | Manager                | Creates and coordinates work; reviews requests.                                             | Tenant-scoped management; assign/reassign; approval; workload; reports; AI assistance.        |
| A04          | Employee               | Performs assigned work and raises/handles requests within granted scope.                    | Own/assigned work; request creation; request processing when assigned; evidence and comments. |
| A05          | AI Agent               | Internal software actor that analyzes and proposes/executes authorized business operations. | Only actions exposed by tool contracts and allowed by current user + tenant policy.           |

# 5. Role & Permission Matrix

| **Module**       | **Platform Admin**   | **Company Admin** | **Manager**                      | **Employee**         |
|------------------|----------------------|-------------------|----------------------------------|----------------------|
| Identity         | A01,A02              | Login/assist      | Login/assist                     | Authenticate/assist  |
| Tenant           | C/R/U/S              | R/U within tenant | \-                               | Scoped by tenant     |
| Organization     | C/R/U/S              | C/R/U/S           | R                                | R                    |
| Service          | R                    | C/R/U/D           | R                                | R                    |
| Workflow         | R                    | C/R/U/D           | R                                | R                    |
| Task             | R                    | R                 | C/R/U/A/Rs                       | R/U/Submit           |
| Request          | R                    | R                 | R/A/Rs                           | C/R/U/Submit/Confirm |
| Approval         | R                    | Config            | Approve/Reject                   | Submit/View          |
| SLA              | R                    | Config            | R                                | R                    |
| Notification     | R                    | Config            | R                                | R                    |
| Evidence         | R                    | R                 | C/R/D where permitted            | C/R/D own/assigned   |
| Audit            | R                    | R tenant          | R allowed                        | R own/allowed        |
| Dashboard/Report | Platform             | Tenant            | Tenant/department scope          | Own/allowed          |
| AI               | Platform AI settings | Tenant AI policy  | Use AI / approve recommendations | Use AI / confirm     |

Legend: C=Create, R=Read/View, U=Update, D=Delete/Deactivate, A=Approve, Rs=Reassign. “Manage” is intentionally not used as a canonical Use Case name; matrix entries are action-oriented.

# 6. System Modules

| **ID** | **Module**                   | **Purpose**                                                                    | **Actors** | **Dependencies** | **Technology**                           |
|--------|------------------------------|--------------------------------------------------------------------------------|------------|------------------|------------------------------------------|
| M01    | Authentication & Identity    | Authentication, password, session/token, employee-code/email login.            | A01-A04    | Tenant context   | ASP.NET Core Identity + JWT              |
| M02    | Tenant & Company             | Company registration, tenant lifecycle, isolation.                             | A01        | M01              | ASP.NET Core + PostgreSQL                |
| M03    | Organization                 | Users, departments, roles, permissions.                                        | A02        | M01/M02          | EF Core + PostgreSQL                     |
| M04    | Service Configuration        | Company-defined internal services and request categories.                      | A02        | M03              | ASP.NET Core + EF Core                   |
| M05    | Task Management              | Task creation, assignment, execution and result lifecycle.                     | A02-A04    | M03/M04          | ASP.NET Core + EF Core + SignalR         |
| M06    | Request Management           | Internal requests, routing, resolution and revision chain.                     | A02-A04    | M03/M04/M05      | ASP.NET Core + EF Core                   |
| M07    | Workflow & Approval          | Versioned workflows, transition rules, approval instances.                     | A02-A03    | M04/M05/M06      | ASP.NET Core + EF Core                   |
| M08    | SLA & Scheduling             | SLA timers, business calendar, warnings, overdue and escalation.               | A02-A03    | M05/M06/M07      | Hangfire + PostgreSQL                    |
| M09    | Notification & Collaboration | In-app/email notifications, comments, activity history.                        | A01-A04    | M05/M06/M08      | SignalR + email provider                 |
| M10    | Evidence & Storage           | Attachments, large-file upload, object metadata.                               | A02-A04    | M03/M05/M06      | MinIO/S3 + PostgreSQL                    |
| M11    | Dashboard & Reporting        | Operational metrics and company reports.                                       | A01-A03    | M05/M06/M08      | PostgreSQL + API                         |
| M12    | AI Agent                     | AI understanding, recommendations, task/request orchestration, tool execution. | A02-A05    | All core modules | OpenAI Responses API + official .NET SDK |
| M13    | Audit & Observability        | Audit logs, application logs, traces, metrics.                                 | A01-A02    | All modules      | Serilog + OpenTelemetry                  |

# 7. Functional Requirements

## 7.1 Authentication & Identity

### FR-AUTH-001 – Authenticate with Employee Code

| **Item**            | **Specification**                                                             |
|---------------------|-------------------------------------------------------------------------------|
| Purpose             | Allow users to log in using their employee code and password.                 |
| Actor               | Employee/Manager/Company Admin                                                |
| Preconditions       | Active user; valid tenant/company context                                     |
| Trigger             | Login form submit                                                             |
| Main Flow           | Validate credentials; create authenticated session/token; resolve tenant      |
| Alternative Flow    | If code is ambiguous globally, require tenant/company context or scoped login |
| Exception Flow      | Invalid credentials, inactive account, locked account                         |
| Input               | EmployeeCode, Password, Tenant/Company context                                |
| Output              | Access token + refresh token/session context                                  |
| Validation          | Required, code must match user and password hash                              |
| Business Rules      | EmployeeCode unique within tenant                                             |
| Permission          | Authentication only within current tenant                                     |
| Database Impact     | User/session/auth metadata                                                    |
| API Impact          | POST /api/v1/auth/login                                                       |
| Notification        | Login success; failed login if security policy requires                       |
| AI involvement      | No AI                                                                         |
| Dependencies        | M01                                                                           |
| Acceptance Criteria | User can log in and receive correct tenant context.                           |

### FR-AUTH-002 – Authenticate with Company Email

| **Item**            | **Specification**                                          |
|---------------------|------------------------------------------------------------|
| Purpose             | Allow company-provided email + password login.             |
| Actor               | Employee/Manager/Company Admin                             |
| Preconditions       | Active account with unique company email                   |
| Trigger             | Login form submit                                          |
| Main Flow           | Validate email and password; create session/token          |
| Alternative Flow    | Employee can use either employee code or email             |
| Exception Flow      | Invalid credentials/inactive/locked                        |
| Input               | Email, Password, tenant/company context                    |
| Output              | Access token + refresh context                             |
| Validation          | Email format; unique within tenant                         |
| Business Rules      | Email belongs to one user in tenant                        |
| Permission          | Same as login policy                                       |
| Database Impact     | User/session/auth metadata                                 |
| API Impact          | POST /api/v1/auth/login                                    |
| Notification        | Optional security notification for repeated failures       |
| AI involvement      | No AI                                                      |
| Dependencies        | M01                                                        |
| Acceptance Criteria | Valid company email credentials authenticate successfully. |

### FR-AUTH-003 – Refresh Access Token

| **Item**            | **Specification**                                      |
|---------------------|--------------------------------------------------------|
| Purpose             | Renew access token without re-entering credentials.    |
| Actor               | Authenticated user                                     |
| Preconditions       | Valid refresh token                                    |
| Trigger             | Token refresh request                                  |
| Main Flow           | Validate and rotate refresh token; return access token |
| Alternative Flow    | Expired refresh token requires login                   |
| Exception Flow      | Revoked/invalid token                                  |
| Input               | Refresh token                                          |
| Output              | New access token                                       |
| Validation          | Token signature, expiry, revocation                    |
| Business Rules      | Refresh token rotation                                 |
| Permission          | Auth only                                              |
| Database Impact     | User/session/auth metadata                             |
| API Impact          | POST /api/v1/auth/refresh                              |
| Notification        | None                                                   |
| AI involvement      | No AI                                                  |
| Dependencies        | M01                                                    |
| Acceptance Criteria | Valid refresh token yields a new access token.         |

### FR-AUTH-004 – Reset Password

| **Item**            | **Specification**                                                              |
|---------------------|--------------------------------------------------------------------------------|
| Purpose             | Reset or administratively issue a new password.                                |
| Actor               | User / Company Admin                                                           |
| Preconditions       | User identity known; email available or admin authority                        |
| Trigger             | Forgot password / admin reset                                                  |
| Main Flow           | Issue reset token via company email; or admin triggers temporary password flow |
| Alternative Flow    | Employee without email uses admin-issued reset                                 |
| Exception Flow      | Expired token, invalid code                                                    |
| Input               | Identifier + reset token + new password                                        |
| Output              | Password reset confirmation                                                    |
| Validation          | Password policy; reset token one-time                                          |
| Business Rules      | Old password invalidated after reset                                           |
| Permission          | Auth permission                                                                |
| Database Impact     | User/session/auth metadata                                                     |
| API Impact          | POST /api/v1/auth/forgot-password; POST /api/v1/auth/reset-password            |
| Notification        | Email reset                                                                    |
| AI involvement      | No AI                                                                          |
| Dependencies        | M01                                                                            |
| Acceptance Criteria | Password can be changed through an authorized flow.                            |

## 7.2 Tenant & Company Onboarding

### FR-TEN-001 – Register Company

| **Item**            | **Specification**                                          |
|---------------------|------------------------------------------------------------|
| Purpose             | Create a registration request for a new company.           |
| Actor               | Public/Platform onboarding flow                            |
| Preconditions       | No active tenant with same company identity                |
| Trigger             | Registration submit                                        |
| Main Flow           | Validate company data; create pending company registration |
| Alternative Flow    | Duplicate company details return review message            |
| Exception Flow      | Validation/duplicate failure                               |
| Input               | Company name, legal/contact data                           |
| Output              | Pending company registration                               |
| Validation          | Required fields; uniqueness rules                          |
| Business Rules      | Registration starts in Pending                             |
| Permission          | Public form or authenticated onboarding                    |
| Database Impact     | Company/Tenant records                                     |
| API Impact          | POST /api/v1/platform/company-registrations                |
| Notification        | Platform Admin notification                                |
| AI involvement      | No AI                                                      |
| Dependencies        | M02                                                        |
| Acceptance Criteria | A valid registration is created in Pending state.          |

### FR-TEN-002 – Create Tenant for Approved Company

| **Item**            | **Specification**                                                        |
|---------------------|--------------------------------------------------------------------------|
| Purpose             | Create isolated tenant/workspace after approval.                         |
| Actor               | Platform Administrator                                                   |
| Preconditions       | Company registration approved                                            |
| Trigger             | Approval action                                                          |
| Main Flow           | Create tenant; initialize default settings; create initial Company Admin |
| Alternative Flow    | Tenant creation retries safely                                           |
| Exception Flow      | Provisioning failure                                                     |
| Input               | CompanyId, tenant key, settings                                          |
| Output              | Active tenant/workspace                                                  |
| Validation          | Tenant key unique                                                        |
| Business Rules      | One primary tenant per company in MVP                                    |
| Permission          | Platform Admin only                                                      |
| Database Impact     | Company/Tenant records                                                   |
| API Impact          | POST /api/v1/platform/tenants                                            |
| Notification        | Company Admin onboarding notification                                    |
| AI involvement      | No AI                                                                    |
| Dependencies        | M01/M02                                                                  |
| Acceptance Criteria | Approved company receives an active isolated tenant.                     |

### FR-TEN-003 – Change Company Status

| **Item**            | **Specification**                                                   |
|---------------------|---------------------------------------------------------------------|
| Purpose             | Activate, suspend or deactivate company operations.                 |
| Actor               | Platform Administrator                                              |
| Preconditions       | Company exists                                                      |
| Trigger             | Status action                                                       |
| Main Flow           | Validate transition; update company/tenant status; preserve history |
| Alternative Flow    | Already-target-status is idempotent                                 |
| Exception Flow      | Invalid transition                                                  |
| Input               | CompanyId, target status, reason                                    |
| Output              | New company status                                                  |
| Validation          | Reason required for suspend/deactivate                              |
| Business Rules      | Suspended tenant blocks normal business actions                     |
| Permission          | Platform Admin                                                      |
| Database Impact     | Company/Tenant records                                              |
| API Impact          | PATCH /api/v1/platform/companies/{id}/status                        |
| Notification        | Tenant admin alert                                                  |
| AI involvement      | No AI                                                               |
| Dependencies        | M02/M13                                                             |
| Acceptance Criteria | Status changes are enforced and audited.                            |

### FR-TEN-004 – Enforce Tenant Data Isolation

| **Item**            | **Specification**                                                                   |
|---------------------|-------------------------------------------------------------------------------------|
| Purpose             | Restrict every business query/action to current tenant.                             |
| Actor               | System / all actors                                                                 |
| Preconditions       | Authenticated user with tenant context                                              |
| Trigger             | Every business request                                                              |
| Main Flow           | Resolve tenant; authorize resource; apply tenant filter; reject cross-tenant access |
| Alternative Flow    | Platform Admin has explicit platform scope only                                     |
| Exception Flow      | Missing tenant, cross-tenant resource                                               |
| Input               | TenantId + resource ID                                                              |
| Output              | Authorized resource or 403/404                                                      |
| Validation          | TenantId required on tenant-owned data                                              |
| Business Rules      | No cross-tenant read/write                                                          |
| Permission          | Applied across API/data layer                                                       |
| Database Impact     | Company/Tenant records                                                              |
| API Impact          | All endpoints                                                                       |
| Notification        | Security alert/audit on suspicious attempts                                         |
| AI involvement      | AI inherits current tenant context                                                  |
| Dependencies        | All modules                                                                         |
| Acceptance Criteria | Cross-tenant integration tests pass.                                                |

## 7.3 Organization, Roles & Permissions

### FR-ORG-001 – Create User Account

| **Item**            | **Specification**                                                          |
|---------------------|----------------------------------------------------------------------------|
| Purpose             | Create a user inside a company.                                            |
| Actor               | Company Administrator                                                      |
| Preconditions       | Tenant active; admin permission                                            |
| Trigger             | Save user                                                                  |
| Main Flow           | Validate identity; create user; assign department/role; activate or invite |
| Alternative Flow    | Invite pending activation                                                  |
| Exception Flow      | Duplicate employee code/email                                              |
| Input               | EmployeeCode, Name, Email, Department, Role, Status                        |
| Output              | User record                                                                |
| Validation          | EmployeeCode and email unique within tenant                                |
| Business Rules      | User belongs to one tenant                                                 |
| Permission          | users.create                                                               |
| Database Impact     | User/Department/Role records                                               |
| API Impact          | POST /api/v1/users                                                         |
| Notification        | Invite notification                                                        |
| AI involvement      | No AI                                                                      |
| Dependencies        | M01-M03                                                                    |
| Acceptance Criteria | New user is visible only in its tenant and has correct permissions.        |

### FR-ORG-002 – Update User Profile

| **Item**            | **Specification**                                                         |
|---------------------|---------------------------------------------------------------------------|
| Purpose             | Update editable user information.                                         |
| Actor               | Company Administrator / authorized user for own profile                   |
| Preconditions       | User exists and in same tenant                                            |
| Trigger             | Save changes                                                              |
| Main Flow           | Validate allowed fields; persist; audit sensitive changes                 |
| Alternative Flow    | Email/employee code change may require re-verification                    |
| Exception Flow      | Invalid format or immutable field                                         |
| Input               | Profile fields                                                            |
| Output              | Updated user                                                              |
| Validation          | Field-specific validation                                                 |
| Business Rules      | EmployeeCode may be immutable after activation unless admin policy allows |
| Permission          | users.update / profile.update                                             |
| Database Impact     | User/Department/Role records                                              |
| API Impact          | PUT /api/v1/users/{id}                                                    |
| Notification        | Optional profile update notification                                      |
| AI involvement      | No AI                                                                     |
| Dependencies        | M03                                                                       |
| Acceptance Criteria | Authorized changes are persisted and audited.                             |

### FR-ORG-003 – Deactivate User Account

| **Item**            | **Specification**                                                     |
|---------------------|-----------------------------------------------------------------------|
| Purpose             | Disable a user without losing history.                                |
| Actor               | Company Administrator                                                 |
| Preconditions       | User exists; no hard-delete rule                                      |
| Trigger             | Deactivate action                                                     |
| Main Flow           | Set inactive; revoke sessions; preserve historical ownership          |
| Alternative Flow    | Blocked if user is required as sole active approver unless reassigned |
| Exception Flow      | Invalid state / critical dependency                                   |
| Input               | UserId, reason                                                        |
| Output              | Inactive user                                                         |
| Validation          | Reason required                                                       |
| Business Rules      | Historical records remain                                             |
| Permission          | users.deactivate                                                      |
| Database Impact     | User/Department/Role records                                          |
| API Impact          | POST /api/v1/users/{id}/deactivate                                    |
| Notification        | Optional notification                                                 |
| AI involvement      | No AI                                                                 |
| Dependencies        | M01/M03/M07                                                           |
| Acceptance Criteria | User can no longer authenticate while history remains intact.         |

### FR-ORG-004 – Create Department

| **Item**            | **Specification**                                     |
|---------------------|-------------------------------------------------------|
| Purpose             | Create department within tenant.                      |
| Actor               | Company Administrator                                 |
| Preconditions       | Tenant active                                         |
| Trigger             | Save department                                       |
| Main Flow           | Validate name/code; create; optionally assign manager |
| Alternative Flow    | Duplicate code rejected                               |
| Exception Flow      | Validation                                            |
| Input               | Name, Code, ParentDepartment optional                 |
| Output              | Department                                            |
| Validation          | Name/code unique within tenant                        |
| Business Rules      | No cross-tenant reference                             |
| Permission          | departments.create                                    |
| Database Impact     | User/Department/Role records                          |
| API Impact          | POST /api/v1/departments                              |
| Notification        | None                                                  |
| AI involvement      | No AI                                                 |
| Dependencies        | M03                                                   |
| Acceptance Criteria | Department appears in tenant organization.            |

### FR-ORG-005 – Configure Role Permissions

| **Item**            | **Specification**                                          |
|---------------------|------------------------------------------------------------|
| Purpose             | Create/update custom roles and permission assignments.     |
| Actor               | Company Administrator                                      |
| Preconditions       | Tenant active                                              |
| Trigger             | Save role permissions                                      |
| Main Flow           | Validate permissions against allowed catalog; save version |
| Alternative Flow    | Cannot remove permission required by system protection     |
| Exception Flow      | Unauthorized permission elevation                          |
| Input               | Role, permission set                                       |
| Output              | Updated role                                               |
| Validation          | Permission IDs valid; role unique within tenant            |
| Business Rules      | Custom roles cannot exceed tenant/platform restrictions    |
| Permission          | roles.configure                                            |
| Database Impact     | User/Department/Role records                               |
| API Impact          | PUT /api/v1/roles/{id}/permissions                         |
| Notification        | Security/config audit                                      |
| AI involvement      | No AI                                                      |
| Dependencies        | M03/M13                                                    |
| Acceptance Criteria | Role permissions apply to users after policy evaluation.   |

## 7.4 Service Configuration

### FR-SVC-001 – Create Internal Service

| **Item**            | **Specification**                                         |
|---------------------|-----------------------------------------------------------|
| Purpose             | Define a service offered to employees inside the company. |
| Actor               | Company Administrator                                     |
| Preconditions       | Tenant active                                             |
| Trigger             | Save service                                              |
| Main Flow           | Create service and category metadata                      |
| Alternative Flow    | Duplicate code rejected                                   |
| Exception Flow      | Validation                                                |
| Input               | Name, code, description, categories, active flag          |
| Output              | Service                                                   |
| Validation          | Code unique within tenant                                 |
| Business Rules      | Service belongs to one tenant                             |
| Permission          | service.create                                            |
| Database Impact     | Service and routing/configuration records                 |
| API Impact          | POST /api/v1/services                                     |
| Notification        | None                                                      |
| AI involvement      | Optional AI can use service catalog as context            |
| Dependencies        | M04                                                       |
| Acceptance Criteria | Service can be selected for future requests.              |

### FR-SVC-002 – Configure Service Routing

| **Item**            | **Specification**                                                 |
|---------------------|-------------------------------------------------------------------|
| Purpose             | Define routing defaults/constraints for a service.                |
| Actor               | Company Administrator                                             |
| Preconditions       | Service exists                                                    |
| Trigger             | Save routing configuration                                        |
| Main Flow           | Set target department rules, allowed assignee scopes and fallback |
| Alternative Flow    | Rules may overlap; system resolves by priority                    |
| Exception Flow      | Invalid department/person references                              |
| Input               | Routing rules                                                     |
| Output              | Versioned routing config                                          |
| Validation          | References same tenant; no cyclic rules                           |
| Business Rules      | Fallback to manual routing                                        |
| Permission          | service.routing.configure                                         |
| Database Impact     | Service and routing/configuration records                         |
| API Impact          | PUT /api/v1/services/{id}/routing                                 |
| Notification        | Config audit                                                      |
| AI involvement      | AI uses routing rules as constraints                              |
| Dependencies        | M04/M12                                                           |
| Acceptance Criteria | Valid service routing guides request assignment.                  |

### FR-SVC-003 – Configure Workflow for Service

| **Item**            | **Specification**                                                |
|---------------------|------------------------------------------------------------------|
| Purpose             | Associate a workflow version with a service.                     |
| Actor               | Company Administrator                                            |
| Preconditions       | Service + workflow exist                                         |
| Trigger             | Save association                                                 |
| Main Flow           | Create/activate workflow version association                     |
| Alternative Flow    | Active workflow cannot be modified in place                      |
| Exception Flow      | Version conflict                                                 |
| Input               | ServiceId, WorkflowVersionId                                     |
| Output              | New active association                                           |
| Validation          | Only compatible workflow                                         |
| Business Rules      | Existing in-flight objects retain old version                    |
| Permission          | service.workflow.configure                                       |
| Database Impact     | Service and routing/configuration records                        |
| API Impact          | PUT /api/v1/services/{id}/workflow                               |
| Notification        | Config audit                                                     |
| AI involvement      | AI can recommend but cannot change association without authority |
| Dependencies        | M04/M07                                                          |
| Acceptance Criteria | New service transactions use the configured workflow version.    |

### FR-SVC-004 – Configure Approval Rule for Service

| **Item**            | **Specification**                                           |
|---------------------|-------------------------------------------------------------|
| Purpose             | Attach approval behavior to a service.                      |
| Actor               | Company Administrator                                       |
| Preconditions       | Service exists                                              |
| Trigger             | Save rule                                                   |
| Main Flow           | Define approver sequence/parallel groups and decision rules |
| Alternative Flow    | Invalid approver reference rejected                         |
| Exception Flow      | No approver available                                       |
| Input               | Approval configuration                                      |
| Output              | Approval rule version                                       |
| Validation          | Approver same tenant and active                             |
| Business Rules      | At least one valid approval step when required              |
| Permission          | service.approval.configure                                  |
| Database Impact     | Service and routing/configuration records                   |
| API Impact          | PUT /api/v1/services/{id}/approval-rules                    |
| Notification        | Config audit                                                |
| AI involvement      | AI may summarize, not approve                               |
| Dependencies        | M04/M07                                                     |
| Acceptance Criteria | Approval instances use the active rule version.             |

## 7.5 Task Management

### FR-TASK-001 – Create Task

| **Item**            | **Specification**                                                               |
|---------------------|---------------------------------------------------------------------------------|
| Purpose             | Create an internal task from structured or natural-language input.              |
| Actor               | Manager                                                                         |
| Preconditions       | Manager has create permission                                                   |
| Trigger             | Create action                                                                   |
| Main Flow           | Validate fields; create DRAFT or ASSIGNED task; optionally invoke AI assistance |
| Alternative Flow    | AI draft can be edited before save                                              |
| Exception Flow      | Validation, duplicate submit                                                    |
| Input               | Title, description, assignee/department optional, deadline, priority, checklist |
| Output              | Task record                                                                     |
| Validation          | Title required; deadline valid; references same tenant                          |
| Business Rules      | Task creator becomes audit owner; task belongs to tenant                        |
| Permission          | tasks.create                                                                    |
| Database Impact     | Task, assignment/progress/result records                                        |
| API Impact          | POST /api/v1/tasks                                                              |
| Notification        | Assignee notification after assignment                                          |
| AI involvement      | AI may extract/suggest fields                                                   |
| Dependencies        | M05/M12                                                                         |
| Acceptance Criteria | Manager can create a valid task and see it in own scope.                        |

### FR-TASK-002 – Assign Task

| **Item**            | **Specification**                                                                                                  |
|---------------------|--------------------------------------------------------------------------------------------------------------------|
| Purpose             | Assign a task to employee or department.                                                                           |
| Actor               | Manager                                                                                                            |
| Preconditions       | Task DRAFT/REJECTED; target within manager scope                                                                   |
| Trigger             | Assign action                                                                                                      |
| Main Flow           | Validate target scope; create assignment; change state to ASSIGNED                                                 |
| Alternative Flow    | Department assignment can trigger AI suggested assignee                                                            |
| Exception Flow      | Unauthorized target                                                                                                |
| Input               | TaskId, target type/id, note                                                                                       |
| Output              | Assignment + state change                                                                                          |
| Validation          | Manager scope; target active                                                                                       |
| Business Rules      | Assignment history immutable                                                                                       |
| Permission          | tasks.assign                                                                                                       |
| Database Impact     | Task, assignment/progress/result records                                                                           |
| API Impact          | POST /api/v1/tasks/{id}/assign                                                                                     |
| Notification        | Assignee/department notified                                                                                       |
| AI involvement      | AI may recommend assignee; human confirmation required for final assignment unless explicit authorized auto-action |
| Dependencies        | M05/M12                                                                                                            |
| Acceptance Criteria | Assigned target can view the task.                                                                                 |

### FR-TASK-003 – Accept Task

| **Item**            | **Specification**                                       |
|---------------------|---------------------------------------------------------|
| Purpose             | Employee confirms receipt of assigned task.             |
| Actor               | Employee                                                |
| Preconditions       | Task ASSIGNED and user is target                        |
| Trigger             | Accept action                                           |
| Main Flow           | Create acceptance record; state ACCEPTED                |
| Alternative Flow    | Employee can reject assignment with reason -\> REJECTED |
| Exception Flow      | Already accepted/cancelled                              |
| Input               | TaskId, acceptance/rejection reason                     |
| Output              | Acceptance record + state                               |
| Validation          | Required reason on rejection                            |
| Business Rules      | Rejecting assignment does not delete task               |
| Permission          | tasks.accept                                            |
| Database Impact     | Task, assignment/progress/result records                |
| API Impact          | POST /api/v1/tasks/{id}/accept                          |
| Notification        | Manager notification                                    |
| AI involvement      | AI none                                                 |
| Dependencies        | M05/M09                                                 |
| Acceptance Criteria | Employee can accept; rejected assignment is auditable.  |

### FR-TASK-004 – Execute Task

| **Item**            | **Specification**                             |
|---------------------|-----------------------------------------------|
| Purpose             | Perform assigned task and record work.        |
| Actor               | Employee                                      |
| Preconditions       | Task ACCEPTED/IN_PROGRESS                     |
| Trigger             | Start work                                    |
| Main Flow           | Set IN_PROGRESS; allow progress actions       |
| Alternative Flow    | Task may return from OVERDUE to IN_PROGRESS   |
| Exception Flow      | Unauthorized/invalid state                    |
| Input               | TaskId                                        |
| Output              | Updated state/history                         |
| Validation          | Only assigned/authorized employee can execute |
| Business Rules      | State transitions enforced                    |
| Permission          | tasks.execute                                 |
| Database Impact     | Task, assignment/progress/result records      |
| API Impact          | POST /api/v1/tasks/{id}/start                 |
| Notification        | Optional realtime update                      |
| AI involvement      | AI can summarize current task context         |
| Dependencies        | M05                                           |
| Acceptance Criteria | Authorized employee can start work.           |

### FR-TASK-005 – Update Task Progress

| **Item**            | **Specification**                                       |
|---------------------|---------------------------------------------------------|
| Purpose             | Record percentage/status/progress note.                 |
| Actor               | Employee                                                |
| Preconditions       | Task active                                             |
| Trigger             | Progress submit                                         |
| Main Flow           | Validate progress; store report; keep state IN_PROGRESS |
| Alternative Flow    | Progress can be submitted with evidence                 |
| Exception Flow      | Invalid percent/state                                   |
| Input               | TaskId, percent, note, attachments                      |
| Output              | Progress report                                         |
| Validation          | 0-100 numeric; non-decreasing unless correction reason  |
| Business Rules      | Every progress update timestamped                       |
| Permission          | tasks.progress.update                                   |
| Database Impact     | Task, assignment/progress/result records                |
| API Impact          | POST /api/v1/tasks/{id}/progress                        |
| Notification        | Manager notification according to preferences           |
| AI involvement      | AI can summarize progress/risk                          |
| Dependencies        | M05/M10                                                 |
| Acceptance Criteria | Manager can see latest progress and history.            |

### FR-TASK-006 – Submit Progress Report

| **Item**            | **Specification**                                          |
|---------------------|------------------------------------------------------------|
| Purpose             | Submit a formal progress report.                           |
| Actor               | Employee                                                   |
| Preconditions       | Task active                                                |
| Trigger             | Report submit                                              |
| Main Flow           | Create report; notify manager; optionally attach evidence  |
| Alternative Flow    | Can be repeated until final result                         |
| Exception Flow      | File validation / state rejection                          |
| Input               | TaskId, content, attachments                               |
| Output              | Progress report                                            |
| Validation          | Content required; files within policy                      |
| Business Rules      | Report immutable after submission except correction record |
| Permission          | tasks.progress-report                                      |
| Database Impact     | Task, assignment/progress/result records                   |
| API Impact          | POST /api/v1/tasks/{id}/progress-reports                   |
| Notification        | Manager notification                                       |
| AI involvement      | AI may summarize report before submit                      |
| Dependencies        | M05/M09/M10/M12                                            |
| Acceptance Criteria | Formal report is stored and traceable.                     |

### FR-TASK-007 – Submit Task Result

| **Item**            | **Specification**                                             |
|---------------------|---------------------------------------------------------------|
| Purpose             | Submit final result/evidence for review.                      |
| Actor               | Employee                                                      |
| Preconditions       | Task IN_PROGRESS/OVERDUE                                      |
| Trigger             | Submit result                                                 |
| Main Flow           | Store result; state SUBMITTED; start review milestone         |
| Alternative Flow    | Manager may request rework -\> task can return to IN_PROGRESS |
| Exception Flow      | Invalid state/file                                            |
| Input               | TaskId, result text, attachments                              |
| Output              | Submitted result                                              |
| Validation          | Result required; attachments validated                        |
| Business Rules      | Submit freezes current result revision                        |
| Permission          | tasks.submit                                                  |
| Database Impact     | Task, assignment/progress/result records                      |
| API Impact          | POST /api/v1/tasks/{id}/result                                |
| Notification        | Manager reviewer notified                                     |
| AI involvement      | AI may check completeness and summarize                       |
| Dependencies        | M05/M10/M12                                                   |
| Acceptance Criteria | Manager receives result for confirmation.                     |

### FR-TASK-008 – Confirm Task Result

| **Item**            | **Specification**                                                                   |
|---------------------|-------------------------------------------------------------------------------------|
| Purpose             | Manager confirms final submitted result.                                            |
| Actor               | Manager                                                                             |
| Preconditions       | Task SUBMITTED; manager is reviewer                                                 |
| Trigger             | Confirm action                                                                      |
| Main Flow           | Validate evidence; record confirmation; complete task                               |
| Alternative Flow    | Reject result -\> REJECTED or back to IN_PROGRESS according to configured workflow  |
| Exception Flow      | Unauthorized reviewer / missing evidence                                            |
| Input               | TaskId, decision, note                                                              |
| Output              | Confirmation record; task state                                                     |
| Validation          | Reviewer permission; decision required                                              |
| Business Rules      | Critical milestone requires reviewer confirmation                                   |
| Permission          | tasks.confirm                                                                       |
| Database Impact     | Task, assignment/progress/result records                                            |
| API Impact          | POST /api/v1/tasks/{id}/confirmation                                                |
| Notification        | Employee notified                                                                   |
| AI involvement      | AI can summarize evidence but cannot final-confirm without authorized action policy |
| Dependencies        | M05/M07/M10                                                                         |
| Acceptance Criteria | Confirmed result causes task completion according to workflow.                      |

### FR-TASK-009 – Reassign Task

| **Item**            | **Specification**                                                              |
|---------------------|--------------------------------------------------------------------------------|
| Purpose             | Move responsibility to another employee/department.                            |
| Actor               | Manager                                                                        |
| Preconditions       | Task not completed/cancelled                                                   |
| Trigger             | Reassign action                                                                |
| Main Flow           | Validate target; close old assignment; create new assignment; preserve history |
| Alternative Flow    | Can use AI recommendation first                                                |
| Exception Flow      | Invalid target                                                                 |
| Input               | TaskId, target, reason                                                         |
| Output              | New active assignment                                                          |
| Validation          | Target within manager scope                                                    |
| Business Rules      | Reason required; audit old/new assignee                                        |
| Permission          | tasks.reassign                                                                 |
| Database Impact     | Task, assignment/progress/result records                                       |
| API Impact          | POST /api/v1/tasks/{id}/reassign                                               |
| Notification        | Old/new target notifications                                                   |
| AI involvement      | AI may recommend new assignee based on workload                                |
| Dependencies        | M05/M12/M13                                                                    |
| Acceptance Criteria | Task history shows full assignment chain.                                      |

### FR-TASK-010 – Cancel Task

| **Item**            | **Specification**                                                       |
|---------------------|-------------------------------------------------------------------------|
| Purpose             | Cancel an active task with reason.                                      |
| Actor               | Manager / authorized Company Admin                                      |
| Preconditions       | Task not completed                                                      |
| Trigger             | Cancel action                                                           |
| Main Flow           | Validate authority; set CANCELLED; record reason                        |
| Alternative Flow    | Completed tasks require special correction/void flow, not cancel        |
| Exception Flow      | Invalid state                                                           |
| Input               | TaskId, reason                                                          |
| Output              | CANCELLED task                                                          |
| Validation          | Reason required                                                         |
| Business Rules      | Cancelled task cannot be re-opened directly; create new task if needed  |
| Permission          | tasks.cancel                                                            |
| Database Impact     | Task, assignment/progress/result records                                |
| API Impact          | POST /api/v1/tasks/{id}/cancel                                          |
| Notification        | Target notification                                                     |
| AI involvement      | AI cannot autonomously cancel unless explicitly allowed                 |
| Dependencies        | M05/M13                                                                 |
| Acceptance Criteria | Cancelled task is excluded from active workload and retained for audit. |

## 7.6 Request Management

### FR-REQ-001 – Create Internal Request

| **Item**            | **Specification**                                                        |
|---------------------|--------------------------------------------------------------------------|
| Purpose             | Create a request using structured form or natural language.              |
| Actor               | Employee                                                                 |
| Preconditions       | User active in tenant                                                    |
| Trigger             | Submit draft/request                                                     |
| Main Flow           | Validate service/category and content; create DRAFT or SUBMITTED         |
| Alternative Flow    | AI can draft structured fields before submit                             |
| Exception Flow      | Missing required info                                                    |
| Input               | Service, title, description, attachments, optional priority              |
| Output              | Request record                                                           |
| Validation          | Service required unless tenant permits generic request; content required |
| Business Rules      | Request belongs to tenant and requester                                  |
| Permission          | requests.create                                                          |
| Database Impact     | Request, routing/resolution/relationship records                         |
| API Impact          | POST /api/v1/requests                                                    |
| Notification        | Submission notification                                                  |
| AI involvement      | AI assistance available before/after draft                               |
| Dependencies        | M06/M10/M12                                                              |
| Acceptance Criteria | Valid request can be submitted and tracked.                              |

### FR-REQ-002 – Analyze Request for Routing

| **Item**            | **Specification**                                                                          |
|---------------------|--------------------------------------------------------------------------------------------|
| Purpose             | Analyze request content for classification/routing.                                        |
| Actor               | AI Agent                                                                                   |
| Preconditions       | Submitted request; AI feature enabled                                                      |
| Trigger             | Request submitted or manual AI request                                                     |
| Main Flow           | Extract entities; classify service/category; detect missing information; recommend routing |
| Alternative Flow    | Low confidence -\> manual review; missing data -\> WAITING_FOR_INFORMATION                 |
| Exception Flow      | AI timeout/provider failure                                                                |
| Input               | Request content + service catalog + org context                                            |
| Output              | Structured AI recommendation                                                               |
| Validation          | Schema validation; confidence range; references must exist                                 |
| Business Rules      | No direct DB mutation by model output                                                      |
| Permission          | request.002                                                                                |
| Database Impact     | Request, routing/resolution/relationship records                                           |
| API Impact          | POST /api/v1/ai/requests/analyze                                                           |
| Notification        | AI processing status                                                                       |
| AI involvement      | Core AI function                                                                           |
| Dependencies        | M12/M04/M06                                                                                |
| Acceptance Criteria | Valid structured recommendation is produced or safe fallback occurs.                       |

### FR-REQ-003 – Route Request

| **Item**            | **Specification**                                                        |
|---------------------|--------------------------------------------------------------------------|
| Purpose             | Route request to department/person.                                      |
| Actor               | Manager / authorized workflow / AI through approved tool                 |
| Preconditions       | Request SUBMITTED; routing recommendation available or manual routing    |
| Trigger             | Route action                                                             |
| Main Flow           | Validate target; create routing record; move ROUTED                      |
| Alternative Flow    | Low-confidence AI output requires human selection                        |
| Exception Flow      | Invalid target / policy                                                  |
| Input               | RequestId, target, reason                                                |
| Output              | Routing record + state                                                   |
| Validation          | Tenant/permission/routing rule                                           |
| Business Rules      | Final route stored separately from AI suggestion                         |
| Permission          | requests.route                                                           |
| Database Impact     | Request, routing/resolution/relationship records                         |
| API Impact          | POST /api/v1/requests/{id}/route                                         |
| Notification        | Department/person notified                                               |
| AI involvement      | AI may recommend; authorized tool can execute with required confirmation |
| Dependencies        | M06/M07/M12                                                              |
| Acceptance Criteria | Request reaches the intended queue/person.                               |

### FR-REQ-004 – Receive Request

| **Item**            | **Specification**                                            |
|---------------------|--------------------------------------------------------------|
| Purpose             | Assigned department/person accepts responsibility.           |
| Actor               | Employee/Manager assigned recipient                          |
| Preconditions       | Request ROUTED                                               |
| Trigger             | Receive/accept action                                        |
| Main Flow           | Record receipt; set RECEIVED; begin SLA if configured        |
| Alternative Flow    | Reject intake with reason can return to ROUTED/manual triage |
| Exception Flow      | Already received/invalid assignee                            |
| Input               | RequestId                                                    |
| Output              | Receipt record/state                                         |
| Validation          | Recipient permission; request active                         |
| Business Rules      | Receipt timestamp starts processing milestone                |
| Permission          | requests.receive                                             |
| Database Impact     | Request, routing/resolution/relationship records             |
| API Impact          | POST /api/v1/requests/{id}/receive                           |
| Notification        | Requester notified                                           |
| AI involvement      | AI none                                                      |
| Dependencies        | M06/M08                                                      |
| Acceptance Criteria | Request becomes visibly owned and active.                    |

### FR-REQ-005 – Process Assigned Request

| **Item**            | **Specification**                                                      |
|---------------------|------------------------------------------------------------------------|
| Purpose             | Work on a request directly or through generated tasks.                 |
| Actor               | Employee/Manager assigned recipient                                    |
| Preconditions       | Request RECEIVED/IN_PROGRESS                                           |
| Trigger             | Start processing                                                       |
| Main Flow           | Create comments/progress; optionally create task; update state         |
| Alternative Flow    | If information missing -\> WAITING_FOR_INFORMATION                     |
| Exception Flow      | Unauthorized update/state                                              |
| Input               | RequestId, note, task link                                             |
| Output              | Updated request/process history                                        |
| Validation          | State transition and assignment checks                                 |
| Business Rules      | Request may have zero or more child tasks                              |
| Permission          | requests.process                                                       |
| Database Impact     | Request, routing/resolution/relationship records                       |
| API Impact          | POST /api/v1/requests/{id}/process-actions                             |
| Notification        | Requester may receive progress notification                            |
| AI involvement      | AI can summarize/recommend next step                                   |
| Dependencies        | M06/M05/M09                                                            |
| Acceptance Criteria | Authorized Employee/Manager can progress request and preserve history. |

### FR-REQ-006 – Create Task from Request

| **Item**            | **Specification**                                                  |
|---------------------|--------------------------------------------------------------------|
| Purpose             | Create a task linked to a request.                                 |
| Actor               | Manager / authorized Employee/Manager                              |
| Preconditions       | Request processing needs actionable work                           |
| Trigger             | Create-task action                                                 |
| Main Flow           | Create task with RequestId; route/assign; preserve parent relation |
| Alternative Flow    | Multiple tasks can be created                                      |
| Exception Flow      | Invalid request state                                              |
| Input               | RequestId, task fields                                             |
| Output              | Task linked to request                                             |
| Validation          | Task inherits tenant/service context                               |
| Business Rules      | Request remains open until workflow resolves it                    |
| Permission          | tasks.from-request                                                 |
| Database Impact     | Request, routing/resolution/relationship records                   |
| API Impact          | POST /api/v1/requests/{id}/tasks                                   |
| Notification        | Assignee notified                                                  |
| AI involvement      | AI may draft task fields                                           |
| Dependencies        | M05/M06/M12                                                        |
| Acceptance Criteria | Task appears in request timeline and can be tracked independently. |

### FR-REQ-007 – Resolve Request

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Mark request resolution ready for requester confirmation.      |
| Actor               | Assigned Employee/Manager / Manager                            |
| Preconditions       | Request IN_PROGRESS; resolution evidence present when required |
| Trigger             | Resolve action                                                 |
| Main Flow           | Validate completion criteria; set RESOLVED; record resolution  |
| Alternative Flow    | Workflow may require approval before resolve                   |
| Exception Flow      | Missing evidence/failed validation                             |
| Input               | RequestId, resolution, attachments                             |
| Output              | RESOLVED request                                               |
| Validation          | Resolution required; workflow conditions                       |
| Business Rules      | Resolution does not equal final close until confirmation       |
| Permission          | requests.resolve                                               |
| Database Impact     | Request, routing/resolution/relationship records               |
| API Impact          | POST /api/v1/requests/{id}/resolve                             |
| Notification        | Requester notified                                             |
| AI involvement      | AI can summarize/check completeness                            |
| Dependencies        | M06/M07/M10                                                    |
| Acceptance Criteria | Requester can review the resolution.                           |

### FR-REQ-008 – Confirm Request Resolution

| **Item**            | **Specification**                                                                          |
|---------------------|--------------------------------------------------------------------------------------------|
| Purpose             | Requester confirms or rejects resolution.                                                  |
| Actor               | Employee requester                                                                         |
| Preconditions       | Request RESOLVED and requester owns request                                                |
| Trigger             | Confirm action                                                                             |
| Main Flow           | Confirm -\> CLOSED; reject -\> configured rework path/IN_PROGRESS with reason              |
| Alternative Flow    | Rejection must preserve prior resolution history                                           |
| Exception Flow      | Unauthorized requester                                                                     |
| Input               | RequestId, decision, note                                                                  |
| Output              | Confirmation + final state                                                                 |
| Validation          | Requester identity; decision required                                                      |
| Business Rules      | Critical milestone confirmation recorded                                                   |
| Permission          | requests.confirm                                                                           |
| Database Impact     | Request, routing/resolution/relationship records                                           |
| API Impact          | POST /api/v1/requests/{id}/confirmation                                                    |
| Notification        | Assigned Employee/Manager notified                                                         |
| AI involvement      | AI may summarize resolution but not confirm on behalf of requester without explicit policy |
| Dependencies        | M06/M13                                                                                    |
| Acceptance Criteria | Confirmed resolution closes the request.                                                   |

### FR-REQ-009 – Create Revised Request from Rejected Request

| **Item**            | **Specification**                                                                                     |
|---------------------|-------------------------------------------------------------------------------------------------------|
| Purpose             | Reuse a rejected request as the basis for a new submission.                                           |
| Actor               | Employee requester / authorized user                                                                  |
| Preconditions       | Original request REJECTED                                                                             |
| Trigger             | Select “Create Revised Request”                                                                       |
| Main Flow           | Create new DRAFT request; copy safe fields; link RevisedFromRequestId; allow edits; submit separately |
| Alternative Flow    | User may choose attachments to reuse                                                                  |
| Exception Flow      | Original not rejected / access denied                                                                 |
| Input               | Original RequestId + edited fields                                                                    |
| Output              | New RequestId in DRAFT or SUBMITTED                                                                   |
| Validation          | Original remains immutable; copied fields revalidated                                                 |
| Business Rules      | New request has independent lifecycle and audit                                                       |
| Permission          | request.009                                                                                           |
| Database Impact     | Request, routing/resolution/relationship records                                                      |
| API Impact          | POST /api/v1/requests/{id}/revise                                                                     |
| Notification        | New request notification after submit                                                                 |
| AI involvement      | AI may help rewrite/satisfy missing data                                                              |
| Dependencies        | M06/M10/M13                                                                                           |
| Acceptance Criteria | Rejected original remains REJECTED and new request is independently traceable.                        |

## 7.7 Workflow & Approval

### FR-WF-001 – Define Workflow Version

| **Item**            | **Specification**                                           |
|---------------------|-------------------------------------------------------------|
| Purpose             | Create a versioned workflow with ordered steps/transitions. |
| Actor               | Company Administrator                                       |
| Preconditions       | Tenant active                                               |
| Trigger             | Save/publish workflow                                       |
| Main Flow           | Validate graph; create immutable version on publish         |
| Alternative Flow    | Draft version can be edited before publish                  |
| Exception Flow      | Cycle/invalid transition                                    |
| Input               | Workflow metadata, steps, transitions                       |
| Output              | Published workflow version                                  |
| Validation          | At least one entry/exit; valid references                   |
| Business Rules      | Published versions immutable                                |
| Permission          | workflows.configure                                         |
| Database Impact     | Workflow/approval runtime and version records               |
| API Impact          | POST /api/v1/workflows; POST /api/v1/workflows/{id}/publish |
| Notification        | Config audit                                                |
| AI involvement      | AI can recommend but not publish                            |
| Dependencies        | M07                                                         |
| Acceptance Criteria | Valid workflow version can be selected by a service.        |

### FR-WF-002 – Execute Workflow Transition

| **Item**            | **Specification**                                                          |
|---------------------|----------------------------------------------------------------------------|
| Purpose             | Move Task/Request to next valid state according to workflow.               |
| Actor               | System + authorized actor                                                  |
| Preconditions       | Object active; transition exists                                           |
| Trigger             | Action/event                                                               |
| Main Flow           | Evaluate guards; record transition; trigger side effects                   |
| Alternative Flow    | Rejected transition returns validation error                               |
| Exception Flow      | Guard failure                                                              |
| Input               | ObjectId, transition, actor                                                |
| Output              | New state + transition log                                                 |
| Validation          | Current state must match source; actor authorized                          |
| Business Rules      | No direct status update bypassing workflow for workflow-controlled objects |
| Permission          | workflow.002                                                               |
| Database Impact     | Workflow/approval runtime and version records                              |
| API Impact          | POST /api/v1/workflow-instances/{id}/transition                            |
| Notification        | Notifications based on transition                                          |
| AI involvement      | AI may request transition via tool but business engine validates           |
| Dependencies        | M07/M05/M06                                                                |
| Acceptance Criteria | Invalid transitions are blocked.                                           |

### FR-WF-003 – Create Approval Instance

| **Item**            | **Specification**                                         |
|---------------------|-----------------------------------------------------------|
| Purpose             | Start approval process for object.                        |
| Actor               | System / Manager                                          |
| Preconditions       | Workflow step requires approval                           |
| Trigger             | Enter approval step                                       |
| Main Flow           | Resolve approvers; create approval instance/steps; notify |
| Alternative Flow    | If no approver found -\> blocked/manual escalation        |
| Exception Flow      | Unavailable approver                                      |
| Input               | ObjectId, rule version                                    |
| Output              | Approval instance                                         |
| Validation          | Approver active and in scope                              |
| Business Rules      | Approval rule snapshot preserved on instance              |
| Permission          | workflow.003                                              |
| Database Impact     | Workflow/approval runtime and version records             |
| API Impact          | POST /api/v1/approvals                                    |
| Notification        | Approver notification                                     |
| AI involvement      | AI may recommend but not approve                          |
| Dependencies        | M07/M09                                                   |
| Acceptance Criteria | Approvers receive an actionable approval request.         |

### FR-WF-004 – Record Approval Decision

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Approver approves or rejects an approval step.                 |
| Actor               | Manager / authorized approver                                  |
| Preconditions       | Pending approval; user in approver list                        |
| Trigger             | Decision submit                                                |
| Main Flow           | Validate decision; record; advance or reject workflow          |
| Alternative Flow    | Reject sends object to REJECTED or revision path per workflow  |
| Exception Flow      | Duplicate decision/expired approval                            |
| Input               | ApprovalId, decision, reason                                   |
| Output              | Approval action + workflow result                              |
| Validation          | Reason required on reject; idempotency                         |
| Business Rules      | Decision cannot be overwritten; correction is new action       |
| Permission          | workflow.004                                                   |
| Database Impact     | Workflow/approval runtime and version records                  |
| API Impact          | POST /api/v1/approvals/{id}/decision                           |
| Notification        | Requester/next approver notified                               |
| AI involvement      | AI can summarize context only                                  |
| Dependencies        | M07/M13                                                        |
| Acceptance Criteria | Approval decision changes workflow correctly and is auditable. |

## 7.8 SLA & Escalation

### FR-SLA-001 – Configure SLA Profile

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Define processing target and warning/escalation rules.         |
| Actor               | Company Administrator                                          |
| Preconditions       | Tenant active                                                  |
| Trigger             | Save SLA                                                       |
| Main Flow           | Set duration, calendar, warning thresholds, escalation actions |
| Alternative Flow    | Version when used by active objects                            |
| Exception Flow      | Invalid duration/calendar                                      |
| Input               | Service/Request type, target, warning, escalation              |
| Output              | SLA profile version                                            |
| Validation          | Nonnegative duration; valid calendar                           |
| Business Rules      | Version immutable for active transactions                      |
| Permission          | sla.configure                                                  |
| Database Impact     | SLA profile/runtime/escalation records                         |
| API Impact          | POST /api/v1/sla-profiles                                      |
| Notification        | Config audit                                                   |
| AI involvement      | AI can recommend SLA but cannot publish automatically          |
| Dependencies        | M08                                                            |
| Acceptance Criteria | Published SLA profile is selectable by service/workflow.       |

### FR-SLA-002 – Monitor SLA Clock

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Track elapsed processing time.                                 |
| Actor               | System                                                         |
| Preconditions       | Object has active SLA                                          |
| Trigger             | Scheduler tick/event                                           |
| Main Flow           | Calculate elapsed time under tenant calendar; update SLA state |
| Alternative Flow    | Paused when configured waiting state                           |
| Exception Flow      | Clock calculation failure                                      |
| Input               | ObjectId, timestamps, calendar                                 |
| Output              | SLA metrics/state                                              |
| Validation          | Consistent timezone/calendar                                   |
| Business Rules      | One authoritative server time source                           |
| Permission          | sla.002                                                        |
| Database Impact     | SLA profile/runtime/escalation records                         |
| API Impact          | Background job/internal service                                |
| Notification        | Warning notification when threshold reached                    |
| AI involvement      | AI receives risk context                                       |
| Dependencies        | M08                                                            |
| Acceptance Criteria | SLA state is updated predictably.                              |

### FR-SLA-003 – Escalate Overdue Work

| **Item**            | **Specification**                                               |
|---------------------|-----------------------------------------------------------------|
| Purpose             | Escalate when SLA/deadline is violated.                         |
| Actor               | System                                                          |
| Preconditions       | Object overdue and escalation configured                        |
| Trigger             | Scheduler detects overdue                                       |
| Main Flow           | Create escalation event; notify target; preserve previous owner |
| Alternative Flow    | Repeated scheduler run must not duplicate escalation            |
| Exception Flow      | Notification failure                                            |
| Input               | ObjectId, escalation rule                                       |
| Output              | Escalation event                                                |
| Validation          | Idempotent event key                                            |
| Business Rules      | At most one escalation per configured level                     |
| Permission          | sla.003                                                         |
| Database Impact     | SLA profile/runtime/escalation records                          |
| API Impact          | Internal job                                                    |
| Notification        | Manager/Admin notification                                      |
| AI involvement      | AI may recommend follow-up, not replace rule engine             |
| Dependencies        | M08/M09/M13                                                     |
| Acceptance Criteria | Overdue item is escalated exactly once per level.               |

### FR-SLA-004 – Pause SLA on Waiting for Information

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Pause SLA when workflow allows requester clarification state.  |
| Actor               | System                                                         |
| Preconditions       | Request WAITING_FOR_INFORMATION and policy says pause          |
| Trigger             | State change                                                   |
| Main Flow           | Freeze SLA clock; record pause reason/time; resume on response |
| Alternative Flow    | If policy says no pause, clock continues                       |
| Exception Flow      | Invalid state/policy                                           |
| Input               | ObjectId, reason                                               |
| Output              | SLA pause record                                               |
| Validation          | State must be configured as pausable                           |
| Business Rules      | Total paused duration traceable                                |
| Permission          | sla.004                                                        |
| Database Impact     | SLA profile/runtime/escalation records                         |
| API Impact          | Internal SLA service                                           |
| Notification        | Requester prompt/owner notification                            |
| AI involvement      | AI may detect missing info                                     |
| Dependencies        | M08/M06                                                        |
| Acceptance Criteria | Configured pause policy is honored.                            |

## 7.9 Collaboration, Evidence & History

### FR-COL-001 – Create Comment

| **Item**            | **Specification**                                           |
|---------------------|-------------------------------------------------------------|
| Purpose             | Add collaboration comment to Task/Request.                  |
| Actor               | Employee/Manager/Company Admin                              |
| Preconditions       | Authorized access                                           |
| Trigger             | Comment submit                                              |
| Main Flow           | Store comment; attach optional files; notify relevant users |
| Alternative Flow    | Edit/delete only within comment policy                      |
| Exception Flow      | Empty comment                                               |
| Input               | ObjectId, content, attachments                              |
| Output              | Comment                                                     |
| Validation          | Content required                                            |
| Business Rules      | Comment history retained                                    |
| Permission          | comments.create                                             |
| Database Impact     | Comment/Attachment/archive metadata                         |
| API Impact          | POST /api/v1/{tasks\|requests}/{id}/comments                |
| Notification        | Relevant users notified                                     |
| AI involvement      | AI may draft/rewrite but user submits                       |
| Dependencies        | M09/M10                                                     |
| Acceptance Criteria | Comment appears in timeline and is auditable.               |

### FR-COL-002 – Upload Attachment

| **Item**            | **Specification**                                                         |
|---------------------|---------------------------------------------------------------------------|
| Purpose             | Upload evidence file up to 500 MB.                                        |
| Actor               | Employee/Manager/Company Admin                                            |
| Preconditions       | Authorized object access                                                  |
| Trigger             | Upload initiation                                                         |
| Main Flow           | Request signed upload; upload direct to object storage; finalize metadata |
| Alternative Flow    | Large files use multipart/resumable upload                                |
| Exception Flow      | Type/size/storage failure                                                 |
| Input               | File metadata + binary                                                    |
| Output              | Attachment record                                                         |
| Validation          | Allowlist; max 500 MB; malware/content validation hook                    |
| Business Rules      | Tenant-prefixed object key; metadata in DB                                |
| Permission          | collaboration.002                                                         |
| Database Impact     | Comment/Attachment/archive metadata                                       |
| API Impact          | POST /api/v1/attachments/upload-session; POST /finalize                   |
| Notification        | Upload complete notification where needed                                 |
| AI involvement      | AI can inspect metadata/content only if feature enabled                   |
| Dependencies        | M10                                                                       |
| Acceptance Criteria | 500 MB file can be uploaded without proxying entire binary through API.   |

### FR-COL-003 – Archive Completed Record

| **Item**            | **Specification**                                                |
|---------------------|------------------------------------------------------------------|
| Purpose             | Move closed/completed records out of active views.               |
| Actor               | Authorized Manager/Company Admin                                 |
| Preconditions       | Task COMPLETED or Request CLOSED                                 |
| Trigger             | Archive action/retention job                                     |
| Main Flow           | Set archived timestamp; remove from active lists; retain history |
| Alternative Flow    | Cannot archive while pending approval/confirmation               |
| Exception Flow      | Invalid state                                                    |
| Input               | ObjectId                                                         |
| Output              | Archived record                                                  |
| Validation          | Only closed states                                               |
| Business Rules      | Archive is not hard delete                                       |
| Permission          | collaboration.003                                                |
| Database Impact     | Comment/Attachment/archive metadata                              |
| API Impact          | POST /api/v1/{tasks\|requests}/{id}/archive                      |
| Notification        | None                                                             |
| AI involvement      | AI summaries may exclude archived data by default                |
| Dependencies        | M05/M06/M13                                                      |
| Acceptance Criteria | Archived records remain searchable according to permission.      |

### FR-COL-004 – Search and Filter Records

| **Item**            | **Specification**                                                                    |
|---------------------|--------------------------------------------------------------------------------------|
| Purpose             | Search/filter Task and Request data in authorized scope.                             |
| Actor               | Employee/Manager/Company Admin                                                       |
| Preconditions       | Authenticated                                                                        |
| Trigger             | Search submit                                                                        |
| Main Flow           | Build scoped query; filter by status, service, department, assignee, dates, priority |
| Alternative Flow    | No results returns empty set                                                         |
| Exception Flow      | Invalid filter                                                                       |
| Input               | Query, filters, paging                                                               |
| Output              | Paged results                                                                        |
| Validation          | All filters tenant-scoped; pagination bounds                                         |
| Business Rules      | Search never bypasses authorization                                                  |
| Permission          | collaboration.004                                                                    |
| Database Impact     | Comment/Attachment/archive metadata                                                  |
| API Impact          | GET /api/v1/tasks; GET /api/v1/requests                                              |
| Notification        | None                                                                                 |
| AI involvement      | AI can translate natural-language search to filters as P2                            |
| Dependencies        | M05/M06                                                                              |
| Acceptance Criteria | Search returns only authorized records with stable pagination.                       |

## 7.10 Dashboard & Reporting

### FR-REP-001 – View Manager Dashboard

| **Item**            | **Specification**                                            |
|---------------------|--------------------------------------------------------------|
| Purpose             | Show workload and task/request operational metrics.          |
| Actor               | Manager                                                      |
| Preconditions       | Authenticated                                                |
| Trigger             | Open dashboard                                               |
| Main Flow           | Aggregate counts, workload, overdue, completion, SLA metrics |
| Alternative Flow    | Drill-down only to authorized data                           |
| Exception Flow      | Aggregation failure                                          |
| Input               | Date range + department filters                              |
| Output              | Dashboard metrics                                            |
| Validation          | Tenant scope; time range limits                              |
| Business Rules      | Metrics use a documented definition                          |
| Permission          | report.001                                                   |
| Database Impact     | Read-only aggregation/report queries                         |
| API Impact          | GET /api/v1/dashboard/manager                                |
| Notification        | None                                                         |
| AI involvement      | AI summary optional                                          |
| Dependencies        | M11                                                          |
| Acceptance Criteria | Manager dashboard metrics match underlying data.             |

### FR-REP-002 – View Company Report

| **Item**            | **Specification**                           |
|---------------------|---------------------------------------------|
| Purpose             | View company-level operational report.      |
| Actor               | Company Administrator                       |
| Preconditions       | Authenticated                               |
| Trigger             | Open report                                 |
| Main Flow           | Aggregate tenant-wide metrics within role   |
| Alternative Flow    | Large range may use async export            |
| Exception Flow      | Aggregation failure                         |
| Input               | Date range, filters                         |
| Output              | Report data                                 |
| Validation          | Tenant scope; metric definition             |
| Business Rules      | No cross-tenant aggregation                 |
| Permission          | report.002                                  |
| Database Impact     | Read-only aggregation/report queries        |
| API Impact          | GET /api/v1/reports/company                 |
| Notification        | Optional report notification                |
| AI involvement      | AI can summarize                            |
| Dependencies        | M11/M13                                     |
| Acceptance Criteria | Company Admin sees tenant-wide report only. |

### FR-REP-003 – Generate Workload Report

| **Item**            | **Specification**                                                  |
|---------------------|--------------------------------------------------------------------|
| Purpose             | Calculate workload by user/department.                             |
| Actor               | Manager/Company Admin                                              |
| Preconditions       | Authorized report scope                                            |
| Trigger             | Generate report                                                    |
| Main Flow           | Aggregate active and recently completed tasks; configurable period |
| Alternative Flow    | If no historical data, return zero/empty                           |
| Exception Flow      | Invalid dates                                                      |
| Input               | Date range, department                                             |
| Output              | Workload report                                                    |
| Validation          | Range \<= configurable max                                         |
| Business Rules      | Exclude cancelled from active workload                             |
| Permission          | report.003                                                         |
| Database Impact     | Read-only aggregation/report queries                               |
| API Impact          | GET /api/v1/reports/workload                                       |
| Notification        | None                                                               |
| AI involvement      | AI can explain outliers                                            |
| Dependencies        | M11                                                                |
| Acceptance Criteria | Workload report matches task assignments and statuses.             |

### FR-REP-004 – Generate SLA Performance Report

| **Item**            | **Specification**                                              |
|---------------------|----------------------------------------------------------------|
| Purpose             | Report on SLA compliance.                                      |
| Actor               | Manager/Company Admin                                          |
| Preconditions       | Authorized report scope                                        |
| Trigger             | Generate report                                                |
| Main Flow           | Compute compliance, overdue count, average elapsed/paused time |
| Alternative Flow    | Incomplete SLA records flagged                                 |
| Exception Flow      | Missing timestamps                                             |
| Input               | Date range, service                                            |
| Output              | SLA report                                                     |
| Validation          | Well-defined metric formulas                                   |
| Business Rules      | Uses server timestamps and SLA snapshots                       |
| Permission          | report.004                                                     |
| Database Impact     | Read-only aggregation/report queries                           |
| API Impact          | GET /api/v1/reports/sla                                        |
| Notification        | None                                                           |
| AI involvement      | AI can summarize trends                                        |
| Dependencies        | M08/M11                                                        |
| Acceptance Criteria | SLA metrics are reproducible from stored records.              |

## 7.11 AI-Assisted Operations

### FR-AI-001 – Assist Task Creation

| **Item**            | **Specification**                                                      |
|---------------------|------------------------------------------------------------------------|
| Purpose             | Turn natural-language manager instruction into a structured draft.     |
| Actor               | Manager + AI Agent                                                     |
| Preconditions       | AI enabled                                                             |
| Trigger             | User requests AI assistance                                            |
| Main Flow           | Gather tenant context; call LLM; validate schema; show draft fields    |
| Alternative Flow    | Missing details highlighted for user                                   |
| Exception Flow      | LLM failure/schema mismatch                                            |
| Input               | Natural-language task instruction + allowed context                    |
| Output              | TaskDraftSuggestion                                                    |
| Validation          | Strict JSON schema; field/reference validation                         |
| Business Rules      | AI output never directly persists task without policy                  |
| Permission          | ai.001                                                                 |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                     |
| API Impact          | POST /api/v1/ai/task-assistance                                        |
| Notification        | AI activity status                                                     |
| AI involvement      | Core AI                                                                |
| Dependencies        | M12/M05                                                                |
| Acceptance Criteria | AI returns editable task draft with structured fields or safe failure. |

### FR-AI-002 – Recommend Task Assignment

| **Item**            | **Specification**                                                              |
|---------------------|--------------------------------------------------------------------------------|
| Purpose             | Recommend department/assignee for a task.                                      |
| Actor               | Manager + AI Agent                                                             |
| Preconditions       | Task draft/context available                                                   |
| Trigger             | Request recommendation                                                         |
| Main Flow           | Use task content + org responsibilities + workload + history; score candidates |
| Alternative Flow    | No suitable candidate -\> manual selection                                     |
| Exception Flow      | Data unavailable/low confidence                                                |
| Input               | Task draft + candidate context                                                 |
| Output              | Recommendation with confidence + reasons                                       |
| Validation          | Referenced users/departments valid; confidence 0-1                             |
| Business Rules      | Recommendation not final assignment by default                                 |
| Permission          | ai.002                                                                         |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                             |
| API Impact          | POST /api/v1/ai/task-assignment-recommendation                                 |
| Notification        | Manager notification only if configured                                        |
| AI involvement      | Core AI                                                                        |
| Dependencies        | M12/M03/M05                                                                    |
| Acceptance Criteria | Manager can accept/override recommendation and final assignment is logged.     |

### FR-AI-003 – Recommend Task Parameters

| **Item**            | **Specification**                                                                          |
|---------------------|--------------------------------------------------------------------------------------------|
| Purpose             | Suggest priority, deadline, checklist, workflow and SLA.                                   |
| Actor               | Manager + AI Agent                                                                         |
| Preconditions       | Task content/service context                                                               |
| Trigger             | AI assistance                                                                              |
| Main Flow           | Return structured suggestions grounded in service/workflow catalog                         |
| Alternative Flow    | Omit uncertain fields rather than invent                                                   |
| Exception Flow      | Invalid enum/date                                                                          |
| Input               | Task text + service catalog + workflow metadata                                            |
| Output              | Recommendation payload                                                                     |
| Validation          | Schema + catalog validation                                                                |
| Business Rules      | AI may suggest only options that exist in tenant                                           |
| Permission          | ai.003                                                                                     |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                                         |
| API Impact          | POST /api/v1/ai/task-parameters                                                            |
| Notification        | None                                                                                       |
| AI involvement      | Core AI                                                                                    |
| Dependencies        | M12/M04/M07/M08                                                                            |
| Acceptance Criteria | Every recommendation references a valid tenant-configured option or is marked unavailable. |

### FR-AI-004 – Break Down Task

| **Item**            | **Specification**                                     |
|---------------------|-------------------------------------------------------|
| Purpose             | Propose subtasks for a complex task.                  |
| Actor               | Manager + AI Agent                                    |
| Preconditions       | Task draft/active and feature enabled                 |
| Trigger             | Ask AI to break down                                  |
| Main Flow           | Generate ordered subtask draft list with dependencies |
| Alternative Flow    | Single task if decomposition not justified            |
| Exception Flow      | Too many/duplicate subtasks                           |
| Input               | Task description/context                              |
| Output              | Subtask plan                                          |
| Validation          | Max subtask count; no cyclic dependencies             |
| Business Rules      | User reviews before creation                          |
| Permission          | ai.004                                                |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records    |
| API Impact          | POST /api/v1/ai/task-breakdown                        |
| Notification        | None                                                  |
| AI involvement      | Core AI                                               |
| Dependencies        | M12/M05                                               |
| Acceptance Criteria | Subtasks can be edited and approved before creation.  |

### FR-AI-005 – Monitor Task Risk

| **Item**            | **Specification**                                                         |
|---------------------|---------------------------------------------------------------------------|
| Purpose             | Identify tasks at risk of missing deadline/SLA.                           |
| Actor               | AI Agent / Manager                                                        |
| Preconditions       | Task active; progress/SLA data available                                  |
| Trigger             | Scheduled evaluation/dashboard query                                      |
| Main Flow           | Analyze current progress against time remaining; produce risk and reasons |
| Alternative Flow    | Insufficient data -\> unknown risk                                        |
| Exception Flow      | AI timeout                                                                |
| Input               | Task progress + deadline/SLA context                                      |
| Output              | Risk recommendation                                                       |
| Validation          | Schema; bounded scores; no state mutation                                 |
| Business Rules      | Rule engine remains authoritative for overdue state                       |
| Permission          | ai.005                                                                    |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                        |
| API Impact          | POST /api/v1/ai/task-risk                                                 |
| Notification        | Optional manager alert                                                    |
| AI involvement      | Core AI                                                                   |
| Dependencies        | M12/M08                                                                   |
| Acceptance Criteria | Risk result is informative and does not override official SLA state.      |

### FR-AI-006 – Summarize Task Progress

| **Item**            | **Specification**                                                       |
|---------------------|-------------------------------------------------------------------------|
| Purpose             | Summarize progress/history into a concise update.                       |
| Actor               | Manager/Employee + AI Agent                                             |
| Preconditions       | User authorized to view task                                            |
| Trigger             | Summary request                                                         |
| Main Flow           | Gather task events/reports; summarize facts; cite relevant dates/actors |
| Alternative Flow    | If no history, state insufficient data                                  |
| Exception Flow      | AI failure                                                              |
| Input               | Task timeline                                                           |
| Output              | Summary text/structured bullets                                         |
| Validation          | No fabricated events; output based on retrieved context                 |
| Business Rules      | Only authorized task data included                                      |
| Permission          | ai.006                                                                  |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                      |
| API Impact          | POST /api/v1/ai/task-summary                                            |
| Notification        | None                                                                    |
| AI involvement      | Core AI                                                                 |
| Dependencies        | M05/M13                                                                 |
| Acceptance Criteria | Summary matches stored timeline within tested factual checks.           |

### FR-AI-007 – Analyze Multi-intent Request

| **Item**            | **Specification**                                         |
|---------------------|-----------------------------------------------------------|
| Purpose             | Detect multiple distinct business intents in one request. |
| Actor               | Employee/Manager + AI Agent                               |
| Preconditions       | Request submitted                                         |
| Trigger             | AI analysis                                               |
| Main Flow           | Detect intents; propose split; preserve parent relation   |
| Alternative Flow    | Single intent -\> no split recommendation                 |
| Exception Flow      | AI failure/low confidence                                 |
| Input               | Request content + service catalog                         |
| Output              | Intent list + confidence                                  |
| Validation          | Schema; min/max intent count; no invented data            |
| Business Rules      | Original request remains intact                           |
| Permission          | ai.007                                                    |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records        |
| API Impact          | POST /api/v1/ai/request-multi-intent                      |
| Notification        | Employee/manager review prompt                            |
| AI involvement      | Core AI                                                   |
| Dependencies        | M12/M06                                                   |
| Acceptance Criteria | Complex request is split only after validation/review.    |

### FR-AI-008 – Split Request into Child Requests

| **Item**            | **Specification**                                                  |
|---------------------|--------------------------------------------------------------------|
| Purpose             | Create proposed child requests for each intent.                    |
| Actor               | AI Agent + authorized human                                        |
| Preconditions       | Multi-intent recommendation exists                                 |
| Trigger             | Confirm split                                                      |
| Main Flow           | Create child drafts linked to parent; copy only safe context       |
| Alternative Flow    | Human can merge/edit before submit                                 |
| Exception Flow      | Duplicate child/conflicting routing                                |
| Input               | ParentId + intent data                                             |
| Output              | Child request drafts                                               |
| Validation          | Each child has valid service/category or marked for manual routing |
| Business Rules      | Children have independent lifecycle; parent remains traceable      |
| Permission          | ai.008                                                             |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                 |
| API Impact          | POST /api/v1/requests/{id}/split                                   |
| Notification        | Child draft notification optional                                  |
| AI involvement      | Core AI                                                            |
| Dependencies        | M06/M12                                                            |
| Acceptance Criteria | No child is created without parent relation and audit record.      |

### FR-AI-009 – Recommend Request Routing

| **Item**            | **Specification**                                                         |
|---------------------|---------------------------------------------------------------------------|
| Purpose             | Recommend service/category/department/person/priority/workflow/SLA.       |
| Actor               | Employee/Manager + AI Agent                                               |
| Preconditions       | Request content and tenant configuration available                        |
| Trigger             | AI analysis                                                               |
| Main Flow           | Retrieve allowed candidates; generate structured recommendation; validate |
| Alternative Flow    | Low confidence -\> manual routing                                         |
| Exception Flow      | Invalid recommendation                                                    |
| Input               | Request + service/org/workflow/SLA context                                |
| Output              | Recommendation payload                                                    |
| Validation          | All IDs same tenant; confidence bounded                                   |
| Business Rules      | Final routing requires human/workflow authority                           |
| Permission          | ai.009                                                                    |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                        |
| API Impact          | POST /api/v1/ai/request-routing                                           |
| Notification        | Manual review notification                                                |
| AI involvement      | Core AI                                                                   |
| Dependencies        | M04/M06/M07/M08/M12                                                       |
| Acceptance Criteria | Recommendation is actionable but does not bypass authorization.           |

### FR-AI-010 – Execute Authorized AI Action

| **Item**            | **Specification**                                                                            |
|---------------------|----------------------------------------------------------------------------------------------|
| Purpose             | Allow AI to invoke a controlled business tool.                                               |
| Actor               | AI Agent                                                                                     |
| Preconditions       | Tool enabled; current user has authority; action policy allows                               |
| Trigger             | Tool call                                                                                    |
| Main Flow           | Validate tool schema; authorize current user/tenant; execute application service; log action |
| Alternative Flow    | Requires confirmation -\> pause and request human confirmation                               |
| Exception Flow      | Tool denied, business-rule failure, stale data                                               |
| Input               | Tool name + structured args + user/tenant context                                            |
| Output              | Tool result                                                                                  |
| Validation          | Strict schema; permission; tenant; business rules; idempotency                               |
| Business Rules      | AI never accesses DB directly                                                                |
| Permission          | ai.010                                                                                       |
| Database Impact     | AIInteraction/Recommendation/AIAgentAction records                                           |
| API Impact          | POST /api/v1/ai/actions/execute                                                              |
| Notification        | Action result + audit                                                                        |
| AI involvement      | Core AI                                                                                      |
| Dependencies        | M12 + target module                                                                          |
| Acceptance Criteria | Unauthorized or invalid actions are rejected and logged.                                     |

Total canonical Functional Requirements in this baseline: 62.

# 8. Core Business Workflows

## 8.1 Manager-to-Employee Task Workflow

1.  Manager enters a task manually or asks AI to interpret a natural-language instruction.

2.  AI returns structured suggestions (optional): title, description, department/assignee, priority, deadline, checklist, workflow/SLA.

3.  Manager reviews and accepts/overrides suggestions.

4.  System validates tenant, permissions, references and workflow constraints.

5.  Task is created and assigned.

6.  Employee accepts the task or rejects the assignment with a reason.

7.  Employee executes the task and updates progress/report/evidence.

8.  Employee submits the final result.

9.  Manager reviews evidence and confirms or rejects the result according to workflow.

10. On confirmation, the task reaches COMPLETED; all milestones are audited.

## 8.2 Employee-to-Department Request Workflow

11. Employee creates a request from a structured form or natural-language text.

12. AI optionally analyzes content, extracts fields, detects missing information and identifies multiple intents.

13. If multiple intents are detected, the system proposes child requests; human review is required before creating/dispatching them unless an explicit tenant policy allows otherwise.

14. AI or routing rules recommend service, department, assignee, priority, workflow and SLA.

15. System validates final routing; request enters ROUTED then RECEIVED.

16. Assigned Employee/Manager works the request, adds comments/evidence, and may create one or more tasks.

17. When resolution criteria are met, the request enters RESOLVED.

18. Requester confirms resolution; if accepted, request becomes CLOSED.

19. If rejected, original Request remains REJECTED and the requester may create a new revised request linked to the original.

## 8.3 Rejected Request Revision Workflow

20. Original request is REJECTED with a reason.

21. Requester selects “Create Revised Request”.

22. System copies allowed fields into a new DRAFT and sets RevisedFromRequestId to the rejected request.

23. Requester edits missing/incorrect information and may explicitly reuse selected attachments.

24. AI may assist with rewriting, completion and routing suggestions.

25. New request is independently submitted and processed.

26. Original rejected request remains unchanged except for immutable audit links.

# 9. State Machines

## 9.1 Task

| **Current State**     | **Transition** | **Who**                  | **Condition**                    |
|-----------------------|----------------|--------------------------|----------------------------------|
| DRAFT                 | ASSIGNED       | Manager                  | Target validated                 |
| ASSIGNED              | ACCEPTED       | Employee                 | Employee is assigned target      |
| ASSIGNED              | REJECTED       | Employee                 | Reason required                  |
| REJECTED              | ASSIGNED       | Manager                  | Task corrected/reassigned        |
| ACCEPTED              | IN_PROGRESS    | Employee                 | Employee starts work             |
| IN_PROGRESS           | SUBMITTED      | Employee                 | Result submitted                 |
| OVERDUE               | IN_PROGRESS    | Employee                 | Work resumes                     |
| SUBMITTED             | CONFIRMED      | Manager                  | Result accepted                  |
| SUBMITTED             | IN_PROGRESS    | Manager                  | Rework required by workflow      |
| CONFIRMED             | COMPLETED      | System/Manager           | Confirmation milestone satisfied |
| Any active            | CANCELLED      | Authorized Manager/Admin | Reason required                  |
| IN_PROGRESS/SUBMITTED | OVERDUE        | System                   | Deadline/SLA threshold crossed   |

OVERDUE is treated as a lifecycle status that can return to active execution; it is not a terminal state.

## 9.2 Request

| **Current State**       | **Transition**          | **Who**                     | **Condition**                       |
|-------------------------|-------------------------|-----------------------------|-------------------------------------|
| DRAFT                   | SUBMITTED               | Employee                    | Required fields valid               |
| SUBMITTED               | ROUTED                  | System/Manager              | Routing successful                  |
| ROUTED                  | RECEIVED                | Assigned Employee/Manager   | Recipient accepts                   |
| RECEIVED                | IN_PROGRESS             | Assigned Employee/Manager   | Processing begins                   |
| IN_PROGRESS             | WAITING_FOR_INFORMATION | Assigned Employee/System    | More requester information required |
| WAITING_FOR_INFORMATION | IN_PROGRESS             | Requester/Assigned Employee | Information supplied                |
| IN_PROGRESS             | RESOLVED                | Employee/Manager            | Resolution criteria satisfied       |
| RESOLVED                | CONFIRMED               | Requester                   | Requester accepts resolution        |
| CONFIRMED               | CLOSED                  | System                      | Close conditions satisfied          |
| SUBMITTED/ROUTED        | REJECTED                | Authorized approver/manager | Rejection reason required           |
| Any active              | CANCELLED               | Authorized actor            | Cancellation allowed by workflow    |
| Active                  | OVERDUE                 | System                      | SLA/deadline exceeded               |

AI_ANALYZED is intentionally not a Request state. AI analysis is an internal activity recorded in AIInteraction/AuditLog while the business lifecycle remains SUBMITTED → ROUTED.

# 10. Business Rules

| **Rule ID** | **Rule**                 | **Statement**                                                                                                                                         |
|-------------|--------------------------|-------------------------------------------------------------------------------------------------------------------------------------------------------|
| BR-001      | Tenant isolation         | Every tenant-owned record has TenantId and every business query/action is evaluated against current tenant scope.                                     |
| BR-002      | Role boundary            | System roles establish baseline capabilities; custom tenant roles can only grant allowed permission catalog items.                                    |
| BR-003      | Manager scope            | A Manager can assign/reassign only within configured management scope.                                                                                |
| BR-004      | Department assignment    | A task/request assigned to a department represents a department queue/team responsibility until a user accepts or is assigned.                        |
| BR-005      | AI recommendation        | AI recommendations are not final business decisions unless a tenant-configured policy explicitly permits automatic action.                            |
| BR-006      | AI action authorization  | Every AI tool call is evaluated using current user identity, tenant, permission, resource scope and business rules.                                   |
| BR-007      | AI direct DB access      | AI never writes to the database directly; all mutations go through application services/tools.                                                        |
| BR-008      | Request revision         | REJECTED Request is immutable as a historical transaction; revision creates a new Request linked through RevisedFromRequestId.                        |
| BR-009      | Workflow immutability    | Published workflow versions cannot be edited in place when referenced by active transactions.                                                         |
| BR-010      | SLA versioning           | Active transactions use an SLA snapshot/version, so later configuration changes do not alter historical calculations.                                 |
| BR-011      | Approval audit           | Approval decisions are append-only actions; no in-place overwrite.                                                                                    |
| BR-012      | Critical confirmation    | Two-sided confirmation is required only at configured critical milestones such as acceptance, result confirmation or request resolution confirmation. |
| BR-013      | Evidence                 | Evidence attachment metadata is tenant-scoped; object storage keys include tenant context.                                                            |
| BR-014      | File size                | No uploaded file may exceed 500 MB.                                                                                                                   |
| BR-015      | Soft delete              | Business records use soft delete/deactivation where deletion is allowed; audit logs are immutable.                                                    |
| BR-016      | Completion               | A task cannot be COMPLETED before required result submission and configured confirmation.                                                             |
| BR-017      | Request closure          | A request cannot be CLOSED until resolution confirmation and required workflow conditions are satisfied.                                              |
| BR-018      | Overdue                  | OVERDUE is system-detected; users cannot manually mark a record overdue.                                                                              |
| BR-019      | Notification idempotency | Repeated scheduler runs must not duplicate the same notification/escalation event.                                                                    |
| BR-020      | Audit coverage           | Security-sensitive and lifecycle-changing actions must create audit records.                                                                          |
| BR-021      | AI confidence            | Confidence is advisory metadata; it cannot by itself override business rules.                                                                         |
| BR-022      | Missing information      | When required data is missing and the workflow supports waiting, request may enter WAITING_FOR_INFORMATION; SLA pause follows tenant SLA policy.      |

# 11. Validation Rules

| **Area**   | **Validation**                                                                                                                                                    |
|------------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Identity   | EmployeeCode required and unique within tenant; company email unique within tenant; password meets security policy; inactive/locked accounts cannot authenticate. |
| Tenant     | TenantId required on tenant-owned entity; all foreign keys must reference same tenant unless relationship is platform-global.                                     |
| Task       | Title required; deadline cannot violate workflow/business policy; assignee must be active and within manager scope; progress 0-100.                               |
| Request    | Service/category required according to tenant form; title/content required; referenced department/employee must belong to same tenant.                            |
| Workflow   | Published workflow must have valid start/end path; transitions reference existing states; approval steps have at least one valid approver rule.                   |
| SLA        | Duration nonnegative; warning threshold less than target; calendar valid; escalation target valid.                                                                |
| Attachment | Allowed content types only; file size \<= 500 MB; object key tenant-scoped; upload session expires.                                                               |
| AI         | Structured output matches schema; enum/ID values verified against tenant configuration; no model-generated ID is trusted without lookup validation.               |
| Search     | Maximum page size; all filters are authorization-scoped; free-text query sanitized and parameterized.                                                             |

# 12. Database Design & Data Dictionary

Recommended database: PostgreSQL 18 with EF Core 10/Npgsql. The conceptual model below is the implementation baseline; exact column lengths may be adjusted during migration design as long as semantics and constraints are preserved.

| **Entity**           | **Purpose**                                    | **Key fields**                                                                                                                                                                                                                                 |
|----------------------|------------------------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Tenant               | Company workspace and isolation boundary       | TenantId UUID PK; CompanyId UUID FK; TenantKey; Name; Status; TimeZone; CreatedAt; UpdatedAt                                                                                                                                                   |
| Company              | Legal/business identity registered on platform | CompanyId UUID PK; Name; Code; ContactEmail; Status; CreatedAt                                                                                                                                                                                 |
| User                 | Authenticated person inside tenant             | UserId UUID PK; TenantId FK; EmployeeCode; Email; PasswordHash; FullName; DepartmentId; Status; LastLoginAt; CreatedAt; UpdatedAt; DeletedAt                                                                                                   |
| Department           | Organizational unit                            | DepartmentId UUID PK; TenantId FK; Name; Code; ParentDepartmentId nullable; Status; CreatedAt; UpdatedAt                                                                                                                                       |
| Role                 | Tenant/system role definition                  | RoleId UUID PK; TenantId nullable for system role; Name; IsSystem; Status; CreatedAt                                                                                                                                                           |
| Permission           | Atomic permission catalog item                 | PermissionId UUID PK; Code; Module; Action; ScopeType                                                                                                                                                                                          |
| RolePermission       | Role-to-permission junction                    | RoleId FK; PermissionId FK; PK(RoleId,PermissionId)                                                                                                                                                                                            |
| UserRole             | User-to-role junction                          | UserId FK; RoleId FK; PK(UserId,RoleId)                                                                                                                                                                                                        |
| Service              | Internal business service                      | ServiceId UUID PK; TenantId; Code; Name; Description; Status; ActiveWorkflowVersionId; ActiveSLAVersionId                                                                                                                                      |
| ServiceCategory      | Category under service                         | ServiceCategoryId; ServiceId; Code; Name; Status                                                                                                                                                                                               |
| RoutingRule          | Service routing rule                           | RoutingRuleId; ServiceId; PriorityOrder; ConditionsJson; TargetDepartmentId; TargetRoleId; Active                                                                                                                                              |
| Workflow             | Logical workflow definition                    | WorkflowId; TenantId; Name; BusinessType; Status                                                                                                                                                                                               |
| WorkflowVersion      | Immutable published workflow version           | WorkflowVersionId; WorkflowId; VersionNo; Status; PublishedAt; DefinitionJson/normalized steps                                                                                                                                                 |
| WorkflowStep         | Workflow step                                  | WorkflowStepId; WorkflowVersionId; StepCode; Name; Type; OrderNo; ConfigJson                                                                                                                                                                   |
| WorkflowTransition   | Allowed transition                             | TransitionId; WorkflowVersionId; FromState; ToState; GuardJson                                                                                                                                                                                 |
| ApprovalRule         | Logical approval rule                          | ApprovalRuleId; TenantId; Name; ObjectType; Status                                                                                                                                                                                             |
| ApprovalRuleVersion  | Versioned approval definition                  | ApprovalRuleVersionId; ApprovalRuleId; VersionNo; DefinitionJson; PublishedAt                                                                                                                                                                  |
| ApprovalInstance     | Runtime approval instance                      | ApprovalInstanceId; TenantId; ObjectType; ObjectId; RuleVersionId; Status; StartedAt; CompletedAt                                                                                                                                              |
| ApprovalStepInstance | Runtime approval step                          | ApprovalStepInstanceId; ApprovalInstanceId; ApproverUserId/Role/Department; SequenceNo; Status; DecisionAt; DecisionReason                                                                                                                     |
| SLAProfile           | Logical SLA configuration                      | SLAProfileId; TenantId; Name; Status                                                                                                                                                                                                           |
| SLAVersion           | Versioned SLA rules                            | SLAVersionId; SLAProfileId; TargetMinutes; WarningMinutes; CalendarId; EscalationConfigJson                                                                                                                                                    |
| BusinessCalendar     | Tenant working calendar                        | CalendarId; TenantId; TimeZone; WorkingHoursJson; HolidaysJson                                                                                                                                                                                 |
| Task                 | Internal task                                  | TaskId; TenantId; RequestId nullable; CreatorId; Title; Description; Priority; Deadline; Status; WorkflowVersionId; SLAVersionId; CompletedAt; CreatedAt; UpdatedAt; DeletedAt                                                                 |
| TaskAssignment       | Task responsibility history                    | TaskAssignmentId; TaskId; DepartmentId nullable; UserId nullable; AssignedBy; AssignedAt; AcceptedAt; RejectedAt; RejectionReason; EndedAt                                                                                                     |
| TaskChecklistItem    | Checklist line                                 | ChecklistItemId; TaskId; Title; SortOrder; IsCompleted; CompletedBy; CompletedAt                                                                                                                                                               |
| TaskProgressReport   | Progress report                                | ProgressReportId; TaskId; AuthorId; Percent; Content; SubmittedAt                                                                                                                                                                              |
| TaskResult           | Submitted final result                         | TaskResultId; TaskId; AuthorId; Content; RevisionNo; SubmittedAt                                                                                                                                                                               |
| Request              | Internal request                               | RequestId; TenantId; RequesterId; ServiceId; CategoryId; ParentRequestId nullable; RevisedFromRequestId nullable; Title; Description; Priority; Status; WorkflowVersionId; SLAVersionId; ResolvedAt; ClosedAt; CreatedAt; UpdatedAt; DeletedAt |
| RequestRouting       | Request routing history                        | RequestRoutingId; RequestId; FromDepartmentId; ToDepartmentId; FromUserId; ToUserId; RoutedBy; RoutedAt; Reason; Source(AI/MANUAL/RULE)                                                                                                        |
| RequestResolution    | Resolution record                              | ResolutionId; RequestId; ResolverId; Content; RevisionNo; CreatedAt                                                                                                                                                                            |
| Comment              | Timeline comment                               | CommentId; TenantId; ObjectType; ObjectId; AuthorId; Content; CreatedAt; EditedAt; DeletedAt                                                                                                                                                   |
| Attachment           | Evidence/file metadata                         | AttachmentId; TenantId; ObjectType; ObjectId; UploadedBy; FileName; ContentType; SizeBytes; ObjectKey; Hash; Status; CreatedAt; DeletedAt                                                                                                      |
| Confirmation         | Milestone confirmation                         | ConfirmationId; TenantId; ObjectType; ObjectId; MilestoneType; ActorId; Decision; Note; ConfirmedAt                                                                                                                                            |
| Notification         | Notification event                             | NotificationId; TenantId; RecipientId; Type; ObjectType; ObjectId; Title; Content; ReadAt; SentAt; IdempotencyKey                                                                                                                              |
| AuditLog             | Immutable business/security audit              | AuditLogId; TenantId nullable; ActorType; ActorId; Action; ObjectType; ObjectId; BeforeJson; AfterJson; MetadataJson; CreatedAt                                                                                                                |
| AIInteraction        | AI call metadata                               | AIInteractionId; TenantId; UserId; Feature; ModelName; InputRef; OutputRef; Status; LatencyMs; TokenUsage; CreatedAt                                                                                                                           |
| AIRecommendation     | Structured AI recommendation                   | RecommendationId; AIInteractionId; ObjectType; ObjectId; RecommendationType; PayloadJson; Confidence; HumanDecision; DecisionBy; DecidedAt                                                                                                     |
| AIAgentAction        | AI tool execution audit                        | AIAgentActionId; AIInteractionId; ToolName; ArgsJson; AuthorizationResult; ExecutionStatus; ResultRef; CreatedAt                                                                                                                               |

# 13. Database Relationships

| **From**                           | **Cardinality** | **To**               | **Meaning**                                                                   |
|------------------------------------|-----------------|----------------------|-------------------------------------------------------------------------------|
| Company                            | 1:N             | Tenant               | A company may have one primary tenant in MVP; schema allows future expansion. |
| Tenant                             | 1:N             | User                 | A tenant owns its users.                                                      |
| Tenant                             | 1:N             | Department           | Tenant-scoped departments.                                                    |
| Department                         | 1:N             | User                 | Employee belongs to one department in MVP.                                    |
| Role                               | N:N             | Permission           | Via RolePermission.                                                           |
| User                               | N:N             | Role                 | Via UserRole.                                                                 |
| Tenant                             | 1:N             | Service              | Company-specific service catalog.                                             |
| Service                            | 1:N             | ServiceCategory      | Service categories.                                                           |
| Service                            | 1:N             | RoutingRule          | Routing behavior.                                                             |
| Workflow                           | 1:N             | WorkflowVersion      | Versioned definitions.                                                        |
| WorkflowVersion                    | 1:N             | WorkflowStep         | Ordered steps.                                                                |
| WorkflowVersion                    | 1:N             | WorkflowTransition   | State transitions.                                                            |
| ApprovalRule                       | 1:N             | ApprovalRuleVersion  | Versioned approval configuration.                                             |
| ApprovalInstance                   | 1:N             | ApprovalStepInstance | Runtime approval steps.                                                       |
| SLAProfile                         | 1:N             | SLAVersion           | Versioned SLA configuration.                                                  |
| Request                            | 1:N             | Task                 | A request may result in zero, one or many tasks.                              |
| Request                            | 1:N self        | Request              | Parent/child multi-intent chain.                                              |
| Request                            | 1:N self        | Request              | RevisedFromRequestId links a new request to a rejected prior request.         |
| Task                               | 1:N             | TaskAssignment       | Assignment history.                                                           |
| Task                               | 1:N             | TaskProgressReport   | Progress history.                                                             |
| Task                               | 1:N             | TaskResult           | Result revisions.                                                             |
| Task/Request                       | 1:N             | Comment              | Timeline collaboration.                                                       |
| Task/Request/Comment/Report/Result | 1:N             | Attachment           | Evidence.                                                                     |
| Task/Request                       | 1:N             | Confirmation         | Milestone confirmations.                                                      |
| Tenant/User                        | 1:N             | AuditLog             | Audit ownership and scope.                                                    |
| AIInteraction                      | 1:N             | AIRecommendation     | One AI call can yield one or multiple recommendations.                        |
| AIInteraction                      | 1:N             | AIAgentAction        | Tool calls generated during the interaction.                                  |

# 14. API Specification

All APIs use REST/JSON and are versioned under /api/v1. Authentication uses bearer access tokens. Tenant context is derived from the authenticated user/session and may be cross-checked against route/resource IDs. Mutating endpoints should support idempotency keys where duplicate submission is a realistic risk.

| **ID**       | **Method** | **Endpoint**                       | **Purpose**                             | **Auth**                            | **Module**   |
|--------------|------------|------------------------------------|-----------------------------------------|-------------------------------------|--------------|
| API-AUTH-01  | POST       | /api/v1/auth/login                 | Login by employee code or company email | Anonymous                           | Auth         |
| API-AUTH-02  | POST       | /api/v1/auth/refresh               | Refresh token                           | Authenticated token                 | Auth         |
| API-AUTH-03  | POST       | /api/v1/auth/forgot-password       | Request reset                           | Anonymous                           | Auth         |
| API-ORG-01   | GET/POST   | /api/v1/users                      | List/create users                       | Company Admin                       | Organization |
| API-ORG-02   | PUT        | /api/v1/users/{id}                 | Update user                             | Company Admin/authorized            | Organization |
| API-ORG-03   | POST       | /api/v1/users/{id}/deactivate      | Deactivate user                         | Company Admin                       | Organization |
| API-ORG-04   | GET/POST   | /api/v1/departments                | List/create departments                 | Company Admin                       | Organization |
| API-RBAC-01  | GET/PUT    | /api/v1/roles/{id}/permissions     | Read/update role permissions            | Company Admin                       | Security     |
| API-SVC-01   | GET/POST   | /api/v1/services                   | List/create services                    | Company Admin                       | Service      |
| API-SVC-02   | PUT        | /api/v1/services/{id}/routing      | Configure routing                       | Company Admin                       | Service      |
| API-WF-01    | POST       | /api/v1/workflows                  | Create workflow draft                   | Company Admin                       | Workflow     |
| API-WF-02    | POST       | /api/v1/workflows/{id}/publish     | Publish workflow version                | Company Admin                       | Workflow     |
| API-TASK-01  | GET/POST   | /api/v1/tasks                      | List/create tasks                       | Manager/Employee scoped             | Task         |
| API-TASK-02  | POST       | /api/v1/tasks/{id}/assign          | Assign task                             | Manager                             | Task         |
| API-TASK-03  | POST       | /api/v1/tasks/{id}/accept          | Accept/reject assignment                | Employee target                     | Task         |
| API-TASK-04  | POST       | /api/v1/tasks/{id}/progress        | Update progress                         | Employee/authorized                 | Task         |
| API-TASK-05  | POST       | /api/v1/tasks/{id}/result          | Submit final result                     | Employee/authorized                 | Task         |
| API-TASK-06  | POST       | /api/v1/tasks/{id}/confirmation    | Confirm result                          | Manager/reviewer                    | Task         |
| API-TASK-07  | POST       | /api/v1/tasks/{id}/reassign        | Reassign task                           | Manager                             | Task         |
| API-REQ-01   | GET/POST   | /api/v1/requests                   | List/create requests                    | Employee/Manager scoped             | Request      |
| API-REQ-02   | POST       | /api/v1/requests/{id}/route        | Route request                           | Manager/authorized                  | Request      |
| API-REQ-03   | POST       | /api/v1/requests/{id}/receive      | Receive request                         | Assigned user                       | Request      |
| API-REQ-04   | POST       | /api/v1/requests/{id}/tasks        | Create task from request                | Manager/authorized Employee/Manager | Request      |
| API-REQ-05   | POST       | /api/v1/requests/{id}/resolve      | Resolve request                         | Employee/Manager                    | Request      |
| API-REQ-06   | POST       | /api/v1/requests/{id}/confirmation | Confirm resolution                      | Requester                           | Request      |
| API-REQ-07   | POST       | /api/v1/requests/{id}/revise       | Create revised request                  | Requester/authorized                | Request      |
| API-SLA-01   | GET/POST   | /api/v1/sla-profiles               | View/create SLA                         | Company Admin                       | SLA          |
| API-SLA-02   | GET        | /api/v1/sla/{objectType}/{id}      | View SLA status                         | Authorized                          | SLA          |
| API-NOTIF-01 | GET/PATCH  | /api/v1/notifications              | List/read notifications                 | Authenticated                       | Notification |
| API-FILE-01  | POST       | /api/v1/attachments/upload-session | Create signed upload session            | Authorized                          | Storage      |
| API-FILE-02  | POST       | /api/v1/attachments/finalize       | Finalize upload metadata                | Authorized                          | Storage      |
| API-AI-01    | POST       | /api/v1/ai/task-assistance         | Task AI assistance                      | Manager                             | AI           |
| API-AI-02    | POST       | /api/v1/ai/request-routing         | Request AI routing                      | Employee/Manager                    | AI           |
| API-AI-03    | POST       | /api/v1/ai/actions/execute         | Execute authorized tool                 | AI Agent internal                   | AI           |
| API-REP-01   | GET        | /api/v1/dashboard/manager          | Manager dashboard                       | Manager                             | Reporting    |
| API-REP-02   | GET        | /api/v1/reports/company            | Company report                          | Company Admin                       | Reporting    |
| API-AUD-01   | GET        | /api/v1/audit-logs                 | Audit records                           | Platform/Company Admin scoped       | Audit        |

## 14.1 Critical API Contract Example – AI Task Assistance

| **Item**    | **Contract**                                                                                                                                                                     |
|-------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Request     | { instruction: string, serviceId?: uuid, contextTaskId?: uuid }                                                                                                                  |
| Context     | Current tenant, current user role/permissions, service catalog, allowed departments/users/workload summary.                                                                      |
| Response    | { title, description, suggestedDepartmentId?, suggestedAssigneeId?, priority?, deadline?, checklist\[\], workflowVersionId?, slaVersionId?, missingInformation\[\], confidence } |
| Validation  | All IDs must exist in current tenant; enum values must match configured catalog; deadline normalized to tenant timezone.                                                         |
| Persistence | Recommendation stored in AIRecommendation. Task is not created until user/policy authorizes persistence.                                                                         |
| Errors      | 422 schema/validation; 403 unauthorized; 409 stale context; 502 provider failure; 429 provider/rate limit.                                                                       |

## 14.2 Critical API Contract Example – Revised Request

| **Item**      | **Contract**                                                                                             |
|---------------|----------------------------------------------------------------------------------------------------------|
| Request       | POST /api/v1/requests/{rejectedRequestId}/revise with editable request fields + selected attachment IDs. |
| Preconditions | Source request belongs to current tenant, status = REJECTED, requester/actor has permission.             |
| Processing    | Create new Request with status DRAFT, RevisedFromRequestId = source ID, preserve source audit linkage.   |
| Attachments   | Only explicitly selected attachments are reused; new attachment association is created.                  |
| Response      | 201 Created with new RequestId and source reference.                                                     |

# 15. Frontend Specification

| **Screen ID** | **Screen**          | **Actor**                  | **Purpose**                                         | **Main components/actions**                                   |
|---------------|---------------------|----------------------------|-----------------------------------------------------|---------------------------------------------------------------|
| UI-01         | Login               | Employee/Manager/Admin     | Employee code or company email + password           | Login mode, form validation, errors, tenant context           |
| UI-02         | Platform Dashboard  | Platform Admin             | Company/tenant status and activity                  | Tables, filters, status actions                               |
| UI-03         | Company Setup       | Company Admin              | Users/departments/roles/services/workflows/SLA      | Tabs, forms, publish/version actions                          |
| UI-04         | Task List           | Employee/Manager           | Search/filter tasks                                 | Table, filters, status chips, pagination                      |
| UI-05         | Task Detail         | Employee/Manager           | Execute/review task                                 | Timeline, checklist, progress, evidence, result, confirmation |
| UI-06         | AI Task Assistant   | Manager                    | Natural-language task drafting                      | Prompt input, suggestion cards, accept/override               |
| UI-07         | Request List        | Employee/Manager           | Search/filter requests                              | Table, filters, SLA indicators                                |
| UI-08         | Request Create      | Employee                   | Submit internal request                             | Service selector, text form, attachment uploader              |
| UI-09         | AI Request Analysis | Employee/Manager           | Review classification/splitting/routing suggestions | Intent cards, confidence, missing-info prompts                |
| UI-10         | Request Detail      | Requester/Employee/Manager | Process request                                     | Timeline, routing, task links, resolution, confirmation       |
| UI-11         | Workflow Designer   | Company Admin              | Define steps/transitions                            | Step editor, transition editor, publish/version               |
| UI-12         | Approval Center     | Manager/Approver           | Review approvals                                    | Queue, detail, approve/reject                                 |
| UI-13         | SLA Monitor         | Manager/Admin              | See overdue/at-risk items                           | Metrics, filters, detail                                      |
| UI-14         | Notifications       | All                        | Read/respond to events                              | Notification list, unread count                               |
| UI-15         | Audit Viewer        | Platform/Company Admin     | Trace changes                                       | Filterable audit table/detail                                 |
| UI-16         | Reports             | Manager/Admin              | View reports/export                                 | Charts/tables/date ranges                                     |

## 15.1 Form Standards

- All validation is duplicated on frontend for UX and backend for security/business correctness.

- Every destructive or irreversible action requires confirmation and reason where defined by business rules.

- Task/Request forms support attachments with visible size/type validation before upload.

- AI suggestions appear as editable proposals with source/context indicators; user must be able to override them.

- List screens must preserve tenant and permission scope and provide stable pagination.

# 16. AI Component Specification

## 16.1 AI Agent Goal

The AI Agent assists users in converting natural-language work/request information into structured, validated business operations, while preserving human control and system security.

## 16.2 AI Context

| **Context category** | **Allowed context**                                                                      |
|----------------------|------------------------------------------------------------------------------------------|
| Identity             | Current user ID, roles, permissions, department, tenant ID                               |
| Organization         | Departments, active employees, responsibilities, workload summaries within allowed scope |
| Configuration        | Services, categories, routing rules, workflow versions, approval rules, SLA profiles     |
| Business object      | Task/Request content and history only if current user is authorized to access it         |
| History              | Relevant comments/progress/audit summaries when required and authorized                  |

## 16.3 AI Input/Output Rules

- Input must be minimized to business data necessary for the requested AI feature.

- Structured Outputs / JSON Schema should be used for business decisions and tool arguments.

- Model-generated IDs, enums, dates and permissions are never trusted without server-side validation.

- AI may propose an option that does not exist only as natural-language fallback; it must not cause persistence until mapped to a valid system entity.

- Confidence is advisory. Low confidence triggers manual review or safe fallback.

- AI interaction metadata is stored without storing unnecessary sensitive prompt data when not required.

## 16.4 AI Guardrails

| **Guardrail**         | **Implementation rule**                                                                                         |
|-----------------------|-----------------------------------------------------------------------------------------------------------------|
| Tenant isolation      | All AI retrieval/tool context carries current TenantId; cross-tenant context is forbidden.                      |
| Permission            | Every action uses the current user’s effective permission, not the model’s inferred authority.                  |
| Tool whitelist        | Agent can call only registered tools with JSON schema and permission metadata.                                  |
| Business validation   | Tool handler revalidates state, resource scope, workflow, SLA and approval conditions.                          |
| Human approval        | Assignment/routing/priority/deadline mutations require user confirmation by default.                            |
| Hallucination control | Structured outputs + entity lookup + allowlisted enums + fallback/manual review.                                |
| Prompt injection      | Treat retrieved business text as untrusted content; system instructions and tool policy remain higher priority. |
| Action logging        | Every AI tool call stores authorization result and execution status.                                            |
| Rate/cost control     | Central token/call budget, timeouts, retries with exponential backoff, and per-tenant limits.                   |

# 17. System Architecture

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Architecture decision<br />
</strong>Use a Modular Monolith for the student project. Do not split Task, Request, Workflow, AI, Notification and SLA into separate microservices. Internal module boundaries and contracts must still be explicit so the architecture can evolve later.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 17.1 Logical Architecture

| **Layer**      | **Responsibilities**                                                      | **Technology**                                               |
|----------------|---------------------------------------------------------------------------|--------------------------------------------------------------|
| Presentation   | SPA, routing, forms, tables, dashboards, AI suggestion UX                 | Angular 22, TypeScript, Angular Material, Tailwind CSS       |
| API            | REST endpoints, authentication, DTOs, validation                          | ASP.NET Core 10 Web API                                      |
| Application    | Use-case orchestration, commands/queries, transaction boundaries          | C# application services                                      |
| Domain         | Entities, value objects, state rules, workflow rules, permission policies | C# domain layer                                              |
| Infrastructure | EF Core, storage, external AI, email, cache, job scheduler                | EF Core 10, Npgsql 10, MinIO/S3, Redis, Hangfire, OpenAI SDK |
| Data           | Transactional persistence and metadata                                    | PostgreSQL 18                                                |
| Observability  | Logs, traces, metrics, audit                                              | Serilog, OpenTelemetry                                       |

## 17.2 AI Request Flow

27. Angular calls an authenticated /api/v1/ai endpoint.

28. Backend resolves current tenant/user/permissions.

29. AI Agent retrieves only permitted business context.

30. OpenAI Responses API receives minimal context and structured-output/tool definitions.

31. AI output is schema-validated.

32. Referenced tenant entities and business rules are server-validated.

33. If recommendation-only, return to UI for human review.

34. If tool action is allowed, execute through the application service, not direct DB access.

35. Persist AI interaction/recommendation/action audit records.

## 17.3 Large File Upload Flow

36. Frontend requests an upload session from backend.

37. Backend checks object permission, size/type policy and creates a signed/multipart upload plan.

38. Frontend uploads directly to MinIO/S3-compatible storage.

39. Frontend finalizes upload with object key/hash/size.

40. Backend verifies metadata and associates Attachment to the Task/Request/Comment/Result.

41. Audit and optional notification are recorded.

# 18. Technology Stack

| **Layer**       | **Technology**                           | **Purpose**                                      | **Decision note**                                           |
|-----------------|------------------------------------------|--------------------------------------------------|-------------------------------------------------------------|
| Frontend        | Angular 22                               | SPA framework; routing/forms/components          | Active support as of Sep 2026                               |
| Language        | TypeScript                               | Frontend typing and tooling                      | Stable choice for Angular                                   |
| UI              | Angular Material + Tailwind CSS          | Enterprise forms/tables + custom layout          | Reduce custom UI effort                                     |
| Backend         | ASP.NET Core 10                          | REST API/business logic                          | LTS baseline                                                |
| Language        | C#                                       | Backend/domain implementation                    | Matches .NET ecosystem                                      |
| ORM             | Entity Framework Core 10                 | Persistence/migrations                           | Aligned with .NET 10                                        |
| DB driver       | Npgsql 10                                | PostgreSQL provider                              | Aligned with EF Core/PostgreSQL                             |
| Database        | PostgreSQL 18                            | Primary transaction store                        | Relational constraints + JSONB + strong transactional model |
| Auth            | ASP.NET Core Identity + JWT              | Authentication/session                           | Built-in .NET security ecosystem                            |
| Authorization   | Policy-based + resource/tenant scope     | RBAC and fine-grained access                     | Required for multi-tenant isolation                         |
| Realtime        | SignalR                                  | Task/request/notification updates                | Native ASP.NET integration                                  |
| Background jobs | Hangfire                                 | SLA checks, reminders, escalation                | Recurring/delayed jobs                                      |
| Cache           | Redis 8.x                                | Cache, ephemeral context, coordination as needed | Not source of truth                                         |
| File storage    | MinIO / S3-compatible                    | Attachment/evidence objects                      | Avoid DB BLOBs                                              |
| AI              | OpenAI Responses API + official .NET SDK | AI Agent reasoning, structured output, tools     | Model name configurable via environment                     |
| AI output       | Structured Outputs / JSON Schema         | Reliable application-facing structures           | Server-side validation still required                       |
| Logging         | Serilog                                  | Structured logs                                  | Tenant/user/request correlation                             |
| Observability   | OpenTelemetry                            | Traces/metrics                                   | Optional but recommended baseline                           |
| Container       | Docker + Docker Compose                  | Local/dev/demo environment                       | Reproducible setup                                          |
| CI/CD           | GitHub Actions                           | Build/test/deploy automation                     | Repository-native                                           |
| Testing         | xUnit + integration tests + Playwright   | Unit/API/E2E coverage                            | Focus on critical flows                                     |
| API docs        | OpenAPI/Swagger                          | API contract and manual testing                  | Generated from API definitions                              |

Current-version verification note: .NET 10 is an LTS release supported through 14 Nov 2028; Angular 22 is actively supported in the Sep 2026 release schedule. OpenAI’s official documentation currently supports the official .NET client and Responses API, including Structured Outputs and tool/function calling. These version references should be pinned to exact package versions in the repository at implementation time.

# 19. Security Specification

| **Control**       | **Requirement**                                                                                               |
|-------------------|---------------------------------------------------------------------------------------------------------------|
| Authentication    | Employee code or company email + password; salted password hashing via Identity; access/refresh token policy. |
| Authorization     | RBAC + policy/resource checks + tenant scope.                                                                 |
| Tenant isolation  | Tenant filter in application layer; optional DB RLS defense-in-depth.                                         |
| Input security    | DTO validation, parameterized queries, output encoding, file allowlist.                                       |
| File security     | Signed uploads, size/type checks, storage path isolation, optional malware scanning hook.                     |
| Secret management | Environment variables/secret store; never hardcode API keys.                                                  |
| AI security       | Prompt injection handling, data minimization, tool whitelist, action authorization, output validation.        |
| Rate limiting     | Authentication endpoints and AI endpoints rate limited; per-user/tenant quotas.                               |
| Audit             | Security and lifecycle events append-only.                                                                    |
| Privacy           | Only necessary data is sent to external AI provider; company data handling policy must be documented.         |
| Transport         | HTTPS in non-local environments; secure cookies if cookie auth is later introduced.                           |

# 20. Logging, Monitoring and Error Handling

## 20.1 Correlation

- Every API request has CorrelationId/TraceId.

- Every business operation logs TenantId, UserId, ObjectId when safe.

- AI calls log feature, model name, latency, token usage and result status; raw sensitive prompt content is not logged by default.

- AuditLog is separate from technical logs and remains queryable by authorized administrators.

## 20.2 Standard Error Envelope

| **Field** | **Meaning**                                                 |
|-----------|-------------------------------------------------------------|
| code      | Stable application error code, e.g. TASK.INVALID_STATE.     |
| message   | Safe human-readable message.                                |
| details   | Field-level validation or contextual information when safe. |
| traceId   | Correlation value for support/debugging.                    |
| timestamp | Server UTC timestamp.                                       |

## 20.3 Error Categories

| **Category**        | **HTTP** | **Examples**                                   | **Response**                                    |
|---------------------|----------|------------------------------------------------|-------------------------------------------------|
| Validation          | 400/422  | Missing title; invalid deadline; bad file type | No state mutation                               |
| Authentication      | 401      | Invalid token/password                         | Generic security-safe message                   |
| Authorization       | 403      | Manager outside scope; AI tool denied          | Audit security event if suspicious              |
| Not found           | 404      | Resource does not exist in scope               | Do not reveal cross-tenant existence            |
| Conflict            | 409      | Stale workflow version; duplicate submission   | Return current state/version                    |
| Rate limit          | 429      | AI/provider/user quota                         | Retry guidance                                  |
| External dependency | 502/503  | LLM/storage/email unavailable                  | Safe fallback + retry/queue as applicable       |
| Internal            | 500      | Unexpected exception                           | Generic message + trace ID; server logs details |

# 21. Non-Functional Requirements

| **ID**        | **Category**       | **Requirement**                                                                                                                                      |
|---------------|--------------------|------------------------------------------------------------------------------------------------------------------------------------------------------|
| NFR-PERF-001  | API responsiveness | \[PROPOSED\] 95% of standard read/write API calls should complete within 2 seconds under demo-scale load, excluding external AI/file upload latency. |
| NFR-PERF-002  | AI latency         | \[PROPOSED\] Show processing state for AI calls; define timeout and fallback rather than blocking indefinitely.                                      |
| NFR-SCAL-001  | Tenant growth      | \[PROPOSED\] Schema and services should support more tenants without tenant-specific code branches.                                                  |
| NFR-SEC-001   | Tenant isolation   | Mandatory; no cross-tenant data access.                                                                                                              |
| NFR-SEC-002   | Least privilege    | Mandatory; every sensitive action requires explicit permission.                                                                                      |
| NFR-REL-001   | Idempotency        | Required for finalization, confirmations, scheduler-triggered notifications and AI actions where duplicate execution is possible.                    |
| NFR-MAINT-001 | Modularity         | Modules must have clear application/domain boundaries even in monolith.                                                                              |
| NFR-OBS-001   | Observability      | Structured logs with correlation IDs required; traces/metrics recommended.                                                                           |
| NFR-USAB-001  | Usability          | Core task/request flows should be understandable without training documentation.                                                                     |
| NFR-DATA-001  | Auditability       | Critical lifecycle/security events are queryable and immutable.                                                                                      |
| NFR-FILE-001  | Attachment         | Support files up to 500 MB using direct object storage upload.                                                                                       |

# 22. Notification System

| **Event**         | **Receiver**                 | **Channel**    | **Trigger**                              |
|-------------------|------------------------------|----------------|------------------------------------------|
| TaskAssigned      | Assigned employee/department | In-app + email | Task assigned                            |
| TaskAccepted      | Manager                      | In-app         | Employee accepted                        |
| TaskRejected      | Manager                      | In-app         | Employee rejected assignment with reason |
| ProgressSubmitted | Manager                      | In-app         | Progress report submitted                |
| ResultSubmitted   | Manager                      | In-app + email | Final result awaiting review             |
| TaskConfirmed     | Employee                     | In-app         | Result confirmed/reworked                |
| RequestSubmitted  | Assigned routing audience    | In-app         | Request submitted                        |
| RequestRouted     | Target department/person     | In-app         | Routing completed                        |
| RequestReceived   | Requester                    | In-app         | Request accepted                         |
| RequestResolved   | Requester                    | In-app + email | Resolution awaiting confirmation         |
| RequestRejected   | Requester                    | In-app + email | Request rejected with reason             |
| RequestRevised    | Relevant Employee/Manager    | In-app         | New revised request submitted            |
| DeadlineWarning   | Owner/Manager                | In-app + email | Near deadline                            |
| Overdue           | Owner/Manager                | In-app + email | Deadline/SLA exceeded                    |
| Escalation        | Escalation target            | In-app + email | Escalation rule fired                    |
| ApprovalRequired  | Approver                     | In-app + email | Approval pending                         |

# 23. External Services and Dependencies

| **Service**    | **Purpose**                                      | **Auth**                         | **Data sent**                 | **Data received**              | **Failure handling**                                     |
|----------------|--------------------------------------------------|----------------------------------|-------------------------------|--------------------------------|----------------------------------------------------------|
| OpenAI API     | AI inference / structured outputs / tool calling | API key                          | Minimal approved context      | Structured response/tool calls | Timeout/retry/fallback to manual flow                    |
| MinIO/S3       | Object storage                                   | Access credentials / signed URLs | File binary via direct upload | Object metadata                | Retry/multipart/resume; orphan cleanup job               |
| Email provider | Password reset, notifications                    | SMTP/API credentials             | Recipient + message           | Delivery result                | Queue/retry; in-app notification remains source of truth |
| Redis          | Cache/temporary coordination                     | Connection string                | Cache keys, transient data    | Cached value                   | Fallback to DB where safe                                |

# 24. Dependency Map

| **Feature**           | **Depends on**                                  | **Can block implementation?**                   |
|-----------------------|-------------------------------------------------|-------------------------------------------------|
| Authentication        | Identity, Tenant                                | Yes                                             |
| Organization          | Authentication, Tenant                          | Yes                                             |
| Service Configuration | Organization                                    | Yes                                             |
| Task                  | Authentication, Organization, Service, Workflow | Yes                                             |
| Request               | Authentication, Organization, Service           | Yes                                             |
| Request → Task        | Request + Task                                  | Yes for full workflow                           |
| Approval              | Workflow + Organization                         | Yes for approval services                       |
| SLA                   | Task/Request + Service + Workflow               | Yes for SLA features                            |
| Notification          | Core entities + event model                     | No for early CRUD; yes for full UX              |
| Attachments           | Auth + object access                            | No for initial CRUD; required for evidence demo |
| AI Task               | Task + Organization + Service                   | Can be implemented after core Task APIs         |
| AI Request            | Request + Service + Organization                | Can be implemented after Request APIs           |
| Dashboard             | Task/Request/SLA                                | After transactional model stabilizes            |
| Audit                 | All modules                                     | Cross-cutting; design before implementation     |

# 25. Implementation Priority

| **Priority** | **Meaning** | **Scope**                                                                                                                                                                      |
|--------------|-------------|--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| P0           | Must have   | Auth, tenant isolation, organization, service catalog, task core lifecycle, request core lifecycle, request-to-task, workflow basics, audit, attachments, basic notifications. |
| P1           | Important   | Approval, SLA, escalation, dashboard/reporting, SignalR realtime, AI task assistance, AI request routing/splitting, revised request flow.                                      |
| P2           | Enhancement | Advanced natural-language search, richer AI analytics, long-term memory/RAG, advanced calendars, advanced export/BI.                                                           |

# 26. Requirement Traceability Matrix

| **Business Rule** | **Functional Requirements**  | **Module**                   | **UI**                       | **API**                     | **Test**     |
|-------------------|------------------------------|------------------------------|------------------------------|-----------------------------|--------------|
| BR-001            | FR-TEN-004                   | M02 + cross-cutting security | TenantId/filter/policy       | Auth/API/data access        | TST-SEC-001  |
| BR-005            | FR-AI-002/003/009            | M12                          | AI recommendation UI         | /api/v1/ai/\*               | TST-AI-003   |
| BR-006            | FR-AI-010                    | M12 + all target modules     | Tool approval/authorization  | /api/v1/ai/actions/execute  | TST-AI-006   |
| BR-008            | FR-REQ-009                   | M06                          | Revised request screen       | /requests/{id}/revise       | TST-REQ-008  |
| BR-010            | FR-SLA-001/002               | M08                          | SLA config/monitor           | /sla/\*                     | TST-SLA-002  |
| BR-012            | FR-TASK-003/008 + FR-REQ-008 | M05/M06                      | Confirmation UI              | /confirmation endpoints     | TST-WF-004   |
| BR-014            | FR-COL-002                   | M10                          | Attachment uploader          | upload-session/finalize     | TST-FILE-001 |
| BR-016            | FR-TASK-007/008              | M05                          | Result/review UI             | result/confirmation API     | TST-TASK-007 |
| BR-017            | FR-REQ-007/008               | M06                          | Resolution/confirmation UI   | resolve/confirmation API    | TST-REQ-007  |
| BR-022            | FR-REQ-002 + FR-SLA-004      | M06/M08/M12                  | Missing-information workflow | AI + request transition API | TST-AI-009   |

# 27. Testing Requirements

## 27.1 Test Levels

- Unit tests: domain state transitions, SLA calculations, permission policies, request revision logic, AI schema validation.

- Integration tests: API + database; tenant isolation; workflow/approval; file metadata finalization; background jobs.

- AI tests: deterministic fixtures for classification/routing/splitting; schema validity; low-confidence fallback; tool authorization; prompt-injection resistance fixtures.

- E2E tests: full manager task flow and employee request flow using Playwright.

| **Test ID**  | **Scenario**         | **Setup/Action**                            | **Expected Result**                                            |
|--------------|----------------------|---------------------------------------------|----------------------------------------------------------------|
| TST-SEC-001  | Tenant isolation     | User from Tenant A requests Tenant B object | 403/404 and no data leak                                       |
| TST-AUTH-001 | Dual login           | Valid employee code/password                | Authenticated in correct tenant                                |
| TST-TASK-001 | Assign/accept        | Manager assigns, employee accepts           | ASSIGNED → ACCEPTED                                            |
| TST-TASK-002 | Reject assignment    | Employee rejects with reason                | Task REJECTED + audit                                          |
| TST-TASK-003 | Progress/result      | Employee updates and submits                | Progress history + SUBMITTED                                   |
| TST-TASK-004 | Confirmation         | Manager confirms result                     | CONFIRMED → COMPLETED                                          |
| TST-REQ-001  | Create/route/receive | Employee request processed                  | SUBMITTED → ROUTED → RECEIVED                                  |
| TST-REQ-002  | Request to task      | Assigned Employee/Manager creates task      | Task linked to Request                                         |
| TST-REQ-003  | Resolve/confirm      | Requester accepts resolution                | RESOLVED → CONFIRMED → CLOSED                                  |
| TST-REQ-008  | Rejected revision    | Rejected request reused                     | Original REJECTED; new request DRAFT with RevisedFromRequestId |
| TST-WF-001   | Invalid transition   | Actor attempts forbidden transition         | Rejected with 422/403                                          |
| TST-WF-004   | Approval             | Approver rejects                            | Workflow follows rejection path                                |
| TST-SLA-001  | Warning              | Job detects warning threshold               | One warning notification                                       |
| TST-SLA-002  | Overdue              | Job detects breach                          | OVERDUE + escalation once                                      |
| TST-FILE-001 | 500 MB file          | Upload 500 MB accepted                      | Attachment finalized                                           |
| TST-FILE-002 | \>500 MB             | Upload 500.01 MB                            | Rejected before finalization                                   |
| TST-AI-003   | Task recommendation  | Prompt produces assignment suggestion       | Schema-valid recommendation with confidence                    |
| TST-AI-006   | Tool authorization   | AI attempts unauthorized action             | Tool denied + audit                                            |
| TST-AI-008   | Multi-intent         | 4 issue paragraph                           | 4 child drafts linked to parent                                |
| TST-AI-009   | Low confidence       | Ambiguous request                           | Manual routing fallback                                        |

# 28. Implementation Contract for AI Coding

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Rule<br />
</strong>If a coding agent encounters an ambiguity that changes business behavior, authorization, database relationships or API contracts, it must STOP AND ASK rather than silently inventing a new rule. Minor UI/layout choices may be decided locally when they do not change behavior.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

## 28.1 Fixed Technical Contract

- Frontend: Angular 22 + TypeScript; Angular Material + Tailwind CSS.

- Backend: ASP.NET Core 10 Web API + C#.

- ORM: EF Core 10 + Npgsql 10.

- Database: PostgreSQL 18.

- Auth: ASP.NET Core Identity + JWT.

- Authorization: RBAC + policy/resource + tenant scope.

- Realtime: SignalR.

- Background jobs: Hangfire.

- Cache: Redis; never the source of transactional truth.

- Object storage: MinIO/S3-compatible, direct upload for large files.

- AI: OpenAI Responses API via official .NET SDK; model name configurable by environment variable.

- AI output: Structured Outputs / JSON Schema; tool/function calling for controlled actions.

- Logging: Serilog; Observability: OpenTelemetry recommended.

- Containers: Docker Compose for local/demo; no microservices or Kubernetes required.

## 28.2 Implementation Rules

42. Do not add new domain modules outside the locked scope without explicit change control.

43. Do not use “Manage X” as the canonical Use Case name; use action-specific names in requirements/UML.

44. Do not bypass application services with controller-to-DB shortcuts for business mutations.

45. Do not let the AI model write SQL or directly access the database.

46. Do not trust AI-produced entity IDs, permissions, status transitions or action arguments without server validation.

47. Every business mutation must be tenant-scoped and authorization-checked.

48. Published workflow/SLA/approval versions are immutable for active transactions.

49. Use UTC in persistence; convert to tenant/user timezone at presentation boundaries.

50. Use soft deletion where specified; never hard-delete audit logs.

51. Use transactions for lifecycle changes that update multiple records together.

52. Use optimistic concurrency/version fields for critical mutable aggregates where needed.

53. Write automated tests before changing core state/permission rules.

## 28.3 Suggested Repository Structure

/repo  
/frontend  
/src/app  
/core  
/shared  
/features/auth  
/features/platform  
/features/organization  
/features/task  
/features/request  
/features/workflow  
/features/sla  
/features/reports  
/features/ai  
/backend  
/BizFlow.Api  
/BizFlow.Application  
/BizFlow.Domain  
/BizFlow.Infrastructure  
/tests  
/BizFlow.UnitTests  
/BizFlow.IntegrationTests  
/BizFlow.E2ETests  
/docs  
/docker-compose.yml

# 29. High-Risk Implementation Areas for AI Coding

| **Area**                    | **Risk**                                                           | **Implementation rule**                                             |
|-----------------------------|--------------------------------------------------------------------|---------------------------------------------------------------------|
| AI tool execution           | Agent may bypass authorization if tools call repositories directly | All tools call application services; central authorization + audit. |
| Workflow transitions        | AI may set arbitrary status                                        | Only workflow engine owns status mutation.                          |
| Tenant filtering            | Generic repository may forget TenantId                             | Tenant-aware data access abstraction + integration tests.           |
| Request revision            | Coder may mutate rejected request instead of creating new one      | Original record immutable; explicit RevisedFromRequestId.           |
| Large files                 | API may proxy 500 MB binary and fail/time out                      | Signed direct upload + multipart.                                   |
| SLA timing                  | Coder may use local server time or wall-clock minutes only         | UTC persistence + tenant calendar service + tested pause/resume.    |
| Approval                    | Coder may overwrite decisions                                      | Append-only approval actions + workflow state engine.               |
| AI hallucinated routing IDs | Model may invent department/user IDs                               | Validate every ID against current tenant catalog.                   |
| Audit completeness          | AI/coder may log only UI actions                                   | Audit at application-service mutation boundary.                     |
| Concurrency                 | Two actors may update same Task/Request simultaneously             | Optimistic concurrency token + conflict responses.                  |

# 30. Requirement Gap Analysis – After Decisions

| **ID**  | **Gap**                                | **Status**           | **Resolution**                                                                                            |
|---------|----------------------------------------|----------------------|-----------------------------------------------------------------------------------------------------------|
| GAP-001 | Company registration → tenant creation | Resolved             | A chosen: registration → Platform approval → tenant creation.                                             |
| GAP-002 | Role model                             | Resolved             | Baseline: system roles + custom tenant roles subject to permission catalog.                               |
| GAP-003 | Manager scope                          | Resolved             | Manager scope is tenant/configuration controlled; only allowed targets are assignable.                    |
| GAP-004 | Department task assignment             | Resolved             | Department queue may be target; AI may recommend user; final assignment follows authority policy.         |
| GAP-005 | Request handler                        | Resolved             | Assigned Employee/Manager processes request; no extra actor added.                                        |
| GAP-006 | Request → Task                         | Resolved             | Explicit RequestId linkage; one-to-many supported.                                                        |
| GAP-007 | Workflow model                         | Resolved             | Versioned steps/transitions with immutable published versions.                                            |
| GAP-008 | Approval model                         | Resolved             | Sequential or parallel configuration supported by rule version.                                           |
| GAP-009 | SLA calendar                           | Resolved             | Tenant business calendar baseline; versioned SLA.                                                         |
| GAP-010 | Task states                            | Resolved             | Confirmed main + out-of-band states.                                                                      |
| GAP-011 | Request states                         | Resolved             | AI_ANALYZED removed; lifecycle defined.                                                                   |
| GAP-012 | Notifications                          | Resolved             | In-app + email baseline.                                                                                  |
| GAP-013 | File policy                            | Partially resolved   | Max 500 MB confirmed; type allowlist baseline; advanced scanning can be P2.                               |
| GAP-014 | Search/archive                         | Resolved             | Core search/filter + archive included.                                                                    |
| GAP-015 | Delete policy                          | Resolved             | Soft delete/deactivation; audit immutable.                                                                |
| GAP-016 | AI action model                        | Resolved             | Whitelisted tools + authorization + human confirmation by default.                                        |
| GAP-017 | Low-confidence fallback                | Resolved             | Manual routing/review fallback.                                                                           |
| GAP-018 | AI data privacy                        | Decision adopted     | External LLM allowed for project baseline with data minimization and secrets control.                     |
| GAP-019 | AI memory                              | Resolved             | No long-term memory/RAG in P0/P1; request/task context is retrieved per operation.                        |
| GAP-020 | AI model/provider                      | Decision adopted     | OpenAI provider; model configurable via environment; exact model is an implementation-time configuration. |
| GAP-021 | Authentication                         | Resolved             | Employee code or company email + password.                                                                |
| GAP-022 | Initial Company Admin                  | Resolved by design   | Created during tenant provisioning/invitation.                                                            |
| GAP-023 | Config versioning                      | Resolved             | Workflow/SLA/approval are versioned and immutable when active.                                            |
| GAP-024 | Deployment                             | Resolved             | Docker local/demo + optional cloud deployment.                                                            |
| GAP-025 | Concurrency                            | Decision adopted     | Optimistic concurrency for important aggregates.                                                          |
| GAP-026 | Reports                                | Resolved at baseline | Defined core workload/completion/overdue/SLA/company report metrics.                                      |

# 31. Consistency Check

| **Check ID** | **Check**                       | **Status** | **Result**                                                                         |
|--------------|---------------------------------|------------|------------------------------------------------------------------------------------|
| CHK-001      | Business requirements → modules | PASS       | All core business flows map to modules M02-M13.                                    |
| CHK-002      | Functions → database entities   | PASS       | Core functions have explicit persistence impact.                                   |
| CHK-003      | API → functional requirements   | PASS       | Core APIs map to FRs; unlisted CRUD endpoints follow same module conventions.      |
| CHK-004      | Frontend → API                  | PASS       | Core screens have corresponding contracts.                                         |
| CHK-005      | AI input/output                 | PASS       | AI features define context, schema validation, fallback and security.              |
| CHK-006      | Roles → permissions             | PASS       | Actor model and RBAC matrix are aligned.                                           |
| CHK-007      | State transitions               | PASS       | Task and Request lifecycle rules defined; AI_ANALYZED removed from Request states. |
| CHK-008      | Rejected Request semantics      | PASS       | Original immutable + revised new record.                                           |
| CHK-009      | 500 MB attachments              | PASS       | Architecture uses direct object storage upload rather than API proxying.           |
| CHK-010      | Multi-tenant isolation          | PASS       | TenantId + authorization specified across layers.                                  |
| CHK-011      | Scope control                   | PASS       | ERP/marketplace/external customer service excluded; P2 clearly separated.          |
| CHK-012      | AI action safety                | PASS       | Tool whitelist, auth, business validation and audit required.                      |

# 32. Final Completeness Scorecard

| **Category**            | **Status**        | **Comment**                                                                          |
|-------------------------|-------------------|--------------------------------------------------------------------------------------|
| Project overview        | Complete          | CONFIRMED + adopted decisions documented.                                            |
| Scope                   | Complete          | In/Out/P2 defined.                                                                   |
| Actors                  | Complete          | Five actors retained.                                                                |
| Roles/Permissions       | Complete          | RBAC + policy/tenant scope.                                                          |
| Modules                 | Complete          | 13 modules defined.                                                                  |
| Functional Requirements | Complete          | 62 canonical requirements listed below.                                              |
| Workflows               | Complete          | Task, Request, revision, AI flows.                                                   |
| State Machines          | Complete          | Task + Request.                                                                      |
| Business Rules          | Complete          | 22 core rules.                                                                       |
| Validation              | Complete          | Core validation matrix.                                                              |
| Database                | Complete baseline | 39 entities; exact field lengths/index choices can be refined during implementation. |
| API                     | Complete baseline | Versioned endpoint inventory + critical contracts.                                   |
| Frontend                | Complete baseline | Core screens + form standards.                                                       |
| AI                      | Complete baseline | 10 core AI requirements + guardrails.                                                |
| Security                | Complete baseline | Auth/AuthZ/Tenant/AI/File controls.                                                  |
| NFR                     | Complete baseline | Performance/scalability/security/observability targets.                              |
| Notification            | Complete baseline | Core events/channels.                                                                |
| Audit                   | Complete baseline | Audit model and coverage.                                                            |
| Testing                 | Complete baseline | Unit/integration/AI/E2E strategy.                                                    |
| Implementation Contract | Complete          | Fixed stack and coding rules.                                                        |
| Open Questions          | Non-blocking      | Only implementation-time choices such as exact package patch versions remain.        |

# 33. Open Questions / Needs Confirmation

No unresolved Critical or High business questions remain in this baseline because the team delegated the remaining design choices to the project lead and explicitly confirmed the key exceptions (Request revision, Task/Request states, dual login, 500 MB attachments). The following are implementation-time configuration choices, not blockers:

- Exact package patch versions to pin in package-lock/.csproj at repository initialization.

- Exact OpenAI model name to use in the deployment environment, chosen by cost/availability at implementation time; API contract does not depend on a specific model name.

- Cloud hosting provider/region if the team chooses cloud deployment; local Docker is the reference development environment.

- Exact email provider credentials/configuration.

- Final company business calendar values for demo data.

# Appendix A – Canonical Vocabulary

| **Term**          | **Canonical meaning**                                                                         |
|-------------------|-----------------------------------------------------------------------------------------------|
| Company           | The business organization registered on BizFlow.                                              |
| Tenant            | Isolated workspace/data boundary for a company.                                               |
| User              | Authenticated person within a tenant.                                                         |
| Department        | Organizational unit that may receive a work/request queue.                                    |
| Service           | Configurable internal business service offered inside a company.                              |
| Task              | Concrete unit of work to be performed.                                                        |
| Request           | Internal need/problem raised by an employee or authorized user.                               |
| Workflow          | Versioned set of processing steps and valid state transitions.                                |
| Approval Rule     | Rule defining who must approve and how decisions are combined.                                |
| SLA               | Configured processing target and warning/escalation policy.                                   |
| Evidence          | Uploaded file/image used to support a task/request/result.                                    |
| Confirmation      | Explicit milestone acknowledgement by a relevant party.                                       |
| Audit Log         | Immutable record of important system/business/security actions.                               |
| AI Recommendation | Model-generated proposal that is not final until validated/accepted by policy.                |
| AI Agent Action   | A tool call executed by the AI Agent through application services under authorization.        |
| Revised Request   | A new Request created from a previously rejected Request; the rejected original is preserved. |

# Appendix B – Recommended Main Demo Scenarios

| **Scenario** | **Title**                        | **Steps**                                                                                                                                   |
|--------------|----------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------|
| Demo A       | AI-assisted task assignment      | Manager natural language → AI suggestions → accept/override → assign → employee accept → progress/evidence → result → manager confirmation. |
| Demo B       | AI-assisted multi-intent request | Employee long request → AI detects 3-4 intents → child request proposals → routing → one child generates task → resolution → confirmation.  |
| Demo C       | Rejected request revision        | Request rejected with reason → requester reuses prior request → edit missing info → submit new request → original remains rejected.         |
| Demo D       | Multi-tenant isolation           | Company A and Company B each have own users/services/workflows; Company A user attempts Company B access and is blocked.                    |
| Demo E       | SLA escalation                   | Request receives SLA → warning → overdue → escalation with no duplicate events.                                                             |

<table>
<colgroup>
<col style="width: 100%" />
</colgroup>
<thead>
<tr class="header">
<th><strong>Implementation baseline<br />
</strong>This specification contains 62 canonical Functional Requirements and is intended to replace earlier informal feature lists. New requirements should be introduced through change control and then traced through module, API, database, UI, AI and tests before implementation.</th>
</tr>
</thead>
<tbody>
</tbody>
</table>

# Appendix C – Detailed Data Dictionary

The following dictionary is the canonical logical data contract for implementation. Physical column names may be adjusted only if the mapping remains one-to-one and is recorded in migration documentation. Header rows are configured to repeat when a dictionary table spans pages.

## Entity – Company

| **Field**    | **Type**     | **Required** | **Default** | **Unique** | **Purpose / Validation**    | **Notes**                           |
|--------------|--------------|--------------|-------------|------------|-----------------------------|-------------------------------------|
| CompanyId    | UUID         | Yes          | UUIDv7      | Yes        | Primary key                 | Server generated                    |
| Code         | VARCHAR(50)  | Yes          | \-          | Yes        | Stable company code         | Uppercase/tenant-independent unique |
| Name         | VARCHAR(200) | Yes          | \-          | No         | Registered/display name     | 1-200 chars                         |
| ContactEmail | VARCHAR(320) | Yes          | \-          | No         | Primary onboarding contact  | Valid email                         |
| Status       | ENUM         | Yes          | PENDING     | No         | Company lifecycle status    | PENDING/ACTIVE/SUSPENDED/INACTIVE   |
| CreatedAt    | TIMESTAMPTZ  | Yes          | UTC now     | No         | Creation timestamp          | UTC                                 |
| UpdatedAt    | TIMESTAMPTZ  | Yes          | UTC now     | No         | Last modification timestamp | UTC                                 |

## Entity – Tenant

| **Field** | **Type**     | **Required** | **Default**  | **Unique** | **Purpose / Validation**    | **Notes**                              |
|-----------|--------------|--------------|--------------|------------|-----------------------------|----------------------------------------|
| TenantId  | UUID         | Yes          | UUIDv7       | Yes        | Tenant/workspace identifier | Server generated                       |
| CompanyId | UUID FK      | Yes          | \-           | No         | Owning company              | Must reference Company                 |
| TenantKey | VARCHAR(80)  | Yes          | \-           | Yes        | Stable tenant key           | Lowercase slug format                  |
| Name      | VARCHAR(200) | Yes          | Company name | No         | Workspace name              | 1-200 chars                            |
| Status    | ENUM         | Yes          | ACTIVE       | No         | Tenant state                | PROVISIONING/ACTIVE/SUSPENDED/INACTIVE |
| TimeZone  | VARCHAR(64)  | Yes          | UTC          | No         | IANA timezone               | Valid IANA timezone                    |
| CreatedAt | TIMESTAMPTZ  | Yes          | UTC now      | No         | Creation time               | UTC                                    |
| UpdatedAt | TIMESTAMPTZ  | Yes          | UTC now      | No         | Last modification           | UTC                                    |

## Entity – User

| **Field**    | **Type**             | **Required** | **Default** | **Unique**        | **Purpose / Validation** | **Notes**                                  |
|--------------|----------------------|--------------|-------------|-------------------|--------------------------|--------------------------------------------|
| UserId       | UUID                 | Yes          | UUIDv7      | Yes               | User identifier          | Server generated                           |
| TenantId     | UUID FK              | Yes          | \-          | No                | Tenant boundary          | Same tenant on all business relations      |
| EmployeeCode | VARCHAR(50)          | Yes          | \-          | Yes within tenant | Employee login code      | Trimmed, unique per tenant                 |
| Email        | VARCHAR(320)         | Yes          | \-          | Yes within tenant | Company-provided email   | Valid email; unique per tenant             |
| PasswordHash | TEXT                 | Yes          | \-          | No                | Identity password hash   | Managed by ASP.NET Identity                |
| FullName     | VARCHAR(200)         | Yes          | \-          | No                | Display name             | 1-200 chars                                |
| DepartmentId | UUID FK nullable     | No           | NULL        | No                | Primary department       | Same tenant; nullable for admin edge cases |
| Status       | ENUM                 | Yes          | ACTIVE      | No                | Account status           | ACTIVE/INACTIVE/LOCKED                     |
| LastLoginAt  | TIMESTAMPTZ nullable | No           | NULL        | No                | Last successful login    | UTC                                        |
| CreatedAt    | TIMESTAMPTZ          | Yes          | UTC now     | No                | Creation                 | UTC                                        |
| UpdatedAt    | TIMESTAMPTZ          | Yes          | UTC now     | No                | Modification             | UTC                                        |
| DeletedAt    | TIMESTAMPTZ nullable | No           | NULL        | No                | Soft delete timestamp    | UTC                                        |

## Entity – Department

| **Field**          | **Type**         | **Required** | **Default** | **Unique**        | **Purpose / Validation** | **Notes**             |
|--------------------|------------------|--------------|-------------|-------------------|--------------------------|-----------------------|
| DepartmentId       | UUID             | Yes          | UUIDv7      | Yes               | Department identifier    | Server generated      |
| TenantId           | UUID FK          | Yes          | \-          | No                | Tenant boundary          | Same tenant           |
| Code               | VARCHAR(50)      | Yes          | \-          | Yes within tenant | Department code          | Unique per tenant     |
| Name               | VARCHAR(200)     | Yes          | \-          | No                | Department name          | 1-200 chars           |
| ParentDepartmentId | UUID FK nullable | No           | NULL        | No                | Hierarchy parent         | Cannot reference self |
| Status             | ENUM             | Yes          | ACTIVE      | No                | Department state         | ACTIVE/INACTIVE       |
| CreatedAt          | TIMESTAMPTZ      | Yes          | UTC now     | No                | Creation                 | UTC                   |
| UpdatedAt          | TIMESTAMPTZ      | Yes          | UTC now     | No                | Modification             | UTC                   |

## Entity – Role

| **Field** | **Type**         | **Required** | **Default** | **Unique** | **Purpose / Validation**                     | **Notes**                             |
|-----------|------------------|--------------|-------------|------------|----------------------------------------------|---------------------------------------|
| RoleId    | UUID             | Yes          | UUIDv7      | Yes        | Role identifier                              | Server generated                      |
| TenantId  | UUID FK nullable | No           | NULL        | No         | Tenant for custom role; NULL for system role | System roles only when IsSystem=true  |
| Name      | VARCHAR(100)     | Yes          | \-          | No         | Role name                                    | Unique within tenant for custom roles |
| IsSystem  | BOOLEAN          | Yes          | false       | No         | System role marker                           | System role cannot be deleted         |
| Status    | ENUM             | Yes          | ACTIVE      | No         | Role state                                   | ACTIVE/INACTIVE                       |
| CreatedAt | TIMESTAMPTZ      | Yes          | UTC now     | No         | Creation                                     | UTC                                   |
| UpdatedAt | TIMESTAMPTZ      | Yes          | UTC now     | No         | Modification                                 | UTC                                   |

## Entity – Permission

| **Field**    | **Type**     | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                                |
|--------------|--------------|--------------|-------------|------------|--------------------------|------------------------------------------|
| PermissionId | UUID         | Yes          | UUIDv7      | Yes        | Permission identifier    | Server generated                         |
| Code         | VARCHAR(120) | Yes          | \-          | Yes        | Atomic permission code   | module.action.scope pattern              |
| Module       | VARCHAR(80)  | Yes          | \-          | No         | Permission module        | Allowlist                                |
| Action       | VARCHAR(80)  | Yes          | \-          | No         | Concrete action          | Allowlist                                |
| ScopeType    | ENUM         | Yes          | TENANT      | No         | Resource scope           | PLATFORM/TENANT/DEPARTMENT/SELF/ASSIGNED |

## Entity – RolePermission

| **Field**    | **Type** | **Required** | **Default** | **Unique**   | **Purpose / Validation** | **Notes**           |
|--------------|----------|--------------|-------------|--------------|--------------------------|---------------------|
| RoleId       | UUID FK  | Yes          | \-          | Composite PK | Role relation            | Existing Role       |
| PermissionId | UUID FK  | Yes          | \-          | Composite PK | Permission relation      | Existing Permission |

## Entity – UserRole

| **Field** | **Type** | **Required** | **Default** | **Unique**   | **Purpose / Validation** | **Notes**     |
|-----------|----------|--------------|-------------|--------------|--------------------------|---------------|
| UserId    | UUID FK  | Yes          | \-          | Composite PK | User relation            | Existing User |
| RoleId    | UUID FK  | Yes          | \-          | Composite PK | Role relation            | Existing Role |

## Entity – Service

| **Field**               | **Type**         | **Required** | **Default** | **Unique**        | **Purpose / Validation**   | **Notes**                     |
|-------------------------|------------------|--------------|-------------|-------------------|----------------------------|-------------------------------|
| ServiceId               | UUID             | Yes          | UUIDv7      | Yes               | Internal service           | Server generated              |
| TenantId                | UUID FK          | Yes          | \-          | No                | Tenant boundary            | Same tenant                   |
| Code                    | VARCHAR(80)      | Yes          | \-          | Yes within tenant | Service code               | Unique per tenant             |
| Name                    | VARCHAR(200)     | Yes          | \-          | No                | Service name               | 1-200 chars                   |
| Description             | TEXT nullable    | No           | NULL        | No                | Service description        | Sanitized text                |
| Status                  | ENUM             | Yes          | ACTIVE      | No                | Service state              | DRAFT/ACTIVE/INACTIVE         |
| ActiveWorkflowVersionId | UUID FK nullable | No           | NULL        | No                | Published workflow version | Must belong to service/tenant |
| ActiveSLAVersionId      | UUID FK nullable | No           | NULL        | No                | Published SLA version      | Must belong to service/tenant |
| CreatedAt               | TIMESTAMPTZ      | Yes          | UTC now     | No                | Creation                   | UTC                           |
| UpdatedAt               | TIMESTAMPTZ      | Yes          | UTC now     | No                | Modification               | UTC                           |

## Entity – ServiceCategory

| **Field**         | **Type**     | **Required** | **Default** | **Unique**         | **Purpose / Validation** | **Notes**          |
|-------------------|--------------|--------------|-------------|--------------------|--------------------------|--------------------|
| ServiceCategoryId | UUID         | Yes          | UUIDv7      | Yes                | Category identifier      | Server generated   |
| ServiceId         | UUID FK      | Yes          | \-          | No                 | Owning service           | Same tenant        |
| Code              | VARCHAR(80)  | Yes          | \-          | Yes within service | Category code            | Unique per service |
| Name              | VARCHAR(200) | Yes          | \-          | No                 | Category name            | 1-200 chars        |
| Status            | ENUM         | Yes          | ACTIVE      | No                 | Category state           | ACTIVE/INACTIVE    |

## Entity – RoutingRule

| **Field**          | **Type**         | **Required** | **Default** | **Unique** | **Purpose / Validation**  | **Notes**        |
|--------------------|------------------|--------------|-------------|------------|---------------------------|------------------|
| RoutingRuleId      | UUID             | Yes          | UUIDv7      | Yes        | Routing rule identifier   | Server generated |
| ServiceId          | UUID FK          | Yes          | \-          | No         | Owning service            | Same tenant      |
| PriorityOrder      | INT              | Yes          | 1           | No         | Rule evaluation order     | Positive integer |
| ConditionsJson     | JSONB            | Yes          | {}          | No         | Rule predicates           | Schema validated |
| TargetDepartmentId | UUID FK nullable | No           | NULL        | No         | Target department         | Same tenant      |
| TargetRoleId       | UUID FK nullable | No           | NULL        | No         | Target role               | Same tenant      |
| Active             | BOOLEAN          | Yes          | true        | No         | Whether rule participates | Boolean          |

## Entity – Workflow

| **Field**    | **Type**     | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                  |
|--------------|--------------|--------------|-------------|------------|--------------------------|----------------------------|
| WorkflowId   | UUID         | Yes          | UUIDv7      | Yes        | Logical workflow         | Server generated           |
| TenantId     | UUID FK      | Yes          | \-          | No         | Tenant boundary          | Same tenant                |
| Name         | VARCHAR(200) | Yes          | \-          | No         | Workflow name            | 1-200 chars                |
| BusinessType | ENUM         | Yes          | REQUEST     | No         | TASK/REQUEST             | Configured business object |
| Status       | ENUM         | Yes          | DRAFT       | No         | Workflow logical status  | DRAFT/ACTIVE/INACTIVE      |
| CreatedAt    | TIMESTAMPTZ  | Yes          | UTC now     | No         | Creation                 | UTC                        |
| UpdatedAt    | TIMESTAMPTZ  | Yes          | UTC now     | No         | Modification             | UTC                        |

## Entity – WorkflowVersion

| **Field**         | **Type**             | **Required** | **Default** | **Unique**          | **Purpose / Validation**              | **Notes**               |
|-------------------|----------------------|--------------|-------------|---------------------|---------------------------------------|-------------------------|
| WorkflowVersionId | UUID                 | Yes          | UUIDv7      | Yes                 | Version identifier                    | Server generated        |
| WorkflowId        | UUID FK              | Yes          | \-          | No                  | Logical workflow                      | Existing Workflow       |
| VersionNo         | INT                  | Yes          | 1           | Unique per workflow | Version number                        | Positive integer        |
| Status            | ENUM                 | Yes          | DRAFT       | No                  | Version state                         | DRAFT/PUBLISHED/RETIRED |
| PublishedAt       | TIMESTAMPTZ nullable | No           | NULL        | No                  | Publish timestamp                     | Required when PUBLISHED |
| DefinitionJson    | JSONB                | Yes          | {}          | No                  | Optional complete definition snapshot | Schema validated        |

## Entity – WorkflowStep

| **Field**         | **Type**     | **Required** | **Default** | **Unique**         | **Purpose / Validation** | **Notes**                    |
|-------------------|--------------|--------------|-------------|--------------------|--------------------------|------------------------------|
| WorkflowStepId    | UUID         | Yes          | UUIDv7      | Yes                | Step identifier          | Server generated             |
| WorkflowVersionId | UUID FK      | Yes          | \-          | No                 | Workflow version         | Existing version             |
| StepCode          | VARCHAR(80)  | Yes          | \-          | Unique per version | Stable step code         | Regex/allowlist              |
| Name              | VARCHAR(200) | Yes          | \-          | No                 | Step label               | 1-200 chars                  |
| Type              | ENUM         | Yes          | ACTION      | No                 | Step type                | ACTION/APPROVAL/CONFIRMATION |
| OrderNo           | INT          | Yes          | 1           | Unique per version | Display/evaluation order | Positive integer             |
| ConfigJson        | JSONB        | Yes          | {}          | No                 | Step config              | Schema validated             |

## Entity – WorkflowTransition

| **Field**         | **Type**       | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                  |
|-------------------|----------------|--------------|-------------|------------|--------------------------|----------------------------|
| TransitionId      | UUID           | Yes          | UUIDv7      | Yes        | Transition identifier    | Server generated           |
| WorkflowVersionId | UUID FK        | Yes          | \-          | No         | Workflow version         | Existing version           |
| FromState         | VARCHAR(50)    | Yes          | \-          | No         | Current state            | Must match supported state |
| ToState           | VARCHAR(50)    | Yes          | \-          | No         | Target state             | Must be valid transition   |
| GuardJson         | JSONB nullable | No           | NULL        | No         | Transition condition     | Schema validated           |

## Entity – ApprovalRule

| **Field**      | **Type**     | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**             |
|----------------|--------------|--------------|-------------|------------|--------------------------|-----------------------|
| ApprovalRuleId | UUID         | Yes          | UUIDv7      | Yes        | Logical approval rule    | Server generated      |
| TenantId       | UUID FK      | Yes          | \-          | No         | Tenant boundary          | Same tenant           |
| Name           | VARCHAR(200) | Yes          | \-          | No         | Rule name                | 1-200 chars           |
| ObjectType     | ENUM         | Yes          | REQUEST     | No         | REQUEST/TASK             | Supported object type |
| Status         | ENUM         | Yes          | DRAFT       | No         | Rule status              | DRAFT/ACTIVE/INACTIVE |

## Entity – ApprovalRuleVersion

| **Field**             | **Type**             | **Required** | **Default** | **Unique**      | **Purpose / Validation** | **Notes**        |
|-----------------------|----------------------|--------------|-------------|-----------------|--------------------------|------------------|
| ApprovalRuleVersionId | UUID                 | Yes          | UUIDv7      | Yes             | Version identifier       | Server generated |
| ApprovalRuleId        | UUID FK              | Yes          | \-          | No              | Logical rule             | Existing rule    |
| VersionNo             | INT                  | Yes          | 1           | Unique per rule | Version number           | Positive integer |
| DefinitionJson        | JSONB                | Yes          | {}          | No              | Approval definition      | Schema validated |
| PublishedAt           | TIMESTAMPTZ nullable | No           | NULL        | No              | Publish time             | UTC              |

## Entity – ApprovalInstance

| **Field**          | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                    |
|--------------------|----------------------|--------------|-------------|------------|--------------------------|------------------------------|
| ApprovalInstanceId | UUID                 | Yes          | UUIDv7      | Yes        | Runtime approval         | Server generated             |
| TenantId           | UUID FK              | Yes          | \-          | No         | Tenant boundary          | Same tenant                  |
| ObjectType         | ENUM                 | Yes          | REQUEST     | No         | Request/task             | Supported object type        |
| ObjectId           | UUID                 | Yes          | \-          | No         | Approved object          | Must exist in same tenant    |
| RuleVersionId      | UUID FK              | Yes          | \-          | No         | Applied rule version     | Immutable snapshot reference |
| Status             | ENUM                 | Yes          | PENDING     | No         | Approval runtime state   | PENDING/APPROVED/REJECTED    |
| StartedAt          | TIMESTAMPTZ          | Yes          | UTC now     | No         | Start                    | UTC                          |
| CompletedAt        | TIMESTAMPTZ nullable | No           | NULL        | No         | Completion               | UTC                          |

## Entity – ApprovalStepInstance

| **Field**              | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**  | **Notes**                 |
|------------------------|----------------------|--------------|-------------|------------|---------------------------|---------------------------|
| ApprovalStepInstanceId | UUID                 | Yes          | UUIDv7      | Yes        | Approval step runtime     | Server generated          |
| ApprovalInstanceId     | UUID FK              | Yes          | \-          | No         | Parent approval instance  | Existing approval         |
| ApproverType           | ENUM                 | Yes          | USER        | No         | USER/ROLE/DEPARTMENT      | Allowlist                 |
| ApproverUserId         | UUID FK nullable     | No           | NULL        | No         | Specific approver         | Required for USER         |
| ApproverRoleId         | UUID FK nullable     | No           | NULL        | No         | Approver role             | Required for ROLE         |
| ApproverDepartmentId   | UUID FK nullable     | No           | NULL        | No         | Approver department       | Required for DEPARTMENT   |
| SequenceNo             | INT                  | Yes          | 1           | No         | Sequence                  | Positive integer          |
| Status                 | ENUM                 | Yes          | PENDING     | No         | Step state                | PENDING/APPROVED/REJECTED |
| DecisionAt             | TIMESTAMPTZ nullable | No           | NULL        | No         | Decision time             | UTC                       |
| DecisionReason         | TEXT nullable        | No           | NULL        | No         | Decision/rejection reason | Sanitized text            |

## Entity – SLAProfile

| **Field**    | **Type**     | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**             |
|--------------|--------------|--------------|-------------|------------|--------------------------|-----------------------|
| SLAProfileId | UUID         | Yes          | UUIDv7      | Yes        | Logical SLA profile      | Server generated      |
| TenantId     | UUID FK      | Yes          | \-          | No         | Tenant boundary          | Same tenant           |
| Name         | VARCHAR(200) | Yes          | \-          | No         | Profile name             | 1-200 chars           |
| Status       | ENUM         | Yes          | DRAFT       | No         | Profile state            | DRAFT/ACTIVE/INACTIVE |

## Entity – SLAVersion

| **Field**            | **Type**       | **Required** | **Default** | **Unique**         | **Purpose / Validation** | **Notes**                |
|----------------------|----------------|--------------|-------------|--------------------|--------------------------|--------------------------|
| SLAVersionId         | UUID           | Yes          | UUIDv7      | Yes                | SLA version              | Server generated         |
| SLAProfileId         | UUID FK        | Yes          | \-          | No                 | Logical SLA              | Existing profile         |
| VersionNo            | INT            | Yes          | 1           | Unique per profile | Version number           | Positive integer         |
| TargetMinutes        | INT            | Yes          | 0           | No                 | Target handling time     | Nonnegative              |
| WarningMinutes       | INT            | Yes          | 0           | No                 | Warning threshold        | 0 \<= warning \<= target |
| CalendarId           | UUID FK        | Yes          | \-          | No                 | Working calendar         | Same tenant              |
| EscalationConfigJson | JSONB nullable | No           | NULL        | No                 | Escalation policy        | Schema validated         |

## Entity – BusinessCalendar

| **Field**        | **Type**    | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**           |
|------------------|-------------|--------------|-------------|------------|--------------------------|---------------------|
| CalendarId       | UUID        | Yes          | UUIDv7      | Yes        | Working calendar         | Server generated    |
| TenantId         | UUID FK     | Yes          | \-          | No         | Tenant boundary          | Same tenant         |
| TimeZone         | VARCHAR(64) | Yes          | UTC         | No         | Calendar timezone        | Valid IANA timezone |
| WorkingHoursJson | JSONB       | Yes          | {}          | No         | Weekly working hours     | Schema validated    |
| HolidaysJson     | JSONB       | Yes          | \[\]        | No         | Holiday list             | Schema validated    |

## Entity – Task

| **Field**         | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                       |
|-------------------|----------------------|--------------|-------------|------------|--------------------------|---------------------------------|
| TaskId            | UUID                 | Yes          | UUIDv7      | Yes        | Task identifier          | Server generated                |
| TenantId          | UUID FK              | Yes          | \-          | No         | Tenant boundary          | Same tenant                     |
| RequestId         | UUID FK nullable     | No           | NULL        | No         | Optional source request  | Same tenant                     |
| CreatorId         | UUID FK              | Yes          | \-          | No         | Task creator             | Same tenant                     |
| Title             | VARCHAR(300)         | Yes          | \-          | No         | Task title               | 1-300 chars                     |
| Description       | TEXT nullable        | No           | NULL        | No         | Task detail              | Sanitized content               |
| Priority          | ENUM                 | Yes          | MEDIUM      | No         | Task priority            | LOW/MEDIUM/HIGH/CRITICAL        |
| Deadline          | TIMESTAMPTZ nullable | No           | NULL        | No         | Due date/time            | Must satisfy workflow/SLA rules |
| Status            | ENUM                 | Yes          | DRAFT       | No         | Task state               | Canonical Task state machine    |
| WorkflowVersionId | UUID FK nullable     | No           | NULL        | No         | Applied workflow version | Immutable reference             |
| SLAVersionId      | UUID FK nullable     | No           | NULL        | No         | Applied SLA version      | Immutable snapshot reference    |
| CompletedAt       | TIMESTAMPTZ nullable | No           | NULL        | No         | Completion time          | UTC                             |
| CreatedAt         | TIMESTAMPTZ          | Yes          | UTC now     | No         | Creation                 | UTC                             |
| UpdatedAt         | TIMESTAMPTZ          | Yes          | UTC now     | No         | Modification             | UTC                             |
| DeletedAt         | TIMESTAMPTZ nullable | No           | NULL        | No         | Soft delete              | UTC                             |

## Entity – TaskAssignment

| **Field**        | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**                 |
|------------------|----------------------|--------------|-------------|------------|--------------------------|---------------------------|
| TaskAssignmentId | UUID                 | Yes          | UUIDv7      | Yes        | Assignment record        | Server generated          |
| TaskId           | UUID FK              | Yes          | \-          | No         | Task                     | Existing Task             |
| DepartmentId     | UUID FK nullable     | No           | NULL        | No         | Target department        | Same tenant               |
| UserId           | UUID FK nullable     | No           | NULL        | No         | Target employee          | Same tenant               |
| AssignedBy       | UUID FK              | Yes          | \-          | No         | Assigning actor          | Authorized manager/system |
| AssignedAt       | TIMESTAMPTZ          | Yes          | UTC now     | No         | Assignment time          | UTC                       |
| AcceptedAt       | TIMESTAMPTZ nullable | No           | NULL        | No         | Acceptance time          | UTC                       |
| RejectedAt       | TIMESTAMPTZ nullable | No           | NULL        | No         | Assignment rejection     | UTC                       |
| RejectionReason  | TEXT nullable        | No           | NULL        | No         | Reason                   | Required on rejection     |
| EndedAt          | TIMESTAMPTZ nullable | No           | NULL        | No         | Assignment end           | UTC                       |

## Entity – TaskChecklistItem

| **Field**       | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**        |
|-----------------|----------------------|--------------|-------------|------------|--------------------------|------------------|
| ChecklistItemId | UUID                 | Yes          | UUIDv7      | Yes        | Checklist item           | Server generated |
| TaskId          | UUID FK              | Yes          | \-          | No         | Task                     | Existing Task    |
| Title           | VARCHAR(300)         | Yes          | \-          | No         | Checklist text           | 1-300 chars      |
| SortOrder       | INT                  | Yes          | 1           | No         | Ordering                 | Positive integer |
| IsCompleted     | BOOLEAN              | Yes          | false       | No         | Completion flag          | Boolean          |
| CompletedBy     | UUID FK nullable     | No           | NULL        | No         | Completing user          | Same tenant      |
| CompletedAt     | TIMESTAMPTZ nullable | No           | NULL        | No         | Completion time          | UTC              |

## Entity – TaskProgressReport

| **Field**        | **Type**      | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**              |
|------------------|---------------|--------------|-------------|------------|--------------------------|------------------------|
| ProgressReportId | UUID          | Yes          | UUIDv7      | Yes        | Progress report          | Server generated       |
| TaskId           | UUID FK       | Yes          | \-          | No         | Task                     | Existing Task          |
| AuthorId         | UUID FK       | Yes          | \-          | No         | Reporter                 | Authorized participant |
| Percent          | SMALLINT      | Yes          | 0           | No         | Progress percent         | 0-100                  |
| Content          | TEXT nullable | No           | NULL        | No         | Progress narrative       | Sanitized text         |
| SubmittedAt      | TIMESTAMPTZ   | Yes          | UTC now     | No         | Submission time          | UTC                    |

## Entity – TaskResult

| **Field**    | **Type**      | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**            |
|--------------|---------------|--------------|-------------|------------|--------------------------|----------------------|
| TaskResultId | UUID          | Yes          | UUIDv7      | Yes        | Final result             | Server generated     |
| TaskId       | UUID FK       | Yes          | \-          | No         | Task                     | Existing Task        |
| AuthorId     | UUID FK       | Yes          | \-          | No         | Submitter                | Authorized performer |
| Content      | TEXT nullable | No           | NULL        | No         | Final result content     | Sanitized text       |
| RevisionNo   | INT           | Yes          | 1           | No         | Result revision          | Positive integer     |
| SubmittedAt  | TIMESTAMPTZ   | Yes          | UTC now     | No         | Submission time          | UTC                  |

## Entity – Request

| **Field**            | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**       | **Notes**                       |
|----------------------|----------------------|--------------|-------------|------------|--------------------------------|---------------------------------|
| RequestId            | UUID                 | Yes          | UUIDv7      | Yes        | Request identifier             | Server generated                |
| TenantId             | UUID FK              | Yes          | \-          | No         | Tenant boundary                | Same tenant                     |
| RequesterId          | UUID FK              | Yes          | \-          | No         | Request creator                | Same tenant                     |
| ServiceId            | UUID FK              | Yes          | \-          | No         | Service requested              | Same tenant                     |
| CategoryId           | UUID FK              | Yes          | \-          | No         | Service category               | Must belong to Service          |
| ParentRequestId      | UUID FK nullable     | No           | NULL        | No         | Parent multi-intent request    | Cannot create cycle             |
| RevisedFromRequestId | UUID FK nullable     | No           | NULL        | No         | Rejected request being revised | Source must be REJECTED         |
| Title                | VARCHAR(300)         | Yes          | \-          | No         | Request title                  | 1-300 chars                     |
| Description          | TEXT                 | Yes          | \-          | No         | Request content                | Sanitized text                  |
| Priority             | ENUM                 | Yes          | MEDIUM      | No         | Request priority               | LOW/MEDIUM/HIGH/CRITICAL        |
| Status               | ENUM                 | Yes          | DRAFT       | No         | Request state                  | Canonical Request state machine |
| WorkflowVersionId    | UUID FK nullable     | No           | NULL        | No         | Applied workflow               | Immutable reference             |
| SLAVersionId         | UUID FK nullable     | No           | NULL        | No         | Applied SLA                    | Immutable snapshot reference    |
| ResolvedAt           | TIMESTAMPTZ nullable | No           | NULL        | No         | Resolution time                | UTC                             |
| ClosedAt             | TIMESTAMPTZ nullable | No           | NULL        | No         | Close time                     | UTC                             |
| CreatedAt            | TIMESTAMPTZ          | Yes          | UTC now     | No         | Creation                       | UTC                             |
| UpdatedAt            | TIMESTAMPTZ          | Yes          | UTC now     | No         | Modification                   | UTC                             |
| DeletedAt            | TIMESTAMPTZ nullable | No           | NULL        | No         | Soft delete                    | UTC                             |

## Entity – RequestRouting

| **Field**        | **Type**         | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**             |
|------------------|------------------|--------------|-------------|------------|--------------------------|-----------------------|
| RequestRoutingId | UUID             | Yes          | UUIDv7      | Yes        | Routing event            | Server generated      |
| RequestId        | UUID FK          | Yes          | \-          | No         | Request                  | Existing Request      |
| FromDepartmentId | UUID FK nullable | No           | NULL        | No         | Previous department      | Same tenant           |
| ToDepartmentId   | UUID FK nullable | No           | NULL        | No         | Target department        | Same tenant           |
| FromUserId       | UUID FK nullable | No           | NULL        | No         | Previous assignee        | Same tenant           |
| ToUserId         | UUID FK nullable | No           | NULL        | No         | Target assignee          | Same tenant           |
| RoutedBy         | UUID FK          | Yes          | \-          | No         | Actor initiating route   | User/system/AI action |
| RoutedAt         | TIMESTAMPTZ      | Yes          | UTC now     | No         | Routing time             | UTC                   |
| Reason           | TEXT nullable    | No           | NULL        | No         | Routing rationale        | Sanitized text        |
| Source           | ENUM             | Yes          | MANUAL      | No         | Routing source           | AI/MANUAL/RULE        |

## Entity – RequestResolution

| **Field**    | **Type**    | **Required** | **Default** | **Unique** | **Purpose / Validation** | **Notes**              |
|--------------|-------------|--------------|-------------|------------|--------------------------|------------------------|
| ResolutionId | UUID        | Yes          | UUIDv7      | Yes        | Resolution record        | Server generated       |
| RequestId    | UUID FK     | Yes          | \-          | No         | Request                  | Existing Request       |
| ResolverId   | UUID FK     | Yes          | \-          | No         | Resolver                 | Authorized participant |
| Content      | TEXT        | Yes          | \-          | No         | Resolution content       | Sanitized text         |
| RevisionNo   | INT         | Yes          | 1           | No         | Resolution revision      | Positive integer       |
| CreatedAt    | TIMESTAMPTZ | Yes          | UTC now     | No         | Creation                 | UTC                    |

## Entity – Comment

| **Field**  | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**     | **Notes**                    |
|------------|----------------------|--------------|-------------|------------|------------------------------|------------------------------|
| CommentId  | UUID                 | Yes          | UUIDv7      | Yes        | Comment identifier           | Server generated             |
| TenantId   | UUID FK              | Yes          | \-          | No         | Tenant boundary              | Same tenant                  |
| ObjectType | ENUM                 | Yes          | TASK        | No         | TASK/REQUEST/RESULT/PROGRESS | Allowlist                    |
| ObjectId   | UUID                 | Yes          | \-          | No         | Referenced object            | Must exist in same tenant    |
| AuthorId   | UUID FK              | Yes          | \-          | No         | Comment author               | Authorized participant       |
| Content    | TEXT                 | Yes          | \-          | No         | Comment text                 | Sanitized; max length policy |
| CreatedAt  | TIMESTAMPTZ          | Yes          | UTC now     | No         | Creation                     | UTC                          |
| EditedAt   | TIMESTAMPTZ nullable | No           | NULL        | No         | Edit time                    | UTC                          |
| DeletedAt  | TIMESTAMPTZ nullable | No           | NULL        | No         | Soft delete                  | UTC                          |

## Entity – Attachment

| **Field**    | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**             | **Notes**                      |
|--------------|----------------------|--------------|-------------|------------|--------------------------------------|--------------------------------|
| AttachmentId | UUID                 | Yes          | UUIDv7      | Yes        | Attachment metadata                  | Server generated               |
| TenantId     | UUID FK              | Yes          | \-          | No         | Tenant boundary                      | Same tenant                    |
| ObjectType   | ENUM                 | Yes          | TASK        | No         | TASK/REQUEST/COMMENT/RESULT/PROGRESS | Allowlist                      |
| ObjectId     | UUID                 | Yes          | \-          | No         | Owning object                        | Must exist in same tenant      |
| UploadedBy   | UUID FK              | Yes          | \-          | No         | Uploader                             | Authorized actor               |
| FileName     | VARCHAR(255)         | Yes          | \-          | No         | Original/display filename            | Sanitized                      |
| ContentType  | VARCHAR(100)         | Yes          | \-          | No         | MIME type                            | Allowlist                      |
| SizeBytes    | BIGINT               | Yes          | \-          | No         | File size                            | 1..524288000                   |
| ObjectKey    | VARCHAR(500)         | Yes          | \-          | Yes        | Storage object key                   | Tenant-scoped                  |
| Hash         | VARCHAR(128)         | Yes          | \-          | No         | Content hash                         | Calculated server-side         |
| Status       | ENUM                 | Yes          | UPLOADING   | No         | Upload state                         | UPLOADING/READY/FAILED/DELETED |
| CreatedAt    | TIMESTAMPTZ          | Yes          | UTC now     | No         | Creation                             | UTC                            |
| DeletedAt    | TIMESTAMPTZ nullable | No           | NULL        | No         | Soft delete                          | UTC                            |

## Entity – Confirmation

| **Field**      | **Type**      | **Required** | **Default** | **Unique** | **Purpose / Validation**  | **Notes**        |
|----------------|---------------|--------------|-------------|------------|---------------------------|------------------|
| ConfirmationId | UUID          | Yes          | UUIDv7      | Yes        | Confirmation record       | Server generated |
| TenantId       | UUID FK       | Yes          | \-          | No         | Tenant boundary           | Same tenant      |
| ObjectType     | ENUM          | Yes          | TASK        | No         | TASK/REQUEST              | Allowlist        |
| ObjectId       | UUID          | Yes          | \-          | No         | Target object             | Same tenant      |
| MilestoneType  | ENUM          | Yes          | RESULT      | No         | RECEIVE/RESULT/RESOLUTION | Allowlist        |
| ActorId        | UUID FK       | Yes          | \-          | No         | Confirming user           | Authorized actor |
| Decision       | ENUM          | Yes          | CONFIRMED   | No         | CONFIRMED/REJECTED        | Allowlist        |
| Note           | TEXT nullable | No           | NULL        | No         | Optional note             | Sanitized text   |
| ConfirmedAt    | TIMESTAMPTZ   | Yes          | UTC now     | No         | Confirmation time         | UTC              |

## Entity – Notification

| **Field**      | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**  | **Notes**               |
|----------------|----------------------|--------------|-------------|------------|---------------------------|-------------------------|
| NotificationId | UUID                 | Yes          | UUIDv7      | Yes        | Notification identifier   | Server generated        |
| TenantId       | UUID FK              | Yes          | \-          | No         | Tenant boundary           | Same tenant             |
| RecipientId    | UUID FK              | Yes          | \-          | No         | Recipient                 | Same tenant             |
| Type           | VARCHAR(80)          | Yes          | \-          | No         | Event type                | Allowlist               |
| ObjectType     | VARCHAR(40) nullable | No           | NULL        | No         | Related object type       | Optional                |
| ObjectId       | UUID nullable        | No           | NULL        | No         | Related object            | Optional same tenant    |
| Title          | VARCHAR(200)         | Yes          | \-          | No         | Notification title        | 1-200 chars             |
| Content        | TEXT                 | Yes          | \-          | No         | Notification content      | Sanitized               |
| ReadAt         | TIMESTAMPTZ nullable | No           | NULL        | No         | Read time                 | UTC                     |
| SentAt         | TIMESTAMPTZ nullable | No           | NULL        | No         | Email delivery time       | UTC                     |
| IdempotencyKey | VARCHAR(180)         | Yes          | \-          | Yes        | Duplicate suppression key | Unique per tenant/event |

## Entity – AuditLog

| **Field**    | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**       | **Notes**                     |
|--------------|----------------------|--------------|-------------|------------|--------------------------------|-------------------------------|
| AuditLogId   | UUID                 | Yes          | UUIDv7      | Yes        | Immutable audit event          | Server generated              |
| TenantId     | UUID FK nullable     | No           | NULL        | No         | Tenant for tenant-scoped event | NULL for platform-level event |
| ActorType    | ENUM                 | Yes          | USER        | No         | USER/SYSTEM/AI_AGENT           | Allowlist                     |
| ActorId      | UUID nullable        | No           | NULL        | No         | Actor identifier               | Nullable for system           |
| Action       | VARCHAR(100)         | Yes          | \-          | No         | Concrete action                | Allowlist/category            |
| ObjectType   | VARCHAR(60) nullable | No           | NULL        | No         | Object type                    | Optional                      |
| ObjectId     | UUID nullable        | No           | NULL        | No         | Object identifier              | Optional                      |
| BeforeJson   | JSONB nullable       | No           | NULL        | No         | Selected old values            | Sensitive fields excluded     |
| AfterJson    | JSONB nullable       | No           | NULL        | No         | Selected new values            | Sensitive fields excluded     |
| MetadataJson | JSONB nullable       | No           | NULL        | No         | Correlation/security metadata  | No raw secrets                |
| CreatedAt    | TIMESTAMPTZ          | Yes          | UTC now     | No         | Event time                     | UTC; append-only              |

## Entity – AIInteraction

| **Field**       | **Type**              | **Required** | **Default**      | **Unique** | **Purpose / Validation**            | **Notes**                         |
|-----------------|-----------------------|--------------|------------------|------------|-------------------------------------|-----------------------------------|
| AIInteractionId | UUID                  | Yes          | UUIDv7           | Yes        | AI call record                      | Server generated                  |
| TenantId        | UUID FK               | Yes          | \-               | No         | Tenant boundary                     | Same tenant                       |
| UserId          | UUID FK               | Yes          | \-               | No         | Requesting user                     | Same tenant                       |
| Feature         | VARCHAR(80)           | Yes          | \-               | No         | AI feature identifier               | Allowlist                         |
| ModelName       | VARCHAR(120)          | Yes          | Configured model | No         | Model used                          | Config value                      |
| InputRef        | VARCHAR(255) nullable | No           | NULL             | No         | Reference to stored sanitized input | No raw prompt by default          |
| OutputRef       | VARCHAR(255) nullable | No           | NULL             | No         | Reference to structured result      | No secrets                        |
| Status          | ENUM                  | Yes          | SUCCEEDED        | No         | AI call state                       | SUCCEEDED/FAILED/TIMEOUT/REJECTED |
| LatencyMs       | INT nullable          | No           | NULL             | No         | Latency                             | Nonnegative                       |
| TokenUsage      | JSONB nullable        | No           | NULL             | No         | Usage metadata                      | Provider response only            |
| CreatedAt       | TIMESTAMPTZ           | Yes          | UTC now          | No         | Call time                           | UTC                               |

## Entity – AIRecommendation

| **Field**          | **Type**             | **Required** | **Default** | **Unique** | **Purpose / Validation**           | **Notes**              |
|--------------------|----------------------|--------------|-------------|------------|------------------------------------|------------------------|
| RecommendationId   | UUID                 | Yes          | UUIDv7      | Yes        | Recommendation identifier          | Server generated       |
| AIInteractionId    | UUID FK              | Yes          | \-          | No         | Source AI interaction              | Existing AIInteraction |
| ObjectType         | ENUM                 | Yes          | TASK        | No         | TASK/REQUEST                       | Allowlist              |
| ObjectId           | UUID nullable        | No           | NULL        | No         | Related object if persisted        | Same tenant            |
| RecommendationType | VARCHAR(80)          | Yes          | \-          | No         | ASSIGNMENT/PARAMETERS/ROUTING/etc. | Allowlist              |
| PayloadJson        | JSONB                | Yes          | {}          | No         | Structured recommendation          | Schema validated       |
| Confidence         | NUMERIC(5,4)         | No           | NULL        | No         | Model confidence                   | 0..1                   |
| HumanDecision      | ENUM nullable        | No           | NULL        | No         | ACCEPTED/OVERRIDDEN/DISMISSED      | Required when reviewed |
| DecisionBy         | UUID FK nullable     | No           | NULL        | No         | Reviewer                           | Same tenant            |
| DecidedAt          | TIMESTAMPTZ nullable | No           | NULL        | No         | Decision time                      | UTC                    |

## Entity – AIAgentAction

| **Field**           | **Type**              | **Required** | **Default**  | **Unique** | **Purpose / Validation**      | **Notes**                                   |
|---------------------|-----------------------|--------------|--------------|------------|-------------------------------|---------------------------------------------|
| AIAgentActionId     | UUID                  | Yes          | UUIDv7       | Yes        | AI tool execution             | Server generated                            |
| AIInteractionId     | UUID FK               | Yes          | \-           | No         | Source AI interaction         | Existing interaction                        |
| ToolName            | VARCHAR(100)          | Yes          | \-           | No         | Allowlisted tool name         | Allowlist                                   |
| ArgsJson            | JSONB                 | Yes          | {}           | No         | Tool arguments                | Schema validated; sensitive fields filtered |
| AuthorizationResult | ENUM                  | Yes          | DENIED       | No         | AUTHORIZED/DENIED             | Server decision                             |
| ExecutionStatus     | ENUM                  | Yes          | NOT_EXECUTED | No         | NOT_EXECUTED/SUCCEEDED/FAILED | Allowlist                                   |
| ResultRef           | VARCHAR(255) nullable | No           | NULL         | No         | Result reference              | No secrets                                  |
| CreatedAt           | TIMESTAMPTZ           | Yes          | UTC now      | No         | Action time                   | UTC                                         |

# Appendix D – Canonical API Catalog

REST API contract uses /api/v1. HTTP responses are JSON unless the endpoint is an upload redirect/session operation. Resource IDs are opaque UUIDs. Every authenticated request is evaluated in the current tenant context.

| **API ID**   | **Method** | **Endpoint**                             | **Purpose**                                          | **Auth**                     | **Module**    |
|--------------|------------|------------------------------------------|------------------------------------------------------|------------------------------|---------------|
| API-AUTH-01  | POST       | /api/v1/auth/login                       | Login with employee code OR company email + password | Anonymous                    | Auth          |
| API-AUTH-02  | POST       | /api/v1/auth/refresh                     | Rotate refresh token and issue access token          | Authenticated                | Auth          |
| API-AUTH-03  | POST       | /api/v1/auth/forgot-password             | Start password reset                                 | Anonymous                    | Auth          |
| API-AUTH-04  | POST       | /api/v1/auth/reset-password              | Complete password reset                              | Reset token                  | Auth          |
| API-TEN-01   | POST       | /api/v1/company-registrations            | Submit company registration                          | Anonymous/Onboarding         | Tenant        |
| API-TEN-02   | GET        | /api/v1/platform/company-registrations   | List registrations                                   | Platform Admin               | Tenant        |
| API-TEN-03   | POST       | /api/v1/platform/companies/{id}/approve  | Approve registration                                 | Platform Admin               | Tenant        |
| API-TEN-04   | POST       | /api/v1/platform/tenants                 | Provision tenant                                     | Platform Admin               | Tenant        |
| API-TEN-05   | POST       | /api/v1/platform/companies/{id}/status   | Change company status                                | Platform Admin               | Tenant        |
| API-ORG-01   | GET        | /api/v1/users                            | List scoped users                                    | Company Admin/authorized     | Organization  |
| API-ORG-02   | POST       | /api/v1/users                            | Create user                                          | Company Admin                | Organization  |
| API-ORG-03   | PUT        | /api/v1/users/{id}                       | Update user                                          | Company Admin/authorized     | Organization  |
| API-ORG-04   | POST       | /api/v1/users/{id}/deactivate            | Deactivate user                                      | Company Admin                | Organization  |
| API-ORG-05   | GET        | /api/v1/departments                      | List departments                                     | Authenticated                | Organization  |
| API-ORG-06   | POST       | /api/v1/departments                      | Create department                                    | Company Admin                | Organization  |
| API-ORG-07   | PUT        | /api/v1/departments/{id}                 | Update department                                    | Company Admin                | Organization  |
| API-RBAC-01  | GET        | /api/v1/roles                            | List roles                                           | Company Admin                | Security      |
| API-RBAC-02  | POST       | /api/v1/roles                            | Create custom role                                   | Company Admin                | Security      |
| API-RBAC-03  | PUT        | /api/v1/roles/{id}/permissions           | Set role permissions                                 | Company Admin                | Security      |
| API-SVC-01   | GET        | /api/v1/services                         | List services                                        | Authenticated                | Service       |
| API-SVC-02   | POST       | /api/v1/services                         | Create service                                       | Company Admin                | Service       |
| API-SVC-03   | PUT        | /api/v1/services/{id}                    | Update service                                       | Company Admin                | Service       |
| API-SVC-04   | PUT        | /api/v1/services/{id}/routing            | Configure routing rules                              | Company Admin                | Service       |
| API-SVC-05   | POST       | /api/v1/services/{id}/categories         | Create service category                              | Company Admin                | Service       |
| API-WF-01    | GET        | /api/v1/workflows                        | List workflows                                       | Company Admin/authorized     | Workflow      |
| API-WF-02    | POST       | /api/v1/workflows                        | Create workflow draft                                | Company Admin                | Workflow      |
| API-WF-03    | POST       | /api/v1/workflows/{id}/versions          | Create next version                                  | Company Admin                | Workflow      |
| API-WF-04    | POST       | /api/v1/workflow-versions/{id}/publish   | Publish version                                      | Company Admin                | Workflow      |
| API-WF-05    | GET        | /api/v1/workflow-versions/{id}           | Read workflow version                                | Authorized                   | Workflow      |
| API-WF-06    | POST       | /api/v1/workflows/{id}/transition        | Validate/execute transition                          | Authorized runtime actor     | Workflow      |
| API-APP-01   | POST       | /api/v1/approval-instances               | Create approval instance                             | System/authorized            | Approval      |
| API-APP-02   | POST       | /api/v1/approval-instances/{id}/decision | Approve/reject                                       | Authorized approver          | Approval      |
| API-TASK-01  | GET        | /api/v1/tasks                            | List/filter tasks                                    | Scoped user                  | Task          |
| API-TASK-02  | POST       | /api/v1/tasks                            | Create task                                          | Manager/authorized           | Task          |
| API-TASK-03  | PUT        | /api/v1/tasks/{id}                       | Update task editable fields                          | Manager/authorized           | Task          |
| API-TASK-04  | POST       | /api/v1/tasks/{id}/assign                | Assign task                                          | Manager                      | Task          |
| API-TASK-05  | POST       | /api/v1/tasks/{id}/accept                | Accept task                                          | Assigned Employee            | Task          |
| API-TASK-06  | POST       | /api/v1/tasks/{id}/reject-assignment     | Reject assignment                                    | Assigned Employee            | Task          |
| API-TASK-07  | POST       | /api/v1/tasks/{id}/progress              | Update progress                                      | Assigned Employee/authorized | Task          |
| API-TASK-08  | POST       | /api/v1/tasks/{id}/progress-reports      | Submit progress report                               | Assigned Employee/authorized | Task          |
| API-TASK-09  | POST       | /api/v1/tasks/{id}/result                | Submit final result                                  | Assigned Employee/authorized | Task          |
| API-TASK-10  | POST       | /api/v1/tasks/{id}/confirmation          | Confirm/rework result                                | Manager/reviewer             | Task          |
| API-TASK-11  | POST       | /api/v1/tasks/{id}/reassign              | Reassign task                                        | Manager                      | Task          |
| API-TASK-12  | POST       | /api/v1/tasks/{id}/cancel                | Cancel task                                          | Authorized Manager/Admin     | Task          |
| API-REQ-01   | GET        | /api/v1/requests                         | List/filter requests                                 | Scoped user                  | Request       |
| API-REQ-02   | POST       | /api/v1/requests                         | Create request                                       | Employee                     | Request       |
| API-REQ-03   | PUT        | /api/v1/requests/{id}                    | Update draft/waiting request                         | Requester/authorized         | Request       |
| API-REQ-04   | POST       | /api/v1/requests/{id}/route              | Route request                                        | Manager/authorized/system    | Request       |
| API-REQ-05   | POST       | /api/v1/requests/{id}/receive            | Receive request                                      | Assigned user/manager        | Request       |
| API-REQ-06   | POST       | /api/v1/requests/{id}/information        | Provide additional information                       | Requester/authorized         | Request       |
| API-REQ-07   | POST       | /api/v1/requests/{id}/tasks              | Create task from request                             | Authorized Manager/Employee  | Request       |
| API-REQ-08   | POST       | /api/v1/requests/{id}/resolve            | Resolve request                                      | Assigned Employee/Manager    | Request       |
| API-REQ-09   | POST       | /api/v1/requests/{id}/confirmation       | Confirm resolution                                   | Requester                    | Request       |
| API-REQ-10   | POST       | /api/v1/requests/{id}/reject             | Reject request                                       | Authorized approver/manager  | Request       |
| API-REQ-11   | POST       | /api/v1/requests/{id}/revise             | Create revised request                               | Requester/authorized         | Request       |
| API-SLA-01   | GET        | /api/v1/sla-profiles                     | List SLA profiles                                    | Company Admin/authorized     | SLA           |
| API-SLA-02   | POST       | /api/v1/sla-profiles                     | Create SLA profile                                   | Company Admin                | SLA           |
| API-SLA-03   | POST       | /api/v1/sla-profiles/{id}/versions       | Create SLA version                                   | Company Admin                | SLA           |
| API-SLA-04   | GET        | /api/v1/sla/{objectType}/{id}            | View SLA status                                      | Authorized                   | SLA           |
| API-SLA-05   | POST       | /api/v1/sla/{objectType}/{id}/pause      | Pause SLA                                            | Authorized workflow actor    | SLA           |
| API-COL-01   | POST       | /api/v1/comments                         | Create comment                                       | Authorized participant       | Collaboration |
| API-COL-02   | PUT        | /api/v1/comments/{id}                    | Edit own comment                                     | Author/authorized            | Collaboration |
| API-COL-03   | POST       | /api/v1/attachments/upload-session       | Create signed/multipart upload session               | Authorized                   | Storage       |
| API-COL-04   | POST       | /api/v1/attachments/finalize             | Finalize attachment                                  | Uploader/backend             | Storage       |
| API-COL-05   | POST       | /api/v1/records/{type}/{id}/archive      | Archive record                                       | Authorized                   | Archive       |
| API-COL-06   | GET        | /api/v1/records/search                   | Search/filter scoped records                         | Authenticated                | Search        |
| API-NOTIF-01 | GET        | /api/v1/notifications                    | List notifications                                   | Authenticated                | Notification  |
| API-NOTIF-02 | PATCH      | /api/v1/notifications/{id}/read          | Mark notification read                               | Recipient                    | Notification  |
| API-REP-01   | GET        | /api/v1/dashboard/manager                | Manager dashboard                                    | Manager                      | Reporting     |
| API-REP-02   | GET        | /api/v1/reports/company                  | Company report                                       | Company Admin                | Reporting     |
| API-REP-03   | GET        | /api/v1/reports/workload                 | Workload report                                      | Manager/Company Admin        | Reporting     |
| API-REP-04   | GET        | /api/v1/reports/sla                      | SLA performance report                               | Manager/Company Admin        | Reporting     |
| API-AI-01    | POST       | /api/v1/ai/task-assistance               | AI task drafting/recommendations                     | Manager                      | AI            |
| API-AI-02    | POST       | /api/v1/ai/request-routing               | AI request analysis/routing                          | Employee/Manager             | AI            |
| API-AI-03    | POST       | /api/v1/ai/actions/execute               | Execute allowlisted AI tool action                   | AI Agent internal            | AI            |
| API-AUD-01   | GET        | /api/v1/audit-logs                       | Read scoped audit events                             | Platform/Company Admin       | Audit         |

# Appendix E – Canonical AI Feature Catalog

AI outputs are advisory unless the action is explicitly allowed by tenant policy. The server validates all IDs, enums, dates, permissions and state transitions before persistence or tool execution.

| **Feature ID** | **Feature**                  | **Trigger**                                | **Input**                                             | **Output**                                           | **Approval**                         | **Guardrail**                       |
|----------------|------------------------------|--------------------------------------------|-------------------------------------------------------|------------------------------------------------------|--------------------------------------|-------------------------------------|
| AI-TASK-01     | Assist Task Creation         | Manager describes work in natural language | Instruction + tenant configuration context            | Structured task draft                                | Human review default                 | No direct persistence               |
| AI-TASK-02     | Recommend Task Assignment    | Task draft exists                          | Task + departments + allowed users + workload summary | Department/user recommendation + confidence          | Human review                         | Server validates IDs                |
| AI-TASK-03     | Recommend Task Parameters    | Task content analyzed                      | Task + service/workflow/SLA context                   | Priority/deadline/checklist/workflow/SLA suggestions | Human review                         | Business policy wins on conflict    |
| AI-TASK-04     | Break Down Task              | Task is decomposable                       | Task text + permitted context                         | Child-task proposal list                             | Human review                         | No recursive uncontrolled execution |
| AI-TASK-05     | Monitor Task Risk            | Task is active                             | Task status + progress + SLA snapshot                 | Risk level + reasons + alert recommendation          | System policy                        | Do not change state autonomously    |
| AI-TASK-06     | Summarize Task Progress      | Task has progress history                  | Progress reports + comments + result metadata         | Concise structured summary                           | Informational                        | Sensitive fields filtered           |
| AI-REQ-01      | Analyze Request              | Employee submits request                   | Title/content/service context/tenant config           | Extracted fields + missing info + intent             | Human/system review depending policy | No AI_ANALYZED state                |
| AI-REQ-02      | Split Multi-intent Request   | Multiple actionable intents detected       | Request content + service catalog                     | Child request drafts + mappings + confidence         | Human review                         | No automatic fan-out without policy |
| AI-REQ-03      | Recommend Request Routing    | Request ready for routing                  | Request + service + routing rules + workload          | Department/user/service/priority recommendation      | Human review default                 | Fallback to rule/manual routing     |
| AI-REQ-04      | Monitor Request SLA          | Request active                             | Request state + SLA snapshot + calendar               | Risk/warning result                                  | System policy                        | No SLA mutation                     |
| AI-REQ-05      | Summarize Request Processing | Request has history                        | Timeline + resolutions + child requests               | Structured summary                                   | Informational                        | Tenant scoped                       |
| AI-OPS-01      | Aggregate Reports            | Manager/Admin asks for summary             | Authorized report data                                | Aggregated operational insight                       | Informational                        | No invented metrics                 |
| AI-OPS-02      | Execute Authorized AI Action | User/tenant policy permits tool            | Validated tool args + current permission context      | Tool result                                          | Permission + confirmation policy     | No direct DB access                 |

# Appendix F – Implementation Contract Addendum

| **Rule**                                   | **Implementation Requirement**                                                                                                                                                                                       |
|--------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Canonical state machines are authoritative | Do not create ad-hoc status values in code. Task and Request transitions must be defined in the domain layer and validated server-side.                                                                              |
| Request revision is a new record           | Never mutate a REJECTED Request back to SUBMITTED. Create a new Request with RevisedFromRequestId set to the rejected source.                                                                                        |
| Tenant boundary is mandatory               | Every tenant-owned query, write, attachment access, AI context retrieval and audit read must enforce TenantId/resource scope.                                                                                        |
| AI is not the business-rule engine         | AI proposes; domain/application services decide whether the proposal is valid and executable.                                                                                                                        |
| AI never writes SQL                        | All AI mutations must pass through allowlisted application tools/services.                                                                                                                                           |
| Large files are direct-upload              | Do not send 500 MB files through the normal JSON API request body; use signed/multipart object-storage upload sessions.                                                                                              |
| Configuration versioning is immutable      | Active workflow/SLA/approval definitions used by transactions cannot be edited in place.                                                                                                                             |
| Audit is append-only                       | Do not update or delete historical audit events through normal business APIs.                                                                                                                                        |
| P2 does not change the core model          | Advanced RAG, semantic search and analytics must not require changes to Task/Request lifecycle unless formally approved as a scope change.                                                                           |
| Stop-and-ask boundary                      | An AI coding agent must stop and request clarification if an implementation decision would change business rules, state transitions, tenant isolation, permission boundaries, entity relationships or API contracts. |

# Appendix G – Technology Verification References

Version snapshot verified against official sources on 28/09/2026. Pin exact patch versions in the repository during implementation.

| **Reference**                 | **Verified Fact / Use**                                                                                           | **Official Source**                                                                   |
|-------------------------------|-------------------------------------------------------------------------------------------------------------------|---------------------------------------------------------------------------------------|
| Microsoft .NET Support Policy | .NET 10 is LTS; latest patch 10.0.12 shown on the official support page as of 08/09/2026.                         | https://dotnet.microsoft.com/platform/support/policy                                  |
| Angular Releases              | Angular 22 is actively supported; the official schedule lists 22.2 around September 2026.                         | https://angular.dev/reference/releases                                                |
| PostgreSQL Releases           | PostgreSQL 18 is released and 18.6 is listed in the official release archive in September 2026.                   | https://www.postgresql.org/docs/release/                                              |
| Npgsql EF Core 10             | Npgsql 10.0 supports EF Core 10 and PostgreSQL 18 features.                                                       | https://www.npgsql.org/efcore/release-notes/10.0.html                                 |
| OpenAI .NET SDK               | Official .NET SDK release 2.14.0 dated 15/09/2026; supports the Responses API surface used by this specification. | https://github.com/openai/openai-dotnet/releases                                      |
| ASP.NET Core SignalR          | SignalR supports ASP.NET Core authentication/authorization and authenticated hub methods.                         | https://learn.microsoft.com/en-us/aspnet/core/signalr/authn-and-authz                 |
| Hangfire                      | Recurring jobs are supported for minute/hour/day schedules and are suitable for SLA monitoring jobs.              | https://docs.hangfire.io/en/latest/background-methods/performing-recurrent-tasks.html |
