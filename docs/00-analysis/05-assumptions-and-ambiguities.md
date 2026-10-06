# AI-Assisted BizFlow Platform — Assumptions & Ambiguities Register

Per SSS §28: *"If a coding agent encounters an ambiguity that changes business behavior, authorization, database relationships or API contracts, it must STOP AND ASK rather than silently inventing a new rule. Minor UI/layout choices may be decided locally."*

This register separates:

- **PART A — Requires your confirmation** (behaviour, authorization, schema or API-contract impact). Not implemented until you confirm.
- **PART B — Engineering decisions taken locally** (resolved from existing requirements or industry convention; reversible; no business change).

Every Part A item closes an *omission*. No requirement is removed, reworded or weakened anywhere in this plan.

---

# PART A — Requires Your Confirmation

## A-01 — How does an anonymous login resolve the tenant? (finding C-08)

**Issue.** FR-AUTH-001/002 allow login with employee code or company email, and login is `Anonymous` (Appendix D). But `EmployeeCode` and `Email` are unique only **within a tenant** (Appendix C). An anonymous request cannot know which tenant to search, so two tenants can legitimately hold the same employee code.

**SSS evidence.** FR-AUTH-001 Alternative Flow: *"If code is ambiguous globally, require tenant/company context or scoped login"* — the SSS anticipates exactly this and prescribes the fallback but not the mechanism.

**Decision (preserves the FR exactly).** `POST /api/v1/auth/login` accepts `{ identifier, password, tenantKey? }`:

1. `tenantKey` supplied → resolve within that tenant only.
2. `tenantKey` absent + identifier matches exactly one active user platform-wide → authenticate, return that tenant's context.
3. `tenantKey` absent + identifier matches users in more than one tenant → `409 AUTH.TENANT_CONTEXT_REQUIRED` with selectable company display names only (never user data), and the login screen shows a company selector.

**Impact.** Login gains an optional company selector shown only when required; the login DTO gains one optional field; no new business rule.

**Confirm?** **Yes** — changes a public API contract and the login screen.

*Alternative rejected:* making employee code globally unique contradicts Appendix C ("unique within tenant") and breaks the multi-tenant model.

---

## A-02 — What is "Manager scope"? (finding C-06, BR-003)

**Issue.** BR-003 and FR-TASK-002/009 require a manager to assign only "within configured management scope", and GAP-003 declares it "tenant/configuration controlled". No entity, field or configuration surface for it exists in Appendix C.

**Decision (simplest thing that satisfies BR-003 and stays auditable).**

| Element | Choice |
|---|---|
| Model | Additive table `ManagementScope (ManagementScopeId, TenantId, UserId, DepartmentId, IncludeDescendants, CreatedBy, CreatedAt)` |
| Effective scope | Union of the user's listed departments (plus descendants when flagged) |
| Default | Seeded from the user's **own department subtree** whenever a user holding `tasks.assign` is created or moved between departments |
| Held by | Any role whose permission set contains `tasks.assign` — scope follows capability, not role display name |
| Configurable | Company Admin edits it on the user detail screen (Managers section) |
| Enforced | `IResourceAuthorizer` rejects out-of-scope assign/reassign targets with `403 TASK.TARGET_OUT_OF_SCOPE` |

**Impact.** One additive table, one configuration surface, no change to any stated rule.

**Confirm?** **Yes** — introduces a schema object.

*Alternative rejected:* "manager scope = own department subtree, not configurable". GAP-003 explicitly calls it configuration-controlled, and the RBAC matrix grants managers reassignment authority that would be unusable if scope could never be widened.

---

## A-03 — Where do tenant policies live? (finding C-07)

**Issue.** BR-005 ("unless a tenant-configured policy explicitly permits automatic action") and BR-022 ("SLA pause follows tenant SLA policy") both reference tenant-level policy, and the RBAC matrix grants Company Admin "Tenant AI policy" and config rights for Notification/SLA. Appendix C has no settings or policy entity.

**Decision.** Additive table `TenantSetting (TenantSettingId, TenantId, Key, ValueJson, UpdatedBy, UpdatedAt)` with a fixed allowlisted key set:

| Key | Type | Default | Governs |
|---|---|---|---|
| `ai.enabled` | bool | `true` | Whether AI features are exposed for the tenant |
| `ai.auto_action_enabled` | bool | `false` | BR-005 — may any tool execute without human confirmation |
| `ai.auto_action_tools` | string[] | `[]` | Exact tools allowed to auto-execute when the flag above is on |
| `ai.confidence_threshold` | number | `0.6` | Below this, force manual review / fallback (BR-021) |
| `ai.monthly_call_limit` | int | tenant policy | Budget guard |
| `sla.pause_on_waiting_for_information` | bool | `true` | BR-022 / FR-SLA-004 |
| `request.generic_service_allowed` | bool | `false` | FR-REQ-001 ("unless tenant permits generic request") |
| `attachment.allowed_content_types` | string[] | GAP-013 baseline | FR-COL-002 validation |
| `attachment.max_size_bytes` | int | `524288000` | BR-014 (never raisable above 500 MB) |
| `report.max_range_days` | int | `366` | FR-REP-003 ("range ≤ configurable max") |
| `notification.email_enabled` | bool | `true` | §22 channels |

**Impact.** One additive table; a policy tab in Settings; the AI guardrail path reads two keys.

**Confirm?** **Yes** — schema addition.

*Alternative rejected:* hard-coded application constants — BR-005/BR-022 explicitly require tenant configuration.

---

## A-04 — Where does SLA runtime state live? (finding C-05)

**Issue.** FR-SLA-002/003/004 declare database impact on "SLA profile/runtime/**escalation records**" and require "at most one escalation per configured level" plus traceable paused duration. Appendix C defines only the *configuration* side (`SLAProfile`, `SLAVersion`, `BusinessCalendar`). Without runtime records these FRs are unimplementable and `TST-SLA-001/002` cannot pass.

**Decision.** Two additive tables:

| Table | Fields | Purpose |
|---|---|---|
| `SLAState` | Id, TenantId, ObjectType, ObjectId, SLAVersionId (snapshot), StartedAtUtc, WarningAtUtc, TargetAtUtc, PausedTotalMinutes, PausedAtUtc, Status (ON_TRACK / AT_RISK / BREACHED / PAUSED), CurrentEscalationLevel, LastEvaluatedAtUtc | One live clock per object; the authoritative source for the SLA badge |
| `SLAEvent` | Id, TenantId, ObjectType, ObjectId, SLAStateId, EventType (STARTED / WARNING / PAUSED / RESUMED / OVERDUE / ESCALATED), Level, OccurredAtUtc, Reason, IdempotencyKey (unique) | Append-only SLA timeline; uniqueness of `IdempotencyKey` is what guarantees escalation fires once (BR-019) |

**Impact.** Two additive tables; the object timeline gains SLA events; FR-SLA-002 becomes verifiable.

**Confirm?** **Yes** — schema addition required to satisfy an existing FR.

---

## A-05 — Workflow runtime: no `WorkflowInstance` table (finding C-04)

**Issue.** FR-WF-002 specifies `POST /api/v1/workflow-instances/{id}/transition`, implying a runtime instance entity; Appendix C defines no such table.

**Decision.** Do **not** invent the entity. Keep the transition endpoint but bind it to the object:

`POST /api/v1/workflow-transitions` with body `{ objectType: "TASK"|"REQUEST", objectId, transitionCode, note? }`

The engine validates the transition against the object's pinned `WorkflowVersionId`, mutates the state, and records an append-only `AuditLog` row (`Action = WORKFLOW.TRANSITION`, before/after states in `BeforeJson`/`AfterJson`, guard result in `MetadataJson`). The object already carries its applied version and therefore *is* its runtime instance; a parallel instance table would duplicate state and create a consistency hazard.

**Impact.** Endpoint path differs from the §7 inline text (permitted by A-06); no entity added; FR-WF-002 "record transition" is satisfied. `ApprovalInstance` / `ApprovalStepInstance` are unaffected and still used exactly as specified.

**Confirm?** **Yes** — a deliberate API-shape deviation from one inline paragraph.

---

## A-06 — Which API reference wins when the SSS contradicts itself? (finding C-02)

**Issue.** Inline FR text and Appendix D disagree on several paths and verbs.

