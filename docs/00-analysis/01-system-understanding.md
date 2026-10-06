# AI-Assisted BizFlow Platform — System Understanding Report

| Item | Value |
|---|---|
| Source of truth | `AI-Assisted_BizFlow_Software_System_Specification.md` (Baseline v1.2, Scope Locked) |
| Analysis date | 2026-09-29 |
| Status | Phase 1 (Understand) — output for user confirmation |
| Canonical FR count (verified) | 62 |
| Canonical screens | 16 (UI-01 … UI-16) |
| Canonical modules | 13 (M01 … M13) |
| Canonical actors | 5 (A01 … A05) |
| Business rules | 22 (BR-001 … BR-022) |

---

## 1. System Classification

**Type:** Multi-tenant, cloud-hosted **SaaS platform for internal work and request orchestration** (B2B back-office / Business Process & Workflow Management).

Explicitly **not** an ERP, not a marketplace, not an external customer-service desk (§2.7 Out of Scope). It sits in the same product family as an ITSM / internal-service-desk / work-orchestration suite, with a configurable service catalog and SLA engine, plus an AI coordination layer.

| Question | Answer |
|---|---|
| What kind of software is this? | Enterprise multi-tenant workflow & request orchestration SaaS |
| Who are the primary users? | Company Admin, Manager, Employee (within a tenant); Platform Admin (above tenants) |
| What do users do most often? | Executing and progressing assigned work; raising internal requests; reviewing/submitting results; approving; monitoring SLA risk |
| What must users see first? | What is mine, what is late, and what needs my decision |
| Most important workflows | Task lifecycle (§8.1), Request lifecycle (§8.2), Rejected-request revision (§8.3) |
| Appropriate UI pattern | **Workflow-oriented enterprise management UI** — decision-first work inbox + operational dashboards + configuration console. Not a marketing SaaS shell, not a CRM pipeline, not mobile-first. |
| Platform Admin pattern | Compact **admin/control-plane console** (tenant registry + lifecycle actions) |

**Why this pattern and not another:** the system's value proposition is *traceability, ownership and deadline control* (§2.2, §2.6). The dominant user question is "what is at risk / what needs me next", so navigation and the landing surface must be organised around **work state and decisions**, not around database entities. Entities (services, workflows, SLA profiles, roles) are configuration, consumed rarely, and belong in a settings area — never in the primary navigation.

---

## 2. Purpose, Problem and Boundaries

**Purpose.** Turn fragmented internal work communication (chat, email, spreadsheets) into traceable, role-controlled, workflow-driven processes with AI-assisted coordination (§2.1, §2.2).

**Boundaries.**

| In scope | Out of scope |
|---|---|
| Multi-tenant onboarding + tenant isolation | Accounting, payroll, inventory, full CRM, sales |
| Identity (employee code **or** company email + password) | Enterprise SSO |
| Org structure, custom roles, permission catalog | Marketplace / B2B commerce |
| Configurable Services, Workflows, Approval Rules, SLA | External customer ticketing portal |
| Task lifecycle + milestones + evidence | Autonomous AI decisions bypassing approval/permission |
| Request lifecycle + routing + revision chain | Kubernetes / microservices |
| Attachments up to 500 MB per file | Long-term AI memory / RAG (P2) |
| Comments, notifications, audit, search/filter, archive | |
| Dashboards and operational reports | |
| Human-in-the-loop AI with permission-constrained tools | |

---

## 3. Actors

| ID | Actor | Type | Purpose | Key authority |
|---|---|---|---|---|
| A01 | Platform Administrator | Human, above tenants | Operate the platform and company/tenant lifecycle | Approve registrations, provision/suspend tenants, platform-level read of company activity, platform AI settings |
| A02 | Company Administrator | Human, tenant-scoped | Configure one company tenant | Users, departments, roles/permissions, services, routing, workflows, approval rules, SLA, company reports, tenant AI policy |
| A03 | Manager | Human, tenant-scoped | Create and coordinate work; review requests and results | Create/assign/reassign/confirm/cancel tasks, route & reject requests, approve, workload reports, use AI |
| A04 | Employee | Human, tenant-scoped | Perform assigned work and raise/handle requests | Own/assigned work, submit progress/result, create requests, confirm resolution, process requests when assigned |
| A05 | AI Agent | **Software actor** | Analyse and propose/execute *authorized* business operations | Only allowlisted tools; always evaluated against the *current human user's* identity, tenant, permission and resource scope |

