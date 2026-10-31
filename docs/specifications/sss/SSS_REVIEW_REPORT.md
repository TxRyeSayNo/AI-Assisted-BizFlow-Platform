# SSS Review & Architectural Validation Gate Report — AI-Assisted BizFlow Platform

| Thuộc tính | Giá trị |
| :--- | :--- |
| **Mã tài liệu** | `BF-SSS-REV-2026-002` |
| **Tên tài liệu** | Báo cáo Rà soát Thiết kế Kiến trúc Hệ thống, Kiểm định Hiện trạng Triển khai & Gate Review cho Software System Specification (SSS) |
| **Phiên bản** | `1.1-DRAFT` |
| **Trạng thái** | `PASSED WITH OPEN DECISIONS (24 ODs)` — Đã hoàn tất đợt Review chuyên sâu về Company Registration, Tenant Onboarding & Workspace Provisioning |
| **Ngày kiểm định** | `2026-10-19` (Bản gốc `1.0-DRAFT`: `2026-10-18`) |
| **Tài liệu được kiểm định & đồng bộ** | Bộ SSS (`docs/specifications/sss/`), đồng bộ hai chiều với RDS (`docs/requirements/rds/`) và SRS (`docs/requirements/srs/`) |

---

## 1. Mục tiêu Kiểm định & Phạm vi Đợt Rà soát `v1.1-DRAFT` (Review Scope & Objectives)

Báo cáo này ghi nhận kết quả kiểm định độc lập và rà soát kỹ thuật cấp kiến trúc hệ thống (Independent System Architecture & Technical Specification Review) cho bộ tài liệu **Software System Specification (SSS)** của dự án **AI-Assisted BizFlow Platform**, cụ thể là **Đợt Rà soát Chuyên sâu (`v1.1-DRAFT`, ngày `2026-10-19`)** tập trung vào **Company Registration, Tenant Onboarding và Workspace Provisioning**.

Quá trình kiểm định đánh giá bộ SSS theo 10 mục tiêu bắt buộc:
1. **Ranh giới Đặc tả Kiến trúc Hệ thống (System Specification Boundary Enforcement)**: Phân định ranh giới rõ ràng: RDS định nghĩa nhu cầu kinh doanh & phạm vi; SRS đặc tả hành vi phần mềm & hợp đồng yêu cầu; SSS chịu trách nhiệm đặc tả cấu trúc mô-đun, luồng tương tác tuần tự (`Sequence Diagrams`), thiết kế dữ liệu, cơ chế giao dịch, xử lý lỗi kỹ thuật và các quyết định kiến trúc (`ADR`).
2. **Kiểm tra 7 Điểm kiểm soát Vòng đời Onboarding (7 Onboarding Lifecycle Checkpoints)**:
   - (1) Điểm vào đăng ký công khai / Landing page (`/register-company`, liên kết CTA từ `/login` `UI-01`).
   - (2) Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Representative Account Registration`).
   - (3) Khởi tạo hồ sơ doanh nghiệp (`Company Creation`).
   - (4) Xác minh địa chỉ email người đại diện (`Email Verification`).
   - (5) Khởi tạo thuê bao (`Tenant Initialization`).
   - (6) Tạo Không gian làm việc chính duy nhất trong MVP (`1 Company = 1 Primary Workspace/Tenant` theo `DEC-WS-001`, loại trừ hoàn toàn multi-workspace và billing/subscription management theo `DEC-WS-002`).
   - (7) Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Owner/Admin Role Assignment`).
