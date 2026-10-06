# Tenant-policy persistence foundation

Scope: approved architecture §4/A-03 `TenantSetting`, a prerequisite for BR-005/014/021/022 and tenant provisioning. This is storage infrastructure, not completion of the settings UI, AI execution, attachments or SLA runtime.

## Implemented contract

The approved six-field entity is preserved: TenantSettingId (server UUIDv7), TenantId, Key, ValueJson (JSONB), UpdatedBy and UpdatedAt (UTC). The physical key column is VARCHAR(128), sufficient for the fixed approved catalog; PostgreSQL xmin supplies optimistic concurrency without a convenience table. `(TenantId, Key)` is unique. Migration `20261002123538_TenantSettingFoundation` brings the application model from 16 to 17 of 43 approved tables.

| Approved key(s) | Stored value validation |
|---|---|
| `ai.enabled`, `ai.auto_action_enabled`, `sla.pause_on_waiting_for_information`, `request.generic_service_allowed`, `notification.email_enabled` | JSON boolean, not a string or null |
| `ai.auto_action_tools`, `attachment.allowed_content_types` | Array of nonblank strings; empty arrays are valid |
| `ai.confidence_threshold` | Number from 0 through 1 |
| `ai.monthly_call_limit` | Nonnegative 32-bit integer |
| `attachment.max_size_bytes` | Integer from 0 through 524288000; never above the approved 500 MB maximum |
| `report.max_range_days` | Positive 32-bit integer |

Unknown keys, malformed JSON and incompatible types/ranges are rejected. No defaults are silently inserted. In particular, no monthly quota is invented where A-03 says “tenant policy.” Zero remains a literal stored numeric value, not an implemented unlimited-budget/upload convention. Each consumer must define and enforce its approved policy semantics before use.

## Isolation and integrity

- EF queries require both current tenant and user context. Anonymous, foreign-tenant and platform-plane reads do not implicitly expose tenant policies.
- Tenant EF writes require current tenant ownership and the actual current user as UpdatedBy, backed by a persisted tenant user. Updates validate the fresh row; detached ownership forgery, key changes and hard deletion are rejected. Stale xmin writes conflict rather than overwrite.
- SQL enforces typed values/ranges, unique keys, tenant/user foreign keys and immutable setting identity/tenant/key. UpdatedBy must refer to the owning tenant or a tenantless platform account. This preserves a truthful actor reference for a future explicitly authorized platform-provisioning path; it does not grant platform write authority, and the ordinary EF tenant path rejects platform writes.
- Empty migration rollback/reapply is supported. Populated rollback refuses to discard policy configuration and preserves constraints/history.

## Remaining required work

- Application-layer permission checks, atomic before/after audit, concurrency contract, configuration API and GRID UI. UpdatedBy/UpdatedAt are not a replacement for immutable AuditLog evidence.
- Explicit initial provisioning/default seeding, including truthful platform actor attribution and safe retry behavior. No production policy row is seeded by this migration.
- Authoritative consumers: actual AI tool registry and confirmation/authorization checks, confidence/manual fallback, call-budget enforcement, SLA pause behavior, generic-request rules, report bounds, notification email policy and upload content/size validation.
- Typed storage is not tool allowlisting: a string in `ai.auto_action_tools` cannot execute anything. A MIME string cannot approve an upload. The 500 MB setting ceiling does not implement the file-upload requirement on its own.
- Hosted CI and packaged-image/browser regression evidence must be recorded per increment, not inferred from an earlier build. See [implementation status](implementation-status.md).
