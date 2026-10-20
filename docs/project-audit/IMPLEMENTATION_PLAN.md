# KẾ HOẠCH TRIỂN KHAI VÀ HOÀN THIỆN DỰ ÁN (IMPLEMENTATION PLAN)

**Dự án:** AI-Assisted BizFlow Platform  
**Mục tiêu:** Trực tiếp giải quyết các khoảng trống P0 / P1 đã xác định trong `IMPLEMENTATION_GAP_ANALYSIS.md`, đưa hệ thống đạt trạng thái sẵn sàng vận hành chuẩn mực cho MVP.

---

## 1. NGUYÊN TẮC VÀ RANH GIỚI TRIỂN KHAI (IMPLEMENTATION BOUNDARIES)

1. **Tuân thủ Kiến trúc Hiện hữu:** Giữ vững mô hình Modular Monolith (.NET 10 Web API + Angular 22). Không viết lại hoặc tách microservices không cần thiết.
2. **Bảo toàn Tính toàn vẹn Đa thuê bao (Multi-Tenant Isolation):** Mọi thao tác truy vấn và ghi dữ liệu của Tenant phải qua Global Query Filter và kiểm tra chặt chẽ `TenantId`. Riêng Platform Administrator (`TenantId == null`) có thẩm quyền khởi tạo Tenant mới và tài khoản ban đầu theo cơ chế kiểm soát nguyên tử (`AssertPlatformAccount`).
3. **Bảo vệ Tính Bất biến và Chặn Xóa cứng:** Duy trì cơ chế Soft Delete (`IsDeleted`) và chống sửa các trường định danh bất biến (`PERSISTENCE.IMMUTABLE_FIELD`).
4. **Kiểm thử Liên tục (Continuous Verification):** Sau mỗi nhóm thay đổi, chạy build và unit test ngay lập tức để ngăn ngừa regression.

---

## 2. DANH MỤC CÁC TÁC VỤ TRIỂN KHAI ĐƯỢC ƯU TIÊN

### Nhóm 1: Module M02 — Quản lý Đăng ký & Khởi tạo Doanh nghiệp (P0 / P1)

#### Task TSK-M02-01: Public Company Registration Intake & DbContext Invariant Adjustment
- **Task ID:** `TSK-M02-01`
- **Requirement IDs:** `FR-TEN-001`, `BR-024`, `IR-UI-004`
- **Problem:**
  - Hiện tại, cổng công khai chưa có API tiếp nhận đơn đăng ký doanh nghiệp mới.
  - `BizFlowDbContext.SaveChangesAsync` có kiểm tra bảo vệ `AssertTenant` chặn mọi entity có `TenantId` nếu `CurrentUserId` là null. Khi doanh nghiệp gửi đăng ký chưa có người dùng đăng nhập, thao tác lưu bản ghi `Company` ở trạng thái `Pending` sẽ bị chặn nếu không có ngoại lệ hợp lệ.
- **Proposed Solution:**
  - Điều chỉnh `AssertTenant` trong `BizFlowDbContext.cs` để cho phép thao tác thêm mới bản ghi `Company` ở trạng thái `CompanyStatus.Pending` khi chưa có người dùng đăng nhập (public registration intake).
  - Thêm DTO `SubmitCompanyRegistrationRequest` và Handler `SubmitCompanyRegistrationCommand`.
  - Expose API `POST /api/v1/company-registrations` (cho phép anonymous access).
- **Affected Files/Modules:**
  - `backend/BizFlow.Infrastructure/Persistence/BizFlowDbContext.cs`
  - `backend/BizFlow.Application/Tenancy/CompanyRegistrationManagement.cs`
  - `backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs`
- **Dependencies:** Entity `Company` hiện có.
- **Risk:** Cần đảm bảo ngoại lệ anonymous chỉ áp dụng DUY NHẤT cho việc thêm mới `Company` với trạng thái `Pending`, tuyệt đối không rò rỉ cho các bảng nghiệp vụ khác.
- **Verification Method:** Unit test `CompanyRegistrationManagementTests` gửi payload hợp lệ/không hợp lệ và kiểm tra trạng thái lưu trữ trong DB.
- **Status:** `COMPLETED`

---

