# BÁO CÁO REFACTOR VÀ HOÀN THIỆN WORKFLOW DESIGNER BIZFLOW THEO NGHIỆP VỤ DOANH NGHIỆP

**Dự án**: AI-Assisted BizFlow Platform  
**Nhánh git**: `develop`  
**Ngày thực hiện**: 11/11/2026  
**Trạng thái**: **100% Hoàn thành — Tất cả kiểm thử Frontend & Backend đều Đạt**

---

## 1. TỔNG QUAN VÀ MỤC TIÊU ĐẠT ĐƯỢC

Toàn bộ yêu cầu nâng cấp, refactor và hoàn thiện module **Workflow Designer** theo chuẩn quy trình doanh nghiệp thực tế đã được triển khai đầy đủ và kiểm thử nghiêm ngặt trên cả 3 tầng kiến trúc:

1. **Tầng Trực quan (Designer & UI Canvas Layer - Angular 22)**:
   - Cung cấp đầy đủ cấu hình thuộc tính nghiệp vụ cho cả 6 loại nút: `START`, `END`, `DEPARTMENT`, `APPROVAL`, `CONFIRMATION`, `DECISION`.
   - Bổ sung **Condition Builder** trực quan cho nút `DECISION` (Cổng rẽ nhánh điều kiện) với các toán tử chuẩn (`EQ`, `NEQ`, `GT`, `GTE`, `LT`, `LTE`, `IN`, `IS_EMPTY`, `IS_NOT_EMPTY`).
   - Cấu hình đường liên kết (Edge) linh hoạt với mã kết quả `outcomeCode` (`APPROVED`, `REJECTED`, `REQUEST_CHANGES`, `COMPLETED`, `CONFIRMED`, `NEEDS_INFORMATION`, `DEFAULT`).
   - Loại bỏ hoàn toàn việc tự ý gán mặc định vai trò `COMPANY_ADMIN` hoặc vai trò đầu tiên khi kéo thả nút mới.

2. **Tầng Định nghĩa & Kiểm tra Hợp lệ (Definition & Validation Layer - .NET 10)**:
   - **Cách ly vai trò & Tenant Scoping**: Loại bỏ triệt để rò rỉ `PLATFORM_ADMIN` khỏi dropdown vai trò công ty (`GetActiveRolesAsync`).
   - Bổ sung tra cứu người dùng nội bộ công ty (`GetActiveUsersAsync`, endpoint `/api/v1/workflows/lookup/users`).
   - Bộ đánh giá điều kiện chuyên biệt `WorkflowConditionEvaluator`: Đánh giá kiểu dữ liệu số, chuỗi, boolean, mảng, danh sách giá trị và rỗng một cách an toàn (không dùng `eval` hay dynamic code injection).
   - Bộ thẩm định sơ đồ `WorkflowValidator`:
     - Kiểm tra tính tương thích giữa sự kiện kích hoạt của `START` với loại nghiệp vụ (`REQUEST_CREATED` cho Request, `TASK_CREATED` cho Task).
     - Kiểm tra trạng thái hoàn tất của sự kiện `END` (`COMPLETED`, `REJECTED`, `CANCELLED`, `FAILED`).
     - Xác thực các phương thức phân công: `DEPARTMENT_QUEUE`, `BUSINESS_ROLE`, `SPECIFIC_USER`, `DEPARTMENT_MANAGER`, `DIRECT_MANAGER`, `REQUESTER`.
     - Cho phép chu trình làm lại có sự tham gia của con người (`APPROVAL -> REQUEST_CHANGES -> REQUESTER / DEPARTMENT`), đồng thời ngăn chặn các vòng lặp rẽ nhánh tự động vô tận (Automatic Infinite Cycles).

3. **Tầng Vận hành & Điều phối Thực thi (Runtime Execution Layer)**:
   - Xây dựng `IWorkflowRuntimeEngine` và `WorkflowExecutionEngine`:
     - Phân giải bước khởi đầu `StartWorkflowAsync` từ `START`.
     - Điều phối bước tiếp theo `ResolveNextStepAsync` / `ResolveStepFromGraph` dựa trên `outcomeCode` và dữ liệu nghiệp vụ `contextPayload`.
     - Tự động đánh giá các cổng điều kiện `DECISION` và rẽ nhánh chính xác theo thứ tự ưu tiên hoặc nhánh mặc định.
     - Phân giải người thực hiện khi bước yêu cầu `REQUESTER`, `SPECIFIC_USER` hoặc hàng đợi phòng ban `DEPARTMENT_QUEUE`.

