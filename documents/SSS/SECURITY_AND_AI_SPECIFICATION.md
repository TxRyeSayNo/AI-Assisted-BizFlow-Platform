# BizFlow Security and AI Specification

| Field | Value |
|---|---|
| Version | 0.1-draft |
| Date | 2026-10-17 |
| Source | SSS v1.2 sections 5, 16, 19, 27 and high-risk implementation areas; SRS v0.1 |
| Status | Target requirements; source-code enforcement and tests NOT VERIFIED |

## 1. Security objectives

1. Prevent one company/tenant from reading, modifying, exporting or otherwise influencing another tenant's protected business data.
2. Enforce the principle of least privilege for platform administration, company administration, managers, employees and AI-assisted actions.
3. Preserve business lifecycle integrity and an auditable history of consequential changes.
4. Protect attachment bytes and metadata with resource-level authorization.
5. Prevent model output or untrusted prompt content from changing application authority or bypassing workflow rules.
6. Avoid secrets and unnecessary sensitive content in logs and external AI requests.

## 2. Principal identities and scopes

| Principal | Authorized scope | Required restriction |
|---|---|---|
| Platform Administrator | Platform company/tenant lifecycle and explicitly assigned platform operations | Access to tenant business data must be explicitly policy-authorized and auditable; no implied unrestricted support access |
| Company Administrator | Organization and configuration within own company/tenant | Cannot grant platform privileges or create permissions outside the approved catalog |
| Manager | Work coordination, reassignment, approval and reports within allowed organizational scope | Must not act outside tenant, department, assignment or approval authority |
| Employee | Own/assigned work, authorized request participation, comments and evidence | Must not access unrelated objects or modify protected lifecycle state |
| AI Agent | Whitelisted application tools executed under the initiating user's current context | Has no independent authentication/authorization authority and cannot call data repositories directly |

The initial account for an approved company may be provisioned as Company Administrator per the source baseline. This does not prove the person's legal-representative status.

## 3. Backend enforcement requirements

Every protected request or tool execution shall validate, as applicable:

1. Authenticated identity and account status.
2. Permission for the requested action.
3. Trusted tenant context and membership.
4. Resource ownership/association with the tenant.
5. Resource- and actor-specific scope (e.g., manager's allowed assignees, assigned employee, authorized approver).
6. Current state and business preconditions.
7. Company/tenant lifecycle state (e.g., suspension restriction).

Frontend guards, hidden buttons, selected workspace labels and globally unique IDs are not sufficient controls.

## 4. Threat scenarios

| ID | Threat | Required mitigation | Verification approach |
|---|---|---|---|
| SEC-001 | Cross-tenant object access/IDOR | Resolve tenant from trusted authenticated context; authorize the resource on server | Attempt read/update/delete with IDs belonging to another tenant |
| SEC-002 | Tenant spoofing via request/body/query/header | Do not accept unvalidated client tenant identifiers as proof; check membership/context | Change tenant IDs, slugs and filters; expect deny/not-found semantics |
| SEC-003 | Role/permission escalation | Only authorized administrators update tenant roles; restrict grants to permission catalog and actor scope | Attempt to assign platform permission or expand own privileges |
| SEC-004 | State/workflow bypass | State transition service verifies source state, role, approval and workflow version | Submit arbitrary statuses via UI, REST and AI tool |
| SEC-005 | Cross-tenant file access | Fresh resource authorization for signed link and download; object key never treated as a credential | Reuse or manipulate object IDs/keys/signed URLs across tenant boundaries |
| SEC-006 | Prompt injection | Treat user content and retrieved files/comments as untrusted data; whitelist tools and schema arguments | Ask AI to ignore instructions, reveal other tenant data or invoke prohibited action |
| SEC-007 | Hallucinated resource/permission | Validate model-generated IDs and action fields against current authorized tenant data and policy | Provide nonexistent user/department/workflow IDs and forbidden enums |
| SEC-008 | Sensitive prompt/token leakage | Data minimization; redact secrets and unnecessary raw prompts from logs; secure API keys | Inspect logs, request payload construction and telemetry redaction |
| SEC-009 | Abuse/cost exhaustion | Rate limiting and usage controls for authentication/AI endpoints, once thresholds are approved | Load/abuse tests and per-user/tenant usage inspection |
| SEC-010 | Privileged administrator misuse | Least privilege, explicit admin routes/actions and audit records | Verify every high-impact action and audit authorization |
| SEC-011 | Attachment abuse | Type/size allowlist, safe object handling, optional malware-scanning hook decision | Test oversize, disallowed, malformed file and failed finalization |
| SEC-012 | Background-job tenant context leak | Store/validate tenant/resource references and re-authorize job purpose at execution time | Cross-tenant job payload mutation/replay tests |

## 5. AI data and execution policy

### 5.1 Allowed AI input

AI may receive only the data needed for the specific use case: current user's allowed task/request content, permitted history, organizational directory within authorized scope, available services/categories, and applicable workflow/approval/SLA configuration. Sending the whole tenant database or unrelated conversations is not justified by the baseline.

### 5.2 Untrusted output

Every model result is untrusted input. Validate:

- Schema and required fields.
- Enum values and dates/deadlines.
- User, department, service, workflow, SLA and object IDs.
- Tenant ownership and active status.
- Current caller permissions and target resource scope.
- State-transition, workflow and approval rules.
- Any user-confirmation policy that applies.

Invalid output must not become a persisted business mutation.

### 5.3 Tool execution

AI tools are explicitly registered, schema constrained and associated with required permissions. Tool handlers call the same application services used by ordinary user operations; they do not access database tables directly. The model cannot choose its own tenant, authority or permitted-tool catalog. Consequential actions remain subject to the source baseline's human confirmation/approval requirements.

### 5.4 Human review

| AI behavior | Default system treatment |
|---|---|
| Summarize, classify or draft | Return a suggestion/result; do not mutate business state unless an explicit use case authorizes persistence |
| Recommend assignee, deadline, priority, workflow/SLA or routing | Present suggestion; validate references and preserve user review/confirmation according to the applicable policy |
| Split complex task/request | Create proposed drafts/links only through authorized application operations and the defined confirmation path |
| Execute an action through a whitelisted tool | Re-check ordinary permission, tenant and business rules; require user confirmation by default for consequential changes unless explicit policy says otherwise |
| Approve a request, confirm final task/request, bypass an approval or widen access | Prohibited unless the existing business authorization model explicitly grants the initiating actor the corresponding action; AI itself is never the authority |

### 5.5 Provider and privacy boundary

- OpenAI is the intended external provider in the source baseline; exact model/version and runtime configuration are not verified.
- API secrets must be server-side and managed outside source control.
- Data sent to the provider must be minimized and scoped to the operation.
- Do not log API keys, passwords, refresh tokens or raw sensitive AI prompts by default.
- Document any business/customer data policy that governs external AI processing before real customer data is sent. No legal compliance claim is made here.
- Source baseline says no long-term memory/RAG in P0/P1; per-operation authorized context is the baseline.

### 5.6 Failure and fallback

| Failure | Required behavior |
|---|---|
| Provider timeout/unavailable | Return a safe recoverable error or manual fallback where available; do not assume recommendation/action succeeded |
| Invalid JSON/schema | Reject and safely retry/ask user to revise only within a defined retry policy; do not persist invalid result |
| Low confidence/missing information | Route to manual review or ask user for missing information according to the use case |
| Invented/unauthorized entity ID | Reject reference and do not execute a tool |
| Unauthorized tool request | Deny via normal backend authorization and record appropriate security/audit event |
| Client times out after AI action request | Use idempotency/state inspection so re-submission does not duplicate a mutation |

Exact timeout seconds, retry count, model name, quotas, allowed prompt retention and numeric AI-quality threshold remain OPEN.

## 6. Minimum security and AI test set

- Employee cannot read another tenant's Task, Request, Department, Workflow, Approval, Attachment, Notification or report.
- Company Administrator cannot perform platform-only registration approval or modify another tenant's role assignments.
- A manager cannot assign work to unauthorized user/department or approve a step outside their permission.
- Altering tenant ID, object ID, file path/key, query filter or AI input does not cross isolation boundaries.
- AI output with forged permission, invented ID, invalid state transition or prompt injection is rejected by backend rules.
- AI can fail without bypassing mandatory manual routing/approval or leaving the UI claiming a successful change.
- Uploaded object cannot be retrieved from another tenant; malformed/oversized upload is rejected.
- Sensitive credentials and prompt content are absent from standard logs; audit and technical log read access is controlled.
- Retried SLA/AI/tool operations do not create duplicates where one logical event is expected.

All tests above are **required/proposed verification**, not evidence of execution or passing.
