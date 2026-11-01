# Bản sửa RDS v1.0 & SDS v1.0 theo mã nguồn thực tế

**Phương án A** — giữ nguyên code, sửa tài liệu cho khớp.

Nguồn đối chiếu (authoritative): `backend/BizFlow.Infrastructure/Persistence/Migrations/BizFlowDbContextModelSnapshot.cs`
(+ 11 file `*ModelConfiguration.cs` / `*Configuration.cs`, các file migration, và `BizFlowDbContext.cs`).

Cách dùng: áp dụng lần lượt Phần 1 → 6. Phần 3 là bản đồ đổi tên dùng cho **mọi** câu SQL trong hai tài liệu.

---

## 1. Sửa số liệu

| Vị trí | Đang ghi | Sửa thành |
|---|---|---|
| RDS §2.1, SDS Fig 1.2 | 32 routes | **34** mục `path:` trong `app.routes.ts` |
| RDS §2.3, SDS (nhiều chỗ) | 39 seeded permissions / 37 tenant-assignable | **45** mã quyền phân biệt trong migrations |
| SDS §I.2a | 19 PostgreSQL enum types | **17** |
| RDS UC-14/UC-15, SDS §II.6 | Task **9 trạng thái** | **10** (`task_state` có thêm `REJECTED`) |
| RDS §1.1 actor #2 | attachments up to 500 MB | 500 MB là **mặc định**; có thể override theo tenant (`GetTenantMaxUploadSizeAsync`) |

Giữ nguyên (đã đúng): 40 bảng, 26 controllers, Request 12 trạng thái, .NET 10 / EF Core 10.0.12 / Angular 22.2.0,
PBKDF2 (`PasswordHasher<UserAccount>`), SignalR `/api/v1/realtime/notifications`, Hangfire + MailKit,
Redis RESP client tự viết, S3 SigV4 tự viết, giới hạn 524.288.000 bytes.

---

## 2. Bảng enum chuẩn — thay toàn bộ bảng "19 Enums" ở SDS §I.2a

| # | PostgreSQL enum | Giá trị |
|---|---|---|
| 1 | `audit_actor_type` | AI_AGENT, SYSTEM, USER |
| 2 | `company_status` | ACTIVE, INACTIVE, PENDING, SUSPENDED |
| 3 | `permission_scope` | ASSIGNED, DEPARTMENT, PLATFORM, SELF, TENANT |
| 4 | `record_status` | ACTIVE, INACTIVE |
| 5 | `request_priority` | CRITICAL, HIGH, LOW, MEDIUM |
| 6 | `request_state` | CANCELLED, CLOSED, CONFIRMED, DRAFT, IN_PROGRESS, OVERDUE, RECEIVED, REJECTED, RESOLVED, ROUTED, SUBMITTED, WAITING_FOR_INFORMATION |
| 7 | `service_category_status` | ACTIVE, INACTIVE |
| 8 | `service_status` | ACTIVE, DRAFT, INACTIVE |
| 9 | `sla_profile_status` | ACTIVE, DRAFT, INACTIVE |
| 10 | `task_priority` | CRITICAL, HIGH, LOW, MEDIUM |
| 11 | `task_state` | ACCEPTED, ASSIGNED, CANCELLED, COMPLETED, CONFIRMED, DRAFT, IN_PROGRESS, OVERDUE, REJECTED, SUBMITTED |
| 12 | `tenant_status` | ACTIVE, INACTIVE, PROVISIONING, SUSPENDED |
| 13 | `user_status` | ACTIVE, INACTIVE, LOCKED |
| 14 | `workflow_business_type` | REQUEST, TASK |
| 15 | `workflow_status` | ACTIVE, DRAFT, INACTIVE |
| 16 | `workflow_step_type` | ACTION, APPROVAL, CONFIRMATION |
| 17 | `workflow_version_status` | DRAFT, PUBLISHED, RETIRED |

**Các enum tài liệu nêu nhưng KHÔNG tồn tại** — phải bỏ hoặc mô tả lại là cột `varchar`:
`request_status`, `task_status`, `work_priority`, `config_version_status`, `checklist_status`,
`confirmation_target_type`, `confirmation_decision`, `work_item_type`, `ai_interaction_status`,
`ai_recommendation_decision`, `notification_channel`.

**Giá trị sai cần sửa trong toàn bộ 2 tài liệu:**

- Priority: `URGENT` → **`CRITICAL`** (mọi chỗ: RDS UC-11, SCR-16/17 field rules, SDS §II.5…)
- `company_status`: bỏ `REJECTED`, thêm `INACTIVE`. ⇒ Luồng "Reject company registration" trong RDS UC-04 / SDS §II.2 **không** chuyển sang `REJECTED` được; phải mô tả lại theo trạng thái thực có.
- `tenant_status`: thêm `INACTIVE`, `PROVISIONING`
- `user_status`: bỏ `PENDING`
- `workflow_step_type`: `TASK_EXECUTION`, `ROUTING` → thực tế chỉ có `ACTION`, `APPROVAL`, `CONFIRMATION`

---

## 3. Bản đồ đổi tên — áp cho MỌI câu SQL trong 2 tài liệu

### 3.1 Tên bảng (entity → bảng vật lý)

