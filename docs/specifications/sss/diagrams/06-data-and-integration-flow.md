# View F: Sơ đồ Sở hữu Dữ liệu, Cô lập Đa Thuê bao và Luồng Tích hợp (Data Ownership, Isolation & Integration Flow)

- **Mã tài liệu:** `SSS-DIAG-06`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 10, Mục 11, Mục 12 & Mục 13
- **Trạng thái:** Đối chiếu trực tiếp với `BizFlowDbContext.cs` (36 bảng đã triển khai / 43 bảng mục tiêu) và cơ chế phòng vệ đa lớp (`Defense-in-Depth`)

---

## 1. Sơ đồ Phân quyền Sở hữu Dữ liệu theo Module (Module Data Ownership Map)

Mỗi bảng dữ liệu trong PostgreSQL thuộc quyền sở hữu ghi (Write Ownership) của **một module duy nhất**. Các module khác chỉ được phép thay đổi dữ liệu thông qua Application Service / Domain Engine của module sở hữu hoặc đọc qua các truy vấn đã áp dụng bộ lọc `TenantId`.

```mermaid
flowchart TB
    subgraph MOD01_02["MOD-001 (Auth) & MOD-002 (Tenant)"]
        T_TEN["Tenants, Workspaces,\nTenantSettings, CompanyRegistrationRequests\n(4/4 Implemented)"]
        T_AUTH["PasswordCredentials, UserSessions,\nRefreshTokens, PasswordResetTokens\n(4/4 Implemented)"]
    end

    subgraph MOD03_DB["MOD-003: Organization & RBAC"]
        T_ORG["Users, Departments, Roles, Permissions,\nRolePermissions, UserRoles, DepartmentMemberships\n(7/7 Implemented)"]
    end

    subgraph MOD04_DB["MOD-004: Service Catalog & Forms"]
        T_SVC["ServiceCategories, ServiceTypes,\nFormDefinitions, FormFields\n(4/4 Implemented)"]
    end

    subgraph MOD05_DB["MOD-005: Task Management"]
        T_TSK["WorkTasks, TaskAssignments,\nTaskBlockers, TaskHistories, Confirmations\n(5/5 Implemented)"]
    end

    subgraph MOD06_DB["MOD-006: Request Management"]
        T_REQ["RequestRecords, RequestFieldValues,\nRequestHistories, RequestTaskLinks\n(4/4 Implemented)"]
    end

    subgraph MOD07_DB["MOD-007: Workflow & Approval"]
        T_WF_IMP["WorkflowDefinitions, WorkflowVersions,\nWorkflowSteps, WorkflowInstances\n(4 Implemented)"]
        T_WF_GAP["RoutingRules, ApprovalRules,\nApprovalRuleVersions, ApprovalInstances,\nApprovalStepInstances\n(5 NOT YET IMPLEMENTED)"]
    end

    subgraph MOD08_DB["MOD-008: SLA & Calendar"]
        T_SLA_IMP["SlaProfiles, Holidays, WorkingHours\n(3 Implemented)"]
        T_SLA_GAP["SLAStates, SLAEvents\n(2 NOT YET IMPLEMENTED)"]
    end

    subgraph MOD09_10_DB["MOD-009 (Notification) & MOD-010 (Collaboration)"]
        T_NOT["Notifications, NotificationPreferences,\nNotificationQueueItems, EmailQueueItems,\nNotificationConnections\n(5/5 Implemented)"]
        T_COL["Comments, Attachments,\nCompletionEvidences, ArchivedRecordViews\n(4/4 Implemented)"]
    end

    subgraph MOD11_12_13_DB["MOD-011 (Report), MOD-012 (AI) & MOD-013 (Audit)"]
        T_RPT["ReportExportJobs, ReportSnapshots\n(2/2 Implemented)"]
        T_AI["AiSuggestions, AiConfigs, AiExecutionLogs\n(3/3 Implemented)"]
        T_AUD["AuditLogs, SecurityEvents\n(2/2 Implemented)"]
    end

    T_TEN --> T_ORG
    T_ORG --> T_SVC
    T_SVC --> T_REQ
    T_REQ --> T_WF_IMP
    T_WF_IMP -.-> T_WF_GAP
    T_WF_IMP --> T_TSK
    T_TSK --> T_SLA_IMP
    T_SLA_IMP -.-> T_SLA_GAP
    T_TSK --> T_COL
    T_TSK --> T_NOT
    T_TSK --> T_RPT
    T_TSK --> T_AI
    T_TSK --> T_AUD
```

