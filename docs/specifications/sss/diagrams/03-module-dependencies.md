# View C: Sơ đồ Phụ thuộc Module và Phân lớp Kiến trúc (Module Dependency Diagram — C4 Level 3)

- **Mã tài liệu:** `SSS-DIAG-03`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 6 & Mục 7; [MODULE_CATALOG.md](../MODULE_CATALOG.md)
- **Trạng thái:** `CONFIRMED` (Quy tắc phân lớp Clean Architecture kết hợp Modular Monolith không chu trình)

---

## 1. Sơ đồ Phân lớp Vật lý (.NET Assemblies & Frontend)

Ở mức độ biên dịch (Compile-time Assembly Dependencies), hệ thống tuân thủ nghiêm ngặt nguyên tắc **Dependency Inversion** của Clean Architecture: phụ thuộc luôn hướng vào trong về phía `BizFlow.Domain` và `BizFlow.Application`.

```mermaid
flowchart TB
    subgraph Presentation["Tầng Giao diện & Cổng giao tiếp (Presentation & Delivery)"]
        FE["MOD-FE-001: Angular 22 SPA\n(frontend/)"]
        API["BizFlow.Api\n(Controllers, Middleware, SignalR Hub, DI Composition Root)"]
    end

    subgraph ApplicationLayer["Tầng Ứng dụng (Use Cases & Contracts)"]
        APP["BizFlow.Application\n(Module Services, DTOs, Validators, Port Interfaces:\nITenantContext, IWorkflowEngine, IAiModelClient,\nIObjectStorageProvider, IBizFlowCacheService)"]
    end

    subgraph InfrastructureLayer["Tầng Hạ tầng & Tích hợp (Adapters & Persistence)"]
        INF["BizFlow.Infrastructure\n(BizFlowDbContext, EF Migrations, Redis, S3/Local Storage,\nOpenAiModelClient, ClaimsTenantContext, DatabaseReadinessProbe)"]
    end

    subgraph DomainLayer["Tầng Miền Nghiệp vụ Lõi (Enterprise Domain Rules)"]
        DOM["BizFlow.Domain\n(Entities, Enums, Value Objects, Domain Exceptions & Invariants)"]
    end

    FE -->|"HTTP REST / SignalR WSS"| API
    API -->|"Project Reference"| APP
    API -->|"Project Reference (DI Wiring Only)"| INF
    INF -->|"Project Reference (Implements Interfaces)"| APP
    INF -->|"Project Reference"| DOM
    APP -->|"Project Reference"| DOM
```

---

## 2. Sơ đồ Phụ thuộc Logic giữa 13 Module Nghiệp vụ Backend (Logical Module Dependencies)

Bên trong khối Modular Monolith, 13 module nghiệp vụ được phân chia thành **4 tầng khả năng (Capability Tiers)** tạo thành một đồ thị có hướng không chu trình (**Directed Acyclic Graph — DAG**). Không module nào ở tầng dưới được phép gọi ngược trực tiếp lên module ở tầng trên.

