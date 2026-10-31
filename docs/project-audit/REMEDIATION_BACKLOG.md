# Danh Mục Công Việc Khắc Phục Ưu Tiên (Remediation Backlog)
## AI-Assisted BizFlow Platform — Phiên Bản Đối Soát v1.2-FINAL

- **Mã tài liệu:** `BIZFLOW-AUDIT-BACKLOG-v1.2`
- **Phiên bản:** `1.2-FINAL`
- **Ngày lập:** `2026-10-21`
- **Tổ kiểm toán kỹ thuật:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Product Owner, Lead Architect, Scrum Master, QA Lead, Senior Developers)
- **Mục đích:** Thiết lập lộ trình thực thi (Actionable Implementation Backlog) có thứ tự ưu tiên chặt chẽ cho giai đoạn phát triển tiếp theo, khắc phục toàn diện các khoảng trống đã được chỉ ra trong đợt kiểm toán.

---

## 1. Nguyên Tắc Sắp Xếp Thứ Tự Ưu Tiên (Prioritization Framework)

Các hạng mục công việc được phân chia theo mức độ khẩn cấp và tầm quan trọng kỹ thuật/nghiệp vụ:
1. **P0 — BẮT BUỘC / AN NINH & BẢO VỆ ĐA THUÊ BAO (Critical Security & Multi-Tenant Invariants):** Phải hoàn thành trước tiên để loại bỏ mọi nguy cơ rò rỉ dữ liệu hoặc phá vỡ tính toàn vẹn phiên làm việc.
2. **P1 — BẮT BUỘC / NGHIỆP VỤ CỐT LÕI v1.2 (Mandatory Core Requirements):** Các tính năng chức năng bắt buộc theo hợp đồng đặc tả v1.2 đã cam kết cho bản phát hành MVP.
3. **P2 — BẮT BUỘC / NÂNG CAO TRẢI NGHIỆM & TRỢ LÝ AI (AI Assistant & Multi-Channel):** Các tính năng trợ lý robot tương tác, bóc tách việc và điều phối thông báo đa kênh.
4. **P3 — KIỂM THỬ TÍCH HỢP & HOÀN THIỆN TÀI LIỆU (Integration Tests & Documentation Closure):** Đóng các quyết định mở, chạy kiểm thử tích hợp trên Docker và cập nhật tài liệu kỹ thuật.
5. **P4 — CẢI TIẾN TÙY CHỌN (Optional Enhancements / Phase 2):** Các tính năng mở rộng nằm ngoài phạm vi MVP.

---

## 2. Bảng Tổng Hợp Công Việc Khắc Phục (Master Remediation Backlog)