`WorkRequest` → `"Request"` · `WorkTask` → `"Task"` · `UserAccount` → `"User"` ·
`InternalService` → `"Service"` · `WorkflowDefinition` → `"Workflow"` ·
`SlaProfile` → `"SLAProfile"` · `SlaVersion` → `"SLAVersion"` · `AiVectorMemory` → `"AIVectorMemory"`

### 3.2 Khóa chính bị ghi sai

| Bảng | Tài liệu ghi | Thực tế |
|---|---|---|
| `RequestResolution` | RequestResolutionId | **ResolutionId** |
| `AIRecommendation` | AIRecommendationId | **RecommendationId** |
| `TaskChecklistItem` | TaskChecklistItemId | **ChecklistItemId** |
| `TaskProgressReport` | TaskProgressReportId | **ProgressReportId** |
| `WorkflowTransition` | WorkflowTransitionId | **TransitionId** |
| `DeadlineReminderSchedule` | ScheduleId | **Id** |
| `NotificationLog` | NotificationLogId | **Id** |
| `WebPushSubscription` | SubscriptionId | **Id** |

Các PK còn lại (`CompanyId`, `TenantId`, `RequestId`, `TaskId`, `UserId`, `DepartmentId`, …) **đã đúng**.

### 3.3 Cột đổi tên

| Bảng | Tài liệu ghi | Thực tế |
|---|---|---|
| mọi bảng có concurrency | `"xmin"` (mô tả là system column) | cột **`xmin`** ánh xạ từ property `Version` (`IsRowVersion()`) — mô tả đúng, chỉ cần nói rõ |
| `AuditLog` | Before / After / Metadata | **BeforeJson / AfterJson / MetadataJson** |
| `AuthenticationSession` | RefreshTokenHash | **TokenHash** |
| `AuthenticationSession` | TokenFamilyId | **FamilyId** |
| `AuthenticationSession` | ReplacedBySessionId | **ReplacedById** |
| `AuthenticationSession` | IssuedAt | **CreatedAt** |
| `Attachment` | FileSizeBytes | **SizeBytes** |
| `Attachment` | Sha256Checksum | **Hash** |
| `Attachment` | IsUploaded (bool) | **Status** |
| `Attachment` / `Comment` / `Confirmation` / `Notification` / `AIRecommendation` | RequestId? + TaskId? (2 cột) | **ObjectId + ObjectType** (đa hình, 1 cặp cột) |
| `Comment` | AuthorUserId | **AuthorId** |
| `Confirmation` | TargetType / ConfirmedByUserId | **ObjectType / ActorId** |
| `Confirmation` | — | có thêm **MilestoneType**, **Note** |
| `Notification` | EventType / Message / IsRead | **Type / Content / ReadAt** (không có cột bool) |
| `Notification` | RelatedEntityType / RelatedEntityId | **ObjectType / ObjectId** |
| `TaskChecklistItem` | ItemOrder / Status / CompletedByUserId | **SortOrder / IsCompleted (bool) / CompletedBy** |
| `TaskProgressReport` | ProgressSummary / ReportedByUserId / ReportedAt | **Content / AuthorId / SubmittedAt** (+ có **Percent**) |
| `TaskResult` | ResultSummary / EvidenceNotes / SubmittedByUserId | **Content / AuthorId** (một cột `Content`, + **RevisionNo**) |
| `TaskAssignment` | AssignedToUserId / AssignedToDepartmentId | **UserId / DepartmentId** |
| `TaskAssignment` | AcceptedByUserId / SupersededAt | **AcceptedAt / EndedAt** |
| `RequestResolution` | ResolutionDetails / ResolvedByUserId / ResolvedAt | **Content / ResolverId / CreatedAt** (+ **RevisionNo**) |
| `RequestRouting` | RoutingReason / RoutedByUserId | **Reason / RoutedBy** (+ **Source**) |
| `Request` / `Task` / `Service` | SLAVersionId | **SLAVersionId** ✓ (property `SlaVersionId`) |
| `Service` | WorkflowVersionId / SLAVersionId | **ActiveWorkflowVersionId / ActiveSLAVersionId** |
| `SLAVersion` | VersionNumber / BusinessCalendarId / EscalationRules | **VersionNo / CalendarId / EscalationConfigJson** |
| `WorkflowVersion` | VersionNumber / DefinitionSnapshot | **VersionNo / DefinitionJson** |
| `WorkflowStep` | StepOrder / StepType | **OrderNo / Type** |
| `BusinessCalendar` | WorkingHours / Holidays | **WorkingHoursJson / HolidaysJson** |
| `TenantSetting` | PolicyKey / PolicyValue | **Key / ValueJson** |
| `AIInteraction` | UseCaseCode / PromptSummary | **Feature / InputRef** (+ **OutputRef**) |
| `AIInteraction` | PromptTokens + CompletionTokens (2 int) | **TokenUsage** (1 cột text) |
| `AIRecommendation` | RecommendationPayload / ConfidenceScore / Decision / DecidedByUserId | **PayloadJson / Confidence / HumanDecision / DecisionBy** |
| `AIRecommendation` | UseCaseCode / TargetEntityType / TargetEntityId | **RecommendationType / ObjectType / ObjectId** |
| `AIAgentAction` | AIRecommendationId | **AIInteractionId** (liên kết tới Interaction, không phải Recommendation) |
| `AIAgentAction` | ActionType / ResultEntityId / ExecutionMetadata / ExecutedAt | **ToolName / ResultRef / ArgsJson / CreatedAt** (+ **ExecutionStatus**, **AuthorizationResult**) |
| `AIVectorMemory` | DocumentType / ContentChunk | **MemoryScope / Content** |
| `NotificationLog` | RecipientUserId / DeliveryStatus / DispatchedAt | **UserId / Status / DeliveredAt** (+ **ScheduledAt**) |
| `DeadlineReminderSchedule` | AssigneeUserId / ReminderType / ScheduledFor | **UserId / TriggerType / RemindAt** |