#### Task TSK-M02-02: Platform Admin Atomic Approval & Rejection Workflow
- **Task ID:** `TSK-M02-02`
- **Requirement IDs:** `FR-TEN-002`, `FR-TEN-003`, `BR-022`, `BR-023`, `OD-024`
- **Problem:**
  - Chưa có API cho Quản trị viên Nền tảng (Platform Administrator) phê duyệt đơn đăng ký doanh nghiệp.
  - Phê duyệt phải thực hiện nguyên tử (Atomic Unit of Work): chuyển trạng thái Company sang `Active`, tạo `Tenant` tương ứng, tạo `UserAccount` đại diện ban đầu với vai trò `COMPANY_ADMIN`, và ghi `AuditLog`.
  - Từ chối phải bắt buộc có lý do (`BR-022`).
- **Proposed Solution:**
  - Trong `BizFlowDbContext.cs`, đảm bảo `AssertTenant` cho phép Platform Administrator (`IsPlatformAdministrator == true` và `CurrentTenantId == null`) tạo `Tenant`, `UserAccount`, và gán `UserRole` cho tenant mới trong cùng một transaction.
  - Xây dựng `ApproveCompanyRegistrationCommand` và `RejectCompanyRegistrationCommand`.
  - Expose endpoints:
    - `POST /api/v1/platform/company-registrations/{id}/approve`
    - `POST /api/v1/platform/company-registrations/{id}/reject`
    - `PATCH /api/v1/platform/company-registrations/{id}/status`
- **Affected Files/Modules:**
  - `backend/BizFlow.Infrastructure/Persistence/BizFlowDbContext.cs`
  - `backend/BizFlow.Application/Tenancy/CompanyRegistrationManagement.cs`
  - `backend/BizFlow.Api/Controllers/PlatformCompanyRegistrationsController.cs`
- **Dependencies:** Task `TSK-M02-01`.
- **Risk:** Rollback transaction toàn phần nếu có bất kỳ bước nào thất bại để tránh tình trạng "mồ côi" Tenant hoặc UserAccount.
- **Verification Method:** Integration test kiểm tra duyệt thành công tạo đủ 3 bản ghi (Company, Tenant, UserAccount) và kiểm tra rollback khi có lỗi.
- **Status:** `COMPLETED`

---

#### Task TSK-M02-03: Frontend Company Registration UI & Approval Actions
- **Task ID:** `TSK-M02-03`
- **Requirement IDs:** `IR-UI-004`, `FR-TEN-001`, `FR-TEN-002`
- **Problem:**
  - Chưa có màn hình `/register-company` cho doanh nghiệp tự đăng ký.
  - Màn hình `/login` chưa có nút liên kết dẫn đến trang đăng ký doanh nghiệp.
  - Màn hình `/platform/companies` của Platform Admin mới chỉ hiển thị danh sách dạng bảng, chưa có nút bấm Duyệt (Approve) hoặc Từ chối (Reject) kèm modal nhập lý do.
- **Proposed Solution:**
  - Tạo Component Angular `RegisterCompanyComponent` với đầy đủ validation (Tên công ty, Mã số thuế, Email đại diện, Số điện thoại, v.v.).
  - Thêm CTA link "Đăng ký doanh nghiệp mới" tại `LoginComponent`.
  - Cập nhật `company-registrations.ts` trên giao diện Platform Admin: thêm action button `Duyệt` và `Từ chối` (có modal xác nhận và nhập lý do bắt buộc).
- **Affected Files/Modules:**
  - `frontend/src/app/features/auth/register-company.ts`
  - `frontend/src/app/features/auth/login.html`
  - `frontend/src/app/features/platform/company-registrations.ts`
  - `frontend/src/app/app.routes.ts`
- **Dependencies:** Task `TSK-M02-01`, `TSK-M02-02`.
- **Risk:** Cần đảm bảo form responsive và hiển thị rõ thông báo thành công sau khi gửi đơn.
- **Verification Method:** `npm test`, `npm run build`, và kiểm thử UI trong browser.
- **Status:** `COMPLETED`

---

### Nhóm 2: Module M03 — Quản lý Người dùng & Phòng ban (P0)