---

## 2. CHI TIẾT CÁC THAY ĐỔI THEO TỪNG GIAI ĐOẠN

### 2.1. Phân quyền và Bảo mật Vai trò (Phase 1)
- **Tập tin**:
  - `backend/BizFlow.Infrastructure/Workflows/WorkflowLibraryStore.cs`
  - `backend/BizFlow.Application/Workflows/WorkflowLibrary.cs`
  - `backend/BizFlow.Api/Controllers/WorkflowsController.cs`
- **Thay đổi chính**:
  - Cập nhật `GetActiveRolesAsync`: Lọc bỏ `PLATFORM_ADMIN` và tất cả các vai trò thuộc quyền cấp nền tảng (`PermissionScope.Platform` hoặc `Code == "PLATFORM_ADMIN"`).
  - Triển khai `GetActiveUsersAsync`: Truy vấn danh sách nhân sự đang hoạt động của công ty (kèm theo mã nhân viên, họ tên, email, phòng ban), loại trừ tài khoản quản trị hệ thống nền tảng.
  - Cung cấp endpoint bảo mật: `GET /api/v1/workflows/lookup/users?departmentId=...`.

### 2.2. Giao diện Canvas và Bảng Thuộc tính Inspector (Phase 2)
- **Tập tin**:
  - `frontend/src/app/features/workflows/workflow-designer.ts`
  - `frontend/src/app/features/workflows/workflow-designer.html`
  - `frontend/src/app/features/workflows/workflow-designer.scss`
  - `frontend/src/app/features/workflows/workflow-version.ts`
  - `frontend/src/app/features/workflows/workflow-version.html`
- **Nâng cấp chi tiết**:
  - **Nút START**: Chọn sự kiện kích hoạt `REQUEST_CREATED`, `TASK_CREATED`, `MANUAL`.
  - **Nút END**: Chọn trạng thái hoàn tất `COMPLETED`, `REJECTED`, `CANCELLED`, `FAILED`.
  - **Nút DEPARTMENT**:
    - Chọn phương thức phân công `assignmentMethod`: Hàng đợi phòng ban (`DEPARTMENT_QUEUE`), Vai trò nghiệp vụ (`BUSINESS_ROLE`), Trưởng phòng (`DEPARTMENT_MANAGER`), Quản lý trực tiếp (`DIRECT_MANAGER`), Chỉ định nhân sự (`SPECIFIC_USER`), hoặc Người tạo yêu cầu (`REQUESTER`).
    - Cấu hình thời hạn SLA bước (giờ).
    - Tùy chọn yêu cầu kết quả: Bắt buộc ghi chú (`requireNotes`), bắt buộc tệp đính kèm (`requireAttachment`), bắt buộc bàn giao sản phẩm (`requireDeliverable`).
  - **Nút APPROVAL**:
    - Chọn phương thức duyệt (`BUSINESS_ROLE`, `DEPARTMENT_MANAGER`, `DIRECT_MANAGER`, `SPECIFIC_USER`).
    - Lựa chọn vai trò nội bộ doanh nghiệp (đã lọc sạch platform role).
    - Chính sách phê duyệt (`SINGLE`, `ALL`, `ANY`).
    - Bắt buộc nhập lý do khi từ chối (`rejectionReasonRequired`).
    - Cấu hình SLA phê duyệt.
  - **Nút CONFIRMATION**:
    - Chọn mốc xác nhận (`RECEIVE` - Tiếp nhận ban đầu, `RESULT` - Kết quả xử lý, `RESOLUTION` - Nghiệm thu kết thúc).
    - Người chịu trách nhiệm xác nhận (`REQUESTER`, `DEPARTMENT_MANAGER`, `SPECIFIC_USER`).
    - Yêu cầu biên bản và nhận xét nghiệm thu.
  - **Nút DECISION (Condition Builder)**:
    - Danh sách thẻ quy tắc điều kiện có đánh số thứ tự `#1`, `#2`...
    - Cấu hình: Tên trường dữ liệu (`field`), toán tử so sánh (`operator`), giá trị đối soát (`value`), bước đích (`targetNodeId` được chọn trực tiếp từ các nút đã kết nối đầu ra).
    - Hỗ trợ nhánh mặc định (Else / Default Branch) tự động kích hoạt khi không thỏa mãn quy tắc nào trước đó.
  - **Liên kết Edge**:
    - Cấu hình mã kết quả `outcomeCode` (`APPROVED`, `REJECTED`, `REQUEST_CHANGES`, `COMPLETED`, `CONFIRMED`, `NEEDS_INFORMATION`, `DEFAULT`).
    - Đặt làm liên kết mặc định (`isDefault`).
    - Nhãn hiển thị trực quan trên Canvas.