| ID | Ưu Tiên | Phân Loại (Category) | Phát Hiện (Finding) | Yêu Cầu Đối Chiếu | Hành Động Khắc Phục Kiến Nghị | Tiêu Chí Nghiệm Thu (Acceptance Criteria) | Phụ Thuộc / Quyết Định Cần |
|---|---|---|---|---|---|---|---|
| **BKL-SEC-01** | **P0** | Code Defect | `GAP-01`: Đăng nhập bằng ID cho phép bỏ trống `WorkspaceKey` và tự động fallback tìm tenant nếu chỉ có 1 ứng viên. | `FR-AUTH-001`, `BR-025`, `SEC-001` | - **Backend:** Sửa `LoginInput.TenantKey` thành `[Required]`; sửa `AuthenticationService.LoginAsync` để kiểm tra đồng thời cả 3 trường (`Identifier`, `Password`, `TenantKey`). Nếu sai bất kỳ trường nào đều trả về `401 AUTH.INVALID_CREDENTIALS`.<br>- **Frontend:** Thêm `Validators.required` cho `tenantKey` trong `login.ts`, render input bắt buộc trên `login.html`. | 1. Đăng nhập thiếu WorkspaceKey $\rightarrow$ trả về lỗi 400 Validation hoặc 401 Invalid Credentials.<br>2. Đăng nhập sai WorkspaceKey $\rightarrow$ trả về 401.<br>3. Unit test xác nhận 100% không còn fallback tự đoán tenant. | Không |
| **BKL-DB-01** | **P1** | Database Gap | `GAP-02`: Bảng `Company` thiếu các trường định danh doanh nghiệp bắt buộc của v1.2 (`TaxId`, `ContactPersonName`, `ContactPhone`); thiếu bảng `CompanyRegistrationDocuments`. | `FR-TEN-001`, `DR-001`, `DR-016` | - **Database:** Tạo EF Core Migration mới bổ sung 3 cột vào bảng `Company` và tạo bảng `CompanyRegistrationDocuments` (`Id`, `CompanyId`, `DocumentType`, `FileName`, `StorageKey`, `ContentType`, `SizeBytes`, `CreatedAt`).<br>- **Domain:** Mở rộng thực thể `Company.cs` và `CompanyRegistrationDocument.cs`. | 1. Lược đồ PostgreSQL được cập nhật qua Migration thành công 0 lỗi.<br>2. Ràng buộc khóa ngoại `CompanyId` và chỉ mục tìm kiếm hoạt động bình thường. | `BizFlowDbContext` |
| **BKL-FNC-01** | **P1** | Missing Functionality | `GAP-02`: Form đăng ký công khai thiếu 3 trường, thiếu nút sinh mã ngẫu nhiên, thiếu check mã real-time, thiếu upload MinIO. | `FR-TEN-001`, `IR-UI-004`, `IR-API-005` | - **Backend:** Bổ sung endpoint `GET /api/v1/tenants/check-code` (`INT-030`) với Rate Limiting; tích hợp `MinIOClient` lưu file vào bucket `company-documents`.<br>- **Frontend:** Cập nhật reactive form `register-company.ts` đủ 6 trường, thêm nút random code generator, thêm debounce 400ms gọi check-code, thêm file uploader. | 1. Nhập đủ 6 trường hợp lệ mới cho phép submit.<br>2. Bấm nút sinh mã ngẫu nhiên sinh ra chuỗi slug hợp lệ.<br>3. Gõ mã đã tồn tại hiển thị lỗi đỏ ngay lập tức.<br>4. File tải lên được lưu thành công trên MinIO. | `BKL-DB-01`, MinIO Bucket |
| **BKL-FNC-02** | **P1** | Missing Functionality | `GAP-01`: Chưa có tính năng đăng nhập không mật khẩu qua email link và hộp thoại chọn không gian làm việc. | `FR-AUTH-005`, `IR-UI-005`, `IR-API-004`, `DR-016` | - **Backend:** Triển khai 3 endpoints `INT-029` (`/api/v1/auth/email-link/request`, `verify`, `select-workspace`), lưu token Redis TTL 15m, gửi email liên kết.<br>- **Frontend:** Thêm Tab Đăng nhập Email trên `login.ts`; Xây dựng `WorkspaceSelectorDialogComponent` (`IR-UI-005`). | 1. Nhập email nhận được link xác thực qua email.<br>2. Link quá 15 phút bị từ chối.<br>3. Email thuộc $\ge 2$ tenant hiển thị modal chọn workspace.<br>4. Chọn workspace thành công cấp JWT chính thức. | Redis, SMTP Server |
| **BKL-FNC-03** | **P1** | Missing Functionality | `GAP-03`: Platform Admin chưa thể xem và tải tài liệu pháp lý của doanh nghiệp khi xem xét phê duyệt. | `FR-TEN-002`, `IR-UI-006` | - **Backend:** Bổ sung endpoint `GET /api/v1/platform/company-registrations/{id}/documents/{docId}/presigned-url` trả URL tạm thời 15 phút.<br>- **Frontend:** Nâng cấp bảng `company-registrations.ts` hiển thị danh sách tài liệu đính kèm kèm viewer modal. | 1. Platform Admin bấm vào tài liệu mở ra file PDF/PNG qua Presigned URL.<br>2. URL tự hết hạn sau 15 phút. | `BKL-DB-01`, MinIO |
| **BKL-FNC-04** | **P2** | Missing Functionality | `GAP-04`: Chưa có tính năng phân rã công việc bằng AI với bản xem trước tương tác và chốt chặn con người bắt buộc. | `FR-AI-013`, `BR-028`, `IR-API-006` | - **Backend:** Xây dựng 2 endpoints `INT-031`: `POST .../ai-decomposition/preview` (pure compute / DTO đề xuất) và `POST .../ai-decomposition/confirm` (thẩm định quyền và ghi DB).<br>- **Frontend:** Xây dựng component `AiTaskDecompositionPreviewComponent` dạng checklist tương tác, cho phép sửa inline và chọn/bỏ chọn. | 1. Nhập mô tả sinh ra danh sách task đề xuất.<br>2. Endpoint preview tuyệt đối không ghi bản ghi nào vào DB (`BR-028`).<br>3. Bấm "Xác nhận tạo việc" mới lưu công việc vào DB. | OpenAI API Client |
| **BKL-FNC-05** | **P2** | Missing Functionality | `GAP-05`: Chưa có Trợ lý Robot Chatbot Widget nhúng trên giao diện ứng dụng. | `FR-AI-011`, `FR-AI-012`, `BR-027`, `BR-029`, `DR-016` | - **Frontend:** Xây dựng `RobotChatbotComponent` nhúng góc dưới bên phải, quản lý state đóng/mở qua `sessionStorage` (`BR-029`), xử lý lệnh `/task` và `/notifications`.<br>- **Backend:** Xây dựng service RAG trả lời câu hỏi tự nhiên về tiến độ công việc bị chặn nghiêm ngặt bởi `ManagementScope` và RBAC (`BR-027`). | 1. Đăng nhập thành công bot tự động mở.<br>2. Bấm [X] bot thu nhỏ và không tự mở lại trong cùng tab.<br>3. Mở tab mới bot mở lại bình thường.<br>4. Bot không trả lời thông tin việc ngoài quyền. | `BKL-FNC-04`, OpenAI API Client |
| **BKL-DB-02** | **P2** | Database Gap | `GAP-06`: Thiếu bảng lưu cấu hình đăng ký Web Push và nhật ký chống trùng lặp nhắc hạn. | `FR-NOTIF-004`, `DR-017`, `OD-032` | - **Database:** Tạo Migration mới cho `WebPushSubscriptions` và `NotificationLogs` với ràng buộc duy nhất kết hợp `UNIQUE(TenantId, TaskId, UserId, TriggerType, ScheduledAt)`. | 1. Migration tạo bảng thành công.<br>2. Ràng buộc Unique ngăn chặn triệt để bản ghi trùng lặp. | `BizFlowDbContext` |
| **BKL-FNC-06** | **P2** | Missing Functionality | `GAP-06`: Chưa có bộ điều phối thông báo nhắc hạn đa kênh (SignalR $\rightarrow$ Web Push $\rightarrow$ Email) và lệnh `/notifications`. | `FR-NOTIF-003`, `FR-NOTIF-004`, `BR-030`, `IR-API-007` | - **Backend:** Xây dựng `DeadlineReminderWorker` (`MOD-BG-001`), tích hợp thư viện `WebPush-CSharp` VAPID, triển khai endpoints `INT-032`.<br>- **Frontend:** Đăng ký Service Worker nhận Push Notification và Dialog cấu hình nhắc hạn cá nhân. | 1. Người dùng online nhận thông báo qua SignalR toast.<br>2. Người dùng offline nhận Web Push qua trình duyệt.<br>3. Không có Web Push nhận email nhắc việc HTML.<br>4. Không gửi trùng lặp thông báo. | `BKL-DB-02`, VAPID Keys |
| **BKL-TST-01** | **P3** | Missing Tests | `GAP-TEST-01`: Bộ `BizFlow.IntegrationTests` bị chặn do Docker Desktop Daemon không chạy. | `TST-001` .. `TST-031` | Khởi động Docker Desktop trên máy chủ kiểm thử, chạy lệnh `dotnet test tests/BizFlow.IntegrationTests/` và ghi nhận kết quả. | 100% Integration Tests kết nối với PostgreSQL và Redis thật thành công PASS. | Docker Desktop Daemon |
| **BKL-DOC-01** | **P3** | Documentation Defect | `CONF-01` & `CONF-03`: Cần đóng các quyết định mở `OD-019`, `OD-021`, `OD-025`, `OD-028`, `OD-031`, `OD-032`. | Toàn bộ bộ tài liệu | Product Owner và Ban Kỹ thuật ký duyệt chính thức các quyết định mở; cập nhật trạng thái `CONFIRMED` trong `OPEN_DECISIONS.md`. | Không còn quyết định mở quan trọng nào ở trạng thái treo. | Product Owner Sign-off |
| **BKL-SEC-02** | **P3** | Architecture Deviation | Cảnh báo an ninh gói NuGet `System.Security.Cryptography.Pkcs` 10.0.0 (`NU1902`/`NU1903`). | `NFR-SEC-002` | Cập nhật gói `System.Security.Cryptography.Pkcs` lên phiên bản vá lỗi khi Microsoft phát hành chính thức cho .NET 10. | Lệnh `dotnet build BizFlow.slnx` trả về 0 Errors, 0 Warnings. | Microsoft .NET Patch |
| **BKL-OPT-01** | **P4** | Optional Improvement | Tự động hóa hoàn toàn quy trình Onboarding qua cổng thanh toán thẻ tín dụng (`FR-TEN-005`, `BIZ-027`). | `FR-TEN-005`, `OD-021` | Tích hợp Stripe/VNPay API, tự động trừ tiền và kích hoạt workspace trong 30 giây (Phase 2 Roadmap). | Doanh nghiệp thanh toán thành công được cấp workspace tự động không cần admin duyệt. | Payment Gateway Contract |
| **BKL-OPT-02** | **P4** | Optional Improvement | Giao diện thiết kế quy trình đồ họa kéo thả trực quan (Drag-and-Drop Workflow Visual Designer). | `FR-WF-001` | Xây dựng visual workflow canvas trên Angular sử dụng `@angular/cdk/drag-drop` hoặc thư viện đồ họa node-based. | Người dùng kéo thả các bước duyệt trực quan thay vì cấu hình biểu mẫu. | Không |