3. **Đối chiếu Tài liệu với Giao diện & Mã nguồn Thực tế (Ground-Truth Codebase Cross-Reference)**: Phân biệt rõ yêu cầu đã xác nhận (`CONFIRMED`), yêu cầu đề xuất (`PROPOSED — NOT APPROVED`), chức năng đã triển khai (`IMPLEMENTED`), triển khai một phần (`PARTIAL`), chưa triển khai (`NOT_IMPLEMENTED`) và chưa được xác minh (`UNVERIFIED`).
4. **Bảo toàn Quyết định Hiện hành & Đề xuất Self-Service Onboarding Trung lập**: Xem **Self-Service Company Registration** (`FR-TEN-005`, `IR-UI-004`, `UC-013`, `SEQ-007`, `ADR-SSS-007`) là phương án đề xuất (`PROPOSED — NOT APPROVED` theo `OD-021`) dựa trên định hướng multi-tenant SaaS, đồng thời giữ nguyên mô hình cơ sở đã xác nhận (`PLATFORM_ADMIN` phê duyệt tại `FR-TEN-001`, `FR-TEN-002`, `UC-001`) và nêu rõ sự khác biệt mà không tự ý ghi đè.
5. **Kiểm tra & Thiết kế Cơ chế cho 6 Tình huống Lỗi & Biên Bắt buộc (6 Mandatory Failure & Edge Cases)**:
   - (1) Đăng ký thất bại (`Registration Failure` — lỗi xác thực `422` hoặc lỗi CSDL `500`).
   - (2) Email hoặc mã doanh nghiệp/`TenantKey` trùng lặp (`Duplicate Conflict` — `409 Conflict`).
   - (3) Email chưa xác minh (`Unverified Email` — chặn cấp JWT và chặn truy cập Tenant Plane với `403 AUTH.EMAIL_NOT_VERIFIED`).
   - (4) Người dùng đăng ký lại (`User Re-registration` sau khi đơn cũ bị `REJECTED` hoặc token hết hạn, bảo toàn lịch sử kiểm toán theo `BR-021` / `OD-023`).
   - (5) Tạo `Tenant`/`Workspace` không hoàn tất (`Incomplete Provisioning` — bắt buộc `Atomic Rollback` và chốt chặn `Post-Provisioning Invariant Guard` theo `BR-023`, `ADR-SSS-007`, `OD-024`).
   - (6) Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Role Assignment` — bắt buộc `Atomic Rollback` toàn phần theo `BR-023`, `ADR-SSS-007`, `OD-024`).
6. **Đồng bộ Truy vết Đa tầng (`RDS` $\leftrightarrow$ `SRS` $\leftrightarrow$ `SSS`) và Bảo toàn Mã nguồn (`0` Source Code Changes)**.

---

## 2. Báo cáo Rà soát Chuyên sâu: Kiến trúc Tiếp nhận Doanh nghiệp & Khởi tạo Thuê bao

### 2.1. Đối chiếu 7 Điểm kiểm soát Vòng đời giữa Thiết kế SSS và Hiện trạng Triển khai

| # | Điểm kiểm soát Vòng đời (Lifecycle Checkpoint) | Trạng thái Đặc tả SSS | Trạng thái Mã nguồn (`backend/` & `frontend/`) | Bằng chứng Thực tế trong Repository (`Ground-Truth Evidence`) |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Landing Page / Điểm vào đăng ký công khai (`/register-company` & CTA trên `/login`)** | `PROPOSED — NOT APPROVED` (`MOD-FE-001`, `INT-002`, `IR-UI-004`, `OD-021`) | **`NOT_IMPLEMENTED`** | Trong [`frontend/src/app/app.routes.ts`](../../../frontend/src/app/app.routes.ts), tuyến đường gốc `''` chuyển hướng về `/login`; hoàn toàn chưa có route `/register-company` hay `/verify-email`. Trong [`frontend/src/app/pages/login/login.html`](../../../frontend/src/app/pages/login/login.html) (`UI-01`), giao diện chỉ có text tĩnh *"Need access? Contact your company administrator."*, chưa có CTA link mở form đăng ký. |
| **2** | **Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Initial Representative`)** | `CONFIRMED` (`MOD-001`, `MOD-002`, `INT-002`) & `PROPOSED` (`FR-TEN-005`) | **`NOT_IMPLEMENTED`** | [`backend/.../Entities/Company.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Domain/Entities/Company.cs) chỉ chứa `Id, Code, Name, ContactEmail, Status, CreatedAt, UpdatedAt`. Chưa có các cột lưu thông tin tài khoản đại diện ban đầu (`FullName`, `EmployeeCode`, `PasswordHash`) trước khi khởi tạo Tenant. |
| **3** | **Tạo hồ sơ doanh nghiệp (`Company Creation`)** | `CONFIRMED` (`MOD-002`, `INT-002`, `DR-002`) | **`PARTIAL` (`GET` only)** | Thực thể `Company` và cấu hình bảng `companies` đã tồn tại trong [`IdentityModelConfiguration.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Persistence/Configurations/IdentityModelConfiguration.cs), nhưng [`CompanyRegistrationsController.cs`](../../../backend/src/Api/BizFlow.Api/Controllers/Platform/CompanyRegistrationsController.cs) mới chỉ hỗ trợ `GET /api/v1/platform/company-registrations`. Endpoint tiếp nhận `POST` đăng ký công khai chưa được cài đặt. |
| **4** | **Xác minh địa chỉ email người đại diện (`Email Verification`)** | `OPEN` / `PROPOSED — NOT APPROVED` (`MOD-001`, `INT-026`, `ADR-SSS-007`, `OD-022`) | **`NOT_IMPLEMENTED` & `UNVERIFIED`** | Bảng `companies`/`users` chưa có cột `EmailVerified`, `EmailVerificationTokenHash`, `EmailVerificationExpiresAt`. Cơ chế gửi email SMTP mới chỉ tồn tại ở dạng khung dịch vụ (`IR-EXT-002`), việc phát sinh và gửi token xác minh thực tế chưa được nối vào luồng đăng ký (`UNVERIFIED`). |
| **5** | **Khởi tạo thuê bao (`Tenant Initialization`)** | `CONFIRMED` (`MOD-002`, `INT-002`, `INT-015`) & `PROPOSED` (`SEQ-007`, `ADR-SSS-007`) | **`NOT_IMPLEMENTED` (ở luồng nghiệp vụ)** | Bảng `tenants` và thực thể `Tenant` đã sẵn sàng trong Persistence, nhưng luồng nghiệp vụ kích hoạt tự động hoặc thao tác phê duyệt từ `CompanyRegistrationsController` chưa được viết mã (mới chỉ là controller tra cứu read-only). |
| **6** | **Tạo Không gian làm việc chính duy nhất trong MVP (`DEC-WS-001`)** | `CONFIRMED` (`DEC-WS-001`, `MOD-002`, `ADR-SSS-007`) | **`IMPLEMENTED` (ở tầng CSDL) & `NOT_IMPLEMENTED` (ở tầng Service)** | Trong [`IdentityModelConfiguration.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Persistence/Configurations/IdentityModelConfiguration.cs), ràng buộc chỉ mục duy nhất `b.HasIndex(x => x.CompanyId).IsUnique();` bảo đảm tại tầng CSDL mỗi Company chỉ có đúng 1 Tenant. Tuy nhiên, logic điều phối dịch vụ khởi tạo chuỗi 5 bước chưa được hiện thực hóa. |
| **7** | **Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Role Assignment`)** | `CONFIRMED` (`MOD-003`, `INT-004`) & `PROPOSED` (`ADR-SSS-007`, `OD-024`) | **`NOT_IMPLEMENTED`** | Seed data đã có sẵn vai trò hệ thống `COMPANY_ADMIN` (`IsSystem = true`), nhưng luồng tự động tạo `UserRole` gắn với `UserAccount` đại diện khi khởi tạo Tenant chưa được cài đặt. |