**Critical architectural reading of A05:** the AI Agent is **not** a privileged actor. It inherits the calling user's authority (§16.4, BR-006). It cannot read across tenants, cannot mutate state directly (BR-007), and cannot override business rules. This means authorization code must be reusable between human-originated and AI-originated commands — one authorization path, two callers.

---

## 4. Modules

| ID | Module | Serves | Core responsibility |
|---|---|---|---|
| M01 | Authentication & Identity | A01–A04 | Login by employee code or email, refresh rotation, password reset, sessions |
| M02 | Tenant & Company | A01 | Registration → approval → tenant provisioning → status lifecycle → isolation |
| M03 | Organization | A02 | Users, departments, custom roles, permission catalog |
| M04 | Service Configuration | A02 | Services, categories, routing rules, workflow/SLA/approval binding |
| M05 | Task Management | A02–A04 | Task lifecycle: create → assign → accept → execute → progress → result → confirm |
| M06 | Request Management | A02–A04 | Request lifecycle: create → route → receive → process → resolve → confirm; revision chain |
| M07 | Workflow & Approval | A02–A03 | Versioned workflow steps/transitions; runtime approval instances |
| M08 | SLA & Scheduling | A02–A03 | SLA profiles/versions, business calendar, warning/overdue/escalation, pause |
| M09 | Notification & Collaboration | A01–A04 | In-app + email notifications, comments, activity history, realtime |
| M10 | Evidence & Storage | A02–A04 | 500 MB direct object-storage upload, attachment metadata |
| M11 | Dashboard & Reporting | A01–A03 | Operational dashboards and workload/completion/overdue/SLA reports |
| M12 | AI Agent | A02–A05 | NL understanding, recommendations, splitting, guardrailed tool execution |
| M13 | Audit & Observability | A01–A02 | Immutable audit log, structured logs, traces, metrics |

---

## 5. Core Workflows (as specified, §8)

1. **Manager → Employee task workflow** (10 steps): NL instruction → AI draft → human review → validation → create+assign → accept/reject → execute with progress/evidence → submit result → manager confirms/reworks → COMPLETED with audited milestones.
2. **Employee → Department request workflow** (9 steps): create → AI analyse (classify, extract, missing info, multi-intent) → optional child-request proposals with human review → route → RECEIVED → process (+ optional child tasks) → resolve → requester confirms → CLOSED; on rejection the original stays REJECTED and a **new** revised request is created.
3. **Rejected-request revision** (7 steps): rejected original is immutable; a new DRAFT carries `RevisedFromRequestId`; selected attachments may be reused; independent lifecycle thereafter.

**Structural observation:** every AI-touched workflow has the same five-stage shape — *generate → validate → present → human decides → system executes*. This is a single reusable pipeline, not ten separate features. It is the backbone of M12's implementation.

---

## 6. Canonical State Machines (authoritative — §9)

### Task (10 states)

`DRAFT → ASSIGNED → ACCEPTED → IN_PROGRESS → SUBMITTED → CONFIRMED → COMPLETED`, plus out-of-band `REJECTED`, `CANCELLED`, `OVERDUE`.

- REJECTED is re-enterable: `REJECTED → ASSIGNED` (manager corrects/reassigns).
- OVERDUE is **not terminal**: `IN_PROGRESS|SUBMITTED → OVERDUE` (system only), `OVERDUE → IN_PROGRESS` (work resumes).
- `SUBMITTED → IN_PROGRESS` is manager-driven rework.
- `CONFIRMED → COMPLETED` is a distinct, system/manager step (BR-016).
- `Any active → CANCELLED` requires a reason; cancelled tasks are not reopened (create a new task).

### Request (12 states)

`DRAFT → SUBMITTED → ROUTED → RECEIVED → IN_PROGRESS → RESOLVED → CONFIRMED → CLOSED`, plus `WAITING_FOR_INFORMATION`, `REJECTED`, `CANCELLED`, `OVERDUE`.

- **`AI_ANALYZED` is deliberately not a state.** AI analysis is activity, not lifecycle (GAP-011). This must be visible in the UI as an *annotation*, never as a status chip.
- `IN_PROGRESS ↔ WAITING_FOR_INFORMATION` (requester supplies information).
- `RESOLVED → CONFIRMED` (requester) → `CLOSED` (system) — BR-017.

### Status-ownership rules