### 2.3. Bộ Đánh giá Điều kiện & Kiểm tra Hợp lệ Sơ đồ (Phase 3)
- **Tập tin**:
  - `backend/BizFlow.Application/Workflows/WorkflowConditionEvaluator.cs`
  - `backend/BizFlow.Application/Workflows/WorkflowValidator.cs`
- **Khả năng**:
  - Hỗ trợ đầy đủ toán tử: `=`, `!=`, `>`, `>=`, `<`, `<=`, `IN`, `IS_EMPTY`, `IS_NOT_EMPTY`.
  - Hỗ trợ so sánh số học an toàn (`decimal`), chuỗi không phân biệt hoa thường, mảng, danh sách phân cách bởi dấu phẩy, giá trị null/rỗng.
  - Kiểm tra tính hợp lệ toàn diện: Tính liên thông từ `START` tới `END`, không có nút cô lập hoặc đường cụt không lối thoát, cổng rẽ nhánh có ít nhất 2 nhánh rẽ, kiểm tra tính hợp lệ của Tenant ID đối với Department/Role/User.
  - **Rework Loop Safe**: Cho phép chu trình hoàn trả hồ sơ (e.g. `Trưởng phòng duyệt -> Yêu cầu làm lại -> Người yêu cầu sửa đổi -> Trình duyệt lại`) vì chu trình này có bước con người tương tác, không gây treo hệ thống.

### 2.4. Động cơ Thực thi Quy trình Runtime Engine (Phase 4)
- **Tập tin**:
  - `backend/BizFlow.Application/Workflows/WorkflowExecutionEngine.cs`
  - `backend/BizFlow.Api/Program.cs`
- **Khả năng**:
  - Tự động bắt đầu quy trình từ nút `START` và chuyển tiếp đến bước đầu tiên.
  - Phân giải bước kế tiếp dựa trên mã hành động (`actionOutcome`) của người dùng hoặc sự kiện hệ thống.
  - Tự động đối soát điều kiện tại các cổng `DECISION` dựa trên dữ liệu payload của yêu cầu/nhiệm vụ (ví dụ: `amount > 20,000,000`).
  - Gán người chịu trách nhiệm chính xác theo `assignmentMethod`:
    - `REQUESTER`: Tự động gán cho ID người tạo yêu cầu ban đầu.
    - `SPECIFIC_USER`: Gán cho nhân viên được cấu hình đích danh.
    - `DEPARTMENT_QUEUE`: Gán cho hàng đợi xử lý chung của phòng ban.
    - `BUSINESS_ROLE`: Gán cho vai trò nghiệp vụ trong phòng ban.

---

## 3. KẾT QUẢ KIỂM THỬ TOÀN DIỆN

### 3.1. Bộ Kiểm thử Backend (.NET 10 - xUnit)
Chạy lệnh: `dotnet test tests/BizFlow.UnitTests/BizFlow.UnitTests.csproj`

```text
Passed!  - Failed: 0, Passed: 568, Skipped: 0, Total: 568, Duration: 4 s - BizFlow.UnitTests.dll (net10.0)
```

**Các nhóm bài kiểm thử trọng tâm**:
- `WorkflowConditionEvaluatorTests` (18 bài kiểm thử):
  - Đánh giá chính xác `>`, `>=`, `<`, `<=`, `=`, `!=` với dữ liệu số và tiền tệ.
  - So sánh chuỗi không phân biệt hoa thường.
  - Đối soát danh sách thành viên với toán tử `IN`.
  - Kiểm tra giá trị rỗng với `IS_EMPTY` và `IS_NOT_EMPTY` trên chuỗi, mảng và null.
