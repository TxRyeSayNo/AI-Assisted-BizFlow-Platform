# Ma trận Truy vết Kiến trúc Hệ thống Toàn diện (SSS Bidirectional Traceability Matrix)

- **Mã tài liệu:** `SSS-TRC-v1.1`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](SSS_v1.0_DRAFT.md) — Mục 23
- **Liên kết ngược tới SRS:** [SRS_v1.0_DRAFT.md](../../requirements/srs/SRS_v1.0_DRAFT.md), [REQUIREMENTS_TRACEABILITY.md](../../requirements/srs/REQUIREMENTS_TRACEABILITY.md), [REQUIREMENTS_CATALOG.csv](../../requirements/srs/REQUIREMENTS_CATALOG.csv)
- **Ngày cập nhật:** `2026-10-19` (`v1.1-DRAFT`)
- **Độ phủ truy vết (100%):**
  - `8/8` Mục tiêu Kinh doanh (`BG-01..08`) & `24/24` Quy tắc Nghiệp vụ (`BR-001..024`) (bổ sung `BR-023`, `BR-024`)
  - `118/118` Yêu cầu SRS (`63 FR`, `13 IR`, `15 DR`, `15 NFR`, `12 SEC`) khớp chính xác 1-1 với `REQUIREMENTS_CATALOG.csv`
  - `15/15` Module Hệ thống (`MOD-001..013`, `MOD-FE-001`, `MOD-BG-001`)
  - `28/28` Giao diện Hệ thống (`INT-001..028`)
  - `7/7` Luồng Tuần tự Trọng yếu (`SEQ-001..007`, bổ sung `SEQ-007`)
  - `10/10` Chiến thuật Chất lượng Kiến trúc (`ARCH-*`) & `21/21` Quyết định Kiến trúc (`DEC-WS-*`, `ADR-*`, `ADR-SSS-*`, bổ sung `ADR-SSS-007`)
  - `31/31` Kịch bản Kiểm thử Nghiệm thu (`TST-001..031`, bổ sung `TST-031`)

---

## 1. Ma trận Truy vết Yêu cầu Chức năng (`FR` — `63` Yêu cầu) sang Kiến trúc SSS và Kiểm thử

