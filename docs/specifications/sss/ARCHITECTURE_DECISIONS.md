# Hồ sơ Quyết định Kiến trúc Hệ thống (System Architecture Decisions)

- **Mã tài liệu:** `SSS-ADR-v1.0`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](SSS_v1.0_DRAFT.md) — Mục 21
- **Ngày lập:** `2026-10-18`
- **Cấu trúc tài liệu:**
  1. **Phần I:** `2` Quyết định Phạm vi Sản phẩm & Kiến trúc đã Xác nhận (`DEC-WS-001` .. `DEC-WS-002`)
  2. **Phần II:** `12` Quyết định Kiến trúc Nền tảng đã Xác nhận và Triển khai trong Repository (`ADR-0001` .. `ADR-0012`)
  3. **Phần III:** `6` Quyết định Kiến trúc Hệ thống Cấp SSS (`ADR-SSS-001` .. `ADR-SSS-006` — đánh dấu rõ `PROPOSED — NOT APPROVED` đối với các mục đề xuất lấp khoảng trống triển khai)

---

## Phần I: Các Quyết định Phạm vi Sản phẩm & Đa Thuê bao Đã Xác nhận (`CONFIRMED`)

| Mã Quyết định | Tiêu đề Quyết định | Nguồn Gốc | Trạng thái | Tóm tắt Nội dung & Hệ quả Kiến trúc | Hiện trạng trong Codebase |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `DEC-WS-001` | **Mô hình 1 Doanh nghiệp = 1 Workspace Chính trong MVP** (`Single Primary Workspace per Company Tenant in MVP`) | [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), [SRS.md](../../../documents/SRS/SRS.md), [SRS_v1.0_DRAFT.md](../../requirements/srs/SRS_v1.0_DRAFT.md) | `CONFIRMED` | Mỗi doanh nghiệp (`Tenant`) trong phạm vi MVP vận hành trên 1 không gian làm việc chính mặc định (`1 Company = 1 Primary Workspace`). Khả năng một doanh nghiệp tạo nhiều Workspace độc lập là định hướng nâng cấp Enterprise trong tương lai. | `IMPLEMENTED` (`TenantId` là khóa cô lập chính trên toàn bộ 36 bảng trong `BizFlowDbContext.cs`). |
| `DEC-WS-002` | **Loại trừ Thanh toán, Gói cước và Quản lý Thuê bao Thương mại khỏi MVP** (`Exclude Billing, Payment, Subscription & Commercial Entitlements from MVP`) | [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), [SRS.md](../../../documents/SRS/SRS.md), [SRS_v1.0_DRAFT.md](../../requirements/srs/SRS_v1.0_DRAFT.md) | `CONFIRMED` | Không xây dựng cổng thanh toán, hóa đơn, gói cước trả phí hay bộ giới hạn thương mại trong MVP. Việc cấp phát và kích hoạt Tenant được điều khiển thông qua quy trình phê duyệt của `Platform Super Admin`. | `IMPLEMENTED` (Không có bảng hay module Billing trong hệ thống). |

---

## Phần II: 12 Quyết định Kiến trúc Nền tảng Đã Xác nhận (`ADR-0001` .. `ADR-0012` — `CONFIRMED` & `IMPLEMENTED`)

