# BizFlow Data Architecture

| Field | Value |
|---|---|
| Version | 0.1-draft |
| Date | 2026-10-17 |
| Source | SSS v1.2 sections 12–13, 17–19; owner workspace decision |
| Status | Conceptual target; no approved RDS or verified migrations found |

## 1. Purpose and boundaries

This document describes high-level ownership and persistence responsibilities. It is not an SQL schema or RDS. Exact columns, primary/foreign keys, indexes, check constraints, uniqueness scopes and migration sequencing must be defined in the canonical RDS against the correct application repository.

## 2. Data stores

| Store | Intended responsibility | Must not be used as |
|---|---|---|
| PostgreSQL 18 | Transactional source of truth for identity/organization associations, task/request state, workflow/approval records, SLA data, audit and attachment metadata | A cache or uncontrolled cross-module mutation surface |
| Redis | Cache/ephemeral context/coordination only where a use case justifies it | Source of truth for business lifecycle or permission grants |
| MinIO/S3-compatible storage | Attachment/evidence bytes and object lifecycle | Authorization authority; possession of object key is not proof of access |
| OpenAI API | External inference for approved AI-assisted flows | Source of truth for entities, state, permission, approval or validated business decisions |

The source baseline names Redis 8.x and PostgreSQL 18, but actual package/configuration and use are not verified.

## 3. Candidate domains and ownership

| Domain | Candidate concepts from SSS v1.2 | Owner module | Notes |
|---|---|---|---|
| Identity and access | User, authentication/session, Role, Permission, RolePermission | M01/M03 | SSS assumes one company/tenant membership per user in MVP. Exact global-vs-tenant uniqueness of email/employee code must match login behavior. |
| Company and tenancy | Company, Tenant/workspace, registration and status history | M02 | One primary workspace per company in MVP; future multi-workspace design is deferred. |
| Organization | Department, employee membership/assignment to department | M03 | Parent department is optional in the source baseline; department assignments remain tenant-scoped. |
| Service/configuration | Service/category, routing configuration | M04 | References to departments, roles and published workflow/approval configuration must not cross tenant. |
| Task | Task, Assignment, Progress/Report, Result, Confirmation/history | M05 | Preserve lifecycle and assignment history; status updates must be validated. |
| Request | Request, routing/resolution history, revised-request link, parent/child relationship, linked task reference | M06 | Rejected source request remains rejected; revised request is a distinct record via `RevisedFromRequestId`. |
| Workflow/approval | Workflow definition/version/instance, approval rule/instance/step/decision | M07 | Published versions used by running processes remain immutable to those instances. |
| SLA | SLA profile/version, runtime clock, pause record, escalation event | M08 | Scheduler retries must not generate duplicate logical escalation records. |
| Collaboration | Comment, Notification, delivery status/history | M09 | Recipients and reads/writes respect resource and tenant permissions. |
| Evidence | Attachment metadata, object key, content type/size/hash/status as specified | M10 | Source baseline's maximum file size is 500 MB; retention/scanning policy remains subject to approval. |
| Reporting | Aggregated work, workload and SLA metrics | M11 | Metrics definitions and freshness need explicit tests; aggregates and exports are tenant-scoped. |
| AI/audit | AI interaction/recommendation/action metadata, Audit Event | M12/M13 | Avoid unnecessary raw prompt storage; preserve action provenance sufficient for investigation. |

**Entity-count discrepancy:** extracted SSS v1.2 has 38 distinct `Entity – ...` headings in Appendix C while its scorecard says 39 entities. Do not add an unsupported entity to “make the count match.” Resolve in the canonical RDS/data dictionary.

## 4. MVP tenancy assumptions

- A company is the customer organization.
- One primary workspace/tenant is provisioned for each approved company in MVP.
- The baseline uses tenant as the primary data-isolation boundary and assumes one tenant/company membership per user in MVP.
- Departments and projects are organizational/work grouping concepts, not automatic tenant boundaries.
- Future paid multi-workspace functionality is a product direction. Do not treat it as a requirement to create multiple tenant/workspace records in MVP.

A future enterprise model must decide whether each workspace is itself a tenant/security boundary, whether membership is at company or workspace level, whether a user can belong to multiple workspaces, how permissions are scoped, which configurations are inherited and how subscription entitlements are enforced. Those decisions are not made by this document.

## 5. Integrity rules

1. Every tenant-owned business record must be connected to a tenant boundary either directly or via an unambiguous, validated parent relationship.
2. Foreign references must remain within the applicable tenant, except for explicitly platform-scoped catalogs with approved semantics.
3. Use database constraints for stable invariants (e.g., uniqueness in approved scope, required references) where feasible; retain application validation for contextual business rules.
4. Task/request transitions and related audit/history effects require deliberate transaction boundaries.
5. Important mutable aggregates should detect stale writes using an approved concurrency mechanism.
6. Workflow, approval and SLA definitions referenced by active processes must preserve their version semantics.
7. Original rejected request records remain immutable except for specifically allowed administrative metadata; the revised request is a separate record.
8. Soft delete/deactivation and history preservation follow the source baseline; audit logs are not hard-deleted under the current design.
9. Exact data retention, export, erasure, restoration and archival requirements are open pending owner/policy approval.

## 6. Redis isolation

If tenant-derived business data is cached, cache keys must include the appropriate tenant and authorization/configuration scope, and cache invalidation must prevent stale authorization/data leakage. Never trust a cache key supplied by an untrusted caller. Since actual Redis usage is not verified, do not introduce distributed cache logic without a documented need.

## 7. Object storage flow

Source baseline upload pattern:

1. Client requests an upload session from the API.
2. API validates caller, tenant, resource permission, size and allowed type.
3. API returns a scoped signed/multipart plan.
4. Client uploads bytes directly to object storage.
5. Client finalizes the upload with metadata; server validates expected object and persists attachment metadata.
6. Downloads are issued only after fresh authorization for the linked Task/Request/Comment/Result.
7. Failures/abandoned uploads need a bounded cleanup policy; exact retention timing is OPEN.

Storage prefixing by tenant is a useful defense-in-depth organization pattern, not a substitute for authorization.

## 8. Transaction and consistency boundaries

PostgreSQL transactions cover relational changes within a database transaction. They do not atomically include Redis, object storage, email or OpenAI. Each cross-boundary workflow requires explicit failure behavior:

- Database commit succeeded but notification failed: preserve the committed business state and record/retry the notification according to policy.
- Object bytes uploaded but metadata finalization failed: keep the file inaccessible through BizFlow and define cleanup/retry behavior.
- AI request times out: do not infer success; return a safe recoverable error or fall back to manual operation.
- Background job retries: use deduplication/idempotency for side effects that must happen at most once per logical event.

Do not add an outbox, message broker or distributed transaction solely as a pattern; propose it only if reliability evidence and workflow volume justify its complexity.

## 9. RDS deliverable checklist

Before declaring data design complete, RDS should identify:

- Canonical entity inventory and resolution of 38-vs-39 count discrepancy.
- Column meaning/type/nullability/defaults and sensitivity.
- PK/FK/unique/check constraints and tenant-aware uniqueness.
- Indexes aligned to tenant-scoped queries and list/filter use.
- Ownership of each data set and allowed write paths.
- Concurrency fields and transition invariants.
- Migration strategy and seeded system permission catalog.
- Data retention/export/deletion policies after approval.
- How tenant isolation is enforced and negatively tested.
