# BÁO CÁO AUDIT VÀ MA TRẬN KHOẢNG CÁCH WORKFLOW (PHASE 0)
**Dự án:** AI-Assisted BizFlow Platform  
**Phiên bản:** 2.0.0-ENTERPRISE  
**Mục tiêu:** Đánh giá thực trạng ba lớp (Thiết kế - Định nghĩa - Thực thi) của Workflow Designer và thiết lập lộ trình hoàn thiện chuẩn nghiệp vụ doanh nghiệp.

---

## 1. Bảng Ma Trận Đánh Giá Khoảng Cách Các Thành Phần (Workflow Gap Matrix)

| Component | Existing Properties | Missing Properties | Persistence | Backend Validation | Runtime Support | Required Changes | Priority |
|---|---|---|---|---|---|---|---|
| **START** | `id`, `name`, `type`, `trigger` | `triggerType` (`MANUAL`, `REQUEST_CREATED`, `TASK_CREATED`), `businessScope`, `allowedRoles` | Lưu trong `DefinitionJson` | Chỉ kiểm tra: duy nhất 1 start, không có incoming, có ít nhất 1 outgoing | Chưa có bộ khởi tạo instance từ trigger thực tế | Thêm dropdown `triggerType`, ràng buộc khởi tạo, kiểm tra trigger tương thích | **P1** |
| **END** | `id`, `name`, `type`, `completionState` | `outcomeCode` (`COMPLETED`, `REJECTED`, `CANCELLED`, `FAILED`), `auditAction` | Lưu trong `DefinitionJson` | Chỉ kiểm tra: không có outgoing, có incoming | Chưa cập nhật trạng thái Task/Request tương ứng với mã kết thúc | Cho phép cấu hình outcome rõ ràng, hỗ trợ nhiều node END với các kết quả khác nhau | **P1** |
| **DEPARTMENT** | `id`, `name`, `departmentId`, `roleId`, `slaHours`, `description` | `stepCode`, `assignmentMethod` (`DEPARTMENT_QUEUE`, `BUSINESS_ROLE`, `WORK_GROUP`, `DEPARTMENT_MANAGER`, `DIRECT_MANAGER`, `SPECIFIC_USER`, `REQUESTER`), `specificUserId`, `outcomes` (`COMPLETED`, `NEEDS_INFORMATION`, `CANNOT_COMPLETE`), `requireNotes`, `requireAttachment` | Lưu trong `DefinitionJson` và `WorkflowStep` | Kiểm tra: có `departmentId`, `roleId` hợp lệ. Chưa kiểm tra phương thức phân công và outcomes | `TaskWorkflowEngine` hiện chặn task có `WorkflowVersionId` | Bổ sung đầy đủ 5 nhóm thuộc tính (Thông tin, Phân công, SLA, Kết quả, Bằng chứng); hỗ trợ chọn người cụ thể; validate chặt chẽ | **P0** |
| **APPROVAL** | `id`, `name`, `approverType`, `roleId`, `description` | `approvalPolicy` (`SINGLE`, `ALL`, `ANY`), `specificUserId`, `outcomes` (`APPROVED`, `REJECTED`, `REQUEST_CHANGES`), `rejectionReasonRequired`, `timeoutHours`, `escalationRole` | Lưu trong `DefinitionJson` và `WorkflowStep` | Chỉ kiểm tra: `roleId` (nếu có). **LỖI LỚN:** Rò rỉ `PLATFORM_ADMIN`, tự động gán `COMPANY_ADMIN` | Chưa có runtime xử lý đa kết quả phê duyệt | Cấm triệt để `PLATFORM_ADMIN`; không mặc định `COMPANY_ADMIN`; bổ sung picker User; cấu hình outcome chuyển tiếp | **P0** |
| **CONFIRMATION** | `id`, `name`, `milestone` (`RECEIVE`, `RESULT`, `RESOLUTION`), `description` | `confirmerPolicy` (`REQUESTER`, `DIRECT_MANAGER`, `SPECIFIC_USER`), `outcomes` (`CONFIRMED`, `REJECTED`, `REQUEST_CHANGES`), `requireNotes`, `requireProof` | Lưu trong `DefinitionJson` và `WorkflowStep` | Chỉ kiểm tra incoming/outgoing cơ bản | Hardcoded theo Task confirmation cố định, không theo workflow definition | Bổ sung chính sách người xác nhận; định nghĩa các outcome và nhánh xử lý tương ứng | **P1** |
| **DECISION** | `id`, `name`, `conditions` (dạng thô `{field, operator, value}`) | **HOÀN TOÀN THIẾU INSPECTOR TRÊN FRONTEND**: Condition Builder, danh mục trường dữ liệu doanh nghiệp, toán tử `EQ, NEQ, GT, GTE, LT, LTE, IN, IS_EMPTY, IS_NOT_EMPTY`, nhánh mặc định `isDefault`, ánh xạ tới `targetEdgeId` | Lưu dạng mảng mộc trong node, `guardJson` trong `WorkflowTransition` | Chỉ kiểm tra: có ít nhất 2 outgoing edges. Không validate cú pháp hay logic rẽ nhánh | Chưa có bộ đánh giá điều kiện (Condition Evaluator) lúc runtime | Xây dựng Condition Builder trên UI; xây dựng Typed Rule Evaluator trên backend an toàn, chống injection | **P0** |
| **EDGES & TRANSITIONS** | `id`, `source`, `target`, `label`, `guard` | `outcomeCode` (từ bước APPROVAL/CONFIRMATION/DEPARTMENT), `conditionId`, `isDefault`, `priorityOrder` | Lưu trong `edges` của `DefinitionJson` và bảng `WorkflowTransitions` | Kiểm tra: source, target tồn tại, không tự kết nối, kiểm tra liên thông reachability | Chưa có cơ chế chọn edge dựa trên outcome hoặc guard lúc runtime | Thiết kế contract Edge chặt chẽ, ràng buộc nhánh điều kiện và kết quả nghiệp vụ | **P0** |
| **RUNTIME ENGINE** | 5 trạng thái cố định trong `TaskWorkflowEngine` | Động cơ điều phối workflow theo `WorkflowVersion` thực tế: xác định bước hiện tại, người thực hiện, kiểm tra guard, chuyển bước, ghi nhật ký kiểm toán | Bảng `WorkTasks`, `Requests` có cột `WorkflowVersionId` nhưng bị chặn | N/A | Bị chặn bởi `task.WorkflowVersionId is not null -> Deny` | Xây dựng `WorkflowExecutionEngine` thực tế để điều khiển Task/Request chạy qua các bước quy trình | **P0** |

