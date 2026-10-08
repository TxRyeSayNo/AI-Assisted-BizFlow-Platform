# Evidence & File Attachments — Implementation Contract

Scope: FR-COL-002, API-COL-03, API-COL-04, NFR-FILE-001, SSS §7.9, SSS §29 Appendix C Entity Attachment (Table 33 of 43), BR-013, BR-014, TST-FILE-001, TST-FILE-002.
Endpoints:
- `POST /api/v1/attachments/upload-session`
- `PUT /api/v1/attachments/upload-session/{id}/binary`
- `POST /api/v1/attachments/finalize`
- `GET /api/v1/attachments`
- `GET /api/v1/attachments/{id}/download`
- `DELETE /api/v1/attachments/{id}`
- Sub-resource endpoints:
  - `POST /api/v1/tasks/{id}/attachments/upload-session`
  - `GET /api/v1/tasks/{id}/attachments`
  - `POST /api/v1/requests/{id}/attachments/upload-session`
  - `GET /api/v1/requests/{id}/attachments`

---

## 1. Architectural and Business Rules (SSS §7.9, §29 Appendix C, Table 33 of 43)

- **Physical Model & Invariants**:
  - Entity `Attachment`:
    - `AttachmentId`: UUIDv7 PK.
    - `TenantId`: UUID FK to `Tenant.TenantId`, immutable.
    - `ObjectType`: VARCHAR(32) constrained to `TASK`, `REQUEST`, `COMMENT`, `RESULT`, `PROGRESS`, immutable.
    - `ObjectId`: UUID polymorphic identifier of target entity, immutable.
    - `UploadedBy`: UUID FK to `User.UserId`, immutable.
    - `FileName`: VARCHAR(255) non-empty sanitized base file name.
    - `ContentType`: VARCHAR(100) allowlisted MIME type.
    - `SizeBytes`: BIGINT between 1 and 524,288,000 bytes (500 MB hard limit).
    - `ObjectKey`: VARCHAR(500) UNIQUE tenant-isolated storage path (`tenants/{tenantId}/{yyyy}/{MM}/{attachmentId}_{filename}`).
    - `Hash`: VARCHAR(128) SHA-256 hex digest verified on finalize.
    - `Status`: VARCHAR(32) constrained to `UPLOADING`, `READY`, `FAILED`, `DELETED`.
    - `CreatedAt`: TIMESTAMPTZ UTC, set upon creation.
    - `DeletedAt`: TIMESTAMPTZ UTC nullable, soft deletion marker.
  - Multi-tenant integrity enforced via database trigger `bizflow_attachment_guard()`:
    - Uploader must belong to the exact same `TenantId` as the attachment.
    - Referenced `TASK`, `REQUEST`, `COMMENT`, `RESULT`, or `PROGRESS` must exist in the exact same `TenantId`.
    - Immutability of `(TenantId, ObjectType, ObjectId, UploadedBy, ObjectKey, SizeBytes)` on update.
  - Check constraints:
    - `CK_Attachment_ObjectType`: Allowed object types (`TASK`, `REQUEST`, `COMMENT`, `RESULT`, `PROGRESS`).
    - `CK_Attachment_Status`: Allowed statuses (`UPLOADING`, `READY`, `FAILED`, `DELETED`).
    - `CK_Attachment_SizeBytes`: Size between 1 and 524,288,000 bytes.
    - `CK_Attachment_FileName_NonEmpty`: Filename non-empty and trimmed.

- **Limits & Policy (BR-013, BR-014, NFR-FILE-001)**:
  - 500 MB hard limit (524,288,000 bytes). Configurable per tenant via `TenantSetting` key `attachment.max_size_bytes` (never exceeding 500 MB).
  - ContentType allowlist: PDF, DOC/DOCX, XLS/XLSX, PPT/PPTX, TXT/CSV, PNG/JPEG/GIF/WEBP, ZIP (configurable per tenant via `attachment.allowed_content_types`).
  - Storage provider abstraction (`IObjectStorageProvider`) supporting direct object-storage upload sessions without proxying large files through API memory.

