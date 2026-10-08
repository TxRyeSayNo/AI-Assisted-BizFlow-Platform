# Work Item Collaboration & Comments — Implementation Contract

Scope: FR-COL-001, API-COL-01, API-COL-02, SSS §7.9, SSS §29 Appendix C Entity Comment (Table 32 of 43).
Endpoints:
- `POST /api/v1/comments`
- `GET /api/v1/comments`
- `PUT /api/v1/comments/{id}`
- `DELETE /api/v1/comments/{id}`
- Sub-resource endpoints:
  - `POST /api/v1/tasks/{id}/comments`
  - `GET /api/v1/tasks/{id}/comments`
  - `POST /api/v1/requests/{id}/comments`
  - `GET /api/v1/requests/{id}/comments`

---

## 1. Architectural and Business Rules (SSS §7.9, §29 Appendix C, Table 32 of 43)

- **Physical Model & Invariants**:
  - Entity `Comment`:
    - `CommentId`: UUIDv7 PK.
    - `TenantId`: UUID FK to `Tenant.TenantId`, immutable.
    - `ObjectType`: VARCHAR(32) constrained to `TASK`, `REQUEST`, `RESULT`, `PROGRESS`, immutable.
    - `ObjectId`: UUID polymorphic identifier of target entity, immutable.
    - `AuthorId`: UUID FK to `User.UserId`, immutable.
    - `Content`: TEXT non-empty, max 4000 characters. Trimmed on input.
    - `CreatedAt`: TIMESTAMPTZ UTC, set upon creation.
    - `EditedAt`: TIMESTAMPTZ UTC nullable, updated upon modification.
    - `DeletedAt`: TIMESTAMPTZ UTC nullable, soft deletion marker.
  - Multi-tenant integrity enforced via database trigger `bizflow_comment_guard()`:
    - Author must belong to the exact same `TenantId` as the comment.
    - Target `WorkTask` or `Request` must exist in the exact same `TenantId`.
    - Immutability of `(TenantId, ObjectType, ObjectId, AuthorId)` on update.
  - Check constraint `CK_Comment_ObjectType` ensuring allowed object types.

- **Authorization & Permissions**:
  - `comments.create`: Required to add new comments. Default granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.
  - `comments.read`: Required to list and read comments. Default granted to `COMPANY_ADMIN`, `MANAGER`, and `EMPLOYEE`.
  - **Editing Rule**: Only the original `AuthorId` may edit comment content while `DeletedAt == null`.
  - **Deletion Rule**: Only the original `AuthorId` or an authorized `COMPANY_ADMIN` may soft delete a comment.

- **Idempotency & Replay Protection**:
  - Optional `Idempotency-Key` header on `POST /api/v1/comments` and sub-resource routes.
  - Key validated (1 to 128 visible ASCII characters) and SHA-256 hashed.
  - Replay index: `UX_AuditLog_CommentCreationReplay` on `AuditLog` (`TenantId`, `ActorId`, `MetadataJson -> 'idempotency' ->> 'keyHash')`.
  - Conflict detection: Replaying with identical key but different input throws `409 Conflict` (`IDEMPOTENCY.CONFLICT`).

- **Audit Logging**:
  - Emits immutable `AuditLog` records:
    - `COMMENT.CREATED`: Contains structured snapshot of comment attributes and idempotency replay metadata.
    - `COMMENT.EDITED`: Captures previous content and updated content with `EditedAt`.
    - `COMMENT.DELETED`: Captures soft delete event and actor ID.

---

## 2. API Contracts

### `POST /api/v1/comments`
- **Headers**: `Idempotency-Key` (Optional, 1-128 visible ASCII chars).
- **Request Body**:
  ```json
  {
    "objectType": "TASK",
    "objectId": "019f7f8a-0000-7000-8000-000000000055",
    "content": "Please verify the firewall logs before proceeding."
  }
  ```
- **Response `201 Created`**:
  ```json
  {
    "commentId": "019f7f8a-0000-7000-8000-000000000099",
    "objectType": "TASK",
    "objectId": "019f7f8a-0000-7000-8000-000000000055",
    "authorId": "019f7f8a-0000-7000-8000-000000000010",
    "authorName": "Alice Engineer",
    "authorEmail": "alice@example.com",
    "content": "Please verify the firewall logs before proceeding.",
    "createdAt": "2026-10-07T12:00:00Z",
    "editedAt": null,
    "isOwner": true,
    "canDelete": true
  }
  ```

### `GET /api/v1/comments`
- **Query Parameters**:
  - `objectType`: `TASK` | `REQUEST` | `RESULT` | `PROGRESS`
  - `objectId`: UUID
- **Response `200 OK`**: Array of `CommentItemView` ordered by `CreatedAt ASC`.

### `PUT /api/v1/comments/{id}`
- **Request Body**:
  ```json
  {
    "content": "Updated comment text addressing recent updates."
  }
  ```
- **Response `200 OK`**: Returns updated `CommentItemView`.

### `DELETE /api/v1/comments/{id}`
- **Response `204 NoContent`**: Soft deletes the comment (`DeletedAt` set). Excluded from future list queries.

---

## 3. Frontend Implementation

- **Component**: `WorkItemComments` (`frontend/src/app/features/collaboration/work-item-comments.ts`)
  - Standalone component with template `work-item-comments.html` and styles `work-item-comments.scss`.
  - Embedded into:
    - `TaskDetail` (`task-detail.html` / `task-detail.ts`)
    - `RequestDetail` (`request-detail.html` / `request-detail.ts`)
  - Timeline view displaying avatar, author name, timestamp with local timezone formatting, and `(edited)` tag.
  - "You" pill indicating current user's comments.
  - Inline editing with character counter (`0 / 4000`) and save/cancel actions.
  - Safe delete confirmation prompt.
  - Accessible keyboard navigation and semantic HTML.

---

## 4. Verification & Test Evidence

- **Unit Tests**:
  - `tests/BizFlow.UnitTests/Collaboration/CommentTests.cs`:
    - Entity creation invariants, required fields, whitespace trimming, 4000 char boundary.
    - Content editing and soft delete rules.
    - `AuditLog` creation for `COMMENT.CREATED`, `COMMENT.EDITED`, `COMMENT.DELETED`.
    - Service layer authorization (`comments.create`, `comments.read`), ownership checks, admin deletion checks, target validation, and idempotency replay.
  - **Result**: 338/338 backend unit tests passed.

- **PostgreSQL 18 Integration Tests**:
  - `tests/BizFlow.IntegrationTests/Persistence/CommentApiTests.cs`:
    - Verified against real Docker container running PostgreSQL 18.3-alpine.
    - Tested task and request comment creation, idempotency replay caching, timeline listing, owner editing, non-owner 403 prevention, bystander delete 403 prevention, and soft-delete exclusion from listing.
  - **Result**: Passed.

- **Frontend Tests**:
  - `frontend/src/app/features/collaboration/work-item-comments.spec.ts`:
    - Tests loading, rendering, posting, inline editing, and deletion confirmation.
  - **Result**: 33/33 test files passed, 178/178 Angular unit tests passed.
  - **Build**: `npm run build` succeeded with 0 errors.