| Mã Yêu cầu SRS | Module SRS / SSS | Mục tiêu & Quy tắc (`BG`/`BR`) | Giao diện (`INT-*`) | Luồng Runtime & ADR (`SEQ-*` / `ADR-*`) | Trạng thái Triển khai (`Repo Fact`) | Kịch bản Kiểm thử (`TST-*`) |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| `FR-AUTH-001` | `M01` / `MOD-001`, `MOD-FE-001` | `BG-08`, `BR-001` | `INT-001`, `INT-015` | `SEQ-001`, `ADR-0002` | `IMPLEMENTED` (`AuthenticationController.cs`) | `TST-001`, `TST-002` |
| `FR-AUTH-002` | `M01` / `MOD-001`, `MOD-FE-001` | `BG-08`, `BR-001` | `INT-001`, `INT-015` | `SEQ-001`, `ADR-0002` | `IMPLEMENTED` (`AuthenticationController.cs`, `AuthenticationService.cs`) | `TST-001`, `TST-002` |
| `FR-AUTH-003` | `M01` / `MOD-001`, `MOD-009` | `BG-08`, `BR-001` | `INT-001`, `INT-020`, `INT-026` | `ADR-0002` | `IMPLEMENTED` (`PasswordResetController.cs`, `PasswordResetService.cs`) | `TST-002` |
| `FR-AUTH-004` | `M01` / `MOD-001`, `MOD-003` | `BG-08`, `BR-001` | `INT-001`, `INT-004` | `ADR-0002` | `PARTIAL` (Đã có đổi mật khẩu/reset; chưa mở luồng Admin cấp mật khẩu tạm buộc đổi lần đầu) | `TST-002` |
| `FR-TEN-001` | `M02` / `MOD-002`, `MOD-FE-001` | `BG-08`, `BR-001` | `INT-002` | `DEC-WS-001`, `DEC-WS-002` | `NOT_IMPLEMENTED` (Đã có thực thể & `GET` queue; chưa mở `POST` nộp đơn công khai) | `TST-003` |
| `FR-TEN-002` | `M02` / `MOD-002`, `MOD-001`, `MOD-003` | `BG-08`, `BR-001`, `BR-002` | `INT-002`, `INT-020` | `DEC-WS-001`, `DEC-WS-002` | `NOT_IMPLEMENTED` (Chưa mở endpoint duyệt & khởi tạo Tenant tự động) | `TST-003` |
| `FR-TEN-003` | `M02` / `MOD-002` | `BG-08`, `BR-001` | `INT-002`, `INT-015` | `SEQ-001`, `SEQ-005` | `NOT_IMPLEMENTED` (Chưa mở endpoint đổi trạng thái Tenant; tầng đọc đã chặn Tenant không active) | `TST-004` |
| `FR-TEN-004` | `M02` / `MOD-002`, `MOD-001..013` | `BG-08`, `BR-001`, `BR-002` | `INT-015`, `INT-022` | `SEQ-005`, `ADR-SSS-001` | `IMPLEMENTED` (`ClaimsTenantContext.cs`, `TenantMembershipAuthorizer.cs`, `BizFlowDbContext.cs`) | `TST-004`, `TST-029` |
| `FR-TEN-005` | `M02` / `MOD-002`, `MOD-001`, `MOD-FE-001` | `BG-08`, `BR-001`, `BR-023`, `BR-024` | `INT-002`, `INT-026` | `SEQ-007`, `ADR-SSS-007`, `DEC-WS-001` | `NOT_IMPLEMENTED` (Đề xuất tự phục vụ; chưa mở endpoint đăng ký/kích hoạt tự động — `OD-021..024`) | `TST-031` |
| `FR-ORG-001` | `M03` / `MOD-003`, `MOD-FE-001` | `BG-07`, `BR-003` | `INT-004` | `ADR-0011` | `NOT_IMPLEMENTED` (Đã có `GET /api/v1/departments`; chưa mở API tạo/cập nhật phòng ban) | `TST-005` |
| `FR-ORG-002` | `M03` / `MOD-003`, `MOD-001` | `BG-07`, `BR-003` | `INT-004` | `ADR-0002` | `NOT_IMPLEMENTED` (Đã có `GET /api/v1/users`; chưa mở API tạo/vô hiệu hóa người dùng) | `TST-005` |
| `FR-ORG-003` | `M03` / `MOD-003` | `BG-07`, `BR-003` | `INT-004` | `ADR-0011` | `NOT_IMPLEMENTED` (Đã có bảng `ManagementScope` & `Department`; chưa mở API gán trưởng phòng) | `TST-005` |
| `FR-ORG-004` | `M03` / `MOD-003`, `MOD-FE-001` | `BG-06`, `BG-08`, `BR-004` | `INT-004`, `INT-015` | `SEQ-001`, `ADR-0011`, `ADR-SSS-006` | `PARTIAL` (Đã thực thi kiểm tra RBAC & phạm vi; chưa mở đầy đủ API gán lại vai trò người dùng) | `TST-006` |
| `FR-ORG-005` | `M03` / `MOD-003`, `MOD-FE-001` | `BG-06`, `BR-004` | `INT-004` | `ADR-SSS-006` | `IMPLEMENTED` (`RolesController.cs`, `RoleManagement.cs`) | `TST-006` |
| `FR-SVC-001` | `M04` / `MOD-004`, `MOD-FE-001` | `BG-01`, `BR-005` | `INT-005` | `SEQ-002`, `ADR-0008` | `IMPLEMENTED` (`ServicesController.cs`, `ServiceCatalog.cs`, `ServiceMutations.cs`) | `TST-007` |
| `FR-SVC-002` | `M04` / `MOD-004`, `MOD-007` | `BG-01`, `BG-02`, `BR-007` | `INT-005`, `INT-006` | `SEQ-002`, `ADR-SSS-003` | `NOT_IMPLEMENTED` (Chưa có bảng `RoutingRule` trong DB — `OD-001`) | `TST-007`, `TST-008` |
| `FR-SVC-003` | `M04` / `MOD-004`, `MOD-007` | `BG-02`, `BR-006` | `INT-005`, `INT-006` | `SEQ-002` | `NOT_IMPLEMENTED` (Chưa mở endpoint gắn `WorkflowVersion` đã phát hành vào dịch vụ) | `TST-007`, `TST-008` |
| `FR-SVC-004` | `M04` / `MOD-004`, `MOD-007` | `BG-02`, `BR-008` | `INT-005`, `INT-006` | `SEQ-002`, `ADR-SSS-003` | `NOT_IMPLEMENTED` (Chưa có bảng `ApprovalRule` trong DB — `OD-002`) | `TST-007`, `TST-009` |
| `FR-TASK-001` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-010` | `INT-009`, `INT-016` | `SEQ-004`, `ADR-0012` | `IMPLEMENTED` (`TasksController.cs`, `TaskCreation.cs`) | `TST-010` |
| `FR-TASK-002` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-011` | `INT-009`, `INT-016` | `SEQ-004`, `ADR-0010` | `IMPLEMENTED` (`TaskAssignmentCommand.cs`, `TaskAssignmentStore.cs`) | `TST-010`, `TST-011` |
| `FR-TASK-003` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-011` | `INT-009`, `INT-016` | `SEQ-004`, `ADR-0010` | `PARTIAL` (`TaskAcceptanceCommand.cs` đã hỗ trợ nhận việc/claim; chưa mở endpoint từ chối nhận việc) | `TST-011` |
| `FR-TASK-004` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-010` | `INT-009`, `INT-016` | `SEQ-004`, `ADR-0003` | `IMPLEMENTED` (`TaskExecutionCommand.cs`, `TaskWorkflowEngine.cs`) | `TST-012` |
| `FR-TASK-005` | `M05` / `MOD-005` | `BG-03`, `BR-010` | `INT-009` | `SEQ-004` | `PARTIAL` (Đã có thực thể báo cáo tiến độ; chưa mở đầy đủ endpoint cập nhật % tiến độ) | `TST-012` |
| `FR-TASK-006` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-012` | `INT-009`, `INT-016` | `ADR-0003` | `PARTIAL` (Máy trạng thái `TaskWorkflowEngine.cs` đã hỗ trợ `BLOCKED`; đang hoàn thiện API quản lý blocker) | `TST-012` |
| `FR-TASK-007` | `M05` / `MOD-005`, `MOD-007`, `MOD-010` | `BG-03`, `BR-013`, `BR-017` | `INT-009`, `INT-010`, `INT-016` | `SEQ-004`, `ADR-0003` | `IMPLEMENTED` (`TaskSubmissionCommand.cs`, `TaskEvidenceConfiguration.cs`) | `TST-013` |
| `FR-TASK-008` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-013` | `INT-009`, `INT-016` | `SEQ-004`, `ADR-0003` | `IMPLEMENTED` (`TaskConfirmationCommand.cs`, `ConfirmationConfiguration.cs`) | `TST-013` |
| `FR-TASK-009` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-011` | `INT-009`, `INT-016` | `ADR-0003` | `NOT_IMPLEMENTED` (Chưa mở endpoint giao lại công việc đang chạy `Reassign`) | `TST-011` |
| `FR-TASK-010` | `M05` / `MOD-005`, `MOD-007` | `BG-03`, `BR-010`, `BR-022` | `INT-009`, `INT-016` | `ADR-0003` | `NOT_IMPLEMENTED` (Máy trạng thái đã có `CANCELLED`; chưa mở endpoint HTTP hủy Task) | `TST-012` |
| `FR-REQ-001` | `M06` / `MOD-006`, `MOD-004`, `MOD-FE-001` | `BG-01`, `BR-005`, `BR-009` | `INT-005`, `INT-008` | `SEQ-002` | `IMPLEMENTED` (`RequestsController.cs`, `RequestCreation.cs`, `RequestSubmission.cs`) | `TST-014` |
| `FR-REQ-002` | `M06` / `MOD-006` | `BG-01`, `BR-009` | `INT-008` | `SEQ-002` | `NOT_IMPLEMENTED` (Chưa mở endpoint chuyển `WAITING_FOR_INFO` và phản hồi thông tin bổ sung) | `TST-014` |
| `FR-REQ-003` | `M06` / `MOD-006`, `MOD-007` | `BG-02`, `BR-007`, `BR-009` | `INT-008` | `SEQ-002` | `IMPLEMENTED` (`RequestRoutingCommand.cs`, `RequestRoutingStore.cs`) | `TST-014`, `TST-015` |
| `FR-REQ-004` | `M06` / `MOD-006` | `BG-02`, `BR-009` | `INT-008` | `SEQ-002` | `IMPLEMENTED` (`RequestReceiptCommand.cs`, `RequestProcessingCommand.cs`) | `TST-015` |
| `FR-REQ-005` | `M06` / `MOD-006`, `MOD-007` | `BG-02`, `BR-008`, `BR-009` | `INT-008` | `SEQ-002`, `ADR-SSS-003` | `PARTIAL` (Đã có từ chối/xử lý Request; phần `ApprovalInstance` đa cấp theo dõi tại `OD-002`) | `TST-009`, `TST-015` |
| `FR-REQ-006` | `M06` / `MOD-006`, `MOD-005` | `BG-02`, `BG-03`, `BR-009` | `INT-008`, `INT-009` | `SEQ-002`, `SEQ-004` | `IMPLEMENTED` (`RequestTaskLinks` trong `RequestModelConfiguration.cs`, `TaskCreation.cs`) | `TST-015` |
| `FR-REQ-007` | `M06` / `MOD-006`, `MOD-005` | `BG-03`, `BR-009` | `INT-008` | `ADR-0009` | `IMPLEMENTED` (`RequestResolutionCommand.cs`, `RequestResolutionStore.cs`) | `TST-016` |
| `FR-REQ-008` | `M06` / `MOD-006` | `BG-03`, `BR-009` | `INT-008` | `ADR-0009` | `IMPLEMENTED` (`RequestConfirmationCommand.cs`, `RequestConfirmationStore.cs`) | `TST-016` |
| `FR-REQ-009` | `M06` / `MOD-006` | `BG-01`, `BR-009` | `INT-008` | `SEQ-002` | `IMPLEMENTED` (`RequestCancellationCommand.cs`, `RequestRejectionCommand.cs`, `RequestReviseCommand.cs`) | `TST-016` |
| `FR-WF-001` | `M07` / `MOD-007`, `MOD-FE-001` | `BG-02`, `BR-006` | `INT-006` | `ADR-SSS-003` | `PARTIAL` (`WorkflowsController.cs` hỗ trợ tạo workflow, tạo bản nháp & xem chi tiết; chưa mở sửa đồ thị bước) | `TST-008` |
| `FR-WF-002` | `M07` / `MOD-007` | `BG-02`, `BR-006` | `INT-006` | `ADR-SSS-003` | `NOT_IMPLEMENTED` (Chưa mở endpoint kiểm tra đồ thị và `Publish` phiên bản workflow) | `TST-008` |
| `FR-WF-003` | `M07` / `MOD-007` | `BG-02`, `BR-006` | `INT-006`, `INT-016` | `ADR-SSS-003` | `NOT_IMPLEMENTED` (Đã có `TaskWorkflowEngine.cs` cho Task; chưa chạy engine động theo `WorkflowVersion`) | `TST-008` |
| `FR-WF-004` | `M07` / `MOD-007` | `BG-02`, `BR-008` | `INT-006`, `INT-008` | `ADR-SSS-003` | `NOT_IMPLEMENTED` (Chưa có bảng `ApprovalInstance` / `ApprovalStepInstance` — `OD-002`) | `TST-009` |
| `FR-SLA-001` | `M08` / `MOD-008`, `MOD-FE-001` | `BG-04`, `BR-014` | `INT-007`, `INT-021` | `ADR-0004..0007` | `PARTIAL` (`SlaProfilesController.cs` đã hỗ trợ hồ sơ & phiên bản SLA bất biến; chờ gắn runtime SLA) | `TST-017` |
| `FR-SLA-002` | `M08` / `MOD-008` | `BG-04`, `BR-015` | `INT-007`, `INT-021` | `ADR-0004`, `ADR-0005` | `NOT_IMPLEMENTED` (Đã lưu `WorkingHoursJson`/`HolidaysJson` trong phiên bản SLA; chưa mở API quản trị lịch riêng) | `TST-017` |
| `FR-SLA-003` | `M08` / `MOD-008`, `MOD-BG-001` | `BG-04`, `BR-014`, `BR-015` | `INT-020`, `INT-021` | `ADR-SSS-002`, `ADR-SSS-004` | `NOT_IMPLEMENTED` (Chưa có bảng `SLAState` và tiến trình tính giờ làm việc tự động — `OD-003`, `OD-020`) | `TST-017` |
| `FR-SLA-004` | `M08` / `MOD-008`, `MOD-009`, `MOD-BG-001` | `BG-04`, `BR-016` | `INT-020`, `INT-021` | `ADR-0006`, `ADR-SSS-002`, `ADR-SSS-004` | `NOT_IMPLEMENTED` (Đã chuẩn hóa `EscalationRulesJson` theo `ADR-0006`; chưa có bảng `SLAEvent` & worker quét) | `TST-017` |
| `FR-COL-001` | `M09;M10` / `MOD-010`, `MOD-009` | `BG-03`, `BR-020` | `INT-010`, `INT-011`, `INT-012` | `SEQ-002`, `SEQ-004` | `IMPLEMENTED` (`CommentsController.cs`, `NotificationsController.cs`, `NotificationHub.cs`) | `TST-018`, `TST-019` |
| `FR-COL-002` | `M10` / `MOD-010` | `BG-01`, `BG-03`, `BR-017` | `INT-010`, `INT-024` | `SEQ-004`, `ADR-SSS-005` | `IMPLEMENTED` (`AttachmentsController.cs`, `S3CompatibleStorageProvider.cs`, `LocalStorageProvider.cs`) | `TST-018` |
| `FR-COL-003` | `M10` / `MOD-010` | `BG-06`, `BR-021` | `INT-010` | `ADR-SSS-001` | `IMPLEMENTED` (`RecordsController.cs`, `RecordArchivalAndSearch.cs`) | `TST-018` |
| `FR-COL-004` | `M10` / `MOD-010`, `MOD-FE-001` | `BG-06`, `BG-07`, `BR-003`, `BR-021` | `INT-010` | `ADR-0011` | `IMPLEMENTED` (`RecordsController.cs`, `RecordSearchReader.cs`) | `TST-018` |
| `FR-REP-001` | `M11` / `MOD-011`, `MOD-FE-001` | `BG-07`, `BR-003` | `INT-013` | `ADR-0011` | `IMPLEMENTED` (`DashboardController.cs`, `ReportingService.cs`) | `TST-020` |
| `FR-REP-002` | `M11` / `MOD-011`, `MOD-FE-001` | `BG-07` | `INT-013` | `ADR-SSS-001` | `IMPLEMENTED` (`ReportsController.cs`, `ReportingService.cs`) | `TST-020` |
| `FR-REP-003` | `M11` / `MOD-011`, `MOD-FE-001` | `BG-07`, `BR-003` | `INT-013` | `ADR-0011` | `IMPLEMENTED` (`ReportsController.cs`, `ReportingReader.cs`) | `TST-020` |
| `FR-REP-004` | `M11` / `MOD-011`, `MOD-008`, `MOD-FE-001` | `BG-04`, `BG-07` | `INT-013` | `ADR-0011` | `IMPLEMENTED` (`ReportsController.cs`, `ReportingReader.cs`) | `TST-020` |
| `FR-AI-001` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-01`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003`, `SEQ-006` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-002` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-01`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-003` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-01`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-004` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-02`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-018`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`, `RequestSplitStore.cs`) | `TST-021` |
| `FR-AI-005` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-03`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-006` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-04`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-007` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-008` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-03`, `BG-05`, `BR-018`, `BR-019` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-009` | `M12` / `MOD-012`, `MOD-FE-001` | `BG-05`, `BG-07`, `BR-003`, `BR-018` | `INT-014`, `INT-017`, `INT-025` | `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, `AiAssistanceService.cs`) | `TST-021` |
| `FR-AI-010` | `M12` / `MOD-012`, `MOD-013`, `MOD-FE-001` | `BG-05`, `BG-06`, `BR-018`, `BR-019`, `BR-022` | `INT-014`, `INT-018`, `INT-019` | `SEQ-003`, `SEQ-006` | `IMPLEMENTED` (`AiController.cs`, `AiActionDispatcher.cs`, `AiStore.cs`) | `TST-022`, `TST-030` |

---

## 2. Ma trận Truy vết Yêu cầu Giao diện, Dữ liệu, Phi Chức năng và Bảo mật (`IR`, `DR`, `NFR`, `SEC` — `55` Yêu cầu)

| Mã Yêu cầu SRS | Nhóm Yêu cầu | Module & Giao diện SSS (`MOD-*` / `INT-*`) | Chiến thuật & Quyết định Kiến trúc (`ARCH-*` / `ADR-*`) | Trạng thái Triển khai (`Repo Fact`) | Kịch bản Kiểm thử (`TST-*`) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `IR-UI-001` | Phân tách Platform Plane & Tenant Workspace Plane | `MOD-FE-001`, `INT-001..014` | `ARCH-SEC-001`, `ARCH-MNT-001`, `ADR-0001`, `DEC-WS-001` | `IMPLEMENTED` (`app.routes.ts`, Guards) | `TST-024` |
| `IR-UI-002` | Xử lý 4 Trạng thái Giao diện Chuẩn (`loading`, `empty`, `error`, `permission-denied`) | `MOD-FE-001`, `INT-001..014` | `ARCH-MNT-001`, `ADR-0001` | `IMPLEMENTED` (`frontend/src/app/`) | `TST-024` |
| `IR-UI-003` | Hiển thị Minh bạch Nhãn `AI suggestion` & Xác nhận Con người | `MOD-FE-001`, `MOD-012`, `INT-014` | `ARCH-AI-001`, `SEQ-003` | `IMPLEMENTED` (`AiController.cs`, Frontend AI components) | `TST-021`, `TST-024` |
| `IR-UI-004` | Giao diện Đăng ký Doanh nghiệp Tự phục vụ & Xác minh Email | `MOD-FE-001`, `MOD-002`, `INT-002` | `ARCH-MNT-001`, `ADR-SSS-007`, `IR-UI-001` | `NOT_IMPLEMENTED` (Chưa có route `/register-company` & `/verify-email` trong `app.routes.ts` — `OD-021..024`) | `TST-031` |
| `IR-API-001` | Hợp đồng RESTful JSON (`/api/v1/*`) & Bearer JWT | `BizFlow.Api`, `INT-001..015` | `ARCH-SEC-001`, `ADR-0002` | `IMPLEMENTED` (`Program.cs`, 19 Controllers) | `TST-001`, `TST-024` |
| `IR-API-002` | Chuẩn hóa Lỗi `ProblemDetails`, `errorCode` & Ngữ nghĩa `404` Không Tiết lộ | `BizFlow.Api`, `INT-001..014`, `INT-028` | `ARCH-SEC-002`, `ARCH-OBS-001`, `ADR-0012` | `IMPLEMENTED` (`ApiExceptionHandler.cs`, `ApiErrors.cs`) | `TST-016`, `TST-024` |
| `IR-API-003` | Kiểm soát Tương tranh (`If-Match` ETag) & Lũy đẳng (`Idempotency-Key`) | `BizFlow.Api`, `INT-005`, `INT-009` | `ARCH-REL-001`, `ADR-0008`, `ADR-0012` | `IMPLEMENTED` (`ServicesController.cs`, `TasksController.cs`) | `TST-007`, `TST-010`, `TST-025` |
| `IR-EXT-001` | Hợp đồng Tích hợp Nhà cung cấp AI (`OpenAiModelClient`) | `MOD-012`, `INT-025` | `ARCH-AI-001`, `SEQ-003`, `SEQ-006` | `IMPLEMENTED` (`OpenAiModelClient.cs`, timeout `45s`) | `TST-021`, `TST-022` |
| `IR-EXT-002` | Hợp đồng Cơ sở Dữ liệu (`PostgreSQL 18`) & Bộ nhớ Đệm (`Redis`) | `BizFlow.Infrastructure`, `INT-022`, `INT-023`, `INT-027` | `ARCH-TEN-001`, `ARCH-PERF-001`, `ADR-SSS-001` | `IMPLEMENTED` (`BizFlowDbContext.cs`, `BizFlowRedisCacheService.cs`, `DatabaseReadinessProbe.cs`) | `TST-025`, `TST-026` |
| `IR-FILE-001` | Giao diện Tải lên Tệp 3 Bước (`Create Session` $\rightarrow$ `Upload` $\rightarrow$ `Finalize`) | `MOD-010`, `INT-010`, `INT-024` | `ARCH-SEC-003`, `ADR-SSS-005` | `IMPLEMENTED` (`AttachmentsController.cs`, `AttachmentQueriesAndCommands.cs`) | `TST-018` |
| `IR-FILE-002` | Tải xuống Tệp An toàn có Kiểm tra Quyền & Cô lập Tenant | `MOD-010`, `INT-010`, `INT-024` | `ARCH-SEC-003`, `ADR-SSS-005` | `IMPLEMENTED` (`AttachmentsController.cs`, `S3CompatibleStorageProvider.cs`, `LocalStorageProvider.cs`) | `TST-018`, `TST-029` |
| `IR-NOTIF-001` | Giao diện Thông báo Thời gian Thực qua SignalR (`/hubs/notifications`) | `MOD-009`, `INT-011`, `INT-012` | `ARCH-SEC-001`, `ARCH-REL-002` | `IMPLEMENTED` (`NotificationHub.cs`, `SignalRNotificationUpdates.cs`) | `TST-019` |
| `IR-NOTIF-002` | Giao diện Chuyển phát Thông báo Email Ra Ngoài (`Outbound Email`) | `MOD-009`, `MOD-BG-001`, `INT-020`, `INT-026` | `ARCH-REL-002`, `ADR-SSS-002` | `NOT_IMPLEMENTED` (Đã có `PasswordResetEmail.cs`; chưa có bộ gửi email thông báo tổng quát — `OD-005`) | `TST-019` |
| `DR-001` | Dữ liệu Thuê bao, Đăng ký Doanh nghiệp & Cấu hình Tenant | `MOD-002`, `INT-022` | `ARCH-TEN-001`, `DEC-WS-001`, `ADR-SSS-001` | `IMPLEMENTED` (`Tenant`, `CompanyRegistrationRequest`, `TenantSetting`) | `TST-003`, `TST-029` |
| `DR-002` | Dữ liệu Định danh, Mật khẩu, Xác minh Email & Phiên Xác thực (`AuthenticationSession`) | `MOD-001`, `MOD-002`, `INT-022` | `ARCH-SEC-001`, `ADR-0002`, `ADR-SSS-007` | `PARTIAL` (`IdentityModelConfiguration.cs` đã có bảng; chưa có cột `EmailVerified`/`TenantKey` & token xác minh — `OD-005`, `OD-018`, `OD-022..024`) | `TST-001`, `TST-002`, `TST-031` |
| `DR-003` | Dữ liệu Cơ cấu Phòng ban, Người dùng & Phạm vi Quản lý (`ManagementScope`) | `MOD-003`, `INT-022` | `ARCH-SEC-002`, `BR-003`, `ADR-0011` | `IMPLEMENTED` (`ManagementScopeConfiguration.cs`, `IdentityModelConfiguration.cs`) | `TST-005`, `TST-006` |
| `DR-004` | Dữ liệu Vai trò, Quyền hạn & Gán Quyền (`RBAC`) | `MOD-003`, `INT-022` | `ARCH-SEC-002`, `ADR-SSS-006` | `IMPLEMENTED` (`RoleStore.cs`, `IdentityModelConfiguration.cs`) | `TST-006` |
| `DR-005` | Dữ liệu Danh mục Dịch vụ, Phân loại & Quy tắc Định tuyến | `MOD-004`, `MOD-007`, `INT-022` | `ARCH-MNT-001`, `ADR-0008`, `ADR-SSS-003` | `PARTIAL` (`ServiceModelConfiguration.cs` đã có; bảng `RoutingRule` chưa tạo — `OD-001`) | `TST-007` |
| `DR-006` | Dữ liệu Hồ sơ Yêu cầu (`RequestRecord`), Liên kết Task & Lịch sử Yêu cầu | `MOD-006`, `INT-022` | `ARCH-REL-001`, `ADR-0009` | `IMPLEMENTED` (`RequestModelConfiguration.cs`, `RequestRoutingModelConfiguration.cs`, `RequestResolutionConfiguration.cs`) | `TST-014`, `TST-016` |
| `DR-007` | Dữ liệu Công việc (`WorkTask`), Phân công (`TaskAssignment`) & Lịch sử Task | `MOD-005`, `INT-016`, `INT-022` | `ARCH-REL-001`, `ADR-0003`, `ADR-0010`, `ADR-0012` | `IMPLEMENTED` (`TaskModelConfiguration.cs`, `TaskAssignmentConfiguration.cs`) | `TST-010`, `TST-011`, `TST-012` |
| `DR-008` | Dữ liệu Bằng chứng Hoàn thành, Báo cáo Tiến độ & Xác nhận (`Confirmation`) | `MOD-005`, `MOD-010`, `INT-022` | `ARCH-REL-001`, `ADR-0003` | `IMPLEMENTED` (`TaskEvidenceConfiguration.cs`, `ConfirmationConfiguration.cs`) | `TST-013` |
| `DR-009` | Dữ liệu Định nghĩa Quy trình (`Workflow*`) & Phê duyệt (`Approval*`) | `MOD-007`, `INT-022` | `ARCH-REL-001`, `ADR-SSS-003` | `PARTIAL` (`WorkflowModelConfiguration.cs` đã có 4 bảng; 4 bảng `Approval*` chưa tạo — `OD-002`) | `TST-008`, `TST-009` |
| `DR-010` | Dữ liệu Hồ sơ SLA (`SlaProfile`, `SLAVersion`, `BusinessCalendar`) & Runtime (`SLAState`, `SLAEvent`) | `MOD-008`, `INT-021`, `INT-022` | `ARCH-REL-002`, `ADR-0004..0007`, `ADR-SSS-004` | `PARTIAL` (`SlaProfileConfiguration.cs`, `SlaSnapshotConfiguration.cs` đã có; 2 bảng `SLAState`, `SLAEvent` chưa tạo — `OD-003`) | `TST-017` |
| `DR-011` | Dữ liệu Bình luận (`Comment`) & Phiên Tệp Đính kèm (`Attachment`) | `MOD-010`, `INT-022`, `INT-024` | `ARCH-SEC-003`, `ADR-SSS-005` | `IMPLEMENTED` (`CommentModelConfiguration.cs`, `AttachmentModelConfiguration.cs`) | `TST-018` |
| `DR-012` | Dữ liệu Hồ sơ Lưu trữ & Chỉ mục Tìm kiếm (`ArchivedRecord`) | `MOD-010`, `INT-022` | `ARCH-SEC-003` | `IMPLEMENTED` (`RecordArchivalStore.cs`, `RecordSearchReader.cs`) | `TST-018` |
| `DR-013` | Dữ liệu Thông báo Hộp thư & Trạng thái Đọc (`Notification`) | `MOD-009`, `INT-022` | `ARCH-REL-002` | `IMPLEMENTED` (`NotificationConfiguration.cs`) | `TST-019` |
| `DR-014` | Dữ liệu Tương tác AI, Đề xuất AI & Hành động AI (`AIInteraction`, `AIRecommendation`, `AIAgentAction`) | `MOD-012`, `INT-022` | `ARCH-AI-001` | `IMPLEMENTED` (`AIModelConfiguration.cs`, `AIEntities.cs`) | `TST-021`, `TST-022` |
| `DR-015` | Dữ liệu Nhật ký Kiểm toán Bất biến (`AuditLog`) | `MOD-013`, `INT-019`, `INT-022` | `ARCH-OBS-001`, `ADR-SSS-001` | `IMPLEMENTED` (`BizFlowDbContext.cs`, Append-Only Guards) | `TST-023`, `TST-027` |
| `NFR-PERF-001` | Hiệu năng Thời gian Phản hồi API Đọc/Ghi Nội bộ | `BizFlow.Api`, `BizFlow.Infrastructure` | `ARCH-PERF-001` (`OD-011`) | `PARTIAL` (Đã tối ưu truy vấn bất đồng bộ & index; ngưỡng cam kết định lượng theo dõi tại `OD-011`) | `TST-025` |
| `NFR-PERF-002` | Phân trang Bắt buộc & Bảo vệ Truy vấn Danh sách Lớn | `BizFlow.Api`, `MOD-001..013` | `ARCH-PERF-001` (`OD-008`) | `PARTIAL` (`PageRequest.cs` đã áp dụng phân trang; ngưỡng xuất dữ liệu lớn theo dõi tại `OD-008`) | `TST-025` |
| `NFR-SCAL-001` | Kiến trúc Không Trạng thái (`Stateless API`) & Bộ nhớ Đệm Phân tán | `BizFlow.Api`, `INT-023` | `ARCH-PERF-001`, `ARCH-MNT-001` (`OD-017`) | `IMPLEMENTED` (JWT + `BizFlowRedisCacheService.cs`) | `TST-025` |
| `NFR-REL-001` | Toàn vẹn Giao dịch ACID, Kiểm soát Tương tranh & Lũy đẳng | `MOD-005..007`, `BizFlowDbContext` | `ARCH-REL-001`, `ADR-0008`, `ADR-0010`, `ADR-0012` | `IMPLEMENTED` (`SaveChangesAsync` guards, ETag, `Idempotency-Key`) | `TST-011`, `TST-027` |
| `NFR-SEC-001` | Cô lập Đa Thuê bao Nhiều Lớp (`Defense-in-Depth Multi-Tenancy`) | `MOD-002`, `BizFlowDbContext`, `INT-015` | `ARCH-TEN-001`, `SEQ-005`, `ADR-SSS-001` | `IMPLEMENTED` (`ConfigureTenantFilter<T>` Fail-Closed) | `TST-029` |
| `NFR-SEC-002` | Kiểm soát Truy cập theo Vai trò & Phạm vi Quản lý (`RBAC + Scoped Access`) | `MOD-003`, `MOD-005`, `MOD-011` | `ARCH-SEC-002`, `ADR-0011`, `ADR-0012` | `IMPLEMENTED` (`ResourceAuthorizer.cs`, `TenantMembershipAuthorizer.cs`) | `TST-006`, `TST-016` |
| `NFR-DATA-001` | Cấm Xóa Cứng (`No Hard Delete`) & Bảo toàn Lịch sử Bất biến | `BizFlowDbContext`, `MOD-013` | `ARCH-SEC-003`, `ARCH-OBS-001`, `ADR-SSS-001` | `IMPLEMENTED` (`PERSISTENCE.HARD_DELETE_DENIED`, `PERSISTENCE.IMMUTABLE_FIELD`) | `TST-023`, `TST-027` |
| `NFR-AI-001` | Kiểm soát Con người đối với AI (`Human-in-the-Loop`) & Suy giảm An toàn | `MOD-012`, `INT-017`, `INT-018`, `INT-025` | `ARCH-AI-001`, `SEQ-003`, `SEQ-006` | `IMPLEMENTED` (`AiAssistanceService.cs`, `AiContextReader.cs`, `AiActionDispatcher.cs`) | `TST-021`, `TST-022`, `TST-030` |
| `NFR-FILE-001` | Giới hạn Tệp 500 MB, Kiểm tra SHA-256 & Cô lập Lưu trữ Đối tượng | `MOD-010`, `INT-010`, `INT-024` | `ARCH-SEC-003`, `ADR-SSS-005` | `IMPLEMENTED` (`AttachmentQueriesAndCommands.cs`, `S3CompatibleStorageProvider.cs`) | `TST-018` |
| `NFR-USAB-001` | Nhất quán Thiết kế GRID & Trải nghiệm Giao diện Đa Vai trò | `MOD-FE-001` | `ARCH-MNT-001`, `ADR-0001` | `IMPLEMENTED` (`frontend/src/`) | `TST-024` |
| `NFR-MAINT-001` | Kiến trúc Modular Monolith Phân tầng Sạch & Kiểm thử Tự động | Toàn bộ Solution (`BizFlow.slnx`) | `ARCH-MNT-001` | `IMPLEMENTED` (`TreatWarningsAsErrors = true`, Unit & Integration Test Suites) | `TST-028` |
| `NFR-OBS-001` | Nhật ký Cấu trúc JSON (`Serilog`), `TraceId` & Điểm Kiểm tra Sức khỏe (`/health/*`) | `BizFlow.Api`, `INT-027`, `INT-028` | `ARCH-OBS-001` | `IMPLEMENTED` (`Program.cs`, `HealthEndpoints.cs`, `DatabaseReadinessProbe.cs`) | `TST-026` |
| `NFR-AI-002` | Kiểm soát Hạn mức Sử dụng AI (`Quota / Cost Governance`) theo Tenant | `MOD-012` | `ARCH-AI-001` (`OD-009`) | `NOT_IMPLEMENTED` (Đang theo dõi chính sách hạn mức tại `OD-009`) | `TST-022` |
| `NFR-I18N-001` | Hỗ trợ Chuẩn Thời gian UTC, Múi giờ Tenant & Mã Lỗi Chuẩn hóa | `BizFlow.Api`, `MOD-002`, `MOD-008` | `ARCH-MNT-001`, `ADR-0012` (`OD-018`) | `IMPLEMENTED` (Chuẩn UTC & `errorCode` đã có; song ngữ đầy đủ theo dõi tại `OD-018`) | `TST-017`, `TST-024` |
| `NFR-BCDR-001` | Sao lưu, Phục hồi Thảm họa (`RPO`/`RTO`) & Lưu trữ Dài hạn | Hạ tầng PostgreSQL & Object Storage | `ARCH-REL-002` (`OD-010`, `OD-012`) | `NOT_IMPLEMENTED` (Phụ thuộc cấu hình hạ tầng đích — `OD-012`) | `TST-026` |
| `SEC-001` | Bộ lọc Truy vấn EF Core Fail-Closed theo `CurrentTenantId` | `BizFlowDbContext`, `INT-015`, `INT-022` | `ARCH-TEN-001`, `SEQ-005`, `ADR-SSS-001` | `IMPLEMENTED` (`ConfigureTenantFilter<T>` trong `BizFlowDbContext.cs`) | `TST-029` |
| `SEC-002` | Ngữ nghĩa `404 Not Found` Không Tiết lộ khi Truy cập Ngoài Phạm vi | `MOD-005`, `MOD-006`, `ApiExceptionHandler` | `ARCH-SEC-002`, `SEQ-005`, `ADR-0012` | `IMPLEMENTED` (`TaskDetailQuery.cs`, `RequestDetailQuery.cs`) | `TST-016`, `TST-029` |
| `SEC-003` | Xác thực Mật khẩu Băm An toàn & Giới hạn Thử Đăng nhập (`Rate Limiting`) | `MOD-001`, `INT-001` | `ARCH-SEC-001`, `ADR-0002` | `IMPLEMENTED` (`IdentityCredentialVerifier.cs`, `AuthenticationAttemptLimiter.cs`) | `TST-001`, `TST-002` |
| `SEC-004` | Xoay vòng Refresh Token & Thu hồi Họ Token khi Phát hiện Replay | `MOD-001`, `INT-001` | `ARCH-SEC-001`, `ADR-0002` | `IMPLEMENTED` (`AuthenticationService.cs`, `AuthenticationStore.cs`) | `TST-001` |
| `SEC-005` | Đóng Kết nối WebSocket/SignalR Tức thời khi Phiên/JWT Hết hạn | `MOD-009`, `INT-012` | `ARCH-SEC-001` | `IMPLEMENTED` (`CloseOnAuthenticationExpiration = true` trong `Program.cs`) | `TST-019` |
| `SEC-006` | Cưỡng chế Kiểm tra Thành viên Tenant & Quyền RBAC tại Backend | `MOD-002`, `MOD-003`, `INT-015` | `ARCH-SEC-002`, `SEQ-001` | `IMPLEMENTED` (`TenantMembershipAuthorizer.cs`, `ResourceAuthorizer.cs`) | `TST-004`, `TST-006`, `TST-029` |
| `SEC-007` | Kiểm soát Phạm vi Đọc theo Cây Phòng ban Quản lý (`ManagementScope`) | `MOD-003`, `MOD-005`, `MOD-011` | `ARCH-SEC-002`, `BR-003`, `ADR-0011` | `IMPLEMENTED` (`ManagementScopeConfiguration.cs`, `TaskListQuery.cs`, `ReportingService.cs`) | `TST-016`, `TST-020` |
| `SEC-008` | Chặn Xóa Cứng (`HARD_DELETE_DENIED`) ở Tầng Persistence | `BizFlowDbContext`, `INT-022` | `ARCH-SEC-003`, `ADR-SSS-001` | `IMPLEMENTED` (`BizFlowDbContext.SaveChangesAsync`) | `TST-027` |
| `SEC-009` | Chặn Sửa đổi Cột Định danh Bất biến (`IMMUTABLE_FIELD`) | `BizFlowDbContext`, `INT-022` | `ARCH-SEC-003`, `ADR-SSS-001` | `IMPLEMENTED` (`BizFlowDbContext.EnforceModifyImmutability`) | `TST-027` |
| `SEC-010` | Bảo vệ Tính Bất biến (`Append-Only`) của `AuditLog` & Lịch sử Nghiệp vụ | `MOD-013`, `BizFlowDbContext`, `INT-019` | `ARCH-OBS-001`, `ADR-SSS-001` | `IMPLEMENTED` (`SecurityAuditWriter.cs`, `BizFlowDbContext.cs`) | `TST-023`, `TST-027` |
| `SEC-011` | Cô lập Lưu trữ Tệp theo Tenant & Kiểm tra Toàn vẹn SHA-256 | `MOD-010`, `INT-010`, `INT-024` | `ARCH-SEC-003`, `ADR-SSS-005` | `IMPLEMENTED` (`AttachmentQueriesAndCommands.cs`, `S3CompatibleStorageProvider.cs`) | `TST-018`, `TST-029` |
| `SEC-012` | Ranh giới Bảo mật AI (`AiContextReader` & `AiActionDispatcher` Human-in-the-Loop) | `MOD-012`, `INT-017`, `INT-018` | `ARCH-AI-001`, `SEQ-003` | `IMPLEMENTED` (`AiContextReader.cs`, `AiActionDispatcher.cs`, `AiToolRegistry.cs`) | `TST-021`, `TST-030` |
