# SRS Review & Validation Gate Report — AI-Assisted BizFlow Platform

| Thuộc tính | Giá trị |
| :--- | :--- |
| **Mã tài liệu** | `BF-SRS-REV-2026-002` |
| **Tên tài liệu** | Báo cáo Rà soát Yêu cầu Phần mềm, Kiểm định Thực tế Triển khai & Gate Review cho Software Requirements Specification (SRS) |
| **Phiên bản** | `1.1-DRAFT` |
| **Trạng thái** | `PASSED WITH OPEN DECISIONS (24 ODs)` — Đã hoàn tất đợt Review chuyên sâu về Company Registration, Tenant Onboarding & Workspace Provisioning |
| **Ngày kiểm định** | `2026-10-19` (Bản gốc `1.0-DRAFT`: `2026-10-18`) |
| **Tài liệu được kiểm định & đồng bộ** | Bộ SRS (`docs/requirements/srs/`), đồng bộ hai chiều với RDS (`docs/requirements/rds/`) và SSS (`docs/specifications/sss/`) |

---

## 1. Mục tiêu Kiểm định & Phạm vi Đợt Rà soát `v1.1-DRAFT` (Review Scope & Objectives)

Báo cáo này ghi nhận kết quả kiểm định độc lập và rà soát kỹ thuật (Independent Software Requirements Quality & Verification Review) cho bộ tài liệu **Software Requirements Specification (SRS)** của dự án **AI-Assisted BizFlow Platform**, cụ thể là **Đợt Rà soát Chuyên sâu (`v1.1-DRAFT`, ngày `2026-10-19`)** tập trung vào **Company Registration, Tenant Onboarding và Workspace Provisioning**.

Quá trình kiểm định đánh giá bộ SRS theo 10 mục tiêu bắt buộc:
1. **Ranh giới Đặc tả Phần mềm (Software Requirements Boundary Enforcement)**: Tuân thủ chuẩn mực ISO/IEC/IEEE 29148:2018; phân định rõ ràng giữa yêu cầu nghiệp vụ (RDS), đặc tả hành vi phần mềm/hợp đồng giao diện/dữ liệu/kiểm thử (SRS) và giải pháp kiến trúc kỹ thuật (SSS).
2. **Kiểm tra 7 Điểm kiểm soát Vòng đời Onboarding (7 Onboarding Lifecycle Checkpoints)**:
   - (1) Điểm vào đăng ký công khai / Landing page (`/register-company`, liên kết CTA từ `/login` `UI-01`).
   - (2) Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Representative Account Registration`).
   - (3) Khởi tạo hồ sơ doanh nghiệp (`Company Creation`).
   - (4) Xác minh địa chỉ email người đại diện (`Email Verification`).
   - (5) Khởi tạo thuê bao (`Tenant Initialization`).
   - (6) Tạo Không gian làm việc chính duy nhất trong MVP (`1 Company = 1 Primary Workspace/Tenant` theo `DEC-WS-001`, không thêm multi-workspace, billing hoặc subscription management theo `DEC-WS-002`).
   - (7) Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Owner/Admin Role Assignment`).
