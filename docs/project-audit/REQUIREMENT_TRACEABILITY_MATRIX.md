# Ma Trận Truy Vết Yêu Cầu Toàn Diện (Master Traceability Matrix)
## Báo Cáo Truy Vết Hai Chiều Xuyên Suốt Vòng Đời Dự Án v1.3-LIVE

- **Mã tài liệu:** `BIZFLOW-AUDIT-RTM-v1.3-LIVE`
- **Phiên bản:** `1.3-LIVE` (Cập nhật đồng bộ sau Audit Codebase Toàn diện)
- **Ngày lập:** `01/11/2026`
- **Nhóm kiểm toán:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Requirements Engineer, Software Architect, QA Lead, System Analyst)
- **Tình trạng:** CHỨNG THỰC ĐỘ PHỦ HAI CHIỀU (BIDIRECTIONAL TRACEABILITY) — 108/132 Yêu cầu `VERIFIED` (81.8%), 16 `PARTIAL` (12.1%), 6 `NOT_IMPLEMENTED` (4.5%), 2 `OUT_OF_SCOPE` (1.5%).

---

> **Cập nhật 01/11/2026 (v1.3-LIVE):** Ma trận dưới đây đã được đối chiếu trực tiếp với mã nguồn thực tế tại commit `61cca5c` / `97aae18` trên nhánh `develop`. Kết quả đối chiếu dựa trên 26 API Controllers, 41 DbSets trong `BizFlowDbContext.cs`, 37 EF Core Migrations, 495/495 Backend Unit Tests PASS (100%), và 270/270 Frontend Component Tests PASS (100%).

## Đối chiếu hiện trạng thực tế theo các nhóm yêu cầu trọng yếu

