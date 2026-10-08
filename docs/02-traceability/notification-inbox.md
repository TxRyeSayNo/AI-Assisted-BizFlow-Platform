# Tenant recipient notification inbox

Scope: API-NOTIF-01/02, the tenant portion of UI-14, SSS §22, Appendix C Notification and BR-001/019/020. The persistent inbox/read-receipt slice now has [recipient SignalR updates](notification-realtime.md). Remaining event dispatch and email notifications are not complete.

## Storage and ownership

Migration `20261001193054_NotificationInbox` adds the already-approved Notification table, taking the implemented application model from 15 to 16 of 43 tables. TenantId remains required. RecipientId references User, and a PostgreSQL trigger enforces same-tenant membership. The default EF query filter requires both the current tenant and the actual recipient. A Company Administrator does not gain access to colleagues' notification content merely by being an administrator.

The §22 sixteen-event catalog is allowlisted in Domain and SQL. Payload/recipient identity is immutable; first ReadAt and SentAt values cannot be overwritten. EF permits the recipient receipt mutation only, rejecting forged detached updates and cross-tenant recipients. No application delete operation exists, and rollback refuses to discard a populated inbox. ObjectType/ObjectId remain optional polymorphic metadata: future event producers must authorize and resolve them through the owning module before creating notifications. No public create/update-payload endpoint or production fixture is introduced.

`(TenantId, IdempotencyKey)` is unique. Future dispatchers must construct a stable key incorporating event occurrence and recipient; this constraint alone does not implement dispatch or prove scheduler exactly-once behavior. `SentAt` means email delivery only and is not used as a creation timestamp. The approved table has no CreatedAt, so lists order deterministically by descending UUIDv7 ID without inventing an event timestamp.

## API

- `GET /api/v1/notifications`: active tenant member; optional page (1), pageSize (25, max 100), unreadOnly (false). Returns `{ items, page, pageSize, total, unreadCount }`. Total respects the unread filter; unreadCount covers the entire recipient inbox. Separate read-committed count/page queries may observe concurrent changes; no frozen snapshot is promised.
- Rows contain notificationId, type, objectType, objectId, title, content, readAt and sentAt. Tenant IDs, recipient IDs, idempotency keys and account data are not exposed. Supplied tenant/recipient query parameters or headers cannot change scope.
- `PATCH /api/v1/notifications/{id}/read` with `{}`: active member **and actual recipient**, not a company-wide permission. Unknown body fields, including timestamps and ownership, return 422. A row lock serializes receipts; Application rechecks active membership after acquiring it, Domain enforces ownership, and the first receipt plus `NOTIFICATION.READ` audit commit atomically. Repeated/concurrent requests preserve the original ReadAt and do not add another audit event. Receipt timestamps use UTC microsecond precision so first/replayed JSON agrees with PostgreSQL storage.
- Other-recipient, foreign-tenant and missing IDs all return 404 with caller-only denial audit. Platform accounts are denied the tenant inbox. The platform Notification.TenantId conflict is awaiting owner confirmation, not silently resolved with a nullable column.
- Both endpoints authenticate server-side and return no-store responses. No new catalog grant is required: the canonical rules are authenticated recipient ownership, not a display-role check. Controllers do not access DbContext.

## GRID UI

Workspace → Notifications (`/notifications`) shows the recipient's events, unread count/filter, pagination, refresh, and mark-read actions. Content is plain text rendered with Angular escaping, never innerHTML. Read time is explicitly UTC. The UI handles loading, empty, failure and denial, cancels stale list requests, refreshes server counts after receipts and recovers an empty last page. It does not optimistically claim a failed receipt succeeded. Task references now link to the implemented scoped Task detail route; current read authorization is rechecked there. Request/Approval references remain text until their authorized routes exist.

## Remaining gates

The tenant inbox now subscribes to a bearer-authenticated SignalR connection while open. Assignment, acceptance and first-read commits send payload-free refresh hints only to live recipient sessions. The client re-reads the existing authorized API, catches up after server-ready/reconnect, reports connection availability, and stops on navigation/session change. Realtime delivery failure never replaces the persisted inbox or reverses a commit. See the linked contract for security and reliability boundaries.

- Remaining Application event producers/dispatcher for the §22 catalog and FR-specific onboarding/status alerts. Task assignment and acceptance now write their in-app events atomically with authoritative resource references and assignment/recipient idempotency keys; acceptance targets the original assigning manager.
- Task-assignment department audiences now expand to active members under membership locks. Remaining event audiences, configured recipient policies, email delivery/retry and correctly persisted SentAt, other resource realtime events, and badge integration with the full authenticated shell remain required.
- Request/Approval deep links after those modules exist, full UI-14 platform support after the pending schema decision, production load/accessibility review and packaged-image/hosted-CI verification. Task deep links are implemented and covered by the real assignment-to-recipient browser flow.
- BR-019 scheduler/escalation guarantees remain unproven until the actual producers/jobs are implemented; a unique inbox key and idempotent read receipt are not a substitute.

Validation evidence is recorded in [implementation status](implementation-status.md). Test fixtures populate only disposable databases; the production inbox has no fabricated events.