---

## 4. Cột/quan hệ KHÔNG tồn tại — phải viết lại phát biểu nghiệp vụ

| Tài liệu khẳng định | Thực tế | Hướng sửa |
|---|---|---|
| `Request.ClientRequestId` (+ unique index TenantId, CreatorUserId, ClientRequestId) — **BR-012** | Không có cột. Khóa idempotency **không** được lưu | `pg_advisory_xact_lock(hashtextextended(...))` **có thật** (RequestCreationStore, RequestCancellationStore, RequestConfirmationStore, AttachmentStore, CommentStore). Sửa BR-012: chỉ còn cơ chế advisory lock trong transaction từ header `Idempotency-Key`; **bỏ** phần unique index và cột lưu trữ |
| `Request.CurrentDepartmentId`, `CurrentAssigneeUserId` | Không có | Phân công hiện tại đọc từ `RequestRouting` (bản ghi có `ToDepartmentId`/`ToUserId`). Sửa mọi SQL filter/scope theo `RequestRouting` |
| `Request.RevisionNumber`, `RejectionReason` | Không có | Chuỗi sửa đổi chỉ qua `RevisedFromRequestId`; số lần nộp lại nằm ở `RequestResolution.RevisionNo` |
| `Request.SubmittedAt / ReceivedAt / ConfirmedAt` | Chỉ có `ResolvedAt`, `ClosedAt` | Bỏ khỏi bảng mô tả & SQL báo cáo |
| `Request.ArchivedAt / ArchivedByUserId`, `Task.ArchivedAt` | Không có; chỉ có **`DeletedAt`** | Thống nhất dùng `DeletedAt`. **BR-025** sửa: "lưu trữ mềm" = `DeletedAt`, không phải `ArchivedAt` |
| `Task.AssignedUserId`, `Task.AssignedDepartmentId` | Không có | Phân công nằm hoàn toàn ở `TaskAssignment`. **Mọi SQL** `UPDATE "Task" SET "AssignedUserId"` đều sai |
| `Task.ParentTaskId`, `Task.ClientRequestId`, `StartedAt`, `SubmittedAt`, `ConfirmedAt` | Không có (chỉ `CompletedAt`) | Bỏ |
| `TaskAssignment.IsCurrent` + filtered unique index `ux_task_assignment_current_per_task` — **BR-018** | Không có cột `IsCurrent` | Bản ghi hiện hành = `EndedAt IS NULL`. Sửa BR-018 theo điều kiện này |
| `Department.ManagerUserId` | Không có | Quan hệ quản lý nằm ở `ManagementScope`. Sửa RDS UC-05 ("assign department managers") và bảng mô tả |
| `SLAProfile.Description`, `BusinessCalendar.Name/Status`, `Role.Description`, `ServiceCategory.TenantId` | Không có | Bỏ khỏi bảng mô tả |
| `SLAVersion.Status / PublishedAt / CreatedBy / CreatedAt` | Không có | SLAVersion chỉ gồm: SLAVersionId, SLAProfileId, VersionNo, TargetMinutes, WarningMinutes, CalendarId, EscalationConfigJson, xmin |
| `WorkflowTransition.FromStepId / ToStepId / ActionCode / ConditionExpression` | Thực tế: **FromState / ToState / GuardJson** | Chuyển trạng thái theo *state*, không theo *step id*. Sửa cả class diagram SDS |
| `WorkflowStep.AssignedRoleName / RequiresConfirmation / AllowDelegation` | Không có (chỉ `ConfigJson`) | Mô tả là cấu hình trong `ConfigJson` |
| `AuthenticationSession.TenantId / SecurityStampSnapshot / RevocationReason / ClientIp / UserAgent / LastUsedAt` | Không có | Thu hồi họ phiên chỉ set `RevokedAt`; không có lý do thu hồi |
| `WebPushSubscription.IsActive / UserAgent` | Không có | "tự động vô hiệu hóa khi 404/410" cần mô tả lại (xóa bản ghi hoặc cơ chế khác) |
| `Notification.Channel`, `CreatedAt` | Không có | Kênh gửi chỉ ghi ở `NotificationLog.Channel` |
| **BR-030**: nhắc hạn 4 mốc cố định 24H/4H/1H/OVERDUE | `DeadlineReminderSchedule.LeadTimeMinutes` — **cấu hình được**, `TriggerType` là varchar | Sửa BR-030, OD-032, NUF-03, UC-17, SCR-20 Database Access: "N mốc do người dùng cấu hình qua LeadTimeMinutes", bỏ con số cố định |
| `NotificationLog` unique (TenantId, TaskId, RecipientUserId, TriggerType, Channel) | cột là `UserId`, không phải `RecipientUserId` | Đổi tên trong phát biểu BR-030 |

---

## 5. Bảng 40 bảng chuẩn — thay RDS §3.1b và SDS §I.2b