```mermaid
flowchart TB
    subgraph Tier4["Tầng 4: Trí tuệ Nhân tạo, Phân tích & Hỗ trợ Quyết định (Intelligence & Analytics Tier)"]
        MOD12["MOD-012: AI Assistant & Tooling\n(AiAssistantService, AiContextReader,\nAiToolRegistry, AiActionDispatcher)"]
        MOD11["MOD-011: Reporting & Dashboard\n(ReportingService, Dashboard, Export Jobs)"]
    end

    subgraph Tier3["Tầng 3: Điều phối Quy trình & Thực thi Công việc (Operational Execution Tier)"]
        MOD06["MOD-006: Request Management\n(Request Intake, Lifecycle, Links)"]
        MOD07["MOD-007: Workflow & Approval Engine\n(TaskWorkflowEngine, Workflow/Approval Rules)"]
        MOD05["MOD-005: Task Management & Execution\n(WorkTask, Assignments, Confirmations, Blockers)"]
        MOD08["MOD-008: SLA & Calendar Management\n(SlaProfiles, Holidays, WorkingHours, SLA Tracking)"]
        MOD10["MOD-010: Collaboration, Evidence & Archival\n(Comments, Attachments, CompletionEvidence, Archive)"]
    end

    subgraph Tier2["Tầng 2: Cấu hình Dịch vụ & Tổ chức (Tenant Configuration Tier)"]
        MOD04["MOD-004: Service Catalog & Form Builder\n(ServiceCategories, ServiceTypes, FormDefinitions)"]
        MOD03["MOD-003: Organization & Access Control\n(Departments, Users, Roles, Permissions, ManagementScope)"]
    end

    subgraph Tier1["Tầng 1: Nền tảng Đa Thuê bao, Định danh & Kiểm toán (Foundation & Cross-Cutting Tier)"]
        MOD01["MOD-001: Identity & Authentication\n(Sessions, JWT, RefreshTokens, PasswordReset)"]
        MOD02["MOD-002: Tenant & Workspace Management\n(Tenants, Workspaces, CompanyRegistrations, Settings)"]
        MOD09["MOD-009: Notification & Real-Time Hub\n(Notifications, NotificationQueue, EmailQueue, SignalR)"]
        MOD13["MOD-013: Audit & Compliance\n(AuditLogs, SecurityEvents, PlatformAudit)"]
    end

    %% Tier 4 dependencies
    MOD12 -->|"Đọc ngữ cảnh có kiểm soát quyền"| MOD05
    MOD12 -->|"Đọc ngữ cảnh có kiểm soát quyền"| MOD06
    MOD12 -->|"Đọc danh mục & cấu hình"| MOD04
    MOD12 -->|"Kiểm tra phạm vi quyền & Tenant"| MOD03
    MOD12 -->|"Ghi Audit / AI Execution Log"| MOD13

    MOD11 -->|"Tổng hợp số liệu chỉ đọc (Read-only)"| MOD05
    MOD11 -->|"Tổng hợp số liệu chỉ đọc (Read-only)"| MOD06
    MOD11 -->|"Tổng hợp số liệu SLA"| MOD08
    MOD11 -->|"Kiểm tra phạm vi phòng ban (BR-003)"| MOD03

    %% Tier 3 dependencies
    MOD06 -->|"Tra cứu Form & Danh mục Dịch vụ"| MOD04
    MOD06 -->|"Kích hoạt định tuyến & quy trình"| MOD07
    MOD06 -->|"Khởi tạo/liên kết WorkTask"| MOD05
    MOD06 -->|"Áp dụng SLA Profile"| MOD08
    MOD06 -->|"Lưu đính kèm, bình luận, lưu trữ"| MOD10

    MOD07 -->|"Điều phối chuyển trạng thái Task (ADR-0001..0004)"| MOD05
    MOD07 -->|"Tra cứu người duyệt & phòng ban"| MOD03
    MOD07 -->|"Phát thông báo bước quy trình"| MOD09
    MOD07 -->|"Ghi nhật ký chuyển trạng thái"| MOD13

    MOD05 -->|"Xác thực thành viên, phòng ban & quyền"| MOD03
    MOD05 -->|"Đính kèm bằng chứng hoàn thành"| MOD10
    MOD05 -->|"Tính hạn xử lý & cảnh báo SLA"| MOD08
    MOD05 -->|"Phát thông báo giao việc / xác nhận"| MOD09
    MOD05 -->|"Ghi TaskHistory & AuditLog"| MOD13

    MOD10 -->|"Kiểm tra quyền truy cập hồ sơ"| MOD03
    MOD10 -->|"Ghi Audit khi tải/xóa tệp & lưu trữ"| MOD13

    MOD08 -->|"Lấy múi giờ & cấu hình Tenant"| MOD02
    MOD08 -->|"Phát cảnh báo sắp quá hạn / quá hạn"| MOD09

    %% Tier 2 dependencies
    MOD04 -->|"Kiểm tra quyền cấu hình Dịch vụ"| MOD03
    MOD04 -->|"Thuộc phạm vi Tenant/Workspace"| MOD02
    MOD03 -->|"Thuộc phạm vi Tenant/Workspace"| MOD02
    MOD03 -->|"Liên kết tài khoản định danh"| MOD01
    MOD03 -->|"Ghi Audit thay đổi Role/Org"| MOD13

    %% Tier 1 dependencies
    MOD01 -->|"Tra cứu trạng thái Tenant"| MOD02
    MOD01 -->|"Gửi email OTP / Reset Password"| MOD09
    MOD01 -->|"Ghi SecurityEvent & AuditLog"| MOD13
```

---

## 3. Các Quy tắc Kiến trúc Bất biến về Phụ thuộc Module (Architectural Dependency Rules)

1. **Cấm Phụ thuộc Ngược Chiều (No Upward Dependencies):**
   - Các module Tầng 1 (`MOD-001`, `MOD-002`, `MOD-009`, `MOD-013`) và Tầng 2 (`MOD-003`, `MOD-004`) **tuyệt đối không** được phụ thuộc vào logic nghiệp vụ của Tầng 3 (`Request`, `Task`, `Workflow`) hoặc Tầng 4 (`AI`, `Reporting`).
2. **Độc quyền Ghi Trạng thái Vòng đời Task (`ADR-0001` & `BizFlowDbContext.ValidateTaskWorkflowMutations`):**
   - Mọi thay đổi lên các trường điều khiển vòng đời của `WorkTask` (`Status`, `WorkflowInstanceId`, `WorkflowStepId`, `CompletedAt`, `CancelledAt`, `ArchivedAt`) bắt buộc phải đi qua `IWorkflowEngine` (`TaskWorkflowEngine` thuộc `MOD-007`). Nếu một service khác tự ý chỉnh sửa các cột này và gọi `SaveChangesAsync`, tầng Persistence sẽ ném lỗi `InvalidOperationException` (`WorkTask.ClearWorkflowMutationMarks`).
3. **Cô lập Tác động của AI (`ADR-0008`):**
   - `MOD-012` (`AI Assistant`) chỉ được phép đọc dữ liệu thông qua `AiContextReader` (đã áp dụng bộ lọc quyền của người gọi) và thực thi các đề xuất không phá hủy qua `IAiActionDispatcher`. `MOD-012` **không** được phép ghi trực tiếp vào các bảng nghiệp vụ lõi hay tự ý chuyển trạng thái `WorkTask` / `RequestRecord` khi chưa có xác nhận của người dùng (`Human-in-the-Loop`).
4. **Tính Độc lập của Kiểm toán (`MOD-013`):**
   - `AuditLog`, `SecurityEvent`, `TaskHistory` và `RequestHistory` là các bảng chỉ ghi thêm (`Append-Only`). Không module nào được phép gọi lệnh `Update` hoặc `Delete` lên các bảng này.