3. **Đối chiếu Tài liệu với Giao diện & Mã nguồn Thực tế (Ground-Truth Codebase Cross-Reference)**: Phân biệt rõ yêu cầu đã xác nhận (`CONFIRMED`), yêu cầu đề xuất (`PROPOSED — NOT APPROVED`), yêu cầu chờ quyết định (`OPEN`), chức năng đã triển khai (`IMPLEMENTED`), triển khai một phần (`PARTIAL`), chưa triển khai (`NOT_IMPLEMENTED`) và chưa được xác minh (`UNVERIFIED`).
4. **Bảo toàn Quyết định Hiện hành & Đề xuất Self-Service Onboarding Trung lập**: Xem **Self-Service Company Registration** (`FR-TEN-005`, `IR-UI-004`, `UC-013`) là phương án đề xuất (`PROPOSED — NOT APPROVED` theo `OD-021`) dựa trên định hướng multi-tenant SaaS, đồng thời giữ nguyên mô hình cơ sở đã xác nhận (`PLATFORM_ADMIN` phê duyệt tại `FR-TEN-001`, `FR-TEN-002`, `UC-001`) và nêu rõ sự khác biệt mà không tự ý ghi đè.
5. **Kiểm tra 6 Tình huống Lỗi & Biên Bắt buộc (6 Mandatory Onboarding Failure & Edge Cases)**:
   - (1) Đăng ký thất bại (`Registration Failure` — lỗi xác thực dữ liệu `422` hoặc lỗi cơ sở dữ liệu `500`).
   - (2) Email hoặc mã doanh nghiệp/`TenantKey` trùng lặp (`Duplicate Email / Company Code / TenantKey` — `409 Conflict`).
   - (3) Email chưa xác minh (`Unverified Email` — chặn kích hoạt workspace và chặn đăng nhập với `403 AUTH.EMAIL_NOT_VERIFIED`).
   - (4) Người dùng đăng ký lại (`User Re-registration` sau khi đơn cũ bị `REJECTED` hoặc token xác minh hết hạn `TTL`, bảo toàn lịch sử theo `BR-021` / `OD-023`).
   - (5) Tạo `Primary Tenant` / dữ liệu cấu hình không hoàn tất (`Incomplete Tenant/Workspace Creation` — bắt buộc `Atomic Rollback` theo `BR-023` / `OD-024`).
   - (6) Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Initial Owner/Admin Role Assignment` — bắt buộc `Atomic Rollback` theo `BR-023` / `OD-024`).
6. **Đồng bộ Truy vết Đa tầng (`RDS` $\leftrightarrow$ `SRS` $\leftrightarrow$ `SSS`) và Bảo toàn Mã nguồn (`0` Source Code Changes)**.

---

## 2. Báo cáo Rà soát Chuyên sâu: Company Registration, Tenant Onboarding & Workspace Provisioning

### 2.1. Đối chiếu 7 Điểm kiểm soát Vòng đời giữa Tài liệu SRS và Hiện trạng Triển khai

| # | Điểm kiểm soát Vòng đời (Lifecycle Checkpoint) | Trạng thái Đặc tả SRS | Trạng thái Mã nguồn (`backend/` & `frontend/`) | Bằng chứng Thực tế trong Repository (`Ground-Truth Evidence`) |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Landing Page / Điểm vào đăng ký công khai (`/register-company` & CTA trên `/login`)** | `PROPOSED — NOT APPROVED` (`IR-UI-004`, `UC-013`, `OD-021`) | **`NOT_IMPLEMENTED`** | Trong [`frontend/src/app/app.routes.ts`](../../../frontend/src/app/app.routes.ts), tuyến đường gốc `''` chuyển hướng về `/login`; hoàn toàn chưa có route `/register-company`. Trong [`frontend/src/app/pages/login/login.html`](../../../frontend/src/app/pages/login/login.html) (`UI-01`), giao diện chỉ có text tĩnh *"Need access? Contact your company administrator."*, chưa có liên kết CTA mở form đăng ký. |
| **2** | **Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Initial Representative`)** | `CONFIRMED` (`FR-TEN-001`) & `PROPOSED` (`FR-TEN-005`) | **`NOT_IMPLEMENTED`** | [`backend/.../Entities/Company.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Domain/Entities/Company.cs) chỉ chứa `Id, Code, Name, ContactEmail, Status, CreatedAt, UpdatedAt`. Chưa có trường lưu tài khoản đại diện ban đầu (`FullName`, `EmployeeCode`, `PasswordHash`) trước khi cấp Tenant. |
| **3** | **Tạo hồ sơ doanh nghiệp (`Company Creation`)** | `CONFIRMED` (`FR-TEN-001`, `DR-002`) | **`PARTIAL` (`GET` only)** | Thực thể `Company` và bảng `companies` đã được cấu hình trong [`IdentityModelConfiguration.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Persistence/Configurations/IdentityModelConfiguration.cs), nhưng [`CompanyRegistrationsController.cs`](../../../backend/src/Api/BizFlow.Api/Controllers/Platform/CompanyRegistrationsController.cs) mới chỉ hỗ trợ `GET /api/v1/platform/company-registrations`. Chưa có endpoint tiếp nhận `POST` đăng ký. |
| **4** | **Xác minh địa chỉ email người đại diện (`Email Verification`)** | `OPEN` / `PROPOSED — NOT APPROVED` (`BR-024`, `FR-TEN-001`, `FR-TEN-005`, `IR-EXT-002`, `OD-022`) | **`NOT_IMPLEMENTED` & `UNVERIFIED`** | Chưa có trường `EmailVerified`, `EmailVerificationTokenHash`, `EmailVerificationExpiresAt` trên `Company`/`UserAccount`. Dịch vụ gửi email SMTP mới chỉ tồn tại ở dạng khung hạ tầng (`IR-EXT-002`), việc gửi thư xác minh thực tế chưa được nối vào luồng đăng ký (`UNVERIFIED`). |
| **5** | **Khởi tạo thuê bao (`Tenant Initialization`)** | `CONFIRMED` (`FR-TEN-002`, `DR-001`, `DR-002`) & `PROPOSED` (`FR-TEN-005`) | **`NOT_IMPLEMENTED` (ở luồng nghiệp vụ)** | Bảng `tenants` và thực thể `Tenant` đã tồn tại trong Persistence, nhưng luồng nghiệp vụ kích hoạt tự động hoặc phê duyệt từ `CompanyRegistrationsController` chưa được hiện thực hóa (mới chỉ là controller tra cứu read-only). |
| **6** | **Tạo Không gian làm việc chính duy nhất trong MVP (`DEC-WS-001`)** | `CONFIRMED` (`DEC-WS-001`, `FR-TEN-002`, `DR-002`, `OD-018`) | **`IMPLEMENTED` (ở tầng CSDL) & `NOT_IMPLEMENTED` (ở tầng Service)** | Trong [`IdentityModelConfiguration.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Persistence/Configurations/IdentityModelConfiguration.cs), cấu hình `b.HasIndex(x => x.CompanyId).IsUnique();` bảo đảm tại tầng CSDL mỗi Company chỉ có đúng 1 Tenant. Tuy nhiên, logic nghiệp vụ provisioning chuỗi 5 bước chưa được cài đặt. |
| **7** | **Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Role Assignment`)** | `CONFIRMED` (`FR-TEN-002`, `DR-004`) & `PROPOSED` (`FR-TEN-005`, `OD-024`) | **`NOT_IMPLEMENTED`** | Seed data đã có sẵn vai trò hệ thống `COMPANY_ADMIN` (`IsSystem = true`), nhưng luồng tự động tạo `UserRole` gắn với `UserAccount` đại diện khi khởi tạo Tenant chưa được viết mã. |