Cột liệt kê theo đúng tên vật lý. `xmin` = token đồng thời lạc quan (property `Version`, `IsRowVersion()`).

| # | Bảng | Cột |
|---|---|---|
| 1 | Company | CompanyId, Code, Name, TaxId, ContactPersonName, ContactEmail, ContactPhone, EvidenceDocumentKey, EvidenceDocumentFileName, Status, CreatedAt, UpdatedAt, xmin |
| 2 | Tenant | TenantId, CompanyId, TenantKey, Name, Status, TimeZone, CreatedAt, UpdatedAt, xmin |
| 3 | TenantSetting | TenantSettingId, TenantId, Key, ValueJson, UpdatedBy, UpdatedAt, xmin |
| 4 | Department | DepartmentId, TenantId, Code, Name, ParentDepartmentId, Status, CreatedAt, UpdatedAt, xmin |
| 5 | User | UserId, TenantId, IsPlatformAdministrator, EmployeeCode, NormalizedEmployeeCode, Email, NormalizedEmail, FullName, PasswordHash, SecurityStamp, DepartmentId, Status, AccessFailedCount, LockoutEnd, MustChangePassword, LastLoginAt, CreatedAt, UpdatedAt, DeletedAt, xmin |
| 6 | ManagementScope | ManagementScopeId, TenantId, UserId, DepartmentId, IncludeDescendants, CreatedBy, CreatedAt, xmin |
| 7 | Role | RoleId, TenantId, Name, IsSystem, Status, CreatedAt, UpdatedAt, xmin |
| 8 | Permission | PermissionId, Code, Module, Action, ScopeType |
| 9 | UserRole | UserId, RoleId |
| 10 | RolePermission | RoleId, PermissionId |
| 11 | AuthenticationSession | AuthenticationSessionId, UserId, FamilyId, TokenHash, CreatedAt, ExpiresAt, RevokedAt, ReplacedById, xmin |
| 12 | Workflow | WorkflowId, TenantId, Name, BusinessType, Status, CreatedAt, UpdatedAt, xmin |
| 13 | WorkflowVersion | WorkflowVersionId, WorkflowId, VersionNo, Status, DefinitionJson, PublishedAt, xmin |
| 14 | WorkflowStep | WorkflowStepId, WorkflowVersionId, StepCode, Name, OrderNo, Type, ConfigJson, xmin |
| 15 | WorkflowTransition | TransitionId, WorkflowVersionId, FromState, ToState, GuardJson, xmin |
| 16 | SLAProfile | SLAProfileId, TenantId, Name, Status, xmin |
| 17 | BusinessCalendar | CalendarId, TenantId, TimeZone, WorkingHoursJson, HolidaysJson, xmin |
| 18 | SLAVersion | SLAVersionId, SLAProfileId, VersionNo, TargetMinutes, WarningMinutes, CalendarId, EscalationConfigJson, xmin |
| 19 | Service | ServiceId, TenantId, Code, Name, Description, ActiveWorkflowVersionId, ActiveSLAVersionId, Status, CreatedAt, UpdatedAt, xmin |
| 20 | ServiceCategory | ServiceCategoryId, ServiceId, Code, Name, Status, xmin |
| 21 | Request | RequestId, TenantId, RequesterId, ServiceId, CategoryId, ParentRequestId, RevisedFromRequestId, Title, Description, Priority, Status, WorkflowVersionId, SLAVersionId, ResolvedAt, ClosedAt, CreatedAt, UpdatedAt, DeletedAt, xmin |
| 22 | RequestRouting | RequestRoutingId, TenantId, RequestId, FromDepartmentId, ToDepartmentId, FromUserId, ToUserId, RoutedBy, RoutedAt, Reason, Source |
| 23 | RequestResolution | ResolutionId, RequestId, ResolverId, Content, RevisionNo, CreatedAt |
| 24 | Task | TaskId, TenantId, RequestId, CreatorId, Title, Description, Priority, Status, Deadline, WorkflowVersionId, SLAVersionId, CompletedAt, CreatedAt, UpdatedAt, DeletedAt, xmin |
| 25 | TaskChecklistItem | ChecklistItemId, TaskId, Title, SortOrder, IsCompleted, CompletedBy, CompletedAt |
| 26 | TaskAssignment | TaskAssignmentId, TaskId, UserId, DepartmentId, AssignedBy, AssignedAt, AcceptedAt, RejectedAt, RejectionReason, EndedAt |
| 27 | TaskProgressReport | ProgressReportId, TaskId, AuthorId, Content, Percent, SubmittedAt |
| 28 | TaskResult | TaskResultId, TaskId, AuthorId, Content, RevisionNo, SubmittedAt |
| 29 | Confirmation | ConfirmationId, TenantId, ObjectType, ObjectId, MilestoneType, ActorId, Decision, Note, ConfirmedAt |
| 30 | Comment | CommentId, TenantId, ObjectType, ObjectId, AuthorId, Content, CreatedAt, EditedAt, DeletedAt |
| 31 | Attachment | AttachmentId, TenantId, ObjectType, ObjectId, UploadedBy, FileName, ContentType, ObjectKey, Hash, SizeBytes, Status, CreatedAt, DeletedAt |
| 32 | Notification | NotificationId, TenantId, RecipientId, Type, ObjectType, ObjectId, Title, Content, ReadAt, SentAt, IdempotencyKey, xmin |
| 33 | DeadlineReminderSchedule | Id, TenantId, TaskId, UserId, LeadTimeMinutes, RemindAt, TriggerType, Status, CreatedAt |
| 34 | WebPushSubscription | Id, TenantId, UserId, Endpoint, P256dh, Auth, CreatedAt, LastActiveAt |
| 35 | NotificationLog | Id, TenantId, TaskId, UserId, TriggerType, ScheduledAt, Channel, Status, DeliveredAt, ErrorMessage |
| 36 | AuditLog | AuditLogId, TenantId, ActorId, ActorType, Action, ObjectType, ObjectId, BeforeJson, AfterJson, MetadataJson, CreatedAt |
| 37 | AIInteraction | AIInteractionId, TenantId, UserId, Feature, ModelName, InputRef, OutputRef, Status, TokenUsage, LatencyMs, CreatedAt |
| 38 | AIRecommendation | RecommendationId, TenantId, AIInteractionId, RecommendationType, ObjectType, ObjectId, PayloadJson, Confidence, HumanDecision, DecisionBy, DecidedAt |
| 39 | AIAgentAction | AIAgentActionId, TenantId, AIInteractionId, ToolName, ArgsJson, ResultRef, ExecutionStatus, AuthorizationResult, CreatedAt |
| 40 | AIVectorMemory | MemoryId, TenantId, MemoryScope, Title, Content, EmbeddingJson, MetadataJson, CreatedAt, UpdatedAt |

