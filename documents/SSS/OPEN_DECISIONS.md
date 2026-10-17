# BizFlow Open Decisions Register

| Field | Value |
|---|---|
| Version | 0.1-draft |
| Date | 2026-10-17 |
| Status | Open register; entries are not resolved without evidence of an actual decision |

Priority meaning: **Blocker** prevents trustworthy code-level/current-state assessment; **High** must be resolved before the affected implementation or demo acceptance; **Medium** needed for design/operations but does not block source-derived SSS drafting; **Deferred** explicitly belongs after MVP.

| ID | Priority | Question / decision needed | Why it matters | Recommendation/default | Owner | Status |
|---|---|---|---|---|---|---|
| OQ-001 | Blocker | What is the exact BizFlow GitHub repository URL and branch? | Accessible `ThangHoQuoc/f` is Django and does not match the Angular/.NET target; code, branch and tests cannot be verified. | Provide the actual repository or an up-to-date source archive. | Product owner/team | OPEN |
| OQ-002 | High | Is `AI-Assisted_BizFlow_Software_System_Specification_v1.2_FINAL` still the active baseline after the latest product decision? | Prevents requirements drift and accidental overwriting of an authoritative spec. | Treat v1.2 as current source snapshot until owner confirms a superseding decision/change. | Product owner | OPEN |
| OQ-003 | High | What is the exact mandatory demo acceptance checklist for 2026-10-19? | Need to choose testable core flows under tight time and avoid scope expansion. | Demo one approved company/workspace, member setup, task flow, request flow and essential permission boundary; owner approval needed. | Product owner/team | OPEN |
| OQ-004 | High | Which tenant/storage isolation strategy is approved for actual implementation? | Shared schema/schema-per-tenant/database-per-tenant affects data architecture and operational tests. | Assess shared DB/shared schema for MVP if risk and customer requirements allow; record a formal decision and test defenses. | Architect/product owner | OPEN |
| OQ-005 | Medium | What evidence is required before an initial Company Administrator is considered verified/authorized? | Registration approver and legal representative are different concepts. | For demo, record manual review status; do not claim legal verification without a process. | Product owner | OPEN |
| OQ-006 | Medium | What are the data retention, deletion, export and audit-retention rules? | Impacts privacy, audit integrity, data lifecycle and future customer commitments. | Preserve source-baseline history; no retention durations or legal-erasure rules should be invented. | Product owner/legal advisor as applicable | OPEN |
| OQ-007 | Medium | What workload and quality targets should be used (response time, concurrency, throughput, uptime, RPO/RTO)? | No performance/reliability claims can be verified without target conditions. | Measure a representative baseline after expected use is described. | Product owner/operations | OPEN |
| OQ-008 | Medium | Which OpenAI model/package version, request timeout/retry, rate limit and budget policy are configured? | Determines deployment configuration, resilience and AI costs. | Keep model configurable; choose exact values from current provider support and test needs. | Backend/AI owner | OPEN |
| OQ-009 | Medium | Is Redis actually used for a required capability in current code? | Avoid adding a dependency without business need; tenant-aware caching adds complexity. | Treat as optional until repository evidence demonstrates use. | Backend owner | OPEN |
| OQ-010 | Medium | Which email provider and delivery/retry policy are used? | Invitations, resets and notifications may depend on delivery. | Identify provider and define safe failure behavior before enabling email-dependent features. | Backend/operations owner | OPEN |
| OQ-011 | Medium | What is the canonical entity/data dictionary, and which entity explains the 38-vs-39 discrepancy in SSS? | Avoid making RDS inconsistent with source. | Resolve from the authoritative data model, not by guess. | Data architect/BA | OPEN |
| OQ-012 | Deferred | Can users belong to multiple independent workspaces under the same company? Are roles/department/config independent per workspace? | Required for future enterprise semantics, not current MVP. | Do not implement until paid upgrade requirements are reviewed with pilot customers. | Product owner | DEFERRED |
| OQ-013 | Deferred | What are the enterprise tier names, prices, quotas, payment provider and failed-payment behavior? | Monetization cannot be designed reliably without commercial model. | Do not add billing/payment code for MVP. | Product owner/business | DEFERRED |
| OQ-014 | Deferred | What happens if a multi-workspace upgrade expires, is downgraded or workspace count exceeds plan limits? | Controls data access and customer operations in a paid upgrade. | Define migration, read-only/export and ownership policy before commercialization; no assumption now. | Product owner/business | DEFERRED |
| OQ-015 | Medium | Which audit events are mandatory, and what happens if audit persistence fails during a critical business mutation? | Audit failure policy affects transaction behavior and incident investigation. | List critical mutation events and decide fail-closed vs retry/outbox treatment per event. | Security/architect | OPEN |

## Closed decisions that should not be re-asked without a conflict

- Product is a multi-tenant SaaS internal task/request management platform, not a marketplace/ERP.
- MVP has one primary workspace per company and manual company-registration approval.
- Company Administrator adds members directly in MVP.
- Future paid multi-workspace upgrade is a product direction; pricing/billing details are not decided.
- The intended stack is Angular 22, ASP.NET Core/.NET 10, PostgreSQL 18, Redis, MinIO/S3, OpenAI API, REST, Modular Monolith and Git/GitHub; actual implementation is not verified.