---

### 2.2. Kiểm tra & Xử lý 6 Tình huống Lỗi và Trường hợp Biên (Failure & Edge Cases)

SRS v1.1-DRAFT đã chuẩn hóa đặc tả hành vi của hệ thống cho toàn bộ 6 tình huống lỗi và biên trong `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `BR-023`, `BR-024` và §10 Error Handling:

1. **Đăng ký thất bại (`Registration Failure`):**
   - Lỗi dữ liệu đầu vào không hợp lệ: Trả về mã lỗi HTTP `422 Unprocessable Entity` với mảng chi tiết trường vi phạm (`ValidationProblemDetails`).
   - Lỗi hạ tầng hoặc cơ sở dữ liệu: Trả về mã lỗi `500 Internal Server Error` kèm mã tương quan truy vết (`CorrelationId`). Không có dữ liệu rác nào được lưu lại.
2. **Email hoặc mã doanh nghiệp / `TenantKey` trùng lặp (`Duplicate Conflict`):**
   - Trả về mã lỗi `409 Conflict` với mã lỗi chuẩn `COMPANY.DUPLICATE_CODE_OR_KEY` hoặc `IDENTITY.DUPLICATE_EMAIL`.
   - Hệ thống phản hồi rõ ràng mà không ghi đè dữ liệu của tenant khác và không tiết lộ dữ liệu nhạy cảm của tài khoản đã tồn tại.
3. **Email chưa xác minh (`Unverified Email`):**
   - Khi người dùng đăng nhập bằng tài khoản chưa xác minh email, hệ thống trả về mã lỗi `403 Forbidden` (`AUTH.EMAIL_NOT_VERIFIED`).
   - Chặn hoàn toàn việc truy cập vào Tenant Plane và chặn kích hoạt Primary Tenant cho đến khi token xác minh được xác thực hợp lệ. Hỗ trợ gửi lại email xác minh với rate limit (`3 lần / 15 phút`).
4. **Người dùng đăng ký lại (`User Re-registration`):**
   - Cho phép người dùng đăng ký lại bằng cùng địa chỉ email và mã công ty nếu đơn đăng ký trước đó đã bị `PLATFORM_ADMIN` từ chối (`REJECTED`) hoặc token xác minh email đã quá hạn (`EXPIRED`) sau khi sửa đổi thông tin hợp lệ.
   - Bảo toàn lịch sử các lần từ chối trong `AuditLog` để phục vụ đối soát (`BR-021`, `OD-023`).
5. **Khởi tạo Tenant / Workspace không hoàn tất (`Incomplete Provisioning Rollback`):**
   - Áp dụng nguyên tắc giao dịch ACID Tất-cả-hoặc-Không-gì (`All-or-Nothing Unit of Work` theo `BR-023` và `OD-024`).
   - Nếu việc tạo `Primary Tenant`, khởi tạo `TenantSettings` hoặc seeding `BusinessCalendar` thất bại, toàn bộ giao dịch bị `ROLLBACK 100%`, hồ sơ công ty giữ nguyên trạng thái `PENDING`, mã lỗi `ONBOARDING.PROVISIONING_ROLLED_BACK`.
6. **Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Role Assignment Rollback`):**
   - Điều kiện kiểm tra sau khởi tạo (`Post-Provisioning Invariant Check` theo `BR-023` và `OD-024`): Hệ thống xác minh tồn tại đúng 01 Tenant (ACTIVE) và ít nhất 01 UserAccount (ACTIVE) được gắn vai trò `COMPANY_ADMIN`.
   - Nếu việc gán vai trò `COMPANY_ADMIN` thất bại, hệ thống hủy bỏ toàn bộ giao dịch, ngăn chặn tuyệt đối tình trạng "Workspace mồ côi không người quản trị" (Adminless Workspace).