---

## 6. SQL viết lại

### 6.1 Nạp quyền hiệu lực (RDS §III.1.1 SQL-2) — chỉ đổi tên bảng

```sql
SELECT DISTINCT p."Code"
FROM "UserRole" AS ur
INNER JOIN "Role" AS r ON ur."RoleId" = r."RoleId"
INNER JOIN "RolePermission" AS rp ON r."RoleId" = rp."RoleId"
INNER JOIN "Permission" AS p ON rp."PermissionId" = p."PermissionId"
WHERE ur."UserId" = @userId AND r."Status" = 'ACTIVE'::record_status;
```
✅ Đúng sẵn, giữ nguyên.

### 6.2 Tra cứu tài khoản đăng nhập (RDS §III.1.1 SQL-1)

```sql
SELECT u."UserId", u."TenantId", u."IsPlatformAdministrator", u."EmployeeCode",
       u."Email", u."FullName", u."PasswordHash", u."SecurityStamp",
       u."Status", u."AccessFailedCount", u."LockoutEnd",
       t."TenantKey", t."Name" AS "TenantName", t."Status" AS "TenantStatus"
FROM "User" AS u
LEFT JOIN "Tenant" AS t ON u."TenantId" = t."TenantId"
WHERE (u."NormalizedEmployeeCode" = @normId OR u."NormalizedEmail" = @normId)
  AND u."DeletedAt" IS NULL
  AND (@tenantKey IS NULL OR t."TenantKey" = @tenantKey);
```
Sửa: thêm `u."DeletedAt" IS NULL` (xóa mềm).

### 6.3 Lưu phiên xác thực (RDS §III.1.1 SQL-3) — **sai 6 cột**

```sql
INSERT INTO "AuthenticationSession" (
    "AuthenticationSessionId", "UserId", "FamilyId",
    "TokenHash", "CreatedAt", "ExpiresAt"
) VALUES (
    @sessionId, @userId, @familyId,
    @refreshTokenHash, @now, @expiresAt
);

UPDATE "User"
SET "AccessFailedCount" = 0, "LockoutEnd" = NULL, "LastLoginAt" = @now, "UpdatedAt" = @now
WHERE "UserId" = @userId;
```
Bỏ: `TenantId`, `SecurityStampSnapshot`, `ClientIp`, `UserAgent` (không tồn tại).

### 6.4 Thu hồi họ phiên khi phát hiện replay (SDS §II.1)

```sql
UPDATE "AuthenticationSession"
SET "RevokedAt" = @now
WHERE "FamilyId" = @familyId AND "RevokedAt" IS NULL;
```
Bỏ `RevocationReason = 'replay_detected'` — không có cột. Nếu muốn giữ lý do thu hồi thì phải thêm cột + migration (sang phương án B).

### 6.5 Kiểm tra mã workspace (RDS §III.1.2 SQL-1)
✅ Đúng sẵn (`"Company"."Code"`, `"Tenant"."TenantKey"`).

### 6.6 Giao dịch phê duyệt doanh nghiệp (RDS §III.1.2 SQL-2)

