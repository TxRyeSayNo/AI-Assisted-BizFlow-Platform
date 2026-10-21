# Danh mục Phân rã Module Hệ thống (System Module Catalog)

- **Mã tài liệu:** `SSS-MOD-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](SSS_v1.0_DRAFT.md) — Mục 6, Mục 7 & Mục 10
- **Ngày lập:** `2026-10-18` (Cập nhật đồng bộ v1.2: `2026-10-21`)
- **Tổng số module đặc tả:** `17` module hệ thống (`MOD-001` .. `MOD-015` cho 15 miền nghiệp vụ Backend, `MOD-FE-001` cho ứng dụng Frontend Angular 22 SPA, và `MOD-BG-001` cho tiến trình Background Worker).

---

## 1. Bảng Tổng quan 17 Module của Hệ thống

| Mã Module | Tên Module | Phân tầng Kiến trúc | Bảng Dữ liệu Sở hữu (`Target` / `Implemented`) | Trạng thái Kiến trúc | Trạng thái Triển khai (`Current Repo`) |
| :--- | :--- | :--- | :--- | :--- | :--- |
| `MOD-001` | Identity & Authentication | Tier 1 — Foundation | `PasswordCredential`, `UserSession`, `RefreshToken`, `PasswordResetToken` (4/4) | `CONFIRMED` | `IMPLEMENTED` (ID Login `PARTIAL`; Email-Link `NOT_IMPLEMENTED`) |
| `MOD-002` | Tenant & Workspace Management | Tier 1 — Foundation | `Tenant`, `Workspace`, `TenantSetting`, `CompanyRegistrationRequest` (`Company`), `CompanyRegistrationDocument` (5/5) | `CONFIRMED` | `PARTIAL` (Read queue `IMPLEMENTED`; 6-field reg + code check + MinIO docs `NOT_IMPLEMENTED`) |
| `MOD-003` | Organization & Access Control (RBAC) | Tier 2 — Configuration | `User`, `Department`, `Role`, `Permission`, `RolePermission`, `UserRole`, `DepartmentMembership` (7/7) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-004` | Service Catalog & Dynamic Form Builder | Tier 2 — Configuration | `ServiceCategory`, `ServiceType`, `FormDefinition`, `FormField` (4/4) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-005` | Task Management & Execution | Tier 3 — Execution | `WorkTask`, `TaskAssignment`, `TaskBlocker`, `TaskHistory`, `Confirmation` (5/5) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-006` | Request Management | Tier 3 — Execution | `RequestRecord`, `RequestFieldValue`, `RequestHistory`, `RequestTaskLink` (4/4) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-007` | Workflow, Routing & Approval Engine | Tier 3 — Execution | Đã có (4): `WorkflowDefinition`, `WorkflowVersion`, `WorkflowStep`, `WorkflowInstance`. Chưa có (5): `RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance` | `CONFIRMED` | `PARTIAL` (Task workflow & Workflow definitions `IMPLEMENTED`; 5 bảng Routing/Approval `NOT YET IMPLEMENTED`) |
| `MOD-008` | SLA & Calendar Management | Tier 3 — Execution | Đã có (3): `SlaProfile`, `Holiday`, `WorkingHour`. Chưa có (2): `SLAState`, `SLAEvent` | `CONFIRMED` | `PARTIAL` (Profiles & Calendar `IMPLEMENTED`; 2 bảng SLA runtime state `NOT YET IMPLEMENTED`) |
| `MOD-009` | Notification & Real-Time Hub | Tier 1 — Cross-Cutting | `Notification`, `NotificationPreference`, `NotificationQueueItem`, `EmailQueueItem`, `NotificationConnection` (5/5) | `CONFIRMED` | `PARTIAL` (In-app + SignalR + Outbox tables `IMPLEMENTED`; Background SMTP dispatcher `PROPOSED`) |
| `MOD-010` | Collaboration, Evidence & Archival | Tier 3 — Execution | `Comment`, `Attachment`, `CompletionEvidence`, `ArchivedRecordView` (4/4) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-011` | Reporting, Analytics & Dashboard | Tier 4 — Intelligence | `ReportExportJob`, `ReportSnapshot` (2/2) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-012` | AI Assistant & Governance | Tier 4 — Intelligence | `AiSuggestion`, `AiConfig`, `AiExecutionLog` (3/3) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-013` | Audit & Security Compliance | Tier 1 — Cross-Cutting | `AuditLog`, `SecurityEvent` (2/2) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-014` | Robot Chatbot & Natural Language Assistance | Tier 4 — Intelligence | `RobotConversation`, `RobotMessage` (2/2) | `CONFIRMED` | `NOT YET IMPLEMENTED` |
| `MOD-015` | Deadline Reminder & Multi-Channel Dispatcher | Tier 1 — Cross-Cutting | `DeadlineReminderSchedule`, `PushSubscription`, `NotificationLog` (3/3) | `CONFIRMED` | `NOT YET IMPLEMENTED` |
| `MOD-FE-001` | Frontend Single-Page Application (Angular 22) | Presentation Tier | Trạng thái phiên cục bộ (`AuthSession`), Router State, UI View State, Robot Session State (`sessionStorage`) | `CONFIRMED` | `IMPLEMENTED` |
| `MOD-BG-001` | Background Job & Timer Worker Host | Infrastructure / Worker Tier | Thực thi trên các bảng hàng đợi của `MOD-008`, `MOD-009`, `MOD-011`, `MOD-015` | `PROPOSED` (`ADR-SSS-002`) | `NOT YET WIRED` (Chưa đăng ký Hangfire/`BackgroundService` trong `Program.cs`) |

---

## 2. Đặc tả Chi tiết Từng Module (`MOD-001` .. `MOD-BG-001`)

### 2.1. `MOD-001`: Identity & Authentication (Định danh và Xác thực)
- **Module ID:** `MOD-001`
- **Module Name:** Identity & Authentication
- **Purpose:** Xác thực danh tính người dùng theo 2 phương thức hợp nhất (Đăng nhập Định danh 3 trường bắt buộc và Đăng nhập Không Mật khẩu qua Email Link), điều phối bộ chọn Workspace (`Workspace Selector`), cấp phát và xoay vòng JWT Access Token / Refresh Token, quản lý vòng đời phiên đăng nhập (`UserSession`), quy trình khôi phục/đổi mật khẩu an toàn và chặn đăng nhập đối với tài khoản chưa xác minh email.
- **Key Responsibilities:**
  - **Phương thức 1 (Đăng nhập Định danh & Mật khẩu — `FR-AUTH-001`, `BR-025`):** Xác thực thông tin đăng nhập yêu cầu chính xác 3 trường: `ID / Mã NV / Tên đăng nhập`, `Mật khẩu` và `Mã Workspace / Tenant Key`. Backend thực thi truy vấn kiểm tra đồng thời cả 3 trường; nếu thiếu hoặc sai bất kỳ trường nào, trả về mã lỗi tổng quát `401 AUTH.INVALID_CREDENTIALS` để phòng chống dò quét tài khoản.
  - **Phương thức 2 (Đăng nhập Không Mật khẩu qua Email Link — `FR-AUTH-005`, `ADR-SSS-008`):** Tiếp nhận yêu cầu chỉ bằng địa chỉ `Email` (không yêu cầu mật khẩu và không yêu cầu mã Workspace ở bước gửi); phát hành mã xác thực mật mã `EmailLoginToken` (HMAC-SHA256, `TTL = 15 phút`, sử dụng một lần) gửi qua Transactional Email (`IR-EXT-002`).
  - **Điều phối Định tuyến Workspace (`Workspace Selector Flow`):** Khi người dùng xác thực token email: nếu thuộc 0 workspace $\rightarrow$ trả về `404 AUTH.USER_NOT_FOUND` kèm link sang `/register-company`; nếu thuộc đúng 1 workspace $\rightarrow$ cấp JWT và vào thẳng; nếu thuộc $\ge 2$ workspaces $\rightarrow$ trả về danh sách workspace hợp lệ để Frontend hiển thị `Workspace Selector Modal` (`IR-UI-005`) cho người dùng lựa chọn trước khi cấp phát JWT phiên chính thức.
  - Kiểm tra trạng thái xác minh email (`EmailVerified` flag); từ chối đăng nhập với mã lỗi `403 AUTH.EMAIL_NOT_VERIFIED` nếu tài khoản đại diện chưa xác thực email hợp lệ (`BR-024`, `OD-022`).
  - Phát hành JWT Access Token có nhúng các claim bắt buộc (`sub`, `tenant_id`, `workspace_id`, `session_id`) theo `ADR-0009`.
  - Quản lý làm mới token (`RefreshToken`), thu hồi phiên đơn lẻ hoặc thu hồi toàn bộ phiên khi đổi mật khẩu / khóa tài khoản.
  - Cung cấp cài đặt `ClaimsTenantContext` (`ITenantContext`) cho toàn bộ chuỗi xử lý HTTP và SignalR.
- **Non-Responsibilities:**
  - Không quản lý sơ đồ cây phòng ban hay định nghĩa ma trận quyền RBAC (thuộc `MOD-003`).
  - Không hỗ trợ SSO/SAML/OIDC doanh nghiệp trong phạm vi MVP.
- **Inputs:** Yêu cầu đăng nhập 3 trường (`LoginRequest`), yêu cầu đăng nhập email link (`EmailLinkRequest`), token xác nhận email link, yêu cầu chọn workspace (`SelectWorkspaceRequest`), yêu cầu làm mới phiên (`RefreshTokenRequest`), yêu cầu quên/đặt lại/đổi mật khẩu, HTTP Header `Authorization: Bearer <token>`.
- **Outputs:** `AuthSessionResponse` (AccessToken, RefreshToken, thông tin hồ sơ phiên), danh sách workspace cho email link, đối tượng `ITenantContext` đã xác thực, bản ghi `SecurityEvent` khi đăng nhập thành công/thất bại hoặc bị chặn do chưa xác minh email.
- **Data Ownership:** `PasswordCredentials`, `UserSessions`, `RefreshTokens`, `PasswordResetTokens`, `EmailLoginTokens`.
- **Dependencies:** `MOD-002` (kiểm tra trạng thái `Tenant` và `Workspace`), `MOD-009` (xếp hàng gửi email liên kết đăng nhập và đặt lại mật khẩu), `MOD-013` (ghi `SecurityEvents` và `AuditLogs`).
- **Public Interfaces:** `INT-001` (`AuthController`, `AccountController`, `SessionsController`), `INT-015` (`ITenantContext`), `INT-029` (`EmailLinkAuthController`).
- **Security/Tenant Requirements:** Áp dụng giới hạn tốc độ (`RateLimiter`) cho các endpoint đăng nhập và khôi phục mật khẩu; mật khẩu phải được băm một chiều có salt; thu hồi ngay lập tức mọi phiên (`UserSessions`) khi người dùng bị vô hiệu hóa hoặc Tenant bị đình chỉ (`BR-001`, `BR-024`, `BR-025`).
- **Related SRS Requirements:** `FR-AUTH-001`, `FR-AUTH-002`, `FR-AUTH-003`, `FR-AUTH-004`, `FR-AUTH-005`, `IR-UI-005`, `IR-API-004`, `BR-001`, `BR-024`, `BR-025`, `SEC-AUTH-001`, `SEC-AUTH-002`, `DR-002`, `DR-004`.
- **Testing Approach:** Unit tests kiểm tra phát hành/thu hồi token và hết hạn phiên; Integration tests kiểm tra đăng nhập 3 trường bắt buộc (`TST-001`), đăng nhập qua liên kết email và bộ chọn workspace (`TST-032`).
- **Status:** `CONFIRMED` | `PARTIAL` (`AuthController.cs` cơ bản đã có; luồng email-link & workspace selector đang ở mức thiết kế chi tiết).

---

### 2.2. `MOD-002`: Tenant & Workspace Management (Quản trị Thuê bao và Không gian Làm việc)
- **Module ID:** `MOD-002`
- **Module Name:** Tenant & Workspace Management
- **Purpose:** Quản lý vòng đời đăng ký doanh nghiệp mới với 6 trường bắt buộc và hồ sơ pháp lý đính kèm, kiểm tra khả dụng mã workspace theo thời gian thực và sinh mã ngẫu nhiên, thẩm định hồ sơ qua Presigned URL, cấp phát nguyên tử Không gian làm việc chính duy nhất trong MVP (`1 Company = 1 Primary Workspace` theo `DEC-WS-001`), gán quyền quản trị viên ban đầu và quản lý cấu hình cấp công ty.
- **Key Responsibilities:**
  - **Tiếp nhận Hồ sơ Đăng ký Doanh nghiệp (`FR-TEN-001`, `BR-026`):** Thu thập chính xác 6 trường bắt buộc: Mã số thuế (`TaxId`), Tên công ty (`CompanyName`), Mã workspace mong muốn (`DesiredWorkspaceCode`), Tên người phụ trách (`ContactPersonName`), Email (`ContactEmail`), Số điện thoại (`ContactPhone`).
  - **Kiểm tra Mã Workspace Real-time & Nút Sinh Mã Ngẫu nhiên (`FR-TEN-001`, `IR-API-005`):** Cung cấp API `GET /api/v1/tenants/check-code?code=...` kiểm tra ngay khi người dùng gõ phím; cung cấp nút "Tạo mã ngẫu nhiên" sinh slug từ tên công ty kết hợp hậu tố ngẫu nhiên (định dạng `^[a-z0-9-]{3,30}$`). Tái kiểm tra tính khả dụng tại thời điểm bấm submit để ngăn ngừa tranh chấp (race condition).
  - **Tải Lên Tài liệu Pháp lý Đính kèm (`FR-TEN-001`, `MOD-010`):** Cho phép tải lên các tệp tài liệu hỗ trợ thẩm định (Giấy chứng nhận đăng ký kinh doanh, giấy phép thành lập...) trực tiếp lên MinIO/S3 bucket riêng biệt `company-documents` (định dạng PDF/PNG/JPG, tối đa 10 MB/tệp, tối đa 50 MB tổng cộng). Lưu vết tham chiếu trong bảng `CompanyRegistrationDocument`.
  - **Thẩm định Hồ sơ cho Platform Admin (`FR-TEN-002`, `IR-UI-006`):** Cung cấp giao diện cho Platform Admin xem chi tiết thông tin pháp lý doanh nghiệp, tạo Presigned URL an toàn có thời hạn ngắn (`TTL = 15 phút`) để tải/xem tài liệu đính kèm trước khi ra quyết định phê duyệt hoặc từ chối (bắt buộc nêu lý do theo `BR-022`).
  - **Thực thi Giao dịch Cấp phát Nguyên tử Kèm Rollback Khép kín (`ADR-SSS-007`, `BR-023`):** Khi phê duyệt, thực thi chuỗi 5 bước trong một giao dịch ACID duy nhất: Kích hoạt `Company (ACTIVE)` + Tạo đúng 01 `Primary Tenant (ACTIVE)` gắn với `CompanyId` (`Unique(CompanyId)` theo `DEC-WS-001`) + Khởi tạo cấu hình hạt giống (`TenantSettings`, `BusinessCalendar`) + Tạo tài khoản đại diện ban đầu (`UserAccount`, `Status = Active`) + Gán vai trò hệ thống `COMPANY_ADMIN` (`UserRole`) + Ghi `AuditLog`.
  - **Kiểm tra Bất biến Sau Khởi tạo (Post-Provisioning Invariant Guard):** Xác minh `Count(PrimaryTenant) == 1 && Count(CompanyAdmin) >= 1`; nếu có bất kỳ bước nào lỗi hoặc bước gán quyền thất bại, **Rollback 100% giao dịch**, giữ đơn ở trạng thái `PENDING`, trả về mã lỗi `500 ONBOARDING.PROVISIONING_ROLLED_BACK` kèm `CorrelationId`, không sinh workspace mồ côi.
  - Cho phép `Platform Super Admin` tạm ngưng (`Suspended`) hoặc kích hoạt lại (`Active`) một doanh nghiệp.
  - Cung cấp bộ lọc `TenantMembershipAuthorizer` xác minh tính hợp lệ của thành viên và trạng thái hoạt động của Tenant trên mọi request.
- **Non-Responsibilities:**
  - Không xử lý thanh toán, gói cước, hóa đơn hoặc giới hạn thương mại (`DEC-WS-002`).
  - Không cho phép tạo nhiều Workspace độc lập trong cùng một Company ở giai đoạn MVP (`DEC-WS-001`).
- **Inputs:** Đơn đăng ký doanh nghiệp 6 trường kèm tệp hồ sơ pháp lý, mã kiểm tra khả dụng, token xác minh email, lệnh duyệt/từ chối/đình chỉ/kích hoạt lại từ Platform Admin, yêu cầu cập nhật `TenantSetting` từ Company Admin.
- **Outputs:** Bản ghi `Tenant`, `Workspace`, `TenantSetting`, `UserAccount`, `UserRole (COMPANY_ADMIN)`, `CompanyRegistrationDocument` đã khởi tạo nguyên tử; Presigned URL xem tài liệu; email thông báo xác minh/chào mừng; phản hồi kiểm tra thành viên từ `TenantMembershipAuthorizer`.
- **Data Ownership:** `Tenants`, `Workspaces`, `TenantSettings`, `CompanyRegistrationRequests` (`Companies`), `CompanyRegistrationDocuments`, `EmailVerificationTokens`.
- **Dependencies:** `MOD-001` (khởi tạo thông tin xác thực cho Company Admin đầu tiên), `MOD-003` (khởi tạo vai trò mặc định của Tenant), `MOD-009` (gửi email xác minh/chào mừng), `MOD-010` (lưu trữ tệp hồ sơ pháp lý lên MinIO/S3), `MOD-013` (ghi nhật ký kiểm toán cấp Platform và Tenant).
- **Public Interfaces:** `INT-002` (`CompanyRegistrationsController`, `PlatformTenantsController`, `PlatformAuditController`), `INT-003` (`WorkspacesController`, `TenantSettingsController`), `INT-030` (`TenantCodeAndDocumentController`).
- **Security/Tenant Requirements:** Chỉ `Platform Super Admin` mới được gọi các API `/api/v1/platform/*`; tài liệu pháp lý phải được lưu trong bucket cô lập `company-documents` và chỉ truy cập được qua Presigned URL có thời hạn; khi `Tenant.Status != Active`, `TenantMembershipAuthorizer` chặn ngay lập tức mọi thao tác nghiệp vụ (`BR-001`, `BR-023`, `BR-026`).
- **Related SRS Requirements:** `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-003`, `FR-TEN-004`, `FR-TEN-005`, `IR-UI-004`, `IR-UI-006`, `IR-API-005`, `IR-EXT-002`, `BR-023`, `BR-024`, `BR-026`, `DR-001`, `DR-002`, `TST-004`, `TST-031`, `TST-033`.
- **Testing Approach:** Integration tests kiểm tra đăng ký 6 trường + kiểm tra mã trùng lặp + tải lên MinIO (`TST-033`), kiểm thử phê duyệt Platform Admin qua Presigned URL và cấp phát nguyên tử có rollback 100% khi lỗi (`TST-004`, `TST-031`).
- **Status:** `CONFIRMED` & `PROPOSED (Self-Service)` | `PARTIAL` (`CompanyRegistrationsController.cs` cơ bản đã có; phần mở rộng 6 trường, kiểm tra mã real-time, upload hồ sơ pháp lý và Presigned URL review đang ở mức thiết kế).

---

### 2.3. `MOD-003`: Organization & Access Control — RBAC (Cơ cấu Tổ chức và Phân quyền)
- **Module ID:** `MOD-003`
- **Module Name:** Organization & Access Control (RBAC)
- **Purpose:** Quản lý cơ cấu phòng ban phân cấp (`Department`), hồ sơ người dùng (`User`), tư cách thành viên phòng ban (`DepartmentMembership`), vai trò (`Role`), quyền hạn (`Permission`) và tính toán phạm vi quản lý (`ManagementScope` theo `BR-003`).
- **Key Responsibilities:**
  - Quản lý cây phòng ban đa cấp (thêm, sửa, ngừng kích hoạt, gán trưởng phòng `ManagerUserId`, chống chu trình lặp).
  - Quản lý vòng đời người dùng trong doanh nghiệp (tạo/mời nhân sự, gán phòng ban chính/phụ, kích hoạt/vô hiệu hóa tài khoản).
  - Quản lý các vai trò mặc định (`Company Admin`, `Department Manager`, `Approver`, `Staff/Employee`, `Internal Auditor`) và vai trò tùy chỉnh cùng tập quyền `RolePermission`.
  - Cung cấp dịch vụ phân giải phạm vi quản lý (`ManagementScope`: tập hợp phòng ban do người dùng làm quản lý trực tiếp cộng với toàn bộ các phòng ban con cấp dưới theo `BR-003`) để phục vụ kiểm soát quyền đọc/báo cáo (`ADR-0011`).
- **Non-Responsibilities:**
  - Không lưu trữ mật khẩu hoặc phiên đăng nhập (thuộc `MOD-001`).
  - Không thực thi chuyển đổi trạng thái công việc (thuộc `MOD-007`).
- **Inputs:** Lệnh tạo/cập nhật phòng ban, nhân sự, vai trò, gán quyền từ Company Admin; truy vấn kiểm tra quyền và phạm vi phòng ban từ các module khác.
- **Outputs:** Cấu trúc tổ chức, danh sách quyền hiệu lực của người dùng, tập `ManagedDepartmentIds` phục vụ lọc dữ liệu theo phạm vi.
- **Data Ownership:** `Users`, `Departments`, `Roles`, `Permissions`, `RolePermissions`, `UserRoles`, `DepartmentMemberships`.
- **Dependencies:** `MOD-002` (ràng buộc `TenantId` và `WorkspaceId`), `MOD-001` (thu hồi phiên khi vô hiệu hóa người dùng), `MOD-013` (ghi `AuditLog` khi thay đổi quyền hạn/cơ cấu tổ chức).
- **Public Interfaces:** `INT-004` (`OrganizationController`, `UsersController`, `DepartmentsController`, `RolesController`, `PermissionsController`, `MembershipsController`).
- **Security/Tenant Requirements:** Cô lập hoàn toàn danh sách người dùng, phòng ban và vai trò theo `TenantId`; bảo vệ các vai trò hệ thống cốt lõi không bị xóa trái phép; đảm bảo thu hồi quyền có hiệu lực ngay trên các truy vấn tiếp theo (`SEC-AUTHZ-001..002`).
- **Related SRS Requirements:** `FR-ORG-001`, `FR-ORG-002`, `FR-ORG-003`, `FR-ORG-004`, `FR-ORG-005`, `SEC-AUTHZ-001`, `SEC-AUTHZ-002`, `DR-003`, `DR-004`.
- **Testing Approach:** Unit tests kiểm tra thuật toán duyệt cây phòng ban (`BR-003`) và chống vòng lặp; Integration tests kiểm tra phân quyền RBAC và phạm vi `tasks.read.managed` (`ADR-0011`).
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`OrganizationController.cs`, `UsersController.cs`, `DepartmentsController.cs`, `RolesController.cs`, `PermissionsController.cs`, `MembershipsController.cs`).

---

### 2.4. `MOD-004`: Service Catalog & Dynamic Form Builder (Danh mục Dịch vụ và Biểu mẫu Động)
- **Module ID:** `MOD-004`
- **Module Name:** Service Catalog & Dynamic Form Builder
- **Purpose:** Cho phép mỗi doanh nghiệp tự định nghĩa danh mục dịch vụ nội bộ (`ServiceCategory`, `ServiceType`) và thiết kế biểu mẫu thu thập dữ liệu động có quản lý phiên bản (`FormDefinition`, `FormField`).
- **Key Responsibilities:**
  - Quản lý nhóm dịch vụ (`ServiceCategory`) và loại dịch vụ (`ServiceType`, ví dụ: "Cấp quyền GitLab", "Mua sắm thiết bị", "Nghỉ phép").
  - Quản lý định nghĩa biểu mẫu (`FormDefinition`) theo từng phiên bản (`Version`) và danh sách trường dữ liệu (`FormField`: kiểu dữ liệu, bắt buộc/tùy chọn, quy tắc kiểm tra hợp lệ, thứ tự hiển thị).
  - Bảo toàn tính bất biến của các phiên bản biểu mẫu đã xuất bản để các `RequestRecord` lịch sử luôn hiển thị đúng cấu trúc tại thời điểm gửi (`BR-005`).
- **Non-Responsibilities:**
  - Không lưu trữ giá trị dữ liệu do người dùng điền trên từng yêu cầu (thuộc `MOD-006` — `RequestFieldValue`).
- **Inputs:** Cấu hình danh mục dịch vụ, định nghĩa trường biểu mẫu, yêu cầu xuất bản phiên bản biểu mẫu mới, dữ liệu đầu vào cần kiểm tra theo schema form.
- **Outputs:** Schema biểu mẫu động đang hoạt động (`Active FormDefinition`) cho giao diện tạo Request và cho AI Assistant (`MOD-012`), kết quả xác thực dữ liệu trường động.
- **Data Ownership:** `ServiceCategories`, `ServiceTypes`, `FormDefinitions`, `FormFields`.
- **Dependencies:** `MOD-002` (ràng buộc `TenantId`), `MOD-003` (kiểm tra quyền `services.read` / `services.configure`), `MOD-013` (ghi `AuditLog`).
- **Public Interfaces:** `INT-005` (`ServiceCategoriesController`, `ServicesController`, `FormsController`).
- **Security/Tenant Requirements:** Mỗi Tenant chỉ nhìn thấy và sử dụng danh mục dịch vụ cùng biểu mẫu của chính Tenant đó; không cho phép ghi đè cấu trúc trường của phiên bản form đã phát sinh `RequestRecord`.
- **Related SRS Requirements:** `FR-SVC-001`, `FR-SVC-002`, `FR-SVC-003`, `FR-SVC-004`, `DR-005`.
- **Testing Approach:** Unit tests kiểm tra bộ xác thực trường biểu mẫu động (`FormField` validation rules); Integration tests kiểm tra tạo phiên bản form mới không làm thay đổi dữ liệu của các Request cũ.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`ServiceCategoriesController.cs`, `ServicesController.cs`, `FormsController.cs`).

---

### 2.5. `MOD-005`: Task Management & Execution (Quản lý và Thực thi Công việc)
- **Module ID:** `MOD-005`
- **Module Name:** Task Management & Execution
- **Purpose:** Quản lý trọn vẹn vòng đời thực thi công việc (`WorkTask`), bao gồm công việc độc lập hoặc công việc sinh ra từ yêu cầu dịch vụ, giao việc cá nhân/phòng ban, nhận việc (`Claim`), quản lý vướng mắc (`TaskBlocker`) và xác nhận hoàn thành (`Confirmation`).
- **Key Responsibilities:**
  - Quản lý thông tin công việc (`WorkTask`) với 9 trạng thái chuẩn theo `ADR-0010` (`Draft`, `Open`, `Assigned`, `InProgress`, `Blocked`, `PendingConfirmation`, `Completed`, `Cancelled`, `Archived`).
  - Hỗ trợ giao việc đích danh cho một cá nhân (`AssigneeUserId`) hoặc giao về hàng đợi phòng ban (`OwnerDepartmentId`, `AssigneeUserId = null`) để nhân viên trong phòng ban chủ động nhận việc (`Claim` theo `ADR-0002`).
  - Quản lý vòng đời vướng mắc (`TaskBlocker` theo `ADR-0003`) và bước xác nhận kết quả công việc (`Confirmation` theo `ADR-0004`, hỗ trợ duyệt `Approved` hoặc trả lại làm lại `Rejected` $\rightarrow$ `InProgress`).
  - Áp dụng nghiêm ngặt quy tắc phân quyền đọc 4 cấp (`tasks.read.tenant`, `tasks.read.managed`, `tasks.read.assigned`, `tasks.read.own` theo `ADR-0011`) và ngữ nghĩa trả về `404 Not Found` khi ngoài phạm vi (`ADR-0012`).
- **Non-Responsibilities:**
  - Không tự ý thay đổi trực tiếp các cột trạng thái vòng đời trên thực thể `WorkTask` bên ngoài `IWorkflowEngine` (`ADR-0001`).
- **Inputs:** Lệnh tạo Task, giao việc, nhận việc (`Claim`), bắt đầu xử lý, báo cáo/gỡ vướng mắc, gửi xác nhận, xác nhận/từ chối kết quả, hủy hoặc lưu trữ Task.
- **Outputs:** Bản ghi `WorkTask`, `TaskAssignment`, `TaskBlocker`, `Confirmation`, bản ghi lịch sử bất biến `TaskHistory`, cùng các sự kiện thông báo.
- **Data Ownership:** `WorkTasks`, `TaskAssignments`, `TaskBlockers`, `TaskHistories`, `Confirmations`.
- **Dependencies:** `MOD-007` (`IWorkflowEngine` điều phối chuyển trạng thái), `MOD-003` (kiểm tra thành viên phòng ban & phạm vi quản lý), `MOD-008` (tính hạn SLA), `MOD-010` (kiểm tra `CompletionEvidence` bắt buộc trước khi hoàn thành), `MOD-009` (thông báo), `MOD-013` (ghi `AuditLog`).
- **Public Interfaces:** `INT-009` (`TasksController`, `TaskBlockersController`, `ConfirmationsController`).
- **Security/Tenant Requirements:** Cưỡng chế kiểm tra quyền đọc theo phạm vi (`ADR-0011`), trả về `404` không tiết lộ sự tồn tại của bản ghi ngoài phạm vi (`ADR-0012`), kiểm tra khóa lạc quan (`Version` / `ADR-0006`) và bảo vệ bất biến tại `BizFlowDbContext.SaveChangesAsync`.
- **Related SRS Requirements:** `FR-TSK-001`, `FR-TSK-002`, `FR-TSK-003`, `FR-TSK-004`, `FR-TSK-005`, `FR-TSK-006`, `FR-TSK-007`, `FR-TSK-008`, `DR-008`, `DR-009`.
- **Testing Approach:** Bộ kiểm thử đơn vị và tích hợp chuyên sâu đối chiếu từng quyết định `ADR-0001` đến `ADR-0012` (bao gồm kiểm thử xung đột nhận việc đồng thời, chặn ghi trực tiếp không qua `TaskWorkflowEngine`, và kiểm thử `404` liên tenant).
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`TasksController.cs`, `TaskBlockersController.cs`, `ConfirmationsController.cs`, `TaskWorkflowEngine.cs`).

---

### 2.6. `MOD-006`: Request Management (Quản lý Yêu cầu Dịch vụ Nội bộ)
- **Module ID:** `MOD-006`
- **Module Name:** Request Management
- **Purpose:** Tiếp nhận, lưu trữ, theo dõi vòng đời các yêu cầu dịch vụ nội bộ (`RequestRecord`) của nhân viên, lưu giá trị các trường biểu mẫu động (`RequestFieldValue`) và liên kết yêu cầu với các công việc thực thi (`RequestTaskLink`).
- **Key Responsibilities:**
  - Cho phép nhân viên tạo bản nháp (`Draft`) hoặc gửi chính thức (`Submitted`) yêu cầu dịch vụ dựa trên `ServiceType` và `FormDefinition`.
  - Lưu trữ giá trị các trường dữ liệu động (`RequestFieldValue`) và lịch sử biến động (`RequestHistory`).
  - Liên kết hai chiều giữa `RequestRecord` và một hoặc nhiều `WorkTask` thực thi thông qua `RequestTaskLink`.
  - Quản lý các chuyển đổi trạng thái của yêu cầu (`Draft`, `Submitted`, `InReview`, `Approved`, `Rejected`, `InExecution`, `Completed`, `Cancelled`, `Closed`, `Archived`).
- **Non-Responsibilities:**
  - Không định nghĩa cấu trúc biểu mẫu động (thuộc `MOD-004`).
  - Không trực tiếp quản lý chi tiết từng bước nhận việc/xử lý công việc của nhân viên thực thi (thuộc `MOD-005` và `MOD-007`).
- **Inputs:** Yêu cầu tạo/cập nhật/gửi/hủy/đóng Request, dữ liệu trường biểu mẫu động, quyết định phê duyệt/từ chối/yêu cầu bổ sung thông tin.
- **Outputs:** Bản ghi `RequestRecord`, `RequestFieldValue`, `RequestHistory`, `RequestTaskLink`, và kích hoạt quy trình/công việc liên quan.
- **Data Ownership:** `RequestRecords`, `RequestFieldValues`, `RequestHistories`, `RequestTaskLinks`.
- **Dependencies:** `MOD-004` (xác thực theo `FormDefinition`), `MOD-007` (định tuyến & quy trình phê duyệt), `MOD-005` (tạo/đồng bộ trạng thái `WorkTask`), `MOD-008` (áp dụng `SlaProfile`), `MOD-009` (gửi thông báo), `MOD-013` (ghi `AuditLog`).
- **Public Interfaces:** `INT-008` (`RequestsController`, `ApprovalsController`).
- **Security/Tenant Requirements:** Cô lập theo `TenantId`; kiểm soát quyền xem chi tiết yêu cầu (người tạo, người phê duyệt, người được giao việc liên quan, quản lý phòng ban trong phạm vi, hoặc quản trị viên); trả về `404` nếu truy cập trái phép.
- **Related SRS Requirements:** `FR-REQ-001`, `FR-REQ-002`, `FR-REQ-003`, `FR-REQ-004`, `FR-REQ-005`, `FR-REQ-006`, `DR-006`.
- **Testing Approach:** Unit tests kiểm tra quy tắc chuyển trạng thái Request và xác thực trường động; Integration tests kiểm tra luồng gửi Request $\rightarrow$ sinh Task liên kết $\rightarrow$ ghi `RequestHistory` và `AuditLog`.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (Bốn bảng `RequestRecord`, `RequestFieldValue`, `RequestHistory`, `RequestTaskLink` và `RequestsController`, `ApprovalsController` đã triển khai; phần tích hợp với 5 bảng `RoutingRule`/`ApprovalInstance` đang ở mức `PARTIAL`).

---

### 2.7. `MOD-007`: Workflow, Routing & Approval Engine (Bộ máy Quy trình, Định tuyến và Phê duyệt)
- **Module ID:** `MOD-007`
- **Module Name:** Workflow, Routing & Approval Engine
- **Purpose:** Quản lý định nghĩa quy trình (`WorkflowDefinition`, `WorkflowVersion`, `WorkflowStep`), thực thi bộ máy chuyển trạng thái công việc chính tắc (`TaskWorkflowEngine` theo `ADR-0001`), và (trong kiến trúc mục tiêu) đánh giá quy tắc định tuyến (`RoutingRule`) cùng quy trình phê duyệt đa cấp (`ApprovalRule`, `ApprovalInstance`).
- **Key Responsibilities:**
  - Cung cấp cài đặt `TaskWorkflowEngine` (`IWorkflowEngine`) làm cổng kiểm soát độc quyền cho mọi chuyển đổi trạng thái, giao việc, nhận việc (`Claim`), chặn (`Block`) và xác nhận (`Confirm`) của `WorkTask`.
  - Quản lý cấu hình `WorkflowDefinition`, `WorkflowVersion`, `WorkflowStep` và trạng thái thực thi `WorkflowInstance`.
  - **(Mục tiêu / `PARTIAL GAP`):** Lưu trữ và đánh giá `RoutingRule` để tự động chọn quy trình/phòng ban xử lý dựa trên thuộc tính Request, và quản lý `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance` cho phê duyệt đa bước tuần tự.
- **Non-Responsibilities:**
  - Không cho phép thực thi mã tùy ý (arbitrary script execution) trong định nghĩa quy trình để đảm bảo an toàn đa thuê bao.
- **Inputs:** Lệnh chuyển trạng thái từ `MOD-005`/`MOD-006`, cấu hình quy trình từ Company Admin, quyết định duyệt/từ chối từ Approver.
- **Outputs:** Trạng thái mới đã được xác thực hợp lệ trên `WorkTask` / `WorkflowInstance`, bản ghi lịch sử chuyển bước, lệnh phát sinh bước tiếp theo.
- **Data Ownership:**
  - **Đã triển khai (4 bảng):** `WorkflowDefinitions`, `WorkflowVersions`, `WorkflowSteps`, `WorkflowInstances`.
  - **Chưa triển khai trong DB (5 bảng mục tiêu):** `RoutingRules`, `ApprovalRules`, `ApprovalRuleVersions`, `ApprovalInstances`, `ApprovalStepInstances`.
- **Dependencies:** `MOD-003` (xác định người phê duyệt, trưởng phòng ban và quyền hạn), `MOD-005` (cập nhật `WorkTask`), `MOD-009` (phát thông báo chuyển bước), `MOD-013` (ghi `AuditLog` trong cùng giao dịch theo `ADR-0005`).
- **Public Interfaces:** `INT-006` (`WorkflowsController`), `INT-008` (`ApprovalsController`), `INT-016` (`IWorkflowEngine`).
- **Security/Tenant Requirements:** Mọi lệnh gọi vào `IWorkflowEngine` phải kiểm tra `TenantId` của người gọi khớp với bản ghi mục tiêu (`ADR-0007`), kiểm tra phiên bản đồng thời (`ADR-0006`) và đánh dấu `WorkTask.MarkWorkflowMutation()` để vượt qua chốt chặn của `BizFlowDbContext`.
- **Related SRS Requirements:** `FR-WF-001`, `FR-WF-002`, `FR-WF-003`, `FR-WF-004`, `FR-WF-005`, `DR-007`.
- **Testing Approach:** Unit tests kiểm tra đầy đủ ma trận chuyển trạng thái hợp lệ/không hợp lệ của `TaskWorkflowEngine`; Integration tests kiểm tra cơ chế chặn sửa trạng thái trực tiếp tại `BizFlowDbContext.ValidateTaskWorkflowMutations`.
- **Status:** `CONFIRMED` | `PARTIAL` (`TaskWorkflowEngine` và 4 bảng `Workflow*` đã `IMPLEMENTED`; 5 bảng `RoutingRule` & `Approval*` là khoảng trống triển khai được theo dõi tại `OD-001` và `OD-002`).

---

### 2.8. `MOD-008`: SLA & Calendar Management (Quản lý Cam kết Thời gian Xử lý và Lịch làm việc)
- **Module ID:** `MOD-008`
- **Module Name:** SLA & Calendar Management
- **Purpose:** Quản lý cấu hình cam kết mức dịch vụ (`SlaProfile`), lịch làm việc (`WorkingHour`), ngày nghỉ lễ (`Holiday`), tính toán thời hạn phản hồi/hoàn thành và theo dõi trạng thái tuân thủ SLA.
- **Key Responsibilities:**
  - Quản lý cấu hình `SlaProfile` (thời gian mục tiêu phản hồi đầu tiên, thời gian hoàn thành, ngưỡng cảnh báo sớm theo mức độ ưu tiên/loại dịch vụ).
  - Quản lý lịch làm việc trong tuần (`WorkingHour`) và danh sách ngày nghỉ lễ (`Holiday`) theo múi giờ của từng Tenant.
  - Tính toán mốc hạn xử lý (`DueAt`, `ResponseDueAt`, `ResolutionDueAt`) có trừ thời gian ngoài giờ làm việc và ngày nghỉ lễ (`BR-014..016`).
  - **(Mục tiêu / `PARTIAL GAP`):** Duy trì trạng thái đồng hồ SLA (`SLAState` — đang chạy, tạm dừng khi chờ thông tin/Blocked, đã đạt, đã vi phạm) và ghi lịch sử sự kiện SLA (`SLAEvent`).
- **Non-Responsibilities:**
  - Không tự động thay đổi nội dung nghiệp vụ của Request/Task khi quá hạn (chỉ đánh dấu trạng thái SLA và phát cảnh báo/escalation).
- **Inputs:** Cấu hình `SlaProfile`, `WorkingHour`, `Holiday`; sự kiện khởi tạo hoặc chuyển trạng thái của `RequestRecord` và `WorkTask`.
- **Outputs:** Mốc thời gian hết hạn chính xác, cờ cảnh báo sắp quá hạn (`NearBreach`) hoặc đã quá hạn (`Breached`), dữ liệu thống kê tuân thủ SLA cho báo cáo.
- **Data Ownership:**
  - **Đã triển khai (3 bảng):** `SlaProfiles`, `Holidays`, `WorkingHours`.
  - **Chưa triển khai trong DB (2 bảng mục tiêu):** `SLAStates`, `SLAEvents`.
- **Dependencies:** `MOD-002` (múi giờ trong `TenantSetting`), `MOD-009` (gửi cảnh báo sắp quá hạn / quá hạn), `MOD-013` (ghi `AuditLog`).
- **Public Interfaces:** `INT-007` (`SlaProfilesController`, `CalendarsController`), `INT-021` (`SLA Evaluation Contract`).
- **Security/Tenant Requirements:** Chỉ `Company Admin` hoặc người có quyền `sla.configure` mới được thay đổi cấu hình SLA và lịch nghỉ lễ của Tenant.
- **Related SRS Requirements:** `FR-SLA-001`, `FR-SLA-002`, `FR-SLA-003`, `FR-SLA-004`, `DR-010`.
- **Testing Approach:** Unit tests kiểm tra thuật toán cộng giờ làm việc qua ngày cuối tuần và ngày nghỉ lễ; Integration tests kiểm tra CRUD `SlaProfile`, `WorkingHour`, `Holiday`.
- **Status:** `CONFIRMED` | `PARTIAL` (3 bảng cấu hình & thuật toán tính hạn đã `IMPLEMENTED`; 2 bảng `SLAState`/`SLAEvent` và tiến trình quét tự động được theo dõi tại `OD-003`, `OD-020`).

---

### 2.9. `MOD-009`: Notification & Real-Time Hub (Thông báo Đa kênh và Kênh Thời gian Thực)
- **Module ID:** `MOD-009`
- **Module Name:** Notification & Real-Time Hub
- **Purpose:** Cung cấp thông báo trong ứng dụng (`In-App Notification`), đẩy sự kiện thời gian thực qua WebSocket/SignalR (`NotificationHub`), quản lý tùy chọn nhận thông báo của người dùng (`NotificationPreference`) và xếp hàng gửi email ra ngoài (`EmailQueueItem`).
- **Key Responsibilities:**
  - Tạo và lưu trữ thông báo trong ứng dụng (`Notification`) gắn với từng người nhận (`RecipientUserId`, `TenantId`).
  - Quản lý kết nối WebSocket đang hoạt động (`NotificationConnection`) và phát sự kiện tức thời qua `NotificationHub` (`/hubs/notifications`).
  - Lưu trữ hàng đợi thông báo (`NotificationQueueItem`) và hàng đợi email (`EmailQueueItem`) theo mẫu Transactional Outbox để đảm bảo độ tin cậy (`NFR-REL-002`).
  - Cho phép người dùng bật/tắt kênh nhận thông báo theo loại sự kiện (`NotificationPreference`).
- **Non-Responsibilities:**
  - Không làm thất bại giao dịch nghiệp vụ gốc (như tạo Request hay chuyển trạng thái Task) nếu kết nối SignalR hoặc máy chủ SMTP bên ngoài gặp sự cố.
- **Inputs:** Sự kiện nghiệp vụ từ các module (`RequestSubmitted`, `TaskAssigned`, `ConfirmationRequested`, `SlaWarning`...), yêu cầu đọc/đánh dấu đã đọc từ người dùng.
- **Outputs:** Bản ghi `Notification`, sự kiện đẩy qua SignalR WSS, bản ghi hàng đợi `EmailQueueItem` để chuyển tới máy chủ SMTP.
- **Data Ownership:** `Notifications`, `NotificationPreferences`, `NotificationQueueItems`, `EmailQueueItems`, `NotificationConnections`.
- **Dependencies:** `MOD-001` (xác thực JWT trên SignalR Hub và tự động đóng kết nối khi token hết hạn), `MOD-BG-001` (quét và gửi các mục trong `EmailQueueItems` / `NotificationQueueItems`).
- **Public Interfaces:** `INT-011` (`NotificationsController`), `INT-012` (`NotificationHub`), `INT-020` (`Outbox Queue`), `INT-026` (`SMTP Adapter`).
- **Security/Tenant Requirements:** Người dùng chỉ nhận và đọc được thông báo của chính mình trong phạm vi `CurrentTenantId`; `NotificationHub` bật `CloseOnAuthenticationExpiration = true` (`Program.cs` dòng 152).
- **Related SRS Requirements:** `FR-NOT-001`, `FR-NOT-002`, `FR-NOT-003`, `FR-NOT-004`, `IR-UI-002`, `IR-EXT-002`, `NFR-REL-002`, `DR-011`.
- **Testing Approach:** Integration tests kiểm tra tạo thông báo, lọc theo người dùng/tenant và kiểm tra kết nối `NotificationHub`.
- **Status:** `CONFIRMED` | `PARTIAL` (Toàn bộ 5 bảng DB, `NotificationsController` và `NotificationHub` đã `IMPLEMENTED`; worker gửi SMTP ngầm từ `EmailQueueItems` đang ở trạng thái `PROPOSED` theo `OD-005`, `OD-020`).

---

### 2.10. `MOD-010`: Collaboration, Evidence & Archival (Cộng tác, Bằng chứng Hoàn thành và Lưu trữ Hồ sơ)
- **Module ID:** `MOD-010`
- **Module Name:** Collaboration, Evidence & Archival
- **Purpose:** Hỗ trợ trao đổi bình luận (`Comment`), quản lý tệp đính kèm (`Attachment`), thu thập bằng chứng hoàn thành công việc bắt buộc (`CompletionEvidence`) và tra cứu hồ sơ đã lưu trữ bất biến (`ArchivedRecordView`).
- **Key Responsibilities:**
  - Quản lý bình luận nội bộ (`Comment`) gắn với `RequestRecord` hoặc `WorkTask`.
  - Xử lý tải lên, lưu trữ siêu dữ liệu (tên tệp, MIME type, kích thước, mã băm kiểm tra, khóa đối tượng) và tải xuống an toàn cho `Attachment` và `CompletionEvidence` thông qua `IObjectStorageProvider`.
  - Xác minh điều kiện đã nộp đủ bằng chứng hoàn thành (`CompletionEvidence`) trước khi cho phép chuyển `WorkTask` sang `PendingConfirmation` hoặc `Completed` (nếu công việc yêu cầu bằng chứng).
  - Cung cấp chỉ mục và giao diện tra cứu chỉ đọc (`ArchivedRecordView`) cho các Request và Task đã chuyển sang trạng thái `Archived` (`BR-021`).
- **Non-Responsibilities:**
  - Không cho phép mở quyền truy cập bucket lưu trữ tệp công khai (Public URL không qua xác thực bị cấm hoàn toàn).
- **Inputs:** Nội dung bình luận, luồng byte tệp tải lên, yêu cầu tải xuống tệp, truy vấn tìm kiếm hồ sơ lưu trữ.
- **Outputs:** Bản ghi `Comment`, `Attachment`, `CompletionEvidence`, tệp lưu trên S3/MinIO hoặc đĩa cục bộ theo khóa `{tenantId}/...`, kết quả tìm kiếm `ArchivedRecordView`.
- **Data Ownership:** `Comments`, `Attachments`, `CompletionEvidences`, `ArchivedRecordViews`.
- **Dependencies:** `MOD-003` (kiểm tra quyền truy cập bản ghi cha và quyền `records.archive.read`), `MOD-005`/`MOD-006` (ràng buộc trạng thái Request/Task), `MOD-013` (ghi `AuditLog` khi tải lên/tải xuống/lưu trữ).
- **Public Interfaces:** `INT-010` (`CommentsController`, `AttachmentsController`, `EvidenceController`, `RecordsController`), `INT-024` (`IObjectStorageProvider`).
- **Security/Tenant Requirements:** Khóa đối tượng trên kho lưu trữ phải bắt đầu bằng `{tenantId}/`; mọi thao tác tải xuống phải kiểm tra quyền đọc của người dùng trên Request/Task chứa tệp đó; hồ sơ đã `Archived` bị khóa chỉnh sửa hoàn toàn (`SEC-DATA-002`, `DR-012`).
- **Related SRS Requirements:** `FR-COL-001`, `FR-COL-002`, `FR-COL-003`, `FR-COL-004`, `FR-COL-005`, `IR-EXT-003`, `SEC-DATA-002`, `DR-012`.
- **Testing Approach:** Integration tests kiểm tra tải tệp qua `LocalStorageProvider`/`S3CompatibleStorageProvider`, chặn truy cập tệp liên tenant và chặn sửa đổi bản ghi đã lưu trữ.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`CommentsController.cs`, `AttachmentsController.cs`, `EvidenceController.cs`, `RecordsController.cs`, `S3CompatibleStorageProvider.cs`, `LocalStorageProvider.cs`).

---

### 2.11. `MOD-011`: Reporting, Analytics & Dashboard (Báo cáo, Phân tích và Bảng điều khiển)
- **Module ID:** `MOD-011`
- **Module Name:** Reporting, Analytics & Dashboard
- **Purpose:** Cung cấp bảng điều khiển vận hành (`Dashboard`), báo cáo khối lượng công việc, báo cáo tuân thủ SLA theo phạm vi phòng ban/công ty và hỗ trợ kết xuất báo cáo (`ReportExportJob`).
- **Key Responsibilities:**
  - Tổng hợp số liệu thời gian thực cho Dashboard cá nhân, Dashboard cấp quản lý phòng ban và Dashboard cấp công ty.
  - Cung cấp các báo cáo chuyên sâu về tình trạng xử lý Request/Task, tỷ lệ đúng hạn/vi phạm SLA, phân bổ tải công việc giữa các phòng ban và nhân sự.
  - Quản lý ảnh chụp số liệu (`ReportSnapshot`) và công việc kết xuất tệp báo cáo (`ReportExportJob`).
- **Non-Responsibilities:**
  - Chỉ thực hiện truy vấn đọc/tổng hợp và ghi bảng xuất báo cáo; tuyệt đối không sửa đổi dữ liệu nghiệp vụ của Request hay Task.
- **Inputs:** Bộ lọc thời gian, phòng ban, loại dịch vụ, trạng thái từ người dùng có quyền xem báo cáo.
- **Outputs:** Dữ liệu biểu đồ/bảng tổng hợp trên Dashboard, báo cáo chi tiết và tệp báo cáo kết xuất.
- **Data Ownership:** `ReportExportJobs`, `ReportSnapshots`.
- **Dependencies:** `MOD-003` (giới hạn dữ liệu báo cáo theo `ManagementScope` của trưởng phòng theo `BR-003`), `MOD-005` (`WorkTasks`), `MOD-006` (`RequestRecords`), `MOD-008` (dữ liệu SLA), `MOD-010` (`IObjectStorageProvider` để lưu tệp export).
- **Public Interfaces:** `INT-013` (`DashboardController`, `ReportsController`, `ReportExportsController`).
- **Security/Tenant Requirements:** Trưởng phòng (`Department Manager`) chỉ được xem số liệu tổng hợp của các phòng ban thuộc `ManagementScope` của mình (`BR-003`); chỉ người có quyền cấp công ty (`reports.company`) mới xem được số liệu toàn Tenant.
- **Related SRS Requirements:** `FR-RPT-001`, `FR-RPT-002`, `FR-RPT-003`, `FR-RPT-004`, `FR-RPT-005`, `DR-013`.
- **Testing Approach:** Unit & Integration tests xác minh tính chính xác của số liệu tổng hợp và kiểm tra cô lập phạm vi phòng ban giữa hai Manager khác nhau trong cùng một Tenant.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`DashboardController.cs`, `ReportsController.cs`, `ReportExportsController.cs`).

---

### 2.12. `MOD-012`: AI Assistant & Governance (Trợ lý Trí tuệ Nhân tạo và Quản trị AI)
- **Module ID:** `MOD-012`
- **Module Name:** AI Assistant & Governance
- **Purpose:** Cung cấp 9 tính năng trợ lý AI có kiểm soát con người (`Human-in-the-Loop` theo `ADR-0008`), đảm bảo an toàn dữ liệu đa thuê bao khi xây dựng ngữ cảnh prompt và lưu vết kiểm toán toàn bộ lệnh gọi mô hình.
- **Key Responsibilities:**
  - Cung cấp 9 năng lực hỗ trợ AI (`FR-AI-001..009`):
    1. Hỗ trợ tiếp nhận & trích xuất thông tin yêu cầu từ ngôn ngữ tự nhiên (`Intake Assist`).
    2. Gợi ý phân loại dịch vụ & mức độ ưu tiên (`Classification & Priority Suggestion`).
    3. Phát hiện thông tin còn thiếu hoặc mơ hồ trong yêu cầu (`Missing Information Detection`).
    4. Gợi ý định tuyến phòng ban/quy trình xử lý (`Routing & Assignment Recommendation`).
    5. Tóm tắt nội dung và lịch sử Request/Task dài (`Contextual Summarization`).
    6. Đề xuất bước hành động tiếp theo cho người xử lý (`Next-Best-Action Suggestion`).
    7. Phân tích nguyên nhân và gợi ý hướng tháo gỡ vướng mắc (`Blocker Analysis`).
    8. Tóm tắt nhận định từ số liệu báo cáo vận hành (`Report Narrative Summary`).
    9. Quản trị cấu hình bật/tắt AI và giới hạn sử dụng theo từng Tenant (`Tenant AI Governance`).
  - Thực thi việc đọc dữ liệu ngữ cảnh độc quyền qua `AiContextReader` để đảm bảo chỉ nạp dữ liệu cùng `TenantId` mà người dùng gọi AI có quyền đọc hợp lệ.
  - Quản lý danh mục công cụ cho phép (`IAiToolRegistry`) và chỉ thực thi thay đổi thông qua `IAiActionDispatcher` sau khi người dùng bấm xác nhận.
  - Ghi nhận mọi đề xuất (`AiSuggestion`) và nhật ký thực thi kỹ thuật (`AiExecutionLog`: tên mô hình, độ trễ, trạng thái thành công/fallback/lỗi).
- **Non-Responsibilities:**
  - **Cấm** tự động phê duyệt yêu cầu, tự động từ chối yêu cầu, tự động đóng công việc hoặc tự ý sửa đổi dữ liệu nghiệp vụ mà không có hành động xác nhận của người dùng (`ADR-0008`, `BR-018`).
- **Inputs:** Yêu cầu hỗ trợ AI từ người dùng kèm `TargetEntityId` và văn bản đầu vào, cấu hình `AiConfig` của Tenant, lệnh xác nhận/bỏ qua đề xuất AI.
- **Outputs:** Đối tượng `AiSuggestion` có cấu trúc (kèm giải thích lý do/độ tin cậy), bản ghi `AiExecutionLog`, hoặc kết quả fallback an toàn khi OpenAI API không khả dụng.
- **Data Ownership:** `AiSuggestions`, `AiConfigs`, `AiExecutionLogs`.
- **Dependencies:** `MOD-003`/`MOD-005`/`MOD-006`/`MOD-004` (đọc ngữ cảnh chỉ đọc qua `AiContextReader`), `BizFlow.Infrastructure` (`OpenAiModelClient`), `MOD-013` (ghi `AuditLog` khi người dùng áp dụng đề xuất AI).
- **Public Interfaces:** `INT-014` (`AiAssistantController`, `AiGovernanceController`), `INT-017` (`AiContextReader`), `INT-018` (`IAiToolRegistry`, `IAiActionDispatcher`), `INT-025` (`IAiModelClient`).
- **Security/Tenant Requirements:** Cưỡng chế lọc quyền qua `AiContextReader` (trả về `404` nếu người gọi không có quyền đọc bản ghi mục tiêu); áp dụng Rate Limiter riêng cho endpoint AI; timeout cứng `45s`; không gửi thông tin bí mật hệ thống (mật khẩu, token) sang OpenAI (`SEC-AI-001..002`).
- **Related SRS Requirements:** `FR-AI-001`, `FR-AI-002`, `FR-AI-003`, `FR-AI-004`, `FR-AI-005`, `FR-AI-006`, `FR-AI-007`, `FR-AI-008`, `FR-AI-009`, `IR-EXT-001`, `NFR-REL-003`, `SEC-AI-001`, `SEC-AI-002`, `DR-014`.
- **Testing Approach:** Unit & Integration tests (`AiIntegrationBoundaryTests`) kiểm tra: (1) chặn AI đọc bản ghi ngoài phạm vi quyền của người gọi, (2) đảm bảo gợi ý AI không tự sửa trạng thái Task/Request trước khi confirm, và (3) fallback an toàn khi `OpenAiModelClient` lỗi hoặc timeout.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`AiAssistantController.cs`, `AiGovernanceController.cs`, `AiAssistantService.cs`, `AiContextReader.cs`, `OpenAiModelClient.cs`).

---

### 2.13. `MOD-013`: Audit & Security Compliance (Kiểm toán và Tuân thủ Bảo mật)
- **Module ID:** `MOD-013`
- **Module Name:** Audit & Security Compliance
- **Purpose:** Ghi nhận, bảo vệ tính bất biến và cung cấp khả năng tra cứu đối với toàn bộ nhật ký kiểm toán nghiệp vụ (`AuditLog`) và sự kiện an ninh (`SecurityEvent`) ở cả cấp Tenant và cấp Platform.
- **Key Responsibilities:**
  - Ghi nhận đồng bộ trong cùng giao dịch cơ sở dữ liệu mọi hành động quan trọng (thay đổi trạng thái Request/Task, thay đổi phân quyền RBAC, duyệt/đình chỉ Tenant, cấu hình hệ thống, áp dụng gợi ý AI) theo `ADR-0005`.
  - Ghi nhận các sự kiện an ninh (`SecurityEvent`: đăng nhập thất bại, thu hồi phiên, cố gắng truy cập trái phép liên tenant).
  - Cung cấp giao diện tra cứu nhật ký kiểm toán cho `Internal Auditor` / `Company Admin` trong phạm vi Tenant (`/api/audit`) và cho `Platform Super Admin` ở cấp toàn nền tảng (`/api/platform/audit`).
- **Non-Responsibilities:**
  - Không bao giờ cho phép chỉnh sửa hoặc xóa bất kỳ bản ghi `AuditLog` hay `SecurityEvent` nào thông qua API hoặc tầng Persistence.
- **Inputs:** Các bản ghi sự kiện kiểm toán từ toàn bộ các module (`MOD-001..012`), truy vấn lọc/tìm kiếm nhật ký từ Auditor.
- **Outputs:** Danh sách phân trang của `AuditLog` và `SecurityEvent` kèm `TraceId`, `ActorUserId`, `Action`, `EntityType`, `EntityId`, `BeforeJson`/`AfterJson`, `TimestampUtc`.
- **Data Ownership:** `AuditLogs`, `SecurityEvents` (cùng quy tắc bảo vệ bất biến cho `TaskHistories` và `RequestHistories`).
- **Dependencies:** Tầng nền tảng không phụ thuộc ngược lên các module nghiệp vụ phía trên; được cưỡng chế trực tiếp tại `BizFlowDbContext` và migration `EnforceAppendOnlyHistoryAndAuditTables`.
- **Public Interfaces:** `INT-014` (`AuditController`), `INT-002` (`PlatformAuditController`), `INT-019` (`Audit Recording Contract`).
- **Security/Tenant Requirements:** Cưỡng chế `Append-Only` ở cả mức ứng dụng (`BizFlowDbContext.SaveChangesAsync` chặn `EntityState.Modified` và `EntityState.Deleted`) và mức cơ sở dữ liệu (`EnforceAppendOnlyHistoryAndAuditTables`); cô lập dữ liệu tra cứu `/api/audit` theo `CurrentTenantId` (`SEC-AUD-001..002`).
- **Related SRS Requirements:** `FR-AUD-001`, `FR-AUD-002`, `FR-AUD-003`, `SEC-AUD-001`, `SEC-AUD-002`, `DR-015`.
- **Testing Approach:** Integration tests kiểm tra mọi lệnh workflow đều sinh `AuditLog` trong cùng transaction và kiểm thử cố tình gọi `Update`/`Remove` trên `AuditLog` bị chặn bởi lỗi `PERSISTENCE.HARD_DELETE_DENIED` / `PERSISTENCE.IMMUTABLE_FIELD`.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`AuditController.cs`, `PlatformAuditController.cs`, `BizFlowDbContext.cs`).

---

### 2.14. `MOD-014`: Robot Chatbot & Natural Language Assistance (Trợ lý Robot Thông minh & Vấn tin Tự nhiên)
- **Module ID:** `MOD-014`
- **Module Name:** Robot Chatbot & Natural Language Assistance
- **Purpose:** Quản lý vòng đời hiển thị và phiên làm việc của Trợ lý Robot nhúng UI (`sessionStorage`), thực thi các truy vấn tiến độ/báo cáo bằng ngôn ngữ tự nhiên ràng buộc theo quyền hạn thực tế (`RBAC` & `ManagementScope`), điều phối các lệnh nhanh slash commands (`/task`, `/notifications`), và chuyển đổi đoạn văn bản tự nhiên thành danh sách công việc đề xuất có cấu trúc phục vụ quy trình bóc tách việc có xác nhận con người bắt buộc (`BR-028`).
- **Key Responsibilities:**
  - **Quản lý Vòng đời Widget Robot UI (`FR-AI-011`, `BR-029`, `OD-029`):** Cung cấp cấu hình mặc định mở khi người dùng đăng nhập; khi người dùng nhấn nút đóng [X], lưu cờ `bizflow_robot_dismissed = true` trong `sessionStorage` của trình duyệt; tự động mở lại khi đăng nhập mới hoặc mở phiên làm việc mới trên trình duyệt.
  - **Duy trì Lịch sử Hội thoại Robot (`DR-016`):** Quản lý thực thể `RobotConversation` và `RobotMessage` phân vùng theo `TenantId` và `UserId`; hỗ trợ tải lại lịch sử tin nhắn gần nhất và xóa lịch sử khi người dùng yêu cầu.
  - **Vấn tin Tiến độ & Báo cáo Tự nhiên Ràng buộc Quyền Hạn (`FR-AI-012`, `BR-027`, `OD-030`):** Phối hợp với `MOD-012` (`AiContextReader`) để trích xuất dữ liệu công việc, hạn chót, công việc quá hạn, việc chờ duyệt dựa trên đúng quyền hạn RBAC và phạm vi quản lý `ManagementScope` của người gọi.
  - **Chốt chặn Chống Ảo giác (`Anti-Hallucination Guardrail` — `BR-027`):** Cưỡng chế prompt hệ thống và logic kiểm tra: bot chỉ trả lời các thông tin có bằng chứng xác thực trong tập ngữ cảnh đã lọc quyền; nghiêm cấm tự suy diễn hoặc tiết lộ dữ liệu ngoài phạm vi quyền; phản hồi từ chối rõ ràng khi không tìm thấy dữ liệu phù hợp.
  - **Điều phối Slash Commands (`/task`, `/notifications`):** Bắt và phân tích cú pháp lệnh `/task <đoạn văn>` để khởi chạy quy trình bóc tách việc AI, và `/notifications` để mở giao diện/hội thoại cấu hình mốc nhắc hạn.
  - **Bóc tách Đoạn văn thành Việc Cần Làm (`FR-AI-013`, `BR-028`, `OD-028`):** Gọi dịch vụ AI phân tích đoạn văn thành danh sách các đầu việc có cấu trúc: phân biệt `Support Request` và `Task Assignment`, trích xuất tiêu đề, phòng ban nhận việc, mô tả chi tiết và checklist; trả về dữ liệu đề xuất cho Angular UI hiển thị bảng xem trước tương tác (preview checklist) với checkbox và khả năng chỉnh sửa trực tiếp.
  - **Cưỡng chế Xác nhận Bắt buộc từ Con người (`BR-028`):** Tuyệt đối không lưu dữ liệu vào bảng `WorkTask` hay `RequestRecord` nếu chưa nhận được lệnh xác nhận `POST /api/v1/tasks/ai-decomposition/confirm` (`IR-API-006`) từ người dùng.
- **Non-Responsibilities:**
  - Không tự ý ghi trực tiếp vào cơ sở dữ liệu mà không có xác nhận con người (`BR-028`).
  - Không cấp quyền vượt quá quyền RBAC của người dùng đang đăng nhập.
- **Inputs:** Tin nhắn ngôn ngữ tự nhiên từ người dùng, lệnh `/task` và `/notifications`, xác nhận tạo việc hàng loạt từ preview checklist UI.
- **Outputs:** Phản hồi hội thoại tự nhiên từ bot, danh sách công việc đề xuất (JSON), các bản ghi `WorkTask`/`RequestRecord` được commit sau khi người dùng xác nhận.
- **Data Ownership:** `RobotConversations`, `RobotMessages`.
- **Dependencies:** `MOD-001` (ngữ cảnh phiên `ITenantContext`), `MOD-003` (kiểm tra RBAC & `ManagementScope`), `MOD-005` (tạo `WorkTask`), `MOD-006` (tạo `RequestRecord`), `MOD-012` (`AiAssistanceService`, `AiContextReader`), `MOD-013` (`AuditLog`).
- **Public Interfaces:** `INT-014` (`AiAssistantController`), `INT-031` (`AiTaskDecompositionController`).
- **Security/Tenant Requirements:** Cách ly hội thoại tuyệt đối theo `TenantId` và `UserId`; áp dụng `Fail-Closed` nếu thiếu ngữ cảnh người dùng; kiểm tra quyền tạo việc của người dùng đối với từng phòng ban mục tiêu khi xác nhận tạo hàng loạt (`SEC-008`, `SEC-009`).
- **Related SRS Requirements:** `FR-AI-011`, `FR-AI-012`, `FR-AI-013`, `IR-API-006`, `BR-027`, `BR-028`, `BR-029`, `DR-016`, `TST-034`, `TST-035`.
- **Testing Approach:** Kiểm thử vòng đời widget robot trong `sessionStorage` (`TST-034`); kiểm thử an toàn câu hỏi hạn chót có ràng buộc `ManagementScope` và chống ảo giác (`TST-034`); kiểm thử bóc tách đoạn văn và xác nhận tạo việc bắt buộc (`TST-035`).
- **Status:** `CONFIRMED` | `NOT YET IMPLEMENTED` (Đặc tả thiết kế chi tiết sẵn sàng triển khai).

---

### 2.15. `MOD-015`: Deadline Reminder & Multi-Channel Notification Dispatcher (Điều phối Thông báo Nhắc Hạn Đa Kênh)
- **Module ID:** `MOD-015`
- **Module Name:** Deadline Reminder & Multi-Channel Notification Dispatcher
- **Purpose:** Quản lý lịch nhắc hạn công việc cá nhân hóa, điều phối phát thông báo tự động đa kênh theo thứ tự ưu tiên (In-App SignalR $\rightarrow$ Web Push VAPID $\rightarrow$ Fallback HTML Email), thực thi bộ lọc chống trùng lặp tuyệt đối và tự động hủy lịch nhắc khi công việc đã hoàn thành.
- **Key Responsibilities:**
  - **Quản lý Cấu hình Lịch Nhắc Hạn Cá nhân hóa (`FR-NOTIF-003`, `BR-030`, `OD-031`):** Cung cấp API tiếp nhận cấu hình các mốc nhắc nhở của người dùng (trước 24 giờ, 4 giờ, 1 giờ và ngay khi quá hạn `OVERDUE`) hoặc xử lý lệnh `/notifications` từ robot widget. Lưu vết tại bảng `DeadlineReminderSchedule`.
  - **Quản lý Đăng ký Web Push Subscription (`IR-EXT-003`):** Lưu trữ và quản lý thông tin đăng ký nhận thông báo đẩy W3C Push API / RFC 8292 VAPID của từng trình duyệt/thiết bị người dùng tại bảng `PushSubscription`.
  - **Quét Định kỳ và Phát Hiện Mốc Nhắc Hạn (`MOD-BG-001` Integration):** Tiến trình nền định kỳ quét các bản ghi nhắc hạn đến hạn kích hoạt (`ScheduledAt <= UtcNow` và `Status = Pending`).
  - **Cơ chế Phát Đa Kênh Thông minh (`Smart Multi-Channel Dispatcher` — `FR-NOTIF-004`, `BR-030`):**
    - **Bước 1 (In-App SignalR):** Kiểm tra trạng thái trực tuyến của người dùng tại `NotificationHub` (`INT-012`). Nếu đang kết nối, phát Toast thông báo nổi và cập nhật Notification Center.
    - **Bước 2 (Web Push VAPID):** Nếu người dùng offline hoặc không mở tab ứng dụng, gửi Web Push qua dịch vụ đẩy của trình duyệt (`IR-EXT-003`).
    - **Bước 3 (Fallback HTML Email):** Nếu người dùng chưa cấp quyền Web Push hoặc gửi Web Push thất bại (`410 Gone`, lỗi mạng), tự động gửi Email thông báo HTML chuyên nghiệp kèm nút CTA đưa thẳng vào công việc.
  - **Bộ Lọc Chống Trùng lặp Tuyệt đối (`Deduplication Guard` — `OD-032`):** Cưỡng chế chỉ mục duy nhất kết hợp `UNIQUE (TenantId, TaskId, UserId, TriggerType, ScheduledAt)` trên bảng `NotificationLog`.
  - **Tự động Hủy Nhắc Hạn khi Công việc Hoàn tất (`OD-032`):** Lắng nghe sự kiện chuyển đổi trạng thái công việc sang `SUBMITTED`, `CONFIRMED` hoặc `CLOSED` để đánh dấu `CANCELLED` toàn bộ các bản ghi nhắc hạn chưa phát thuộc công việc đó.
- **Non-Responsibilities:**
  - Không quản lý logic tính toán đồng hồ SLA (thuộc `MOD-008`).
  - Không gửi email tiếp thị hoặc email quảng cáo.
- **Inputs:** Cấu hình nhắc hạn từ người dùng, thông tin đăng ký PushSubscription, sự kiện đến hạn từ timer/worker, sự kiện hoàn tất công việc từ `TaskWorkflowEngine`.
- **Outputs:** Tín hiệu WebSocket qua SignalR, thông báo Web Push tới trình duyệt, thư điện tử HTML qua SMTP, bản ghi `NotificationLog`.
- **Data Ownership:** `DeadlineReminderSchedules`, `PushSubscriptions`, `NotificationLogs`.
- **Dependencies:** `MOD-001` (`ITenantContext`), `MOD-005` (theo dõi `WorkTask`), `MOD-009` (`NotificationHub`), `MOD-BG-001` (host worker), SMTP Relay, W3C Push Service (VAPID).
- **Public Interfaces:** `INT-011` (`NotificationsController`), `INT-012` (`NotificationHub`), `INT-032` (`DeadlineReminderController`).
- **Security/Tenant Requirements:** Mọi thông báo nhắc việc phải cô lập nghiêm ngặt theo `TenantId`; Web Push sử dụng cặp khóa VAPID bảo mật; thông tin tóm tắt trong push/email không chứa dữ liệu nhạy cảm (`SEC-005`, `SEC-010`).
- **Related SRS Requirements:** `FR-NOTIF-003`, `FR-NOTIF-004`, `IR-API-007`, `IR-EXT-003`, `BR-030`, `DR-017`, `TST-036`, `TST-037`.
- **Testing Approach:** Kiểm thử cấu hình nhắc hạn qua API và `/notifications` (`TST-036`); kiểm thử phát thông báo đa kênh theo thứ tự ưu tiên và kiểm tra không gửi trùng lặp qua `NotificationLog` (`TST-037`); kiểm thử tự động hủy nhắc hạn khi công việc hoàn thành trước hạn.
- **Status:** `CONFIRMED` | `NOT YET IMPLEMENTED` (Đặc tả thiết kế chi tiết sẵn sàng triển khai).

---

### 2.16. `MOD-FE-001`: Frontend Single-Page Application (Ứng dụng Giao diện Web Angular 22)
- **Module ID:** `MOD-FE-001`
- **Module Name:** Frontend Single-Page Application (Angular 22 SPA)
- **Purpose:** Cung cấp giao diện người dùng tương tác cho toàn bộ các vai trò (Applicant, Platform Admin, Company Admin, Department Manager, Approver, Staff, Internal Auditor) trên trình duyệt web hiện đại.
- **Key Responsibilities:**
  - Quản lý định tuyến phía client (`app.routes.ts` gồm 28 route) được bảo vệ bởi bộ 3 Guard: `sessionGuard` (kiểm tra đăng nhập), `tenantGuard` (kiểm tra ngữ cảnh workspace/tenant đang hoạt động) và `permissionGuard` (kiểm tra quyền hiển thị màn hình).
  - Cung cấp điểm vào công khai đăng ký doanh nghiệp `/register-company` kèm liên kết CTA từ màn hình đăng nhập `/login` (`UI-01`) và màn hình xác minh email `/verify-email` (`IR-UI-004`, `UC-013`, `OD-021`, `OD-022`).
  - Xử lý phản hồi `403 AUTH.EMAIL_NOT_VERIFIED` từ Backend để hiển thị hộp thoại cảnh báo tài khoản chưa xác minh email và cung cấp nút yêu cầu gửi lại email xác minh (`Resend Verification Email`).
  - Tự động đính kèm JWT Access Token, xử lý làm mới phiên (`RefreshToken`) và chuẩn hóa hiển thị lỗi từ `ProblemDetails` thông qua HTTP Interceptors.
  - Duy trì kết nối WebSocket thời gian thực tới `/hubs/notifications` bằng `@microsoft/signalr` để cập nhật trung tâm thông báo và trạng thái công việc tức thời.
  - Hiển thị rõ ràng các đề xuất của AI với nhãn phân biệt và nút xác nhận/chỉnh sửa trước khi gửi lệnh áp dụng về Backend (`Human-in-the-Loop UI`).
- **Non-Responsibilities:**
  - Không được coi kiểm tra quyền ở Frontend (`permissionGuard`) là rào chắn bảo mật duy nhất — mọi kiểm tra phân quyền và cô lập Tenant đều phải được Backend xác thực lại 100%.
- **Inputs:** Thao tác của người dùng trên trình duyệt, dữ liệu JSON từ REST API (`INT-001..014`, `INT-018`) và sự kiện đẩy từ SignalR (`INT-012`).
- **Outputs:** Các lệnh gọi HTTP REST chuẩn hóa tới `BizFlow.Api` và giao diện hiển thị trực quan.
- **Data Ownership:** Trạng thái phiên trình duyệt và trạng thái hiển thị tạm thời (View State).
- **Dependencies:** Phụ thuộc qua mạng (HTTP/WSS) vào `BizFlow.Api` (`INT-001` .. `INT-014`, `INT-018`).
- **Public Interfaces:** Tiêu thụ `INT-001` .. `INT-014`, `INT-018`; cung cấp các màn hình ứng dụng cho người dùng cuối.
- **Security/Tenant Requirements:** Không lưu thông tin nhạy cảm của Tenant khác trong bộ nhớ trình duyệt sau khi đăng xuất; tự động xóa trạng thái phiên và điều hướng về `/login` hoặc `/access-denied` khi nhận mã `401`/`403`.
- **Related SRS Requirements:** `IR-UI-001`, `IR-UI-002`, `IR-UI-003`, `IR-UI-004`, `FR-TEN-005`, `TST-029`, `TST-031`, cùng toàn bộ các yêu cầu chức năng có giao diện người dùng.
- **Testing Approach:** Kiểm thử biên dịch TypeScript/Angular, kiểm thử đơn vị cho Guards/Services và kiểm thử tích hợp luồng giao diện.
- **Status:** `CONFIRMED` | `IMPLEMENTED` (`frontend/src/app/`) & `PROPOSED (Self-service routes /register-company & /verify-email)`.

---

### 2.17. `MOD-BG-001`: Background Job & Timer Worker Host (Tiến trình Xử lý Ngầm và Định kỳ)
- **Module ID:** `MOD-BG-001`
- **Module Name:** Background Job & Timer Worker Host
- **Purpose:** Thực thi các tác vụ bất đồng bộ ngoài luồng HTTP request chính, bao gồm quét đồng hồ SLA định kỳ, xử lý hàng đợi gửi email/thông báo (`Transactional Outbox`), kết xuất báo cáo lớn và dọn dẹp phiên hết hạn.
- **Key Responsibilities:**
  - **(Mục tiêu):** Quét định kỳ các `RequestRecord` và `WorkTask` đang mở để phát hiện sắp quá hạn (`NearBreach`) hoặc đã vi phạm (`Breached`) SLA, ghi nhận `SLAEvent` và phát thông báo leo thang (`Escalation`).
  - **(Mục tiêu):** Đọc các bản ghi chờ xử lý trong `EmailQueueItems` và `NotificationQueueItems` để gửi qua SMTP / SignalR với cơ chế thử lại có kiểm soát (`Exponential Backoff`) và chống gửi trùng (`Idempotency`).
  - **(Mục tiêu):** Xử lý các yêu cầu `ReportExportJob` nặng và lưu tệp kết quả lên `IObjectStorageProvider`.
- **Non-Responsibilities:**
  - Không bỏ qua các quy tắc bất biến của miền nghiệp vụ (khi Worker cần cập nhật trạng thái công việc hoặc ghi nhật ký, vẫn phải gọi qua Application Services / Domain Engines chuẩn).
- **Inputs:** Các bản ghi ở trạng thái `Pending` trong `EmailQueueItems`, `NotificationQueueItems`, `ReportExportJobs` và các bản ghi công việc có mốc hạn SLA.
- **Outputs:** Email được gửi qua SMTP, trạng thái hàng đợi được cập nhật (`Sent`/`Failed`), sự kiện cảnh báo SLA và tệp báo cáo hoàn tất.
- **Data Ownership:** Không sở hữu bảng nghiệp vụ riêng; vận hành trên các bảng hàng đợi của `MOD-008`, `MOD-009`, `MOD-011` và bảng điều phối job (Hangfire schema hoặc tương đương).
- **Dependencies:** `MOD-008`, `MOD-009`, `MOD-010`, `MOD-011`, `MOD-013`, PostgreSQL, Redis, SMTP, S3/MinIO.
- **Public Interfaces:** `INT-020` (`Outbox Consumer`), `INT-021` (`SLA Timer Evaluator`), `INT-026` (`SMTP Dispatcher`).
- **Security/Tenant Requirements:** Mỗi tác vụ ngầm (`Job Payload`) phải mang theo `TenantId` tường minh và thiết lập ngữ cảnh Tenant hợp lệ trong phạm vi thực thi của Job để không vi phạm bộ lọc cô lập dữ liệu.
- **Related SRS Requirements:** `FR-SLA-003`, `FR-SLA-004`, `FR-NOT-002`, `FR-RPT-005`, `NFR-REL-002`.
- **Testing Approach:** Kiểm thử tính lũy đẳng (`Idempotency`) khi một Job chạy lại nhiều lần và kiểm thử cô lập `TenantId` trong ngữ cảnh chạy ngầm.
- **Status:** `PROPOSED` (`ADR-SSS-002`, `OD-020`) | `NOT YET WIRED` (Các bảng hàng đợi đã có trong `BizFlowDbContext.cs`, nhưng `Program.cs` hiện chưa đăng ký Hangfire Server hoặc `IHostedService`).
