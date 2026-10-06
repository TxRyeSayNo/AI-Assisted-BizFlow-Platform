# ADR-0008: Separate authority for service metadata updates

Status: Accepted by the project owner on 2026-10-04.

## Context and decision

Canonical API-SVC-03 requires `PUT /api/v1/services/{id}` for Company Administrators but does not name its atomic permission. The owner explicitly approved a separate tenant-scoped `service.update` permission, with default grant only to the COMPANY_ADMIN system role.

The endpoint edits code, name, description and active flag using required If-Match concurrency. It preserves category identities/configuration and workflow/SLA bindings. Custom tenant roles may grant this catalog permission through the existing authorized role-management use case; actor authority must not be inferred from role names. `service.create` alone does not grant update authority.

This decision does not authorize new service states, category editing/deletion, routing edits or workflow/SLA/approval binding changes through the metadata endpoint. It also does not resolve any Request lifecycle ambiguity.

Trace: API-SVC-03, FR-SVC-001 metadata, BR-001/002/020 and architecture §5 mutable-aggregate concurrency.