```sql
BEGIN TRANSACTION;

UPDATE "Company"
SET "Status" = 'ACTIVE'::company_status, "UpdatedAt" = @now
WHERE "CompanyId" = @companyId AND "Status" = 'PENDING'::company_status;

INSERT INTO "Tenant" ("TenantId","CompanyId","TenantKey","Name","Status","TimeZone","CreatedAt","UpdatedAt")
VALUES (@tenantId, @companyId, @tenantKey, @companyName, 'ACTIVE'::tenant_status,
        'Asia/Ho_Chi_Minh', @now, @now);

INSERT INTO "Department" ("DepartmentId","TenantId","Code","Name","Status","CreatedAt","UpdatedAt")
VALUES (@deptId, @tenantId, 'GENERAL', 'General Operations', 'ACTIVE'::record_status, @now, @now);

INSERT INTO "User" (
    "UserId","TenantId","IsPlatformAdministrator","EmployeeCode","NormalizedEmployeeCode",
    "Email","NormalizedEmail","FullName","PasswordHash","SecurityStamp",
    "DepartmentId","Status","AccessFailedCount","MustChangePassword","CreatedAt","UpdatedAt"
) VALUES (
    @adminUserId, @tenantId, FALSE, 'ADMIN001', 'ADMIN001',
    @contactEmail, UPPER(@contactEmail), @contactPersonName, @passwordHash, @securityStamp,
    @deptId, 'ACTIVE'::user_status, 0, FALSE, @now, @now
);

INSERT INTO "UserRole" ("UserId","RoleId") VALUES (@adminUserId, @companyAdminRoleId);

COMMIT;
```
Sửa: bỏ UUID vai trò hard-code trong tài liệu (ID vai trò tra theo `Name = 'COMPANY_ADMIN' AND IsSystem`, xem `CompanyProvisioningStore`).

### 6.7 Thay quyền của vai trò tùy biến (RDS §III.2.1 SQL-1)

```sql
UPDATE "Role"
SET "UpdatedAt" = @now
WHERE "RoleId" = @roleId AND "TenantId" = @tenantId AND "IsSystem" = FALSE AND xmin = @expectedXmin;

DELETE FROM "RolePermission" WHERE "RoleId" = @roleId;

INSERT INTO "RolePermission" ("RoleId","PermissionId")
SELECT @roleId, p."PermissionId"
FROM "Permission" AS p
WHERE p."Code" = ANY(@permissionCodes) AND p."ScopeType" <> 'PLATFORM'::permission_scope;
```
Sửa: `"xmin"` → `xmin` (system column, không đặt trong nháy kép).

### 6.8 Phát hành phiên bản SLA (RDS §III.2.1 SQL-2) — **sai 5 cột**

```sql
INSERT INTO "SLAVersion" (
    "SLAVersionId", "SLAProfileId", "VersionNo", "TargetMinutes",
    "WarningMinutes", "CalendarId", "EscalationConfigJson"
) VALUES (
    @slaVersionId, @slaProfileId,
    COALESCE((SELECT MAX("VersionNo") FROM "SLAVersion" WHERE "SLAProfileId" = @slaProfileId), 0) + 1,
    @targetMinutes, @warningMinutes, @calendarId, @escalationConfigJson::jsonb
);
```
Bỏ: `Status`, `PublishedAt`, `CreatedBy`, `CreatedAt` (không tồn tại).

### 6.9 Advisory lock + kiểm tra BR-011 (RDS §III.3.1 SQL-1)

```sql
-- Idempotency: khóa tư vấn trong transaction (KHÔNG lưu ClientRequestId)
SELECT pg_advisory_xact_lock(hashtextextended(@lockKey, 0));

-- BR-011: chặn Resolve khi còn task con chưa xong
SELECT EXISTS (
    SELECT 1 FROM "Task" AS t
    WHERE t."TenantId" = @tenantId
      AND t."RequestId" = @requestId
      AND t."DeletedAt" IS NULL
      AND t."Status" NOT IN ('CONFIRMED'::task_state, 'COMPLETED'::task_state, 'CANCELLED'::task_state)
);
```
Sửa: `hashtext(...)` → `hashtextextended(..., 0)`; `task_status` → `task_state`; thêm `DeletedAt IS NULL`.

### 6.10 Giải quyết yêu cầu (RDS §III.3.1 SQL-2) — **sai tên bảng + 4 cột**

```sql
UPDATE "Request"
SET "Status" = 'RESOLVED'::request_state, "ResolvedAt" = @now, "UpdatedAt" = @now
WHERE "RequestId" = @requestId AND "TenantId" = @tenantId AND xmin = @expectedXmin;

INSERT INTO "RequestResolution" (
    "ResolutionId", "RequestId", "ResolverId", "Content", "RevisionNo", "CreatedAt"
) VALUES (
    @resolutionId, @requestId, @actorUserId, @content, @revisionNo, @now
);
```
Lưu ý: `RequestResolution` **không có** `TenantId`.

### 6.11 Nhận việc nguyên tử từ hàng đợi phòng ban (RDS §III.3.2 SQL-1, SDS §II.6) — **viết lại hoàn toàn**

Tài liệu đang `UPDATE "Task" SET "AssignedUserId"` — cột đó không tồn tại. SQL thật (`TaskAcceptanceStore.cs`):

```sql
-- Khóa người thực hiện
SELECT *, xmin FROM "User"
WHERE "TenantId" = @tenantId AND "UserId" = @actorId
  AND "Status" = 'ACTIVE' AND "DeletedAt" IS NULL
FOR SHARE;

-- Giành quyền sở hữu nguyên tử trên BẢN GHI PHÂN CÔNG
UPDATE "TaskAssignment"
SET "UserId" = @actorId, "AcceptedAt" = @now
WHERE "TaskAssignmentId" = @assignmentId
  AND "TaskId" = @taskId
  AND "AcceptedAt" IS NULL AND "RejectedAt" IS NULL AND "EndedAt" IS NULL
  AND ("UserId" = @actorId OR ("UserId" IS NULL AND "DepartmentId" = @actorDepartmentId));

UPDATE "Task"
SET "Status" = 'ACCEPTED'::task_state, "UpdatedAt" = @now
WHERE "TaskId" = @taskId AND "TenantId" = @tenantId AND xmin = @expectedXmin;

INSERT INTO "Confirmation" (
    "ConfirmationId","TenantId","ObjectType","ObjectId","MilestoneType",
    "ActorId","Decision","Note","ConfirmedAt"
) VALUES (
    @confirmationId, @tenantId, 'TASK', @taskId, @milestoneType,
    @actorId, @decision, @note, @now
);
```