**Decision.** **Appendix D (Canonical API Catalog) is authoritative.** The SSS labels it canonical and it is the more complete, later enumeration; differing inline paths are treated as superseded.

**Impact.** A handful of endpoints follow Appendix D (e.g. `POST /api/v1/platform/companies/{id}/status`). All catalogue endpoints are implemented; none are dropped.

**Confirm?** **Yes** — it sets the API contract of record.

---

## A-07 — The AI endpoint catalogue is far smaller than the AI feature set (finding C-03)

**Issue.** Appendix D lists only **three** AI endpoints (`/ai/task-assistance`, `/ai/request-routing`, `/ai/actions/execute`), but the FR list requires ten distinct AI operations (FR-AI-001…010) and Appendix E enumerates twelve AI features. FR-AI-007 (`/ai/request-multi-intent`) and FR-AI-008 (`/requests/{id}/split`) are absent from Appendix D entirely, and FR-REQ-002's inline path (`/ai/requests/analyze`) differs from the catalogue's `/ai/request-routing`.

**Decision.** The FRs and Appendix E define the feature set; Appendix D names only the core three. Each required AI operation therefore gets its own endpoint under `/api/v1/ai/`, using the FR-specified path where one is given and a consistent sibling path where none is. Appendix D's three names are preserved exactly.

| Endpoint | Required by | Catalogue status |
|---|---|---|
| `POST /api/v1/ai/task-assistance` | FR-AI-001 | API-AI-01 (exact) |
| `POST /api/v1/ai/task-assignment-recommendation` | FR-AI-002 | added |
| `POST /api/v1/ai/task-parameters` | FR-AI-003 | added |
| `POST /api/v1/ai/task-breakdown` | FR-AI-004 | added |
| `POST /api/v1/ai/task-risk` | FR-AI-005 | added |
| `POST /api/v1/ai/task-summary` | FR-AI-006 | added |
| `POST /api/v1/ai/request-routing` | FR-REQ-002, FR-AI-009 | API-AI-02 (exact) |
| `POST /api/v1/ai/request-multi-intent` | FR-AI-007 | added |
| `POST /api/v1/requests/{id}/split` | FR-AI-008 | added |
| `POST /api/v1/ai/actions/execute` | FR-AI-010 | API-AI-03 (exact) |

**Impact.** Nine AI endpoints plus one request sub-resource, all inside the M12 contract, all behind the same guardrail pipeline, and all authenticated with the caller's own permissions.

*Alternative rejected:* collapsing every AI capability into `/ai/task-assistance` and `/ai/request-routing`. It would satisfy the catalogue but leave FR-AI-002…006 unaddressed as distinct capabilities and make per-feature audit, rate limiting and cost attribution impossible — Appendix E treats them as separate features with separate guardrails.

**Confirm?** **Yes** — API additions.

---

## A-08 — Which SLA warning rule applies? (finding C-09)

**Issue.** §11 Validation says "warning threshold less than target"; Appendix C says `0 <= warning <= target`.

**Decision.** Enforce the **stricter** rule: `0 <= warningMinutes < targetMinutes` with `targetMinutes > 0`. A configuration where warning equals target yields no usable warning window and is rejected with `422 SLA.INVALID_THRESHOLD`.

**Impact.** One validation rule; no business behaviour lost.

**Confirm?** **Yes** (trivial) — confirm, or tell me to allow equality.

---

## A-09 — Polymorphic attachment/comment targets (finding C-10)

**Issue.** `Attachment.ObjectType` allows TASK/REQUEST/COMMENT/RESULT/PROGRESS and `Comment.ObjectType` allows TASK/REQUEST/RESULT/PROGRESS, while FR-COL-001/002 name only "Task/Request".

**Decision.** Implement the **Appendix C superset** — evidence may hang off a progress report or a final result, which the workflows require in practice (FR-TASK-005/006/007 all attach evidence) — plus COMMENT so an attachment can belong to a comment. Integrity is enforced in the application layer because the reference is polymorphic by design.

**Impact.** Broader object-type enum; no requirement removed.

**Confirm?** **Yes** (minor) — confirm the superset is intended.

---

## A-10 — Permission catalog and system roles (§5, BR-002)