> **Lưu ý kiểm kê bảng dữ liệu (`BizFlowDbContext.cs`):**
> - Trong `BizFlowDbContext.cs` hiện có **36 `DbSet<T>`** đã được triển khai (bao gồm các bảng mở rộng kỹ thuật như `NotificationConnections`, `ReportSnapshots`, `AiExecutionLogs`, `SecurityEvents` tương ứng với các thực thể vận hành trong hệ thống).
> - Có **7 thực thể nghiệp vụ mục tiêu** trong `docs/00-analysis/bizflow-system-analysis.md` chưa được tạo bảng và migration: `RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance`, `SLAState`, `SLAEvent`.

---

## 2. Sơ đồ 5 Lớp Cô lập Đa Thuê bao và Bảo vệ Dữ liệu (`5-Layer Multi-Tenant Defense-in-Depth`)

```mermaid
flowchart TB
    REQ["Yêu cầu từ Người dùng / Trình duyệt\n(HTTP REST hoặc SignalR WSS)"]

    subgraph L1["Lớp 1: Cổng Giao diện & Xác thực Biên (Edge & Route Guard Layer)"]
        L1_FE["Angular Guards: sessionGuard, tenantGuard, permissionGuard"]
        L1_BE["ASP.NET Core: JwtBearer + ClaimsTenantContext + TenantMembershipAuthorizer\n(Kiểm tra User thuộc Tenant, Tenant đang Active, Session hợp lệ)"]
    end

    subgraph L2["Lớp 2: Kiểm soát Phạm vi Nghiệp vụ (Application Scope & Policy Layer)"]
        L2_POL["Kiểm tra Quyền theo Phạm vi (ADR-0011 & BR-003):\n- tasks.read.tenant (Toàn công ty)\n- tasks.read.managed (Phòng ban quản lý + phòng ban con)\n- tasks.read.assigned / own (Cá nhân)\n- Trả về 404 Not Found khi ngoài phạm vi (ADR-0012)"]
    end

    subgraph L3["Lớp 3: Bộ lọc Truy vấn Cơ sở Dữ liệu Tự động (EF Core Global Query Filter)"]
        L3_FLT["BizFlowDbContext.ConfigureTenantFilter<T>:\nHasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId)\n[Cơ chế Fail-Closed: Nếu thiếu UserId hoặc TenantId -> trả về tập rỗng]"]
    end

    subgraph L4["Lớp 4: Chốt chặn Ghi & Bất biến Dữ liệu (Persistence Write & Immutability Guard)"]
        L4_WRT["BizFlowDbContext.SaveChangesAsync Override:\n1. Cấm gọi SaveChanges() đồng bộ (NotSupportedException)\n2. Cấm xóa cứng (Hard Delete -> PERSISTENCE.HARD_DELETE_DENIED)\n3. Cấm sửa cột bất biến (TenantId, CreatedBy... -> PERSISTENCE.IMMUTABLE_FIELD)\n4. Kiểm tra WorkTask.HasWorkflowMutation & Confirmation Invariants"]
    end

    subgraph L5["Lớp 5: Cô lập Lưu trữ Tệp, Cache, Real-Time & Ngữ cảnh AI"]
        L5_S3["Object Storage: Khóa tệp phân vùng theo {tenantId}/..."]
        L5_RD["Redis Cache: Khóa cache phân vùng theo bizflow:{tenantId}:..."]
        L5_SR["SignalR Hub: Định tuyến theo kết nối đã xác thực của từng User/Tenant"]
        L5_AI["AiContextReader: Chỉ nạp dữ liệu cùng Tenant mà Caller có quyền đọc"]
    end

    REQ --> L1
    L1 --> L2
    L2 --> L3
    L3 --> L4
    L4 --> L5
```