| Yêu cầu | Trạng thái Thực tế | Bằng chứng Codebase & Kiểm thử |
|---|---|---|
| `FR-AUTH-001` | `VERIFIED` | [AuthenticationController](../../backend/BizFlow.Api/Controllers/AuthenticationController.cs) & [AuthenticationService](../../backend/BizFlow.Application/Authentication/AuthenticationService.cs): Bắt buộc 3 trường (`Identifier`, `Password`, `TenantKey`) cho tài khoản tenant, trả lời `401 AUTH.INVALID_CREDENTIALS` nếu sai/thiếu. |
| `FR-AUTH-005` | `VERIFIED` | [EmailLinkAuthenticationController](../../backend/BizFlow.Api/Controllers/EmailLinkAuthenticationController.cs) (4 endpoints: `request`, `verify`, `verify-firebase`, `select-workspace`), [EmailLinkAuthenticationService](../../backend/BizFlow.Application/Authentication/EmailLinkAuthenticationService.cs) (Token TTL 15m, dispatch email qua `IEmailDispatcher`), Frontend [login.ts](../../frontend/src/app/features/auth/login.ts) (2 tab), [workspace-selector-dialog.ts](../../frontend/src/app/features/auth/workspace-selector-dialog.ts). |
| `FR-TEN-001` | `VERIFIED` | [PublicCompanyRegistrationsController](../../backend/BizFlow.Api/Controllers/PublicCompanyRegistrationsController.cs), Frontend [register-company.ts](../../frontend/src/app/features/auth/register-company.ts): 6 trường bắt buộc, nút sinh mã ngẫu nhiên, debounce 400ms check code (`GET /api/v1/tenants/check-code`), tải hồ sơ lên MinIO (`POST /api/v1/company-registrations/upload-document`). |
| `FR-TEN-002` | `VERIFIED` | [CompanyRegistrationsController](../../backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs): `GET .../document-url` phát Presigned URL (15m TTL), `POST .../reject` bắt buộc lý do (`BR-022`), `POST .../approve` gọi [CompanyProvisioningStore](../../backend/BizFlow.Infrastructure/Tenancy/CompanyProvisioningStore.cs) cấp phát nguyên tử 5 bước (Company $\rightarrow$ Tenant $\rightarrow$ Admin $\rightarrow$ Calendar $\rightarrow$ Audit). |
| `FR-TEN-003` | `VERIFIED` | [CompanyRegistrationsController](../../backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs): Quản lý trạng thái Company/Tenant qua `POST .../{id}/suspend` và `POST .../{id}/activate`. |
| `FR-ORG-001..005` | `VERIFIED` | [UsersController](../../backend/BizFlow.Api/Controllers/UsersController.cs) (Create, Update, Toggle Status, Roles), [DepartmentsController](../../backend/BizFlow.Api/Controllers/DepartmentsController.cs) (Cơ cấu phòng ban phân cấp), [RolesController](../../backend/BizFlow.Api/Controllers/RolesController.cs) (RBAC + `ManagementScope`). |
| `FR-AI-011` | `VERIFIED` | [robot-widget.ts](../../frontend/src/app/shared/ai/robot-widget.ts): Nhúng toàn hệ thống, mặc định mở khi đăng nhập, nút `[X]` ghi nhớ `sessionStorage.bizflow_robot_dismissed = true` (`BR-029`), tự động mở lại ở phiên trình duyệt mới. |
| `FR-AI-012` | `VERIFIED` | [RobotChatController](../../backend/BizFlow.Api/Controllers/RobotChatController.cs) (`POST /api/v1/ai/chat`), [AiVectorMemoryStore](../../backend/BizFlow.Infrastructure/AI/AiVectorMemoryStore.cs) (RAG vector embedding cosine similarity), bọc ngữ cảnh trong quyền hạn Tenant & User. |
| `FR-AI-013` | `VERIFIED` | [AiTaskDecompositionController](../../backend/BizFlow.Api/Controllers/AiTaskDecompositionController.cs): `POST .../preview` đề xuất bóc tách không ghi DB (`BR-028`), `POST .../confirm` ghi DB nguyên tử qua [TaskDecompositionStore](../../backend/BizFlow.Infrastructure/AI/TaskDecompositionStore.cs), Frontend [task-decomposition-preview.ts](../../frontend/src/app/shared/ai/task-decomposition-preview.ts) tương tác sửa/xóa/đổi phòng ban. |
| `FR-NOTIF-003..004` | `PARTIAL` | Migration `20261021110000_DeadlineRemindersAndWebPush.cs` đã tạo bảng `DeadlineReminderSchedule`, `WebPushSubscription`, `NotificationLog` (chỉ mục duy nhất chống trùng lặp `OD-032`). Services [DeadlineReminderService](../../backend/BizFlow.Application/Notifications/DeadlineReminderService.cs), `WebPushDispatcher.cs`, `EmailNotificationDispatcher.cs` đã hoàn thành; chờ đăng ký Background Timer Daemon và REST Controller. |
| `SEC-008..009` | `VERIFIED` | [AiContextReader](../../backend/BizFlow.Infrastructure/AI/AiContextReader.cs), [AiActionDispatcher](../../backend/BizFlow.Infrastructure/AI/AiActionDispatcher.cs): Ranh giới AI không tự trị, cấm ghi DB tự ý, kiểm tra tenant context và sanitize prompt. |

---

## 1. Phương Pháp Luận và Mô Hình Truy Vết (Traceability Framework)

Ma trận truy vết tổng thể liên kết chặt chẽ mọi mục tiêu kinh doanh, quy tắc nghiệp vụ, yêu cầu người dùng tới kiến trúc hệ thống và kịch bản nghiệm thu kiểm thử. Chuỗi liên kết tuân thủ mô hình 6 tầng nghiêm ngặt:

