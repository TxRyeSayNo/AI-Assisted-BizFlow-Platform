# BizFlow Requirements and Architecture Traceability

| Field | Value |
|---|---|
| Version | 0.1-draft |
| Date | 2026-10-17 |
| Source baseline | SSS v1.2, sections 2, 6–10, 16–27; owner decision on workspace scope |
| Status | Source-derived; source-code/test links NOT VERIFIED |

## 1. Traceability method

Functional requirement IDs from SSS v1.2 are preserved. This matrix maps capability families to owning modules, architecture responsibilities, related data/interfaces and verification directions. It does not claim that mapped code or tests exist. Full requirement narratives remain in the SRS/source SSS.

## 2. Business objective to architecture map

| Objective | Business outcome | Requirement IDs / source area | Owning module(s) | Architecture component / data | Verification |
|---|---|---|---|---|---|
| OBJ-001 | Centralize work assignment and visible ownership | `FR-TASK-001`–`FR-TASK-010`; task flow | M05, M09, M10, M13 | Task/application service, lifecycle state, assignment history, evidence, notifications and audit | End-to-end task lifecycle; valid/invalid state transitions; authorization; result confirmation and history |
| OBJ-002 | Collect/process internal requests with traceability | `FR-REQ-001`–`FR-REQ-009`; request flow | M06, M04, M05, M07–M10, M13 | Request/application service, route/resolve history, service config, request-task relation, attachments/comments | End-to-end request; manual route; missing information; reject/revision; cross-tenant negative tests |
| OBJ-003 | Configure repeatable workflows and approval controls | `FR-SVC-003/004`, `FR-WF-001`–`FR-WF-004` | M04, M07 | Versioned workflow/approval definitions and instances | Invalid transition rejection; approved/rejected decision; immutable active version |
| OBJ-004 | Track deadlines and escalations | `FR-SLA-001`–`FR-SLA-004` | M08 + M05/M06/M07/M09 | SLA profile/runtime, business calendar, warning/escalation events | Pause/resume; timezone/calendar; rerun scheduler without duplicate escalation |
| OBJ-005 | Keep org configuration and access scoped | `FR-AUTH-001`–`FR-AUTH-004`, `FR-TEN-001`–`FR-TEN-004`, `FR-ORG-001`–`FR-ORG-005` | M01, M02, M03, M13 | Identity/session, company/tenant, member/department/role and permission data | Registration approval; active/suspended states; authorization matrix; cross-tenant negative tests |
| OBJ-006 | Preserve evidence and collaboration context | `FR-COL-001`–`FR-COL-004`, related task/request requirements | M09, M10, M13 | Comments, attachment metadata/object storage, archive/history/audit | File size/type; authorized download; tenant isolation; comment/history persistence |
| OBJ-007 | Provide operational visibility | `FR-REP-001`–`FR-REP-004` | M11 + M05/M06/M08/M13 | Dashboard/report queries and export paths | Metric definition checks, tenant/department/user scope checks, export access tests |
| OBJ-008 | AI helps with task/request work without gaining authority | `FR-AI-001`–`FR-AI-010`, AI guardrails | M12 + each owning module, M01, M02, M13 | Context assembly, schema validation, tool registry, normal application services, audit | Prompt-injection, invalid IDs, unauthorized tool, user confirmation, provider error/fallback |
| OBJ-009 | Prevent data leakage between companies | `FR-TEN-004`, security and tenant isolation sections | M01–M13 for tenant-owned operations | Trusted tenant context; scoped repositories/query policies; tenant-safe cache/files/jobs/AI/reporting | Negative tests by resource type and entry point; never rely on UI filtering only |
| OBJ-010 | Deliver useful MVP while postponing enterprise expansion | SSS v1.2 scope; DEC-002/003 | M02 plus workspace-sensitive modules | One primary workspace per company now; future multi-workspace design deferred | Review MVP scope and demo path; confirm no unintended billing/multi-workspace behavior |

## 3. Functional requirement family traceability

