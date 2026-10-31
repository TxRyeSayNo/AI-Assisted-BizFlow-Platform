# View B: Sơ đồ Kiến trúc Container và Runtime (Container & Runtime Architecture — C4 Level 2)

- **Mã tài liệu:** `SSS-DIAG-02`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 4, Mục 6 & Mục 8
- **Trạng thái:** Phân tách rõ **Kiến trúc Mục tiêu (`TARGET ARCHITECTURE`)** và **Triển khai Hiện tại trong Repository (`CURRENT IMPLEMENTATION`)**

---

## 1. Sơ đồ Container Kiến trúc Mục tiêu (Target Container Architecture)

Trong kiến trúc mục tiêu (`docs/00-analysis/bizflow-system-analysis.md`, `ADR-0001..0012`), hệ thống bao gồm ứng dụng Frontend SPA, khối Backend Modular Monolith, tiến trình Background Worker (Hangfire) chạy định kỳ/bất đồng bộ, cùng 3 dịch vụ lưu trữ dữ liệu (PostgreSQL 18, Redis, MinIO/S3).

```mermaid
flowchart TB
    subgraph ClientTier["Tầng Giao diện Người dùng (Client Tier)"]
        SPA["Angular 22 Single-Page Application (MOD-FE-001)\n(TypeScript, RxJS, Angular Router, SignalR Client)\n[Guards: sessionGuard, tenantGuard, permissionGuard]"]
    end

    subgraph AppTier["Tầng Ứng dụng Backend (ASP.NET Core .NET 10 Modular Monolith)"]
        API["BizFlow.Api — Presentation & Real-Time Host\n(31 REST Controllers, ApiExceptionHandler, RateLimiter,\nClaimsTenantContext, TenantMembershipAuthorizer,\nNotificationHub [/hubs/notifications])"]
        APP["BizFlow.Application — Use-Case & Orchestration Layer\n(Services, Commands/Queries, DTOs, Validators,\nTaskWorkflowEngine, AiAssistantService, AiContextReader,\nIAiToolRegistry, IAiActionDispatcher)"]
        DOM["BizFlow.Domain — Core Domain Model\n(Entities, Value Objects, Enums, Domain Invariants\nfor Tenant, User, WorkTask, RequestRecord, Workflow, SLA)"]
        INF["BizFlow.Infrastructure — Persistence & Adapters\n(BizFlowDbContext, EF Core Query Filters, SaveChangesAsync Guards,\nOpenAiModelClient, BizFlowRedisCacheService,\nS3CompatibleStorageProvider, LocalStorageProvider)"]
        BGW["Background Worker Host (MOD-BG-001 — Target: Hangfire)\n(SLA Breach Evaluators, Outbound Email Dispatcher,\nNotification Outbox Processor, Archival Jobs, Export Jobs)"]
    end

    subgraph DataTier["Tầng Lưu trữ Dữ liệu & Trạng thái (Data & State Tier)"]
        PG[("PostgreSQL 18\n(Primary Relational DB)\n[Target: 43 Tables | Current: 36 Tables]\nRow-level TenantId Isolation")]
        REDIS[("Redis\n(Cache & Ephemeral State)\n[IDistributedCache, Rate Limit,\nSignalR Scale-out Backplane (Target)]")]
        OBJ[("MinIO / S3-Compatible Storage\n(Private Bucket Object Store)\n[Tenant-prefixed Object Keys]")]
    end

    subgraph ExtTier["Dịch vụ Bên ngoài (External Services)"]
        OAI["OpenAI API\n(HTTPS REST)"]
        SMTP["SMTP Server\n(TLS Email Relay)"]
    end

    SPA -->|"HTTPS REST (/api/*)\nBearer JWT + X-Correlation-Id"| API
    SPA <-->|"WSS SignalR (/hubs/notifications)\nReal-time Push Events"| API

    API --> APP
    BGW --> APP
    APP --> DOM
    APP --> INF
    INF --> DOM

    INF -->|"Npgsql / EF Core 10\n(Async I/O, HasQueryFilter, SaveChangesAsync Guard)"| PG
    INF -->|"StackExchange.Redis\n(BizFlowRedisCacheService)"| REDIS
    INF -->|"AWS SDK for S3 / Local FS\n(IObjectStorageProvider)"| OBJ
    INF -->|"HttpClient (Timeout 45s)\n(OpenAiModelClient)"| OAI
    BGW -.->|"Target: Poll & Dispatch EmailQueueItems"| SMTP
    BGW -.->|"Target: Hangfire Storage / Job Locks"| PG
```

---

## 2. Sơ đồ Container Triển khai Hiện tại trong Repository (Current Repository Implementation)

Đối chiếu với `backend/BizFlow.Api/Program.cs`, `BizFlow.slnx` và `backend/BizFlow.Infrastructure/Persistence/BizFlowDbContext.cs`, cấu trúc hiện tại đã hiện thực hóa đầy đủ 4 project (`BizFlow.Api`, `BizFlow.Application`, `BizFlow.Domain`, `BizFlow.Infrastructure`) và `frontend/`, nhưng **chưa đăng ký Hangfire Worker trong `Program.cs`** và **còn thiếu 7 bảng dữ liệu của Approval/Routing/SLA runtime**.