- **Authorization & Permissions**:
  - `attachments.upload`: Required to create upload sessions. Granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.
  - `attachments.read`: Required to list and download attachments. Granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.
  - `attachments.delete`: Required to delete attachments. Soft deletion permitted only to original uploader or `COMPANY_ADMIN`.

- **Idempotency & Replay Protection**:
  - Optional `Idempotency-Key` header on `POST /api/v1/attachments/upload-session` and sub-resource routes.
  - Key validated (1 to 128 visible ASCII characters) and SHA-256 hashed.
  - Replay index: `UX_AuditLog_AttachmentSessionReplay` on `AuditLog` (`TenantId`, `ActorId`, `MetadataJson -> 'idempotency' ->> 'keyHash')`.
  - Conflict detection: Replaying with identical key but different input throws `409 Conflict` (`IDEMPOTENCY.CONFLICT`).

- **Audit Logging**:
  - Emits immutable `AuditLog` records:
    - `ATTACHMENT.UPLOAD_SESSION_CREATED`: Captures object target, filename, size, content type, and idempotency replay metadata.
    - `ATTACHMENT.FINALIZED`: Captures status transition to `READY` with verified SHA-256 hash.
    - `ATTACHMENT.DELETED`: Captures soft delete event and actor ID.

---

## 2. API Contracts

### `POST /api/v1/attachments/upload-session`
- **Headers**: `Idempotency-Key` (Optional, 1-128 visible ASCII chars).
- **Request Body**:
```json
{
  "objectType": "TASK",
  "objectId": "01944888-0000-7000-8000-000000000001",
  "fileName": "audit-report.pdf",
  "contentType": "application/pdf",
  "sizeBytes": 1048576
}
```
- **Response** (`201 Created`):
```json
{
  "attachmentId": "01944888-0000-7000-8000-000000000002",
  "objectKey": "tenants/01944888-0000-7000-8000-000000000000/2026/10/01944888-0000-7000-8000-000000000002_audit-report.pdf",
  "uploadUrl": "/api/v1/attachments/upload-session/01944888-0000-7000-8000-000000000002/binary",
  "expiresAt": "2026-10-07T21:00:00Z"
}
```

### `PUT /api/v1/attachments/upload-session/{id}/binary`
- **Request Body**: Raw binary stream.
- **Request Size Limit**: 524,288,000 bytes (500 MB).
- **Response**: `204 No Content`.

### `POST /api/v1/attachments/finalize`
- **Request Body**:
```json
{
  "attachmentId": "01944888-0000-7000-8000-000000000002",
  "clientHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```
- **Response** (`200 OK`): `AttachmentItemView` with status `READY`.

### `GET /api/v1/attachments`
- **Query Params**: `objectType` (`TASK` | `REQUEST` | ...), `objectId` (UUID).
- **Response** (`200 OK`): List of active (`DeletedAt == null`) attachments for the target object.

### `GET /api/v1/attachments/{id}/download`
- **Response** (`200 OK`): Binary stream with `Content-Disposition: attachment; filename="{fileName}"` and original `Content-Type`.

### `DELETE /api/v1/attachments/{id}`
- **Response**: `204 No Content` (soft-deletes attachment, setting `DeletedAt = now()`, status `DELETED`).

---

## 3. Frontend Integration

- Component: `<bf-work-item-attachments [objectType]="'TASK' | 'REQUEST'" [objectId]="id" />`
  - Located in `frontend/src/app/features/collaboration/work-item-attachments.ts`.
  - Supports file selection, client-side 500 MB limit check, upload session creation, binary stream upload, SHA-256 verification, finalize, download, and soft deletion.
  - Embedded into `TaskDetail` (`task-detail.html`) and `RequestDetail` (`request-detail.html`).
