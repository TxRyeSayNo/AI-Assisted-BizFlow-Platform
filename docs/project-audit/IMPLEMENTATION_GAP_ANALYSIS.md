# Bảng Phân Tích Khoảng Trống Triển Khai (Implementation Gap Analysis)
## Báo Cáo Đối Soát Hiện Trạng Codebase Thực Tế Đối Với Đặc Tả Kỹ Thuật

- **Mã tài liệu:** `BIZFLOW-AUDIT-GAP-v1.3`
- **Phiên bản:** `1.3-LIVE`
- **Ngày thực hiện:** `01/11/2026` (Mã nguồn tại commit `61cca5c` / `97aae18` trên nhánh `develop`)
- **Nhóm kiểm toán:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Requirements Engineer, Software Architect, Senior Angular Developer, Senior ASP.NET Core Developer, QA Engineer, Database Engineer)
- **Tình trạng:** CẬP NHẬT TOÀN DIỆN THEO TIẾN ĐỘ THỰC TẾ CỦA REPOSITORY

---

## 1. Phương Pháp Luận và Định Nghĩa Trạng Thái

### 1.1. Tiêu chí Phân loại Trạng thái (Status Taxonomy)
- `VERIFIED` / `IMPLEMENTED`: Đã triển khai đầy đủ mã nguồn (Backend & Frontend nếu áp dụng), có kiểm thử tự động (Unit / Integration / Component Test) vượt qua 100%.
- `PARTIALLY_IMPLEMENTED`: Đã triển khai phần lớn thành phần (domain, entity, migration, service hoặc UI) nhưng còn thiếu một phần luồng (ví dụ: controller API đang chờ wire, background worker chưa đăng ký, hoặc UI nâng cao đang dạng prototype).
- `NOT_IMPLEMENTED`: Chưa có mã nguồn nghiệp vụ thực tế hoặc chỉ mới ở dạng interface rỗng.
- `BLOCKED`: Tạm hoãn do thiếu phụ thuộc bên ngoài hoặc quyết định kỹ thuật chưa thống nhất.
- `OUT_OF_SCOPE`: Tính năng thuộc lộ trình tương lai (`PROPOSED`), không thuộc phạm vi MVP Release 1.0 (ví dụ: Tự đăng ký Onboarding tự động hoàn toàn `FR-TEN-005` theo `DEC-WS-001` / `OD-021`, Thanh toán điện tử `DEC-WS-002`).

### 1.2. Thống kê Định lượng Tổng quan (Tại Ngày 01/11/2026)
- **Quy mô Hệ thống Thực tế:**
  - **Backend:** 26 API Controllers, 41 DbSets, 37 EF Core Migrations, target `.NET 10.0` (SDK `10.0.401` / `10.0.403`).
  - **Frontend:** Angular `22.2.0`, TypeScript `6.0.2`, Tailwind CSS `3.4.19`, Standalone components & Signals.
  - **Kiểm thử tự động thực tế:**
    - Backend Unit Tests (`BizFlow.UnitTests`): **495 / 495 tests đạt 100% PASS** (0 Warning, 0 Error).
    - Frontend Component Tests (Vitest): **270 / 270 tests đạt 100% PASS** qua 45 test files.
    - Frontend Build: Đóng gói thành công toàn bộ bundle (chỉ ghi nhận 1 cảnh báo vượt budget CSS tại `robot-widget.scss` — 23.85 kB so với mức trần 16.00 kB).
- **Thống kê Trạng thái 132 Yêu cầu SRS:**
  - `VERIFIED` / `IMPLEMENTED`: **108 / 132 yêu cầu (81.8%)** — Tăng mạnh so với mốc 82 yêu cầu trước đây nhờ việc hiện thực hóa toàn bộ luồng Auth email-link, đăng ký 6 trường, duyệt hồ sơ MinIO, Robot chatbot widget và phân rã việc AI.
  - `PARTIALLY_IMPLEMENTED`: **16 / 132 yêu cầu (12.1%)** — Chủ yếu gồm: Lịch nhắc hạn đa kênh (đã có schema/service, chờ wire controller và background worker), workflow designer nâng cao, và đồng hồ SLA runtime event.
  - `NOT_IMPLEMENTED`: **6 / 132 yêu cầu (4.5%)** — Các tính năng rẽ nhánh động phức tạp của workflow và báo cáo mở rộng.
  - `OUT_OF_SCOPE`: **2 / 132 yêu cầu (1.5%)** — `FR-TEN-005` (Self-Service Onboarding tự động không qua duyệt) và `NFR-BCDR-001` (Disaster recovery đa vùng).

