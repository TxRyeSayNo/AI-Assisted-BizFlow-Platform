# AI-Assisted BizFlow Platform — Requirement Traceability Matrix

Every canonical Functional Requirement is traced to its module, API, persistence, screen and verification. This is the artefact that proves no requirement was lost (task §20) and it is re-verified in Phase 9.

## Owner-approved implementation amendments — 2026-10-06

TD-01–04 are accepted in [ADR-0012](../01-decisions/ADR-0012-task-creation-and-detail-contract.md). API/Application rules are synchronized in [the creation/detail contract](../02-traceability/task-creation-and-detail.md); required tests are specified in [the acceptance plan](../../tests/task-decision-acceptance.md), before implementation.

| Decision | Requirement/module | API/business rule | Persistence/UI | Test acceptance |
|---|---|---|---|---|
| TD-01 | M05 / SSS §9.1 / ADR-0003 | Unpinned Tasks use exact canonical Domain transitions | No new state/table; unchanged workflow boundary | Exhaustive canonical state-pair tests |
| TD-02 | FR-TASK-001 | Supplied absolute explicit-offset creation deadline strictly after server now, normalized UTC | Task.Deadline; creation form | Offset, omission, equal/past rejection and UTC persistence |
| TD-03 | FR-TASK-001 / mutation replay | Optional Idempotency-Key, normalized fingerprint, same-result replay, conflict on changed payload | Immutable AuditLog metadata, database lock and unique index; retained UI retry key | Concurrent duplicate/conflict, tenant/actor isolation, revoked grants, rollback and expired-deadline replay |
| TD-04 | API-TASK/UI-05 / ADR-0011 | Added GET `/api/v1/tasks/{id}`, scoped read with non-revealing 404 | Existing Task/checklist/assignment/evidence tables and detail UI | All read scopes, foreign/missing/denied equivalence, parent-scoped evidence and pagination |

These rows record approved implementation requirements, not completed-test claims. Current evidence remains in [implementation status](../02-traceability/implementation-status.md).

### Task receipt implementation mapping

FR-TASK-003 / API-TASK-05 / BR-004/012/020 / ADR-0010 map to `TaskAcceptanceCommand` → `IWorkflowEngine.AcceptTask` → the canonical ASSIGNED/ACCEPTED policy → `TaskAcceptanceStore` → `POST /api/v1/tasks/{id}/accept` → the GRID Task-detail acceptance dialog. The transaction writes the existing Task/TaskAssignment/AuditLog/Notification records and the specification's ten-field Confirmation entity. The table is part of the approved target model, not an additive convenience table. Actor, target, one-time queue claim, immutable receipt and replay behavior are specified in [the acceptance contract](../02-traceability/task-acceptance-command.md).

Verification maps to `TaskAcceptanceTests`, `TaskAcceptanceApiTests`, Angular `task-acceptance.spec.ts`, and the real desktop/mobile assignment → recipient acceptance → manager notification scenario. Remaining critical-milestone workflow configuration, rejection, execution and the polymorphic consistency-check job remain acceptance gates; no complete FR-TASK-003 claim follows from this mapping.

**Reading note.** Where the SSS's inline FR text specifies an endpoint that Appendix D omits, the FR-specified endpoint is implemented and marked *(FR-spec)*. Findings and their resolutions are in `05-assumptions-and-ambiguities.md`.

**Test-ID note.** §26 cites `TST-REQ-007` and `TST-TASK-007`, which §27 does not define. Both identifiers are intended (request resolve/confirm; task result/confirm) and are implemented under those exact IDs so the traceability links in §26 resolve. Recorded as a minor documentation inconsistency.

---

## 1. Authentication & Identity (M01)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-AUTH-001 | Login with employee code | `POST /api/v1/auth/login` (A-01) | User, Tenant, RefreshToken | UI-01 | TST-AUTH-001 |
| FR-AUTH-002 | Login with company email | `POST /api/v1/auth/login` (A-01) | User, Tenant, RefreshToken | UI-01 | TST-AUTH-001 |
| FR-AUTH-003 | Refresh access token | `POST /api/v1/auth/refresh` | RefreshToken | token interceptor | integration |
| FR-AUTH-004 | Reset password (email + admin-issued) | `POST /api/v1/auth/forgot-password`, `POST /api/v1/auth/reset-password`, `POST /api/v1/users/{id}/reset-password` (A-12) | User, PasswordResetToken | UI-01, `/settings/organization/users` | integration |