---

## 3. Lộ Trình Triển Khai Kiến Nghị Theo Sprint (Recommended Sprint Plan)

### Sprint 1: Củng Cố An Ninh & Đăng Ký Doanh Nghiệp Chuẩn v1.2 (Thời lượng: 2 tuần)
- **Mục tiêu:** Đóng lỗ hổng an ninh đăng nhập và hoàn thiện luồng đăng ký doanh nghiệp công khai 6 trường kèm hồ sơ MinIO.
- **Hạng mục thực hiện:**
  - `BKL-SEC-01`: Cưỡng chế 3 trường bắt buộc khi đăng nhập ID (P0).
  - `BKL-DB-01`: Migration thêm trường `TaxId`, SĐT, người liên hệ và bảng `CompanyRegistrationDocuments` (P1).
  - `BKL-FNC-01`: Hoàn thiện form `register-company.ts`, check mã workspace real-time, upload file MinIO (P1).
  - `BKL-FNC-03`: Platform Admin xem xét hồ sơ pháp lý qua Presigned URL 15 phút (P1).
- **Kết quả nghiệm thu Sprint 1:** Quy trình tiếp nhận doanh nghiệp đạt chuẩn 100% v1.2.

### Sprint 2: Đăng Nhập Email-Link & Phân Rã Việc Bằng AI (Thời lượng: 2 tuần)
- **Mục tiêu:** Trải nghiệm đăng nhập không mật khẩu đa công ty và tự động hóa khởi tạo việc thông minh.
- **Hạng mục thực hiện:**
  - `BKL-FNC-02`: Đăng nhập Email-link passwordless và Workspace Selector modal (P1).
  - `BKL-FNC-04`: Phân rã công việc bằng AI với bảng xem trước tương tác và chốt chặn xác nhận con người bắt buộc `BR-028` (P2).
