# View E: Sơ đồ Kiến trúc Triển khai và Hạ tầng (Deployment & Infrastructure View)

- **Mã tài liệu:** `SSS-DIAG-05`
- **Thuộc bộ tài liệu:** [SSS_v1.0_DRAFT.md](../SSS_v1.0_DRAFT.md) — Mục 17 & Mục 18
- **Trạng thái:** Phân biệt rõ **Môi trường Triển khai Mục tiêu (`TARGET PRODUCTION DEPLOYMENT`)** và **Môi trường Phát triển Hiện tại (`CURRENT LOCAL / REPO TOPOLOGY`)**

---

## 1. Sơ đồ Triển khai Mục tiêu cho Môi trường Production/Staging (`Target Deployment Topology`)

Kiến trúc triển khai mục tiêu sử dụng mô hình container hóa, đặt sau tầng Reverse Proxy / Ingress chấm dứt TLS (HTTPS/WSS), cho phép mở rộng ngang (Horizontal Scaling) đối với `BizFlow.Api` nhờ lưu trữ trạng thái phiên/cache trên Redis và dữ liệu bền vững trên PostgreSQL 18 cùng S3/MinIO.

```mermaid
flowchart TB
    subgraph Internet["Mạng Bên ngoài (Public Internet / Corporate Network)"]
        BROWSER["Trình duyệt Người dùng\n(Chrome, Edge, Firefox, Safari)"]
    end

    subgraph DMZ["Tầng Cổng & Phân phối (Ingress & Edge Layer)"]
        PROXY["Reverse Proxy / Ingress Controller\n(Nginx / Cloud Load Balancer)\n[TLS 1.2/1.3 Termination, Static SPA Hosting, Rate-Limit Edge]"]
    end

    subgraph AppVPC["Mạng Nội bộ Ứng dụng (Private Application Subnet)"]
        API1["BizFlow.Api Container Instance #1\n(ASP.NET Core .NET 10)\n[REST API + SignalR Hub + Health Probes]"]
        API2["BizFlow.Api Container Instance #N\n(ASP.NET Core .NET 10)\n[Stateless Horizontal Scale-out]"]
        WORKER["BizFlow Worker Container (MOD-BG-001 — Target)\n(Hangfire Server / .NET Host)\n[SLA Timers, Email/Notification Outbox, Exports]"]
    end

    subgraph DataVPC["Mạng Nội bộ Dữ liệu (Isolated Data Subnet)"]
        PG_PRI[("PostgreSQL 18 Primary\n(Transactional Relational Store)\n[43 Target Tables, Daily Backups]")]
        REDIS_CL[("Redis Instance / Cluster\n(Distributed Cache, Rate-Limit Counters,\nSignalR Backplane)")]
        MINIO_S3[("MinIO / AWS S3 Bucket\n(Private Object Storage)\n[Attachments & Completion Evidence]")]
    end

    subgraph ExternalCloud["Dịch vụ Đám mây Bên ngoài (External SaaS Endpoints)"]
        OPENAI_CLOUD["OpenAI API\n(api.openai.com:443)"]
        SMTP_CLOUD["SMTP Relay Service\n(Port 587 / 465 TLS)"]
    end

    BROWSER -->|"HTTPS (443) & WSS (443)"| PROXY
    PROXY -->|"HTTP/WS Nội bộ"| API1
    PROXY -->|"HTTP/WS Nội bộ"| API2

    API1 -->|"TCP 5432 (Npgsql Pool)"| PG_PRI
    API2 -->|"TCP 5432 (Npgsql Pool)"| PG_PRI
    WORKER -->|"TCP 5432"| PG_PRI

    API1 <-->|"TCP 6379"| REDIS_CL
    API2 <-->|"TCP 6379"| REDIS_CL
    WORKER <-->|"TCP 6379"| REDIS_CL

    API1 -->|"HTTP/HTTPS (S3 API)"| MINIO_S3
    API2 -->|"HTTP/HTTPS (S3 API)"| MINIO_S3
    BROWSER -.->|"Direct Upload/Download qua Presigned URL (Tùy chọn cấu hình)"| MINIO_S3

    API1 -->|"HTTPS 443 (Outbound)"| OPENAI_CLOUD
    API2 -->|"HTTPS 443 (Outbound)"| OPENAI_CLOUD
    WORKER -->|"SMTP TLS (Outbound)"| SMTP_CLOUD
```