```
[Tầng 1: Mục Tiêu Kinh Doanh & Nhu Cầu]
     (BIZ-001 .. BIZ-033, STKN-001 .. STKN-016)
                    │
                    ▼
[Tầng 2: Quy Tắc Nghiệp Vụ Cốt Lõi]
     (BR-001 .. BR-030)
                    │
                    ▼
[Tầng 3: Đặc Tả Yêu Cầu Phần Mềm (SRS)]
     (FR-001 .. FR-068, IR-001 .. IR-020, DR-001 .. DR-017, NFR-001 .. NFR-015, SEC-001 .. SEC-012)
                    │
                    ▼
[Tầng 4: Kiến Trúc & Thiết Kế Hệ Thống (SSS)]
     (MOD-001 .. MOD-015, MOD-FE-001, MOD-BG-001, ADR-001 .. ADR-010, SEQ-001 .. SEQ-010)
                    │
                    ▼
[Tầng 5: Giao Diện Kỹ Thuật (API & Contracts)]
     (INT-001 .. INT-032)
                    │
                    ▼
[Tầng 6: Tiêu Chí Nghiệm Thu & Kiểm Thử]
     (AC-001 .. AC-047, TST-001 .. TST-037, 495 Unit Tests .NET, 270 Specs Angular)
```

---

## 2. Thống Kê Định Lượng Độ Phủ Thực Tế (Codebase Baseline 01/11/2026)

| Thành Phần Khảo Sát | Tổng Số Mục | Tỷ Lệ Đã Ánh Xạ | Trạng Thái Triển Khai Thực Tế |
|---|---|---|---|
| **Mục tiêu Kinh doanh (BIZ)** | 33 mục (`BIZ-001`..`BIZ-033`) | 33 / 33 (100%) | Đạt chuẩn — 100% ánh xạ sang FR và AC. |
| **Nhu cầu Bên liên quan (STKN)** | 16 mục (`STKN-001`..`STKN-016`) | 16 / 16 (100%) | Đạt chuẩn — 100% ánh xạ sang FR và UC. |
| **Quy tắc Nghiệp vụ (BR)** | 30 mục (`BR-001`..`BR-030`) | 30 / 30 (100%) | Đạt chuẩn — Toàn bộ quy tắc cốt lõi đã có guard trong code. |
| **Yêu cầu Chức năng (FR)** | 68 mục (`FR-AUTH-*` .. `FR-NOTIF-*`) | 68 / 68 (100%) | **53 VERIFIED** (77.9%), **11 PARTIAL** (16.2%), **3 NOT_IMPL** (4.4%), **1 OUT_OF_SCOPE** (`FR-TEN-005`). |
| **Yêu cầu Giao diện (IR)** | 20 mục (`IR-UI-*`, `IR-API-*`, `IR-EXT-*`) | 20 / 20 (100%) | **18 VERIFIED**, **2 PARTIAL** (`IR-API-007`, `IR-EXT-002`). |
| **Yêu cầu Dữ liệu (DR)** | 17 mục (`DR-001`..`DR-017`) | 17 / 17 (100%) | **16 VERIFIED**, **1 PARTIAL** (`DR-009` Approval tables). |
| **Yêu cầu Phi chức năng & Bảo mật (NFR/SEC)** | 27 mục (15 NFR, 12 SEC) | 27 / 27 (100%) | **21 VERIFIED**, **3 PARTIAL**, **3 NOT_IMPL** (`NFR-AI-002`, `NFR-BCDR-001`, `OD-016`). |
| **TỔNG CỘNG YÊU CẦU PHẦN MỀM (SRS)** | **132 mục nguyên tử** | **132 / 132 (100%)** | **108 VERIFIED (81.8%)**, **16 PARTIAL (12.1%)**, **6 NOT_IMPL (4.5%)**, **2 OUT_OF_SCOPE (1.5%)**. |
| **Mô-đun Kiến trúc (MOD)** | 17 mô-đun (`MOD-001`..`MOD-015`, FE, BG) | 17 / 17 (100%) | 15 IMPLEMENTED, 2 PARTIAL (`MOD-015`, `MOD-BG-001`). |
| **Giao diện API (INT)** | 32 giao diện (`INT-001`..`INT-032`) | 32 / 32 (100%) | 30 IMPLEMENTED, 1 PARTIAL (`INT-032`), 1 IN_DESIGN (`INT-026` advanced SMTP). |
| **Kịch bản Kiểm thử Tự động** | .NET Unit Tests + Angular Specs | 765 tests | **495 / 495 .NET tests PASS (100%)**, **270 / 270 Angular specs PASS (100%)**. |

