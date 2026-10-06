# AI-Assisted BizFlow Platform — UI/UX Design Proposal

Derived from SSS §15 (Frontend Specification), §15.1 (Form Standards), §22 (Notifications), §5 (Permissions), §7 (FRs) and §8–9 (Workflows & States).

---

## 1. Design Pattern Decision

| Decision | Choice |
|---|---|
| Product category | Multi-tenant enterprise workflow & request orchestration |
| UI pattern | **Workflow-oriented enterprise management UI** = decision-first work inbox + operational dashboards + a separate configuration console |
| Landing strategy | **Role-adaptive home**, not a single generic dashboard |
| Navigation model | Left sidebar (workspace) / top-nav console (platform plane), driven by workflow not entities |
| Density | Information-dense but breathable: table-centric lists, 8px spacing scale, one accent colour |
| Scope of visual language | Supplied by the selected design system; structure below is design-agnostic |

### Why this pattern

1. **The dominant question is "what needs me now?"** — the SSS's value is ownership, deadlines and traceability (§2.2, §2.6). A generic KPI dashboard answers "how is the company doing", which is the *secondary* question. So the primary surface is an actionable queue and the dashboards sit behind it.
2. **Two distinct planes exist and must not be merged.** Platform Admin operates *above* tenants (FR-TEN-002/003); Company Admin/Manager/Employee operate *inside* one tenant. Merging them into one menu would imply the platform admin can see tenant work content, which contradicts the actor model. They get separate navigation shells.
3. **Configuration is rare, transactional work is constant.** Services, workflows, SLA profiles, roles and calendars belong in a clearly-labelled Settings area, deliberately not in the main nav — otherwise daily users wade through administrator furniture.
4. **Status is the primary object of attention.** Both canonical state machines have 11–13 states, several of which mean "someone must act". Status must therefore be visible in every list row and every detail header, using identical vocabulary everywhere.
5. **AI is a proposal, not a state.** `AI_ANALYZED` is explicitly not a Request state (§9.2), so AI must never appear as a status chip. It appears as an annotation and an editable panel.

---

## 2. Role-Based Information Hierarchy

| Role | First question | Primary surface | Secondary |
|---|---|---|---|
| A04 Employee | What do I have to do, and is anything late? | **My Work** inbox: needs-my-action, deadlines | My requests, notifications, request confirmation |
| A03 Manager | What is at risk, and what needs my decision? | **Manager Home**: decision queue + risk table | Team tasks, approvals, routing, workload report |
| A02 Company Admin | Is the tenant configured and healthy? | **Company Home**: setup completeness + operational KPIs | Config console, company report, audit |
| A01 Platform Admin | Any pending onboarding or unhealthy tenant? | **Platform Home**: registration queue + tenant health | Tenant registry, platform audit, AI settings |

**Component budget per role is deliberately unequal.** Employees get queues and lists; managers get queues + charts; admins get tables + configuration. Nothing shows analytics to someone who cannot act on it.

---

## 3. Navigation Structure

### 3.1 Platform plane (A01 only) — top-nav console, no sidebar

| Item | Route | Purpose |
|---|---|---|
| Home | `/platform` | Pending registrations queue, tenant health summary |
| Companies | `/platform/companies` | Registration list, approve/reject, status change |
| Tenants | `/platform/tenants` | Provisioned workspaces, suspend/activate |
| Platform AI settings | `/platform/ai` | Global AI enablement + provider configuration status |
| Audit | `/platform/audit` | Platform-level audit events |

### 3.2 Tenant workspace (A02–A04) — collapsible left sidebar

| # | Nav item | Route | Employee | Manager | Company Admin |
|---|---|---|---|---|---|
| 1 | Home | `/home` | ✓ | ✓ | ✓ |
| 2 | My Work | `/work` | ✓ | ✓ | – |
| 3 | Tasks | `/tasks` | – | ✓ | ✓ (read) |
| 4 | Requests | `/requests` | ✓ | ✓ | ✓ (read) |
| 5 | Approvals | `/approvals` | – | ✓ | ✓ (config) |
| 6 | SLA Monitor | `/sla-monitor` | – | ✓ | ✓ |
| 7 | Reports | `/reports` | ✓ (own/allowed) | ✓ | ✓ |
| 8 | Audit | `/audit` | – | ✓ (allowed) | ✓ |
| 9 | Settings | `/settings` | – | – | ✓ |