| Mã ADR | Tiêu đề Quyết định | Nguồn Gốc | Trạng thái Kiến trúc | Quy tắc Thiết kế Cốt lõi | Bằng chứng Thực thi trong Codebase (`Current Repo`) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `ADR-0001` | **Hệ thống Thiết kế Giao diện GRID** (`GRID Design System & Typography Contract`) | [ADR-0001-design-system.md](../../01-decisions/ADR-0001-design-system.md) | `CONFIRMED` | Áp dụng ngôn ngữ thiết kế Swiss Grids (`Plus Jakarta Sans`, `JetBrains Mono`, bảng màu `#18181B`, `#2563EB`) thống nhất cho cả Platform Plane và Tenant Workspace Plane. | `IMPLEMENTED`: Tokens trong `docs/01-decisions/design-tokens/` và `frontend/src/`. |
| `ADR-0002` | **Lưu trữ Phiên Xác thực và Quy tắc `TenantId` của Platform Admin** (`Authentication Session Persistence & Platform Admin Null TenantId`) | [ADR-0002-authentication-persistence.md](../../01-decisions/ADR-0002-authentication-persistence.md) | `CONFIRMED` | Bổ sung bảng thứ 43 `AuthenticationSession` (`UserSession`) lưu băm refresh token/reset token để hỗ trợ xoay vòng token và phát hiện replay; cho phép `User.TenantId == null` duy nhất cho tài khoản `PLATFORM_ADMIN` (`IsPlatformAdministrator = true`). | `IMPLEMENTED`: [AuthenticationService.cs](../../../backend/BizFlow.Application/Authentication/AuthenticationService.cs), [IdentityModelConfiguration.cs](../../../backend/BizFlow.Infrastructure/Persistence/IdentityModelConfiguration.cs). |
| `ADR-0003` | **Thứ tự Ưu tiên Máy Trạng thái Công việc và Vòng lặp Từ chối Kết quả** (`Task Transition Precedence & Rejection Rework Loop`) | [ADR-0003-task-transition-precedence.md](../../01-decisions/ADR-0003-task-transition-precedence.md) | `CONFIRMED` | Giải quyết xung đột `B-01`/`C-02`: Ưu tiên máy trạng thái Task §9.1; cấm chuyển trực tiếp `OVERDUE -> SUBMITTED` (phải qua `OVERDUE -> IN_PROGRESS -> SUBMITTED`); khi người quản lý từ chối kết quả tại `SUBMITTED`, công việc quay lại `IN_PROGRESS` để làm lại. | `IMPLEMENTED`: [TaskWorkflowEngine.cs](../../../backend/BizFlow.Domain/Workflows/TaskWorkflowEngine.cs), [TaskConfirmationCommand.cs](../../../backend/BizFlow.Application/Tasks/TaskConfirmationCommand.cs). |
| `ADR-0004` | **Tính Bất biến của Bản chụp Phiên bản SLA và Lịch làm việc** (`SLA Snapshot and Business Calendar Immutability`) | [ADR-0004-sla-snapshot-immutability.md](../../01-decisions/ADR-0004-sla-snapshot-immutability.md) | `CONFIRMED` | Giải quyết `B-05`: Một khi `SLAVersion` hoặc `BusinessCalendar` đã được tạo và tham chiếu, dữ liệu của phiên bản đó là bất biến; mọi thay đổi về thời lượng mục tiêu hay lịch làm việc phải tạo phiên bản mới. | `IMPLEMENTED`: [SlaSnapshotConfiguration.cs](../../../backend/BizFlow.Infrastructure/Persistence/SlaSnapshotConfiguration.cs), [SlaVersionCreation.cs](../../../backend/BizFlow.Application/Sla/SlaVersionCreation.cs). |
| `ADR-0005` | **Hợp đồng Ngưỡng Cảnh báo SLA (`WarningMinutes`) và Cấu trúc JSON Lịch làm việc** (`SLA Warning Minutes & Calendar JSON Contract`) | [ADR-0005-sla-warning-calendar-contract.md](../../01-decisions/ADR-0005-sla-warning-calendar-contract.md) | `CONFIRMED` | Giải quyết `A-08`: `WarningMinutes` là số phút làm việc đã trôi qua tính từ lúc bắt đầu đồng hồ SLA (`0 <= WarningMinutes < TargetMinutes`); chuẩn hóa cấu trúc JSON cho `WorkingHoursJson` và `HolidaysJson`. | `IMPLEMENTED`: [SlaVersionCreation.cs](../../../backend/BizFlow.Application/Sla/SlaVersionCreation.cs), [SlaProfilesController.cs](../../../backend/BizFlow.Api/Controllers/SlaProfilesController.cs). |
| `ADR-0006` | **Hợp đồng Quy tắc Leo thang SLA (`EscalationRulesJson`)** (`SLA Escalation Rules Contract`) | [ADR-0006-sla-escalation-contract.md](../../01-decisions/ADR-0006-sla-escalation-contract.md) | `CONFIRMED` | Giải quyết `A-07`: Chuẩn hóa `EscalationRulesJson` với các cấp `level` tăng dần `1..N`, `afterMinutes` tăng nghiêm ngặt sau mốc `OVERDUE`, người nhận thuộc cùng Tenant đang hoạt động, chỉ thông báo một lần mỗi cấp và không tự động đổi chủ sở hữu công việc. | `IMPLEMENTED`: [SlaVersionCreation.cs](../../../backend/BizFlow.Application/Sla/SlaVersionCreation.cs). |
| `ADR-0007` | **Hợp đồng Truy vấn Lịch sử Phiên bản SLA** (`SLA Version History Endpoint Contract`) | [ADR-0007-sla-version-history.md](../../01-decisions/ADR-0007-sla-version-history.md) | `CONFIRMED` | Bổ sung endpoint `GET /api/v1/sla-profiles/{id}/versions` yêu cầu quyền `sla.read` để hiển thị lịch sử phiên bản SLA trên màn hình `UI-12`. | `IMPLEMENTED`: [SlaVersionHistory.cs](../../../backend/BizFlow.Application/Sla/SlaVersionHistory.cs), [SlaProfilesController.cs](../../../backend/BizFlow.Api/Controllers/SlaProfilesController.cs). |
| `ADR-0008` | **Phân quyền Cập nhật Dịch vụ (`service.update`) và Kiểm soát Tương tranh bằng ETag** (`Service Update Permission & If-Match Concurrency`) | [ADR-0008-service-update-permission.md](../../01-decisions/ADR-0008-service-update-permission.md) | `CONFIRMED` | Tách quyền `service.update` riêng cho `PUT /api/v1/services/{id}`, bắt buộc kiểm tra `If-Match` ETag để ngăn chặn ghi đè tương tranh khi nhiều quản trị viên cùng sửa cấu hình dịch vụ. | `IMPLEMENTED`: [ServiceMetadataUpdate.cs](../../../backend/BizFlow.Application/Services/ServiceMetadataUpdate.cs), [ServicesController.cs](../../../backend/BizFlow.Api/Controllers/ServicesController.cs). |
| `ADR-0009` | **Làm rõ Chuyển tiếp Trạng thái Yêu cầu (`RequestRecord`) khi Xác nhận hoặc Từ chối Kết quả** (`Request Transition Clarification`) | [ADR-0009-request-transition-clarification.md](../../01-decisions/ADR-0009-request-transition-clarification.md) | `CONFIRMED` | Giải quyết `B-02`/`C-01`: Khi người gửi từ chối kết quả tại `RESOLVED`, chuyển `RESOLVED -> IN_PROGRESS`; khi tiếp tục xử lý yêu cầu quá hạn, chuyển `OVERDUE -> IN_PROGRESS`; khi người gửi xác nhận hài lòng, chuyển tuần tự `RESOLVED -> CONFIRMED -> CLOSED` trong cùng một giao dịch. | `IMPLEMENTED`: [RequestConfirmationCommand.cs](../../../backend/BizFlow.Application/Requests/RequestConfirmationCommand.cs), [RequestConfirmationStore.cs](../../../backend/BizFlow.Infrastructure/Requests/RequestConfirmationStore.cs). |
| `ADR-0010` | **Ngữ nghĩa Nhận việc (`Claim` / `Accept`) đối với Công việc Giao cho Phòng ban** (`Department Task Acceptance Claim Semantics`) | [ADR-0010-department-task-acceptance-claim.md](../../01-decisions/ADR-0010-department-task-acceptance-claim.md) | `CONFIRMED` | Khi một `WorkTask` được giao cho phòng ban (`DepartmentId != null`, `UserId == null`), bất kỳ nhân viên đang hoạt động nào thuộc phòng ban đó khi gọi lệnh `accept` sẽ chiếm quyền sở hữu cá nhân nguyên tử một lần (`UserId = actorId`). | `IMPLEMENTED`: [TaskAcceptanceCommand.cs](../../../backend/BizFlow.Application/Tasks/TaskAcceptanceCommand.cs), [TaskAcceptanceStore.cs](../../../backend/BizFlow.Infrastructure/Tasks/TaskAcceptanceStore.cs). |
| `ADR-0011` | **Chính sách Phân quyền Đọc Công việc theo 4 Cấp Phạm vi** (`Scoped Task Read Authorization Policy`) | [ADR-0011-scoped-task-read-policy.md](../../01-decisions/ADR-0011-scoped-task-read-policy.md) | `CONFIRMED` | Thay thế quyền đọc đơn lẻ bằng 4 quyền đọc theo phạm vi: `tasks.read.tenant` (toàn công ty), `tasks.read.managed` (cây phòng ban quản lý theo `BR-003`), `tasks.read.own` (do mình tạo/giao), `tasks.read.assigned` (được giao cho mình hoặc hàng đợi phòng ban của mình) theo nguyên tắc hợp (`union-of-grants`). | `IMPLEMENTED`: [TaskListQuery.cs](../../../backend/BizFlow.Application/Tasks/TaskListQuery.cs), [TaskDetailQuery.cs](../../../backend/BizFlow.Application/Tasks/TaskDetailQuery.cs). |
| `ADR-0012` | **Hợp đồng Tạo Công việc, Tính Lũy đẳng (`Idempotency-Key`) và Ngữ nghĩa `404` Không Tiết lộ khi Đọc Chi tiết** (`Task Creation, Idempotency & Non-Revealing 404 Detail Contract`) | [ADR-0012-task-creation-and-detail-contract.md](../../01-decisions/ADR-0012-task-creation-and-detail-contract.md) | `CONFIRMED` | Chốt `TD-01..TD-04`: Task tạo mới dùng máy trạng thái chuẩn §9.1; hạn `Deadline` phải là mốc UTC tương lai; hỗ trợ `Idempotency-Key` qua `AuditLog`; `GET /api/v1/tasks/{id}` trả về `404 Not Found` (thay vì `403`) nếu bản ghi thuộc Tenant khác hoặc nằm ngoài phạm vi quyền đọc. | `IMPLEMENTED`: [TaskCreation.cs](../../../backend/BizFlow.Application/Tasks/TaskCreation.cs), [TaskDetailQuery.cs](../../../backend/BizFlow.Application/Tasks/TaskDetailQuery.cs), [TasksController.cs](../../../backend/BizFlow.Api/Controllers/TasksController.cs). |

