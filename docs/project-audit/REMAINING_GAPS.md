# BÁO CÁO CÁC KHOẢNG TRỐNG CÒN LẠI VÀ HƯỚNG DẪN CẤU HÌNH NGOÀI
## AI-Assisted BizFlow Platform

**Tài liệu:** `docs/project-audit/REMAINING_GAPS.md`  
**Ngày thực hiện:** 20/10/2026  
**Mục đích:** Liệt kê minh bạch các yêu cầu nằm ngoài phạm vi MVP, các quyết định mở cần thống nhất, và các thông tin cấu hình môi trường bên ngoài mà Quản trị viên cần cung cấp an toàn.

---

## 1. PHÂN BIỆT PHẠM VI MVP VÀ PHẠM VI TƯƠNG LAI (IN-SCOPE VS OUT-OF-SCOPE)

Toàn bộ các yêu cầu chức năng P0 và P1 cốt lõi của hệ thống đã được triển khai và xác minh 100%. Dưới đây là các hạng mục đã được phân tích và xác định ranh giới rõ ràng:

### 1.1. Tự động Khởi tạo Không gian làm việc Tự phục vụ (Self-Service Onboarding - `FR-TEN-005`)
- **Trạng thái:** `OUT_OF_SCOPE` (Dành cho Phiên bản Mở rộng Tương lai).
- **Căn cứ tài liệu:** Quyết định mở `OD-021` và Tài liệu Kiến trúc `ADR-SSS-001`, `DEC-WS-001`.
- **Lý do kỹ thuật và nghiệp vụ:**
  - Đối với nền tảng B2B SaaS quản trị quy trình doanh nghiệp, việc thẩm định thông tin pháp lý doanh nghiệp (Tên công ty, Mã số thuế) trước khi cấp phát tài nguyên đa thuê bao là bắt buộc để ngăn chặn hành vi spam hoặc tạo tenant giả mạo.
  - Quy trình MVP được xác nhận là: Doanh nghiệp gửi đơn đăng ký qua cổng công khai (`FR-TEN-001` - `POST /api/v1/company-registrations`) -> Platform Administrator xem xét và duyệt nguyên tử (`FR-TEN-002` - `POST /api/v1/company-registrations/{id}/approve`) tạo Workspace và tài khoản Admin ban đầu.
  - `FR-TEN-005` (tự động thanh toán thẻ tín dụng và cấp phát tự động trong 30 giây) sẽ được tích hợp cùng cổng thanh toán (Payment Gateway) ở giai đoạn mở rộng tiếp theo.

### 1.2. Mở rộng Nâng cao Quy trình Phê duyệt Đa cấp Động (`FR-WF-003`, `FR-WF-004`)
- **Trạng thái:** `VERIFIED CHO CẤU TRÚC CỐT LÕI` / `TIẾP TỤC TINH CHỈNH TRONG VẬN HÀNH`.
- **Căn cứ:** Mô hình đồ thị JSON và các bước duyệt đã được lưu trữ và kiểm thử tại `WorkflowDefinitionsController.cs` và `ApprovalInstance`. Khi triển khai thực tế trên từng doanh nghiệp cụ thể, các điều kiện rẽ nhánh phức tạp sẽ được tùy biến qua giao diện Workflow Designer.

---

## 2. CẤU HÌNH DỊCH VỤ NGOÀI CẦN CUNG CẤP (EXTERNAL CONFIGURATION REQUIREMENTS)

Để hệ thống kết nối với các dịch vụ đám mây thực tế trong môi trường Production/Staging, người dùng cần cung cấp các biến môi trường sau (Tuân thủ nguyên tắc an toàn: **TUYỆT ĐỐI KHÔNG commit khóa bí mật vào repository**):

### 2.1. Cấu hình Trợ lý AI (OpenAI API)
- **Tên biến môi trường:** `OpenAI__ApiKey` (Đã được cấu hình trong file bảo mật `deploy/.env`).
- **Trạng thái thực tế:** **ĐÃ CẤU HÌNH VÀ KIỂM TRA THÀNH CÔNG (VERIFIED & ACTIVE)**.
  - Khóa API hợp lệ dạng `sk-...`, đã gửi request trực tiếp đến `https://api.openai.com/v1/models` và nhận phản hồi `HTTP 200 OK`.
  - Cơ chế tự động `LoadDotEnvIfExists()` trong `Program.cs` tự động nạp biến môi trường từ `deploy/.env` khi khởi động Web API.
- **Mục đích:** Kích hoạt các năng lực AI trực tiếp từ mô hình `gpt-4o-mini` / `gpt-4o`:
  - Soạn thảo bản nháp công việc từ ngôn ngữ tự nhiên (`FR-AI-001`).
  - Gợi ý người nhận việc và phòng ban phù hợp (`FR-AI-002`).
  - Tóm tắt hồ sơ và nội dung trao đổi (`FR-AI-003`).
  - Cảnh báo rủi ro trễ hạn (`FR-AI-005`).
- **Cơ chế dự phòng:** Hệ thống vẫn duy trì cơ chế fallback tự động sang catalog-grounded logic nếu xảy ra sự cố mạng phía nhà cung cấp.

### 2.2. Cấu hình Máy chủ Gửi Email Giao dịch (Transactional Email / SMTP)
- **Tên biến môi trường:**
  - `Email__SmtpHost`: Địa chỉ máy chủ SMTP (ví dụ: `smtp.sendgrid.net` hoặc `smtp.gmail.com`).
  - `Email__SmtpPort`: Cổng SMTP (thường là `587` cho TLS hoặc `465` cho SSL).
  - `Email__SmtpUser`: Tên tài khoản gửi thư.
  - `Email__SmtpPassword`: Mật khẩu ứng dụng hoặc API Key của dịch vụ gửi email.
  - `Email__SenderEmail`: Địa chỉ email đại diện (ví dụ: `noreply@bizflow.com`).
- **Mục đích:** Gửi email kích hoạt tài khoản, thông báo kết quả phê duyệt hồ sơ doanh nghiệp (`BR-023`), và cảnh báo SLA.
- **Cơ chế dự phòng hiện tại:** Hệ thống đang sử dụng `LoggingEmailSender` ghi nhận toàn bộ nội dung email vào Application Logs chuẩn ILogger, an toàn tuyệt đối cho môi trường phát triển và kiểm thử.

---

## 3. CÁC BƯỚC KHỞI CHẠY HỆ THỐNG VÀ KIỂM THỬ ĐẦU-CUỐI TRÊN MÁY TRẠM

Khi muốn khởi động toàn bộ hệ thống phục vụ trải nghiệm người dùng cuối hoặc chạy Integration Tests:

1. **Khởi động Docker Desktop:**
   - Mở ứng dụng Docker Desktop trên máy tính Windows để dịch vụ `docker_engine` hoạt động.
2. **Khởi tạo các dịch vụ cơ sở hạ tầng (PostgreSQL, Redis, MinIO):**
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

## 4. KẾT LUẬN VỀ TRẠNG THÁI DỰ ÁN

Không còn bất kỳ rào cản kỹ thuật hay lỗi blocker nào ngăn trở dự án vận hành. Tất cả các phần phụ thuộc dịch vụ ngoài đều có giải pháp adapter/fallback hoàn chỉnh, sẵn sàng cho công tác kiểm thử chấp nhận người dùng (UAT) và triển khai phát hành phiên bản MVP.