---

## 2. Sơ đồ Triển khai Hiện tại trong Repository (`Current Local / Developer Topology`)

Trong kho mã nguồn hiện tại (`d:\FA26\AI-Assisted-BizFlow-Platform`), chưa có tệp `Dockerfile` hoặc `docker-compose.yml` được commit vào nhánh chính (theo dõi tại `OD-014`). Hệ thống được chạy trực tiếp trên máy trạm phát triển hoặc CI runner với cấu hình fallback linh hoạt trong `Program.cs` và `appsettings.json`.

```mermaid
flowchart LR
    subgraph DevMachine["Máy Trạm Phát triển / CI Environment (Current Repo)"]
        NG_DEV["Angular CLI Dev Server\n(npm start -> http://localhost:4200)\nHoặc Static Build (dist/)"]
        DOTNET_HOST["dotnet run (BizFlow.Api)\n(http://localhost:5120)\n- REST API (/api/*)\n- SignalR (/hubs/notifications)\n- Health (/health/live, /health/ready)"]
        LOCAL_FS[("LocalStorageProvider Fallback\n(App_Data/storage/{tenantId}/...)\n[Khi Storage:Provider = Local]")]
    end

    subgraph LocalInfra["Dịch vụ Hạ tầng Cục bộ (Local Services)"]
        PG_LOCAL[("PostgreSQL (localhost:5432)\nDatabase: bizflow_db\n36 Tables via 34 EF Migrations")]
        REDIS_LOCAL[("Redis (localhost:6379)\nKey Prefix: bizflow:")]
        MINIO_LOCAL[("MinIO / S3 (Tùy chọn)\n[Khi Storage:Provider = S3 hoặc MinIO]")]
    end

    NG_DEV -->|"CORS HTTP + WS"| DOTNET_HOST
    DOTNET_HOST -->|"EF Core Npgsql"| PG_LOCAL
    DOTNET_HOST -->|"StackExchange.Redis"| REDIS_LOCAL
    DOTNET_HOST -->|"Mặc định trong appsettings.json"| LOCAL_FS
    DOTNET_HOST -.->|"Khi cấu hình S3/MinIO"| MINIO_LOCAL
```

---

## 3. Đặc tả Cổng Giao tiếp, Điểm Kiểm tra Sức khỏe (Health Probes) và Cơ chế Dự phòng

| Thành phần / Endpoint | Giao thức & Cổng Mặc định | Mục đích Vận hành | Hiện trạng trong `Program.cs` |
| :--- | :--- | :--- | :--- |
| **`GET /health/live`** | HTTP/HTTPS (`5120` local / `443` prod) | Kiểm tra tiến trình `BizFlow.Api` còn sống (Liveness Probe cho Kubernetes/Container orchestrator). Luôn trả về `200 OK` kèm `{ status: "Live", timestampUtc }` mà không phụ thuộc DB. | `IMPLEMENTED` (`Program.cs` dòng 120–125) |
| **`GET /health/ready`** | HTTP/HTTPS (`5120` local / `443` prod) | Kiểm tra hệ thống sẵn sàng nhận lưu lượng (Readiness Probe). Gọi `DatabaseReadinessProbe` kiểm tra kết nối PostgreSQL; trả về `200 OK` nếu sẵn sàng hoặc `503 Service Unavailable` nếu DB mất kết nối. | `IMPLEMENTED` (`Program.cs` dòng 127–148) |
| **`/hubs/notifications`** | WebSocket / SignalR (`WSS`) | Kênh đẩy thông báo thời gian thực tới trình duyệt; bật `CloseOnAuthenticationExpiration = true` để tự động ngắt kết nối khi JWT hết hạn. | `IMPLEMENTED` (`Program.cs` dòng 150–153) |
| **Chuyển đổi Lưu trữ Tệp (`Storage:Provider`)** | Cấu hình `Local` vs `S3` / `MinIO` | Cho phép phát triển và chạy kiểm thử tự động bằng `LocalStorageProvider` mà không bắt buộc dựng MinIO, đồng thời chuyển sang `S3CompatibleStorageProvider` trên Staging/Production chỉ bằng biến môi trường. | `IMPLEMENTED` (`Program.cs` dòng 65–72) |