| Requirement family | Source requirement IDs | Owning module | Principal verification | Implementation status |
|---|---|---|---|---|
| Authentication/identity | `FR-AUTH-001`–`FR-AUTH-004` | M01 | Login method, account status, token/session handling, password reset failure cases | NOT VERIFIED |
| Company/tenant | `FR-TEN-001`–`FR-TEN-004` | M02 | Pending registration, only platform admin approval, safe provision, suspended tenant block, cross-tenant access denial | NOT VERIFIED |
| Organization | `FR-ORG-001`–`FR-ORG-005` | M03 | Add/update/deactivate user, department constraints, permission assignment scope | NOT VERIFIED |
| Service configuration | `FR-SVC-001`–`FR-SVC-004` | M04 | Tenant-scoped catalog, valid routing references, workflow/approval association | NOT VERIFIED |
| Task lifecycle | `FR-TASK-001`–`FR-TASK-010` | M05 | Creation, assignment, accept/reject, progress/result, confirmation/rework, reassign/cancel | NOT VERIFIED |
| Request lifecycle | `FR-REQ-001`–`FR-REQ-009` | M06 | Create/analyze/route/receive/process/resolve/confirm/revise | NOT VERIFIED |
| Workflow/approval | `FR-WF-001`–`FR-WF-004` | M07 | Immutable versions for active instances, valid transition, authorized decision and reason | NOT VERIFIED |
| SLA/escalation | `FR-SLA-001`–`FR-SLA-004` | M08 | Configure/monitor/pause/resume/escalate idempotently | NOT VERIFIED |
| Collaboration/evidence/archive/search | `FR-COL-001`–`FR-COL-004` | M09/M10 | Comment, secure 500 MB upload policy, archive, tenant-scoped search/filter | NOT VERIFIED |
| Reports | `FR-REP-001`–`FR-REP-004` | M11 | Manager/company/workload/SLA metrics constrained by role and tenant | NOT VERIFIED |
| AI operations | `FR-AI-001`–`FR-AI-010` | M12 | Structured suggestion, authorized tool, human oversight, invalid output and provider failure | NOT VERIFIED |

The families sum to 62 canonical functional requirements in the SSS v1.2 source baseline. This count describes the document's requirement inventory only; it is not a count of implemented features.

## 4. NFR/security-to-component traceability

| Requirement concern | Components | Verification direction | Status |
|---|---|---|---|
| Tenant/resource isolation | M01/M02, all modules, API middleware/application interfaces, PostgreSQL, object storage, Redis if used, jobs, AI | Negative tests that substitute resource/tenant identifiers at every interface; verify no data/side effects leak | Implementation NOT VERIFIED |
| Workflow integrity | M05/M06/M07/M08/M13 | Transition table and concurrency/replay tests, audit-state matching | Implementation NOT VERIFIED |
| AI safety | M12, M01/M02, owning application services, M13 | Schema validation, tool allowlist, prompt injection, confirmation and fallback tests | Implementation NOT VERIFIED |
| Evidence security | M10 + owning Task/Request services | Type/size/upload/finalization/access tests, cross-tenant download denial | Implementation NOT VERIFIED |
| Audit/observability | M13 + all mutation modules | Required event coverage, correlation check, redaction and role-scope review | Implementation NOT VERIFIED |
| Performance | M11/list APIs, database query paths, external adapters | Workload definition must be approved before load test targets can be set | Threshold OPEN |
| Recovery/retention | PostgreSQL, object storage, configuration/deployment | Approve retention, backup, RPO/RTO and perform restore tests | Policy OPEN |

## 5. Architecture decisions traceability

| Decision | Connected requirements / objectives | Impacted areas |
|---|---|---|
| DEC-001 Modular Monolith | Maintainability, five-person team, module responsibility | API/application layer, all modules, test organization |
| DEC-002 One primary workspace/company MVP | `FR-TEN-*`, `FR-ORG-*`, tenant isolation | M02/M03 and all tenant-scoped reads/writes |
| DEC-003 Future paid multi-workspace | Future Enterprise scope; not current functional requirement | Future membership/entitlement/billing, data model and UX — deferred |
| DEC-004 GRID design system | UI consistency and accessibility target | Angular routes/forms/tables/status indicators |
| DEC-005 Tenant isolation / DB approach OPEN | `FR-TEN-004`, security requirements | Query policies, tenant-owned data, cache/files/jobs/reports/AI |
| DEC-006 AI constrained tool execution | `FR-AI-001`–`FR-AI-010`, AI guardrails | M12, application services, authorization and audit |
| DEC-007 Workflow versioning | `FR-WF-*`, `FR-SVC-003/004`, SLA links | M04/M07/M08 and in-flight task/request records |

## 6. Known traceability gaps

- No requirement-to-specific-source-file mapping is possible until the correct BizFlow repository is identified.
- Test cases exist as source/SSS acceptance scenarios but no executed test evidence was inspected.
- Exact API schema and RDS columns/constraints are not verified against application source.
- NFR numerical targets and approved retention/recovery policy remain open.
- Entity count in the source SSS has an unresolved 38-vs-39 discrepancy.