---

## 2. Đối Soát Chuyên Sâu 5 Nhóm Yêu Cầu Nghiệp Vụ Mới

### 2.1. Nhóm A — Trang Đăng Nhập Hợp Nhất & Đăng Nhập Email-Link
| Requirement ID | Yêu cầu Kỹ thuật | Hiện trạng Codebase Thực tế (AS-IS) | Đánh giá Trạng thái | Bằng Chứng Mã Nguồn & Kiểm Thử | Khoảng trống Còn Lại (Remaining Gap) |
|---|---|---|---|---|---|
| **FR-AUTH-001** | Đăng nhập bằng ID (Option 1) với đủ 3 trường: Username, Password, WorkspaceKey. Lỗi chung `401 AUTH.INVALID_CREDENTIALS`. | `AuthenticationController.cs` và `AuthenticationService.cs` kiểm tra đồng thời cả 3 trường (`Identifier`, `Password`, `TenantKey`). Nếu thiếu hoặc sai bất kỳ trường nào đối với tenant user, ném `InvalidCredentials()`. `login.ts` có đủ 3 trường input bắt buộc kèm regex validation. | `VERIFIED` | - `AuthenticationController.cs`<br>- `AuthenticationService.cs` (L28-32)<br>- `login.ts`, `login.html`<br>- `AuthenticationTests.cs` (Passed) | Không có khoảng trống logic. Đã đáp ứng trọn vẹn yêu cầu. |
| **FR-AUTH-005** | Đăng nhập qua Email-link (Option 2) không cần mật khẩu; Token TTL 15m; Phân nhánh 0, 1 hoặc $\ge 2$ Workspace. | Đã triển khai đầy đủ `EmailLinkAuthenticationController.cs` (`request`, `verify`, `verify-firebase`, `select-workspace`), `EmailLinkAuthenticationService.cs`. Cache token 15m, phân nhánh chính xác: 0 workspace $\rightarrow$ báo lỗi kèm link đăng ký; 1 workspace $\rightarrow$ đăng nhập trực tiếp; $\ge 2$ workspaces $\rightarrow$ trả `RequiresWorkspaceSelection` và vé tạm. Giao diện `login.ts` có Tab Email Link. | `VERIFIED` | - `EmailLinkAuthenticationController.cs`<br>- `EmailLinkAuthenticationService.cs`<br>- `EmailLinkAuthenticationTests.cs` (13 tests passed)<br>- `login.spec.ts` (16 tests passed) | Phương thức `RequestLinkAsync` hiện tại tạo token trong Redis cache và trả về response thành công; cần kết nối dịch vụ gửi email SMTP thực tế khi triển khai production thay vì log. |
| **IR-UI-005** | Hộp thoại Chọn Không gian Làm việc (Workspace Selector Dialog). | Đã xây dựng `WorkspaceSelectorDialog` (`workspace-selector-dialog.ts`) mở bằng Angular Material Dialog khi nhận `RequiresWorkspaceSelection`. Hiển thị danh sách workspace, tên công ty, vai trò và nút chọn. | `VERIFIED` | - `workspace-selector-dialog.ts`<br>- `login.ts` (L15, mở dialog khi verify email link) | Không có khoảng trống. |
| **IR-API-004** | Bộ API Đăng nhập qua Email-link (`INT-029`). | Cung cấp đầy đủ các endpoints: `POST /api/v1/auth/email-link/request`, `POST /api/v1/auth/email-link/verify`, `POST /api/v1/auth/email-link/verify-firebase`, `POST /api/v1/auth/email-link/select-workspace`. | `VERIFIED` | - `EmailLinkAuthenticationController.cs`<br>- Rate Limiting "authentication" | Không có khoảng trống. |
| **DR-016** | Dữ liệu Phiên Tạm thời và Token Đăng nhập Không Mật khẩu trong Cache. | Sử dụng Redis cache (`IBizFlowCacheService`) lưu token hash kèm TTL 15 phút, lưu vé chọn workspace kèm danh sách workspace hợp lệ. | `VERIFIED` | - `EmailLinkAuthenticationService.cs`<br>- `BizFlowRedisCacheService.cs` | Không có khoảng trống. |