### 6.12 Danh sách công việc theo phạm vi (RDS §III.3.2 SQL-2) — **viết lại phần JOIN**

Không có `Task.AssignedUserId`/`AssignedDepartmentId`/`ArchivedAt`; phải join `TaskAssignment` đang hiệu lực:

```sql
SELECT t."TaskId", t."Title", t."Status", t."Priority", t."Deadline",
       t."RequestId", ta."UserId" AS "AssignedUserId", u."FullName" AS "AssignedUserName",
       ta."DepartmentId" AS "AssignedDepartmentId", d."Name" AS "AssignedDepartmentName",
       t."CreatedAt"
FROM "Task" AS t
LEFT JOIN "TaskAssignment" AS ta ON ta."TaskId" = t."TaskId" AND ta."EndedAt" IS NULL
LEFT JOIN "User" AS u ON ta."UserId" = u."UserId"
LEFT JOIN "Department" AS d ON ta."DepartmentId" = d."DepartmentId"
WHERE t."TenantId" = @tenantId
  AND t."DeletedAt" IS NULL
  AND (
       @hasTenantRead = TRUE
    OR t."CreatorId" = @actorUserId
    OR ta."UserId" = @actorUserId
    OR ta."DepartmentId" = ANY(@managedDeptIds)
  )
ORDER BY
  CASE WHEN t."Status" = 'OVERDUE'::task_state THEN 0 ELSE 1 END ASC,
  t."Deadline" ASC NULLS LAST,
  t."CreatedAt" DESC
LIMIT @pageSize OFFSET @offset;
```

### 6.13 Hàng đợi phê duyệt (RDS §III.3.3 SQL-1)
Sửa tương tự 6.12: bỏ `t."ArchivedAt"` → `t."DeletedAt" IS NULL`; bỏ `t."SubmittedAt"` (không có cột) → sắp xếp theo `t."UpdatedAt" ASC`; `task_status` → `task_state`.

### 6.14 Tổng hợp tuân thủ SLA (RDS §III.3.4 SQL-1) — **sai 5 cột**

```sql
SELECT r."RequestId", r."ServiceId", s."Name" AS "ServiceName",
       r."Status", r."CreatedAt", r."ResolvedAt", r."ClosedAt",
       sv."TargetMinutes", sv."WarningMinutes"
FROM "Request" AS r
INNER JOIN "Service" AS s ON r."ServiceId" = s."ServiceId"
LEFT JOIN "SLAVersion" AS sv ON COALESCE(r."SLAVersionId", s."ActiveSLAVersionId") = sv."SLAVersionId"
WHERE r."TenantId" = @tenantId
  AND r."DeletedAt" IS NULL
  AND r."CreatedAt" >= @fromUtc AND r."CreatedAt" <= @untilUtc;
```
Bỏ: `r."SubmittedAt"`, `r."CurrentDepartmentId"` (bộ lọc theo phòng ban phải join `RequestRouting`); `s."SLAVersionId"` → `s."ActiveSLAVersionId"`.

### 6.15 Ghi vết AI (RDS §III.4.1 SQL-1) — **viết lại gần như toàn bộ**

```sql
INSERT INTO "AIInteraction" (
    "AIInteractionId", "TenantId", "UserId", "Feature", "ModelName",
    "InputRef", "OutputRef", "Status", "TokenUsage", "LatencyMs", "CreatedAt"
) VALUES (
    @interactionId, @tenantId, @userId, @feature, @modelName,
    @inputRef, @outputRef, @status, @tokenUsage, @latencyMs, @now
);

INSERT INTO "AIRecommendation" (
    "RecommendationId", "TenantId", "AIInteractionId", "RecommendationType",
    "ObjectType", "ObjectId", "PayloadJson", "Confidence",
    "HumanDecision", "DecisionBy", "DecidedAt"
) VALUES (
    @recommendationId, @tenantId, @interactionId, @recommendationType,
    @objectType, @objectId, @payloadJson::jsonb, @confidence,
    @humanDecision, @decisionBy, @decidedAt
);
```

### 6.16 Tự phản tỉnh + RAG (RDS §III.4.1 SQL-2)

```sql
SELECT "RecommendationType", "HumanDecision", COUNT(*) AS "Count"
FROM "AIRecommendation"
WHERE "TenantId" = @tenantId AND "HumanDecision" IS NOT NULL
GROUP BY "RecommendationType", "HumanDecision";

SELECT "MemoryId", "MemoryScope", "Title", "Content", "EmbeddingJson", "MetadataJson", "CreatedAt"
FROM "AIVectorMemory"
WHERE "TenantId" = @tenantId
  AND (@memoryScope IS NULL OR "MemoryScope" = @memoryScope)
ORDER BY "CreatedAt" DESC
LIMIT 250;
```
Bỏ ép kiểu `::ai_recommendation_decision` (enum không tồn tại); bỏ `DocumentType`, `SourceEntityId`, `ContentChunk`.

