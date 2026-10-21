# Hồ sơ Quyết định Kiến trúc Hệ thống (System Architecture Decisions)

- **Mã tài liệu:** `SSS-ADR-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](SSS_v1.0_DRAFT.md) — Mục 21
- **Ngày lập:** `2026-10-18` (Cập nhật đồng bộ v1.2: `2026-10-21`)
- **Cấu trúc tài liệu:**
  1. **Phần I:** `2` Quyết định Phạm vi Sản phẩm & Kiến trúc đã Xác nhận (`DEC-WS-001` .. `DEC-WS-002`)
  2. **Phần II:** `12` Quyết định Kiến trúc Nền tảng đã Xác nhận và Triển khai trong Repository (`ADR-0001` .. `ADR-0012`)
  3. **Phần III:** `10` Quyết định Kiến trúc Hệ thống Cấp SSS (`ADR-SSS-001` .. `ADR-SSS-010` — phân định rõ ràng giữa các cơ chế đã xác nhận `CONFIRMED` và các đề xuất `PROPOSED — NOT APPROVED`)

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

---

### `ADR-SSS-007`: Tính Nguyên tử Giao dịch, Kiểm tra Bất biến Sau Khởi tạo và Rollback Khép kín khi Cấp phát Tenant / Workspace (`Transactional Provisioning & Post-Provisioning Invariant Guard`)
- **Trạng thái:** `PROPOSED — NOT APPROVED` (Theo dõi tại `OD-021`, `OD-024`, `BR-023`)
- **Nguồn phát sinh:** Rà soát Onboarding & Workspace Provisioning; đối chiếu `IdentityModelConfiguration.cs` (`Tenant.CompanyId` unique index, `CK_Role_Scope`) và `BizFlowDbContext.cs`.
- **Quyết định Đề xuất (`PROPOSED — NOT APPROVED`):**
  1. Toàn bộ chuỗi 5 bước provisioning: (1) `Company` activation (`ACTIVE`), (2) `Primary Tenant / Workspace` creation (ràng buộc `Unique(CompanyId)` theo `DEC-WS-001`), (3) `TenantSettings` & `BusinessCalendar` seeding, (4) Initial Representative `UserAccount` creation (`Status = Active`), (5) Initial `COMPANY_ADMIN` role assignment + `AuditLog` bắt buộc phải được đóng gói trong **một giao dịch cơ sở dữ liệu ACID duy nhất** (`All-or-Nothing Unit of Work`).
  2. Bổ sung bước kiểm tra bất biến sau khởi tạo (**Post-Provisioning Invariant Guard**): Trước khi commit giao dịch, hệ thống bắt buộc kiểm tra điều kiện bất biến: Tồn tại đúng `01 Primary Tenant (ACTIVE)` gắn với `CompanyId` và tồn tại ít nhất `01 UserAccount (ACTIVE)` thuộc `Tenant` đó được gán vai trò `COMPANY_ADMIN`. Nếu bất kỳ bước nào lỗi hoặc invariant check thất bại, hệ thống tự động **Rollback 100%**, giữ đơn đăng ký ở trạng thái an toàn (`PENDING`), trả về mã lỗi `500 ONBOARDING.PROVISIONING_ROLLED_BACK` kèm `CorrelationId`, không tạo bản ghi mồ côi.
  3. Tác vụ gửi email thông báo kích hoạt/xác minh được tách khỏi giao dịch DB (qua Transactional Outbox hoặc Message Queue) để lỗi mạng/SMTP không làm rollback giao dịch hợp lệ.

---

### `ADR-SSS-008`: Kiến trúc Xác thực Đa Chế độ Hợp nhất, Đăng nhập Không Mật khẩu qua Email Link & Bộ chọn Workspace (`Unified Multi-Mode Authentication & Passwordless Email-Link Architecture with Workspace Selector Modal`)
- **Trạng thái:** `CONFIRMED` (Theo `BR-025`, `OD-025`, `FR-AUTH-001`, `FR-AUTH-005`, `IR-UI-005`, `IR-API-004`)
- **Nguồn phát sinh:** Rà soát yêu cầu xác thực hợp nhất và trải nghiệm người dùng đa doanh nghiệp.
- **Quyết định Thiết kế:**
  1. **Trang đăng nhập hợp nhất (`/login`, `UI-01`):** Cung cấp 2 lựa chọn độc lập trên cùng một giao diện:
     - **Lựa chọn 1 (Đăng nhập bằng Định danh & Mật khẩu):** Yêu cầu chính xác 3 trường bắt buộc: `ID / Mã nhân viên / Tên đăng nhập`, `Mật khẩu`, và `Mã Workspace / Tenant Key`. Backend xác thực đồng thời cả 3 trường trong cùng 1 câu truy vấn; thiếu hoặc sai bất kỳ trường nào đều trả về mã lỗi tổng quát `401 AUTH.INVALID_CREDENTIALS` kèm gợi ý liên hệ Admin để chống tấn công dò quét tài khoản (`SEC-001`).
     - **Lựa chọn 2 (Đăng nhập Không Mật khẩu qua Liên kết Email):** Người dùng chỉ cần nhập duy nhất địa chỉ `Email`. Tuyệt đối không yêu cầu mật khẩu và không yêu cầu mã Workspace ở bước gửi yêu cầu.
  2. **Vòng đời Mã Xác thực Email Link (`EmailLoginToken`):** Khi người dùng gửi yêu cầu, hệ thống tạo một token mật mã ngẫu nhiên bảo mật cao (HMAC-SHA256, thời gian sống `TTL = 15 phút`, sử dụng một lần duy nhất). Token được gửi tới email của người dùng qua dịch vụ thư giao dịch (`IR-EXT-002`).
  3. **Quy trình Định tuyến Không gian Làm việc (`Workspace Routing Flow`):**
     - Khi người dùng nhấp vào liên kết trong email (`/auth/verify-link?token=...`) hoặc gọi `POST /api/v1/auth/email-link/verify`:
     - **Trường hợp 0 Workspace:** Nếu địa chỉ email không thuộc bất kỳ Tenant nào đang hoạt động (`ACTIVE`), hệ thống từ chối đăng nhập với thông báo lỗi rõ ràng kèm liên kết điều hướng trực tiếp sang màn hình đăng ký doanh nghiệp mới `/register-company` (`IR-UI-004`).
     - **Trường hợp 1 Workspace:** Hệ thống tự động xác thực, cấp phát phiên làm việc (JWT Access Token + Refresh Token) và điều hướng thẳng vào Dashboard của Workspace chính đó.
     - **Trường hợp $\ge$ 2 Workspaces:** Hệ thống trả về token phiên tạm thời và danh sách các Workspace hợp lệ (`WorkspaceId`, `WorkspaceName`, `TenantKey`, `RoleName`). Giao diện Frontend hiển thị ngay `Modal Chọn Không Gian Làm Việc` (`Workspace Selector Modal` — `IR-UI-005`). Người dùng chọn Workspace đích, Frontend gửi `POST /api/v1/auth/email-link/select-workspace` để hoàn tất phát hành JWT phiên chính thức.

---

### `ADR-SSS-009`: Trợ lý Robot Thông minh Nhúng UI, Giới hạn Quyền Hạn Thực Tế và Quy trình Bóc tách Việc Có Xác nhận Con người Bắt buộc (`Interactive Embedded Cute Robot Chatbot, Scoped Natural Language Assistance & Mandatory Explicit Human Confirmation AI Workflow`)
- **Trạng thái:** `CONFIRMED` (Theo `BR-027`, `BR-028`, `BR-029`, `OD-028`, `OD-029`, `OD-030`, `FR-AI-011`..`013`, `IR-API-006`, `DR-016`)
- **Nguồn phát sinh:** Yêu cầu nâng cao trải nghiệm tác nghiệp thông minh thông qua Trợ lý Robot nhúng UI và tự động hóa bóc tách việc.
- **Quyết định Thiết kế:**
  1. **Vòng đời Widget Robot Trợ lý Nhúng UI (`Interactive Cute Robot Widget Lifecycle`):**
     - Widget robot thông minh được nhúng sẵn ở góc dưới bên phải giao diện ứng dụng và **mặc định mở** sau khi người dùng đăng nhập thành công.
     - Widget cung cấp nút thu nhỏ/đóng [X] ở góc trên bên phải. Khi người dùng bấm [X], widget được ẩn đi và lưu trạng thái vào `sessionStorage` của trình duyệt (`sessionStorage.setItem('bizflow_robot_dismissed', 'true')`).
     - **Quy tắc phiên (`BR-029`):** Trạng thái đóng chỉ có hiệu lực trong phạm vi phiên làm việc hiện tại của trình duyệt. Khi người dùng đăng xuất và đăng nhập lại, hoặc mở phiên làm việc mới trên tab/cửa sổ mới, widget trợ lý robot tự động mở lại ở trạng thái sẵn sàng đón chào.
     - Lịch sử hội thoại được duy trì tại bảng `RobotConversation` và `RobotMessage` (`DR-016`), phân vùng theo `TenantId` và `UserId`.
  2. **Cơ chế Vấn tin Tiến độ & Báo cáo Tự nhiên Ràng buộc Theo Quyền Hạn (`Scoped Natural Language Queries`):**
     - Trợ lý robot tiếp nhận các câu hỏi tự nhiên về tiến độ, hạn chót, danh sách công việc trễ hạn, việc chờ xử lý/phê duyệt, và phân bổ công việc phòng ban.
     - Tầng `AiContextReader` bắt buộc lọc dữ liệu đầu vào theo đúng `CurrentTenantId`, `CurrentUserId`, các quyền RBAC cụ thể và phạm vi quản lý `ManagementScope` của người dùng (`BR-003`, `ADR-0011`).
     - **Chốt chặn chống ảo giác (`Anti-Hallucination Guardrail` — `BR-027`):** AI chỉ được phép tổng hợp và trả lời dựa trên tập dữ liệu ngữ cảnh thực tế đã được lọc quyền. Tuyệt đối không được suy diễn số liệu hoặc hiển thị thông tin công việc, nhân sự thuộc phạm vi ngoài quyền của người dùng. Nếu không tìm thấy dữ liệu phù hợp trong ngữ cảnh, bot phản hồi thông báo rõ ràng "Tôi không tìm thấy dữ liệu công việc trong phạm vi được phân quyền của bạn".
  3. **Lệnh Điều khiển Slash Commands (`/task` & `/notifications`):**
     - Hỗ trợ cú pháp lệnh nhanh ngay trong khung chat: `/task <đoạn văn bản mô tả>` và `/notifications`.
  4. **Quy trình Bóc tách Việc AI với Xác nhận Con người Bắt buộc (`AI Task Decomposition & Mandatory Human Confirmation` — `BR-028`):**
     - Khi người dùng nhập đoạn văn bản mô tả công việc (qua chat hoặc `/task`), dịch vụ AI phân tích đoạn văn và bóc tách thành danh sách các đầu việc có cấu trúc: phân loại rõ `Support Request` (Yêu cầu Hỗ trợ Dịch vụ) hay `Task Assignment` (Giao việc Trực tiếp), tiêu đề, phòng ban nhận việc, nội dung chi tiết và checklist các bước thực hiện.
     - Giao diện Frontend hiển thị bảng xem trước tương tác (`Interactive Preview Checklist Modal/Panel`): Người dùng có thể chọn hoặc bỏ chọn từng đầu việc (checkbox), sửa đổi tiêu đề, điều chỉnh phòng ban nhận việc, hoặc xóa các đầu việc không phù hợp.
     - **Bất biến Kiến trúc Bắt buộc (`BR-028`): AI TUYỆT ĐỐI KHÔNG ĐƯỢC GHI TRỰC TIẾP VÀO CƠ SỞ DỮ LIỆU KHI CHƯA CÓ SỰ XÁC NHẬN CỦA CON NGƯỜI.**
     - Chỉ khi người dùng nhấp vào nút "Xác nhận tạo việc" (`Confirm Create Tasks`), Frontend mới gửi yêu cầu tạo hàng loạt `POST /api/v1/tasks/ai-decomposition/confirm` (`IR-API-006`).
     - Backend kiểm tra thẩm quyền tạo việc/yêu cầu của người dùng đối với từng phòng ban mục tiêu (`TenantMembershipAuthorizer`, `tasks.create`, `requests.create`). Toàn bộ danh sách công việc được phê duyệt được ghi vào cơ sở dữ liệu trong cùng một giao dịch an toàn kèm ghi nhận `AuditLog`.

---

### `ADR-SSS-010`: Kiến trúc Phát Thông báo Nhắc Hạn Đa Kênh: In-App SignalR, Web Push VAPID & Fallback Email Tự động với Bộ Lọc Chống Trùng lặp (`Multi-Channel Deadline Notification Dispatching: In-App SignalR, Web Push & Email Fallback with Deduplication Guard`)
- **Trạng thái:** `CONFIRMED` (Theo `BR-030`, `OD-031`, `OD-032`, `FR-NOTIF-003`, `FR-NOTIF-004`, `IR-API-007`, `IR-EXT-003`, `DR-017`)
- **Nguồn phát sinh:** Yêu cầu nhắc nhở hạn chót công việc kịp thời, giảm thiểu tỷ lệ trễ hạn và tối ưu hóa tỷ lệ tiếp cận thông báo đối với nhân viên không thường trực trên ứng dụng.
- **Quyết định Thiết kế:**
  1. **Cấu hình Mốc Nhắc Hạn Cá nhân hóa (`Configurable Personalized Reminders`):**
     - Hệ thống hỗ trợ người dùng cấu hình lịch nhắc hạn linh hoạt cho từng công việc hoặc thiết lập mặc định cá nhân: trước 24 giờ, 4 giờ, 1 giờ và ngay khi quá hạn (`OVERDUE`).
     - Cấu hình có thể thực hiện trên giao diện chi tiết công việc hoặc qua lệnh điều khiển `/notifications` trên widget trợ lý robot (`FR-NOTIF-003`).
     - Dữ liệu lịch nhắc được lập chỉ mục trong bảng `DeadlineReminderSchedule` (`DR-017`).
  2. **Cơ chế Điều phối Đa Kênh Thông minh (`Smart Multi-Channel Notification Dispatcher` — `BR-030`):**
     - Tiến trình nền `DeadlineReminderWorker` (`MOD-015` / `MOD-BG-001`) định kỳ quét các mốc nhắc hạn đến kỳ phát.
     - **Kênh 1 — In-App SignalR (Ưu tiên cao nhất khi Online):** Hệ thống kiểm tra trạng thái kết nối trực tuyến của người dùng tại `NotificationHub` (`INT-012`). Nếu người dùng đang mở ứng dụng, hệ thống lập tức phát Toast thông báo nổi trên màn hình và cập nhật số lượng badge thông báo chưa đọc.
     - **Kênh 2 — Web Push qua Trình duyệt (Khi Offline / Không mở tab):** Nếu người dùng không kết nối SignalR, hệ thống kiểm tra bảng `PushSubscription` của người dùng. Nếu có đăng ký Web Push hợp lệ (chuẩn W3C Push API / RFC 8292 VAPID), hệ thống gửi thông báo đẩy trực tiếp tới trình duyệt máy tính hoặc thiết bị di động của người dùng (`IR-EXT-003`).
     - **Kênh 3 — Fallback Email Tự động:** Nếu người dùng chưa cấp quyền Web Push hoặc tiến trình gửi Web Push trả về lỗi (hết hạn subscription `410 Gone` hoặc lỗi mạng), hệ thống tự động kích hoạt kênh dự phòng gửi Email thông báo HTML chuyên nghiệp chứa đầy đủ thông tin: Tiêu đề công việc, Hạn chót còn lại, Người giao việc, và nút CTA đưa thẳng vào chi tiết công việc trong hệ thống.
  3. **Bộ Lọc Chống Trùng lặp Tuyệt đối (`Deduplication & Cancellation Guard` — `OD-032`):**
     - Bảng `NotificationLog` áp dụng ràng buộc duy nhất kết hợp: `UNIQUE (TenantId, TaskId, UserId, TriggerType, ScheduledAt)`. Bất kỳ yêu cầu phát trùng lặp nào cho cùng một mốc nhắc hạn của cùng một công việc đều bị loại bỏ ở tầng lưu trữ.
     - **Cơ chế Hủy tự động khi Hoàn tất Công việc:** Khi một `WorkTask` chuyển sang trạng thái hoàn thành (`SUBMITTED`, `CONFIRMED` hoặc `CLOSED`), hệ thống tự động cập nhật trạng thái của tất cả các bản ghi nhắc hạn chưa phát thuộc công việc đó trong `DeadlineReminderSchedule` thành `CANCELLED`, ngăn chặn tuyệt đối tình trạng gửi thông báo nhắc việc cho công việc đã hoàn thành.

