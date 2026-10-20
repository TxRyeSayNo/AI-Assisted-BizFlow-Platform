# Báo cáo Kiểm toán Toàn diện Dự án (Project Audit Report)

- **Dự án:** AI-Assisted BizFlow Platform
- **Mã tài liệu:** `BIZFLOW-AUDIT-v1.0`
- **Ngày lập:** `2026-10-19`
- **Nhóm đánh giá:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Master Prompt, Senior PM, BA, Requirements, System Analyst, Architect, Fullstack, Database, AI, QA, DevOps)
- **Tài liệu đối chiếu nguồn:**
  - `docs/requirements/rds/` (`RDS_v1.0_DRAFT.md`, `REQUIREMENTS_CATALOG.csv`, `USE_CASES.md`, `OPEN_DECISIONS.md`)
  - `docs/requirements/srs/` (`SRS_v1.0_DRAFT.md`, `REQUIREMENTS_CATALOG.csv`, `REQUIREMENTS_TRACEABILITY.md`, `OPEN_DECISIONS.md`)
  - `docs/specifications/sss/` (`SSS_v1.0_DRAFT.md`, `MODULE_CATALOG.md`, `INTERFACE_CATALOG.md`, `ARCHITECTURE_DECISIONS.md`, `OPEN_DECISIONS.md`)
  - Source code hiện hành tại `backend/`, `frontend/`, `tests/`, `deploy/`.

---

## 1. Tóm tắt Điều hành (Executive Summary)

Nền tảng **AI-Assisted BizFlow Platform** là giải pháp SaaS B2B đa thuê bao (Multi-Tenant) phục vụ quản lý và điều phối luồng công việc nội bộ (yêu cầu dịch vụ, phân công công việc, quy trình phê duyệt, cam kết mức dịch vụ SLA và trợ lý AI).

### Hiện trạng Baseline của Dự án:
1. **Kiến trúc Tổng thể:** Tuân thủ mô hình **Modular Monolith** phân tầng rõ ràng:
   - Backend: ASP.NET Core .NET 10, Entity Framework Core 10, C# 13/14.
   - Frontend: Angular 22 Single Page Application, TypeScript 5, Angular Material, GRID Design System.
   - Database: PostgreSQL 18 với 34 EF Core Migrations, 37 bảng quan hệ.
   - Cache: Redis 7 phân vùng khóa theo `TenantId`.
   - Storage: S3 / MinIO (qua drop-in container `pgsty/silo`) hỗ trợ tải lên 3 bước tới 500 MB kèm băm SHA-256.
   - AI: Tích hợp OpenAI API phía backend với 10 use case AI (Human-in-the-Loop, không tự trị).
2. **Kiểm thử Tự động Baseline:**
   - Backend: 405/405 Unit Tests đạt 100% Green.
   - Frontend: 205/205 Component & Service Tests đạt 100% Green.
   - Build Production: Frontend đóng gói thành công 0 lỗi.
   - Health Probes: `/health/live` và `/health/ready` đạt HTTP 200 OK.
3. **Mô hình Đa Thuê bao & Bảo mật:**
   - Áp dụng triệt để nguyên tắc `1 Company = 1 Primary Tenant` trong MVP (`DEC-WS-001`).
   - Cưỡng chế phòng vệ 5 lớp (`ADR-SSS-001`), bộ lọc EF Core Fail-Closed `TenantId == CurrentTenantId`.
   - Cấm xóa cứng lịch sử (`PERSISTENCE.HARD_DELETE_DENIED`) và bảo vệ trường định danh bất biến (`PERSISTENCE.IMMUTABLE_FIELD`).

---

## 2. Khoảng trống Triển khai Cốt lõi (Critical Gaps Identified)

Đối chiếu với 63 Yêu cầu Chức năng (`FR-*`), 28 Giao diện (`INT-*`) và 13 Ca sử dụng (`UC-*`):

### 1. Mô-đun M02 — Quản lý Đăng ký Doanh nghiệp (`FR-TEN-001`, `FR-TEN-002`, `FR-TEN-003`, `IR-UI-004`)
- **Hiện trạng:** Bảng `Company` và `Tenant` đã tồn tại trong DB, API đọc hàng đợi `GET /api/v1/platform/company-registrations` đã có, giao diện `company-registrations.ts` đang hiển thị danh sách chỉ đọc.
- **Khoảng trống:**
  - Chưa có endpoint tiếp nhận đăng ký công khai `POST /api/v1/company-registrations` (`FR-TEN-001`).
  - Chưa có endpoint phê duyệt `POST /api/v1/platform/company-registrations/{id}/approve` thực thi giao dịch nguyên tử (`BR-023`, `OD-024`): kích hoạt Company + tạo Primary Tenant + khởi tạo tài khoản `COMPANY_ADMIN` ban đầu (`FR-TEN-002`).
  - Chưa có endpoint từ chối `POST /api/v1/platform/company-registrations/{id}/reject` bắt buộc nhập lý do (`BR-022`).
  - Chưa có endpoint cập nhật trạng thái `PATCH /api/v1/platform/company-registrations/{id}/status` (`FR-TEN-003`).
  - Frontend thiếu các nút hành động Approve/Reject trên `company-registrations.ts`, thiếu liên kết CTA từ `/login` sang `/register-company` (`IR-UI-004`), và chưa có màn hình đăng ký doanh nghiệp `/register-company`.

### 2. Mô-đun M03 — Quản lý Cơ cấu Tổ chức & Người dùng (`FR-ORG-001`, `FR-ORG-002`, `FR-ORG-003`, `FR-ORG-004`)
- **Hiện trạng:** Đã có đọc danh bạ phòng ban (`DepartmentQuery`), người dùng (`UserDirectoryQuery`), và quản lý vai trò tùy chỉnh (`RoleManagement` / `RolesController`).
- **Khoảng trống:**
  - Chưa có endpoint tạo tài khoản người dùng mới `POST /api/v1/users` (`FR-ORG-001`).
  - Chưa có endpoint cập nhật hồ sơ/phòng ban `PUT /api/v1/users/{id}` (`FR-ORG-002`).
  - Chưa có endpoint khóa/mở khóa người dùng `PATCH /api/v1/users/{id}/status` (`FR-ORG-003`).
  - Chưa có endpoint tạo/sửa phòng ban `POST/PUT /api/v1/departments` (`FR-ORG-004`).

---

## 3. Đánh giá Rủi ro & Chiến lược An toàn

1. **Rủi ro Phá vỡ Multi-Tenant Guard:**
   - Bộ bảo vệ `BizFlowDbContext.SaveChangesAsync` rất nghiêm ngặt. Khi Platform Administrator tạo Tenant hoặc gán quyền ban đầu, phải đảm bảo `AssertPlatformAccount()` cho phép xử lý mà không vi phạm quy tắc `AssertTenant()`.
2. **Rủi ro Giao dịch Nguyên tử (Atomic Rollback):**
   - Theo `BR-023` và `OD-024`, nếu bất kỳ bước nào trong chuỗi cấp phát 5 bước thất bại, toàn bộ giao dịch phải được rollback để không để lại Tenant mồ côi không có `COMPANY_ADMIN`.
3. **Bảo toàn Test Suite Hiện có:**
   - Toàn bộ 405 backend tests và 205 frontend tests phải được giữ vững 100% Green trong suốt quá trình hoàn thiện.