---

### 2.2. Thiết kế Kiến trúc Xử lý 6 Tình huống Lỗi và Biên (Failure & Edge Cases Architecture)

SSS v1.1-DRAFT đã đặc tả chi tiết cơ chế kỹ thuật và ranh giới giao dịch tại `ADR-SSS-007`, `SEQ-007`, `MOD-002`, `INT-002`, §15.3 và Phụ lục 24.1:

1. **Đăng ký thất bại (`Registration Failure`):**
   - Lỗi dữ liệu đầu vào không hợp lệ: Pipeline xử lý trả về mã lỗi HTTP `422 Unprocessable Entity` kèm cấu trúc `ValidationProblemDetails`.
   - Lỗi hạ tầng hoặc cơ sở dữ liệu: `ApiExceptionHandler` bắt ngoại lệ, kích hoạt rollback giao dịch và trả về mã lỗi `500 Internal Server Error` kèm `traceId` tương ứng trong log Serilog. Không có bản ghi rác nào được lưu lại.
2. **Email hoặc mã doanh nghiệp / `TenantKey` trùng lặp (`Duplicate Conflict`):**
   - Trả về mã lỗi `409 Conflict` với mã lỗi chuẩn `COMPANY.DUPLICATE_CODE_OR_KEY` hoặc `IDENTITY.DUPLICATE_EMAIL`.
   - Hệ thống từ chối ghi đè dữ liệu của tenant khác và không làm lộ dữ liệu nhạy cảm của tài khoản đã tồn tại (`BR-024`).
3. **Email chưa xác minh (`Unverified Email`):**
   - Khi người dùng đăng nhập bằng tài khoản chưa xác minh email, `MOD-001` từ chối cấp JWT và trả về mã lỗi `403 Forbidden` (`AUTH.EMAIL_NOT_VERIFIED`).
   - Chặn hoàn toàn việc truy cập vào Tenant Plane và chặn kích hoạt Primary Tenant cho đến khi token xác minh được xác thực hợp lệ. Hỗ trợ endpoint gửi lại email xác minh với rate limit (`3 lần / 15 phút`).