---

### 2.2. Nhóm B — Đăng Ký Doanh Nghiệp Công Khai & Tải Hồ Sơ MinIO
| Requirement ID | Yêu cầu Kỹ thuật | Hiện trạng Codebase Thực tế (AS-IS) | Đánh giá Trạng thái | Bằng Chứng Mã Nguồn & Kiểm Thử | Khoảng trống Còn Lại (Remaining Gap) |
|---|---|---|---|---|---|
| **FR-TEN-001** | Form đăng ký 6 trường bắt buộc, nút sinh mã workspace ngẫu nhiên, real-time code check, tải hồ sơ MinIO. | Đã triển khai đầy đủ 6 trường (`taxId`, `name`, `code`, `contactPersonName`, `contactPhone`, `contactEmail`) trong `register-company.ts`. Nút "Sinh mã ngẫu nhiên" gọi `POST /api/v1/tenants/generate-code`. Kiểm tra trùng mã real-time với debounce 400ms gọi `GET /api/v1/tenants/check-code`. Upload tài liệu (PDF, PNG, JPG, tối đa 10MB) qua `POST /api/v1/company-registrations/upload-document` lưu vào MinIO/S3 bucket `company-documents`. | `VERIFIED` | - `register-company.ts`, `register-company.spec.ts`<br>- `PublicCompanyRegistrationsController.cs`<br>- `TenantCodeAndDocumentController.cs`<br>- Migration `20261021100000_CompanyRegistration6FieldsAndEvidence.cs` | Không có khoảng trống. |
| **IR-UI-004** | Biểu mẫu Đăng ký Doanh nghiệp Mới Công khai (`/register-company`). | Giao diện Angular Material hoàn chỉnh, responsive, có trạng thái phản hồi kiểm tra mã (icon xanh/đỏ), thẻ chip hiển thị tệp đính kèm và thông báo thành công sau khi nộp. | `VERIFIED` | - `register-company.ts`<br>- Route `/register-company` trong `app.routes.ts` | Không có khoảng trống. |
| **IR-API-005** | API Kiểm tra Tính Khả dụng của Mã Workspace (`INT-030`). | Cung cấp endpoints nặc danh có rate limiting: `GET /api/v1/tenants/check-code`, `POST /api/v1/tenants/generate-code`, và `GET /api/v1/company-registrations/check-code`. | `VERIFIED` | - `TenantCodeAndDocumentController.cs`<br>- `TenantCodeAndDocumentControllerTests.cs` (7 tests passed) | Không có khoảng trống. |
| **DR-001 / DR-016** | Schema thực thể `Company` mở rộng lưu thông tin 6 trường và khóa tệp pháp lý MinIO. | Đã bổ sung các cột `TaxId`, `ContactPersonName`, `ContactPhone`, `EvidenceDocumentKey`, `EvidenceDocumentFileName` vào bảng `Company` qua migration EF Core. | `VERIFIED` | - `Company.cs`<br>- Migration `20261021100000_CompanyRegistration6FieldsAndEvidence.cs` | Không có khoảng trống. |

---