## 2. Tenant & Company (M02)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-TEN-001 | Register company (→ Pending) | `POST /api/v1/company-registrations` | Company | public onboarding form | integration |
| FR-TEN-002 | Approve + provision tenant with initial Company Admin | `POST /api/v1/platform/companies/{id}/approve`, `POST /api/v1/platform/tenants` | Company, Tenant, User, UserRole, Role, TenantSetting, BusinessCalendar | UI-02 | integration (M1) |
| FR-TEN-003 | Change company/tenant status | `POST /api/v1/platform/companies/{id}/status` (A-06) | Company, Tenant, AuditLog | UI-02 | integration |
| FR-TEN-004 | Enforce tenant data isolation | cross-cutting on **all** endpoints | TenantId on every tenant-owned entity, AuditLog | all | **TST-SEC-001** |

## 3. Organization, Roles & Permissions (M03)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-ORG-001 | Create user account | `POST /api/v1/users` | User, UserRole, Department, ManagementScope (A-02) | `/settings/organization/users` | integration |
| FR-ORG-002 | Update user profile | `PUT /api/v1/users/{id}` | User, UserRole, ManagementScope | `/settings/organization/users`, `/profile` | integration |
| FR-ORG-003 | Deactivate user | `POST /api/v1/users/{id}/deactivate` | User, RefreshToken, TaskAssignment | `/settings/organization/users` | integration |
| FR-ORG-004 | Create department | `GET/POST /api/v1/departments`, `PUT /api/v1/departments/{id}` | Department | `/settings/organization/departments` | integration |
| FR-ORG-005 | Configure role permissions | `GET /api/v1/roles`, `POST /api/v1/roles`, `PUT /api/v1/roles/{id}/permissions` | Role, Permission, RolePermission (A-10) | `/settings/organization/roles` | integration |

## 4. Service Configuration (M04)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-SVC-001 | Create internal service | `GET/POST /api/v1/services`, `PUT /api/v1/services/{id}`, `POST /api/v1/services/{id}/categories` | Service, ServiceCategory | `/settings/services` | integration |
| FR-SVC-002 | Configure service routing | `PUT /api/v1/services/{id}/routing` | RoutingRule, Department, Role | `/settings/services/:id` | integration |
| FR-SVC-003 | Configure workflow for service | `PUT /api/v1/services/{id}/workflow` *(FR-spec)* | Service.ActiveWorkflowVersionId, WorkflowVersion | `/settings/services/:id` | integration |
| FR-SVC-004 | Configure approval rule for service | `PUT /api/v1/services/{id}/approval-rules` *(FR-spec)* | ApprovalRule, ApprovalRuleVersion, Service.ActiveApprovalRuleVersionId (A-14) | `/settings/services/:id` | integration |

## 5. Task Management (M05)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-TASK-001 | Create task (structured or AI draft) | `GET/POST /api/v1/tasks` | Task, TaskChecklistItem, WorkflowVersion, SLAVersion | `/tasks/new` (UI-06), `/tasks` | TST-TASK-001 |
| FR-TASK-002 | Assign task to user or department | `POST /api/v1/tasks/{id}/assign` | TaskAssignment, ManagementScope | `/tasks/:id` | TST-TASK-001, TST-TASK-002 |
| FR-TASK-003 | Accept or reject assignment | `POST /api/v1/tasks/{id}/accept`, `POST /api/v1/tasks/{id}/reject-assignment` | TaskAssignment, Confirmation | `/work`, `/tasks/:id` | TST-TASK-001, TST-TASK-002 |
| FR-TASK-004 | Execute task (start work) | `POST /api/v1/tasks/{id}/start` *(FR-spec)* | Task, AuditLog | `/tasks/:id` | integration |
| FR-TASK-005 | Update task progress | `POST /api/v1/tasks/{id}/progress` | TaskProgressReport, Attachment | `/tasks/:id` | TST-TASK-003 |
| FR-TASK-006 | Submit formal progress report | `POST /api/v1/tasks/{id}/progress-reports` | TaskProgressReport, Attachment | `/tasks/:id` | TST-TASK-003 |
| FR-TASK-007 | Submit task result | `POST /api/v1/tasks/{id}/result` | TaskResult, Attachment | `/tasks/:id` | TST-TASK-003, **TST-TASK-007** |
| FR-TASK-008 | Confirm task result | `POST /api/v1/tasks/{id}/confirmation` | Confirmation, Task, ApprovalInstance | `/tasks/:id` | TST-TASK-004, **TST-TASK-007** |
| FR-TASK-009 | Reassign task | `POST /api/v1/tasks/{id}/reassign` | TaskAssignment, ManagementScope, AuditLog | `/tasks/:id` | integration |
| FR-TASK-010 | Cancel task | `POST /api/v1/tasks/{id}/cancel` | Task, AuditLog | `/tasks/:id` | integration |
## 6. Request Management (M06)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-REQ-001 | Create internal request | `GET/POST /api/v1/requests` | Request, Attachment, WorkflowVersion, SLAVersion | UI-08 | TST-REQ-001 |
| FR-REQ-002 | Analyze request for routing (AI) | `POST /api/v1/ai/request-routing` (A-07) | AIInteraction, AIRecommendation | UI-09 | TST-AI-009 |
| FR-REQ-003 | Route request | `POST /api/v1/requests/{id}/route` | RequestRouting, Request, SLAState | UI-10 | TST-REQ-001 |
| FR-REQ-004 | Receive request | `POST /api/v1/requests/{id}/receive` | Confirmation, Request, SLAState | UI-10 | TST-REQ-001 |
| FR-REQ-005 | Process assigned request | `POST /api/v1/requests/{id}/process-actions`, `POST /api/v1/requests/{id}/information` | Request, Comment, Task, SLAState/SLAEvent | UI-10 | TST-REQ-002 |
| FR-REQ-006 | Create task from request | `POST /api/v1/requests/{id}/tasks` | Task (RequestId FK), Request | UI-10 | TST-REQ-002 |
| FR-REQ-007 | Resolve request | `POST /api/v1/requests/{id}/resolve` | RequestResolution, Request, Attachment | UI-10 | TST-REQ-003, **TST-REQ-007** |
| FR-REQ-008 | Confirm request resolution | `POST /api/v1/requests/{id}/confirmation` | Confirmation, Request | UI-10 | TST-REQ-003 |
| FR-REQ-009 | Create revised request from rejected request | `POST /api/v1/requests/{id}/revise` | Request (RevisedFromRequestId), Attachment | UI-10, `/requests/:id/revise` | **TST-REQ-008** |