---

### 2.3. So sánh Hai Mô hình Tiếp nhận Doanh nghiệp: Admin-Approved vs. Self-Service

| Tiêu chí | Mô hình `CONFIRMED` Hiện hành (Admin-Approved) | Mô hình `PROPOSED — NOT APPROVED` (Self-Service Onboarding) |
| :--- | :--- | :--- |
| **Cơ sở xác lập** | `FR-TEN-001`, `FR-TEN-002`, `UC-001`, `SRC-002` | `FR-TEN-005`, `IR-UI-004`, `UC-013`, `OD-021`, `SRC-001` |
| **Quy trình tiếp nhận** | Gửi đơn $\rightarrow$ Chờ duyệt $\rightarrow$ `PLATFORM_ADMIN` duyệt tại `UI-02` $\rightarrow$ Khởi tạo Tenant | Đăng ký trực tuyến $\rightarrow$ Xác minh Email $\rightarrow$ Hệ thống tự động khởi tạo Tenant & cấp quyền |
| **Trạng thái phê duyệt** | `CONFIRMED` — Là baseline chuẩn hiện tại của hệ thống | `PROPOSED — NOT APPROVED` — Cần Stakeholder / PO phê duyệt tại `OD-021` |
| **Thời gian kích hoạt** | Phụ thuộc vào thời gian thao tác thủ công của `PLATFORM_ADMIN` | Tức thời ngay sau khi người dùng bấm liên kết xác minh email |
| **Khuyến nghị kiến trúc** | Giữ nguyên khả năng phê duyệt thủ công trên `UI-02` khi cần kiểm soát chặt | Hỗ trợ cờ cấu hình linh hoạt `Onboarding:ApprovalMode = "SelfService" \| "PlatformAdminApproval"` |

