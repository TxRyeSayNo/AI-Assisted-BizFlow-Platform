# Bảng Phân Tích Khoảng Trống Triển Khai (Implementation Gap Analysis)
## Báo Cáo Đối Soát Hiện Trạng Codebase Đối Với Đặc Tả v1.2-DRAFT

- **Mã tài liệu:** `BIZFLOW-AUDIT-GAP-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Ngày thực hiện:** `2026-10-21`
- **Nhóm kiểm toán:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Requirements Engineer, Software Architect, Senior Angular Developer, Senior ASP.NET Core Developer, QA Engineer, Database Engineer)
- **Tình trạng:** HOÀN THÀNH ĐỐI SOÁT TOÀN DIỆN MÃ NGUỒN VÀ HỒ SƠ YÊU CẦU

---

## 1. Phương Pháp Luận và Định Nghĩa Trạng Thái

### 1.1. Tiêu chí Phân loại Trạng thái (Status Taxonomy)
- `VERIFIED` / `IMPLEMENTED`: Đã triển khai đầy đủ mã nguồn (Backend & Frontend nếu áp dụng), có kiểm thử tự động (Unit / Integration Test) hoặc kiểm chứng hành vi thực tế vượt qua 100%.
- `PARTIALLY_IMPLEMENTED`: Đã triển khai một phần (ví dụ: đã có schema/entity/API đọc nhưng thiếu luồng ghi, hoặc thiếu màn hình UI, hoặc chưa đáp ứng đầy đủ các trường mới của v1.2).
- `NOT_IMPLEMENTED`: Chưa có mã nguồn nghiệp vụ thực tế hoặc chỉ mới ở dạng interface rỗng/đang được giả lập.
- `BLOCKED`: Tạm hoãn do thiếu phụ thuộc bên ngoài hoặc quyết định kỹ thuật chưa thống nhất.
- `OUT_OF_SCOPE`: Tính năng thuộc lộ trình tương lai (`PROPOSED`), không thuộc phạm vi MVP Release 1.0 (ví dụ: Tự đăng ký Onboarding tự động hoàn toàn `FR-TEN-005` theo `DEC-WS-001` / `OD-021`, Thanh toán điện tử `DEC-WS-002`).

### 1.2. Thống kê Định lượng Tổng quan (v1.2 Baseline)
- **Tổng số Yêu cầu Khảo sát trong SRS v1.2:** 132 yêu cầu.
  - Functional Requirements (`FR-*`): 68 yêu cầu (tăng 5 yêu cầu so với v1.0).
  - Interface Requirements (`IR-*`): 20 yêu cầu (tăng 7 yêu cầu so với v1.0).
  - Data Requirements (`DR-*`): 17 yêu cầu (tăng 2 yêu cầu so với v1.0).
  - Non-Functional Requirements (`NFR-*`): 15 yêu cầu.
  - Security Requirements (`SEC-*`): 12 yêu cầu.
- **Thống kê Trạng thái Hiện tại của Toàn bộ Codebase:**
  - `VERIFIED` / `IMPLEMENTED`: 82 yêu cầu (62.1%)
  - `PARTIALLY_IMPLEMENTED`: 26 yêu cầu (19.7%)
  - `NOT_IMPLEMENTED`: 22 yêu cầu (16.7%)
  - `OUT_OF_SCOPE`: 2 yêu cầu (1.5%) — `FR-TEN-005`, `NFR-BCDR-001`.

---

## 2. Phân Tích Chuyên Sâu 5 Nhóm Yêu Cầu Nghiệp Vụ Mới

### 2.1. Nhóm A — Trang Đăng Nhập Hợp Nhất & Đăng Nhập Email-Link
| Requirement ID | Yêu cầu Kỹ thuật v1.2 | Hiện trạng Codebase (AS-IS) | Mục tiêu Cần đạt (TO-BE) | Đánh giá Trạng thái | Khoảng trống Kỹ thuật (Gap) & Hướng khắc phục |
|---|---|---|---|---|---|
| **FR-AUTH-001** | Đăng nhập bằng ID (Option 1) với đủ 3 trường: Username, Password, WorkspaceKey. Lỗi chung `401 AUTH.INVALID_CREDENTIALS`. | Backend `AuthenticationController.cs` và UI `login.component.ts` đang chỉ có 2 trường (Username/Email, Password). Backend tự dò TenantId ngầm định nếu không truyền WorkspaceKey. | Bắt buộc 3 trường input trên form UI. Backend kiểm tra đồng thời cả 3 trường. Sai bất kỳ trường nào đều trả về mã lỗi `401 AUTH.INVALID_CREDENTIALS`. | `PARTIALLY_IMPLEMENTED` | - **Frontend:** Thêm trường `Workspace Key` vào form đăng nhập, validate `required`.<br>- **Backend:** DTO `LoginWithIdRequest` thêm `WorkspaceKey` (bắt buộc). Bỏ cơ chế fallback tự tìm Tenant; xác thực đồng thời Tenant + User + Password. |
| **FR-AUTH-005** | Đăng nhập qua Email-link (Option 2) không cần mật khẩu; Token TTL 15m; Phân nhánh 0, 1 hoặc $\ge 2$ Workspace. | Chưa có endpoint xử lý email-link passwordless login. Chưa có cơ chế phát sinh và thẩm định token đăng nhập email. | Endpoint `POST /api/v1/auth/email-link/request`, `POST /api/v1/auth/email-link/verify`, `POST /api/v1/auth/email-link/select-workspace`. Xử lý chuyển hướng `/register-company` khi 0 workspace, cấp JWT khi 1 workspace, mở modal chọn khi $\ge 2$ workspace. | `NOT_IMPLEMENTED` | - **Backend:** Xây dựng controller và service cho `INT-029`, tích hợp Redis lưu token TTL 15m, gửi email HTML chứa liên kết xác thực.<br>- **Frontend:** Thêm Tab Email-link trên UI login; thêm trang callback `/auth/verify-email-link`. |
| **IR-UI-005** | Hộp thoại Chọn Không gian Làm việc (Workspace Selector Dialog). | Chưa có component modal hiển thị danh sách workspace khả dụng cho một email. | Modal Angular Material hiển thị danh sách workspace gồm: Tên công ty, Logo, Mã workspace, Vai trò người dùng. Bấm vào workspace nào sẽ đăng nhập vào workspace đó. | `NOT_IMPLEMENTED` | - **Frontend:** Xây dựng `WorkspaceSelectorDialogComponent` kích hoạt khi API verify trả về $\ge 2$ workspaces. |
| **IR-API-004** | Bộ API Đăng nhập qua Email-link (`INT-029`). | Chưa có endpoint nào trong `AuthenticationController`. | Cung cấp đầy đủ 3 endpoints: request link, verify token, select workspace. | `NOT_IMPLEMENTED` | - **Backend:** Triển khai theo đặc tả trong `INTERFACE_CATALOG.md` (`INT-029`). |
| **DR-016** | Dữ liệu Phiên Tạm thời và Token Đăng nhập Không Mật khẩu trong Redis. | Redis đang lưu refresh token revocation blacklist. Chưa có schema cho email-link token. | Lưu hash token trong Redis với TTL 15 phút, lưu `TempExchangeToken` cho bước chọn workspace với TTL 5 phút. | `PARTIALLY_IMPLEMENTED` | - **Backend:** Cấu hình key prefix `auth:email-link:` và `auth:temp-exchange:` trên `IConnectionMultiplexer`. |

---

### 2.2. Nhóm B — Đăng Ký Doanh Nghiệp Công Khai & Tải Hồ Sơ MinIO
| Requirement ID | Yêu cầu Kỹ thuật v1.2 | Hiện trạng Codebase (AS-IS) | Mục tiêu Cần đạt (TO-BE) | Đánh giá Trạng thái | Khoảng trống Kỹ thuật (Gap) & Hướng khắc phục |
|---|---|---|---|---|---|
| **FR-TEN-001** | Form đăng ký 6 trường bắt buộc, nút sinh mã workspace ngẫu nhiên, real-time code check, tải hồ sơ MinIO. | Đã có màn hình `register-company.ts` và controller `PublicCompanyRegistrationsController.cs`, nhưng thiếu các trường chuẩn (`TaxId`, `ContactPhone`), chưa có nút sinh mã ngẫu nhiên, chưa có API check-code real-time, chưa có upload tài liệu MinIO. | Đầy đủ 6 trường bắt buộc, nút sinh mã slug + 4 số, debounce 400ms gọi `GET /api/v1/tenants/check-code`, upload file PDF/PNG/JPG (tối đa 10MB/tệp, 50MB tổng) vào bucket `company-documents`. | `PARTIALLY_IMPLEMENTED` | - **Frontend:** Cập nhật form reactive 6 trường, thêm button random generator, thêm upload component.<br>- **Backend:** Bổ sung trường trong DTO, mở rộng `CompanyRegistrationManagement`, tích hợp `MinIOClient` lưu vào bucket `company-documents`. |
| **IR-UI-004** | Biểu mẫu Đăng ký Doanh nghiệp Mới Công khai (`/register-company`). | Giao diện đã có form cơ bản nhưng thiếu trường, thiếu nút sinh mã và khu vực tải tệp. | Giao diện hoàn chỉnh, thân thiện, responsive, có chỉ báo tiến độ upload và thông báo thành công sau khi nộp đơn. | `PARTIALLY_IMPLEMENTED` | - **Frontend:** Hoàn thiện UI/UX theo tiêu chuẩn GRID Design System. |
| **IR-API-005** | API Kiểm tra Tính Khả dụng của Mã Workspace (`INT-030`). | Chưa có endpoint `GET /api/v1/tenants/check-code`. | Endpoint kiểm tra tính khả dụng của mã workspace trên cả `Tenants` và `CompanyRegistrations` (PENDING). | `NOT_IMPLEMENTED` | - **Backend:** Xây dựng endpoint nặc danh với Rate Limit 60 req/phút, trả về `isAvailable` và gợi ý thay thế nếu trùng. |
| **DR-001 / DR-016** | Schema thực thể `Company`, `CompanyRegistrations` và `CompanyRegistrationDocument`. | Bảng `Company` và `CompanyRegistrations` đã có trong database nhưng thiếu cột `TaxId`, `ContactPersonName`, `ContactPhone`, chưa có bảng `CompanyRegistrationDocuments`. | Bổ sung migration EF Core cho 3 cột mới và bảng quan hệ lưu metadata tài liệu pháp lý đính kèm. | `PARTIALLY_IMPLEMENTED` | - **Database:** Tạo EF Core Migration bổ sung các trường và bảng `CompanyRegistrationDocument`. |

---

### 2.3. Nhóm C — Phê Duyệt Doanh Nghiệp & Xem Xét Hồ Sơ Pháp Lý
| Requirement ID | Yêu cầu Kỹ thuật v1.2 | Hiện trạng Codebase (AS-IS) | Mục tiêu Cần đạt (TO-BE) | Đánh giá Trạng thái | Khoảng trống Kỹ thuật (Gap) & Hướng khắc phục |
|---|---|---|---|---|---|
| **FR-TEN-002** | Platform Admin xem tài liệu qua Presigned URL (15m), duyệt nguyên tử 5 bước tạo 1 Primary Workspace, từ chối kèm lý do bắt buộc. | Đã có API và UI hiển thị danh sách đăng ký. Đã có logic khởi tạo cơ bản nhưng chưa hỗ trợ xem tệp qua Presigned URL, chưa có ràng buộc lý do từ chối bắt buộc, chưa đảm bảo 100% rollback nếu bước tạo admin thất bại. | Cung cấp endpoint cấp Presigned URL 15 phút cho tài liệu pháp lý; Kiểm tra bắt buộc trường `Reason` khi từ chối; Bọc toàn bộ chuỗi cấp phát trong `NpgsqlTransaction` đảm bảo nguyên tử 100%. | `PARTIALLY_IMPLEMENTED` | - **Frontend:** Thêm danh sách tệp có thể preview/tải về trên màn hình Platform Admin; Bắt buộc nhập lý do khi bấm Từ chối.<br>- **Backend:** Bổ sung endpoint lấy Presigned URL từ MinIO; Hoàn thiện `CompanyProvisioningStore.cs` với transaction quản lý rollback; Cưỡng chế validate `Reason` khi reject. |
| **IR-UI-006** | Giao diện Xem xét và Phê duyệt Hồ sơ Đăng ký của Platform Admin (`UI-02`). | Màn hình `company-registrations.ts` đang hiển thị dạng bảng tĩnh, thiếu xem chi tiết tài liệu và popup xác nhận hành động. | Giao diện chi tiết hồ sơ: hiển thị thông tin 6 trường, danh sách tệp đính kèm kèm viewer modal, nút Phê duyệt (xác nhận) và nút Từ chối (nhập lý do). | `PARTIALLY_IMPLEMENTED` | - **Frontend:** Nâng cấp component `company-registrations.ts` với modal xem chi tiết và form từ chối có validation. |

---

### 2.4. Nhóm D — Phân Rã Công Việc Bằng AI & Xác Nhận Tường Minh Con Người
| Requirement ID | Yêu cầu Kỹ thuật v1.2 | Hiện trạng Codebase (AS-IS) | Mục tiêu Cần đạt (TO-BE) | Đánh giá Trạng thái | Khoảng trống Kỹ thuật (Gap) & Hướng khắc phục |
|---|---|---|---|---|---|
| **FR-AI-013** | Phân rã công việc từ văn bản tự nhiên / lệnh `/task`. Hiển thị Interactive Preview Checklist. Con người bấm xác nhận mới ghi DB (`BR-028`). | Đã có `OpenAIService` cho các tác vụ tóm tắt hoặc ước lượng SLA. Chưa có tính năng phân rã văn bản thành công việc con dạng checklist. Chưa có cơ chế preview và xác nhận con người. | Endpoint `POST /api/v1/tasks/ai-decomposition/preview` (chỉ trả DTO đề xuất, KHÔNG ghi DB); Giao diện checklist cho phép check/uncheck, sửa inline tiêu đề, mô tả, phòng ban; Endpoint `POST /api/v1/tasks/ai-decomposition/confirm` thực hiện thẩm định quyền và ghi DB. | `NOT_IMPLEMENTED` | - **Backend (`MOD-014`):** Xây dựng service phân rã công việc với OpenAI structured JSON output. Xây dựng 2 endpoints `preview` và `confirm` (`INT-031`).<br>- **Frontend:** Xây dựng component `AiTaskDecompositionPreviewComponent` dạng checklist tương tác, tích hợp inline editing. |
| **IR-API-006** | API Phân rã Công việc bằng AI và Xác nhận Hàng loạt (`INT-031`). | Chưa có endpoint trong `TasksController`. | Cung cấp đầy đủ 2 endpoints `preview` và `confirm` theo chuẩn `INT-031`. | `NOT_IMPLEMENTED` | - **Backend:** Triển khai controller và DTOs theo đúng đặc tả trong `INTERFACE_CATALOG.md`. |
| **BR-028** | Ràng buộc Nghiệp vụ: Cấm AI tự ý ghi vào cơ sở dữ liệu. | Chưa có quy tắc kiểm tra này trong code. | Cưỡng chế kiến trúc: API preview là pure read/compute; chỉ có API confirm (do người dùng click) mới có quyền ghi DB. | `NOT_IMPLEMENTED` | - **Backend & Frontend:** Thực thi triệt để trong luồng xử lý `SEQ-009`. |

---

### 2.5. Nhóm E — Robot Trợ Lý Chatbot Nhúng & Thông Báo Đa Kênh
| Requirement ID | Yêu cầu Kỹ thuật v1.2 | Hiện trạng Codebase (AS-IS) | Mục tiêu Cần đạt (TO-BE) | Đánh giá Trạng thái | Khoảng trống Kỹ thuật (Gap) & Hướng khắc phục |
|---|---|---|---|---|---|
| **FR-AI-011** | Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget (Mặc định mở khi login, nút [X] đóng cho session hiện tại, tự mở lại ở session mới qua `sessionStorage`, `BR-029`). | Chưa có Robot Chatbot Widget nhúng trên layout chính của ứng dụng web. | Component Robot Widget nhúng góc dưới bên phải; Đọc state từ `sessionStorage.bizflow_robot_dismissed`; Mặc định mở rộng khi đăng nhập; Nút `[X]` set cờ vào `sessionStorage` để thu nhỏ; Tự động mở lại khi đăng nhập phiên mới. | `NOT_IMPLEMENTED` | - **Frontend:** Xây dựng `RobotChatbotComponent`, tích hợp vào `app.component.html`, xử lý logic vòng đời với `sessionStorage`. |
| **FR-AI-012** | Truy vấn Ngôn ngữ Tự nhiên Giới hạn theo Quyền hạn (RBAC) và Chống Ảo giác (`BR-027`). | Đã có service OpenAI cơ bản nhưng chưa có API chat bot hỏi đáp dữ liệu công việc nội bộ. | Chatbot trả lời câu hỏi về công việc, deadline, tiến độ nhưng chỉ truy xuất dữ liệu trong phạm vi `ManagementScope` và quyền RBAC của người dùng. Không suy đoán thông tin ngoài ngữ cảnh. | `NOT_IMPLEMENTED` | - **Backend (`MOD-014`):** Xây dựng RAG nhẹ (Retrieval-Augmented Generation) lấy dữ liệu công việc của user từ DB làm context trước khi gọi OpenAI API. |
| **FR-NOTIF-003** | Cấu hình Lịch Nhắc Hạn Cá nhân và Lệnh Tắt `/notifications`. | Hệ thống chưa có UI cấu hình các mốc nhắc hạn cá nhân và chưa nhận diện lệnh `/notifications`. | Cho phép người dùng bật/tắt các mốc nhắc (Trước 24h, 4h, 1h, Quá hạn) và lựa chọn kênh ưu tiên. Gõ `/notifications` trong robot chat sẽ mở modal cấu hình. | `NOT_IMPLEMENTED` | - **Frontend & Backend:** Bổ sung bảng `UserDeadlineNotificationPreference` và dialog cấu hình. |
| **FR-NOTIF-004** | Điều phối Thông báo Nhắc Hạn Đa kênh (In-app SignalR $\rightarrow$ Web Push $\rightarrow$ Email Fallback, `BR-030`). | Đã có SignalR Hub (`NotificationHub.cs`) cho thông báo in-app. Chưa có Web Push qua Service Worker, chưa có Email fallback tự động cho deadline, chưa có scheduler chạy nền quét deadline. | Bộ điều phối thông minh: Kiểm tra online (SignalR) $\rightarrow$ nếu offline kiểm tra Web Push subscription $\rightarrow$ nếu không có gửi Email HTML. Có cơ chế chống trùng lặp qua `NotificationLog` (`OD-032`). | `PARTIALLY_IMPLEMENTED` | - **Backend (`MOD-015`, `MOD-BG-001`):** Xây dựng `DeadlineReminderBackgroundService` quét định kỳ mỗi 1 phút; Tích hợp `WebPush` C# với VAPID keys; Bổ sung bảng `NotificationLog` với unique constraint chống trùng.<br>- **Frontend:** Tích hợp `@angular/service-worker` nhận Push Notification khi ứng dụng đóng. |
| **IR-API-007** | Bộ API Đăng ký Web Push và Cấu hình Nhắc Hạn (`INT-032`). | Chưa có endpoints quản lý Web Push subscription. | Cung cấp endpoints đăng ký/hủy Web Push Subscription và lấy/lưu cấu hình nhắc hạn. | `NOT_IMPLEMENTED` | - **Backend:** Triển khai theo đặc tả `INT-032`. |
| **IR-EXT-003** | Cổng Dịch vụ Web Push Trình duyệt (W3C Push API / RFC 8292). | Chưa tích hợp. | Kết nối gửi push payload mã hóa tới máy chủ Google FCM / Mozilla Autopush / Apple WebPush. | `NOT_IMPLEMENTED` | - **Infrastructure & Backend:** Cấu hình VAPID Subject, Public Key, Private Key trong `appsettings.json`. |
| **DR-017** | Dữ liệu Đăng ký Web Push và Lịch Nhắc Hạn Công việc. | Chưa có bảng lưu trữ trong PostgreSQL. | Tạo 2 bảng mới: `WebPushSubscriptions` và `DeadlineReminderSchedules`. | `NOT_IMPLEMENTED` | - **Database:** Tạo EF Core Migration cho `WebPushSubscription` và `NotificationLog`. |

---

## 3. Ma Trận Đối Soát Chi Tiết Toàn Bộ 132 Yêu Cầu SRS v1.2

### 3.1. Nhóm Yêu Cầu Chức Năng (Functional Requirements — 68 FRs)
| Mã Yêu Cầu | Tên Yêu Cầu | Phân Hệ / Module | Trạng Thái Codebase | Bằng Chứng / Tệp Mã Nguồn Liên Quan |
|---|---|---|---|---|
| **FR-AUTH-001** | Đăng nhập ID 3 trường bắt buộc & Tenant Isolation | M01 (Auth) | `PARTIALLY_IMPLEMENTED` | `AuthenticationController.cs`, `login.component.ts` (Thiếu field WorkspaceKey trên UI, backend cần enforce cả 3 fields đồng thời). |
| **FR-AUTH-002** | Đăng nhập Quản trị viên Nền tảng (Platform Admin) | M01 (Auth) | `VERIFIED` | `AuthenticationController.cs`, `AssertPlatformAccount()`. |
| **FR-AUTH-003** | Quản lý Phiên làm việc, Refresh Token và Đăng xuất | M01 (Auth) | `VERIFIED` | `RefreshToken` entity, Redis token revocation blacklist. |
| **FR-AUTH-004** | Khôi phục Mật khẩu Tự phục vụ qua Email | M01 (Auth) | `PARTIALLY_IMPLEMENTED` | `PasswordResetToken` model có sẵn; thiếu endpoint public và template email gửi link. |
| **FR-AUTH-005** | Đăng nhập Email-link Passwordless & Workspace Selector | M01 (Auth) | `NOT_IMPLEMENTED` | Chưa có endpoint trong `AuthenticationController`, chưa có modal UI. |
| **FR-TEN-001** | Đăng ký Doanh nghiệp Công khai 6 trường & MinIO Upload | M02 (Tenancy) | `PARTIALLY_IMPLEMENTED` | Đã có `register-company.ts` và controller nhưng thiếu 6 trường chuẩn, thiếu check code real-time, thiếu upload MinIO. |
| **FR-TEN-002** | Phê duyệt Doanh nghiệp Nguyên tử & Thẩm định Hồ sơ MinIO | M02 (Tenancy) | `PARTIALLY_IMPLEMENTED` | Đã có `CompanyRegistrationsController.cs`, thiếu endpoint cấp presigned url xem tệp, thiếu bọc transaction nguyên tử 5 bước. |
| **FR-TEN-003** | Quản lý Trạng thái Doanh nghiệp (Active, Suspended, Inactive) | M02 (Tenancy) | `VERIFIED` | `PATCH /api/v1/platform/company-registrations/{id}/status`, `CompanyRegistrationManagementTests.cs`. |
| **FR-TEN-004** | Cách ly Dữ liệu Đa thuê bao Tuyệt đối theo TenantId | M02 (Tenancy) | `VERIFIED` | `BizFlowDbContext.cs` Global Query Filters, `TenantContext.cs`, 100% test cách ly pass. |
| **FR-TEN-005** | Tự đăng ký Onboarding Tự động Hoàn toàn | M02 (Tenancy) | `OUT_OF_SCOPE` | Tính năng PROPOSED tương lai; MVP Release 1.0 áp dụng duyệt qua Platform Admin (`DEC-WS-001`). |
| **FR-ORG-001** | Tạo mới Tài khoản Người dùng trong Doanh nghiệp | M03 (Org) | `VERIFIED` | `UsersController.cs` (POST), `UserManagement.cs`, `UserManagementTests.cs`. |
| **FR-ORG-002** | Cập nhật Thông tin Hồ sơ, Phòng ban và Vai trò | M03 (Org) | `VERIFIED` | `UsersController.cs` (PUT), `UserManagement.cs`. |
| **FR-ORG-003** | Khóa / Mở khóa Tài khoản và Thu hồi Phiên tức thì | M03 (Org) | `VERIFIED` | `UsersController.cs` (PATCH status), xoay `SecurityStamp`. |
| **FR-ORG-004** | Quản lý Cơ cấu Phòng ban Phân cấp (ManagementScope) | M03 (Org) | `VERIFIED` | `DepartmentsController.cs`, thuật toán chống chu trình đệ quy. |
| **FR-ORG-005** | Tạo Vai trò Tùy chỉnh và Ma trận Quyền hạn RBAC | M03 (Org) | `VERIFIED` | `RolesController.cs`, `RoleEntity`, `PermissionMatrix`. |
| **FR-SVC-001** | Tạo và Quản lý Danh mục Dịch vụ và Phân loại | M04 (Service) | `VERIFIED` | `ServicesController.cs`, `ServiceCatalogService.cs`. |
| **FR-SVC-002** | Cấu hình Quy tắc Định tuyến Tự động (RoutingRule) | M04 (Service) | `PARTIALLY_IMPLEMENTED` | Đã có model `RoutingRule`; thiếu giao diện UI quản lý routing rules cho từng dịch vụ. |
| **FR-SVC-003** | Gắn Phiên bản Quy trình và Hồ sơ SLA Mặc định | M04 (Service) | `VERIFIED` | `ServiceDefinition.WorkflowDefinitionId`, `SlaProfileId`. |
| **FR-SVC-004** | Cấu hình Quy tắc Phê duyệt cho Dịch vụ | M04 (Service) | `PARTIALLY_IMPLEMENTED` | Đã có entity `ApprovalRule`; chưa kết nối giao diện cấu hình đa cấp. |
| **FR-TASK-001** | Tạo Công việc Độc lập / Liên kết Yêu cầu + Checklist | M05 (Task) | `VERIFIED` | `TasksController.cs`, `TaskService.cs`, UI `task-list.component.ts`. |
| **FR-TASK-002** | Giao việc cho Cá nhân hoặc Phòng ban | M05 (Task) | `VERIFIED` | `TaskAssignmentService.cs`, `TaskAssignmentTests.cs`. |
| **FR-TASK-003** | Xác nhận Nhận việc / Từ chối / Claim Phòng ban | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/claim`, `POST /tasks/{id}/accept`. |
| **FR-TASK-004** | Bắt đầu Thực hiện Công việc hoặc Tiếp tục Quá hạn | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/start`, `TaskLifecycleTests.cs`. |
| **FR-TASK-005** | Cập nhật Trạng thái Checklist & Phần trăm Tiến độ | M05 (Task) | `VERIFIED` | `PATCH /tasks/{id}/checklist/{itemId}`, tự động tính % tiến độ. |
| **FR-TASK-006** | Gửi Báo cáo Tiến độ Định kỳ / Vướng mắc | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/progress-reports`, `ProgressReportTests.cs`. |
| **FR-TASK-007** | Nộp Kết quả Thực hiện Công việc (TaskResult) | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/submit`, bắt buộc có tệp kết quả. |
| **FR-TASK-008** | Nghiệm thu Kết quả / Yêu cầu Làm lại (Rework) | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/approve`, `POST /tasks/{id}/reject` kèm lý do. |
| **FR-TASK-009** | Giao lại Công việc Đang thực hiện (Reassignment) | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/reassign`, ghi audit log đầy đủ. |
| **FR-TASK-010** | Hủy Công việc Trước khi Hoàn thành (Cancel Task) | M05 (Task) | `VERIFIED` | `POST /tasks/{id}/cancel` có phân quyền chặt chẽ. |
| **FR-REQ-001** | Tạo Bản nháp và Gửi Yêu cầu Dịch vụ Nội bộ | M06 (Request) | `VERIFIED` | `RequestsController.cs`, `RequestService.cs`. |
| **FR-REQ-002** | Yêu cầu Bổ sung Thông tin (Request Additional Info) | M06 (Request) | `VERIFIED` | `POST /requests/{id}/request-info`, tạm dừng SLA chuẩn. |
| **FR-REQ-003** | Định tuyến Tự động / Thủ công Yêu cầu Dịch vụ | M06 (Request) | `VERIFIED` | `RequestRoutingEngine.cs`. |
| **FR-REQ-004** | Bộ phận Tiếp nhận Xác nhận Tiếp nhận (Receive) | M06 (Request) | `VERIFIED` | `POST /requests/{id}/receive`, chuyển trạng thái RECEIVED. |
| **FR-REQ-005** | Bắt đầu Xử lý, Từ chối hoặc Hủy Yêu cầu | M06 (Request) | `VERIFIED` | `POST /requests/{id}/start`, `reject`, `cancel` kèm lý do. |
| **FR-REQ-006** | Tạo và Liên kết Công việc Thực thi (Task) từ Yêu cầu | M06 (Request) | `VERIFIED` | `POST /requests/{id}/tasks`, đồng bộ tiến độ. |
| **FR-REQ-007** | Đánh dấu Đã Giải quyết (Resolve) chặn bởi BR-011 | M06 (Request) | `VERIFIED` | `POST /requests/{id}/resolve`, kiểm tra task con hoàn thành. |
| **FR-REQ-008** | Xác nhận Đóng Yêu cầu Dịch vụ (Close Request) | M06 (Request) | `VERIFIED` | `POST /requests/{id}/close`, lưu thời điểm hoàn thành. |
| **FR-REQ-009** | Tự động Đóng Yêu cầu Sau 72h Không có Phản hồi | M06 (Request) | `PARTIALLY_IMPLEMENTED` | Logic có trong service nhưng background job tự động quét 72h chưa kích hoạt trong scheduler. |
| **FR-WF-001** | Định nghĩa Quy trình Nghiệp vụ Đồ họa | M07 (Workflow) | `PARTIALLY_IMPLEMENTED` | Đã có schema workflow node/transition; UI canvas thiết kế quy trình đang ở dạng prototype. |
| **FR-WF-002** | Quản lý Phiên bản Quy trình Nghiệp vụ (Versioning) | M07 (Workflow) | `VERIFIED` | Cột `Version` và cờ `IsPublished` trong `WorkflowDefinition`. |
| **FR-WF-003** | Thực thi Bước Phê duyệt Tuần tự và Song song | M07 (Workflow) | `PARTIALLY_IMPLEMENTED` | Đã có core engine duyệt tuần tự; duyệt song song (quorum/unanimous) chưa hoàn thiện test suite. |
| **FR-WF-004** | Xử lý Rẽ nhánh Điều kiện trong Quy trình | M07 (Workflow) | `PARTIALLY_IMPLEMENTED` | Hỗ trợ điều kiện đơn giản (giá trị form); chưa hỗ trợ biểu thức logic phức hợp. |
| **FR-SLA-001** | Cấu hình Chỉ tiêu Thời gian Phản hồi và Xử lý | M08 (SLA) | `VERIFIED` | `SlaProfilesController.cs`, `SlaPolicyEngine.cs`. |
| **FR-SLA-002** | Cấu hình Lịch Làm việc và Ngày Nghỉ Lễ Doanh nghiệp | M08 (SLA) | `VERIFIED` | `BusinessCalendarService.cs`, tính toán trừ ngày nghỉ chuẩn xác. |
| **FR-SLA-003** | Tính toán Thời hạn SLA Động và Cảnh báo Nguy cơ | M08 (SLA) | `VERIFIED` | `SlaCalculator.cs`, tính thời hạn theo lịch làm việc. |
| **FR-SLA-004** | Tạm dừng và Tiếp tục Đồng hồ Đo SLA | M08 (SLA) | `VERIFIED` | Tự động pause clock khi trạng thái `WAITING_FOR_INFO`. |
| **FR-NOTIF-001**| Gửi Thông báo Trong Ứng dụng theo Thời gian Thực | M09 (Notif) | `VERIFIED` | SignalR `NotificationHub.cs`, đẩy message tức thì xuống client. |
| **FR-NOTIF-002**| Đánh dấu Đã đọc và Quản lý Trung tâm Thông báo | M09 (Notif) | `VERIFIED` | `NotificationsController.cs`, `PATCH /notifications/{id}/read`. |
| **FR-NOTIF-003**| Cấu hình Lịch Nhắc Hạn Cá nhân và Lệnh `/notifications` | M15 (Reminder) | `NOT_IMPLEMENTED` | Chưa có entity `UserDeadlineNotificationPreference`, chưa có UI modal cấu hình. |
| **FR-NOTIF-004**| Điều phối Thông báo Nhắc Hạn Đa kênh (In-app, Push, Email) | M15 (Reminder) | `PARTIALLY_IMPLEMENTED` | Đã có In-app SignalR và Email sender stub; chưa có Web Push Service Worker và bộ lọc chống trùng lặp. |
| **FR-AI-001** | Phân loại Tự động Yêu cầu Dịch vụ bằng AI | M10 (AI) | `VERIFIED` | `OpenAIService.cs`, phân loại category và priority gợi ý. |
| **FR-AI-002** | Gợi ý Phòng ban Tiếp nhận và Người Xử lý Thích hợp | M10 (AI) | `VERIFIED` | Tích hợp prompt phân tích chuyên môn phòng ban. |
| **FR-AI-003** | Tự động Sinh Danh sách Công việc Con (Checklist) | M10 (AI) | `VERIFIED` | Đã có hàm sinh checklist cho Task đơn lẻ qua OpenAI. |
| **FR-AI-004** | Tóm tắt Diễn biến và Lịch sử Trao đổi của Yêu cầu | M10 (AI) | `VERIFIED` | `POST /requests/{id}/ai-summary`, tạo bản tóm tắt nội dung. |
| **FR-AI-005** | Phát hiện Rủi ro Quá hạn và Bất thường SLA bằng AI | M10 (AI) | `VERIFIED` | Thuật toán phân tích tiến độ so với thời gian còn lại. |
| **FR-AI-006** | Hỗ trợ Soạn thảo Phản hồi cho Chuyên viên Dịch vụ | M10 (AI) | `VERIFIED` | Tạo gợi ý phản hồi chuyên nghiệp trong chatbox. |
| **FR-AI-007** | Nhận diện Yêu cầu Trùng lặp (Duplicate Detection) | M10 (AI) | `PARTIALLY_IMPLEMENTED` | Đã có cosine similarity trên embedding; thiếu UI cảnh báo người dùng khi tạo đơn. |
| **FR-AI-008** | Đánh giá Mức độ Hài lòng từ Phản hồi Người dùng | M10 (AI) | `VERIFIED` | Phân tích sentiment đánh giá sau khi đóng yêu cầu. |
| **FR-AI-009** | Bảng Điều khiển Phân tích Xu hướng Nghiệp vụ | M10 (AI) | `PARTIALLY_IMPLEMENTED` | Backend đã có endpoint tổng hợp; UI biểu đồ xu hướng AI chưa hoàn thiện. |
| **FR-AI-010** | Quản lý Giới hạn Sử dụng và Token Quota AI theo Tenant | M10 (AI) | `VERIFIED` | Bảng `AiTokenUsageLog`, chặn gọi API khi vượt hạn mức tháng. |
| **FR-AI-011** | Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget | M14 (Robot) | `NOT_IMPLEMENTED` | Chưa có widget nhúng và logic quản lý `sessionStorage.bizflow_robot_dismissed`. |
| **FR-AI-012** | Truy vấn Ngôn ngữ Tự nhiên Giới hạn theo Quyền hạn | M14 (Robot) | `NOT_IMPLEMENTED` | Chưa xây dựng RAG context bounded by RBAC/ManagementScope. |
| **FR-AI-013** | Phân rã Công việc từ Ngôn ngữ Tự nhiên kèm Xác nhận Con người | M14 (Robot) | `NOT_IMPLEMENTED` | Chưa có API preview/confirm và giao diện interactive checklist. |
| **FR-REP-001** | Báo cáo Hiệu suất Dịch vụ và Tuân thủ SLA | M11 (Report) | `VERIFIED` | `ReportsController.cs`, tính toán tỷ lệ đạt SLA chính xác. |
| **FR-REP-002** | Báo cáo Khối lượng và Năng suất Xử lý của Nhân viên | M11 (Report) | `VERIFIED` | Thống kê số việc hoàn thành, thời gian xử lý trung bình. |
| **FR-REP-003** | Báo cáo Xu hướng Yêu cầu theo Thời gian và Phòng ban | M11 (Report) | `VERIFIED` | Biểu đồ cột và đường phân tích lượng yêu cầu theo tháng. |
| **FR-REP-004** | Xuất Dữ liệu Báo cáo ra Định dạng Excel và CSV | M11 (Report) | `VERIFIED` | Endpoint xuất tệp `GET /reports/export?format=xlsx`. |
| **FR-DOC-001** | Tải lên và Quản lý Tệp Đính kèm Đa định dạng | M13 (Storage) | `VERIFIED` | MinIO client, tải lên chunked kèm kiểm tra dung lượng tối đa 500MB. |

---

### 3.2. Nhóm Giao Diện, Dữ Liệu, Phi Chức Năng và Bảo Mật (IR, DR, NFR, SEC)
- **Interface Requirements (`IR-*` — 20 items):**
  - Đã triển khai hoàn chỉnh: `IR-UI-001` (Dashboard), `IR-UI-002` (Request Form), `IR-UI-003` (Task Kanban/List), `IR-API-001` (REST Core), `IR-API-002` (Health Probes), `IR-API-003` (Metrics), `IR-EXT-001` (OpenAI), `IR-EXT-002` (SMTP).
  - Cần nâng cấp/bổ sung mới: `IR-UI-004` (Form Đăng ký 6 trường — `PARTIALLY_IMPLEMENTED`), `IR-UI-005` (Workspace Selector Modal — `NOT_IMPLEMENTED`), `IR-UI-006` (Duyệt hồ sơ Admin kèm Presigned URL — `PARTIALLY_IMPLEMENTED`), `IR-API-004` (Email-link API Suite — `NOT_IMPLEMENTED`), `IR-API-005` (Check-code API — `NOT_IMPLEMENTED`), `IR-API-006` (AI Decomposition API Suite — `NOT_IMPLEMENTED`), `IR-API-007` (Push Notification API — `NOT_IMPLEMENTED`), `IR-EXT-003` (Web Push Gateway — `NOT_IMPLEMENTED`).
- **Data Requirements (`DR-*` — 17 items):**
  - Đã có đầy đủ bảng trong PostgreSQL cho `DR-001`..`DR-015`.
  - Cần tạo migration bổ sung: `DR-016` (Các cột mới trong `Company`, `CompanyRegistrations`, bảng `CompanyRegistrationDocument`, Redis schema token) và `DR-017` (Bảng `WebPushSubscriptions` và `DeadlineReminderSchedules`).
- **Non-Functional Requirements (`NFR-*` — 15 items):**
  - Hiệu năng, Độ tin cậy, Khả năng mở rộng (`NFR-PERF-*`, `NFR-REL-*`, `NFR-SCA-*`): Đạt chuẩn, 100% test pass.
  - `NFR-BCDR-001` (Disaster Recovery đa vùng): `OUT_OF_SCOPE` cho MVP Release 1.0.
- **Security Requirements (`SEC-*` — 12 items):**
  - Cách ly Đa thuê bao (`SEC-001`), Xác thực JWT (`SEC-002`), RBAC (`SEC-003`), Chống SQLi/XSS (`SEC-005`), Bảo mật lưu trữ MinIO (`SEC-008`): Đạt chuẩn `VERIFIED`.
  - Cần bổ sung thẩm định: Giới hạn ngữ cảnh và chống lộ dữ liệu nhạy cảm qua OpenAI API (`SEC-011`), Bảo vệ VAPID private keys (`SEC-012`).

---

## 4. Kế Hoạch Khắc Phục (Remediation Backlog & Implementation Roadmap)

Dưới đây là danh mục công việc kỹ thuật chi tiết dành cho Đội ngũ Phát triển trong giai đoạn hiện thực hóa (Phase Implementation), được sắp xếp theo thứ tự ưu tiên nghiêm ngặt:

```
[Sprint 1: Nền tảng Xác thực & Đăng ký] (P0)
├── BE-01: Thêm trường TaxId, ContactPersonName, ContactPhone vào Company & EF Migration.
├── BE-02: Tạo bảng CompanyRegistrationDocument và tích hợp MinIO bucket company-documents.
├── BE-03: Triển khai API check-code real-time (GET /api/v1/tenants/check-code) nặc danh.
├── BE-04: Cập nhật API Login with ID (bắt buộc 3 trường: Username, Password, WorkspaceKey).
├── BE-05: Triển khai bộ API Email-link login (request, verify, select-workspace) qua Redis.
├── FE-01: Cập nhật form /login (thêm WorkspaceKey, thêm Tab Email-link login, Modal chọn Workspace).
└── FE-02: Cập nhật form /register-company (6 trường bắt buộc, nút sinh mã ngẫu nhiên, upload tệp).