- `WorkflowRoleScopingTests` (4 bài kiểm thử):
  - Từ chối vai trò nền tảng `PLATFORM_ADMIN` và các vai trò lạ không thuộc Tenant.
  - Phê duyệt các phương thức phân công nội bộ doanh nghiệp.
  - Chấp thuận chu trình hoàn trả hồ sơ có con người can thiệp (Human Rework Loop).
  - Từ chối các vòng lặp rẽ nhánh tự động kín không có điểm thoát (Infinite Auto-Cycle).
- `WorkflowExecutionEngineTests` (5 bài kiểm thử - Kịch bản mua sắm thiết bị mục 12.D):
  - Thẩm định thành công toàn bộ sơ đồ quy trình mua sắm 6 bước.
  - Kịch bản chi phí cao (`amount = 50,000,000` > 20tr): Tự động rẽ nhánh qua Giám đốc Tài chính (CFO) duyệt -> Phòng Mua sắm đặt hàng -> Người yêu cầu nghiệm thu -> Hoàn tất.
  - Kịch bản chi phí tiêu chuẩn (`amount = 15,000,000` <= 20tr): Tự động bỏ qua CFO, đi thẳng tới Phòng Mua sắm đặt hàng.
  - Kịch bản Trưởng phòng từ chối (`REJECTED`): Đi trực tiếp tới điểm Kết thúc Từ chối mua sắm (`REJECTED`).
  - Kịch bản Trưởng phòng yêu cầu sửa đổi (`REQUEST_CHANGES`): Chuyển tiếp tới bước sửa đổi gán cho Người yêu cầu (`REQUESTER`), sau đó nộp lại quay về Trưởng phòng.

### 3.2. Bộ Kiểm thử Frontend (Angular 22 - Vitest)
Chạy lệnh: `npm --prefix frontend test`

```text
Test Files  45 passed (45)
     Tests  270 passed (270)
  Duration  27.96s
```

Tất cả 45 tệp kiểm thử với 270 test cases bao gồm `workflow-version.spec.ts`, `workflows.spec.ts`, `request-detail.spec.ts`, `tasks.spec.ts`, v.v. đều hoạt động ổn định và hoàn toàn xanh (100% Pass).

---

## 4. HƯỚNG DẪN TRẢI NGHIỆM TRỰC QUAN TRÊN GIAO DIỆN

Người dùng quản trị có thể kiểm tra trực tiếp tính năng mới theo các bước:

1. **Truy cập**: Đăng nhập với quyền Quản trị viên Doanh nghiệp (`COMPANY_ADMIN`), vào mục **Cài đặt** -> **Quy trình làm việc (Workflows)**.
2. **Mở Canvas Thiết kế**: Bấm chọn một quy trình bản nháp (`DRAFT`) để mở **Workflow Designer**.
3. **Thêm Nút & Kiểm tra Inspector**:
   - Kéo nút **Sự kiện bắt đầu (START)**: Bảng bên phải hiển thị lựa chọn sự kiện kích hoạt (`REQUEST_CREATED` / `TASK_CREATED` / `MANUAL`).
   - Kéo nút **Bước phê duyệt (APPROVAL)**: Bảng bên phải cho phép chọn phương thức duyệt (`Theo vai trò`, `Trưởng phòng`, `Người chỉ định`). Dropdown vai trò chỉ chứa vai trò công ty, hoàn toàn không có `PLATFORM_ADMIN`.
   - Kéo nút **Cổng rẽ nhánh điều kiện (DECISION)**:
     - Bấm **+ Thêm quy tắc** để tạo điều kiện phân loại (ví dụ: `amount` `>` `20000000`).
     - Chọn bước tiếp theo trực tiếp từ dropdown các nút đã nối đầu ra.
     - Tùy chọn đặt làm **Nhánh mặc định (Else)**.
   - Chọn đường nối (**Edge**): Chọn mã kết quả `outcomeCode` (`APPROVED`, `REJECTED`, `REQUEST_CHANGES`...) để điều khiển hướng rẽ nhánh.
4. **Kiểm tra sơ đồ (Validate)**: Bấm nút **Kiểm tra sơ đồ** trên thanh công cụ; nếu thiếu phòng ban, cô lập bước hoặc sai điều kiện, hệ thống sẽ mở ngăn cảnh báo với gợi ý chi tiết bằng tiếng Việt có dấu chuẩn Unicode.
5. **Lưu bản nháp & Xuất bản**: Bấm **Lưu bản nháp** để lưu trữ toàn bộ cấu hình vào cơ sở dữ liệu PostgreSQL.