## 7. Workflow & Approval (M07)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-WF-001 | Define workflow version | `GET/POST /api/v1/workflows`, `POST /api/v1/workflows/{id}/versions`, `POST /api/v1/workflow-versions/{id}/publish`, `GET /api/v1/workflow-versions/{id}` | Workflow, WorkflowVersion, WorkflowStep, WorkflowTransition | UI-11 | TST-WF-001 |
| FR-WF-002 | Execute workflow transition | `POST /api/v1/workflow-transitions` (A-05) | WorkflowVersion/Step/Transition, AuditLog, Task/Request status | UI-05, UI-10 | TST-WF-001 |
| FR-WF-003 | Create approval instance | `POST /api/v1/approval-instances` | ApprovalInstance, ApprovalStepInstance, ApprovalRuleVersion | UI-12 | TST-WF-004 |
| FR-WF-004 | Record approval decision | `POST /api/v1/approval-instances/{id}/decision` | ApprovalStepInstance, ApprovalInstance, AuditLog | UI-12 | TST-WF-004 |

## 8. SLA & Escalation (M08)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-SLA-001 | Configure SLA profile | `GET/POST /api/v1/sla-profiles`, `POST /api/v1/sla-profiles/{id}/versions` | SLAProfile, SLAVersion, BusinessCalendar (A-08) | `/settings/sla-profiles` | integration |
| FR-SLA-002 | Monitor SLA clock | background job + `GET /api/v1/sla/{objectType}/{id}` | SLAState, SLAEvent, BusinessCalendar (A-04) | UI-13, SLA badge | TST-SLA-001 |
| FR-SLA-003 | Escalate overdue work | background job (internal) | SLAState, SLAEvent (unique IdempotencyKey), Notification | UI-13 | TST-SLA-002 |
| FR-SLA-004 | Pause SLA on waiting for information | `POST /api/v1/sla/{objectType}/{id}/pause`, `POST /api/v1/requests/{id}/information` | SLAState, SLAEvent, TenantSetting (A-03) | UI-10, UI-13 | TST-AI-009, integration |

## 9. Collaboration, Evidence & History (M09/M10)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-COL-001 | Create comment | `POST /api/v1/comments`, `PUT /api/v1/comments/{id}` | Comment, Attachment, Notification | UI-05, UI-10 | integration |
| FR-COL-002 | Upload attachment ≤ 500 MB | `POST /api/v1/attachments/upload-session`, `POST /api/v1/attachments/finalize` | Attachment (A-09, A-03 policy) | uploader component | **TST-FILE-001**, **TST-FILE-002** |
| FR-COL-003 | Archive completed record | `POST /api/v1/records/{type}/{id}/archive` | Task/Request.DeletedAt-style archive, AuditLog | UI-04, UI-07 | integration |
| FR-COL-004 | Search and filter records | `GET /api/v1/tasks`, `GET /api/v1/requests`, `GET /api/v1/records/search` | read-only queries over Task/Request | UI-04, UI-07 | integration |

