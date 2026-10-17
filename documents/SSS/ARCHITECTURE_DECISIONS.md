# BizFlow Architecture and Product Decisions

| Field | Value |
|---|---|
| Version | 0.1-draft |
| Date | 2026-10-17 |
| Status | Combines explicit product decisions, source baseline decisions and proposals; review required |

## DEC-001 — Modular Monolith

- **Status:** CONFIRMED in source baseline.
- **Context:** Five-person project team, core requirements span tasks, requests, workflow, approval, SLA, AI, notifications and evidence.
- **Decision:** Keep one primary application deployment with module boundaries and explicit internal contracts.
- **Alternatives:** Microservices; unstructured monolith.
- **Rationale:** Avoid service distribution overhead now while retaining maintainable ownership boundaries.
- **Consequences:** No circular dependencies; module ownership and API contracts need review; independent scale/deploy of modules is not a current objective.
- **Revisit when:** Separate scaling/deployment/fault isolation is demonstrated as a real need from operating evidence.
- **Evidence:** SSS v1.2 sections 3, 17 and 28.

## DEC-002 — One primary workspace per company for MVP

- **Status:** CONFIRMED by explicit user decision (2026-10-16).
- **Context:** The user chose Option C to keep the initial product practical and demoable.
- **Decision:** Provision one primary workspace/tenant per approved company for MVP. Use departments, projects, role scopes and workflow configuration to organize work inside that environment.
- **Alternatives:** Multiple fully independent workspaces for each company from the start; one shared workspace with organizational subdivisions.
- **Rationale:** Aligns with SSS v1.2's “one primary tenant per company in MVP” baseline, avoids scope expansion before demo and focuses on core internal task/request workflows.
- **Consequences:** Do not promise multi-workspace behavior in MVP. The Company-vs-Tenant-vs-Workspace terms must remain distinct. Future upgrade may need new membership/entitlement semantics.
- **Revisit when:** Customer evidence shows independent data/config/access boundaries per branch/business unit are a meaningful paid need.
- **Evidence:** SSS v1.2; explicit user decision from conversation.

## DEC-003 — Future paid Multi-Workspace Upgrade

- **Status:** CONFIRMED as product direction; implementation design OPEN.
- **Context:** User explicitly stated that Option A should be available later through a paid enterprise upgrade.
- **Decision:** Classify multiple independent workspaces per company as Future Enterprise. It is not a requirement for the 2026-10-19 MVP/demo.
- **Alternatives:** Include full multi-workspace and subscription billing in MVP; exclude the capability from roadmap entirely.
- **Rationale:** Preserves the monetization direction without adding unvalidated billing/authorization/data-model complexity before the core product has been tested.
- **Consequences:** Price, tier names, workspace limits, entitlement checks, billing provider, failed payment behavior, cross-workspace user membership, per-workspace roles and configuration inheritance are all OPEN. Do not create payment APIs or paid-plan behavior now.
- **Revisit when:** Pilot customers request/validate multiple isolated workspaces and a commercial model is approved.

## DEC-004 — GRID design system

- **Status:** Accepted in `ADR-0001-design-system.md` dated 2026-09-29.
- **Decision:** Use the GRID design system and `--tp-*` tokens, desktop sidebar/mobile bottom-tab responsive navigation, status icon + text, focus visibility and a WCAG 2.1 AA contrast target.
- **Consequences:** No decorative imagery in the operational app; evidence images are contextual. Auth screens omit social login because the SSS baseline requires employee code/company email and password, not an external identity provider.
- **Caveat:** Actual token/asset integration must be checked in the target repository.

## DEC-005 — Tenant isolation is mandatory; DB strategy remains open

- **Status:** Requirement CONFIRMED; physical isolation strategy OPEN.
- **Context:** Multiple companies share one platform and company data must not cross boundaries.
- **Decision:** Enforce tenant/resource scope at backend for all operations, including jobs, files, cache, search, reports, notifications and AI context. Do not treat client tenant ID or globally unique object ID as sufficient.
- **Options for storage strategy:** shared database/shared schema; schema per tenant; database per tenant; hybrid. The source baseline does not evidence an approved strategy.
- **Proposed default for evaluation:** shared PostgreSQL database/shared schema with mandatory tenant ownership and systematic authorization may suit a student MVP; consider PostgreSQL RLS only as defense in depth after correct transaction/session scope and connection pooling design.
- **Consequences:** Must be demonstrated through negative integration tests; no strategy is considered “secure” by name alone.
- **Revisit when:** customer contracts, risk assessment, scale, regulatory or operational isolation requirements justify dedicated schemas/databases.

## DEC-006 — AI is a constrained assistant, not an authority

- **Status:** CONFIRMED in source baseline.
- **Decision:** AI works via structured output and whitelisted tools, validates entity references and calls normal application services. It cannot directly write database records or bypass permissions/workflows. Consequential actions require user confirmation by default unless an explicit approved policy authorizes otherwise.
- **Consequences:** Per-use-case authorization, schema validation, tenant scope, error fallback and audit metadata are mandatory.

## DEC-007 — Versioned workflow/approval/SLA definitions

- **Status:** CONFIRMED in source baseline.
- **Decision:** Published versions used by active transactions are not mutated in-place in ways that silently change running process semantics; changes create a new version for applicable future work.
- **Consequences:** Workflow instance must reference a version; version behavior should be tested.

## DEC-008 — Authentication implementation choice

- **Status:** Source baseline proposes ASP.NET Core Identity + JWT; implementation NOT VERIFIED.
- **Context:** Login supports employee code/password or company-provided email/password and requires valid company/tenant context.
- **Decision:** Preserve the behavior as a requirement, but do not assert actual JWT/refresh token behavior until the application manifests/config/auth code are inspected.
- **Open details:** token storage, lifetimes, refresh rotation, session revocation, login tenant resolution and exact password policy.

## DEC-009 — Redis usage

- **Status:** OPEN/NOT VERIFIED.
- **Decision:** Redis may be used only for an identified requirement. If tenant-derived data is cached, keys/invalidation must incorporate tenant and authorization/configuration scope.
- **Rationale:** Redis appears in intended stack but use and value are not established in code evidence.

## DEC-010 — Deployment topology

- **Status:** OPEN beyond local/demo intent.
- **Source direction:** Docker/Docker Compose, with optional cloud deployment, appears in source baseline.
- **Decision:** Do not claim a production cloud provider, Kubernetes topology or verified CI/CD until actual deployment files and approval are available.

## Decision log maintenance

A decision moves to accepted/confirmed only when explicit owner approval or an authoritative source provides evidence. A design that appears technically sensible remains `PROPOSED` until approved. When decisions conflict with source requirements, cite both and update the canonical source through change control rather than silently overwriting it.