**Top bar (always):** tenant/workspace name · global search · notification bell with unread count → `/notifications` · user menu (profile, password, sign out).

**Deliberate exclusions from navigation:** Services, Workflow, SLA Profiles, Roles, Calendar and AI policy are reachable only through **Settings**, because FR-SVC/FR-WF/FR-SLA configuration is a Company Admin task performed occasionally, and exposing them at top level would misrepresent the product to its daily users.

Badge counts (unread notifications, pending approvals, items awaiting my decision) appear on nav items — the nav itself becomes a work signal.

---

## 4. Main Screens (16 canonical + supporting)

| SSS ID | Screen | Route | Plane | Primary components |
|---|---|---|---|---|
| UI-01 | Login | `/login` | public | Login-mode toggle (employee code / email), optional company selector, error region, tenant-context feedback |
| UI-02 | Platform Dashboard | `/platform` | platform | Metric cards, registration queue table, tenant health table, filters, status actions |
| UI-03 | Company Setup | `/settings` + sub-routes | tenant | Tabbed config: Organization · Services · Workflows · Approval Rules · SLA · Calendar · AI Policy |
| UI-04 | Task List | `/tasks` | tenant | Data table, filter bar, status chips, SLA badges, pagination, saved views |
| UI-05 | Task Detail | `/tasks/:id` | tenant | Header status + actions, checklist, progress, evidence, result, confirmation, timeline, comments |
| UI-06 | AI Task Assistant | `/tasks/new` (panel) + `/tasks/:id` | tenant | Prompt input, structured suggestion cards, accept/override, missing-info prompts, confidence |
| UI-07 | Request List | `/requests` | tenant | Data table, filter bar, SLA indicators, status chips |
| UI-08 | Request Create | `/requests/new` | tenant | Wizard: service/category → content → attachments → review |
| UI-09 | AI Request Analysis | inside `/requests/new` and `/requests/:id` | tenant | Intent cards, missing-info prompts, routing recommendation, split preview, confidence |
| UI-10 | Request Detail | `/requests/:id` | tenant | Timeline, routing history, child tasks, resolution, confirmation, comments, revision link |
| UI-11 | Workflow Designer | `/settings/workflows/:id/versions/:v` | tenant | Step list editor, transition matrix editor, validation panel, publish action |
| UI-12 | Approval Center | `/approvals`, `/approvals/:id` | tenant | Queue with badge, detail, approve/reject with mandatory reason, append-only decision history |
| UI-13 | SLA Monitor | `/sla-monitor` | tenant | Metric cards, at-risk/overdue tables, filters by service/department/owner, drill-down |
| UI-14 | Notifications | `/notifications` | tenant + platform | List, unread filter, mark read, deep link to object |
| UI-15 | Audit Viewer | `/audit` (and `/platform/audit`) | both | Filterable audit table (actor, action, object, date), before/after detail drawer |
| UI-16 | Reports | `/reports` (+ `/workload`, `/sla`, `/company`) | tenant | Date-range controls, charts, tables, export |

**Supporting screens required by the FRs but not enumerated in §15:** `/home` (role-adaptive), `/work` (My Work inbox), `/settings/organization/{users,departments,roles}`, `/settings/services/:id` (routing + workflow + approval + SLA binding), `/settings/sla-profiles/:id`, `/settings/calendar`, `/settings/ai-policy`, `/profile`, and `/requests/:id/revise` (FR-REQ-009). These are implied by FR-ORG-001…005, FR-SVC-001…004, FR-SLA-001, FR-REQ-009 and are implemented as real screens, not modal stubs.

---

## 5. Dashboard Structure

### 5.1 Manager Home (A03) — decision-first

```
┌ Decision queue ───────────────────────────────────────────────┐
│ [ 4 results awaiting my confirmation ] [ 2 approvals pending ] │
│ [ 3 requests to route ]                                        │
└────────────────────────────────────────────────────────────────┘
┌ KPI row ───────────────────────────────────────────────────────┐
│ Open in scope │ Overdue │ At risk (warning) │ Throughput │ SLA │
└────────────────────────────────────────────────────────────────┘
┌ Workload by employee (bar) ┐ ┌ SLA compliance (gauge)  ┐
└────────────────────────────┘ └─────────────────────────┘
┌ At-risk & overdue table ───────────────────────────────────────┐
│ Item │ Owner │ Deadline │ SLA remaining │ Status │ Action      │
└────────────────────────────────────────────────────────────────┘
```
The decision queue is the first thing on the page and every count is a link to the filtered list. "At risk" rows carry an advisory AI risk reason where FR-AI-005 has produced one, visually marked as advice and never replacing the authoritative SLA state (BR-021).