## 10. Dashboard & Reporting (M11)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-REP-001 | View manager dashboard | `GET /api/v1/dashboard/manager` | read-only aggregation (A-11 metrics) | `/home` | integration |
| FR-REP-002 | View company report | `GET /api/v1/reports/company` | read-only aggregation, AuditLog | UI-16 | integration |
| FR-REP-003 | Generate workload report | `GET /api/v1/reports/workload` | Task/TaskAssignment aggregation | UI-16 | integration |
| FR-REP-004 | Generate SLA performance report | `GET /api/v1/reports/sla` | SLAState, SLAEvent, SLAVersion snapshots | UI-16 | TST-SLA-002 |

## 11. AI-Assisted Operations (M12)

| FR | Requirement | API | Entities | Screen | Verification |
|---|---|---|---|---|---|
| FR-AI-001 | Assist task creation | `POST /api/v1/ai/task-assistance` | AIInteraction, AIRecommendation | UI-06 | TST-AI-003 |
| FR-AI-002 | Recommend task assignment | `POST /api/v1/ai/task-assignment-recommendation` (A-07) | AIInteraction, AIRecommendation, User, Department | UI-06 | TST-AI-003 |
| FR-AI-003 | Recommend task parameters | `POST /api/v1/ai/task-parameters` (A-07) | AIRecommendation, Service, WorkflowVersion, SLAVersion | UI-06 | TST-AI-003 |
| FR-AI-004 | Break down task | `POST /api/v1/ai/task-breakdown` (A-07) | AIRecommendation | UI-06 | unit (schema + no cyclic subtasks) |
| FR-AI-005 | Monitor task risk | `POST /api/v1/ai/task-risk` (A-07) | AIRecommendation, SLAState | `/home`, UI-13 | integration (never mutates state) |
| FR-AI-006 | Summarize task progress | `POST /api/v1/ai/task-summary` (A-07) | AIInteraction, Task/TaskProgressReport (authorized read) | UI-05 | fact-check fixture |
| FR-AI-007 | Analyze multi-intent request | `POST /api/v1/ai/request-multi-intent` (A-07) | AIInteraction, AIRecommendation | UI-09 | **TST-AI-008** |
| FR-AI-008 | Split request into child requests | `POST /api/v1/requests/{id}/split` (A-07) | Request (ParentRequestId), AIRecommendation | UI-09, UI-10 | **TST-AI-008** |
| FR-AI-009 | Recommend request routing | `POST /api/v1/ai/request-routing` | AIRecommendation, Service, RoutingRule, Department | UI-09 | **TST-AI-009** |
| FR-AI-010 | Execute authorized AI action | `POST /api/v1/ai/actions/execute` (internal) | AIAgentAction, AIInteraction, AuditLog | none (server-side) | **TST-AI-006** |

---

## 12. Cross-Cutting Coverage

| Concern | Requirement | Where enforced | Verification |
|---|---|---|---|
| Tenant isolation | BR-001, FR-TEN-004 | global query filter + write assertion + AI context scoping | TST-SEC-001 |
| Audit coverage | BR-020 | `IAuditWriter` at every application-service mutation | audit-completeness integration test |
| Notification idempotency | BR-019 | unique `IdempotencyKey` on Notification and SLAEvent | TST-SLA-001/002 |
| Immutable versions | BR-009, BR-010 | publish-only versioning; snapshot FKs on Task/Request | unit + integration |
| Approval append-only | BR-011 | insert-only `ApprovalStepInstance` decisions | TST-WF-004 |
| Concurrency | GAP-025 | RowVersion/ETag on mutable aggregates | conflict integration test |
| Error envelope | §20.2 | global exception/validation middleware | API contract test |
| Rate limiting | §19 | `/auth/*` and `/ai/*` policies | integration |

---

## 13. Coverage Statement

| Category | Count | Status |
|---|---|---|
| Functional requirements traced | 62 / 62 | ✅ |
| Modules covered | 13 / 13 | ✅ |
| Actors supported | 5 / 5 | ✅ |
| Canonical screens mapped | 16 / 16 (+ 9 supporting screens required by FRs) | ✅ |
| Business rules with an enforcement point | 22 / 22 | ✅ |
| §27 test IDs assigned to at least one requirement | all except TST-SEC-001-scope-adjacent (mapped to FR-TEN-004) | ✅ |

Nine items in Part A of the assumptions register close physical-model omissions and must be confirmed before Phase 2. No requirement in this matrix is deferred, weakened or dropped; P2 items (natural-language search, RAG, advanced BI, advanced calendars) are excluded by the SSS itself (§25) and appear nowhere above.