**Issue.** The RBAC matrix is action-oriented but has no exhaustive permission-code list, and system role definitions are not enumerated.

**Decision.** Seed a code catalog following Appendix C's `module.action.scope` pattern, derived from the FR permission fields: `tasks.create / assign / accept / execute / progress.update / progress-report / submit / confirm / reassign / cancel / from-request`, `requests.create / route / receive / process / resolve / confirm`, `users.create / update / deactivate`, `departments.create`, `roles.configure`, `service.create / routing.configure / workflow.configure / approval.configure`, `workflows.configure`, `sla.configure`, `comments.create`, `collaboration.002–004`, `ai.001–010`, `report.001–004`, `audit.read`, plus the remaining module actions named in the §5 matrix. Seed four system roles — `PLATFORM_ADMIN`, `COMPANY_ADMIN`, `MANAGER`, `EMPLOYEE` — flagged `IsSystem = true` and non-deletable. The SSS's own identifiers (`request.002`, `request.009`, `workflow.002/003/004`, `sla.002/003/004`) are registered as readable aliases mapped onto the same codes so SSS identifiers stay traceable.

**Impact.** Seeded reference data; custom tenant roles are subsets of this catalog (BR-002); the §5 matrix is enforceable exactly as written.

**Confirm?** No — derived directly from §5 and the FR permission fields.

---

## A-11 — Report metric definitions (GAP-026)

**Issue.** GAP-026 says core metrics are "defined at baseline" but gives no formulas, while §11 requires "well-defined metric formulas" for SLA reporting.

**Decision.** Pin these definitions as documented constants reused by dashboard, reports and exports:

| Metric | Definition |
|---|---|
| Open tasks | Status ∉ {COMPLETED, CANCELLED, REJECTED} |
| Overdue | Objects currently in OVERDUE (authoritative SLA state, never recomputed client-side) |
| At-risk | Active SLA with `WarningAtUtc <= now < TargetAtUtc` |
| Completion rate | COMPLETED ÷ (COMPLETED + CANCELLED) within range |
| Throughput | Objects reaching a terminal state per period |
| SLA compliance | Objects closed within target ÷ objects with an SLA, per period |
| Average resolution time | Mean (`ResolvedAt − SubmittedAt`) excluding paused minutes |
| Workload | Active tasks per user/department, excluding CANCELLED (FR-REP-003) |

**Impact.** Deterministic, reproducible numbers; FR-REP-004 "reproducible from stored records" becomes verifiable.

**Confirm?** No — but tell me if any definition must differ.

---

## A-12 — Password reset for employees without company email (FR-AUTH-004)

**Issue.** FR-AUTH-004 Alternative Flow says "Employee without email uses admin-issued reset" without specifying the mechanism.

**Decision.** Company Admin triggers an admin reset for a user, which generates a single-use temporary password, sets `MustChangePassword = true`, forces a change at next login, and writes an audit event. Email-based reset remains the primary path when an email exists.

**Impact.** One user flag, one admin action, one forced-change screen.

**Confirm?** No.

---

## A-13 — AI provider unavailable / no API key configured

**Issue.** The AI provider is an external key-bearing dependency (§23) and no key exists in this environment; §16.3 requires safe fallback.

**Decision.** `IAiModelClient` has two implementations: the OpenAI Responses client (used when a key is configured) and a deterministic **stub** used otherwise, returning schema-valid, clearly-marked fixture suggestions. Every AI feature stays demoable and testable offline, the manual-fallback path required by FR-REQ-002 / FR-AI-009 is genuinely exercised, and no secret is committed. Active mode is logged at startup and shown on the AI settings screen.

**Impact.** No functional loss; AI tests become deterministic.

**Confirm?** No — but supply a key in `.env` for real inference.

---

## A-14 — The Service entity has no approval-rule binding (finding C-13)

**Issue.** FR-SVC-004 requires attaching an approval rule to a service, but Appendix C's `Service` entity exposes only `ActiveWorkflowVersionId` and `ActiveSLAVersionId`, and `ApprovalRule` carries no `ServiceId`. Without a link, FR-SVC-004 cannot be implemented and the approval rule for a service transaction cannot be resolved.