4. **Người dùng đăng ký lại (`User Re-registration`):**
   - Cho phép người dùng đăng ký lại bằng cùng địa chỉ email và mã công ty nếu đơn đăng ký trước đó đã bị `PLATFORM_ADMIN` từ chối (`REJECTED`) hoặc token xác minh email đã quá hạn (`EXPIRED`) sau khi sửa đổi thông tin hợp lệ.
   - Bản ghi cũ được đánh dấu trạng thái lịch sử và lưu vết đầy đủ trong `AuditLog` để phục vụ đối soát (`BR-021`, `OD-023`).
5. **Khởi tạo Tenant / Workspace không hoàn tất (`Incomplete Provisioning Rollback`):**
   - Áp dụng nguyên tắc giao dịch ACID Tất-cả-hoặc-Không-gì (`All-or-Nothing Unit of Work` qua `IDbContextTransaction` theo `BR-023`, `ADR-SSS-007` và `OD-024`).
   - Nếu việc tạo `Primary Tenant`, khởi tạo `TenantSettings` hoặc seeding `BusinessCalendar` thất bại, toàn bộ giao dịch bị `ROLLBACK 100%`, hồ sơ công ty giữ nguyên trạng thái `PENDING`, mã lỗi `ONBOARDING.PROVISIONING_ROLLED_BACK`.