---

## 3. Danh mục Hồ sơ Tài liệu SRS Đã Cập nhật và Bổ sung

| Tên tệp | Vị trí | Tóm tắt Thay đổi trong Đợt Rà soát `v1.1-DRAFT` |
| :--- | :--- | :--- |
| [`SRS_v1.0_DRAFT.md`](./SRS_v1.0_DRAFT.md) | `docs/requirements/srs/` | Nâng cấp toàn diện lên `BIZFLOW-SRS-v1.1` (`v1.1-DRAFT`). Cập nhật Sections 0, 1, 2.6, 2.7, 4.2 (`BR-023`, `BR-024`), 4.3.2 (Sequence Diagram so sánh 2 luồng), 5.0 (`UC-001`, `UC-013`), 5.2 (`FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`), 6.1 (`IR-UI-004`), 6.3 (`IR-EXT-002`), 7 (`DR-002`), 10 (Mã lỗi onboarding), 12 (`TST-004`, `TST-031`), 13 (Thống kê 63 FR, 13 IR, 15 DR, 15 NFR, 12 SEC), 14 (`OD-001`..`OD-024`). |
| [`REQUIREMENTS_CATALOG.csv`](./REQUIREMENTS_CATALOG.csv) | `docs/requirements/srs/` | Cập nhật chính xác `118` mục yêu cầu (63 FR, 13 IR, 15 DR, 15 NFR, 12 SEC). Bổ sung dòng `FR-TEN-005` và `IR-UI-004`; cập nhật trạng thái `PARTIAL` và truy vết cho `IR-EXT-002` và `DR-002`. |
| [`OPEN_DECISIONS.md`](./OPEN_DECISIONS.md) | `docs/requirements/srs/` | Nâng cấp lên `BIZFLOW-SRS-OD-v1.1` (`v1.1-DRAFT`). Bổ sung Câu hỏi Khám phá số 8 và đặc tả chi tiết 6 quyết định mở: `OD-019`, `OD-020`, `OD-021`, `OD-022`, `OD-023`, `OD-024`. |
| [`REQUIREMENTS_TRACEABILITY.md`](./REQUIREMENTS_TRACEABILITY.md) | `docs/requirements/srs/` | Nâng cấp lên `BIZFLOW-SRS-RTM-v1.1` (`v1.1-DRAFT`). Đồng bộ ma trận truy vết hai chiều cho `FR-TEN-005`, `IR-UI-004`, `TST-031`, cập nhật liên kết `BG-006` với `BR-023`, `BR-024`, `UC-013`. |
| [`SRS_REVIEW_REPORT.md`](./SRS_REVIEW_REPORT.md) | `docs/requirements/srs/` | Tạo mới báo cáo rà soát độc lập `BF-SRS-REV-2026-002` ghi nhận bằng chứng đối chiếu mã nguồn, 7 checkpoint, 6 failure cases và quản trị quyết định mở. |

---

## 4. Bảng Tổng hợp Yêu cầu SRS Được Bổ sung và Cập nhật

