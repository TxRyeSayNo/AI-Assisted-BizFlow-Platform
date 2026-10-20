# RDS Review & Validation Gate Report — AI-Assisted BizFlow Platform

| Thuộc tính | Giá trị |
| :--- | :--- |
| **Mã tài liệu** | `BF-RDS-REV-2026-002` |
| **Tên tài liệu** | Báo cáo Rà soát Yêu cầu, Kiểm định Chất lượng & Gate Review cho Requirements Definition Specification (RDS) |
| **Phiên bản** | `1.1-DRAFT` |
| **Trạng thái** | `PASSED WITH OPEN DECISIONS (24 ODs)` — Đã hoàn tất đợt Review chuyên sâu về Company Registration, Tenant Onboarding & Workspace Provisioning |
| **Ngày kiểm định** | `2026-10-19` (Bản gốc `1.0-DRAFT`: `2026-10-18`) |
| **Tài liệu được kiểm định & đồng bộ** | Bộ RDS (`docs/requirements/rds/`), đồng bộ hai chiều với SRS (`docs/requirements/srs/`) và SSS (`docs/specifications/sss/`) |

---

## 1. Mục tiêu Kiểm định & Phạm vi Đợt Rà soát `v1.1-DRAFT` (Review Scope & Objectives)

Báo cáo này ghi nhận kết quả kiểm định độc lập (Independent Requirements Quality & Gate Review) cho bộ tài liệu **Requirements Definition Specification (RDS)** của dự án **AI-Assisted BizFlow Platform**, bao gồm **Đợt Rà soát Yêu cầu Chuyên sâu (`v1.1-DRAFT`, ngày `2026-10-19`)** tập trung vào **Company Registration, Tenant Onboarding và Workspace Provisioning**.

Quá trình kiểm định đánh giá bộ RDS theo 10 mục tiêu bắt buộc:
1. **Ranh giới tài liệu (Document Boundary Enforcement)**: Đảm bảo `RDS = Requirements Definition Specification` tuyệt đối không bị biến thành Relational Database Specification (không chứa SQL DDL, bảng vật lý, index, migration).
2. **Kiểm tra 7 Điểm kiểm soát Vòng đời Onboarding (7 Onboarding Lifecycle Checkpoints)**:
   - (1) Điểm vào đăng ký công khai / Landing page (`/register-company`, liên kết CTA từ `/login` `UI-01`).
   - (2) Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Representative / Initial Owner Account`).
   - (3) Khởi tạo hồ sơ doanh nghiệp (`Company`).
   - (4) Xác minh địa chỉ email người đại diện (`Email Verification`).
   - (5) Khởi tạo thuê bao (`Tenant Initialization`).
   - (6) Tạo Không gian làm việc chính duy nhất trong MVP (`1 Company = 1 Primary Workspace/Tenant` theo `DEC-WS-001`, không thêm multi-workspace, billing hoặc subscription management theo `DEC-WS-002`).
   - (7) Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Owner/Admin Role Assignment`).