```mermaid
flowchart LR
    subgraph ImplementedNow["ĐÃ TRIỂN KHAI TRONG REPOSITORY (IMPLEMENTED)"]
        FE_CUR["frontend/ (Angular 22 SPA)\n28 Feature Routes + Guards + Auth/Error Interceptors"]
        API_CUR["BizFlow.Api (.NET 10 WebApplication)\n- Serilog Compact JSON + TraceId\n- AddBizFlowAuthentication (JWT)\n- TenantMembershipAuthorizer\n- RateLimiter (Auth, AI, Public)\n- 31 REST Controllers\n- NotificationHub (/hubs/notifications)\n- /health/live & /health/ready"]
        APP_CUR["BizFlow.Application\n- TaskWorkflowEngine (ADR-0001..0004)\n- AiAssistantService + AiContextReader (ADR-0008)\n- Request/Task/Org/Service/SLA/Report Services"]
        INF_CUR["BizFlow.Infrastructure\n- BizFlowDbContext (36 DbSets, 34 Migrations)\n- Fail-Closed EF Query Filter + SaveChangesAsync Guard\n- BizFlowRedisCacheService\n- S3CompatibleStorageProvider + LocalStorageProvider\n- OpenAiModelClient (HttpClient 45s)"]
    end

    subgraph GapPending["CHƯA KÍCH HOẠT HOẶC CHƯA ĐẦY ĐỦ (PLANNED / PARTIAL GAP)"]
        GAP_BG["MOD-BG-001: Hangfire Server / BackgroundService\nChưa được đăng ký trong Program.cs\n(EmailQueueItems, NotificationQueueItems, ReportExportJobs\nvà SLA background timers chưa có tiến trình quét tự động)"]
        GAP_DB["7 Bảng DB chưa có trong BizFlowDbContext:\n1. RoutingRule\n2. ApprovalRule\n3. ApprovalRuleVersion\n4. ApprovalInstance\n5. ApprovalStepInstance\n6. SLAState\n7. SLAEvent"]
    end

    FE_CUR -->|"REST + SignalR"| API_CUR
    API_CUR --> APP_CUR
    APP_CUR --> INF_CUR
    APP_CUR -.->|"Cần bổ sung để khép kín vòng đời Request/SLA/Outbox"| GAP_BG
    INF_CUR -.->|"Cần bổ sung Migration & Entity"| GAP_DB
```

---

## 3. Bảng Đối chiếu Thành phần Container: Mục tiêu vs. Hiện trạng

| Thành phần Container | Công nghệ Mục tiêu | Hiện trạng trong Codebase (`d:\FA26\AI-Assisted-BizFlow-Platform`) | Trạng thái | Ghi chú Kiến trúc |
| :--- | :--- | :--- | :--- | :--- |
| **Frontend Web App (`MOD-FE-001`)** | Angular 22, TypeScript, TailwindCSS/SCSS, SignalR Client | Dự án `frontend/` có đầy đủ cấu trúc core (`auth`, `guards`, `interceptors`, `layout`, `notifications`) và các feature modules (`requests`, `tasks`, `settings`, `reports`, `dashboard`, `records`, `platform`, `audit`). | `IMPLEMENTED` | Có sự khác biệt nhỏ về tên chuỗi permission ở một số route so với tài liệu cũ (được ghi nhận tại `OD-019`). |
| **API & Real-Time Host (`BizFlow.Api`)** | ASP.NET Core .NET 10, REST Controllers, SignalR Hub | `Program.cs` cấu hình Serilog, JWT Auth, `ClaimsTenantContext`, `TenantMembershipAuthorizer`, Rate Limiting, `ApiExceptionHandler`, `/health/live`, `/health/ready`, `/hubs/notifications` và 31 Controllers. | `IMPLEMENTED` | Chạy trong một tiến trình web duy nhất. |
| **Application & Domain (`BizFlow.Application`, `BizFlow.Domain`)** | C# .NET 10, Clean/Layered Modular Monolith | Triển khai đầy đủ `TaskWorkflowEngine`, `AiAssistantService`, `AiContextReader`, `AiToolRegistry`, `AiActionDispatcher`, cùng các dịch vụ quản trị Tenant, Org, Service, Task, Request, Audit. | `IMPLEMENTED` (Core) / `PARTIAL` (Approval/SLA runtime) | Logic chuyển trạng thái Task và kiểm soát AI (`ADR-0001..0008`) đã có mặt và có Unit/Integration Tests. |
| **Persistence & Adapters (`BizFlow.Infrastructure`)** | EF Core 10 (Npgsql), StackExchange.Redis, AWS S3 SDK, HttpClient | `BizFlowDbContext` có 36 `DbSet`, 34 migrations, `HasQueryFilter` fail-closed theo `TenantId`, và `SaveChangesAsync` chặn xóa cứng / sửa cột bất biến. | `IMPLEMENTED` (36/43 bảng) | Còn thiếu 7 bảng (`RoutingRule`, `ApprovalRule*`, `Approval*Instance`, `SLAState`, `SLAEvent`). |
| **Background Worker (`MOD-BG-001`)** | Hangfire Server (PostgreSQL/Redis storage) hoặc `.NET BackgroundService` | Các bảng hàng đợi (`EmailQueueItems`, `NotificationQueueItems`, `ReportExportJobs`, `AiExecutionLogs`) đã có trong DB, nhưng `Program.cs` **chưa đăng ký** Hangfire hay `IHostedService` quét hàng đợi và kiểm tra vi phạm SLA. | `PROPOSED` / `NOT YET WIRED` | Được theo dõi tại quyết định mở `OD-020` và `ADR-SSS-002`. |