---

## Phần III: Các Quyết định Kiến trúc Hệ thống Cấp SSS (`ADR-SSS-001` .. `ADR-SSS-006`)

Các quyết định dưới đây tổng hợp các cơ chế bảo vệ cấp hệ thống đã có trong codebase (`CONFIRMED` & `IMPLEMENTED`) và các đề xuất thiết kế để khép kín khoảng trống triển khai (`PROPOSED — NOT APPROVED`).

---

### `ADR-SSS-001`: Chiến lược Cô lập Đa Thuê bao 5 Lớp, Bộ lọc Truy vấn Fail-Closed và Chốt chặn Bất biến tại `BizFlowDbContext`
- **Trạng thái:** `CONFIRMED` & `IMPLEMENTED` ([BizFlowDbContext.cs](../../../backend/BizFlow.Infrastructure/Persistence/BizFlowDbContext.cs), [TaskWorkflowEngine.cs](../../../backend/BizFlow.Domain/Workflows/TaskWorkflowEngine.cs))
- **Quyết định:**
  1. Áp dụng mô hình phòng vệ chiều sâu 5 lớp (`5-Layer Defense-in-Depth`): (L1) Route Guard & `TenantMembershipAuthorizer`, (L2) Application Scoped Policy (`ADR-0011`/`ADR-0012`), (L3) EF Core Global Query Filter (`ConfigureTenantFilter<T>`), (L4) Persistence Write Guard trong `SaveChangesAsync`, (L5) Phân vùng khóa theo `TenantId` trên S3/MinIO, Redis, SignalR và `AiContextReader`.
  2. Bộ lọc EF Core bắt buộc hoạt động theo cơ chế **Fail-Closed**: `HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId)`.
  3. Tại `SaveChangesAsync`, cấm gọi `SaveChanges()` đồng bộ, cấm xóa cứng (`PERSISTENCE.HARD_DELETE_DENIED`), cấm sửa cột định danh bất biến (`PERSISTENCE.IMMUTABLE_FIELD`), và bắt buộc mọi thay đổi trạng thái `WorkTask` phải đi qua `TaskWorkflowEngine` (`ValidateTaskWorkflowMutations`).