| Object | Who may mutate status | Enforcement |
|---|---|---|
| Task | Manager (assign/reassign/confirm/cancel), Employee (accept/reject/inform), System (overdue), plus workflow engine | Domain state machine + workflow guard check on every write |
| Request | Manager/Assigned (route/receive/resolve/reject), System (route/close/overdue), Requester (confirm), plus workflow engine | Same |

No controller or AI tool may write a status field directly (FR-WF-002, BR-018, §28.2 rule 44).

---

## 7. Functional Requirement Inventory (62 verified)

| Module | FR prefix | Count | Requirements |
|---|---|---|---|
| M01 | FR-AUTH | 4 | 001 employee-code login, 002 email login, 003 refresh, 004 reset password |
| M02 | FR-TEN | 4 | 001 register company, 002 provision tenant, 003 company status, 004 enforce isolation |
| M03 | FR-ORG | 5 | 001 create user, 002 update user, 003 deactivate user, 004 create department, 005 configure role permissions |
| M04 | FR-SVC | 4 | 001 create service, 002 routing, 003 workflow binding, 004 approval-rule binding |
| M05 | FR-TASK | 10 | 001 create, 002 assign, 003 accept/reject, 004 execute, 005 progress, 006 progress report, 007 submit result, 008 confirm result, 009 reassign, 010 cancel |
| M06 | FR-REQ | 9 | 001 create, 002 AI routing analysis, 003 route, 004 receive, 005 process, 006 create task from request, 007 resolve, 008 confirm resolution, 009 revised request |
| M07 | FR-WF | 4 | 001 define version, 002 execute transition, 003 create approval instance, 004 record decision |
| M08 | FR-SLA | 4 | 001 configure profile, 002 monitor clock, 003 escalate overdue, 004 pause on waiting-for-information |
| M09/M10 | FR-COL | 4 | 001 comment, 002 upload attachment, 003 archive, 004 search/filter |
| M11 | FR-REP | 4 | 001 manager dashboard, 002 company report, 003 workload report, 004 SLA report |
| M12 | FR-AI | 10 | 001 task assistance, 002 assignment recommendation, 003 parameter recommendation, 004 breakdown, 005 risk monitoring, 006 progress summary, 007 multi-intent analysis, 008 split into children, 009 routing recommendation, 010 authorized action execution |
| **Total** | | **62** | Matches the baseline declaration exactly |

---

## 8. Business Rules — Implementation-Critical Reading

| Rule | What it forces in code |
|---|---|
| BR-001 Tenant isolation | A mandatory global query filter on every tenant-owned aggregate + tenant assertion on every write + AI context scoped to tenant |
| BR-002 Role boundary | Permission catalog is an allowlist; a custom role is a subset; system roles are protected from deletion |
| BR-003 Manager scope | Assign/reassign must resolve the actor's *management scope* and reject out-of-scope targets |
| BR-004 Department assignment | Assignment targets are polymorphic (user **or** department queue); department assignment stays "unclaimed" until a user accepts/claims |
| BR-005 / BR-021 AI advisory | Recommendations persist as `AIRecommendation` with `HumanDecision`; confidence never authorizes an action |
| BR-006 AI authorization | Tool calls run through the same policy evaluator as human calls, using the *current user's* effective permissions |
| BR-007 No direct DB writes by AI | AI tools may only invoke application services |
| BR-008 Revision | A REJECTED request is immutable; revision creates a new record via `RevisedFromRequestId` |
| BR-009 / BR-010 Immutability | Published Workflow/SLA/Approval versions are never edited in place; active transactions hold a version snapshot |
| BR-011 Approval append-only | Decisions are new rows; never an update of a prior decision |
| BR-012 Critical confirmation | Confirmation rows are created only at RECEIVE / RESULT / RESOLUTION milestones |
| BR-013 / BR-014 Evidence | Tenant-prefixed object keys; per-file hard cap 500 MB (524,288,000 bytes) |
| BR-015 Soft delete | Delete = `DeletedAt`/deactivation; `AuditLog` is never deleted or updated |
| BR-016 / BR-017 Completion gates | Cannot COMPLETE a task without submitted result + configured confirmation; cannot CLOSE a request without confirmation + workflow conditions |
| BR-018 Overdue | System-only; no user endpoint transitions to OVERDUE |
| BR-019 Notification idempotency | Unique `IdempotencyKey`; one escalation per configured level |
| BR-020 Audit coverage | Audit written at the application-service mutation boundary, not in controllers or UI |
| BR-022 Missing information | `WAITING_FOR_INFORMATION` + SLA pause only when the tenant SLA policy says pausable |