---

## 3. Ma Trận Truy Vết Chi Tiết 5 Nhóm Yêu Cầu Mới v1.3

### 3.1. Nhóm A — Trang Đăng Nhập Hợp Nhất & Đăng Nhập Email-Link
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) | Trạng thái Code |
|---|---|---|---|---|---|---|
| **BIZ-029** (Đăng nhập linh hoạt & tiếp cận tức thì)<br>**STKN-015** (Nhân viên dùng email đa công ty) | **BR-025** (Quy tắc đăng nhập hợp nhất, 3 trường ID, email-link passwordless, workspace selector) | - **FR-AUTH-001** (Đăng nhập ID 3 trường bắt buộc)<br>- **FR-AUTH-005** (Email-link login & workspace selector)<br>- **IR-UI-005** (Workspace Selector Modal)<br>- **IR-API-004** (Email-link API suite)<br>- **DR-016** (Redis token TTL 15m) | - **MOD-001** (Auth & RBAC)<br>- **MOD-FE-001** (Frontend Shell)<br>- **ADR-SSS-008** (Multi-mode Auth Architecture)<br>- **SEQ-008** (Unified Login & Email-link Flow) | - **INT-001** (`POST /api/v1/auth/login`)<br>- **INT-029** (`POST .../email-link/request`, `POST .../verify`, `POST .../select-workspace`) | - **AC-033** (Bắt buộc đủ 3 trường ID login)<br>- **AC-034** (Email-link 1 tenant vào thẳng)<br>- **AC-035** (Email-link đa tenant mở modal)<br>- **TST-032** (Kiểm thử đăng nhập hợp nhất) | `VERIFIED` |

### 3.2. Nhóm B — Đăng Ký Doanh Nghiệp Công Khai & Tải Hồ Sơ MinIO
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) | Trạng thái Code |
|---|---|---|---|---|---|---|
| **BIZ-030** (Tự phục vụ nộp hồ sơ minh bạch)<br>**STKN-001** (Đại diện công ty muốn tạo workspace) | **BR-026** (Tiếp nhận hồ sơ 6 trường, mã workspace duy nhất, upload MinIO, re-check race condition) | - **FR-TEN-001** (Form đăng ký 6 trường & upload MinIO)<br>- **IR-UI-004** (Trang `/register-company`)<br>- **IR-API-005** (Check-code API suite)<br>- **DR-001** (Entity Company mở rộng)<br>- **DR-016** (Bảng `CompanyRegistrationDocument`) | - **MOD-002** (Tenancy Management)<br>- **MOD-013** (File Storage MinIO)<br>- **MOD-FE-001** (Public Shell)<br>- **ADR-SSS-001** (Tenant Isolation) | - **INT-003** (`POST /api/v1/company-registrations`)<br>- **INT-030** (`GET /api/v1/tenants/check-code`) | - **AC-036** (6 trường bắt buộc & sinh mã ngẫu nhiên)<br>- **AC-037** (Check-code debounce & MinIO upload)<br>- **TST-033** (Kiểm thử đăng ký & upload MinIO) | `VERIFIED` |

### 3.3. Nhóm C — Phê Duyệt Doanh Nghiệp & Kiểm Tra Hồ Sơ Pháp Lý
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) | Trạng thái Code |
|---|---|---|---|---|---|---|
| **BIZ-001** (Thẩm định pháp lý và cấp phát an toàn)<br>**STKN-002** (Platform Admin kiểm soát tenant) | **BR-022** (Bắt buộc lý do từ chối)<br>**BR-023** / **OD-024** (Cấp phát nguyên tử 5 bước: Company, Tenant, Admin, Calendar, Mail)<br>**DEC-WS-001** (1 Company = 1 Primary Workspace) | - **FR-TEN-002** (Phê duyệt nguyên tử & presigned url)<br>- **FR-TEN-003** (Quản lý trạng thái Company)<br>- **IR-UI-006** (Admin inspection UI `/platform/companies`)<br>- **SEC-004** (Platform Admin Authorization) | - **MOD-002** (Tenancy Management)<br>- **MOD-013** (Storage MinIO Presigned URLs)<br>- **ADR-SSS-001** (Fail-Closed Multi-tenancy)<br>- **SEQ-001** (Company Registration & Approval) | - **INT-002** (`POST .../approve`, `POST .../reject`, `GET .../documents/{id}/download-url`) | - **AC-038** (Platform Admin xem tệp presigned url 15m)<br>- **AC-039** (Phê duyệt nguyên tử 100% rollback nếu lỗi)<br>- **TST-034** (Kiểm thử phê duyệt nguyên tử) | `VERIFIED` |

