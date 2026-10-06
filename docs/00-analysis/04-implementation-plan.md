# AI-Assisted BizFlow Platform — Implementation Plan

Aligned with SSS §24 (Dependency Map), §25 (Implementation Priority), §17 (Architecture), §27 (Testing) and §28 (Implementation Contract).

---

## 0. Delivery Principles

1. **Vertical slices, not horizontal layers.** Each phase ends with something a person can log into and use, not a half-built layer.
2. **P0 before P1 before P2** (§25). P0 = auth, tenant isolation, organization, service catalog, task core lifecycle, request core lifecycle, request→task, workflow basics, audit, attachments, basic notifications.
3. **Business rules and tests land together.** §28.2 rule 53: automated tests are written before changing core state/permission rules.
4. **Nothing is "done" without its four UI states** (loading, empty, error, permission-denied) and its server-side authorization check.
5. **No scope invention.** Any deviation is raised, not decided silently (§28 stop-and-ask boundary).

---

## 1. Phase Plan

### Phase 1 — Understand ✅ (this document set)

**Deliverable:** `docs/00-analysis/01…06`.

**Exit criteria verified:** all 62 FRs enumerated and accounted for; both state machines read as authoritative; 22 business rules mapped to enforcement points; 12 SSS-internal inconsistencies identified with a resolution decision each.

---

### Phase 2 — Architecture

| Deliverable | Detail |
|---|---|
| Solution skeleton | `BizFlow.Api`, `.Application`, `.Domain`, `.Infrastructure`, `tests/*` |
| Local infrastructure | `deploy/docker-compose.yml` (PostgreSQL 18, MinIO, Redis 8), `.env.example` |
| Persistence baseline | `BizFlowDbContext` with tenant global query filter, soft-delete filters, concurrency tokens |
| Domain baseline | Entities + value objects + enums from Appendix C, plus the four gap-resolving tables |
| Cross-cutting | `ITenantContext`, `IResourceAuthorizer`, `IWorkflowEngine`, `ISlaService`, `IAuditWriter`, `INotificationDispatcher`, `IUnitOfWork` |
| API conventions | Error envelope, pagination, idempotency, ETag, OpenAPI, correlation IDs, Serilog + OpenTelemetry |
| ADR log | `docs/01-decisions/` — one short ADR per High finding C-05…C-08 |
| Seed | Two demo tenants per §4.4 of the architecture report |

**Exit criteria:** `dotnet build` green; migrations applied to a containerised PostgreSQL 18; `/health` and Swagger reachable; seed data loads idempotently.

---

### Phase 3 — UX Foundation

| Deliverable | Detail |
|---|---|
| Design system | Tokens (colour, type, spacing, radius, elevation, motion) implemented once in Tailwind config + Angular Material theme |
| Shells | `AppShell` (tenant sidebar) and `PlatformShell` (top-nav console) |
| Shared components | The component catalogue in `03-uiux-design-proposal.md` §7, built one by one with their four states |
| Routing | Full route tree with lazy-loaded feature modules and role/permission guards |

**Exit criteria:** both shells render with real navigation, role-based menu filtering works from a mocked session, component gallery page demonstrates every shared component in all states, mobile drawer + bottom bar verified at 375px.

---

### Phase 4 — Core Platform (P0, foundation)

| Workstream | Requirements | Notes |
|---|---|---|
| Identity & auth | FR-AUTH-001…004 | Dual-identifier login with tenant resolution (decision A-01); JWT + rotating refresh; reset flow; lockout |
| Tenant & company | FR-TEN-001…004 | Registration → approval → provisioning with initial Company Admin; status lifecycle; **isolation enforced and integration-tested here, not later** |
| Organization | FR-ORG-001…005 | Users, departments, roles, permission catalog, manager scope |
| Audit | M13 | `IAuditWriter` wired into every mutation path from the first use case onward |
| Notifications (foundation) | — | Dispatcher + idempotency substrate; event handlers added per feature |
| Frontend | UI-01, UI-02, platform console | Login, platform dashboard, company/tenant registry |

**Exit criteria:** `TST-SEC-001` (cross-tenant access blocked) passes; login works by employee code **and** email with correct tenant context; a Platform Admin can approve a registration and provision a working tenant end-to-end through the UI; audit rows exist for tenant lifecycle actions.

---

### Phase 5 — Core Business Workflows (P0)

| Workstream | Requirements | UI |
|---|---|---|
| Service catalogue | FR-SVC-001…004 (routing rules active; workflow/approval binding stubs resolve once M07 lands) | `/settings/services` |
| Workflow versioning | FR-WF-001, FR-WF-002 (engine + immutability) | UI-11 |
| Task lifecycle | FR-TASK-001…010 | UI-04, UI-05, `/work` |
| Request lifecycle | FR-REQ-001, 003…009 (excluding AI-driven 002) | UI-07, UI-08, UI-10 |
| Request → Task | FR-REQ-006 | request timeline |
| Collaboration | FR-COL-001…004 | comments, timeline, search/filter |
| Evidence | FR-COL-002 | 500 MB signed direct upload + finalize + orphan cleanup |
| Notifications | §22 event set for task/request lifecycle | UI-14, SignalR |

**Exit criteria:** the full task lifecycle and request lifecycle run end-to-end through the UI; state transitions are impossible outside the workflow engine; `TST-TASK-001…004`, `TST-REQ-001…003`, `TST-REQ-008`, `TST-WF-001`, `TST-FILE-001/002` pass; overdue/cancel/reject reasons are mandatory and audited.

