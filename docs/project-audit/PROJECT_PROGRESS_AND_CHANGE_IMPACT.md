# Báo cáo Tiến độ Dự án và Đánh giá Tác động Thay đổi
## (Project Progress and Change Impact Assessment Report)

- **Mã tài liệu:** `BIZFLOW-AUDIT-PROGRESS-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Ngày lập:** `2026-10-21`
- **Nhóm đánh giá:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Master Prompt, Senior BA, Senior Requirements Engineer, Senior Software Architect, Senior Angular Developer, Senior ASP.NET Core Developer, Senior Database Engineer, AI Integration Architect, Application Security Engineer, QA Engineer, Technical Documentation Engineer)
- **Môi trường đánh giá:** Antigravity IDE, Workspace `d:\FA26\AI-Assisted-BizFlow-Platform`, Git branch `develop`.
- **Tài liệu đối chiếu:**
  - `docs/requirements/rds/` (`RDS_v1.0_DRAFT.md` v1.2, `REQUIREMENTS_CATALOG.csv`, `USE_CASES.md`, `OPEN_DECISIONS.md`, `REQUIREMENTS_TRACEABILITY.md`)
  - `docs/requirements/srs/` (`SRS_v1.0_DRAFT.md` v1.2, `REQUIREMENTS_CATALOG.csv`, `REQUIREMENTS_TRACEABILITY.md`, `OPEN_DECISIONS.md`)
  - `docs/specifications/sss/` (`SSS_v1.0_DRAFT.md` v1.2, `MODULE_CATALOG.md`, `INTERFACE_CATALOG.md`, `ARCHITECTURE_DECISIONS.md`, `diagrams/04-sequence-workflows.md`, `SSS_TRACEABILITY.md`)
  - Mã nguồn thực tế tại `backend/`, `frontend/`, `tests/`, `deploy/`.

---

## 1. Tóm tắt Điều hành (Executive Summary)

Dự án **AI-Assisted BizFlow Platform** là nền tảng SaaS B2B đa thuê bao (Multi-Tenant) chuyên biệt cho điều phối dịch vụ nội bộ doanh nghiệp, tích hợp trợ lý AI thông minh (Human-in-the-Loop) và tự động hóa quy trình nghiệp vụ (SLA, Routing, Approval, Multi-Channel Notifications).

Báo cáo này được thực hiện trong khuôn khổ đợt kiểm toán toàn diện và cập nhật hồ sơ đặc tả kỹ thuật phiên bản **v1.2-DRAFT (2026-10-21)**. Mục tiêu cốt lõi:
1. Đánh giá hiện trạng triển khai thực tế của toàn bộ codebase so với baseline trước đó.
2. Đánh giá toàn diện tác động kỹ thuật và nghiệp vụ của **5 nhóm yêu cầu mới** vừa được thống nhất.
3. Cung cấp cơ sở định hướng rõ ràng cho giai đoạn thực thi (Remediation Backlog), đảm bảo tính toàn vẹn kiến trúc và cam kết **tuyệt đối không sửa mã nguồn trong giai đoạn rà soát tài liệu**.

---

## 2. Xác minh Công nghệ & Hiện trạng Codebase Thực tế

Quá trình kiểm tra thực tế (inspection) trên package manifests, project files, cấu hình Docker và thư mục mã nguồn đã xác minh stack công nghệ như sau:

| Thành phần | Khai báo Ban đầu | Thực tế Codebase | Ghi nhận Đánh giá & Sai khác |
|---|---|---|---|
| **Front-end Framework** | Angular 22 | Angular 22.2.0 (`package.json`) | Trùng khớp. Sử dụng standalone components, signals, Angular Material 22, TypeScript 5.9.3. |
| **Back-end Framework** | ASP.NET Core .NET 10 | .NET 10 (Target `net10.0`, SDK 10.0.401) | Trùng khớp. C# 13/14, Entity Framework Core 10.0.0-preview. |
| **Database** | PostgreSQL 18 | PostgreSQL 18.3-alpine (`docker-compose.yml`) | Trùng khớp. 34 EF Core Migrations, 37 bảng quan hệ đã cấu hình Global Query Filters. |
| **Cache / Broker** | Redis | Redis 7.4-alpine (`docker-compose.yml`) | Trùng khớp. Phân vùng key theo `TenantId`, hỗ trợ token blacklist và sliding cache. |
| **Object Storage** | MinIO / S3-compatible | MinIO (`RELEASE.2024-11-07T00-52-28Z`) | Trùng khớp. Đã có client wrapper hỗ trợ chunked upload và presigned URLs. |
| **AI Integration** | OpenAI API | OpenAI API (Client wrapper `OpenAIService`) | Trùng khớp. Đã có mock/live client cho mô hình GPT-4o / GPT-4o-mini. |
| **Kiến trúc** | Modular Monolith | Modular Monolith (Clean Architecture) | Trùng khớp. 17 modules logic phân tầng Domain, Application, Infrastructure, Presentation. |

### Hiện trạng Kiểm thử Tự động Baseline:
- **Backend Tests:** 405/405 Unit & Integration Tests đạt trạng thái **PASS** (100% Green).
- **Frontend Tests:** 205/205 Component & Service Tests đạt trạng thái **PASS** (100% Green).
- **Frontend Production Packaging:** `ng build --configuration production` thành công 0 lỗi.
- **Tính toàn vẹn mã nguồn:** Trong toàn bộ chu kỳ audit tài liệu v1.2, **0 dòng mã nguồn** trong `backend/`, `frontend/`, `tests/` bị thay đổi.

---

## 3. Đánh giá Tác động của 5 Nhóm Yêu cầu Nghiệp vụ Mới

### 3.1. Nhóm A — Trang Đăng nhập Hợp nhất & Đăng nhập Email-Link

#### Mô tả Yêu cầu Nghiệp vụ Mới:
Hệ thống cung cấp một trang đăng nhập duy nhất tại đường dẫn `/login` (`UI-01`) hỗ trợ hai phương thức độc lập:
1. **Đăng nhập bằng ID (Option 1):** Bắt buộc nhập đủ 3 trường: Mã người dùng (`Username` / `EmployeeCode`), Mật khẩu (`Password`), và Mã Workspace (`TenantKey` / `WorkspaceKey`). Backend xác thực đồng thời cả 3 trường. Thiếu hoặc sai bất kỳ trường nào đều trả về mã lỗi chung `401 AUTH.INVALID_CREDENTIALS` để chống tấn công dò quét tài khoản (Enumeration Attack).
2. **Đăng nhập qua Email-link (Option 2):** Người dùng chỉ cần nhập địa chỉ `Email`. Không cần mật khẩu và không cần Workspace Key trước. Backend phát sinh token mã hóa một lần (`TTL = 15 phút`) gửi qua email. Khi người dùng click link xác thực:
   - Nếu email thuộc 0 Workspace $\rightarrow$ từ chối kèm link chuyển hướng đến trang Đăng ký doanh nghiệp `/register-company`.
   - Nếu email thuộc đúng 1 Workspace $\rightarrow$ đăng nhập trực tiếp vào hệ thống.
   - Nếu email thuộc từ 2 Workspace trở lên $\rightarrow$ hiển thị modal chọn Workspace (`Workspace Selector`, `IR-UI-005`) để người dùng chọn Workspace làm việc trước khi cấp Access Token/Refresh Token.

#### Đánh giá Tác động Kỹ thuật:
- **Frontend (`MOD-FE-001`, `UI-01`):**
  - Cần tái cấu trúc component `login.component.ts` và template HTML: Bổ sung trường bắt buộc `Workspace Key` vào Form 1; Thêm Tab/Switch sang Form 2 (chỉ có trường `Email` và nút "Gửi liên kết đăng nhập").
  - Xây dựng component mới `WorkspaceSelectorDialogComponent` (`IR-UI-005`) hiển thị danh sách workspace khả dụng kèm logo, tên công ty và vai trò của user tại từng workspace.
  - Xây dựng route tiếp nhận callback: `/auth/verify-email-link?token=...`.
- **Backend (`MOD-001` — Authentication & Authorization):**
  - Cập nhật DTO `LoginWithIdRequest`: Bổ sung `WorkspaceKey` (bắt buộc, không cho phép null/empty).
  - Cập nhật logic `AuthenticationController.LoginWithId`: Loại bỏ fallback tự dò Tenant; thực thi thẩm định đồng thời `TenantKey` + `UserAccount` + `PasswordHash`. Cưỡng chế thông báo lỗi vô danh `AUTH.INVALID_CREDENTIALS`.
  - Bổ sung interface `INT-029`:
    - `POST /api/v1/auth/email-link/request`: Tiếp nhận email, kiểm tra tần suất (Rate limit 3 lần/10 phút), tạo token ngẫu nhiên an toàn, lưu Redis hash kèm `TTL = 15 phút`, gửi email thông báo.
    - `POST /api/v1/auth/email-link/verify`: Kiểm tra tính hợp lệ của token, tra cứu toàn bộ `Membership` liên kết với email trên tất cả các `Tenant`.
    - Trả về payload phân nhánh:
      - 0 Tenant: Mã phản hồi `AUTH.NO_WORKSPACE_FOUND`, kèm link `/register-company`.
      - 1 Tenant: Trả về cặp JWT Access/Refresh Token và Workspace Context ngay lập tức.
      - $\ge 2$ Tenants: Trả về `TempExchangeToken` (`TTL = 5 phút`) và mảng `AvailableWorkspaces` (`TenantId`, `WorkspaceCode`, `CompanyName`, `RoleName`).
    - `POST /api/v1/auth/email-link/select-workspace`: Nhận `TempExchangeToken` và `SelectedTenantId`, phát hành JWT Access/Refresh Token chính thức.
- **Cơ sở Dữ liệu & Redis (`DR-016`):**
  - Redis Key: `auth:email-link:{TokenHash}` lưu `{ Email: string, CreatedAt: DateTime, ExpiresAt: DateTime }`.
  - Redis Key: `auth:temp-exchange:{ExchangeTokenHash}` lưu `{ Email: string, AllowedTenantIds: string[] }`.
  - Bảng `UserAccount` cần chỉ mục `CREATE INDEX idx_user_account_email_normalized ON "UserAccounts" (LOWER("Email"));` phục vụ tìm kiếm nhanh đa thuê bao.

---

### 3.2. Nhóm B — Đăng ký Doanh nghiệp Công khai & Tải lên Hồ sơ Pháp lý

#### Mô tả Yêu cầu Nghiệp vụ Mới:
Trang đăng ký doanh nghiệp công khai tại `/register-company` (`IR-UI-004`) tiếp nhận hồ sơ doanh nghiệp mới với 6 trường thông tin bắt buộc:
1. Mã số thuế (`TaxId`)
2. Tên công ty (`CompanyName`)
3. Mã workspace mong muốn (`DesiredWorkspaceCode`)
4. Tên người đại diện / phụ trách (`ContactPersonName`)
5. Email người phụ trách (`ContactEmail`)
6. Số điện thoại người phụ trách (`ContactPhone`)

Hệ thống cung cấp:
- Nút sinh mã workspace ngẫu nhiên (`Random Generator`): tạo tự động mã theo định dạng slug hợp lệ (viết thường, số, dấu gạch ngang, 3-30 ký tự, kết thúc bằng 4 chữ số ngẫu nhiên).
- Kiểm tra tính khả dụng của mã workspace theo thời gian thực (Real-time check) qua API `GET /api/v1/tenants/check-code?code=...` (`INT-030`).
- Kiểm tra lại tính khả dụng tại thời điểm nộp đơn (Submit-time validation) để chống tranh chấp dữ liệu (Race Condition).
- Hỗ trợ tải lên tệp tài liệu pháp lý đính kèm (Giấy phép ĐKKD, điều lệ công ty) lưu trữ trực tiếp vào MinIO bucket `company-documents` (hỗ trợ PDF, PNG, JPG; dung lượng tối đa 10MB/tệp, tổng cộng tối đa 50MB).

#### Đánh giá Tác động Kỹ thuật:
- **Frontend (`MOD-FE-001`, `IR-UI-004`):**
  - Tái cấu trúc form `register-company.ts`: Bổ sung đầy đủ 6 trường bắt buộc, ràng buộc regex chặt chẽ cho `TaxId` (10 hoặc 13 chữ số), `ContactPhone` (10 chữ số), `ContactEmail` (RFC 5322), `DesiredWorkspaceCode` (`^[a-z0-9-]{3,30}$`).
  - Thêm nút "Sinh mã ngẫu nhiên" bên cạnh trường `DesiredWorkspaceCode` (tự động tạo slug từ `CompanyName` + 4 chữ số ngẫu nhiên).
  - Tích hợp directive/debounce 400ms gọi `GET /api/v1/tenants/check-code` để hiển thị icon checkmark xanh / cảnh báo trùng lặp đỏ ngay khi người dùng gõ.
  - Bổ sung component upload tài liệu pháp lý (Drag & Drop, hiển thị tiến độ tải lên, kiểm tra định dạng và dung lượng trước khi submit).
- **Backend (`MOD-002` — Tenancy & Company Registration):**
  - Thêm endpoint `GET /api/v1/tenants/check-code` (`INT-030`): Cho phép gọi nặc danh (Rate limit 60 req/min), kiểm tra trùng lặp trên cả bảng `Tenants` (`TenantCode`) và bảng `CompanyRegistrations` (`DesiredWorkspaceCode` ở trạng thái `PENDING_APPROVAL`).
  - Cập nhật DTO `SubmitCompanyRegistrationRequest` và nghiệp vụ `CompanyRegistrationManagement`: Tiếp nhận đầy đủ 6 trường bắt buộc và danh sách đính kèm `DocumentAttachments`.
  - Tích hợp dịch vụ MinIO (`IMinioStorageService`): Tạo bucket riêng biệt `company-documents` với chính sách Private (Access Denied cho public). Tiếp nhận tải lên và lưu metadata tệp.
- **Cơ sở Dữ liệu (`DR-001`, `DR-016`):**
  - Mở rộng bảng `Company`: Thêm các cột `TaxId` (`VARCHAR(20) NOT NULL`), `ContactPersonName` (`VARCHAR(100) NOT NULL`), `ContactPhone` (`VARCHAR(20) NOT NULL`).
  - Mở rộng bảng `CompanyRegistrations`: Thêm các cột tương ứng và `DesiredWorkspaceCode` (`VARCHAR(50) NOT NULL`).
  - Bổ sung bảng quan hệ `CompanyRegistrationDocument`: `Id`, `RegistrationId`, `FileName`, `StoragePath`, `ContentType`, `FileSizeBytes`, `UploadedAt`, `Sha256Checksum`.

---

### 3.3. Nhóm C — Phê duyệt Đăng ký Doanh nghiệp & Xem Xét Hồ sơ Pháp lý

#### Mô tả Yêu cầu Nghiệp vụ Mới:
Giao diện Quản trị viên Nền tảng (Platform Administrator) tại `/platform/companies` hoặc `/platform/registrations` (`UI-02`, `IR-UI-006`) cung cấp chức năng:
- Xem chi tiết hồ sơ đăng ký doanh nghiệp và xem/tải các tệp tài liệu pháp lý đã tải lên thông qua cơ chế Presigned URL an toàn (thời hạn hiệu lực `TTL = 15 phút`).
- Phê duyệt đăng ký (`Approve`): Thực thi chuỗi cấp phát tài nguyên nguyên tử (Atomic Provisioning Transaction) gồm 5 bước (`BR-023`, `OD-024`):
  1. Kích hoạt thực thể `Company` (chuyển trạng thái sang `ACTIVE`).
  2. Tạo duy nhất 1 Primary Workspace (`Tenant`) gắn liền với `Company` theo quyết định `DEC-WS-001`.
  3. Khởi tạo tài khoản `COMPANY_ADMIN` ban đầu với thông tin từ người phụ trách (`ContactEmail`, `ContactPersonName`).
  4. Cấu hình lịch làm việc mặc định và thiết lập hệ thống ban đầu cho Tenant.
  5. Cập nhật hồ sơ đăng ký sang `APPROVED`, gửi email chào mừng và link kích hoạt tài khoản.
  *Nếu bất kỳ bước nào trong chuỗi thất bại, hệ thống thực hiện Rollback 100% dữ liệu để tránh tình trạng Tenant mồ côi.*
- Từ chối đăng ký (`Reject`): Bắt buộc nhập lý do từ chối rõ ràng (`BR-022`), cập nhật hồ sơ sang `REJECTED` và gửi email thông báo kèm lý do cho người phụ trách.

#### Đánh giá Tác động Kỹ thuật:
- **Frontend (`MOD-FE-001`, `UI-02`):**
  - Cập nhật component `company-registrations.ts`: Bổ sung danh sách tệp đính kèm có thể click xem trước (Preview Modal) hoặc tải về qua Presigned URL; Thêm nút hành động "Phê duyệt" (hiển thị popup xác nhận) và nút "Từ chối" (mở modal bắt buộc nhập lý do từ chối tối thiểu 10 ký tự).
- **Backend (`MOD-002`):**
  - Bổ sung endpoint `GET /api/v1/platform/company-registrations/{id}/documents/{documentId}/download-url`: Sinh MinIO Presigned URL có thời hạn 15 phút, chỉ cho phép `PLATFORM_ADMIN` truy cập.
  - Hoàn thiện nghiệp vụ `CompanyProvisioningStore.ProvisionApprovedCompanyAsync`: Bọc toàn bộ 5 bước cấp phát trong một `NpgsqlTransaction` với mức cô lập `ReadCommitted`. Đảm bảo Rollback toàn diện nếu có lỗi phát sinh.
  - Cập nhật endpoint `POST /api/v1/platform/company-registrations/{id}/reject`: Kiểm tra bắt buộc trường `Reason` (không được để trống), lưu lý do vào trường `RejectionReason` và kích hoạt email thông báo từ chối.
- **Bảo mật & Quyền hạn (`SEC-004`, `SEC-008`):**
  - Enforce chính sách phân quyền: Chỉ tài khoản có Claim `Role == "PLATFORM_ADMIN"` và `TenantId == null` mới có quyền gọi các endpoint duyệt/từ chối và đọc presigned url của tài liệu pháp lý.

---

### 3.4. Nhóm D — Phân rã Công việc bằng AI & Xác nhận Tường minh của Con người

#### Mô tả Yêu cầu Nghiệp vụ Mới:
Người dùng có thể nhập một đoạn văn bản mô tả tự nhiên vào khung chat của Robot hoặc thông qua lệnh `/task <đoạn văn bản>`. AI sẽ phân tích ngữ cảnh và phân rã thành danh sách các công việc con có cấu trúc (gồm: Tiêu đề, Loại việc: Yêu cầu hỗ trợ hoặc Phân công thực hiện, Phòng ban tiếp nhận dự kiến, Mô tả chi tiết).

**Nguyên tắc Cốt lõi — Xác nhận Tường minh của Con người (`BR-028`):**
- AI **tuyệt đối không được tự ý ghi hoặc tạo công việc trực tiếp vào cơ sở dữ liệu** mà không có sự phê duyệt tường minh của người dùng.
- Hệ thống hiển thị kết quả phân rã dưới dạng **Giao diện Danh sách Xem trước Tương tác (Interactive Preview Checklist)**:
  - Cho phép người dùng bật/tắt (check/uncheck) từng công việc con được đề xuất.
  - Cho phép chỉnh sửa trực tiếp nội dung (inline editing) của tiêu đề, mô tả, loại việc và phòng ban tiếp nhận.
  - Nút "Xác nhận tạo việc" (`Confirm Create Tasks`) chỉ tạo những công việc đang được chọn (checked).
- Backend thẩm định quyền hạn người dùng đối với từng phòng ban tiếp nhận được chọn trước khi lưu vào database.

#### Đánh giá Tác động Kỹ thuật:
- **Frontend (`MOD-FE-001`, `MOD-014`):**
  - Xây dựng component giao diện `AiTaskDecompositionPreviewComponent`: Hiển thị danh sách thẻ công việc dạng checklist có checkbox, input inline cho tiêu đề, dropdown chọn phòng ban và loại việc, textarea cho mô tả.
  - Cung cấp nút "Thêm việc mới thủ công", nút "Bỏ chọn tất cả", và nút CTA chính "Xác nhận tạo việc (X việc đã chọn)".
- **Backend (`MOD-014` — AI Task Assistance & `MOD-005` — Task Management):**
  - Bổ sung interface `INT-031`:
    - `POST /api/v1/tasks/ai-decomposition/preview`: Tiếp nhận prompt văn bản thô, gọi OpenAI API với System Prompt quy định cấu trúc JSON đầu ra (Title, TaskType, SuggestedDepartmentId, Description, EstimatedHours). Trả về bản nháp đề xuất cho Frontend hiển thị (chưa ghi DB).
    - `POST /api/v1/tasks/ai-decomposition/confirm`: Tiếp nhận danh sách công việc đã được người dùng chỉnh sửa và xác nhận; thực hiện thẩm định quyền `TASK_CREATE` và tính hợp lệ của `DepartmentId` trong Tenant; ghi đồng thời vào bảng `Tasks` và `ChecklistItems` trong một transaction.
- **Ràng buộc An toàn & Chống ảo giác (`BR-027`, `ADR-SSS-009`):**
  - System prompt của AI phải kèm metadata danh sách phòng ban thực tế của Tenant (`TenantContext`) để AI chỉ đề xuất các phòng ban hiện hữu, tránh đề xuất phòng ban không tồn tại trong hệ thống.

---

### 3.5. Nhóm E — Robot Trợ lý Chatbot Nhúng & Thông báo Đa kênh

#### Mô tả Yêu cầu Nghiệp vụ Mới:
1. **Robot Trợ lý Tương tác Dễ thương (Cute Robot Assistant Widget):**
   - Được nhúng cố định tại góc dưới màn hình giao diện web, mặc định tự động mở rộng (open state) ngay sau khi người dùng đăng nhập thành công.
   - Nút `[X]` nhỏ ở góc trên bên phải của widget: Khi người dùng bấm vào, widget thu nhỏ lại thành biểu tượng robot trôi nổi (floating icon). Trạng thái thu nhỏ này **chỉ duy trì trong phiên làm việc hiện tại của trình duyệt** (`sessionStorage.bizflow_robot_dismissed = true`).
   - Khi người dùng đăng nhập ở phiên làm việc mới hoặc mở tab mới sau khi tắt trình duyệt, widget sẽ **tự động mở lại theo mặc định** (`BR-029`).
   - Hỗ trợ trả lời các câu hỏi ngôn ngữ tự nhiên về công việc, tiến độ, hạn chót, công việc quá hạn, việc chờ xử lý của người dùng. **Phạm vi truy vấn dữ liệu bị giới hạn nghiêm ngặt theo RBAC và ManagementScope của người dùng** (`BR-027`).
   - Hỗ trợ 2 lệnh tắt (Slash Commands): `/task` (kích hoạt luồng phân rã công việc) và `/notifications` (mở bảng cấu hình nhắc việc).
2. **Hệ thống Nhắc Hạn & Thông báo Đa kênh (Multi-Channel Notifications, `BR-030`):**
   - Tự động kích hoạt thông báo nhắc hạn theo các mốc thời gian: Trước 24 giờ, Trước 4 giờ, Trước 1 giờ, và Ngay khi quá hạn.
   - Cơ chế điều phối kênh theo trạng thái người dùng:
     - **Khi người dùng đang Online (Active Web App):** Gửi thông báo In-App qua SignalR (Toast popup tức thời + lưu vào Notification Center).
     - **Khi người dùng Offline nhưng đã cấp quyền thông báo trình duyệt:** Gửi Web Push Notification thông qua Service Worker (W3C Push API / RFC 8292 VAPID).
     - **Khi người dùng Offline và không bật Web Push:** Tự động gửi Email thông báo (HTML Email) kèm nút liên kết sâu (Deep Link) dẫn thẳng vào chi tiết công việc.
   - **Cơ chế Chống trùng lặp (Deduplication Guard, `OD-032`):**
     - Scheduler chạy nền kiểm tra định kỳ (mỗi 1 phút). Sử dụng bảng `NotificationLog` với khóa duy nhất phức hợp `(TenantId, TaskId, UserId, TriggerType, ScheduledAt)` để đảm bảo mỗi mốc nhắc nhở chỉ được gửi đúng một lần duy nhất.
     - Tự động hủy các thông báo nhắc hạn đang chờ nếu công việc đã được hoàn thành trước hạn.

#### Đánh giá Tác động Kỹ thuật:
- **Frontend (`MOD-FE-001`, `MOD-014`, `MOD-015`):**
  - Xây dựng component `RobotChatbotComponent`: Khởi tạo state đọc từ `sessionStorage.getItem('bizflow_robot_dismissed')`. Nếu chưa có, set mặc định mở rộng. Bấm `[X]` set flag vào `sessionStorage`.
  - Tích hợp Angular Service Worker (`@angular/service-worker`): Đăng ký Push Manager, xin quyền người dùng (`Notification.requestPermission()`), gửi `PushSubscription` lên backend qua API `POST /api/v1/notifications/push-subscription`.
  - Bổ sung UI Modal cấu hình nhắc việc mở ra khi gõ `/notifications`.
- **Backend (`MOD-014`, `MOD-015` — Deadline Reminder & Multi-Channel Notification Dispatcher):**
  - Bổ sung interface `INT-032`: Quản lý Push Subscription, cấu hình nhắc việc, và nhận diện trạng thái online của kết nối SignalR (`ConnectionManager`).
  - Xây dựng Background Worker `DeadlineReminderBackgroundService` (`MOD-BG-001`): Quét các task sắp đến hạn theo Tenant calendar, tính toán mốc kích hoạt, kiểm tra `NotificationLog` chống trùng lặp, xác định kênh gửi phù hợp (SignalR $\rightarrow$ Web Push $\rightarrow$ Email Fallback).
  - Tích hợp thư viện `WebPush` C# với cặp khóa VAPID (Public/Private Keys) được cấu hình an toàn trong `appsettings.json` hoặc Environment Variables.

---

## 4. Bảng Đánh giá Tổng thể Tác động Kiến trúc & Invariants

| Nguyên tắc / Invariant | Tình trạng Tuân thủ | Đánh giá Tác động & Biện pháp Đảm bảo |
|---|---|---|
| **Mô hình Đa thuê bao: 1 Company = 1 Primary Tenant (`DEC-WS-001`)** | **TUÂN THỦ TUYỆT ĐỐI** | Cả hai luồng Đăng ký công khai (`FR-TEN-001`) và Duyệt đăng ký (`FR-TEN-002`) đều chỉ khởi tạo duy nhất 1 Tenant ứng với 1 Company. Ràng buộc `UNIQUE("CompanyId")` trong database được giữ nguyên. |
| **Không Thương mại hóa / Billing trong MVP (`DEC-WS-002`)** | **TUÂN THỦ TUYỆT ĐỐI** | Loại bỏ hoàn toàn các gói cước trả phí, checkout hoặc cổng thanh toán. Tất cả các tenant mới tạo đều được gán gói mặc định `TIER_STANDARD` không thu phí. |
| **Cách ly Dữ liệu Đa thuê bao Fail-Closed (`ADR-SSS-001`)** | **TUÂN THỦ TUYỆT ĐỐI** | Tất cả các API mới (Email-link login, AI Decomposition, Multi-channel notifications) đều thực thi trong ngữ cảnh Tenant xác định thông qua `TenantContext`. Ngoại trừ endpoint công khai kiểm tra mã code và tiếp nhận đăng ký. |
| **Quyền tự trị của Con người đối với AI (`BR-028`, `ADR-SSS-009`)** | **TUÂN THỦ TUYỆT ĐỐI** | AI chỉ đóng vai trò đề xuất giao diện xem trước (Preview Checklist). Giao dịch ghi database chỉ xảy ra khi người dùng ấn nút xác nhận rõ ràng. Không có bất kỳ background task nào của AI được quyền ghi dữ liệu tự động. |
| **Kiểm soát Phiên Robot Widget (`BR-029`)** | **TUÂN THỦ TUYỆT ĐỐI** | Sử dụng `sessionStorage` thay vì `localStorage` đảm bảo trạng thái thu nhỏ bị xóa bỏ khi đóng trình duyệt, duy trì đúng yêu cầu widget tự động mở lại ở phiên làm việc kế tiếp. |
| **Chống Trùng lặp Thông báo Đa kênh (`OD-032`, `ADR-SSS-010`)** | **TUÂN THỦ TUYỆT ĐỐI** | Khóa phức hợp duy nhất trong `NotificationLog` cùng với phân cấp kênh thông minh (SignalR $\rightarrow$ Web Push $\rightarrow$ Email) ngăn chặn triệt để tình trạng spam người dùng. |

---

## 5. Kết luận và Kế hoạch Bước tiếp theo

Toàn bộ 5 nhóm yêu cầu nghiệp vụ mới đã được rà soát sâu sắc về mặt kiến trúc, kỹ thuật và dữ liệu. Hồ sơ đặc tả kỹ thuật (RDS, SRS, SSS) đã được đồng bộ hóa hoàn toàn lên phiên bản **v1.2-DRAFT (2026-10-21)**.

Mã nguồn hiện tại của dự án đã sẵn sàng để đón nhận các thay đổi này trong giai đoạn tiếp theo (Phase Implementation) theo đúng lộ trình Remediation Backlog được định nghĩa chi tiết trong tài liệu `IMPLEMENTATION_GAP_ANALYSIS.md`.