### 5.2 Employee Home (A04) — action-only

Needs-my-action list (assignment to accept, work in progress, information requested of me, resolutions awaiting my confirmation) · sorted upcoming deadlines · my recent requests with status · recent notifications. **No charts** — an employee cannot act on aggregate analytics, so showing them would be noise.

### 5.3 Company Admin Home (A02)

Configuration completeness checklist (users? departments? services? published workflow? SLA? calendar?) linking into Settings · tenant KPIs (active users, open tasks/requests, overdue, SLA compliance) · recent security-relevant audit events · pending configuration gaps. The checklist converts a configuration product into an actionable one.

### 5.4 Platform Admin Home (A01)

Pending company registrations queue (approve/reject with reason) · tenant health table (status, users, activity, last change) · platform counters. No tenant business content is visible from here.

---

## 6. Core Workflows — Screen-by-Screen UX

### 6.1 Create & assign a task (with AI) — Demo A

| Step | Screen | What the user sees | Feedback / next action |
|---|---|---|---|
| 1 | `/tasks/new` | Plain-language textarea + structured form, "Ask AI" affordance | — |
| 2 | same | AI panel: proposed title, description, priority, deadline, department/assignee, checklist, workflow, SLA — each editable; confidence; "context used" disclosure | Loading skeleton during the call; provider failure → inline fallback message, form remains fully usable manually (NFR-PERF-002) |
| 3 | same | Missing-information prompts highlighted inline | Cannot submit until required fields satisfied |
| 4 | same | Manager overrides any suggestion | Overrides recorded against the recommendation |
| 5 | Save | Validation errors per field | Success toast + navigate to `/tasks/:id` |
| 6 | `/tasks/:id` | Assign panel (user or department), note | Assignee notified |
| 7 | Employee `/work` | "Needs your action" card with accept / reject | Reject opens reason dialog |

### 6.2 Employee request → routing → resolution — Demo B

`/requests/new` wizard (service → content → attachments → review) with AI analysis available at review · intent cards when several intents are detected, each selectable for splitting · child-request drafts created only after explicit confirmation and each shows its parent link · `/requests/:id` gives timeline, routing history, child tasks, resolution and requester confirmation · low-confidence analysis falls back to manual routing with a visible explanation, never a silent guess.

### 6.3 Rejected request revision — Demo C

`/requests/:id` on a REJECTED request shows the rejection reason and an immutable-history notice, plus **Create Revised Request** → `/requests/:id/revise` prefilled with copied safe fields and selectable attachments → new DRAFT with a visible "revised from #<id>" badge. The UI makes the immutability of the original explicit; there is no "reopen" affordance anywhere.

### 6.4 Approval — UI-12

Queue ordered by waiting time, showing object, requester, step, and SLA. Decision requires a reason on reject. Prior decisions render as an append-only list with actor and timestamp; no edit affordance exists (BR-011).

### 6.5 SLA escalation — Demo E

`/sla-monitor` separates On track / At risk / Breached. Warning and escalation events are visible in the object timeline with a single authoritative event per level, demonstrating idempotency (FR-SLA-003).

---

## 7. Component Strategy

A single shared design-system layer in `/src/app/shared`; no screen styles its own primitives.

| Group | Components |
|---|---|
| Layout | `AppShell` (sidebar + topbar), `PlatformShell`, `PageHeader`, `ContentCard`, `SplitLayout` (detail + timeline rail), `SectionTabs` |
| Data | `DataTable` (sorting, pagination, sticky header, responsive card-fallback), `FilterBar`, `StatCard`, `MetricDelta`, `SlaBadge`, `StatusChip`, `PriorityBadge`, `ConfidenceMeter`, `AvatarName`, `EntityPicker` |
| Timeline | `ActivityTimeline`, `EventRow`, `CommentThread`, `AttachmentList`, `AttachmentUploader` |
| AI | `AiProposalPanel`, `AiSuggestionCard`, `AiMissingInfoList`, `AiConfidenceNote`, `AiIntentCards`, `AiContextDisclosure` |
| Forms | `FormField`, `FormSection`, `ReasonDialog` (mandatory justification), `ConfirmDialog`, `DateField` (tenant timezone), `RichTextField`, `Wizard` |
| Feedback | `SkeletonLoader`, `EmptyState`, `ErrorState`, `InlineAlert`, `ToastService`, `PermissionDeniedState` |

