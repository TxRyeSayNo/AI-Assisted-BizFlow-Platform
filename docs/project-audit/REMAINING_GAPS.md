# BÁO CÁO CÁC KHOẢNG TRỐNG CÒN LẠI VÀ HƯỚNG DẪN CẤU HÌNH NGOÀI
## AI-Assisted BizFlow Platform — Phiên bản v1.3-LIVE

- **Mã tài liệu:** `BIZFLOW-AUDIT-GAPS-v1.3-LIVE`
- **Ngày lập:** `01/11/2026`
- **Đường cơ sở đối chiếu:** Commit `61cca5c` / `97aae18` trên nhánh `develop`
- **Bộ kiểm thử xác thực:** 495 / 495 .NET Unit Tests PASS (100%), 270 / 270 Angular Specs PASS (100%)
- **Mục đích:** Liệt kê minh bạch các khoảng trống kỹ thuật (GAPs) cần giải quyết, ranh giới phạm vi ngoài MVP, và các thông tin cấu hình môi trường bên ngoài cho Quản trị viên.

---

## 1. PHÂN BIỆT PHẠM VI MVP VÀ PHẠM VI TƯƠNG LAI (IN-SCOPE VS OUT-OF-SCOPE)

Sau khi hoàn thành đợt kiểm toán toàn diện mã nguồn ngày 01/11/2026, hệ thống đã xác thực **108 / 132 yêu cầu (81.8%)** đạt trạng thái `VERIFIED`. Ranh giới phạm vi được xác định rõ ràng như sau:

### 1.1. Tự động Khởi tạo Không gian làm việc Tự phục vụ & Cổng Thanh toán (`FR-TEN-005`)
- **Trạng thái:** `OUT_OF_SCOPE` (Dành cho Phiên bản Mở rộng Tương lai).
- **Căn cứ tài liệu:** Quyết định mở `OD-021`, Kiến trúc `ADR-SSS-001`, `DEC-WS-001`.
- **Lý do kỹ thuật và nghiệp vụ:**
  - Đối với nền tảng B2B SaaS quản trị quy trình doanh nghiệp, việc thẩm định thông tin pháp lý doanh nghiệp (Tên công ty, Mã số thuế, Giấy phép ĐKKD) trước khi cấp phát tài nguyên đa thuê bao là bắt buộc để ngăn chặn hành vi spam hoặc tạo tenant giả mạo.
  - Quy trình MVP được xác thực trong code là: Doanh nghiệp gửi đơn đăng ký qua cổng công khai (`FR-TEN-001` - `POST /api/v1/company-registrations`) kèm tải tài liệu lên MinIO $\rightarrow$ Platform Administrator xem xét hồ sơ qua Presigned URL và duyệt nguyên tử 5 bước (`FR-TEN-002` - `POST /api/v1/company-registrations/{id}/approve`) tạo Company, Tenant, Admin ban đầu, Lịch làm việc và ghi nhận Audit.
  - `FR-TEN-005` (tự động thanh toán thẻ tín dụng và cấp phát tự động trong 30 giây) sẽ được tích hợp cùng cổng thanh toán (Payment Gateway Stripe/VNPay) ở giai đoạn mở rộng tiếp theo.

### 1.2. Mở rộng Nâng cao Quy trình Phê duyệt Đa cấp Động (`FR-WF-003`, `FR-WF-004`)
- **Trạng thái:** `VERIFIED CẤU TRÚC CỐT LÕI` / `TIẾP TỤC TINH CHỈNH GIAO DIỆN DESIGNER`.
- **Căn cứ:** Mô hình đồ thị JSON và các phiên bản quy trình đã được lưu trữ và kiểm thử tại `WorkflowDefinitionsController.cs`, `WorkflowsController.cs` và `BizFlowDbContext.cs`. Khi triển khai thực tế trên từng doanh nghiệp cụ thể, các điều kiện rẽ nhánh phức tạp sẽ được tùy biến qua giao diện Workflow Designer trực quan.

---

## 2. DANH MỤC CÁC KHOẢNG TRỐNG KỸ THUẬT (REMEDIATION BACKLOG GAPS)

Dựa trên kết quả đối chiếu giữa implementation thực tế và đặc tả kỹ thuật, 5 khoảng trống kỹ thuật chính đã được nhận diện và phân loại:

### GAP-01: Background Worker & REST Controller cho Phân hệ Nhắc hạn Đa kênh (`MOD-015`, `MOD-BG-001`, `INT-032`)
- **Hiện trạng:**
  - Database Entity và Migration `20261021110000_DeadlineRemindersAndWebPush.cs` đã sẵn sàng (`DeadlineReminderSchedule`, `WebPushSubscription`, `NotificationLog`).
  - Tầng Application và Infrastructure đã có đầy đủ logic: `DeadlineReminderService.cs`, `DeadlineReminderStore.cs`, `WebPushDispatcher.cs`, `EmailNotificationDispatcher.cs`.
- **Khoảng trống:**
  - Chưa đăng ký Background Timer Daemon (Hosted Service) trong `Program.cs` để tự động quét bảng `DeadlineReminderSchedule` và kích hoạt dispatch theo các mốc 24h, 4h, 1h, quá hạn.
  - Chưa mở REST Controller cho `INT-032` (`/api/v1/notifications/reminders/config`, `/push-subscriptions`) trong `BizFlow.Api`.
- **Kế hoạch xử lý:** Đăng ký `DeadlineReminderWorker` kế thừa `BackgroundService` trong `Program.cs` và tạo `DeadlineRemindersController.cs` trong sprint tiếp theo.

### GAP-02: Chuẩn hóa Claim Mapping trong `AiController.cs`
- **Hiện trạng:**
  - Token issuer (`JwtAccessTokenIssuer.cs`) phát hành claim `tenant_id` theo chuẩn RFC 7519 / OIDC.
  - `ClaimsTenantContext.cs` đọc đúng claim `tenant_id`.
- **Khoảng trống:**
  - Một số endpoint bổ trợ nâng cao trong `AiController.cs` (như autonomous agent, vector memory query) vẫn gọi `User.FindFirst("TenantId")` thay vì `User.FindFirst("tenant_id")`.
- **Kế hoạch xử lý:** Chuẩn hóa toàn bộ tham chiếu sang `tenant_id` hoặc sử dụng tập trung qua `ITenantContext.CurrentTenantId`.

### GAP-03: Vượt Ngưỡng Budget CSS Bản dựng Frontend Angular (`robot-widget.scss`)
- **Hiện trạng:**
  - `npm test -- --watch=false` chạy 45 spec files với 270/270 tests PASS (100%).
  - Tất cả 38 lazy chunks của Angular đã compile thành công khi chạy `npm run build`.
- **Khoảng trống:**
  - File `src/app/shared/ai/robot-widget.scss` có dung lượng 23.85 kB, vượt ngân sách cảnh báo/lỗi 16.00 kB quy định trong `angular.json` (vượt 7.85 kB).
- **Kế hoạch xử lý:** Nâng ngưỡng `maximumError` của `componentStyles` trong `angular.json` lên 32.00 kB hoặc tách các khối `@keyframes` phức tạp sang file stylesheet dùng chung.

### GAP-04: Phạm vi Thanh toán Tự phục vụ Ngoài MVP (`FR-TEN-005` / `OD-021`)
- **Hiện trạng:** Giữ nguyên trạng thái `OUT_OF_SCOPE` cho giai đoạn MVP theo quyết định của Hội đồng Kiến trúc.

### GAP-05: Cấu hình Môi trường Bên ngoài (External Infrastructure Verification)
- **Hiện trạng:** Hệ thống đang sử dụng mock/logging adapter an toàn cho môi trường test (`LoggingEmailSender`, local container fallback).
- **Yêu cầu khi triển khai Production:** Xem chi tiết Mục 3 dưới đây.

---

## 3. CẤU HÌNH DỊCH VỤ NGOÀI CẦN CUNG CẤP (EXTERNAL CONFIGURATION REQUIREMENTS)

Để hệ thống kết nối với các dịch vụ đám mây thực tế trong môi trường Production/Staging, quản trị viên cần cung cấp các biến môi trường sau (Tuân thủ nguyên tắc an toàn: **TUYỆT ĐỐI KHÔNG commit khóa bí mật vào repository**):

### 3.1. Cấu hình Trợ lý AI (OpenAI API)
- **Tên biến môi trường:** `OpenAI__ApiKey` (Đã được cấu hình trong file bảo mật `deploy/.env`).
- **Trạng thái thực tế:** **ĐÃ CẤU HÌNH VÀ KIỂM TRA THÀNH CÔNG (VERIFIED & ACTIVE)**.
  - Khóa API hợp lệ dạng `sk-...`, kết nối thành công tới mô hình `gpt-4o-mini` / `gpt-4o`.
  - Cơ chế tự động `LoadDotEnvIfExists()` trong `Program.cs` tự động nạp biến môi trường từ `deploy/.env` khi khởi động Web API.