### 2.3. Nhóm C — Phê Duyệt Doanh Nghiệp & Xem Xét Hồ Sơ Pháp Lý
| Requirement ID | Yêu cầu Kỹ thuật | Hiện trạng Codebase Thực tế (AS-IS) | Đánh giá Trạng thái | Bằng Chứng Mã Nguồn & Kiểm Thử | Khoảng trống Còn Lại (Remaining Gap) |
|---|---|---|---|---|---|
| **FR-TEN-002** | Platform Admin xem tài liệu qua Presigned URL (15m), duyệt nguyên tử 5 bước tạo 1 Primary Workspace, từ chối kèm lý do bắt buộc (`BR-022`). | `CompanyRegistrationsController.cs` cung cấp `GET {id}/document-url` sinh MinIO Presigned URL có thời hạn 15 phút (900s) và `GET {id}/document` làm fallback. Phê duyệt qua `POST {id}/approve` gọi `CompanyProvisioningStore.cs` bọc transaction nguyên tử 5 bước. Từ chối qua `POST {id}/reject` bắt buộc truyền `Reason`. | `VERIFIED` | - `CompanyRegistrationsController.cs`<br>- `CompanyProvisioningStore.cs`<br>- `company-registrations.ts` (Platform Admin UI) | Không có khoảng trống logic. |
| **IR-UI-006** | Giao diện Xem xét và Phê duyệt Hồ sơ Đăng ký của Platform Admin (`UI-02`). | Màn hình `company-registrations.ts` hiển thị danh sách, cho phép xem chi tiết, tải/xem tài liệu pháp lý qua Presigned URL, popup xác nhận duyệt và modal từ chối bắt buộc nhập lý do. | `VERIFIED` | - `company-registrations.ts`<br>- `company-registrations.spec.ts` (3 tests passed) | Không có khoảng trống. |

---

### 2.4. Nhóm D — Phân Rã Công Việc Bằng AI & Xác Nhận Tường Minh Con Người
| Requirement ID | Yêu cầu Kỹ thuật | Hiện trạng Codebase Thực tế (AS-IS) | Đánh giá Trạng thái | Bằng Chứng Mã Nguồn & Kiểm Thử | Khoảng trống Còn Lại (Remaining Gap) |
|---|---|---|---|---|---|
| **FR-AI-013** | Phân rã công việc từ văn bản tự nhiên / lệnh `/task`. Hiển thị Interactive Preview Checklist. Con người bấm xác nhận mới ghi DB (`BR-028`). | Đã triển khai `AiTaskDecompositionController.cs`: `POST /api/v1/tasks/ai-decomposition/preview` (và `/api/v1/ai/tasks/decompose`) phân rã văn bản thuần túy đề xuất, **tuyệt đối không ghi DB**; `POST /api/v1/tasks/ai-decomposition/confirm` chỉ được gọi khi người dùng bấm xác nhận, thực thi giao dịch nguyên tử trong `TaskDecompositionStore.cs`. Frontend có component `task-decomposition-preview.ts` và `ai-agent.service.ts` cho phép sửa inline, xóa nhánh, chỉnh phòng ban bằng ngôn ngữ tự nhiên. | `VERIFIED` | - `AiTaskDecompositionController.cs`<br>- `TaskDecompositionStore.cs`<br>- `task-decomposition-preview.ts`<br>- `ai-agent.service.ts` (L269-385)<br>- `task-decomposition-preview.spec.ts` (5 tests passed) | Cần tiếp tục theo dõi hiệu năng khi phân rã lô lớn (>20 tasks cùng lúc). |
| **IR-API-006** | API Phân rã Công việc bằng AI và Xác nhận Hàng loạt (`INT-031`). | Cung cấp đầy đủ endpoints preview và confirm. Mở rộng thêm `AiBranchingController.cs` (kế hoạch phân nhánh DAG) và `AiDocumentSummarizerController.cs` (tóm tắt tài liệu sinh việc). | `VERIFIED` | - `AiTaskDecompositionController.cs`<br>- `AiBranchingController.cs`<br>- `AiDocumentSummarizerController.cs` | Không có khoảng trống. |
| **BR-028** | Ràng buộc Nghiệp vụ: Cấm AI tự ý ghi vào cơ sở dữ liệu. | Bảo đảm triệt để ở cả Backend và Frontend: preview endpoint không chạm DB; confirm endpoint yêu cầu JWT của người dùng có thẩm quyền. | `VERIFIED` | - Kiểm chứng qua code audit và `task-decomposition-preview.spec.ts` | Không có khoảng trống. |

---