### 6.17 Báo cáo khối lượng công việc (RDS §III.4.2 SQL-1) — join lại qua TaskAssignment

```sql
SELECT u."UserId", u."EmployeeCode", u."FullName", u."DepartmentId", d."Name" AS "DepartmentName",
       COUNT(t."TaskId") AS "TotalAssigned",
       COUNT(t."TaskId") FILTER (WHERE t."Status" IN ('ASSIGNED','ACCEPTED','IN_PROGRESS','SUBMITTED')) AS "ActiveTasks",
       COUNT(t."TaskId") FILTER (WHERE t."Status" IN ('CONFIRMED','COMPLETED')) AS "CompletedTasks",
       COUNT(t."TaskId") FILTER (WHERE t."Status" = 'OVERDUE') AS "OverdueTasks"
FROM "User" AS u
LEFT JOIN "Department" AS d ON u."DepartmentId" = d."DepartmentId"
LEFT JOIN "TaskAssignment" AS ta ON ta."UserId" = u."UserId" AND ta."EndedAt" IS NULL
LEFT JOIN "Task" AS t ON t."TaskId" = ta."TaskId" AND t."TenantId" = u."TenantId" AND t."DeletedAt" IS NULL
WHERE u."TenantId" = @tenantId AND u."Status" = 'ACTIVE'::user_status AND u."DeletedAt" IS NULL
GROUP BY u."UserId", u."EmployeeCode", u."FullName", u."DepartmentId", d."Name"
ORDER BY "ActiveTasks" DESC, u."FullName" ASC;
```

### 6.18 Truy vấn nhật ký kiểm toán (RDS §III.4.3 SQL-1) — sửa 3 cột

```sql
SELECT a."AuditLogId", a."TenantId", a."ActorType", a."ActorId", a."Action",
       a."ObjectType", a."ObjectId", a."BeforeJson", a."AfterJson", a."MetadataJson", a."CreatedAt"
FROM "AuditLog" AS a
WHERE ((@isPlatformPlane = TRUE AND a."TenantId" IS NULL)
    OR (@isPlatformPlane = FALSE AND a."TenantId" = @tenantId))
  AND (@action IS NULL OR a."Action" = @action)
  AND (@actorType IS NULL OR a."ActorType" = @actorType::audit_actor_type)
  AND (@actorId IS NULL OR a."ActorId" = @actorId)
  AND (@objectType IS NULL OR a."ObjectType" = @objectType)
  AND (@objectId IS NULL OR a."ObjectId" = @objectId)
  AND (@fromUtc IS NULL OR a."CreatedAt" >= @fromUtc)
  AND (@untilUtc IS NULL OR a."CreatedAt" < @untilUtc)
ORDER BY a."CreatedAt" DESC, a."AuditLogId" DESC
LIMIT @pageSize OFFSET @offset;
```

### 6.19 Nhắc hạn đa kênh (SDS §II.10) — **viết lại**

```sql
SELECT s."Id", s."TenantId", s."TaskId", s."UserId", s."TriggerType", s."RemindAt", s."LeadTimeMinutes"
FROM "DeadlineReminderSchedule" AS s
WHERE s."Status" = @pendingStatus AND s."RemindAt" <= @now
ORDER BY s."RemindAt" ASC
LIMIT 200;

INSERT INTO "NotificationLog" (
    "Id", "TenantId", "TaskId", "UserId",
    "TriggerType", "ScheduledAt", "Channel", "Status", "ErrorMessage", "DeliveredAt"
) VALUES (
    @logId, @tenantId, @taskId, @userId,
    @triggerType, @scheduledAt, @channel, @status, @errorMessage, @now
);
```
Bỏ `IsTriggered`/`IsCancelled`/`ScheduledFor`/`RecipientUserId`/`DeliveryStatus`/`DispatchedAt`.
Mệnh đề `ON CONFLICT (...)` phải đối chiếu lại index thật trước khi ghi vào tài liệu.

### 6.20 Trigger immutability SLAVersion (SDS §II.4) — sửa tên cột

Trong thân `bizflow_sla_version_immutable()`: `"VersionNumber"` → `"VersionNo"`,
`"BusinessCalendarId"` → `"CalendarId"`, `"EscalationRules"` → `"EscalationConfigJson"`.

### 6.21 Trigger Attachment (SDS §II.9) — sửa tên cột

`NEW."FileSizeBytes"` → `NEW."SizeBytes"` (ngưỡng 524288000 giữ nguyên).

---

## 7. Việc còn lại cần tự đối chiếu

Những mục dưới đây tôi **chưa** xác minh, đừng chép nguyên từ tài liệu cũ:

1. **22 PL/pgSQL trigger** (SDS §I.2) — tên hàm/trigger và migration nguồn cần đọc lại từ các file migration.
2. **Unique index & check constraint** đầy đủ cho từng bảng (Phần 5 ở trên chỉ liệt kê cột).
3. **`ON CONFLICT`** của `NotificationLog` — cần xác nhận index thật.
4. **Số file** trong SDS §I.1 (47 Domain / 53 Application / 124 Infrastructure / 37 Api / 55 xUnit / 60 integration / 16 Playwright / 39 Vitest) — chưa đếm lại.
5. **Danh sách 45 mã quyền** — nên xuất thẳng từ migrations vào phụ lục thay vì ghi con số.