**Decision.** Add one nullable FK to `Service`: `ActiveApprovalRuleVersionId → ApprovalRuleVersion`, mirroring exactly the existing workflow/SLA binding pattern (same lifecycle rule: the referenced version is immutable once active, so BR-009/BR-010 applies identically).

**Impact.** One additive column; no new table; the approval-resolution path for a new request/task becomes well-defined.

**Confirm?** **Yes** — schema change.

---

# PART B — Engineering Decisions Taken Locally

| # | Decision | Rationale |
|---|---|---|
| B-01 | Attachment allowlist: PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT/CSV, PNG/JPEG/GIF/WEBP, ZIP — max 500 MB | GAP-013 baseline; configurable via A-03 |
| B-02 | Email in development = log/no-op sink behind `IEmailSender` | §23 allows email failure; in-app notification remains the source of truth |
| B-03 | Redis used only for cache, rate-limit counters and AI budget counters — never transactional truth | §18 explicit |
| B-04 | Default business calendar: `Asia/Ho_Chi_Minh`, Mon–Fri 08:00–17:30; holidays seeded empty | §3.1 one configurable default; §33 defers exact values |
| B-05 | Task `Deadline` optional unless the bound workflow/SLA requires it | Appendix C: nullable + "must satisfy workflow/SLA rules" |
| B-06 | DRAFT objects visible only to their creator until submitted/assigned | Keeps drafts out of teammates' queues; consistent with "Create DRAFT or ASSIGNED" |
| B-07 | Cross-tenant and non-existent resources both return an identical `404` | §20.3: "do not reveal cross-tenant existence" |
| B-08 | Native PostgreSQL enums for the two canonical state sets (an invalid status cannot be stored at all); `VARCHAR` + check for extensible sets | Reinforces BR-018 and Appendix F |
| B-09 | Datetimes stored `TIMESTAMPTZ` in UTC, rendered in tenant timezone | §28.2 rule 49 |
| B-10 | Optimistic concurrency on Task, Request, WorkflowVersion, SLAVersion, ApprovalInstance, Attachment | GAP-025 |
| B-11 | Pagination: default 25, maximum 100 | §11 "maximum page size" |
| B-12 | Seed two tenants with distinct users/services/workflows, one published workflow per business type, SLA with warning + escalation | §3.2 permits simulated data; required for Demo D |
| B-13 | English UI copy in this build, i18n-ready string layer | SSS is English with a Vietnamese product name; §3.1 does not require runtime language switching |
| B-14 | OpenTelemetry exporting to console/OTLP in development | §18 "optional but recommended baseline" |

---

# PART C — Deliberately Out of Scope (confirmed exclusions, not gaps)

ERP functions · marketplace / B2B commerce · external customer-service portal · enterprise SSO · autonomous AI decisions bypassing approval or permission · Kubernetes / microservices · long-term AI memory / RAG · advanced natural-language search · advanced BI export · advanced multi-calendar support. Each is excluded by SSS §2.7, §25 (P2) or §28.1 — enumerated here so their absence is understood as a decision, not an oversight.

---

# Summary for Confirmation

**Ten Part A items need your decision before Phase 2 begins.** Recommended answers:

| ID | Recommended |
|---|---|
| A-01 | Optional `tenantKey` at login; resolve automatically, ask only when ambiguous |
| A-02 | `ManagementScope` table defaulting to the manager's department subtree, editable by Company Admin |
| A-03 | `TenantSetting` key/value policy table with the allowlisted keys above |
| A-04 | `SLAState` + `SLAEvent` runtime tables (required by FR-SLA-002/003/004) |
| A-05 | No `WorkflowInstance` table; transitions recorded in `AuditLog` |
| A-06 | Appendix D is the authoritative API contract |
| A-07 | Implement the ten FR-required AI endpoints (Appendix D names only three) |
| A-08 | Enforce `warning < target` strictly |
| A-09 | Implement the Appendix C superset of polymorphic object types |
| A-14 | Add nullable `Service.ActiveApprovalRuleVersionId` so FR-SVC-004 has a binding |

Every recommendation closes a documentation omission while preserving the specified business behaviour. Nothing in Part A alters a role, permission, workflow, state machine or business rule.