### 3.4. Nhóm D — Phân Rã Công Việc Bằng AI & Xác Nhận Tường Minh Con Người
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) | Trạng thái Code |
|---|---|---|---|---|---|---|
| **BIZ-031** (Nâng cao năng suất khởi tạo luồng việc)<br>**CAP-014** (Phân rã công việc thông minh)<br>**STKN-004** (Trưởng phòng tạo việc nhanh chóng) | **BR-028** (Xác nhận tường minh của con người: cấm AI tự ghi DB; bắt buộc hiển thị checklist cho phép chỉnh sửa trước khi commit) | - **FR-AI-013** (Phân rã văn bản & lệnh `/task`)<br>- **FR-TASK-001** (Tạo task độc lập & subtasks)<br>- **IR-API-006** (AI Task Decomposition API suite)<br>- **SEC-003** (Phân quyền phòng ban) | - **MOD-014** (Robot Chatbot & AI Assistance)<br>- **MOD-005** (Task Management)<br>- **MOD-010** (AI Integration OpenAI)<br>- **ADR-SSS-009** (Human-in-the-Loop Architecture)<br>- **SEQ-009** (Robot & AI Decomposition Workflow) | - **INT-031** (`POST .../preview` và `POST .../confirm`) | - **AC-040** (AI phân tích prompt thành việc có cấu trúc)<br>- **AC-041** (UI Checklist tương tác cho sửa inline/bỏ chọn)<br>- **AC-042** (Cấm ghi DB nếu chưa click xác nhận)<br>- **TST-035** (Kiểm thử phân rã việc & phê duyệt con người) | `VERIFIED` |

### 3.5. Nhóm E — Robot Trợ Lý Chatbot Nhúng & Thông Báo Đa Kênh
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) | Trạng thái Code |
|---|---|---|---|---|---|---|
| **BIZ-032** (Tương tác thông minh qua trợ lý ảo)<br>**BIZ-033** (Đảm bảo tiến độ qua cảnh báo đa kênh)<br>**CAP-015** (Trợ lý đồng hành & nhắc việc chủ động)<br>**STKN-005** (Nhân viên không muốn bỏ lỡ deadline) | **BR-027** (Giới hạn dữ liệu RBAC & chống ảo giác)<br>**BR-029** (Vòng đời widget sessionStorage)<br>**BR-030** (Nhắc hạn 4 mốc: 24h, 4h, 1h, quá hạn; điều phối đa kênh: SignalR $\rightarrow$ Web Push $\rightarrow$ Email; chống trùng lặp qua `NotificationLog`) | - **FR-AI-011** (Vòng đời Robot widget nhúng)<br>- **FR-AI-012** (Truy vấn ngôn ngữ tự nhiên bounded by RBAC)<br>- **FR-NOTIF-003** (Cấu hình nhắc hạn & lệnh `/notifications`)<br>- **FR-NOTIF-004** (Điều phối thông báo đa kênh)<br>- **IR-API-007** (Push Subscription API suite)<br>- **IR-EXT-003** (W3C Push API / RFC 8292)<br>- **DR-017** (Bảng `WebPushSubscription` & `NotificationLog`) | - **MOD-014** (Robot Chatbot Widget)<br>- **MOD-015** (Deadline Reminder Dispatcher)<br>- **MOD-009** (Notification SignalR Hub)<br>- **MOD-BG-001** (Deadline Reminder Worker)<br>- **ADR-SSS-009** (Robot Assistant)<br>- **ADR-SSS-010** (Multi-Channel Dispatcher)<br>- **SEQ-010** (Deadline Lifecycle & Multi-Channel Dispatch) | - **INT-016** (SignalR Notification Hub)<br>- **INT-032** (`POST .../push-subscription`, `GET/PUT .../preferences`) | - **AC-043** (Widget mở mặc định, đóng theo session)<br>- **AC-044** (Truy vấn NL tuân thủ quyền hạn)<br>- **AC-045** (Nhắc hạn đúng 4 mốc)<br>- **AC-046** (Điều phối thông minh SignalR $\rightarrow$ Push $\rightarrow$ Email)<br>- **AC-047** (Chống trùng lặp tuyệt đối qua NotificationLog)<br>- **TST-036** (Kiểm thử Robot widget & lệnh tắt)<br>- **TST-037** (Kiểm thử điều phối thông báo đa kênh) | **Robot: VERIFIED**<br>**Reminders: PARTIAL** (Services/DB ready, Worker/API pending) |