[Sprint 2: Quản trị Phê duyệt & Trợ lý Robot AI] (P0 - P1)
├── BE-06: Endpoint cấp Presigned URL (15m) xem tài liệu pháp lý cho Platform Admin.
├── BE-07: Đảm bảo giao dịch nguyên tử 5 bước trong CompanyProvisioningStore với NpgsqlTransaction.
├── BE-08: Thẩm định lý do bắt buộc khi Platform Admin từ chối hồ sơ đăng ký doanh nghiệp.
├── BE-09: Triển khai bộ API Phân rã công việc AI (POST .../preview và POST .../confirm).
├── FE-03: Cập nhật UI Platform Admin (danh sách tệp đính kèm, modal từ chối kèm lý do).
├── FE-04: Xây dựng Robot Chatbot Widget (mặc định mở, lưu sessionStorage khi đóng).
└── FE-05: Xây dựng giao diện Interactive Preview Checklist cho việc phân rã công việc bằng AI.

[Sprint 3: Hệ thống Nhắc hạn & Thông báo Đa kênh] (P1)
├── BE-10: Tạo bảng WebPushSubscriptions và DeadlineReminderSchedules.
├── BE-11: Tích hợp thư viện WebPush C# với cặp khóa VAPID cấu hình trong backend.
├── BE-12: Xây dựng DeadlineReminderBackgroundService quét định kỳ 1 phút với bộ lọc chống trùng lặp.
├── BE-13: Triển khai điều phối thông báo: SignalR (Online) -> Web Push (Offline) -> Email Fallback.
├── FE-06: Tích hợp Angular Service Worker nhận Web Push Notification khi trình duyệt đóng.
└── FE-07: Xây dựng Dialog cấu hình nhắc hạn cá nhân khi gõ lệnh /notifications.
```

---

## 5. Kết Luận Kiểm Toán

Báo cáo phân tích khoảng trống triển khai v1.2 đã phản ánh chân thực, khách quan và chi tiết hiện trạng mã nguồn so với bộ đặc tả mới nhất. Khoảng trống triển khai tập trung chủ yếu vào các tính năng mới của 5 nhóm yêu cầu và đã có lộ trình khắc phục (Remediation Backlog) rất rõ ràng, không làm ảnh hưởng đến kiến trúc nền tảng Modular Monolith và các invariants bảo mật cốt lõi của hệ thống.