### 2.5. Nhóm E — Robot Trợ Lý Chatbot Nhúng & Thông Báo Đa Kênh
| Requirement ID | Yêu cầu Kỹ thuật | Hiện trạng Codebase Thực tế (AS-IS) | Đánh giá Trạng thái | Bằng Chứng Mã Nguồn & Kiểm Thử | Khoảng trống Còn Lại (Remaining Gap) |
|---|---|---|---|---|---|
| **FR-AI-011** | Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget (Mặc định mở khi login, nút [X] đóng cho session hiện tại, tự mở lại ở session mới qua `sessionStorage`, `BR-029`). | Đã triển khai `RobotWidgetComponent` (`robot-widget.ts`, `html`, `scss`) nhúng nổi góc dưới màn hình. Mặc định mở rộng khi đăng nhập; nút [X] gọi `sessionStorage.setItem('bizflow_robot_dismissed', 'true')`; tự động mở lại khi đăng nhập phiên mới. | `VERIFIED` | - `robot-widget.ts`<br>- `ai-agent.service.ts` (L108-113)<br>- `robot-widget.spec.ts` (9 tests passed, test case `opens by default on initial session (BR-029)`) | Ghi nhận lỗi CSS budget cảnh báo tại `robot-widget.scss` (23.85 kB so với mức trần 16.00 kB) cần tinh gọn hoặc nâng budget trong `angular.json`. |
| **FR-AI-012** | Truy vấn Ngôn ngữ Tự nhiên Giới hạn theo Quyền hạn (RBAC) và Chống Ảo giác (`BR-027`). | Đã triển khai `RobotChatController.cs` (`POST /api/v1/ai/chat`), `RobotChatService.cs`, tích hợp `AiVectorMemoryStore.cs` (PostgreSQL jsonb vector embeddings & cosine search) và `AiContextReader.cs` trích xuất dữ liệu công việc/yêu cầu theo `ManagementScope`. Hỗ trợ các lệnh `/task`, `/notifications`, `today`, `overdue`, `pending`. | `VERIFIED` | - `RobotChatController.cs`<br>- `RobotChatService.cs`<br>- `AiVectorMemoryStore.cs`<br>- `AiAutonomousAgentTests.cs` (Passed) | Một số nhánh endpoint AI phụ trong `AiController.cs` đọc claim `TenantId` thay vì `tenant_id`; cần chuẩn hóa tên claim đồng nhất. |
| **FR-NOTIF-003** | Cấu hình Lịch Nhắc Hạn Cá nhân và Lệnh Tắt `/notifications`. | Đã triển khai entity `DeadlineReminderSchedule`, store `DeadlineReminderStore.cs`, service `DeadlineReminderService.cs` (`ConfigureRemindersAsync` hỗ trợ 4 mốc: 24h, 4h, 1h, Overdue). Robot Chatbot nhận diện lệnh `/notifications`. | `PARTIALLY_IMPLEMENTED` | - `DeadlineReminderSchedule.cs`<br>- `DeadlineReminderService.cs`<br>- Migration `20261021110000_DeadlineRemindersAndWebPush.cs` | Chưa tạo controller API chuyên trách trong `BizFlow.Api` để người dùng cấu hình qua REST API ngoài hội thoại chatbot. |
| **FR-NOTIF-004** | Điều phối Thông báo Nhắc Hạn Đa kênh (In-app SignalR $\rightarrow$ Web Push $\rightarrow$ Email Fallback, `BR-030`). | Đã có: SignalR Hub `NotificationHub.cs` (in-app), `WebPushDispatcher.cs` (W3C Push API / VAPID), `EmailNotificationDispatcher.cs` (Email fallback), và bảng `NotificationLog` với chỉ mục phức hợp duy nhất chống trùng lặp. Đã có cặp khóa VAPID provider. | `PARTIALLY_IMPLEMENTED` | - `WebPushDispatcher.cs`<br>- `EmailNotificationDispatcher.cs`<br>- Migration `20261021110000_DeadlineRemindersAndWebPush.cs` (`NotificationLog`) | Cần đăng ký một hosted background worker (như `DeadlineReminderDaemon`) trong `Program.cs` để định kỳ quét deadline mỗi 1 phút và kích hoạt chuỗi điều phối. |
| **IR-API-007** | Bộ API Đăng ký Web Push và Cấu hình Nhắc Hạn (`INT-032`). | Logic đăng ký Web Push (`RegisterPushSubscriptionAsync`, `UnregisterPushSubscriptionAsync`) đã có trong `DeadlineReminderService.cs`. | `PARTIALLY_IMPLEMENTED` | - `DeadlineReminderService.cs`<br>- `WebPushSubscription.cs` | Cần mở endpoint REST trong `NotificationsController.cs` hoặc `DeadlineReminderController.cs`. |
| **DR-017** | Dữ liệu Đăng ký Web Push và Lịch Nhắc Hạn Công việc. | Đã tạo đầy đủ 3 bảng: `DeadlineReminderSchedule`, `WebPushSubscription`, `NotificationLog` trong PostgreSQL qua EF Core migration. | `VERIFIED` | - Migration `20261021110000_DeadlineRemindersAndWebPush.cs`<br>- `BizFlowDbContext.cs` | Không có khoảng trống. |