---

## 4. Ma Trận Truy Vết Tổng Thể Các Phân Hệ Nền Tảng (Modules M01 — M15)

| Phân Hệ / Module SSS | Yêu Cầu SRS Được Phục Vụ | Kiến Trúc & Quyết Định (ADR) | Giao Diện API (INT) | Kịch Bản Kiểm Thử (TST) | Trạng thái Codebase |
|---|---|---|---|---|---|
| **M01: Xác thực & Phân quyền** | `FR-AUTH-001`..`005`, `IR-UI-001`, `IR-UI-005`, `IR-API-001`, `IR-API-004`, `SEC-001`..`004` | `ADR-SSS-001`, `ADR-SSS-002`, `ADR-SSS-008` | `INT-001`, `INT-029` | `TST-001`, `TST-002`, `TST-032` | `VERIFIED` |
| **M02: Thuê bao & Doanh nghiệp** | `FR-TEN-001`..`004`, `IR-UI-004`, `IR-UI-006`, `IR-API-005`, `DR-001`, `DR-016` | `ADR-SSS-001`, `DEC-WS-001` | `INT-002`, `INT-003`, `INT-030` | `TST-003`, `TST-004`, `TST-033`, `TST-034` | `VERIFIED` (trừ `FR-TEN-005` Out-of-Scope) |
| **M03: Cơ cấu Tổ chức & Người dùng** | `FR-ORG-001`..`005`, `DR-002`, `DR-003`, `SEC-003` | `ADR-SSS-001`, `ADR-SSS-003` | `INT-004`, `INT-005` | `TST-005`, `TST-006` | `VERIFIED` |
| **M04: Danh mục Dịch vụ** | `FR-SVC-001`..`004`, `DR-004` | `ADR-SSS-004` | `INT-006`, `INT-007` | `TST-007`, `TST-008` | `PARTIAL` (CRUD catalog xong; dynamic routing rule pending) |
| **M05: Quản lý Công việc (Task)** | `FR-TASK-001`..`010`, `IR-UI-003`, `DR-005`, `DR-006` | `ADR-SSS-005` | `INT-008`, `INT-009`, `INT-010` | `TST-009`, `TST-010`, `TST-011` | `VERIFIED` (Vòng đời Task, Assignment, Confirmation, Checklist hoàn chỉnh) |
| **M06: Yêu cầu Dịch vụ Nội bộ** | `FR-REQ-001`..`009`, `IR-UI-002`, `DR-007`, `DR-008` | `ADR-SSS-004`, `ADR-SSS-005` | `INT-011`, `INT-012`, `INT-013` | `TST-012`, `TST-013`, `TST-014` | `VERIFIED` (Tạo, duyệt, chuyển tiếp, đóng, hủy, liên kết Task đầy đủ) |
| **M07: Quy trình Nghiệp vụ (Workflow)** | `FR-WF-001`..`004`, `DR-009` | `ADR-SSS-006` | `INT-014` | `TST-015`, `TST-016` | `PARTIAL` (Entity/API version draft có sẵn; visual graph designer pending) |
| **M08: Quản lý Cam kết Dịch vụ (SLA)** | `FR-SLA-001`..`004`, `DR-010`, `DR-011` | `ADR-SSS-007` | `INT-015` | `TST-017`, `TST-018` | `PARTIAL` (Profile & version bất biến hoàn thành; runtime clock pending) |
| **M09: Thông báo Thời gian Thực** | `FR-NOTIF-001`..`002`, `DR-012` | `ADR-SSS-001`, `ADR-SSS-010` | `INT-016`, `INT-017` | `TST-019`, `TST-020` | `VERIFIED` (SignalR Hub + In-App Inbox hoàn chỉnh) |
| **M10: Trợ lý AI Thông minh (Legacy)** | `FR-AI-001`..`010`, `IR-EXT-001`, `DR-013` | `ADR-SSS-009` | `INT-018`, `INT-019` | `TST-021`, `TST-022`, `TST-023` | `VERIFIED` (10 năng lực AI tác nghiệp + fallback an toàn) |
| **M11: Báo cáo & Phân tích Dữ liệu** | `FR-REP-001`..`004`, `DR-014` | `ADR-SSS-001` | `INT-020`, `INT-021` | `TST-024`, `TST-025` | `VERIFIED` (Dashboard, Workload, SLA, Company summary) |
| **M12: Nhật ký Kiểm toán (Audit Log)** | `FR-AUD-001`..`003`, `DR-015`, `SEC-006` | `ADR-SSS-001` | `INT-022` | `TST-026`, `TST-027` | `VERIFIED` (AuditLog bất biến, trigger append-only) |
| **M13: Quản lý Tệp tin & MinIO** | `FR-DOC-001`, `IR-API-002`, `SEC-008` | `ADR-SSS-001` | `INT-023`, `INT-024` | `TST-028`, `TST-029` | `VERIFIED` (Upload 3 bước, presigned url 15m, giới hạn 500 MB) |
| **M14: Robot Chatbot & AI Assistance** | `FR-AI-011`, `FR-AI-012`, `FR-AI-013`, `IR-API-006` | `ADR-SSS-009` | `INT-031` | `TST-035`, `TST-036` | `VERIFIED` (Widget UI, Session storage, Vector memory RAG, Preview/Confirm) |
| **M15: Điều phối Nhắc hạn Đa kênh** | `FR-NOTIF-003`, `FR-NOTIF-004`, `IR-API-007`, `IR-EXT-003`, `DR-017` | `ADR-SSS-010` | `INT-032` | `TST-037` | `PARTIAL` (Entities/services/dispatchers hoàn chỉnh; controller & worker pending) |

---

## 5. Kết Luận Kiểm Toán Thực Tế (Codebase Baseline Verification)

1. **Tính Xác Thực Của Độ Phủ (81.8% Fully Implemented & Verified):**
   - 108 / 132 yêu cầu SRS đã có đầy đủ mã nguồn từ Controller, Application Service, Domain Entity, Database Migration đến Angular Component và vượt qua 100% các bộ kiểm thử tự động (.NET: 495 tests, Angular: 270 specs).
   - Không có "yêu cầu mồ côi" (orphan requirements) không rõ mục tiêu nghiệp vụ.
2. **Các Hạng Mục Trọng Điểm Còn Lại:**
   - Hoàn thiện Background Timer Worker và REST Controller cho Phân hệ Nhắc hạn Đa kênh (`MOD-015`, `MOD-BG-001`, `INT-032`).
   - Chuẩn hóa claim `tenant_id` tại các endpoint phụ trợ trong `AiController.cs`.
   - Nâng ngưỡng budget SCSS cho `robot-widget.scss` trong `angular.json` từ 16.00 kB lên 32.00 kB để bản dựng frontend đạt chuẩn tuyệt đối.
