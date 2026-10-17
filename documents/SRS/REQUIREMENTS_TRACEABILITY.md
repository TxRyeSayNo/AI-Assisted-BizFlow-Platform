# BizFlow Requirements Traceability

Version 0.1 · Draft · Companion to [`SRS.md`](SRS.md) and [`REQUIREMENTS_REGISTER.md`](REQUIREMENTS_REGISTER.md).

## 1. Business objectives to use cases and requirements


|Objective|Objective statement|Use cases|Representative requirements|Source / status|
|---|---|---|---|---|
|OBJ-001|Centralize internal work and requests|UC-003; UC-004; UC-005|FR-TASK-001..010; FR-REQ-001..008|SSS 2.1–2.7; confirmed in source|
|OBJ-002|Clarify ownership and routing|UC-003; UC-004|FR-TASK-002/009; FR-REQ-003..005; FR-SVC-002|SSS FR tables; confirmed in source|
|OBJ-003|Configurable workflows and approvals|UC-007|FR-SVC-003/004; FR-WF-001..004; BR-009/011|SSS FR/WF/BR; confirmed in source|
|OBJ-004|Track SLA and overdue work|UC-008|FR-SLA-001..004; FR-REP-004; BR-018/019/022|SSS 7.8, 10, 21; NFR target details partly proposed|
|OBJ-005|Evidence and auditability|UC-003; UC-004; UC-010|FR-COL-001..004; BR-012..020; SEC-005/009/011|SSS 7.9, 10, 19, 21|
|OBJ-006|Safe AI assistance|UC-009|FR-AI-001..010; BR-005..007/021; SEC-006..008|SSS 7.11, 16, 19; implementation unverified|
|OBJ-007|Prevent cross-tenant data access|UC-001; UC-010|FR-TEN-004; NFR-SEC-001; SEC-001..004/012; BR-001|SSS tenant isolation and tests|
|OBJ-008|Paid multi-workspace upgrade later|Future use case not in MVP|Future Enterprise change set; do not add MVP FR until approved|Explicit user decision 2026-10-16|

## 2. Use cases to requirements

|Use case|Related requirements|Primary verification|
|---|---|---|
|UC-001|FR-TEN-001..004|TST-AUTH-001; TST-SEC-001|
|UC-002|FR-ORG-001..005|Tenant-scoped CRUD/role tests; negative privilege escalation|
|UC-003|FR-TASK-001..010|TST-TASK-001..004|
|UC-004|FR-REQ-001..008|TST-REQ-001; TST-REQ-003|
|UC-005|FR-REQ-006|TST-REQ-002|
|UC-006|FR-REQ-009; BR-008|TST-REQ-008|
|UC-007|FR-WF-001..004|TST-WF-001; TST-WF-004|
|UC-008|FR-SLA-001..004; FR-REP-001..004|TST-SLA-001/002; report aggregation checks|
|UC-009|FR-AI-001..010; SEC-006..008|TST-AI-003/006/008/009; prompt-injection and tenant-context tests|
|UC-010|FR-TEN-004; SEC-001..004; TST-SEC-001|TST-SEC-001|


## 3. Requirements to source traceability


|ID pattern / decision|Source|Coverage note|
|---|---|---|
|FR-AUTH-*|SSS v1.2 Section 7.1|Identity/login/refresh/reset|
|FR-TEN-*|SSS v1.2 Section 7.2|Company onboarding and tenant isolation|
|FR-ORG-*|SSS v1.2 Section 7.3|Users, departments, roles and permissions|
|FR-SVC-*|SSS v1.2 Section 7.4|Services/routing/workflow association|
|FR-TASK-*|SSS v1.2 Section 7.5|Task lifecycle|
|FR-REQ-*|SSS v1.2 Section 7.6|Request lifecycle and revision|
|FR-WF-*|SSS v1.2 Section 7.7|Workflow/approval|
|FR-SLA-*|SSS v1.2 Section 7.8|SLA/escalation|
|FR-COL-*|SSS v1.2 Section 7.9|Comments, evidence, archive and search|
|FR-REP-*|SSS v1.2 Section 7.10|Dashboard and reports|
|FR-AI-*|SSS v1.2 Section 7.11 / Appendix E|AI assisted operations|
|NFR-*|SSS v1.2 Section 21|Non-functional baseline, some proposals|
|BR-*|SSS v1.2 Section 10|Business invariants|
|SEC-*|SSS Sections 10, 16, 19 and rules summarized in register|Security requirements derived from source controls|
|DEC-WS-001|User conversation, 2026-10-16|MVP one workspace per company; paid multi-workspace upgrade later|
|EVD-REPO-001|Connected GitHub repository discovery|Only accessible repository mismatches target stack; implementation cannot be verified|

## 4. Security and verification traceability

| Threat/control | Requirements | Verification scenario | State |
|---|---|---|---|
| Cross-tenant read/write | FR-TEN-004; NFR-SEC-001; SEC-001/003/012; BR-001 | TST-SEC-001; substitute identifiers for task/request/attachment/report/AI context | Acceptance test specified; execution not verified |
| Privilege escalation | FR-ORG-005; SEC-002/004 | Attempt to grant disallowed platform/system permission from tenant admin | Test required; execution not verified |
| Unsafe attachment access | FR-COL-002; SEC-005/011; BR-013/014 | Cross-tenant download; disallowed file type; >500 MB upload | TST-FILE-001/002 and negative access tests; execution not verified |
| Invalid workflow transition | FR-WF-002; BR-009/011/016/017 | Submit transition inconsistent with current state, actor or version | TST-WF-001; execution not verified |
| AI tool bypass | FR-AI-010; SEC-006..008; BR-005..007 | Unauthorized target/tool, invented entity ID, prompt-injection content | TST-AI-006 plus dedicated security cases; execution not verified |
| Duplicate scheduled side effect | FR-SLA-003; NFR-REL-001; BR-019 | Repeat scheduler event for same overdue/escalation key | TST-SLA-001/002; execution not verified |
| Audit tampering | SEC-009; BR-015/020; NFR-DATA-001 | Attempt to update/delete previous audit event through business API | Add integration/security test; execution not verified |

## 5. Incomplete traceability / evidence limits

1. The exact Git repository for the BizFlow application was not discoverable from the connected GitHub account; code-symbol/route/migration/test-level traceability is therefore not available.
2. SSS has a baseline API catalog and test plan, but none of the endpoints or test outcomes could be compared against the running application.
3. Proposed numerical NFRs do not yet have approved measurement workload/conditions.
4. Future multi-workspace is a product decision only; detailed future data model, entitlements and billing are intentionally untraced to MVP implementation.