---

## 9. Validation Summary (§11 — enforced server-side, mirrored in forms)

| Area | Server enforcement |
|---|---|
| Identity | EmployeeCode/email unique **within tenant**; password policy; inactive/locked cannot authenticate |
| Tenant | `TenantId` required on tenant-owned rows; all FKs must resolve inside the same tenant |
| Task | Title required; deadline must satisfy workflow/business policy; assignee active and in manager scope; progress 0–100 |
| Request | Service + category + title + content required per tenant form definition; referenced department/user must be same-tenant |
| Workflow | Valid start path; transitions reference existing states; approval steps have ≥1 valid approver rule |
| SLA | Duration ≥ 0; `warning < target`; calendar valid; escalation target valid |
| Attachment | Content-type allowlist; ≤ 500 MB; tenant-scoped key; upload session expiry |
| AI | JSON-schema conformance; every enum/ID re-verified against the tenant catalog; model-supplied identifiers never trusted |
| Search | Max page size; all filters authorization-scoped; parameterized free-text |

**Verified from the SSS:** SLA warning threshold must be **strictly less than** target (§11: "warning threshold less than target"), while the `SLAVersion.WarningMinutes` dictionary note says `0 <= warning <= target`. The stricter **strict-less-than** rule governs; equal values are rejected. Recorded as a documentation inconsistency in the assumptions register.

---

## 10. Requirement Consistency Findings (SSS-internal, Phase 1)

These are documentation-level inconsistencies found while reading the SSS as a whole. None of them invalidate a business requirement; each is resolved by an explicit, documented decision in `05-assumptions-and-ambiguities.md`.

| # | Finding | Where | Severity |
|---|---|---|---|
| C-01 | Data dictionary defines **38** entities; §32 scorecard states **39** | Appendix C vs §32 | Low (cosmetic) |
| C-02 | Inline FR endpoints conflict with the Canonical API Catalog (e.g. `POST /api/v1/ai/requests/analyze` vs `POST /api/v1/ai/request-routing`; `PATCH …/companies/{id}/status` vs `POST`) | §7 vs Appendix D | Low (naming only) |
| C-03 | Multi-intent endpoints `/ai/request-multi-intent` (FR-AI-007) and `/requests/{id}/split` (FR-AI-008) are **absent** from the Canonical API Catalog | §7.11 vs Appendix D | Medium (catalog incomplete) |
| C-04 | `POST /api/v1/workflow-instances/{id}/transition` (FR-WF-002) references a **WorkflowInstance** entity that is not in the data dictionary | §7.7 vs Appendix C | Medium |
| C-05 | SLA runtime/escalation records are referenced as database impact ("SLA profile/runtime/escalation records") but **no runtime entity is defined** | §7.8 vs Appendix C | High |
| C-06 | Manager scope (BR-003) is a required authorization input but **has no entity or configuration field** | §10 vs Appendix C | High |
| C-07 | Tenant-level policies referenced by BR-005 ("unless a tenant-configured policy explicitly permits automatic action") and BR-022 (SLA pause policy) have **no settings entity** | §10 vs Appendix C | High |
| C-08 | Anonymous login must resolve a tenant, but `EmployeeCode`/`Email` are unique only **within** a tenant | FR-AUTH-001/002 | High |
| C-09 | SLA warning threshold: strict-less-than (§11) vs less-than-or-equal (dictionary) | §11 vs Appendix C | Low |
| C-10 | `Attachment.ObjectType` allows RESULT/PROGRESS while FR-COL-002 scopes uploads to "Task/Request"; `Comment.ObjectType` similarly differs from FR-COL-001 | §7.9 vs Appendix C | Low |
| C-11 | Task state machine is silent on `DRAFT → CANCELLED` and `ACCEPTED → OVERDUE`, while both are reachable under "Any active → CANCELLED" / "IN_PROGRESS|SUBMITTED → OVERDUE" | §9.1 | Low |
| C-12 | `AIInteraction.ModelName` default is "Configured model" while §33 defers the model name to deployment configuration | Appendix C vs §33 | Low |

Each High finding is resolved by a decision that **preserves the specified business behaviour** and is presented for confirmation before implementation. None of them requires new business capability — they are omissions in the physical model, not changes to the process.