---

### Phase 6 — P1 Capabilities

| Workstream | Requirements | Notes |
|---|---|---|
| Approval | FR-WF-003, FR-WF-004 | Sequential + parallel rule versions; append-only decisions; UI-12 |
| SLA & escalation | FR-SLA-001…004 | Profile/version configuration, business calendar, Hangfire evaluation, warning/overdue/escalation with idempotency, pause on `WAITING_FOR_INFORMATION`; UI-13 |
| Dashboards & reports | FR-REP-001…004 | Manager dashboard, company report, workload, SLA performance; UI-16 |
| Request revision | FR-REQ-009 | Immutable original + `RevisedFromRequestId`; `/requests/:id/revise` |
| AI — tasks | FR-AI-001…006 | Assistance, assignment, parameters, breakdown, risk, summary; UI-06 |
| AI — requests | FR-REQ-002, FR-AI-007, FR-AI-008, FR-AI-009 | Analysis, multi-intent, split, routing; UI-09 |
| AI — execution | FR-AI-010 | Tool registry, authorization, confirmation, `AIAgentAction` audit |

**Exit criteria:** `TST-SLA-001/002`, `TST-WF-004`, `TST-AI-003/006/008/009` pass; AI provider outage degrades to manual flow with a visible message and no data loss; escalations fire exactly once per level under repeated scheduler runs.

---

### Phase 7 — UI Refinement

Layout, spacing, typography, responsive behaviour, all four data states audited on every screen, accessibility pass (contrast, focus order, keyboard operability, `aria-live`), consistency audit of status vocabulary, and empty-state copy that tells the user the next useful action.

---

### Phase 8 — Testing

Unit (domain state machines, SLA working-time maths, permission policies, revision logic, AI schema validation) · integration (API + DB, tenant isolation, workflow/approval, file finalization, background jobs) · AI fixtures (deterministic classification/routing/splitting, low-confidence fallback, tool authorization, prompt-injection resistance) · E2E Playwright (Demo A and Demo B). Every §27 test ID is implemented and green.

---

### Phase 9 — Final Audit

Re-walk the SSS against the build: 62 FRs, 22 business rules, 5 actors, 16 screens, 89 endpoints (76 catalogue + 13 FR-required additions), 42 tables, §27 test IDs and the five demo scenarios (A–E). Produce `docs/02-traceability/final-audit.md` listing anything unimplemented, changed or justified.

---

## 2. Dependency Order (§24, honoured literally)

```
Auth ──► Organization ──► Service ──► Workflow ──► Task ──► Request ──► Request→Task
                │                                     │          │
                └──────────────► Approval ◄───────────┘          │
                                 │                               │
                                 └──► SLA ◄──────────────────────┘
                                        │
                     Notification ◄─────┴─────► Attachments
                                        │
                             Dashboard ◄┴──► Reporting
                                        │
                      AI Task / AI Request (require Task + Request + Org + Service)
                                        │
                                    AI Execution (requires all target modules)
```
Audit is cross-cutting and is implemented in Phase 4, before the modules it observes — not bolted on at the end (§24: "design before implementation").

---

## 3. Milestones (demoable checkpoints)

| # | Milestone | Demonstrates |
|---|---|---|
| M1 | Login + tenant provisioning + isolation | Demo D |
| M2 | Task lifecycle without AI | core task flow |
| M3 | Request lifecycle + request→task | core request flow |
| M4 | Approval + SLA/escalation | Demo E |
| M5 | AI task assistance + AI request analysis/split | Demo A, Demo B |
| M6 | Revision, dashboards, reports, refined UI | Demo C, full product |

---

## 4. Risk Register (SSS §3.3 → concrete mitigations)

| Risk | Mitigation in this plan |
|---|---|
| Scope sprawl | P0/P1/P2 gates; P2 (NL search, RAG, advanced BI, advanced calendars) explicitly deferred and never allowed to alter the core model |
| AI hallucination | Structured outputs + entity/enum lookup gate + confidence + human review + manual fallback, all enforced in Phase 6 |
| Tenant data leakage | Global query filter + single authorization path + `TST-SEC-001` as a Phase-4 exit criterion |
| 500 MB upload failure | Signed direct-to-storage upload with multipart; the API never proxies the binary |
| Config change breaking live transactions | Immutable published versions with snapshot references on the object |
| Integration complexity | Modular monolith with explicit module contracts and one API convention set |
| AI provider outage / cost | `IAiModelClient` abstraction, timeouts, backoff, per-tenant budget guard, manual fallback |

---

## 5. Explicitly Out of Scope for this Build

ERP functions, marketplace, external customer portal, enterprise SSO, autonomous AI decisions, Kubernetes/microservices, and all P2 enhancements (NL search, long-term AI memory/RAG, advanced calendars, BI export) — each excluded by §2.7 / §25(P2) / §28.1, not by preference.

---

## 6. Definition of Done (per feature)

A feature is complete only when all of the following hold: the FR's main flow **and** its exception flows are implemented; business rules are enforced server-side; authorization is enforced server-side (not merely by hiding UI); tenant scope is asserted; audit is written at the mutation boundary; validation is mirrored in the form; loading/empty/error/permission-denied states exist; it is responsive; no secret is hard-coded; unit + integration tests pass; and the feature is reachable through navigation (integration completeness).