---

## 2. Các Rủi Ro Nghiệp Vụ Cốt Lõi Cần Xử Lý Ngay

1. **Lỗ hổng Phân quyền & Rò rỉ Platform Role (P0)**:
   - `WorkflowLibraryStore.GetActiveRolesAsync`: Truy vấn `(r.TenantId == tenantId || r.IsSystem)`. Vai trò `PLATFORM_ADMIN` (có `IsSystem = true`) bị trả về trong danh sách lựa chọn của tenant.
   - Cần lọc loại bỏ toàn bộ vai trò có phạm vi `PermissionScope.Platform` hoặc tên `PLATFORM_ADMIN`. Cấm validate/publish workflow nếu tham chiếu platform role.
2. **Lỗi Mặc định Gán Quyền Cấp Cao (P0)**:
   - Frontend `workflow-designer.ts` khi thêm node APPROVAL tự động lấy `this.roles[0]` (thường là `COMPANY_ADMIN`). Phải để trạng thái chưa cấu hình và bắt buộc quản trị viên chọn rõ ràng.
3. **DECISION Không Thể Cấu Hình (P0)**:
   - `workflow-designer.html` hoàn toàn không có khối `@if (node.type === 'DECISION')` trong bảng thuộc tính Inspector. Người dùng vẽ node DECISION nhưng không thể nhập điều kiện hoặc gán nhánh!
4. **Động cơ Thực thi Workflow Bị Ngắt Kết Nối (P0)**:
   - Trong `TaskWorkflowEngine.cs`, mọi thao tác trên Task có `WorkflowVersionId != null` đều bị từ chối (`TaskTransitionDenial.WorkflowDenied`). Cần xây dựng engine thực thi thực thụ cho Workflow.

---

## 3. Lộ Trình Triển Khai Chi Tiết Từng Phase

- **Phase 1**: Sửa triệt để rò rỉ `PLATFORM_ADMIN`, thiết kế mô hình phân công (`DEPARTMENT_QUEUE`, `BUSINESS_ROLE`, `SPECIFIC_USER`, `DIRECT_MANAGER`, `DEPARTMENT_MANAGER`, `REQUESTER`).
- **Phase 2**: Hoàn thiện toàn bộ thuộc tính và Inspector của 6 node (START, END, DEPARTMENT, APPROVAL, CONFIRMATION, DECISION). Xây dựng Condition Builder.
- **Phase 3**: Chuẩn hóa hệ thống Edge, Transition, Outcome code và Condition mappings, nâng cấp Backend `WorkflowValidator`.
- **Phase 4**: Xây dựng Runtime Workflow Execution Engine kết nối thực tế với Task và Request lifecycle.
- **Phase 5**: Hoàn thiện giao diện, Inspector groups, Canvas interaction, Undo/Redo, và kiểm thử nghiệm thu toàn diện A, B, C, D, E.