**Four states are mandatory for every data surface:** loading (skeleton), empty (with the next useful action), error (retryable where sensible), and permission-denied (explains *that* access is denied without revealing whether the record exists — matching the non-revealing 404 policy).

**Destructive/irreversible actions** (cancel task, reject assignment, reject request, reassign, deactivate user, reject result, suspend tenant) always route through `ReasonDialog`, mirroring the server rule so the user is never surprised by a 422.

---

## 8. Status, SLA and Priority Presentation

One vocabulary, used identically in lists, headers, timelines and emails:

| Semantic | Used for | Presentation |
|---|---|---|
| Neutral | DRAFT, CANCELLED, CLOSED | Grey chip, plus a struck/quiet treatment for CANCELLED |
| Informational | ASSIGNED, ACCEPTED, SUBMITTED, ROUTED, RECEIVED | Accent-tinted chip |
| Active work | IN_PROGRESS | Accent-filled chip with a progress affordance |
| Needs someone else | SUBMITTED, RESOLVED, WAITING_FOR_INFORMATION, PENDING approval | Warning chip; WAITING_FOR_INFORMATION carries a clock icon and shows the SLA is paused when policy says so |
| Positive terminal | CONFIRMED, COMPLETED | Success chip |
| Negative | REJECTED | Error chip |
| System-detected breach | OVERDUE | Error chip + distinct icon, always paired with the SLA badge; UI offers no way to set it (BR-018) |

**Rule:** every chip is icon + text, never colour alone (accessibility), and the same state name is used in the API, the UI and notification copy — no synonyms.

**SLA badge** shows remaining/elapsed working time in tenant timezone with a three-state ramp (on track / at risk / breached) and the pause indicator when a request is `WAITING_FOR_INFORMATION` and the tenant policy pauses the clock (FR-SLA-004). It always reads from the authoritative SLA record, never from a client calculation.

---

## 9. Responsive Strategy

| Breakpoint | Behaviour |
|---|---|
| ≥1280px | Full sidebar, two-column detail (content + timeline rail), full tables |
| 1024–1279px | Icon-rail sidebar (labels on hover), single-column detail, tables keep priority columns |
| 768–1023px | Sidebar becomes a drawer; bottom bar for Home / My Work / Requests / Approvals / More; tables switch to card lists |
| <768px | Single column, bottom bar retained; the four action-heavy flows (accept, progress, submit result, confirm resolution, comment, upload) are fully usable on a phone |

**Explicit decision:** the **Workflow Designer (UI-11)** and the heaviest Settings screens are desktop-optimised. On small screens they render read-only with a clear notice. Reason: authoring a versioned workflow graph through a phone is a usability lie, and the roles that own it (A02) work on desktop. This is a documented, deliberate scope choice within the "responsive" requirement, not an omission — every workflow that an *employee or manager* performs on a phone is fully functional.

Touch targets ≥ 44×44 px; no horizontal overflow at any width.

---

## 10. Accessibility

WCAG 2.1 AA as the working bar: 4.5:1 text contrast, visible keyboard focus, full keyboard operability of tables/forms/dialogs, labelled inputs with associated error text, `aria-live` for toasts and AI processing state, status conveyed by icon+text, dialogs trapping focus and restoring it on close, `prefers-reduced-motion` respected, and no colour-only meaning anywhere.

---

## 11. Visual Design Direction

Structure, navigation, hierarchy, component behaviour and interaction patterns above are settled. The **visual language** (palette, type scale, elevation, radii, motion) is taken from the design system selected with the user, applied through the shared component layer so that it lands consistently across all 16 screens.

**Non-negotiable constraints regardless of the chosen theme:**
- No purple/blue decorative gradients; no gradient-as-decoration.
- Icons for all UI functions — never emoji as a button, nav item, badge or empty-state marker.
- Restrained elevation and radius; no oversized headings, no decorative illustration in work surfaces.
- One accent colour plus a semantic set (success, warning, error, info); status colour always paired with an icon and a label.
- Typography optimised for dense tabular reading: a legible UI sans with tabular numerals for tables, dates and counts.