- **Kết quả nghiệm thu Sprint 2:** Hoàn thiện 2 tính năng trải nghiệm người dùng đột phá nhất của v1.2.

### Sprint 3: Trợ Lý Robot Tương Tác & Thông Báo Đa Kênh (Thời lượng: 2 tuần)
- **Mục tiêu:** Tích hợp Robot chatbot đồng hành và mạng lưới nhắc nhở deadline đa kênh.
- **Hạng mục thực hiện:**
  - `BKL-FNC-05`: Robot Chatbot Widget nhúng UI, lưu state qua `sessionStorage` (`BR-029`), lệnh `/task` (P2).
  - `BKL-DB-02`: Migration bảng `WebPushSubscriptions` và `NotificationLogs` (P2).
  - `BKL-FNC-06`: Background worker quét deadline, điều phối SignalR $\rightarrow$ Web Push $\rightarrow$ Email (P2).
  - `BKL-TST-01`: Chạy toàn bộ Integration Tests trên môi trường Docker bật sẵn (P3).
  - `BKL-DOC-01`: Đóng toàn bộ các quyết định mở với chữ ký của Product Owner (P3).
- **Kết quả nghiệm thu Sprint 3:** Hệ thống đạt trạng thái 100% phù hợp với toàn bộ hồ sơ đặc tả v1.2-FINAL, sẵn sàng ra mắt thị trường.