---

### `ADR-SSS-002`: Kiến trúc Xử lý Bất đồng bộ qua Hàng đợi Bền vững và Tiến trình Background Worker (`MOD-BG-001`)
- **Trạng thái:** `PROPOSED — NOT APPROVED` (Theo dõi tại `OD-020`)
- **Quyết định Đề xuất (`PROPOSED — NOT APPROVED`):** Sử dụng Hangfire hoặc `.NET BackgroundService` có điều khiển bằng cờ cấu hình (`BackgroundJobs:Enabled`) để thực thi định kỳ việc đánh giá đồng hồ SLA (`SLAState`/`SLAEvent`), gửi email từ hàng đợi và xử lý các tác vụ nền mà không làm tăng độ trễ của luồng HTTP chính.

---

### `ADR-SSS-003`: Hoàn thiện Mô hình Thực thi Định tuyến Yêu cầu (`RoutingRule`) và Phê duyệt Đa bước (`ApprovalInstance`)
- **Trạng thái:** `PROPOSED — NOT APPROVED` (Đề xuất thiết kế khép kín khoảng trống 5 bảng chưa có trong `BizFlowDbContext` theo `OD-001` và `OD-002`)
- **Quyết định Đề xuất (`PROPOSED — NOT APPROVED`):** Bổ sung 5 thực thể `RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance` vào `BizFlow.Domain` và `BizFlowDbContext`, áp dụng đầy đủ `ConfigureTenantFilter<T>`, chống xóa cứng và ghi nhận `RequestHistory` + `AuditLog` nguyên tử.