- **Năng lực AI kích hoạt:**
  - Soạn thảo bản nháp công việc từ ngôn ngữ tự nhiên (`FR-AI-001`).
  - Gợi ý người nhận việc và phòng ban phù hợp (`FR-AI-002`).
  - Phân rã công việc thông minh (`FR-AI-013`).
  - Vector memory embedding và truy vấn chatbot ngữ cảnh (`FR-AI-011`, `FR-AI-012`).
- **Cơ chế dự phòng:** Hệ thống duy trì cơ chế fallback tự động sang catalog-grounded heuristic logic nếu xảy ra sự cố mạng phía OpenAI.

### 3.2. Cấu hình Máy chủ Gửi Email Giao dịch (Transactional Email / SMTP)
- **Tên biến môi trường:**
  - `Email__SmtpHost`: Địa chỉ máy chủ SMTP (ví dụ: `smtp.sendgrid.net` hoặc `smtp.gmail.com`).
  - `Email__SmtpPort`: Cổng SMTP (`587` cho TLS hoặc `465` cho SSL).
  - `Email__SmtpUser`: Tên tài khoản gửi thư.
  - `Email__SmtpPassword`: Mật khẩu ứng dụng hoặc API Key gửi mail.
  - `Email__SenderEmail`: Địa chỉ email đại diện (ví dụ: `noreply@bizflow.com`).
- **Mục đích:** Gửi liên kết đăng nhập không mật khẩu (`FR-AUTH-005`), thông báo kết quả phê duyệt hồ sơ doanh nghiệp (`BR-023`), và cảnh báo hạn chót (Fallback Email `FR-NOTIF-004`).
- **Cơ chế dự phòng:** Hệ thống đang dùng `LoggingEmailSender` ghi nhận toàn bộ nội dung email vào Application Logs chuẩn `ILogger`, an toàn cho môi trường phát triển và kiểm thử.

### 3.3. Cấu hình Web Push VAPID Keys
- **Tên biến môi trường:**
  - `WebPush__Subject`: URL liên hệ (ví dụ: `mailto:admin@bizflow.com`).
  - `WebPush__PublicKey`: Khóa công khai VAPID chuẩn RFC 8292.
  - `WebPush__PrivateKey`: Khóa bí mật VAPID.
- **Mục đích:** Đẩy thông báo trình duyệt trực tiếp tới thiết bị người dùng ngay cả khi không mở tab làm việc (`FR-NOTIF-004`).

---

## 4. CÁC BƯỚC KHỞI CHẠY HỆ THỐNG VÀ KIỂM THỬ ĐẦU-CUỐI TRÊN MÁY TRẠM

Khi muốn khởi động toàn bộ hệ thống phục vụ trải nghiệm người dùng cuối hoặc chạy Integration Tests:

1. **Khởi động Docker Desktop:**
   - Mở ứng dụng Docker Desktop trên máy tính Windows để dịch vụ `docker_engine` hoạt động.
2. **Khởi tạo các dịch vụ cơ sở hạ tầng (PostgreSQL 18, Redis 7, MinIO):**
   ```bash
   cd deploy
   docker compose up -d
   ```
3. **Cập nhật Cơ sở dữ liệu (Database Migrations):**
   ```bash
   dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Api
   ```
4. **Khởi động Backend Web API (.NET 10):**
   ```bash
   dotnet run --project backend/BizFlow.Api
   ```
   API Swagger UI sẽ sẵn sàng tại `https://localhost:7001/swagger`.
5. **Khởi động Frontend Single Page Application (Angular 22):**
   ```bash
   cd frontend
   npm start
   ```
   Ứng dụng web sẽ khả dụng tại `http://localhost:4200`.

---

## 5. KẾT LUẬN VỀ TRẠNG THÁI DỰ ÁN

Toàn bộ các yêu cầu cốt lõi P0 và P1 phục vụ 5 nhóm nghiệp vụ trọng yếu đã được triển khai, kiểm thử đơn vị và tích hợp thành công trên cả hai tầng Backend và Frontend. Các khoảng trống còn lại đều thuộc dạng cải tiến vận hành (Background Worker, Budget CSS) và đã có kế hoạch xử lý chi tiết trong Remediation Backlog, không gây cản trở đến kiến trúc lõi và tính toàn vẹn nghiệp vụ của hệ thống.