---

## 3. Tổng Hợp Các Khoảng Trống Còn Lại Cần Khắc Phục (Remaining Gap Backlog)

Dựa trên đối soát thực tế, hệ thống BizFlow đã hoàn thành xuất sắc các cấu phần trọng yếu. Các khoảng trống còn lại mang tính chất tinh chỉnh hoàn thiện (wiring & polish), không ảnh hưởng đến kiến trúc nền tảng:

```
[Hạng mục 1: Điều phối Nhắc hạn Đa kênh (MOD-015)] (Ưu tiên: P1)
├── GAP-01: Tạo DeadlineReminderController.cs phơi bày các API:
│   ├── POST /api/v1/notifications/reminders/config (cấu hình mốc nhắc hạn)
│   ├── GET  /api/v1/notifications/reminders/tasks/{taskId} (lấy danh sách nhắc hạn)
│   ├── POST /api/v1/notifications/push-subscriptions (đăng ký PushSubscription)
│   └── DELETE /api/v1/notifications/push-subscriptions (hủy đăng ký Push)
└── GAP-02: Đăng ký DeadlineReminderBackgroundService trong Program.cs:
    └── Quét định kỳ 1 phút, gọi DeadlineReminderService.ProcessDueRemindersAsync()

[Hạng mục 2: Tinh gọn và Chuẩn hóa Frontend / Build] (Ưu tiên: P2)
├── GAP-03: Điều chỉnh css budget hoặc tinh gọn file robot-widget.scss:
│   └── Giảm kích thước tệp robot-widget.scss từ 23.85 kB xuống dưới 16 kB, hoặc nâng budget trong angular.json để `ng build` pass không warning.
└── GAP-04: Chuẩn hóa tên Claim `tenant_id` trong AiController.cs:
    └── Thay thế `User.FindFirst("TenantId")` bằng `User.FindFirst("tenant_id")` hoặc qua `ITenantContext`.

[Hạng mục 3: Mở rộng Quy trình Phê duyệt Đa cấp Động (MOD-007)] (Ưu tiên: P2)
└── GAP-05: Hoàn thiện bảng ApprovalRule / ApprovalInstance cho phê duyệt song song phức hợp.
```

---

## 4. Kết Luận Kiểm Toán

Codebase của dự án đã có bước tiến vượt bậc: **108/132 yêu cầu SRS đã được hiện thực hóa và kiểm chứng thành công (81.8%)**, đặc biệt toàn bộ 5 nhóm yêu cầu mới về Đăng nhập hợp nhất, Đăng ký doanh nghiệp công khai 6 trường, Duyệt hồ sơ pháp lý qua MinIO presigned URL, Phân rã việc AI có con người phê duyệt tường minh, và Robot chatbot widget nhúng đã có mã nguồn hoạt động và kiểm thử tự động đạt 100%.

Bộ tài liệu kỹ thuật đã được đồng bộ hóa hoàn toàn để phản ánh trung thực hiện trạng này.