3. **Đối chiếu Tài liệu với Giao diện & Mã nguồn Thực tế (Ground-Truth Codebase Cross-Reference)**: Phân biệt rõ yêu cầu đã xác nhận (`CONFIRMED`), yêu cầu đề xuất (`PROPOSED — NOT APPROVED`), yêu cầu chờ quyết định (`OPEN`), chức năng đã triển khai (`IMPLEMENTED`), triển khai một phần (`PARTIAL`), chưa triển khai (`NOT_IMPLEMENTED`) và chưa được xác minh (`UNVERIFIED`).
4. **Bảo toàn Quyết định Hiện hành & Đề xuất Self-Service Onboarding Trung lập**: Xem **Self-Service Company Registration** (`UC-013`, `BIZ-027`, `FR-TEN-005`, `IR-UI-004`) là phương án đề xuất (`PROPOSED — NOT APPROVED` theo `OD-021`) dựa trên định hướng multi-tenant SaaS, đồng thời giữ nguyên mô hình cơ sở đã xác nhận (`PLATFORM_ADMIN` phê duyệt tại `UC-001`, `BIZ-003`, `FR-TEN-001`..`002`) và nêu rõ sự khác biệt mà không tự ý ghi đè.
5. **Kiểm tra 6 Tình huống Lỗi & Biên Bắt buộc (6 Mandatory Onboarding Failure & Edge Cases)**:
   - (1) Đăng ký thất bại (`Registration Failure` — lỗi validate `422` hoặc lỗi hạ tầng `500`).
   - (2) Email hoặc mã doanh nghiệp/`TenantKey` trùng lặp (`Duplicate Email / Company Code / TenantKey` — `409 Conflict`).
   - (3) Email chưa xác minh (`Unverified Email` — chặn kích hoạt workspace và chặn đăng nhập với `403 EMAIL_NOT_VERIFIED`).
   - (4) Người dùng đăng ký lại (`User Re-registration` sau khi đơn cũ bị `REJECTED` hoặc mã xác minh hết hạn `TTL`, bảo toàn lịch sử theo `BR-021` / `OD-023`).
   - (5) Tạo `Primary Tenant` / dữ liệu hạt giống không hoàn tất (`Incomplete Tenant/Workspace Creation` — bắt buộc `Atomic Rollback` theo `BR-023` / `OD-024`).
   - (6) Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Initial Owner/Admin Role Assignment` — bắt buộc `Atomic Rollback` theo `BR-023` / `OD-024`).
6. **Đồng bộ Truy vết Đa tầng (`RDS` $\leftrightarrow$ `SRS` $\leftrightarrow$ `SSS`) và Bảo toàn Mã nguồn (`0` Source Code Changes)**.

---

## 2. Báo cáo Rà soát Chuyên sâu: Company Registration, Tenant Onboarding & Workspace Provisioning (`v1.1-DRAFT`)

### 2.1. Đối chiếu 7 Điểm kiểm soát Vòng đời giữa Tài liệu và Thực tế Triển khai (`Codebase vs. Docs`)

| # | Điểm kiểm soát Vòng đời (Lifecycle Checkpoint) | Trạng thái Đặc tả (RDS / SRS / SSS) | Trạng thái Mã nguồn (`backend/` & `frontend/`) | Bằng chứng Thực tế trong Repository (`Ground-Truth Evidence`) |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Landing Page / Điểm vào đăng ký công khai (`/register-company` & CTA trên `/login`)** | `PROPOSED — NOT APPROVED` (`BIZ-027`, `UC-013`, `IR-UI-004`, `OD-021`) | **`NOT_IMPLEMENTED`** | Trong [`app.routes.ts`](../../../frontend/src/app/app.routes.ts), đường dẫn gốc `''` chuyển hướng thẳng về `/login`; hoàn toàn **chưa có** route `/register` hay `/register-company`. Trong [`login.html`](../../../frontend/src/app/features/auth/login/login.html) (`UI-01`), chân trang chỉ hiển thị văn bản tĩnh *"Need access? Contact your company administrator."* chứ chưa có liên kết CTA mở biểu mẫu đăng ký doanh nghiệp. |
| **2** | **Đăng ký tài khoản người dùng đại diện / chủ sở hữu ban đầu (`Initial Representative`)** | `CONFIRMED` (`BIZ-003`, `UC-001`, `FR-TEN-001`) & `PROPOSED` (`BIZ-027`, `UC-013`, `FR-TEN-005`) | **`NOT_IMPLEMENTED`** | [`Company.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Domain/Entities/Company.cs) mới chỉ có `Id, Code, Name, ContactEmail, Status, CreatedAt, UpdatedAt`, chưa có trường lưu thông tin người đại diện đăng ký (`FullName`, `EmployeeCode`, `PasswordHash`) trước khi tạo `Tenant`. |
| **3** | **Tạo hồ sơ doanh nghiệp (`Company Creation`)** | `CONFIRMED` (`BIZ-003`, `FR-TEN-001`, `DR-002`) | **`PARTIAL` (`GET` only)** | Thực thể `Company` và bảng `companies` đã có (`DR-002`), nhưng [`CompanyRegistrationsController.cs`](../../../backend/src/Api/BizFlow.Api/Controllers/Platform/CompanyRegistrationsController.cs) mới chỉ lập trình duy nhất `GET /api/v1/platform/company-registrations`. Chưa có API `POST` nộp đơn đăng ký mới. |
| **4** | **Xác minh địa chỉ email người đại diện (`Email Verification`)** | `OPEN` / `PROPOSED — NOT APPROVED` (`BR-024`, `BIZ-028`, `FR-TEN-001`, `FR-TEN-005`, `OD-022`) | **`NOT_IMPLEMENTED` & `UNVERIFIED`** | Chưa có cột `EmailVerified`, `EmailVerificationTokenHash`, `EmailVerificationExpiresAt` trên `Company`/`UserAccount`; chưa có endpoint `POST /verify-email` và `POST /resend-verification`. Giao diện gửi email SMTP thực tế cho luồng xác minh đăng ký chưa được xác minh (`UNVERIFIED`). |
| **5** | **Khởi tạo thuê bao (`Tenant Initialization`)** | `CONFIRMED` (`BIZ-003`, `FR-TEN-002`, `DR-002`) & `PROPOSED` (`FR-TEN-005`, `OD-021`) | **`NOT_IMPLEMENTED` (API) / `IMPLEMENTED` (DB Schema)** | Thực thể [`Tenant.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Domain/Entities/Tenant.cs) đã có trong DB và Seed Data (`PlatformSeedData`), nhưng chưa có API động để xét duyệt (`approve`) hoặc tự động khởi tạo `Tenant` từ đơn đăng ký. Màn hình [`company-registrations.ts`](../../../frontend/src/app/features/platform/company-registrations/company-registrations.ts) (`UI-02`) ghi rõ: *"This view does not approve or provision a workspace"*. |
| **6** | **Tạo Không gian làm việc chính duy nhất trong MVP (`1 Company = 1 Primary Workspace/Tenant`)** | `CONFIRMED` (`DEC-WS-001`, `CON-002`, `BR-023`, `DR-002`) | **`IMPLEMENTED` (DB Constraint)** | Trong [`IdentityModelConfiguration.cs`](../../../backend/src/Modules/Identity/BizFlow.Modules.Identity.Infrastructure/Persistence/Configurations/IdentityModelConfiguration.cs), bảng `tenants` đặt ràng buộc `b.HasIndex(x => x.CompanyId).IsUnique()`, chứng minh ở tầng CSDL thực tế mỗi `Company` có đúng **01 `Primary Tenant` duy nhất** trong MVP (`DEC-WS-001`). Multi-workspace, Billing và Subscription Management hoàn toàn nằm ngoài phạm vi (`DEC-WS-002`). |
| **7** | **Cấp quyền `COMPANY_ADMIN` ban đầu (`Initial Owner/Admin Role Assignment`)** | `CONFIRMED` (`BIZ-003`, `BR-023`, `FR-TEN-002`) & `PROPOSED` (`FR-TEN-005`, `OD-024`) | **`NOT_IMPLEMENTED` (Dynamic API)** | Cấu trúc bảng `UserAccount`, `Role(COMPANY_ADMIN)` và `UserRole` đã có trong DB và chạy tốt với dữ liệu seed mẫu, nhưng luồng tự động tạo `UserAccount` + gán `UserRole(COMPANY_ADMIN)` khi kích hoạt doanh nghiệp mới chưa được lập trình. |

### 2.2. Kiểm tra 6 Tình huống Lỗi & Biên trong Quy trình Onboarding (Failure & Edge Cases Audit)

| # | Tình huống Lỗi / Biên (Failure / Edge Case) | Quy tắc & Yêu cầu Đặc tả | Hành vi Hệ thống Bắt buộc trong RDS / SRS / SSS | Quyết định Mở Liên kết |
| :---: | :--- | :--- | :--- | :--- |
| **1** | **Đăng ký thất bại (`Registration Failure`)** | `BR-024`, `BIZ-028`, `UC-001 (E1)`, `UC-013 (E1)`, `FR-TEN-001`, `FR-TEN-005`, `AC-032` | Trả về `422 VALIDATION.FAILED` kèm danh sách lỗi từng trường nếu dữ liệu không hợp lệ; trả về `500` và hoàn tác sạch nếu lỗi DB (không để lại bản ghi `Company` rác). Nếu lỗi dịch vụ gửi mail (`EXT-003`), vẫn lưu đơn `PENDING` và cho phép bấm gửi lại mã xác minh. | `OD-022` |
| **2** | **Email trùng / Mã doanh nghiệp hoặc `TenantKey` trùng (`Duplicate Email / Code / Key`)** | `BR-024`, `BIZ-028`, `UC-001 (E2)`, `UC-013 (E2)`, `FR-TEN-001`, `FR-TEN-005`, `AC-032` | Kiểm tra không phân biệt hoa thường (`case-insensitive`); nếu trùng với bản ghi đang `ACTIVE` hoặc `PENDING` còn hạn thì từ chối ngay với `409 Conflict` (`COMPANY.DUPLICATE_CODE_OR_KEY` / `IDENTITY.DUPLICATE_EMAIL`) và không tạo bản ghi thứ hai. | `OD-023` |
| **3** | **Email chưa xác minh (`Unverified Email`)** | `BR-024`, `BIZ-028`, `UC-001 (E3)`, `UC-013 (E3)`, `FR-TEN-001`, `FR-TEN-005`, `AC-032` | Khi `EmailVerified == false`, chặn kích hoạt `Primary Tenant` (ở cả chế độ tự phục vụ và chế độ chờ duyệt theo `OD-022`) và chặn đăng nhập với mã lỗi tường minh `403 AUTH.EMAIL_NOT_VERIFIED`, đồng thời hiển thị nút gửi lại email xác minh. | `OD-022` |
| **4** | **Người dùng đăng ký lại (`User Re-registration`)** | `BR-021`, `BR-024`, `BIZ-028`, `UC-001 (E4)`, `UC-013 (E4)`, `FR-TEN-001`, `FR-TEN-005`, `AC-032` | Nếu đơn đang `PENDING` còn hạn thì hướng dẫn xác minh lại/tiếp tục đơn hiện có (`409 REGISTRATION_ALREADY_PENDING`); nếu đơn cũ đã bị `REJECTED` hoặc hết hạn `TTL` thì cho phép gửi đơn đăng ký mới hợp lệ nhưng **bắt buộc giữ nguyên bản ghi đơn cũ trong lịch sử kiểm toán (`BR-021`)**, không xóa cứng. | `OD-023` |
| **5** | **Tạo `Primary Tenant` / Workspace không hoàn tất (`Incomplete Tenant/Workspace Creation`)** | `BR-023`, `BIZ-003`, `BIZ-028`, `UC-001 (E6)`, `UC-013 (E5)`, `FR-TEN-002`, `FR-TEN-005`, `AC-032` | Nếu bước tạo `Primary Tenant` (`DEC-WS-001`) hoặc nạp dữ liệu hạt giống (`Role/Permission`, `BusinessCalendar`, `TenantSetting`) gặp lỗi giữa chừng, hệ thống **hoàn tác nguyên tử (`Atomic Rollback`) toàn bộ giao dịch**, giữ `Company` ở `PENDING` và đảm bảo `0` `Tenant` lỗi/nửa vời tồn tại. | `OD-005`, `OD-024` |
| **6** | **Cấp quyền `COMPANY_ADMIN` ban đầu không thành công (`Failed Initial Owner/Admin Role Assignment`)** | `BR-023`, `BIZ-003`, `BIZ-028`, `UC-001 (E7)`, `UC-013 (E6)`, `FR-TEN-002`, `FR-TEN-005`, `AC-032` | Nếu bước tạo `UserAccount` người đại diện hoặc gán `UserRole(COMPANY_ADMIN)` thất bại, hoặc kiểm tra hậu điều kiện phát hiện `Count(ACTIVE COMPANY_ADMIN) < 1`, hệ thống **hoàn tác nguyên tử (`Atomic Rollback`) cả `Tenant` và trạng thái `Company`**, đảm bảo không bao giờ có `ACTIVE Tenant` mồ côi không có quản trị viên. | `OD-024` |

### 2.3. Danh sách Toàn bộ File Tài liệu Đã Cập nhật và Requirement ID Đã Bổ sung / Sửa đổi

1. **Bộ tài liệu RDS (`docs/requirements/rds/`)**:
   - [`RDS_v1.0_DRAFT.md`](./RDS_v1.0_DRAFT.md) (`v1.1-DRAFT`): Cập nhật Chương `0`, `1`, `6.2.4 (Flow D)`, `7`, `8.2`, `9.1`, `9.2`, `10`, `11`, `13`, `14`, `15.2`, `16.2`, `17`, `18.1`, `19`, `20`.
   - [`REQUIREMENTS_CATALOG.csv`](./REQUIREMENTS_CATALOG.csv) (`111` dòng):
     - **Bổ sung mới (4 ID)**: `BR-023`, `BR-024`, `BIZ-027` (`PROPOSED`), `BIZ-028` (`OPEN`).
     - **Cập nhật làm giàu (6 ID)**: `STKN-001`, `BIZ-003`, `CAP-002`, `INFO-001`, `EXT-001`, `EXT-003`.
   - [`USE_CASES.md`](./USE_CASES.md) (`v1.1-DRAFT`): Cập nhật `UC-001` (bổ sung bảng đối chiếu 2 mô hình và 7 luồng ngoại lệ `E1`–`E7`) và **bổ sung mới `UC-013`** (`PROPOSED — NOT APPROVED`: Tự đăng ký Doanh nghiệp, Xác minh Email & Khởi tạo Tự động `Primary Tenant`).
   - [`OPEN_DECISIONS.md`](./OPEN_DECISIONS.md) (`v1.1-DRAFT`): Bổ sung Câu hỏi khám phá số 8 và **4 Quyết định Mở mới (`OD-021`, `OD-022`, `OD-023`, `OD-024`)**.
   - [`REQUIREMENTS_TRACEABILITY.md`](./REQUIREMENTS_TRACEABILITY.md) (`v1.1-DRAFT`): Đồng bộ truy vết 2 chiều giữa `111` RDS IDs $\leftrightarrow$ `118` SRS IDs $\leftrightarrow$ SSS $\leftrightarrow$ `AC-001`..`AC-032` (`Bổ sung mới AC-031, AC-032`) / `TST-001`..`TST-031`.
   - [`RDS_REVIEW_REPORT.md`](./RDS_REVIEW_REPORT.md) (`v1.1-DRAFT` — tài liệu này).
2. **Bộ tài liệu SRS (`docs/requirements/srs/`)**:
   - [`SRS_v1.0_DRAFT.md`](../srs/SRS_v1.0_DRAFT.md) (`v1.1-DRAFT`): Làm giàu `FR-TEN-001`, `FR-TEN-002`, `IR-UI-001`, `IR-EXT-002`, `DR-002`, `TST-004`; **bổ sung mới `FR-TEN-005` (`PROPOSED`), `IR-UI-004` (`PROPOSED`), `TST-031`** và `OD-021`..`OD-024`.
   - [`REQUIREMENTS_CATALOG.csv`](../srs/REQUIREMENTS_CATALOG.csv) (`118` dòng: `63 FR`, `13 IR`, `15 DR`, `15 NFR`, `12 SEC`): Bổ sung `FR-TEN-005`, `IR-UI-004`; cập nhật `FR-TEN-001`, `FR-TEN-002`, `IR-UI-001`, `IR-EXT-002`, `DR-002`.
   - [`OPEN_DECISIONS.md`](../srs/OPEN_DECISIONS.md) (`v1.1-DRAFT`): Đồng bộ `OD-001`..`OD-024`.
   - [`REQUIREMENTS_TRACEABILITY.md`](../srs/REQUIREMENTS_TRACEABILITY.md) (`v1.1-DRAFT`): Đồng bộ `118/118` SRS IDs và `TST-001`..`TST-031`.
   - [`SRS_REVIEW_REPORT.md`](../srs/SRS_REVIEW_REPORT.md) (`v1.1-DRAFT`): Ghi nhận chi tiết kết quả review SRS.
3. **Bộ tài liệu SSS (`docs/specifications/sss/`)**:
   - [`SSS_v1.0_DRAFT.md`](../../specifications/sss/SSS_v1.0_DRAFT.md) (`v1.1-DRAFT`): Cập nhật luồng tương tác kiến trúc giữa Frontend (`MOD-FE-001`), Backend Authentication (`MOD-001`), Tenant Provisioning & Workspace Initialization (`MOD-002`), Initial Authorization (`MOD-003`), Transactional Email (`INT-002`), bổ sung `SEQ-007` và `ADR-SSS-007`.
   - [`MODULE_CATALOG.md`](../../specifications/sss/MODULE_CATALOG.md) (`v1.1-DRAFT`): Cập nhật trách nhiệm của `MOD-001`, `MOD-002`, `MOD-003`, `MOD-FE-001`.
   - [`INTERFACE_CATALOG.md`](../../specifications/sss/INTERFACE_CATALOG.md) (`v1.1-DRAFT`): Cập nhật hợp đồng API onboarding, xác minh email và `INT-002`.
   - [`diagrams/04-sequence-workflows.md`](../../specifications/sss/diagrams/04-sequence-workflows.md) (`v1.1-DRAFT`): Bổ sung sơ đồ trình tự **`SEQ-007`** (End-to-End Company Registration, Email Verification, Atomic Primary Workspace Provisioning, Initial `COMPANY_ADMIN` Grant & Atomic Rollback).
   - [`ARCHITECTURE_DECISIONS.md`](../../specifications/sss/ARCHITECTURE_DECISIONS.md) (`v1.1-DRAFT`): Bổ sung **`ADR-SSS-007`** (`PROPOSED — NOT APPROVED`).
   - [`OPEN_DECISIONS.md`](../../specifications/sss/OPEN_DECISIONS.md), [`SSS_TRACEABILITY.md`](../../specifications/sss/SSS_TRACEABILITY.md) & [`SSS_REVIEW_REPORT.md`](../../specifications/sss/SSS_REVIEW_REPORT.md) (`v1.1-DRAFT`).

---

## 3. Tổng hợp Kết quả Kiểm định Tự động (Automated Verification Metrics)

Kịch bản kiểm định tự động bằng PowerShell đã được thực thi trực tiếp trên thư mục `docs/requirements/rds/`. Kết quả định lượng đạt **100% tiêu chí vượt qua (PASS)**:

| Hạng mục Kiểm định | Chỉ số Kỳ vọng | Kết quả Thực tế | Đánh giá |
| :--- | :--- | :--- | :--- |
| **Số lượng file bàn giao trong `docs/requirements/rds/`** | Đủ 7 file theo cấu trúc yêu cầu | `7 / 7` files (`RDS_v1.0_DRAFT.md`, `REQUIREMENTS_CATALOG.csv`, `USE_CASES.md`, `REQUIREMENTS_TRACEABILITY.md`, `OPEN_DECISIONS.md`, `SOURCE_REGISTER.md`, `RDS_REVIEW_REPORT.md`) | **PASS** |
| **Độ đầy đủ cấu trúc `RDS_v1.0_DRAFT.md`** | Đủ 21 chương mục (Section `0` đến Section `20`) | `21 / 21` chương mục đầy đủ, chi tiết (`~282 KB`, `1,320+` dòng) | **PASS** |
| **Tổng số yêu cầu định danh trong `REQUIREMENTS_CATALOG.csv`** | Phân loại đầy đủ theo 7 nhóm yêu cầu RDS | **111 yêu cầu** (`14 STKN`, `24 BR`, `28 BIZ`, `13 CAP`, `12 INFO`, `5 EXT`, `15 QREQ`) | **PASS** |
| **Kiểm tra trùng lặp mã định danh (Duplicate RDS IDs)** | `0` mã trùng lặp | `0` duplicate IDs | **PASS** |
| **Độ phủ truy vết sang SRS (`118` SRS IDs)** | `118 / 118` SRS IDs (`100%`) được ánh xạ trong Catalog & Traceability Matrix | `118 / 118` (`0` missing in RDS Catalog, `0` missing in RDS Traceability) | **PASS** |
| **Số lượng Use Cases đặc tả chi tiết trong `USE_CASES.md`** | Đầy đủ các luồng cốt lõi MVP (`UC-001`..`UC-013`) | `13 / 13` Use Cases có đủ Preconditions, Postconditions, Main/Alt/Exception Flows, Business Rules, Acceptance Criteria | **PASS** |
| **Kiểm tra ranh giới `RDS != Relational Database Spec`** | `0` câu lệnh SQL DDL (`CREATE TABLE`, `ALTER TABLE`, `CREATE INDEX`, `FOREIGN KEY`) | `0` SQL DDL statements | **PASS** |
| **Kiểm tra liên kết nội bộ & liên tài liệu (Broken Relative Links)** | `0` liên kết gãy (broken links) trong tất cả các file `.md` thuộc `docs/requirements/rds/` | `0` broken relative links | **PASS** |
| **Kiểm tra không sửa đổi source code dự án (`git status`)** | Không sửa đổi bất kỳ file mã nguồn nào trong `backend/` hoặc `frontend/` | `0` modified source code files | **PASS** |

---

## 4. Kết luận Gate Review & Các Quyết định Mở Cần Stakeholder Phê duyệt (Sign-Off Recommendation)

### 4.1. Phán quyết Kiểm định (Gate Verdict)

Bộ tài liệu **Requirements Definition Specification (RDS v1.1-DRAFT)** cho dự án **AI-Assisted BizFlow Platform** **ĐẠT YÊU CẦU CHẤT LƯỢNG (PASSED QUALITY GATE)** về mặt cấu trúc, độ chính xác nghiệp vụ, tính nguyên tử, khả năng kiểm thử, độ phủ truy vết 100% (`111` RDS IDs $\leftrightarrow$ `118` SRS IDs $\leftrightarrow$ SSS) và đã đặc tả đầy đủ cả 7 điểm kiểm soát vòng đời cùng 6 kịch bản lỗi/hoàn tác nguyên tử của quy trình **Company Registration, Tenant Onboarding & Workspace Provisioning**.

### 4.2. Các Quyết định Mở Trọng yếu về Onboarding Cần Chốt Trước Khi Lập trình (`OD-021`..`OD-024`)

Để chuyển các yêu cầu onboarding từ `PROPOSED / OPEN` sang `CONFIRMED` và tiến hành lập trình các API còn thiếu (`NOT_IMPLEMENTED`), Chủ sở hữu sản phẩm (Product Owner) và Giảng viên hướng dẫn cần chốt **Câu hỏi Khám phá số 8** tại [`OPEN_DECISIONS.md`](./OPEN_DECISIONS.md):
- **`OD-021`**: Chọn mô hình onboarding cho MVP: (A) Giữ nguyên `PLATFORM_ADMIN` phê duyệt thủ công (`UC-001`), (B) Chuyển sang `Self-Service Onboarding` tự động cấp phát `Primary Tenant` ngay sau khi xác minh email (`UC-013`), hay (C) Hỗ trợ cả hai qua cờ cấu hình `ONBOARDING_MODE`.
- **`OD-022`**: Xác nhận thời hạn mã xác minh email (`TTL = 24 giờ`), giới hạn gửi lại (`3 lần/giờ`) và quy tắc chặn đăng nhập khi `EmailVerified = false`.
- **`OD-023`**: Xác nhận bổ sung trạng thái `CompanyStatus.Rejected` và cho phép đăng ký lại khi đơn cũ bị từ chối/hết hạn mà vẫn bảo toàn lịch sử (`BR-021`).
- **`OD-024`**: Xác nhận ranh giới giao dịch nguyên tử (`Atomic Rollback`) cho chuỗi `Company` + `Primary Tenant` (`DEC-WS-001`) + `UserAccount` + `UserRole(COMPANY_ADMIN)` + dữ liệu hạt giống (`BR-023`).