| Mã Yêu cầu | Loại | Tiêu đề | Trạng thái Đặc tả | Trạng thái Code | Phương pháp & Kịch bản Xác minh | Quyết định Mở Liên quan |
| :--- | :---: | :--- | :---: | :---: | :---: | :---: |
| `FR-TEN-001` | FR | Gửi Hồ sơ Đăng ký Doanh nghiệp Mới và Thông tin Tài khoản Đại diện Ban đầu | `CONFIRMED` | `NOT_IMPLEMENTED` | `T;D` (`TST-004`, `TST-031`) | `OD-009`, `OD-022`, `OD-023` |
| `FR-TEN-002` | FR | Phê duyệt hoặc Từ chối Đăng ký Doanh nghiệp và Khởi tạo Nguyên tử Primary Tenant + COMPANY_ADMIN | `CONFIRMED` | `NOT_IMPLEMENTED` | `T;D` (`TST-004`, `TST-031`) | `OD-005`, `OD-024` |
| `FR-TEN-005` | FR | Tự đăng ký Doanh nghiệp, Xác minh Email và Khởi tạo Tự động Primary Tenant (Self-Service Onboarding) | `PROPOSED — NOT APPROVED` | `NOT_IMPLEMENTED` | `T;D` (`TST-031`) | `OD-021`, `OD-022`, `OD-023`, `OD-024` |
| `IR-UI-004` | IR | Giao diện Điểm Vào Đăng ký Doanh nghiệp Công khai (`/register-company`), Liên kết CTA từ `/login` và Màn hình Xác minh Email | `PROPOSED — NOT APPROVED` | `NOT_IMPLEMENTED` | `T;D` (`TST-031`) | `OD-021`, `OD-022` |
| `IR-EXT-002` | IR | Giao diện Dịch vụ Gửi Thư điện tử Giao dịch (Transactional Email / SMTP Interface) | `CONFIRMED` | `PARTIAL` | `T` (`TST-003`, `TST-031`) | `OD-022` |
| `DR-002` | DR | Dữ liệu Doanh nghiệp, Thuê bao và Thiết lập Cấu hình Cấp Tenant | `CONFIRMED` | `PARTIAL` | `T;I` (`TST-004`, `TST-005`, `TST-031`) | `OD-005`, `OD-018`, `OD-022`, `OD-023`, `OD-024` |
| `TST-031` | TST | Kịch bản Kiểm thử Vòng đời Onboarding, Xác minh Email, Tính Nguyên tử Provisioning và Khả năng Đăng ký Lại | `CONFIRMED` | `NOT_IMPLEMENTED` | `T;D` (Integration & E2E) | `OD-021`..`OD-024` |

---

## 5. Kết luận Kiểm định & Khuyến nghị Hành động

1. **Kết luận Kiểm định (Validation Verdict):**
   - Bộ tài liệu SRS v1.1-DRAFT đã đạt độ chín muồi cao, bao phủ đầy đủ 7 điểm kiểm soát vòng đời Onboarding và 6 kịch bản lỗi/biên.
   - Sự phân định giữa mô hình đã xác nhận (`CONFIRMED` — Admin-Approved) và mô hình đề xuất (`PROPOSED — NOT APPROVED` — Self-Service) được tuân thủ nghiêm ngặt, không tự ý chọn chính sách nghiệp vụ chưa được phê duyệt.
   - Quy tắc 1 Doanh nghiệp = 1 Workspace chính (`DEC-WS-001`) và loại trừ billing/subscription (`DEC-WS-002`) được bảo vệ xuyên suốt từ tầng yêu cầu đến kiểm thử.
2. **Cam kết Bảo toàn Mã nguồn (Codebase Integrity):**
   - **Tuyệt đối không có bất kỳ dòng mã nguồn nào trong `backend/`, `frontend/`, hoặc `tests/` bị chỉnh sửa trong đợt rà soát tài liệu này.**
3. **Các Bước Tiếp theo:**
   - Hoàn tất đồng bộ bộ tài liệu Software System Specification (SSS) tại `docs/specifications/sss/`.
   - Trình duyệt 24 Quyết định Còn Mở (`OD-001` đến `OD-024`) tới Product Owner / Stakeholder để chính thức phê duyệt baseline cho giai đoạn triển khai tiếp theo.