---

### `ADR-SSS-004`: Cơ chế Lưu trữ Trạng thái Đồng hồ SLA (`SLAState`, `SLAEvent`) và Quy tắc Tạm dừng theo Lịch làm việc
- **Trạng thái:** `PROPOSED — NOT APPROVED` (Đề xuất thiết kế khép kín khoảng trống 2 bảng SLA runtime theo `OD-003` và `OD-004`)
- **Quyết định Đề xuất (`PROPOSED — NOT APPROVED`):** Bổ sung 2 bảng `SLAStates` và `SLAEvents` vào `BizFlowDbContext` thuộc quyền sở hữu của `MOD-008` để lưu trạng thái đồng hồ SLA (`Running`, `Paused`, `Met`, `Breached`) và lịch sử các mốc cảnh báo/leo thang theo đúng quy tắc của `ADR-0005` và `ADR-0006`.

---

### `ADR-SSS-005`: Kiến trúc Trừu tượng hóa Lưu trữ Tệp Đính kèm và Bằng chứng Hoàn thành (`S3` / `MinIO` + `Local` Fallback)
- **Trạng thái:** `CONFIRMED` & `IMPLEMENTED` ([S3CompatibleStorageProvider.cs](../../../backend/BizFlow.Infrastructure/Collaboration/S3CompatibleStorageProvider.cs) và [LocalStorageProvider.cs](../../../backend/BizFlow.Infrastructure/Collaboration/LocalStorageProvider.cs))
- **Quyết định:** Tự động chọn `S3CompatibleStorageProvider` khi `Storage:Provider` là `S3` hoặc `MinIO`, và chuyển về `LocalStorageProvider` khi `Storage:Provider` là `Local`; thực thi tải lên 3 bước giới hạn 500 MB kèm kiểm tra SHA-256 và cô lập đường dẫn theo `TenantId`.

---

### `ADR-SSS-006`: Chuẩn hóa Từ vựng Mã Quyền hạn (Permission Codes) giữa Đặc tả Hệ thống, Frontend Router và Backend Seed
- **Trạng thái:** `PROPOSED — NOT APPROVED` (Theo dõi tại `OD-019`)
- **Quyết định Đề xuất (`PROPOSED — NOT APPROVED`):** Chọn tập mã quyền đang hoạt động thực tế trong Backend & [app.routes.ts](../../../frontend/src/app/app.routes.ts) (`roles.configure`, `workflows.read`, `platform.company-registrations.read`, `platform.audit.read`, `reports.manager`, `reports.company`, `reports.workload`, `reports.sla`) làm chuẩn chính thức trong SSS để đồng bộ hóa hoàn toàn giữa tài liệu và mã nguồn.