6. **Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Role Assignment Rollback`):**
   - Điều kiện kiểm tra sau khởi tạo (`Post-Provisioning Invariant Guard` theo `ADR-SSS-007`): Hệ thống tự động xác minh tồn tại đúng 01 Tenant (ACTIVE) và ít nhất 01 UserAccount (ACTIVE) được gắn vai trò `COMPANY_ADMIN` trước khi commit giao dịch.
   - Nếu việc gán vai trò `COMPANY_ADMIN` thất bại, hệ thống kích hoạt rollback toàn bộ giao dịch, ngăn chặn tuyệt đối tình trạng "Workspace mồ côi không người quản trị" (Adminless Workspace).

---

### 2.3. So sánh Kiến trúc Hai Mô hình Tiếp nhận Doanh nghiệp: Admin-Approved vs. Self-Service

| Tiêu chí Thiết kế | Mô hình `CONFIRMED` Hiện hành (Admin-Approved) | Mô hình `PROPOSED — NOT APPROVED` (Self-Service Onboarding) |
| :--- | :--- | :--- |
| **Cơ sở xác lập** | `FR-TEN-001`, `FR-TEN-002`, `UC-001`, `INT-002` | `FR-TEN-005`, `IR-UI-004`, `UC-013`, `SEQ-007`, `ADR-SSS-007`, `OD-021` |
| **Điều phối Quy trình** | Người dùng gửi đơn $\rightarrow$ Chờ duyệt $\rightarrow$ `PLATFORM_ADMIN` gọi API duyệt qua `UI-02` $\rightarrow$ Khởi tạo Tenant | Đăng ký trực tuyến $\rightarrow$ Xác minh Email $\rightarrow$ Hệ thống tự động kích hoạt Transaction khởi tạo Tenant & cấp quyền |
| **Trạng thái Phê duyệt** | `CONFIRMED` — Là baseline thiết kế hiện tại của hệ thống | `PROPOSED — NOT APPROVED` — Cần Stakeholder / PO phê duyệt tại `OD-021` |
| **Cơ chế Khởi tạo Dữ liệu** | Transaction kích hoạt bởi lệnh `Approve` của Platform Admin | Transaction kích hoạt tự động qua sự kiện `EmailVerified` |
| **Khuyến nghị Kiến trúc** | Giữ nguyên khả năng phê duyệt thủ công trên `UI-02` khi doanh nghiệp yêu cầu kiểm duyệt chặt chẽ | Hỗ trợ cờ cấu hình linh hoạt `Onboarding:ApprovalMode = "SelfService" \| "PlatformAdminApproval"` |

---

## 3. Danh mục Hồ sơ Tài liệu SSS Đã Cập nhật và Bổ sung

| Tên tệp | Vị trí | Tóm tắt Thay đổi trong Đợt Rà soát `v1.1-DRAFT` |
| :--- | :--- | :--- |
| [`ARCHITECTURE_DECISIONS.md`](./ARCHITECTURE_DECISIONS.md) | `docs/specifications/sss/` | Nâng cấp lên `BIZFLOW-ADR-v1.1` (`v1.1-DRAFT`). Bổ sung `ADR-SSS-007` (`Transactional Provisioning & Post-Provisioning Invariant Guard`, `PROPOSED — NOT APPROVED`, liên kết `OD-024`, `BR-023`). |
| [`OPEN_DECISIONS.md`](./OPEN_DECISIONS.md) | `docs/specifications/sss/` | Nâng cấp lên `BIZFLOW-SSS-OD-v1.1` (`v1.1-DRAFT`). Bổ sung Câu hỏi Khám phá số 8 và mở rộng sổ đăng ký lên 24 quyết định mở (`OD-001` đến `OD-024`), đặc biệt là `OD-021`..`024`. |
| [`diagrams/04-sequence-workflows.md`](./diagrams/04-sequence-workflows.md) | `docs/specifications/sss/diagrams/` | Nâng cấp lên `v1.1-DRAFT`. Bổ sung Sơ đồ Tuần tự số 7 (`SEQ-007`: Company Registration, Email Verification & Atomic Provisioning Workflow) mô tả chi tiết luồng rollback và chốt chặn invariant guard. |
| [`MODULE_CATALOG.md`](./MODULE_CATALOG.md) | `docs/specifications/sss/` | Nâng cấp lên `v1.1-DRAFT`. Bổ sung trách nhiệm cho `MOD-001` (kiểm tra email verification `403`), `MOD-002` (vòng đời onboarding, kiểm tra trùng lặp, tái đăng ký, provisioning nguyên tử, invariant guard, rollback), và `MOD-FE-001` (`/register-company`, `/verify-email`). |
| [`INTERFACE_CATALOG.md`](./INTERFACE_CATALOG.md) | `docs/specifications/sss/` | Nâng cấp lên `v1.1-DRAFT`. Cập nhật hợp đồng giao diện `INT-002` (tiếp nhận đăng ký công khai, xác minh email, phê duyệt admin, mã lỗi `409`, `403`, `500`) và `INT-026` (chuyển phát token xác minh). |
| [`SSS_v1.0_DRAFT.md`](./SSS_v1.0_DRAFT.md) | `docs/specifications/sss/` | Nâng cấp toàn diện lên `BIZFLOW-SSS-v1.1` (`v1.1-DRAFT`). Cập nhật Sections 0, 1.2 (118 yêu cầu SRS), 2.2, 2.3, 8 (bảng sequence thêm `SEQ-007`), 15.3 (Atomic Provisioning & Invariant Guard), 21 (ADR-SSS-007), 22.1 (`RSK-007`), 22.2 (24 ODs), 23 (liên kết ma trận), 24.1 (mã lỗi onboarding), 24.2 (route frontend). |
| [`SSS_TRACEABILITY.md`](./SSS_TRACEABILITY.md) | `docs/specifications/sss/` | Nâng cấp lên `SSS-TRC-v1.1` (`v1.1-DRAFT`). Đồng bộ ma trận truy vết hai chiều cho toàn bộ 118 yêu cầu SRS (`63 FR` gồm `FR-TEN-005`, `13 IR` gồm `IR-UI-004`, `15 DR`, `15 NFR`, `12 SEC`), 24 BRs, 7 sequence flows, 21 ADRs, và 31 test suites. |
| [`SSS_REVIEW_REPORT.md`](./SSS_REVIEW_REPORT.md) | `docs/specifications/sss/` | Tạo mới báo cáo rà soát độc lập `BF-SSS-REV-2026-002` ghi nhận bằng chứng đối chiếu mã nguồn, 7 checkpoint, 6 failure cases và quản trị quyết định kiến trúc mở. |

---

## 4. Bảng Tổng hợp Thành phần Kiến trúc SSS Được Bổ sung và Cập nhật

| Mã Thành phần | Loại | Tiêu đề | Trạng thái Thiết kế | Trạng thái Code | Yêu cầu SRS Đáp ứng | Quyết định Mở Liên quan |
| :--- | :---: | :--- | :---: | :---: | :---: | :---: |
| `ADR-SSS-007` | ADR | Khởi tạo Thuê bao và Không gian Làm việc Nguyên tử kèm Chốt chặn Bất biến Hậu Khởi tạo (`Transactional Provisioning & Post-Provisioning Invariant Guard`) | `PROPOSED — NOT APPROVED` | `NOT_IMPLEMENTED` | `FR-TEN-002`, `FR-TEN-005`, `BR-023`, `BR-024` | `OD-024` |
| `SEQ-007` | SEQ | Luồng Đăng ký Doanh nghiệp, Xác minh Email và Khởi tạo Thuê bao Nguyên tử (kèm nhánh Rollback & Invariant Guard) | `PROPOSED — NOT APPROVED` | `NOT_IMPLEMENTED` | `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `IR-UI-004` | `OD-021`..`OD-024` |
| `MOD-001` | MOD | Authentication & Identity Module (bổ sung kiểm tra `EmailVerified` và chặn truy cập Tenant Plane với `403`) | `CONFIRMED` | `PARTIAL` | `FR-AUTH-001..005`, `SEC-AUTH-001` | `OD-022` |
| `MOD-002` | MOD | Tenant & Subscription Governance Module (bổ sung tiếp nhận đăng ký, xử lý trùng lặp, khởi tạo nguyên tử chuỗi 5 bước và rollback) | `CONFIRMED` | `PARTIAL` | `FR-TEN-001..005`, `SEC-TEN-001` | `OD-021`, `OD-023`, `OD-024` |
| `MOD-FE-001` | MOD | Single-Page Application (Angular 19) Shell (bổ sung route `/register-company` và `/verify-email`) | `PROPOSED — NOT APPROVED` | `NOT_IMPLEMENTED` | `IR-UI-001..004` | `OD-021`, `OD-022` |
| `INT-002` | INT | Hợp đồng API Tiếp nhận Đăng ký Doanh nghiệp, Quản trị Thuê bao và Xác minh Email | `CONFIRMED` / `PROPOSED` | `PARTIAL` (`GET` queue only) | `FR-TEN-001..005`, `IR-API-001..002` | `OD-021`..`OD-024` |
| `INT-026` | INT | Giao diện Chuyển phát Thông báo Email Giao dịch Ra Ngoài (Transactional Email / SMTP) | `CONFIRMED` | `PARTIAL` | `IR-NOTIF-002`, `IR-EXT-002` | `OD-005`, `OD-022` |
| `RSK-007` | RISK | Nguy cơ khởi tạo Tenant/Workspace dở dang hoặc cấp quyền Admin ban đầu thất bại dẫn đến Tenant mồ côi | `HIGH` | N/A (Kiến trúc) | `NFR-REL-001`, `BR-023` | `ADR-SSS-007`, `OD-024` |