#### Task TSK-M03-01: User Management Commands & Write Endpoints
- **Task ID:** `TSK-M03-01`
- **Requirement IDs:** `FR-ORG-001`, `FR-ORG-002`, `FR-ORG-003`, `BR-001`
- **Problem:**
  - `UsersController.cs` hiện tại chỉ hỗ trợ các thao tác đọc (GET list/detail).
  - Chưa có API để Quản trị viên Doanh nghiệp thêm nhân viên mới, cập nhật thông tin phòng ban/vai trò, hoặc vô hiệu hóa tài khoản.
- **Proposed Solution:**
  - Xây dựng `CreateUserCommand`, `UpdateUserCommand`, `UpdateUserStatusCommand` trong Application layer.
  - Kiểm tra tính duy nhất của Username/Email trong cùng Tenant.
  - Bổ sung các endpoints vào `UsersController.cs`:
    - `POST /api/v1/users`
    - `PUT /api/v1/users/{id}`
    - `PATCH /api/v1/users/{id}/status`
- **Affected Files/Modules:**
  - `backend/BizFlow.Application/Organization/UserManagement.cs`
  - `backend/BizFlow.Infrastructure/Organization/UserManagementStore.cs`
  - `backend/BizFlow.Api/Controllers/UsersController.cs`
- **Dependencies:** Auth & RBAC module.
- **Risk:** Đảm bảo password băm an toàn qua PBKDF2/BCrypt và kiểm tra phân quyền `USERS_MANAGE`.
- **Verification Method:** Unit tests cho UserManagement và API integration tests.
- **Status:** `COMPLETED`

---

#### Task TSK-M03-02: Department Structure & Hierarchy Management
- **Task ID:** `TSK-M03-02`
- **Requirement IDs:** `FR-ORG-004`, `BR-003`
- **Problem:**
  - `DepartmentsController.cs` hiện chỉ có endpoint đọc cây phòng ban.
  - Thiếu API tạo và cập nhật phòng ban, đồng thời cần kiểm tra chống đệ quy vòng lặp (`ParentDepartmentId` trỏ vào chính nó hoặc con của nó).
- **Proposed Solution:**
  - Xây dựng `CreateDepartmentCommand`, `UpdateDepartmentCommand`.
  - Triển khai thuật toán kiểm tra chu trình đồ thị phòng ban.
  - Bổ sung endpoints:
    - `POST /api/v1/departments`
    - `PUT /api/v1/departments/{id}`
- **Affected Files/Modules:**
  - `backend/BizFlow.Application/Organization/DepartmentManagement.cs`
  - `backend/BizFlow.Infrastructure/Organization/DepartmentManagementStore.cs`
  - `backend/BizFlow.Api/Controllers/DepartmentsController.cs`
- **Dependencies:** TenantContext.
- **Risk:** Kiểm tra chu trình đệ quy phải an toàn và không gây tràn ngăn xếp (Stack Overflow).
- **Verification Method:** Unit test tạo cấu trúc phòng ban hợp lệ và test phát hiện vòng lặp bị từ chối với lỗi `BR-003`.
- **Status:** `COMPLETED`

---

## 3. LỘ TRÌNH THỰC THI (EXECUTION PHASES)

1. **Phase 1 (M02 - Backend Core):**
   - Sửa `BizFlowDbContext.cs` cho phép anonymous registration và atomic admin provisioning.
   - Viết `CompanyRegistrationManagement.cs` và cập nhật controllers.
   - Chạy `dotnet test BizFlow.slnx` để xác nhận không có regression.
2. **Phase 2 (M02 - Frontend UI):**
   - Viết component `/register-company` và liên kết từ login.
   - Thêm nút Duyệt/Từ chối và modal xử lý trên `/platform/companies`.
   - Chạy `npm test` và `npm run build`.
3. **Phase 3 (M03 - Backend Core):**
   - Viết `UserManagement.cs` và `DepartmentManagement.cs`.
   - Cập nhật `UsersController` và `DepartmentsController`.
   - Viết unit tests kiểm chứng.
4. **Phase 4 (Final Validation & Audit Progress):**
   - Chạy toàn bộ test suites (Backend + Frontend).
   - Cập nhật các tài liệu tiến độ `IMPLEMENTATION_PROGRESS.md`, `VALIDATION_REPORT.md`, `REMAINING_GAPS.md`.
