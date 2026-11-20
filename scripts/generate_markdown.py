import os

md_path = "docs/BizFlow_Final_Release_Document.md"

md_content = """# TRƯỜNG ĐẠI HỌC FPT - FPT UNIVERSITY

<p align="center">
  <img src="../test_img1.png" alt="FPT University Logo" width="320" />
</p>

<br/>

# <span style="color:#C00000">AI-Assisted BizFlow Platform</span>
## <span style="color:#C00000">Final Release Document</span>

<br/><br/>

**– Hanoi, November 2026 –**

---

## Table of Contents
* **0. Release Notes v1.0.0** ............................................................................................ 3
  * 0.1 Executive Summary ............................................................................................ 3
  * 0.2 Release Scope .................................................................................................... 3
  * 0.3 Bug Fixes & Resolved Issues .............................................................................. 5
  * 0.4 Known Issues & Limitations ............................................................................... 6
  * 0.5 Deployment & Rollback Strategy ....................................................................... 7
  * 0.6 Post-Release Monitoring & Operational Metrics ................................................... 8
* **I. Deliverable Package** ............................................................................................ 9
* **II. Installation Guides** ............................................................................................. 11
  * 1. Environment Prerequisites & Hardware Specifications ...................................... 11
  * 2. Configuration & Environment Variables Reference ........................................... 12
  * 3. Infrastructure & Backing Services Deployment ................................................. 14
  * 4. Backend Service Setup & Execution (.NET 10 Web API) .................................... 15
  * 5. Frontend Application Setup & Execution (Angular 22 SPA) ............................... 17
  * 6. Automated Test Suite Execution & Quality Metrics ............................................ 18
  * 7. Baseline Test Accounts & Access Credentials Matrix ........................................... 19
* **III. User Manual** ................................................................................................... 21
  * 1. Overview ........................................................................................................... 21
    * 1.1 Dual-Plane Architectural Governance ........................................................... 21
    * 1.2 Four Primary User Roles & Responsibility Matrix .......................................... 22
    * 1.3 Core State Machine Lifecycles ....................................................................... 23
  * 2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration) ......... 26
  * 3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication) ........ 30
  * 4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Tenant Setup) .......... 34
  * 5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (Task Lifecycle Management) ........ 41
  * 6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (Service Request) ............ 48
  * 7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Decomposition) ........ 55
  * 8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA & Reports) ........ 59
  * 9. Workflow 8: Khôi phục Mật khẩu & Thu hồi Phiên (Password Recovery) ........ 65

---

## 0. Release Notes v1.0.0

### 0.1 Executive Summary
Bản phát hành v1.0.0 (ngày 15/11/2026) đóng gói toàn bộ năng lực cốt lõi của nền tảng AI-Assisted BizFlow Platform trên hạ tầng .NET 10 và Angular 22. Hệ thống vận hành theo mô hình đa thuê bao (multi-tenant) với 2 mặt phẳng quản trị tách biệt (Platform Plane và Tenant Plane), bao gồm 8 luồng nghiệp vụ hoàn chỉnh, máy trạng thái 10 bước cho Nhiệm vụ (Task) và 12 bước cho Yêu cầu dịch vụ (Request), cùng trợ lý AI bóc tách công việc có sự xác nhận bắt buộc của con người (Human-in-the-Loop). Kết quả kiểm toán đối soát kỹ thuật xác thực 108/132 yêu cầu (81.8% VERIFIED cốt lõi); 36 màn hình và chức năng UI đáp ứng điều kiện vận hành MVP. Dự án hiện có 8 hạn chế kỹ thuật đã nhận diện (Known Issues) được kiểm soát với giải pháp thay thế (workaround) cụ thể, không còn lỗi chặn nghiêm trọng (Blocker). Đánh giá tổng thể chất lượng bàn giao đạt trạng thái: **GO** (Đủ điều kiện nghiệm thu đồ án).

### 0.2 Release Scope
Bảng phạm vi tính năng bàn giao thực tế trong bản phát hành v1.0.0:

| Tính năng | Mã FR | Screen ID | Route thật | Trạng thái |
|:---|:---|:---:|:---|:---:|
| Đăng ký doanh nghiệp công khai | FR-TEN-001, FR-TEN-005 | S-04 | `/register-company` | Done |
| Phê duyệt cấp phát doanh nghiệp | FR-TEN-002 | S-08 | `/platform/companies` | Done |
| Nhật ký kiểm toán toàn nền tảng | FR-AUD-001 | S-09 | `/platform/audit` | Done |
| Đăng nhập xác thực hợp nhất | FR-AUTH-001, FR-AUTH-005 | S-01, S-02 | `/login` | Done |
| Khôi phục mật khẩu qua email | FR-AUTH-003 | S-05, S-06 | `/forgot-password`, `/reset-password` | Partial |
| Bảng điều khiển không gian làm việc | FR-WS-001 | S-07 | `/workspace` | Done |
| Quản lý danh bạ người dùng | FR-ORG-001 | S-14 | `/settings/organization/users` | Done |
| Quản lý cơ cấu phòng ban | FR-ORG-002 | S-13 | `/settings/organization/departments` | Done |
| Quản lý vai trò & quyền hạn RBAC | FR-SEC-001 | S-15 | `/settings/organization/roles` | Done |
| Danh mục dịch vụ nội bộ | FR-SVC-001 | S-16 | `/settings/services` | Done |
| Quản lý quy trình & Thiết kế trực quan | FR-WF-001 | S-17, S-18 | `/settings/workflows`, `/settings/workflows/:id/versions/:v` | Done |
| Quản lý hồ sơ & phiên bản cam kết SLA | FR-SLA-001, FR-SLA-002 | S-19, S-20 | `/settings/sla-profiles`, `/settings/sla-profiles/:id/versions` | Done |
| Danh sách & Khởi tạo nhiệm vụ | FR-TASK-001, FR-TASK-002 | S-10, S-11 | `/tasks`, `/tasks/new` | Done |
| Chi tiết nhiệm vụ & Vòng đời 10 bước | FR-TASK-003 | S-13 | `/tasks/:id` | Done |
| Trợ lý AI bóc tách công việc thông minh | FR-AI-013 | S-12 | `/tasks/ai-decomposition` | Done |
| Danh sách & Khởi tạo yêu cầu dịch vụ | FR-REQ-001, FR-REQ-002 | S-14, S-15 | `/requests`, `/requests/new` | Done |
| Chi tiết yêu cầu dịch vụ & Vòng đời 12 bước | FR-REQ-003 | S-16 | `/requests/:id` | Done |
| Hàng đợi phê duyệt yêu cầu đa cấp | FR-REQ-004 | S-26 | `/approvals` | Partial |
| Giám sát vi phạm SLA thời gian thực | FR-SLA-003 | S-31 | `/sla-monitor` | Done |
| Báo cáo quản trị hiệu suất | FR-REP-001 | S-30 | `/reports` | Done |
| Tra cứu lưu trữ hồ sơ tập trung | FR-ARC-001 | S-32 | `/records/search` | Done |
| Trung tâm thông báo sự kiện | FR-NOTIF-001 | S-33 | `/notifications` | Done |
| Nhật ký kiểm toán doanh nghiệp | FR-AUD-002 | S-21 | `/audit` | Done |

### 0.3 Bug Fixes & Resolved Issues
Danh mục các khiếm khuyết kỹ thuật và lỗi kiến trúc đã được xử lý triệt để trước khi phát hành v1.0.0:

| Mã Defect | Mô tả lỗi | Mức độ | Module | Phương án sửa đổi | Người verify |
|:---|:---|:---:|:---|:---|:---|
| **BKL-SEC-01** | Đăng nhập ID cho phép bỏ trống `WorkspaceKey` và tự động đoán tenant nếu chỉ có 1 ứng viên. | Critical | Authentication | Sửa `LoginInput.TenantKey` thành bắt buộc `[Required]`, cưỡng chế kiểm tra đồng thời cả 3 trường và trả `401 AUTH.INVALID_CREDENTIALS` nếu sai. | Truyền / Lead QA |
| **BKL-DB-01** | Bảng `Company` thiếu các trường định danh bắt buộc v1.2 (`TaxId`, `ContactPhone`, `ContactPersonName`); thiếu bảng lưu tài liệu pháp lý. | High | Tenancy | Tạo migration `20261021100000_CompanyRegistration6FieldsAndEvidence.cs` bổ sung 3 cột và tạo bảng `CompanyRegistrationDocuments`. | Tường / DB Lead |
| **BKL-FNC-01** | Form đăng ký công khai thiếu kiểm tra trùng mã workspace real-time và tải tệp MinIO. | High | Onboarding | Bổ sung endpoint check-code debounce 400ms và tích hợp upload Presigned URL MinIO an toàn. | Tường / Dev |
| **BKL-FNC-03** | Platform Admin không mở và xem được tài liệu giấy phép kinh doanh khi phê duyệt. | High | Platform | Bổ sung endpoint sinh Presigned URL 15 phút an toàn cho Platform Admin tải xem tài liệu thẩm định. | Nguyên / Sec Lead |
| **BKL-FNC-04** | Phân rã công việc bằng AI tự động lưu ngầm dữ liệu khi preview, vi phạm nguyên tắc con người kiểm soát. | Medium | AI Assistant | Tách 2 endpoint riêng biệt: preview thuần túy trả DTO tính toán không ghi DB; chỉ ghi DB khi người dùng bấm xác nhận (`confirm`). | Hà / AI Lead |
| **BKL-DB-02** | Thiếu ràng buộc chống trùng lặp bản ghi lịch nhắc hẹn deadline đa kênh. | Medium | Notifications | Tạo migration bổ sung bảng với ràng buộc Unique kết hợp `(TenantId, TaskId, UserId, TriggerType, ScheduledAt)`. | Dũng / Backend |
| **BKL-FNC-05** | Widget Chatbot AI chưa lưu trạng thái đóng/mở theo tab làm việc. | Low | Frontend | Quản lý state widget qua `sessionStorage` để không tự bung lại khi người dùng đã chủ động tắt. | Truyền / Frontend |

**Thống kê:** Tổng cộng 7/7 lỗi và khiếm khuyết kỹ thuật trọng yếu đã được khắc phục hoàn toàn trên nhánh develop (commit `150eabb`).

### 0.4 Known Issues & Limitations
Bảng danh mục các hạn chế và vấn đề kỹ thuật còn tồn tại, giải pháp thay thế và kế hoạch khắc phục:

| Mã Issue | Mô tả vấn đề | Mức độ | Lý do kỹ thuật | Giải pháp thay thế (Workaround) | Kế hoạch |
|:---|:---|:---:|:---|:---|:---|
| **KISS-01** | Đăng nhập bằng Magic Link chưa gửi email thực tế. | Medium | Chưa cấu hình máy chủ SMTP production; nút magic link chưa hiển thị trên UI chính. | Đăng nhập bằng Mã không gian + Tên đăng nhập + Mật khẩu hoặc Google Sign-in. | Bổ sung cấu hình SMTP trong Sprint sau. |
| **KISS-02** | Tính năng Quên mật khẩu bị tắt mặc định. | Medium | Biến cấu hình `ResetEmail__Enabled` mặc định `false` để tránh lỗi gửi mail khi chưa có SMTP; API trả `AUTH.RESET_UNAVAILABLE`. | Quản trị viên cấu hình máy chủ SMTP TLS và bật `ResetEmail__Enabled=true`. | Cung cấp giao diện cấu hình SMTP cho Admin. |
| **KISS-03** | Kịch bản seed chưa tạo tài khoản Super Admin. | High | Tệp `seed_enterprise_simulation.sql` đặt `IsPlatformAdministrator = FALSE` cho toàn bộ tài khoản. | Chèn thủ công bản ghi Super Admin hoặc bổ sung kịch bản seed. | [CẦN BỔ SUNG SEED SUPER ADMIN]. |
| **KISS-04** | Nginx Reverse Proxy giới hạn tải file 1MB. | Medium | Tệp `deploy/nginx.conf` chưa cấu hình chỉ thị `client_max_body_size` (mặc định 1MB), trong khi Backend cho phép tới 500MB. | Thêm dòng `client_max_body_size 500M;` vào khối `server` trong `nginx.conf`. | Cập nhật tệp cấu hình container app. |
| **KISS-05** | Quy trình phê duyệt đa cấp động mới hỗ trợ một phần. | Medium | Kiến trúc lưu trữ JSON graph đã sẵn sàng nhưng chưa mở rộng toàn bộ trigger đa cấp trên giao diện. | Sử dụng phê duyệt cấp phòng ban và phê duyệt đơn cấp tại `/approvals` (lọc trạng thái `SUBMITTED`). | Mở rộng giao diện Designer. |
| **KISS-06** | Trợ lý AI phụ thuộc vào OpenAI API ngoài. | Low | Khi môi trường không có internet hoặc thiếu `OpenAI__ApiKey`, hệ thống chạy ở chế độ dự phòng Heuristic. | Hệ thống tự động fallback sang luật heuristic dựa trên danh mục dịch vụ. | Tích hợp mô hình AI cục bộ. |
| **KISS-07** | Một số nhãn giao diện và thông báo lỗi hiển thị tiếng Anh. | Low | Kế thừa từ token assets và mã lỗi tiêu chuẩn của hệ thống backend. | Tra cứu mã lỗi theo tài liệu kỹ thuật. | Song ngữ hóa toàn bộ i18n. |
| **KISS-08** | Chuỗi bí mật mẫu lưu trong `appsettings.Development.json`. | Low | Cấu hình mặc định phục vụ chạy thử nghiệm cục bộ tiện lợi. | Chỉ dùng cho môi trường dev/chấm bài; môi trường production nạp biến qua Secret Manager. | Xóa bỏ tệp development trong image prod. |

### 0.5 Deployment & Rollback Strategy
Quy trình triển khai bản phát hành và các bước phục hồi thảm họa chi tiết:
* **Bước 1: Sao lưu toàn vẹn dữ liệu:** Thực thi sao lưu cơ sở dữ liệu trước khi nâng cấp:
```bash
docker exec -t bizflow-local-postgres-1 pg_dump -U bizflow -d bizflow > backup_pre_release.sql
```
* **Bước 2: Triển khai phát hành:** Khởi chạy các container ứng dụng đóng gói thông qua overlay app:
```bash
docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d --build
```
*Thời gian gián đoạn dự kiến (Downtime): Dưới 3 phút.*

* **Bước 3: Kịch bản Smoke Test kiểm tra sức khỏe phát hành:**
```bash
curl -f http://localhost:5154/health/live
curl -f http://localhost:5154/health/ready
curl -f http://localhost:4200
```

**Chiến lược Rollback khi xảy ra sự cố nghiêm trọng:**

| Kịch bản sự cố | Hành động Rollback | Công cụ / Lệnh thực thi | Thời gian phục hồi |
|:---|:---|:---|:---:|
| **Sự cố mã nguồn App / UI** | Quay về Docker image phiên bản trước | `docker compose -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d` | < 2 phút |
| **Sự cố Migration DB hỏng** | Hạ cấp schema về migration trước | `dotnet ef database update <PreviousMigrationName> --project backend/BizFlow.Infrastructure` | < 3 phút |
| **Sự cố hỏng cấu trúc dữ liệu** | Khôi phục dữ liệu từ tệp pg_dump | `docker exec -i bizflow-local-postgres-1 psql -U bizflow -d bizflow < backup_pre_release.sql` | < 5 phút |

### 0.6 Post-Release Monitoring & Operational Metrics
Các chỉ số vận hành trọng yếu cần theo dõi liên tục sau khi đưa bản phát hành vào hoạt động:

| Chỉ số giám sát | Ngưỡng tiêu chuẩn | Nguồn theo dõi | Hành động ứng phó |
|:---|:---|:---|:---|
| **Endpoint sẵn sàng (/health/ready)** | HTTP 200 OK (< 200ms) | `/health/ready` probe | Nếu trả 503: kiểm tra kết nối DB và migration history drift. |
| **Tỷ lệ lỗi máy chủ (5xx Error Rate)** | < 0.5% tổng request | Serilog / Docker API logs | Kiểm tra log lỗi unhandled exception trong container API. |
| **Hangfire Failed Jobs** | 0 jobs lỗi liên tiếp | Hangfire / DB Job table | Kiểm tra kết nối Redis và thông tin cấu hình máy chủ SMTP. |
| **Tỷ lệ lỗi Trợ lý AI (OpenAI API)** | < 2% tổng prompt | OpenAiTaskAssistance log | Hệ thống tự động fallback sang Heuristic; kiểm tra quota/key OpenAI. |
| **Tải tài nguyên container** | CPU < 80%, RAM < 85% | `docker stats` | Khởi động lại container hoặc nâng ngưỡng cấp phát bộ nhớ Docker. |

---

## I. Deliverable Package

| No. | File | Version | Ngày | Notes |
|:---:|:---|:---:|:---:|:---|
| 1 | `G1-BizFlow_DB_final.sql` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Kịch bản PostgreSQL 18 chứa toàn bộ cấu trúc 39 bản EF Core Migration, các bảng phân vùng, khóa ngoại, chỉ mục và dữ liệu mô phỏng doanh nghiệp (Enterprise Simulation Seed). |
| 2 | `G1-BizFlow_SRS_final.docx` [CẦN XÁC NHẬN MÃ NHÓM] | v1.1 | 15/11/2026 | Tài liệu đặc tả yêu cầu phần mềm cuối cùng bao gồm 16 Use Cases (UC-001 đến UC-016), các module yêu cầu chức năng (FR-AUTH, FR-TEN, FR-TASK, FR-REQ, FR-SLA, FR-AI, FR-AUD) và phi chức năng. |
| 3 | `G1-BizFlow_SDS_final.docx` [CẦN XÁC NHẬN MÃ NHÓM] | v1.1 | 15/11/2026 | Tài liệu thiết kế hệ thống phần mềm chi tiết về Clean Architecture, lược đồ quan hệ thực thể ERD, sơ đồ tuần tự, đặc tả máy trạng thái Task/Request và danh mục API endpoint. |
| 4 | `G1-BizFlow_Final_Product_Backlog.xlsx` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Trạng thái hoàn thành 36 chức năng hệ thống qua 3 lần lặp (Iter1, Iter2, Iter3). Tỷ lệ kiểm toán đạt 108/132 yêu cầu nghiệp vụ (81.8% VERIFIED cốt lõi); 36 màn hình UI đáp ứng điều kiện vận hành MVP. |
| 5 | `G1-BizFlow_Issues_Report.xlsx` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Báo cáo theo dõi danh sách toàn bộ sự cố, lỗi kiểm thử (Defects) trong suốt vòng đời dự án, phân loại độ nghiêm trọng, nguyên nhân gốc rễ, phương án khắc phục và trạng thái nghiệm thu. |
| 6 | `G1-BizFlow_Screen_Flow_Guide.docx` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Tài liệu hướng dẫn luồng màn hình và tiến trình nghiệp vụ chi tiết 33 màn hình, 4 vai trò người dùng và 8 luồng vận hành chính. |
| 7 | `G1-BizFlow_Backend_Source_Code.zip` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Mã nguồn giải pháp Backend hoàn chỉnh xây dựng trên .NET 10, C# 13, Entity Framework Core 10, ASP.NET Core Web API, Hangfire Worker và SignalR Hub. |
| 8 | `G1-BizFlow_Frontend_Source_Code.zip` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Mã nguồn ứng dụng Frontend hiện đại xây dựng trên Angular 22, TypeScript, Tailwind CSS, Standalone Components, Angular Signals và bộ kiểm thử Playwright. |
| 9 | `G1-BizFlow_Deployment_Package.zip` [CẦN XÁC NHẬN MÃ NHÓM] | v1.0.0 | 15/11/2026 | Gói triển khai đóng gói container hóa gồm docker-compose.yml, Dockerfiles, cấu hình Nginx Reverse Proxy, tệp biến môi trường mẫu (.env.example) và kịch bản kiểm tra sẵn sàng. |

*G1-BizFlow là mã nhóm sinh viên và mã dự án (ví dụ: G1-BizFlow đại diện cho nhóm G1 phát triển nền tảng AI-Assisted BizFlow Platform). [CẦN XÁC NHẬN MÃ NHÓM: G1-BizFlow]*

**Other related deliverables:**
* **Tagged source codes:** [https://github.com/TxRyeSayNo/AI-Assisted-BizFlow-Platform/releases/tag/v1.0.0](https://github.com/TxRyeSayNo/AI-Assisted-BizFlow-Platform/releases/tag/v1.0.0) `[CẦN TẠO TAG: git tag -a v1.0.0 -m "Release v1.0.0"]`
* **Demonstration video:** `[CẦN DÁN LINK THẬT: Video demo YouTube 11 ký tự, ví dụ: https://youtu.be/XXXXXXXXXXX]`

---

## II. Installation Guides

### 1. Environment Prerequisites & Hardware Specifications
Trước khi tiến hành cài đặt, máy chủ hoặc máy phát triển cần đáp ứng các yêu cầu phần cứng và phần mềm sau:

| Thành phần | Cấu hình tối thiểu | Cấu hình khuyến nghị |
|:---|:---|:---|
| **Hệ điều hành** | Windows 10/11 x64, macOS 13+, hoặc Ubuntu 22.04 LTS | Windows 11 Pro 64-bit với WSL2 kích hoạt |
| **Phần cứng** | CPU 4 nhân @ 2.0 GHz, 8 GB RAM, 10 GB ổ SSD trống | CPU 8 nhân @ 3.2 GHz, 16 GB+ RAM, NVMe SSD |
| **.NET SDK** | .NET SDK 10.0.100+ | .NET SDK 10.0.401 (khai báo trong `global.json`) |
| **Node.js & npm** | Node.js v22.17.0 LTS / npm 10.8+ | Node.js v22.22.3 LTS & npm 11.6.2 (khai báo trong `frontend/.nvmrc`) |
| **Container Virtualization** | Docker Desktop 4.25+ (Linux containers mode) | Docker Desktop 4.34+ (Linux engine, cấp 4GB+ RAM) |
| **Backing Services** | PostgreSQL 17.x, Redis 7.x, S3-Compatible Storage | PostgreSQL 18.3-alpine, Redis 7-alpine, MinIO S3 |

### 2. Configuration & Environment Variables Reference
Bảng tổng hợp các biến môi trường mà mã nguồn Backend thực sự đọc (khai báo tại `deploy/.env.example`, `Program.cs`, `PasswordResetRegistration.cs`):

| Tên biến môi trường | Bắt buộc | Mục đích sử dụng & Giá trị mặc định | Cơ chế dự phòng (Fallback) |
|:---|:---:|:---|:---|
| `ConnectionStrings__BizFlow` | Có | Chuỗi kết nối PostgreSQL (mặc định cổng 5433 hoặc 5432, DB: `bizflow`, User: `bizflow`). | Không có. Khởi động dừng nếu thiếu. |
| `Jwt__Issuer` | Có | Tên đơn vị phát hành JWT token (mặc định: `BizFlow`). | Không có. Khởi động dừng nếu thiếu. |
| `Jwt__Audience` | Có | Đơn vị tiêu thụ JWT token (mặc định: `BizFlow.Web`). | Không có. Khởi động dừng nếu thiếu. |
| `Jwt__SigningKeyBase64` | Có | Khóa ký HMAC-SHA256 mã hóa Base64 (độ dài tối thiểu 32 byte). | Không có. Khởi động dừng nếu thiếu. |
| `Redis__ConnectionString` | Không | Chuỗi kết nối Redis cache (ví dụ: `localhost:6379`). | Tự động chuyển sang `IMemoryCache` trong bộ nhớ RAM. |
| `Storage__Provider` / `Endpoint` / `Bucket`... | Không | Cấu hình lưu trữ tệp đính kèm và tài liệu MinIO/S3. | Tự động lưu trữ tệp cục bộ trên ổ cứng máy chủ (Local File Storage). |
| `OpenAI__ApiKey` / `Model` / `BaseUrl`... | Không | Khóa API và mô hình OpenAI (mặc định: `gpt-4o-mini`). | Tự động kích hoạt cơ chế dự phòng Heuristic dựa trên danh mục dịch vụ. |
| `ResetEmail__Enabled` / `Host` / `Port`... | Không | Cấu hình gửi email khôi phục mật khẩu qua máy chủ SMTP có TLS. | Mặc định `Enabled=false`, API trả `AUTH.RESET_UNAVAILABLE` nếu chưa bật. |
| `PasswordReset__TokenLifetimeMinutes` | Không | Thời gian hiệu lực của mã token đặt lại mật khẩu (mặc định: 15 phút). | Mặc định sử dụng 15 phút nếu không cấu hình. |

> [!WARNING]
> **[CẢNH BÁO AN NINH / MÔI TRƯỜNG MẪU]** Mọi chuỗi bí mật, mật khẩu, và khóa ký JWT trong tài liệu này là GIÁ TRỊ MẪU DÀNH CHO MÔI TRƯỜNG PHÁT TRIỂN / CHẤM BÀI (DEV/EVALUATION). TUYỆT ĐỐI KHÔNG sử dụng lại các giá trị này trên môi trường Production!

### 3. Infrastructure & Backing Services Deployment
Nền tảng sử dụng Docker Compose để khởi chạy PostgreSQL 18.3-alpine, Redis 7-alpine và MinIO (image `pgsty/silo`).

**Bước 3.1: Sao chép tệp cấu hình môi trường:**
* PowerShell 7:
```powershell
Copy-Item deploy\.env.example deploy\.env
```
* Bash:
```bash
cp deploy/.env.example deploy/.env
```

**Bước 3.2: Khởi chạy các container dịch vụ ở chế độ chạy nền:**
* PowerShell 7 & Bash:
```bash
docker compose --env-file deploy/.env -f deploy/docker-compose.yml up -d
```
Kiểm tra trạng thái container (tên container mặc định là `bizflow-local-postgres-1`, `bizflow-local-redis-1`, `bizflow-local-minio-1`):
```bash
docker compose ps
```

### 4. Backend Service Setup & Execution (.NET 10 Web API)
**Bước 4.1: Khôi phục công cụ và gói thư viện .NET:**
```powershell
dotnet tool restore
dotnet restore BizFlow.slnx
```

**Bước 4.2: Thiết lập biến môi trường kết nối cơ sở dữ liệu và bảo mật:**
* PowerShell 7:
```powershell
$env:ConnectionStrings__BizFlow = 'Host=127.0.0.1;Port=5433;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
$env:Jwt__Issuer = 'BizFlow'
$env:Jwt__Audience = 'BizFlow.Web'
$env:Jwt__SigningKeyBase64 = 'Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='
```
* Bash:
```bash
export ConnectionStrings__BizFlow='Host=127.0.0.1;Port=5433;Database=bizflow;Username=bizflow;Password=replace-with-a-unique-local-secret'
export Jwt__Issuer='BizFlow'
export Jwt__Audience='BizFlow.Web'
export Jwt__SigningKeyBase64='Loizdg9h99sl7lZJSV1LJwNCNTIdi7PjAtR3yib+brk='
```

**Bước 4.3: Áp dụng các bản di chuyển cơ sở dữ liệu (39 migrations):**
```powershell
dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure
```

**Bước 4.4 (BẮT BUỘC VỚI NGƯỜI CHẤM BÀI): Nạp dữ liệu mô phỏng doanh nghiệp:**
* PowerShell 7 & Bash:
```bash
docker compose -f deploy/docker-compose.yml exec -T postgres psql -U bizflow -d bizflow < deploy/seed_enterprise_simulation.sql
```

**Bước 4.5: Khởi chạy dịch vụ Backend Web API:**
```powershell
dotnet run --project backend/BizFlow.Api
```
*Dịch vụ API lắng nghe tại `http://localhost:5154`. Kiểm tra probe sẵn sàng tại `http://localhost:5154/health/ready` và tài liệu OpenAPI tại `http://localhost:5154/openapi/v1.json` (chỉ kích hoạt ở môi trường Development).*

### 5. Frontend Application Setup & Execution (Angular 22 SPA)
**Bước 5.1: Mở cửa sổ dòng lệnh thứ hai, vào thư mục frontend và cài đặt dependencies theo tệp package-lock.json:**
```powershell
cd frontend
npm ci
```

**Bước 5.2: Khởi động máy chủ phát triển Angular:**
```powershell
npm start
```
*Ứng dụng web sẵn sàng tại `http://localhost:4200`. Máy chủ Angular tự động chuyển tiếp (proxy) các request bắt đầu bằng `/api` sang Backend API tại cổng 5154.*

### 6. Automated Test Suite Execution & Quality Metrics
Kết quả kiểm tra tự động hóa thực tế trên mã nguồn:

| Bộ kiểm thử | Lệnh thực thi | Kết quả thực tế | Ghi chú kỹ thuật |
|:---|:---|:---|:---|
| **Backend Unit & Integration Tests** | `dotnet test BizFlow.slnx` | 495/495 tests PASS (theo tài liệu 01/11/2026) | [CẦN CHẠY LẠI: dotnet test BizFlow.slnx để cập nhật số liệu mới nhất] |
| **Frontend Unit Tests (Jasmine/Karma)** | `cd frontend && npm test -- --watch=false` | 268/270 tests PASS (99.2%) | 2 tests lỗi: `users.spec.ts` (cancels pending retrieval when destroyed) và `requests.spec.ts` khi chạy chung suite. |
| **End-to-End Mock Tests (Playwright)** | `cd tests/BizFlow.E2ETests && npm test` | 2/6 tests PASS (33.3%) | 4 tests mock cũ không khớp giao diện tiếng Việt mới. |
| **Source Code Prettier Check** | `cd frontend && npm run format:check` | 89 files chưa chuẩn format | Không ảnh hưởng tới runtime build; các token asset được giữ nguyên bản. |

### 7. Baseline Test Accounts & Access Credentials Matrix
Bảng tài khoản thử nghiệm thực tế được tạo từ tệp `deploy/seed_enterprise_simulation.sql`:

**Mã không gian làm việc (Workspace Code):** `bizflow-corp` | **Mật khẩu chung:** `Password123!`

| Vai trò | Email / Tài khoản | Mã nhân viên | Họ tên & Chức danh | Phòng ban |
|:---|:---|:---:|:---|:---|
| **Company Admin** | `admin@bizflow.local` | `ADMIN001` | Phạm Minh Đức (CEO) | Ban Giám đốc & Điều hành (EXEC) |
| **Ban Giám đốc** | `huong.nguyen@bizflow.local` | `EXEC001` | Nguyễn Thị Mai Hương (COO) | Ban Giám đốc & Điều hành (EXEC) |
| **Department Manager** | `manager@bizflow.local` | `MGR001` | Lê Hoàng Nam (CTO) | Phòng Công nghệ & Chuyển đổi số (TECH) |
| **DevOps Engineer** | `huy.do@bizflow.local` | `TECH002` | Đỗ Quang Huy (DevOps) | Phòng Công nghệ & Chuyển đổi số (TECH) |
| **Employee (Dev)** | `employee@bizflow.local` | `EMP001` | Nguyễn Văn Nhân Viên (Dev) | Phòng Công nghệ & Chuyển đổi số (TECH) |
| **Department Manager** | `dat.nguyen@bizflow.local` | `SALES_MGR` | Nguyễn Tiến Đạt (Trưởng phòng Sales) | Phòng Kinh doanh B2B & Đối tác (SALES) |
| **Employee (Sales)** | `kiet.hoang@bizflow.local` | `SALES001` | Hoàng Tuấn Kiệt (Chuyên viên B2B) | Phòng Kinh doanh B2B & Đối tác (SALES) |
| **Department Manager** | `thao.vu@bizflow.local` | `MKT_MGR` | Vũ Phương Thảo (Trưởng phòng Marketing) | Phòng Tiếp thị & Truyền thông (MKT) |
| **Employee (Content)** | `linh.le@bizflow.local` | `MKT001` | Lê Mỹ Linh (Chuyên viên Content) | Phòng Tiếp thị & Truyền thông (MKT) |

> [!WARNING]
> **[CẢNH BÁO AN NINH / SEED SUPER ADMIN]** Kịch bản `seed_enterprise_simulation.sql` hiện chưa tạo tài khoản Super Admin (toàn bộ người dùng có `IsPlatformAdministrator = FALSE`). Để thử nghiệm mặt phẳng `/platform`, người chấm bài cần chèn thêm bản ghi Super Admin hoặc bổ sung vào seed `[CẦN BỔ SUNG SEED SUPER ADMIN]`.

---

## III. User Manual

### 1. Overview
AI-Assisted BizFlow Platform là nền tảng điều phối công việc và yêu cầu dịch vụ nội bộ đa doanh nghiệp, hỗ trợ tự động hóa luồng chuyển trạng thái, giám sát cam kết thời hạn (SLA) và ứng dụng Trí tuệ Nhân tạo hỗ trợ bóc tách nhiệm vụ với sự phê chuẩn bắt buộc của con người.

#### 1.1 Dual-Plane Architectural Governance
Hệ thống phân tách quyền hạn thành hai mặt phẳng độc lập:
* **Platform Plane (Quản trị Nền tảng):** Dành riêng cho Platform Administrator tại `/platform/companies` và `/platform/audit`. Chịu trách nhiệm thẩm định giấy phép đăng ký kinh doanh của doanh nghiệp mới, cấp phát dữ liệu thuê bao (Tenant Provisioning) và theo dõi nhật ký kiểm toán bất biến toàn cầu.
![Không gian Quản trị Toàn nền tảng](screenshots/08-workspace-superadmin.png)
*Hình 1: Không gian Quản trị Toàn nền tảng (Platform Plane Workspace)*

* **Tenant Plane (Vận hành Doanh nghiệp):** Dành cho các thành viên doanh nghiệp tại `/workspace`. Dữ liệu từng doanh nghiệp hoàn toàn cô lập theo mã TenantId, quản lý sơ đồ tổ chức phòng ban, phân quyền vai trò (RBAC), danh mục dịch vụ, quy trình nghiệp vụ và hồ sơ SLA.
![Không gian Quản trị Doanh nghiệp](screenshots/11-workspace-admin.png)
*Hình 2: Không gian Quản trị Doanh nghiệp (Tenant Plane Workspace)*

#### 1.2 Four Primary User Roles & Responsibility Matrix

| Vai trò (Role) | Phạm vi quản trị | Trách nhiệm chính | Các route truy cập chính |
|:---|:---|:---|:---|
| **Super Admin** | Platform Plane | Thẩm tra đăng ký doanh nghiệp, duyệt cấp phát tenant, giám sát nhật ký kiểm toán nền tảng. | `/platform`, `/platform/companies`, `/platform/audit` |
| **Company Admin** | Tenant Plane | Cấu hình tham số doanh nghiệp, phòng ban, tài khoản người dùng, vai trò, dịch vụ, quy trình và hồ sơ SLA. | `/workspace`, `/settings`, `/settings/organization/*`, `/settings/services`, `/settings/workflows`, `/settings/sla-profiles`, `/audit` |
| **Department Manager** | Department Scope | Khởi tạo nhiệm vụ, phân rã công việc bằng AI, giao việc nhân viên, duyệt yêu cầu dịch vụ, nghiệm thu kết quả, giám sát vi phạm SLA. | `/workspace`, `/tasks`, `/tasks/new`, `/tasks/ai-decomposition`, `/requests`, `/approvals`, `/reports`, `/sla-monitor` |
| **Employee** | Assigned / Own Scope | Gửi yêu cầu dịch vụ, nhận việc được phân công, thực thi và nộp kết quả kèm tệp bằng chứng, tra cứu hồ sơ và nhận thông báo. | `/workspace`, `/tasks`, `/requests`, `/requests/new`, `/notifications` |

#### 1.3 Core State Machine Lifecycles
Hệ thống quản lý vòng đời chặt chẽ qua máy trạng thái tầng Domain. Mức độ ưu tiên áp dụng cho cả Task và Request gồm 4 cấp độ: **Low, Medium, High, Critical**.

**Bảng 1: Ma trận chuyển trạng thái Nhiệm vụ (TaskLifecyclePolicy - 10 trạng thái):**

| Trạng thái hiện tại | Trạng thái tiếp theo | Điều kiện & Bằng chứng (Evidence) | Quyền hạn yêu cầu (Permission) |
|:---|:---|:---|:---|
| **Draft** hoặc **Rejected** | `Assigned` | Bắt buộc có phòng ban hoặc cá nhân nhận việc hợp lệ | `tasks.assign` |
| **Assigned** | `Accepted` | Người nhận việc xác nhận đồng ý tiếp nhận | `tasks.accept` |
| **Assigned** | `Rejected` | Người nhận việc từ chối, bắt buộc nhập lý do từ chối | `tasks.accept` |
| **Accepted** hoặc **Overdue** | `InProgress` | Người nhận việc bắt đầu bấm thực thi công việc | `tasks.execute` |
| **InProgress** | `Submitted` | Bắt buộc có tệp minh chứng kết quả (Submitted Result) | `tasks.submit` |
| **Submitted** | `Confirmed` | Người nghiệm thu phê duyệt kết quả đạt yêu cầu | `tasks.confirm` |
| **Submitted** | `InProgress` | Người nghiệm thu yêu cầu làm lại (Rework), chuyển về thực thi | `tasks.confirm` |
| **Confirmed** | `Completed` | Chuyển trạng thái hoàn thành tự động bởi hệ thống | `System Only` |
| **InProgress** hoặc **Submitted** | `Overdue` | Quá hạn deadline hoặc vi phạm SLA (kích hoạt bởi hệ thống) | `System Only` |
| **Mọi trạng thái** (trừ Completed, Cancelled) | `Cancelled` | Hủy bỏ nhiệm vụ, bắt buộc nhập lý do hủy | `tasks.cancel` |

**Bảng 2: Ma trận chuyển trạng thái Yêu cầu Dịch vụ (RequestLifecyclePolicy - 12 trạng thái):**

| Trạng thái hiện tại | Trạng thái tiếp theo | Điều kiện & Bằng chứng (Evidence) | Quyền hạn yêu cầu (Permission) |
|:---|:---|:---|:---|
| **Draft** | `Submitted` | Người gửi nộp yêu cầu dịch vụ | `requests.submit` |
| **Submitted** | `Routed` | Điều phối yêu cầu về phòng ban xử lý chuyên môn | `requests.route` |
| **Routed** | `Received` | Phòng ban tiếp nhận yêu cầu vào hàng đợi xử lý | `requests.receive` |
| **Received** | `InProgress` | Bắt đầu xử lý yêu cầu dịch vụ | `requests.process` |
| **InProgress** | `WaitingForInformation` | Tạm dừng để yêu cầu người gửi bổ sung thông tin | `requests.process` |
| **WaitingForInformation** | `InProgress` | Tiếp tục xử lý sau khi đã nhận đủ thông tin | `requests.process` |
| **InProgress** | `Resolved` | Chuyên viên nhập giải pháp xử lý và hoàn tất (Resolution) | `requests.resolve` |
| **Resolved** | `Confirmed` | Người gửi xác nhận hài lòng với kết quả xử lý | `requests.confirm` |
| **Resolved** | `InProgress` | Người gửi phản hồi kết quả chưa đạt, yêu cầu xử lý lại | `requests.confirm` |
| **Confirmed** | `Closed` | Hệ thống tự động đóng yêu cầu vĩnh viễn | `System Only` |
| **Submitted** hoặc **Routed** | `Rejected` | Từ chối yêu cầu, bắt buộc nhập lý do từ chối | `requests.reject` |
| **InProgress, Received...** | `Overdue` | Quá thời hạn cam kết SLA xử lý (kích hoạt bởi hệ thống) | `System Only` |
| **Mọi trạng thái** (trừ Closed, Cancelled, Rejected) | `Cancelled` | Hủy bỏ yêu cầu, bắt buộc nhập lý do hủy | `requests.cancel` |

---

### 2. Workflow 1: Đăng ký & Cấp phát Doanh nghiệp (Company Registration & Tenant Provisioning)
* **Mục đích:** Tiếp nhận hồ sơ đăng ký của doanh nghiệp mới, xác thực thông tin pháp lý và cấp phát tự động không gian làm việc độc lập.
* **Tác nhân:** Đại diện Doanh nghiệp (Guest), Super Admin (Platform Owner).
* **Trình tự thao tác chi tiết:**
  1. **Bước 1:** Đại diện doanh nghiệp truy cập cổng đăng ký công khai tại route `/register-company`. Biểu mẫu yêu cầu cung cấp đúng 6 trường pháp lý bắt buộc: Tên công ty (Company Name), Mã số thuế (Tax Code), Mã không gian làm việc mong muốn (Workspace Code), Họ tên người đại diện pháp luật, Số điện thoại liên hệ, và Email nhận thông báo.
![Biểu mẫu Đăng ký Doanh nghiệp](screenshots/03-register-company.png)
*Hình 3: Giao diện Biểu mẫu Đăng ký Doanh nghiệp mới (S-04)*

  2. **Bước 2:** Đại diện doanh nghiệp tải lên tệp Giấy phép Đăng ký Kinh doanh (.pdf hoặc .png, dung lượng tối đa 10MB). Hệ thống gọi API kiểm tra trùng mã workspace real-time với debounce 400ms và tải tài liệu trực tiếp lên bucket MinIO qua Presigned URL an toàn. Nhấn "Gửi đăng ký" `[Trạng thái: Khởi tạo → PendingReview]`.
![Nhập thông tin pháp lý ĐKKD](screenshots/04-register-filled.png)
*Hình 4: Nhập đầy đủ thông tin pháp lý và đính kèm giấy phép ĐKKD (S-04)*

  3. **Bước 3:** Super Admin đăng nhập vào mặt phẳng quản trị toàn nền tảng, điều hướng đến `/platform/companies` để xem danh sách hàng đợi đăng ký. Super Admin mở chi tiết hồ sơ doanh nghiệp và nhấp vào tệp giấy phép kinh doanh để xem tài liệu qua URL tạm thời 15 phút.
![Hàng đợi Duyệt Đăng ký](screenshots/09-platform-companies.png)
*Hình 5: Hàng đợi Duyệt Đăng ký Doanh nghiệp của Super Admin (S-08)*

  4. **Bước 4:** Super Admin thực hiện hành động phê duyệt: mở hộp thoại duyệt, nhập bắt buộc các trường: Tenant Key (chuỗi slug chữ thường, ví dụ `acme-corp`), Mã nhân viên Admin, Email Admin, Họ tên Admin, Mật khẩu Admin ban đầu (tối thiểu 8 ký tự), và Múi giờ IANA (ví dụ `Asia/Ho_Chi_Minh`). Hệ thống thực thi giao dịch nguyên tử: tạo Company, Tenant, tài khoản Admin ban đầu, lịch làm việc và ghi nhận Audit log. *(Lưu ý: Không có email tự động kích hoạt hay mật khẩu ngẫu nhiên được gửi đi; Admin sử dụng thông tin do Super Admin thiết lập để đăng nhập)*. Nếu từ chối, Super Admin bắt buộc nhập lý do từ chối từ 5 đến 1000 ký tự `[Trạng thái: PendingReview → Approved / Rejected]`.
![Nhật ký Kiểm toán Toàn nền tảng](screenshots/10-platform-audit.png)
*Hình 6: Nhật ký Kiểm toán Toàn nền tảng xác nhận cấp phát Tenant thành công (S-09)*

---

### 3. Workflow 2: Xác thực & Quản trị Không gian làm việc (Authentication & Workspace Navigation)
* **Mục đích:** Xác thực người dùng an toàn vào hệ thống, giải quyết trường hợp một tài khoản tham gia nhiều doanh nghiệp và điều hướng về bảng điều khiển phù hợp vai trò.
* **Tác nhân:** Toàn bộ người dùng hệ thống.
* **Trình tự thao tác chi tiết:**
  1. **Bước 1:** Người dùng truy cập trang đăng nhập tại route `/login`. Hệ thống cung cấp 2 phương thức xác thực trên giao diện: (1) Đăng nhập bằng Mã không gian làm việc (Workspace Code) + Tên đăng nhập/Email + Mật khẩu; (2) Đăng nhập bằng tài khoản Google thông qua Firebase Popup (yêu cầu cấu hình Firebase Project).
![Giao diện Đăng nhập Hợp nhất](screenshots/01-login.png)
*Hình 7: Giao diện Đăng nhập Hợp nhất BizFlow (S-01)*

  2. **Bước 2:** Trong trường hợp người dùng đăng nhập bằng Google hoặc Email và địa chỉ email này thuộc về hai hay nhiều doanh nghiệp khác nhau, hộp thoại Workspace Selector (S-03) sẽ tự động hiển thị để người dùng chọn không gian làm việc muốn làm việc. Phiên đăng nhập (JWT token) chỉ được cấp phát sau khi người dùng đã chỉ định chính xác TenantId.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh hộp thoại S-03 Workspace Selector khi tài khoản thuộc nhiều doanh nghiệp] [CẦN CHỤP LẠI]`*

  3. **Bước 3:** Hệ thống điều hướng người dùng về màn hình Không gian làm việc (`/workspace`) được cá nhân hóa theo vai trò:
     * **Trưởng phòng (Department Manager):** Bảng điều khiển hiển thị hiệu suất phòng ban, nhiệm vụ cần phân công, cảnh báo vi phạm SLA và hàng đợi phê duyệt kết quả.
![Không gian làm việc Trưởng phòng](screenshots/22-workspace-manager.png)
*Hình 8: Không gian làm việc Trưởng phòng (Manager Workspace - S-22)*

     * **Nhân viên tác nghiệp (Employee):** Bảng điều khiển tập trung vào danh sách nhiệm vụ được giao riêng (My Assigned Tasks) và danh sách yêu cầu dịch vụ do mình gửi.
![Không gian làm việc Nhân viên](screenshots/34-workspace-employee.png)
*Hình 9: Không gian làm việc Nhân viên tác nghiệp (Employee Workspace - S-34)*

---

### 4. Workflow 3: Cấu hình Doanh nghiệp & Danh mục Dịch vụ (Initial Tenant & Service Configuration)
* **Mục đích:** Thiết lập nền tảng tổ chức ban đầu cho doanh nghiệp gồm tham số chung, phòng ban, người dùng, phân quyền vai trò, dịch vụ nội bộ, quy trình trực quan và cam kết chất lượng SLA.
* **Tác nhân:** Company Admin (Tenant Administrator).
* **Trình tự thao tác chi tiết:**
  1. **Bước 1:** Thiết lập tham số doanh nghiệp tại route `/settings` để cập nhật logo, múi giờ vận hành IANA và chính sách mật khẩu nội bộ.
![Thiết lập Tham số Doanh nghiệp](screenshots/12-settings.png)
*Hình 10: Thiết lập Tham số Doanh nghiệp (Tenant Settings - S-12)*

  2. **Bước 2:** Quản lý cơ cấu phòng ban tại route `/settings/organization/departments` để khởi tạo sơ đồ cây phòng ban, chỉ định Trưởng phòng phụ trách và email nhận thông báo chung.
![Quản lý Cơ cấu Phòng ban](screenshots/13-departments.png)
*Hình 11: Quản lý Cơ cấu Phòng ban Doanh nghiệp (S-13) [Route thật: /settings/organization/departments]*

  3. **Bước 3:** Quản lý tài khoản thành viên tại route `/settings/organization/users` để thêm nhân viên mới, gán vào phòng ban tương ứng và cấp phát vai trò ban đầu.
![Danh bạ & Quản lý Tài khoản](screenshots/14-users.png)
*Hình 12: Danh bạ & Quản lý Tài khoản Nhân viên (S-14) [Route thật: /settings/organization/users]*

  4. **Bước 4:** Cấu hình vai trò và quyền hạn tinh chỉnh tại route `/settings/organization/roles` để thiết lập ma trận quyền hạn granular (`tasks.create`, `tasks.assign`, `requests.route`, `sla.read`...).
![Cấu hình Ma trận Quyền hạn](screenshots/15-roles.png)
*Hình 13: Cấu hình Ma trận Quyền hạn Vai trò (S-15) [Route thật: /settings/organization/roles]*

  5. **Bước 5:** Xây dựng danh mục dịch vụ nội bộ tại route `/settings/services` để tạo các loại yêu cầu phục vụ (cấp thiết bị, nghỉ phép, hỗ trợ phần mềm...), gán phòng ban xử lý và thời hạn cam kết SLA mặc định.
![Danh mục Dịch vụ Nội bộ](screenshots/16-services.png)
*Hình 14: Danh mục Dịch vụ Nội bộ Doanh nghiệp (S-16) [Route thật: /settings/services]*

  6. **Bước 6:** Quản lý quy trình và thiết kế trực quan: Truy cập `/settings/workflows` để xem danh mục quy trình đã ban hành, và `/settings/workflows/:id/versions/:v` để xem/chỉnh sửa phiên bản quy trình trên canvas đồ họa. Quy trình được xuất bản sẽ áp dụng vào điều kiện kiểm soát `WorkflowAllowsTransition`.
![Thư viện Quy trình Doanh nghiệp](screenshots/17-workflows.png)
*Hình 15: Thư viện Quy trình Doanh nghiệp (S-17) [Route thật: /settings/workflows]*
![Trình Thiết kế Quy trình Trực quan](screenshots/18-workflow-designer.png)
*Hình 16: Trình Thiết kế Quy trình Trực quan (S-18) [Route thật: /settings/workflows/:id/versions/:v]*

  7. **Bước 7:** Thiết lập hồ sơ và phiên bản cam kết SLA: Truy cập `/settings/sla-profiles` để cấu hình thời gian phản hồi (Response Target), thời gian xử lý (Resolution Target), lịch làm việc (loại trừ cuối tuần, ngày lễ), và xem lịch sử phiên bản SLA tại `/settings/sla-profiles/:id/versions`.
![Quản lý Hồ sơ Cam kết SLA](screenshots/19-sla-profiles.png)
*Hình 17: Quản lý Hồ sơ Cam kết Thời gian SLA (S-19) [Route thật: /settings/sla-profiles]*
![Lịch sử Phiên bản Cam kết SLA](screenshots/20-sla-versions.png)
*Hình 18: Lịch sử Phiên bản Cam kết SLA (S-20) [Route thật: /settings/sla-profiles/:id/versions]*

---

### 5. Workflow 4: Vòng đời Nhiệm vụ Đầu - Cuối (End-to-End Task Lifecycle Management)
* **Mục đích:** Điều phối toàn bộ quá trình giao việc, phân công, tiếp nhận, thực thi, nộp minh chứng kết quả và nghiệm thu theo máy trạng thái 10 bước.
* **Tác nhân:** Department Manager, Assignee (Employee), Quality Reviewer.
* **Trình tự thao tác chi tiết:**
  1. **Bước 1 (Khởi tạo Nhiệm vụ):** Trưởng phòng truy cập route `/tasks/new` (hoặc `/tasks/create`) để tạo nhiệm vụ mới. Nhập tiêu đề, mô tả, độ ưu tiên (Low, Medium, High, Critical), hạn chót deadline, danh sách đầu việc kiểm tra (Checklist), và chọn Hồ sơ SLA áp dụng `[Trạng thái: Khởi tạo → Draft]`.
![Biểu mẫu Khởi tạo Nhiệm vụ](screenshots/24-task-create.png)
*Hình 19: Biểu mẫu Khởi tạo Nhiệm vụ & Thiết lập Checklist (S-24)*

  2. **Bước 2 (Phân công Nhiệm vụ):** Người quản lý phân công nhiệm vụ cho cá nhân cụ thể hoặc đưa về Hàng đợi chưa nhận của phòng ban tại route `/tasks` `[Trạng thái: Draft → Assigned]`.
![Danh sách Nhiệm vụ Phòng ban](screenshots/23-tasks.png)
*Hình 20: Danh sách Nhiệm vụ Phòng ban với Bộ lọc Phân quyền (S-23)*

  3. **Bước 3 (Tiếp nhận Nhiệm vụ):** Nhân viên kiểm tra danh sách nhiệm vụ được giao riêng tại `/tasks`. Nhân viên mở chi tiết nhiệm vụ `/tasks/:id` và nhấp nút "Tiếp nhận Nhiệm vụ" `[Trạng thái: Assigned → Accepted]`. Nếu không thể nhận việc, nhân viên nhấp "Từ chối" và bắt buộc nhập lý do `[Trạng thái: Assigned → Rejected]`.
![Danh sách Nhiệm vụ Được giao Riêng](screenshots/36-tasks-scoped.png)
*Hình 21: Danh sách Nhiệm vụ Được giao Riêng cho Nhân viên (S-36)*

  4. **Bước 4 (Bắt đầu Thực thi & Ghi nhận Tiến độ):** Nhân viên nhấp nút "Bắt đầu Thực thi" `[Trạng thái: Accepted → InProgress]`. Trong quá trình thực hiện, nhân viên tích chọn các đầu việc hoàn thành trong Checklist, nhập ghi chú trao đổi và đính kèm tài liệu sơ bộ.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác tích checklist và cập nhật tiến độ trong quá trình InProgress] [CẦN CHỤP LẠI]`*

  5. **Bước 5 (Nộp Kết quả Bàn giao & Minh chứng):** Khi công việc hoàn tất, nhân viên nhấp nút "Nộp Kết quả" (Submit Result). Hộp thoại yêu cầu nhập nội dung tóm tắt kết quả bàn giao và bắt buộc tải lên tệp minh chứng (.pdf, .zip dung lượng cho phép tới 500MB theo cấu hình tenant) `[Trạng thái: InProgress → Submitted]`.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh hộp thoại S-13d Submit Result Dialog tải tệp minh chứng kết quả] [CẦN CHỤP LẠI]`*

  6. **Bước 6 (Nghiệm thu & Đóng Nhiệm vụ):** Người quản lý hoặc Người nghiệm thu được chỉ định mở màn hình `/tasks/:id` để thẩm định kết quả và kiểm tra tệp minh chứng: Nếu đạt yêu cầu, nhấp "Nghiệm thu Đạt" `[Trạng thái: Submitted → Confirmed]`, sau đó hệ thống tự động hoàn tất `[Trạng thái: Confirmed → Completed]`. Nếu chưa đạt, nhấp "Yêu cầu Làm lại" (Rework) kèm chỉ dẫn chỉnh sửa bắt buộc; nhiệm vụ chuyển ngược lại `[Trạng thái: Submitted → InProgress]` cho nhân viên xử lý tiếp.
![Không gian Thực thi Nhiệm vụ](screenshots/28-task-detail.png)
*Hình 22: Không gian Thực thi Nhiệm vụ với 10 Trạng thái, Thảo luận & Minh chứng (S-28)*

---

### 6. Workflow 5: Vòng đời Yêu cầu Dịch vụ Đầu - Cuối (End-to-End Service Request Management)
* **Mục đích:** Tiếp nhận các yêu cầu dịch vụ nội bộ từ nhân viên, hỗ trợ phê duyệt đa cấp, điều phối về phòng ban chuyên môn, giải quyết và cho phép người gửi xác nhận kết quả.
* **Tác nhân:** Requester (Nhân viên gửi), Approver (Cấp phê duyệt), Dispatcher/Specialist (Phòng ban thụ lý).
* **Trình tự thao tác chi tiết:**
  1. **Bước 1 (Khởi tạo Yêu cầu Dịch vụ):** Nhân viên truy cập route `/requests/new`, chọn dịch vụ trong Danh mục Dịch vụ Nội bộ, mô tả nhu cầu chi tiết, chọn độ ưu tiên (Low, Medium, High, Critical) và đính kèm tài liệu liên quan `[Trạng thái: Khởi tạo → Submitted]`.
![Biểu mẫu Gửi Yêu cầu Dịch vụ](screenshots/35-request-create.png)
*Hình 23: Biểu mẫu Gửi Yêu cầu Dịch vụ Nội bộ (S-35)*

  2. **Bước 2 (Phê duyệt Yêu cầu):** Yêu cầu ở trạng thái Submitted sẽ xuất hiện trong Hàng đợi Phê duyệt tại route `/approvals`. Cấp quản lý kiểm tra nội dung và thực hiện phê duyệt hoặc từ chối kèm lý do bắt buộc `[Trạng thái: Submitted → Rejected nếu từ chối]`.
![Hàng đợi Phê duyệt Yêu cầu](screenshots/26-approvals.png)
*Hình 24: Hàng đợi Phê duyệt Yêu cầu Đa cấp (S-26) [Hiển thị Task & Request ở trạng thái SUBMITTED]*

  3. **Bước 3 (Điều phối & Tiếp nhận Phòng ban):** Yêu cầu được điều phối về Hàng đợi của Phòng ban xử lý tại route `/requests` `[Trạng thái: Submitted → Routed]`. Điều phối viên phòng ban tiếp nhận yêu cầu `[Trạng thái: Routed → Received]` và phân công chuyên viên xử lý `[Trạng thái: Received → InProgress]`.
![Danh sách Quản lý Yêu cầu](screenshots/27-requests.png)
*Hình 25: Danh sách Quản lý Yêu cầu Dịch vụ Phòng ban (S-27)*

  4. **Bước 4 (Phân nhánh Nhiệm vụ Liên kết - Linked Tasks):** Đối với các yêu cầu kỹ thuật phức tạp, chuyên viên có thể tạo các Nhiệm vụ con liên kết (Linked Tasks) trực tiếp từ màn hình chi tiết yêu cầu để theo dõi tiến độ cụ thể.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác tạo Nhiệm vụ con liên kết (Linked Task) từ màn hình chi tiết Yêu cầu] [CẦN CHỤP LẠI]`*

  5. **Bước 5 (Giải quyết & Xác nhận Hoàn tất):** Sau khi hoàn thành xử lý, chuyên viên nhập nội dung giải pháp (Resolution) và nhấp "Giải quyết Yêu cầu" `[Trạng thái: InProgress → Resolved]`. Người gửi ban đầu nhận thông báo kiểm tra kết quả tại route `/requests/:id`: Nếu hài lòng, người gửi nhấp "Xác nhận Đóng" `[Trạng thái: Resolved → Confirmed]`, hệ thống tự động hoàn tất `[Trạng thái: Confirmed → Closed]`. Nếu chưa đạt, người gửi nhấp "Phản hồi Chưa đạt" kèm lý do; yêu cầu chuyển ngược lại `[Trạng thái: Resolved → InProgress]` để xử lý bổ sung.
![Chi tiết Xử lý Yêu cầu Dịch vụ](screenshots/29-request-detail.png)
*Hình 26: Chi tiết Xử lý Yêu cầu Dịch vụ & Liên kết Nhiệm vụ (S-29)*

---

### 7. Workflow 6: Trợ lý AI & Bóc tách Công việc Tự động (AI Assistance & Task Decomposition)
* **Mục đích:** Tăng tốc độ lập kế hoạch dự án bằng AI thông qua cơ chế kiểm soát con người (Human-in-the-Loop). Mô hình AI chỉ đưa ra đề xuất bản nháp, con người có toàn quyền xem xét và xác nhận trước khi lưu vào cơ sở dữ liệu.
* **Tác nhân:** Department Manager, Company Admin.
* **Trình tự thao tác chi tiết:**
  1. **Bước 1:** Từ màn hình Khởi tạo Nhiệm vụ (`/tasks/new`), người quản lý nhập mục tiêu lớn của dự án và nhấp nút "Bóc tách bằng AI" (Decompose with AI).  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác nhập mục tiêu công việc và bấm nút Decompose with AI tại /tasks/new] [CẦN CHỤP LẠI]`*

  2. **Bước 2:** Hệ thống chuyển hướng sang màn hình Trợ lý AI Bóc tách Công việc tại route `/tasks/ai-decomposition`. Mô hình AI phân tích mục tiêu và trả về danh sách các nhiệm vụ con có cấu trúc: Tiêu đề nhiệm vụ, mô tả chi tiết, thời lượng dự kiến, độ ưu tiên và cây phụ thuộc tuần tự (Directed Acyclic Graph - DAG). Thuật toán kiểm tra chu trình bảo đảm cây phụ thuộc không có vòng lặp kín.
![Trợ lý AI Đề xuất Bóc tách Công việc](screenshots/25-ai-decomposition.png)
*Hình 27: Giao diện Trợ lý AI Đề xuất Bóc tách Công việc & Cây Phụ thuộc DAG (S-25)*

  3. **Bước 3 (Cơ chế Human-in-the-Loop):** Endpoint preview thuần túy tính toán và trả về DTO xem trước, tuyệt đối không ghi bản ghi nào vào DB. Người quản lý có toàn quyền chỉnh sửa trực tiếp nội dung từng đề xuất, thêm mới, xóa bớt hoặc bỏ chọn các nhiệm vụ không cần thiết.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thao tác người quản lý chỉnh sửa inline trực tiếp trên danh sách đề xuất của AI] [CẦN CHỤP LẠI]`*

  4. **Bước 4:** Nhấp nút "Xác nhận & Tạo Hàng loạt Nhiệm vụ". Hệ thống kiểm tra quyền hạn và thực thi lệnh khởi tạo đồng loạt các nhiệm vụ hợp lệ vào cơ sở dữ liệu, tự động thiết lập liên kết cha - con và ràng buộc phụ thuộc thực thi.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh thông báo xác nhận tạo thành công hàng loạt nhiệm vụ sau khi bấm confirm] [CẦN CHỤP LẠI]`*

  5. **Bước 5 (Trợ lý AI Chat Sidebar):** Trong màn hình Chi tiết Nhiệm vụ và Chi tiết Yêu cầu, người dùng có thể mở widget BizFlow AI Agent để tra cứu nhanh rủi ro hạn chót, yêu cầu AI tóm tắt nội dung trao đổi hoặc gợi ý phân công công việc tối ưu.  
  *`[CHƯA CÓ ẢNH CHỤP: Cần chụp ảnh widget BizFlow AI Agent góc dưới màn hình khi đang tương tác trả lời câu hỏi] [CẦN CHỤP LẠI]`*

---

### 8. Workflow 7: Giám sát Cam kết SLA & Báo cáo Vận hành (SLA Monitoring & Operational Reports)
* **Mục đích:** Cung cấp công cụ giám sát cam kết dịch vụ theo thời gian thực, phát hiện điểm nghẽn vận hành, phân tích hiệu suất và truy vết kiểm toán bất biến.
* **Tác nhân:** Company Admin, Department Manager, Ban Giám đốc.
* **Trình tự thao tác chi tiết:**
  1. **Bước 1 (Giám sát SLA Thời gian thực):** Truy cập route `/sla-monitor` để theo dõi toàn bộ các nhiệm vụ và yêu cầu đang hoạt động. Trực quan hóa 3 mức cảnh báo: Màu xanh (An toàn, < 75% thời hạn), Màu vàng (Cảnh báo sớm, ≥ 75% thời hạn), và Màu đỏ (Vi phạm SLA, > 100% thời hạn). Quản lý có thể can thiệp kích hoạt leo thang xử lý khẩn cấp.
![Bảng Giám sát Vi phạm SLA](screenshots/31-sla-monitor.png)
*Hình 28: Bảng Giám sát Vi phạm SLA Thời gian thực (S-31) [Route thật: /sla-monitor]*

  2. **Bước 2 (Báo cáo Quản trị Hiệu suất):** Truy cập route `/reports` để theo dõi biểu đồ phân tích năng suất phòng ban, tỷ lệ hoàn thành đúng hạn (On-time Delivery Rate), thời gian xử lý trung bình và phân bổ tải công việc.
![Báo cáo Quản trị Hiệu suất](screenshots/30-reports-admin.png)
*Hình 29: Báo cáo Quản trị Hiệu suất & Chỉ số Vận hành Doanh nghiệp (S-30) [Route thật: /reports]*

  3. **Bước 3 (Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung):** Truy cập route `/records/search` để tra cứu nhanh toàn bộ hồ sơ nghiệp vụ, tài liệu đính kèm và kết quả thực thi cũ thông qua bộ lọc đa tiêu chí.
![Tra cứu Lịch sử & Lưu trữ Hồ sơ](screenshots/32-record-search.png)
*Hình 30: Tra cứu Lịch sử & Lưu trữ Hồ sơ Tập trung (S-32) [Route thật: /records/search]*

  4. **Bước 4 (Nhật ký Kiểm toán Doanh nghiệp):** Truy cập route `/audit` để xem sổ lưu vết bất biến toàn bộ các thao tác nghiệp vụ trong doanh nghiệp (thời điểm, người thao tác, hành động, giá trị cũ, giá trị mới).
![Nhật ký Kiểm toán Doanh nghiệp](screenshots/21-audit-tenant.png)
*Hình 31: Nhật ký Kiểm toán Doanh nghiệp Bất biến (Tenant Audit Trail - S-21) [Route thật: /audit]*

  5. **Bước 5 (Trung tâm Thông báo Sự kiện):** Truy cập route `/notifications` để theo dõi các thông báo nhắc hạn deadline, cập nhật trạng thái nhiệm vụ và yêu cầu mới nhất.
![Trung tâm Thông báo Sự kiện](screenshots/33-notifications.png)
*Hình 32: Trung tâm Thông báo Sự kiện Hàng ngày (S-33) [Route thật: /notifications]*

---

### 9. Workflow 8: Khôi phục Mật khẩu & Thu hồi Phiên (Password Recovery & Session Invalidation)
* **Mục đích:** Khôi phục quyền truy cập tài khoản thông qua tiến trình gửi email ngầm bất đồng bộ (Hangfire Worker) và tự động thu hồi toàn bộ phiên làm việc cũ khi đổi mật khẩu để bảo đảm an ninh.
* **Tác nhân:** Toàn bộ người dùng đã đăng ký email trong hệ thống.
* **Lưu ý:** Tính năng này bị TẮT mặc định trong cấu hình (`ResetEmail__Enabled=false`, API trả `AUTH.RESET_UNAVAILABLE`) cho đến khi người vận hành cấu hình máy chủ SMTP hỗ trợ TLS.
* **Trình tự thao tác chi tiết:**
  1. **Bước 1 (Yêu cầu Khôi phục Mật khẩu):** Tại màn hình đăng nhập, người dùng nhấp liên kết "Quên mật khẩu?" để chuyển đến route `/forgot-password`. Người dùng nhập địa chỉ email đã đăng ký và nhấn "Gửi liên kết khôi phục".
![Yêu cầu Khôi phục Mật khẩu](screenshots/05-forgot-password.png)
*Hình 33: Màn hình Yêu cầu Khôi phục Mật khẩu (S-05) [Route thật: /forgot-password]*

  2. **Bước 2 (Xử lý Bất đồng bộ qua Hangfire Worker):** Hệ thống sinh mã Token đặt lại mật khẩu có chữ ký bảo mật và giới hạn thời gian hiệu lực (15 phút). Tiến trình Hangfire Worker chạy nền kết nối máy chủ SMTP và gửi thư chứa liên kết khôi phục duy nhất tới hòm thư người dùng.

  3. **Bước 3 (Thiết lập Mật khẩu Mới):** Người dùng nhấp vào liên kết trong email để mở màn hình `/reset-password?token=....` Nhập mật khẩu mới thỏa mãn chính sách an toàn (tối thiểu 12 ký tự, chứa ít nhất 4 ký tự khác nhau) và xác nhận lại mật khẩu.
![Thiết lập Mật khẩu Mới](screenshots/06-reset-password.png)
*Hình 34: Thiết lập Mật khẩu Mới từ Mã Token An toàn (S-06) [Route thật: /reset-password]*

  4. **Bước 4 (Thu hồi Phiên làm việc Cũ & Đăng nhập Lại):** Sau khi đổi mật khẩu thành công, hệ thống tự động thu hồi toàn bộ các Refresh Token và phiên làm việc trước đó của tài khoản trên mọi thiết bị để ngăn ngừa rủi ro bảo mật, sau đó chuyển hướng người dùng về trang `/login` để đăng nhập bằng mật khẩu mới.
"""

with open(md_path, "w", encoding="utf-8") as f:
    f.write(md_content)

print(f"Markdown written to {md_path}, size: {os.path.getsize(md_path)} bytes")