---

## 5. Kết luận Kiểm định & Khuyến nghị Hành động

1. **Kết luận Kiểm định (Validation Verdict):**
   - Bộ tài liệu SSS v1.1-DRAFT đã đạt độ chín muồi và tính khả thi kiến trúc cao, đặc tả chi tiết ranh giới giao dịch, cơ chế phòng vệ và sơ đồ tuần tự cho toàn bộ 7 điểm kiểm soát vòng đời Onboarding và 6 kịch bản lỗi/biên.
   - Sự phân định giữa mô hình đã xác nhận (`CONFIRMED` — Admin-Approved) và mô hình đề xuất (`PROPOSED — NOT APPROVED` — Self-Service Onboarding) được bảo toàn nhất quán, không tự ý ghi đè quyết định hiện hành của hệ thống.
   - Ràng buộc 1 Doanh nghiệp = 1 Workspace chính (`DEC-WS-001`) và loại trừ billing/subscription (`DEC-WS-002`) được bảo vệ nghiêm ngặt ở cả tầng cơ sở dữ liệu (`b.HasIndex(x => x.CompanyId).IsUnique();`) và tầng dịch vụ.
2. **Cam kết Bảo toàn Mã nguồn (Codebase Integrity):**
   - **Tuyệt đối không có bất kỳ dòng mã nguồn nào trong `backend/`, `frontend/`, hoặc `tests/` bị chỉnh sửa trong đợt rà soát tài liệu này.**
3. **Các Bước Tiếp theo:**
   - Trình duyệt bộ 3 tài liệu đồng bộ hoàn chỉnh (`RDS v1.1`, `SRS v1.1`, `SSS v1.1`) kèm 24 Quyết định Còn Mở (`OD-001` đến `OD-024`) tới Hội đồng Thẩm định Dự án / Product Owner để chính thức phê duyệt baseline trước khi triển khai sprint phát triển tiếp theo.
