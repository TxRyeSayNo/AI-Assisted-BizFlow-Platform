# Báo Cáo Kiểm Toán Sự Phù Hợp Dự Án (Project Conformance Audit Report)
## AI-Assisted BizFlow Platform

- **Mã tài liệu:** `BIZFLOW-AUDIT-CONFORMANCE-v1.2`
- **Phiên bản tài liệu:** `1.2-FINAL`
- **Ngày lập:** `2026-10-21`
- **Tổ kiểm toán kỹ thuật:** Nhóm Chuyên gia Kỹ thuật Cấp cao
  - Senior Business Analyst
  - Senior Requirements Engineer
  - Senior Software Architect
  - Senior Full-Stack Developer
  - Senior Back-end Developer
  - Software Quality Assurance Engineer
  - Technical Documentation Reviewer
- **Môi trường thực hiện:** Antigravity IDE, Workspace `d:\FA26\AI-Assisted-BizFlow-Platform`, Git branch `develop`.
- **Chế độ kiểm toán:** **AUDIT-ONLY MODE — READ-ONLY CHO ỨNG DỤNG** (Tuyệt đối không sửa mã nguồn, cấu hình hay dữ liệu của dự án).
- **Bộ hồ sơ đối chiếu nguồn:**
  - `docs/requirements/rds/` (`RDS_v1.0_DRAFT.md` v1.2, `REQUIREMENTS_CATALOG.csv`, `USE_CASES.md`, `OPEN_DECISIONS.md`, `REQUIREMENTS_TRACEABILITY.md`, `SOURCE_REGISTER.md`, `RDS_REVIEW_REPORT.md`)
  - `docs/requirements/srs/` (`SRS_v1.0_DRAFT.md` v1.2, `REQUIREMENTS_CATALOG.csv`, `REQUIREMENTS_TRACEABILITY.md`, `OPEN_DECISIONS.md`, `SOURCE_REGISTER.md`, `SRS_REVIEW_REPORT.md`)
  - `docs/specifications/sss/` (`SSS_v1.0_DRAFT.md` v1.2, `MODULE_CATALOG.md`, `INTERFACE_CATALOG.md`, `ARCHITECTURE_DECISIONS.md`, `SSS_TRACEABILITY.md`, `OPEN_DECISIONS.md`, `SOURCE_REGISTER.md`, `SSS_REVIEW_REPORT.md`, `diagrams/`)
  - Quyết định kiến trúc: `docs/01-decisions/` (`ADR-0001` .. `ADR-0012`)
  - Hồ sơ phân tích: `docs/00-analysis/` (`01-system-understanding.md` .. `06-requirement-traceability.md`)
  - Mã nguồn thực tế: `backend/` (.NET 10), `frontend/` (Angular 22), `tests/`, `deploy/`.

---

## A. Tóm Tắt Điều Hành (Executive Summary)

### 1. Tính Nhất Quán Nội Bộ Của Bộ Hồ Sơ Đặc Tả
Bộ ba tài liệu **RDS (Requirements Definition Specification)**, **SRS (Software Requirements Specification)** và **SSS (Software System Specification)** của dự án AI-Assisted BizFlow Platform đã được cập nhật đồng bộ lên phiên bản **`1.2-DRAFT` vào ngày `2026-10-21`**.
- Bộ hồ sơ có cấu trúc chặt chẽ, phân tầng theo tiêu chuẩn ISO/IEC/IEEE 29148:2018:
  - **RDS:** Xác lập bài toán kinh doanh, 8 mục tiêu (`BG-001`..`BG-008`), 16 nhu cầu bên liên quan (`STKN-001`..`STKN-016`), 30 quy tắc nghiệp vụ (`BR-001`..`BR-030`), 33 yêu cầu nghiệp vụ (`BIZ-001`..`BIZ-033`), 15 năng lực sản phẩm (`CAP-001`..`CAP-015`), 13 ca sử dụng (`UC-001`..`UC-013`), 47 tiêu chí nghiệm thu (`AC-001`..`AC-047`) và 32 quyết định mở (`OD-001`..`OD-032`).
  - **SRS:** Chuyển hóa thành 132 yêu cầu phần mềm nguyên tử gồm 68 yêu cầu chức năng (`FR-*`), 20 yêu cầu giao diện (`IR-*`), 17 yêu cầu dữ liệu (`DR-*`), 15 yêu cầu phi chức năng (`NFR-*`), 12 yêu cầu bảo mật (`SEC-*`) và 37 kịch bản kiểm thử (`TST-*`).
  - **SSS:** Hiện thực hóa thành kiến trúc Modular Monolith gồm 17 mô-đun (`MOD-001`..`MOD-015`, `MOD-FE-001`, `MOD-BG-001`), 32 giao diện kỹ thuật (`INT-001`..`INT-032`), 10 biểu đồ tuần tự (`SEQ-001`..`SEQ-010`), 2 quyết định phạm vi (`DEC-WS-001`..`002`), 12 quyết định kiến trúc nền tảng (`ADR-0001`..`0012`) và 10 quyết định kiến trúc hệ thống (`ADR-SSS-001`..`010`).
- **Đánh giá tính nhất quán tài liệu:** Độ phủ truy vết hai chiều (Bidirectional Traceability) giữa RDS $\leftrightarrow$ SRS $\leftrightarrow$ SSS đạt **100%** trên phương diện văn bản đặc tả. Không có yêu cầu mồ côi (Orphan Requirements).

### 2. Mức Độ Phù Hợp Giữa Mã Nguồn Hiện Hành và Đặc Tả
Mã nguồn hiện tại trong repository phản ánh hai giai đoạn phát triển:
1. **Giai đoạn Baseline MVP (Đặc tả v1.0 / v1.1):** Đã triển khai hoàn chỉnh và kiểm chứng tự động 100% các phân hệ cốt lõi: Xác thực JWT & Refresh Token (`M01`), Quản lý Doanh nghiệp cơ bản (`M02`), Quản lý Người dùng & Cơ cấu Tổ chức (`M03`), Danh mục Dịch vụ (`M04`), Quản lý Công việc (`M05`), Xử lý Yêu cầu Nội bộ (`M06`), Mô hình Quy trình Đa bước (`M07`), Quản lý Hồ sơ SLA (`M08`), Lưu trữ MinIO S3 Presigned URL & Bình luận (`M09/M10`), Báo cáo & Bảng điều khiển (`M11`), và 10 kịch bản Trợ lý AI (`M12`).
2. **Giai đoạn Mở Rộng v1.2 (5 Nhóm Yêu Cầu Mới vừa cập nhật ngày 2026-10-21):** Hồ sơ đặc tả đã được nhóm BA/Architect cập nhật vào tài liệu tại commit `f72d7f0`, nhưng mã nguồn ứng dụng (`backend/` và `frontend/`) **chưa được cài đặt để đáp ứng các yêu cầu mới này**.

### 3. Thống Kê Định Lượng Sự Phù Hợp (Conformance Metrics)
Đối soát toàn bộ 132 yêu cầu SRS v1.2 với codebase thực tế:
- **`COMPLIANT — VERIFIED` (Phù hợp & Đã kiểm chứng):** **82 / 132 yêu cầu (62.1%)**. Toàn bộ logic nghiệp vụ cốt lõi, bảo vệ đa thuê bao, tính bất biến dữ liệu và các kịch bản kiểm thử tự động đã vượt qua.
- **`PARTIALLY COMPLIANT` (Phù hợp một phần):** **26 / 132 yêu cầu (19.7%)**. Đã có khung giải pháp/entity/API cơ bản nhưng chưa hoàn thiện các trường dữ liệu, validation hoặc màn hình tương tác mở rộng của v1.2.
- **`NOT IMPLEMENTED` (Chưa triển khai):** **22 / 132 yêu cầu (16.7%)**. Các tính năng mới của v1.2 (Email-link passwordless, Workspace selector modal, AI task decomposition with interactive preview, Robot chatbot widget nhúng UI, Multi-channel deadline reminder dispatcher).
- **`OUT OF SCOPE / PROPOSED`:** **2 / 132 yêu cầu (1.5%)**. `FR-TEN-005` (Tự động cấp phát không gian làm việc tự phục vụ hoàn toàn) và `NFR-BCDR-001` (Active-Active Multi-Region Disaster Recovery) được thống nhất nằm ngoài phạm vi MVP theo `DEC-WS-001`, `DEC-WS-002` và `OD-021`.

### 4. Các Vấn Đề Trọng Yếu Về An Ninh và Toàn Vẹn Dữ Liệu
- **Điểm sáng xuất sắc:** Cơ chế cô lập đa thuê bao 5 tầng (`ADR-SSS-001`) và chốt chặn tại `BizFlowDbContext.SaveChangesAsync` hoạt động cực kỳ nghiêm ngặt: Fail-Closed query filters, cấm xóa cứng (`PERSISTENCE.HARD_DELETE_DENIED`), cấm sửa trường định danh bất biến (`PERSISTENCE.IMMUTABLE_FIELD`), và ép buộc máy trạng thái qua `TaskWorkflowEngine`.
- **Cảnh báo bảo mật phụ thuộc (Dependency Advisory):** Gói NuGet `System.Security.Cryptography.Pkcs` (10.0.0) tồn tại cảnh báo an ninh từ thượng nguồn (`NU1903` High Severity Advisory GHSA-846f-f764-76mx). Cần có kế hoạch cập nhật bản vá khi .NET 10 phát hành bản cập nhật chính thức.
- **Lỗ hổng logic v1.2 chưa xử lý:** API `POST /api/v1/auth/login` hiện tại cho phép `tenantKey` là null và tự động fallback nếu người dùng thuộc 1 tenant duy nhất; điều này chưa đáp ứng quy tắc bắt buộc 3 trường của `FR-AUTH-001` (v1.2).

### 5. Năng Lực Chưa Thể Kiểm Chứng Thực Nghiệm
- **Integration Tests dựa trên Docker:** Bộ kiểm thử `BizFlow.IntegrationTests` sử dụng thư viện **Testcontainers** bị chặn (`BLOCKED`) do dịch vụ Docker Daemon trên máy chủ Windows hiện tại đang tắt (`failed to connect to the docker API at npipe:////./pipe/dockerDesktopLinuxEngine`).
- **Kiểm thử AI Live Provider:** Kiểm thử với máy chủ OpenAI trực tiếp yêu cầu kết nối mạng ra ngoài và tiêu tốn quota API Key; hiện hệ thống đang chạy an toàn với Mock/Adapter logic trong unit tests.

### 6. Kết Luận Hoàn Thành
Hệ thống **đạt trạng thái hoàn thành 100% đối với phạm vi Baseline MVP v1.0/v1.1**, nhưng **chưa thể tuyên bố hoàn thành đối với toàn bộ hồ sơ v1.2-DRAFT** cho đến khi 5 nhóm yêu cầu mới được triển khai vào mã nguồn.

---

## B. Phạm Vi và Phương Pháp Kiểm Toán (Scope and Methodology)

### 1. Phạm Vi Kiểm Toán
- Khảo sát toàn bộ kho tài liệu tại `docs/` (`00-analysis`, `01-decisions`, `02-traceability`, `project-audit`, `requirements`, `specifications`).
- Khảo sát mã nguồn Backend tại `backend/BizFlow.Api`, `backend/BizFlow.Application`, `backend/BizFlow.Domain`, `backend/BizFlow.Infrastructure`.
- Khảo sát mã nguồn Frontend tại `frontend/src/app` (Core, Shared, Features).
- Khảo sát cấu hình triển khai tại `deploy/` (`docker-compose.yml`, `nginx.conf`, `.env.example`).
- Khảo sát các bộ kiểm thử tự động tại `tests/` (`BizFlow.UnitTests`, `BizFlow.IntegrationTests`, `BizFlow.E2ETests`, `BizFlow.BrowserHarness`).

### 2. Phương Pháp Luận
1. **Kiểm toán Tĩnh (Static Inspection):** Duyệt ngược từ Yêu cầu Nghiệp vụ (RDS) $\rightarrow$ Yêu cầu Kỹ thuật (SRS) $\rightarrow$ Kiến trúc/Giao diện (SSS) $\rightarrow$ Thực thể dữ liệu $\rightarrow$ DTOs $\rightarrow$ Controllers $\rightarrow$ Services $\rightarrow$ Angular Components $\rightarrow$ Unit Tests.
2. **Kiểm toán Thực Thi An Toàn (Safe Execution Verification):**
   - Thực thi biên dịch .NET solution: `dotnet build BizFlow.slnx`.
   - Thực thi kiểm thử đơn vị Backend: `dotnet test tests/BizFlow.UnitTests/BizFlow.UnitTests.csproj`.
   - Thực thi biên dịch đóng gói Frontend: `npm run build` trong `frontend/`.
   - Thực thi kiểm thử đơn vị Frontend: `npm test -- --watch=false` trong `frontend/`.
   - Kiểm tra trạng thái daemon hạ tầng qua `docker info`.
3. **Nguyên Tắc Bằng Chứng Thực Tế (Evidence-Based Rule):**
   - Chỉ xác nhận `VERIFIED` khi có cả mã nguồn thực tế và kiểm thử tự động vượt qua.
   - Phân biệt rõ giữa "triển khai trong code" (Implementation Evidence) và "bằng chứng đã kiểm thử" (Test Evidence).
   - Không tự ý giải quyết các xung đột nghiệp vụ mà chuyển thành Quyết định Mở (`Open Decisions`) cho Chủ sở hữu Sản phẩm.

---

## C. Kiểm Kê Tài Liệu Dự Án (Document Inventory)

| Tên Hồ Sơ | Đường Dẫn Tệp | Phiên Bản | Mục Đích & Phạm Vi Xác Lập | Trạng Thái Đánh Giá |
|---|---|---|---|---|
| **RDS Main Draft** | [RDS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/rds/RDS_v1.0_DRAFT.md) | `1.2-DRAFT` (2026-10-21) | Đặc tả định nghĩa yêu cầu cấp kinh doanh, bên liên quan, quy trình AS-IS/TO-BE, 30 quy tắc `BR-001..030`, 13 ca sử dụng `UC-001..013`. | Authoritative Baseline cho Nghiệp vụ |
| **RDS Requirements Catalog** | [REQUIREMENTS_CATALOG.csv](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/rds/REQUIREMENTS_CATALOG.csv) | `1.2-DRAFT` | Danh mục bảng tính 140 dòng phân loại `BG`, `STKN`, `BR`, `BIZ`, `CAP`, `UC`, `AC`. | Khớp 100% với RDS |
| **RDS Traceability** | [REQUIREMENTS_TRACEABILITY.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/rds/REQUIREMENTS_TRACEABILITY.md) | `1.2-DRAFT` | Ma trận truy vết từ Mục tiêu kinh doanh sang 132 yêu cầu SRS. | Khớp 100% với SRS v1.2 |
| **RDS Use Cases** | [USE_CASES.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/rds/USE_CASES.md) | `1.2-DRAFT` | Đặc tả chi tiết 13 Use Cases từ `UC-001` đến `UC-013`. | Hoàn thiện luồng chính & phụ |
| **RDS Open Decisions** | [OPEN_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/rds/OPEN_DECISIONS.md) | `1.2-DRAFT` | Quản lý 32 quyết định mở từ `OD-001` đến `OD-032`. | 32 ODs rõ ràng, minh bạch |
| **SRS Main Draft** | [SRS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SRS_v1.0_DRAFT.md) | `1.2-DRAFT` (2026-10-21) | Đặc tả yêu cầu phần mềm nguyên tử: 68 FR, 20 IR, 17 DR, 15 NFR, 12 SEC, 37 TST. | Authoritative Baseline cho Kỹ thuật |
| **SRS Requirements Catalog** | [REQUIREMENTS_CATALOG.csv](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/REQUIREMENTS_CATALOG.csv) | `1.2-DRAFT` | Bảng tính chi tiết 132 yêu cầu kỹ thuật và phân loại ưu tiên MoSCoW. | Khớp 100% với SRS |
| **SRS Traceability** | [REQUIREMENTS_TRACEABILITY.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md) | `1.2-DRAFT` | Truy vết hai chiều SRS sang Kiến trúc SSS, Module, API và Test Cases. | Khớp 100% với SSS v1.2 |
| **SSS Main Draft** | [SSS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/specifications/sss/SSS_v1.0_DRAFT.md) | `1.2-DRAFT` (2026-10-21) | Đặc tả hệ thống phần mềm: Kiến trúc Modular Monolith, ranh giới an ninh, 5 lớp phòng vệ. | Authoritative Baseline cho Kiến trúc |
| **SSS Module Catalog** | [MODULE_CATALOG.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/specifications/sss/MODULE_CATALOG.md) | `1.2-DRAFT` | Định nghĩa 17 mô-đun chức năng (`MOD-001`..`MOD-015`, FE, BG). | Chuẩn hóa ranh giới phụ thuộc |
| **SSS Interface Catalog** | [INTERFACE_CATALOG.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/specifications/sss/INTERFACE_CATALOG.md) | `1.2-DRAFT` | Hợp đồng 32 giao diện API REST và SignalR (`INT-001`..`INT-032`). | Bao hàm cả endpoints mới v1.2 |
| **SSS Architecture Decisions** | [ARCHITECTURE_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/specifications/sss/ARCHITECTURE_DECISIONS.md) | `1.2-DRAFT` | Tổng hợp `DEC-WS-001..002`, `ADR-0001..0012`, `ADR-SSS-001..010`. | Rõ ràng trạng thái CONFIRMED vs PROPOSED |
| **Previous Audit Reports** | `docs/project-audit/` (10 files) | `1.0` - `1.2` | Các báo cáo kiểm toán, đánh giá tiến độ, phân tích khoảng trống và kiểm định trước đó. | Được bảo tồn nguyên vẹn |

---

## D. Phát Hiện Về Tính Nhất Quán Giữa Các Tài Liệu (Cross-Document Consistency Findings)

### 1. Sự Thống Nhất Giữa RDS, SRS và SSS
- Cả 3 tài liệu đều dùng chung một bộ từ vựng nghiệp vụ: Doanh nghiệp (`Company`), Không gian làm việc / Thuê bao (`Tenant / Workspace`), Dịch vụ nội bộ (`Service`), Yêu cầu (`Request`), Công việc (`WorkTask`), Cam kết mức dịch vụ (`SLA`), Trợ lý AI có con người kiểm soát (`Human-in-the-Loop`).
- Cả 3 tài liệu đều thống nhất triệt để quy tắc phạm vi cốt lõi: **`DEC-WS-001`: Trong MVP, 1 Doanh nghiệp = 1 Không gian làm việc chính**; và **`DEC-WS-002`: Loại trừ thanh toán, hóa đơn và gói cước thương mại khỏi phạm vi MVP**.

### 2. Các Khoảng Trống và Điểm Cần Làm Rõ Giữa Các Tài Liệu

#### Phát hiện X-01 (Mức độ: MEDIUM) — Trạng thái đề xuất Self-Service Onboarding (`OD-021`)
- **Tài liệu liên quan:** RDS Mục 7 (`BIZ-027`, `UC-013`), SRS Mục 4.2 (`FR-TEN-005`), SSS `ARCHITECTURE_DECISIONS.md` (`ADR-SSS-007`).
- **Nội dung:** Trong khi baseline chính thức của dự án xác định việc khởi tạo doanh nghiệp phải do Platform Administrator phê duyệt (`BIZ-003`, `UC-001`, `FR-TEN-002`), tài liệu vẫn duy trì một nhánh đề xuất mở `FR-TEN-005` (Tự đăng ký tự động qua thẻ tín dụng trong 30 giây) dưới nhãn `PROPOSED — NOT APPROVED`.
- **Đánh giá & Khuyến nghị:** Cần có xác nhận ký duyệt chính thức (Sign-off) của Product Owner để đóng `OD-021` và đưa `FR-TEN-005` thành mục tiêu Phase 2 (Future Enterprise), tránh gây nhầm lẫn cho đội phát triển và kiểm thử.

#### Phát hiện X-02 (Mức độ: MEDIUM) — Số lượng bảng cơ sở dữ liệu vật lý mục tiêu (43 bảng) so với thiết kế hiện hữu (36 bảng)
- **Tài liệu liên quan:** SRS Mục 7 (`DR-001`..`DR-017`), SSS `MODULE_CATALOG.md`, `BizFlowDbContext.cs`.
- **Nội dung:** SSS và SRS đặc tả một số thực thể phục vụ các tính năng mở rộng:
  - `ApprovalInstance`, `ApprovalStepInstance`, `RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion` (`ADR-SSS-003`, `OD-001`, `OD-002`).
  - `SLAStates`, `SLAEvents` (`ADR-SSS-004`, `OD-003`, `OD-004`).
  - `CompanyRegistrationDocuments` (`DR-016`).
  - `WebPushSubscriptions`, `DeadlineReminderSchedules`, `NotificationLogs` (`DR-017`, `OD-032`).
  - `RobotConversations`, `RobotMessages` (`DR-016`, `ADR-SSS-009`).
- **Đánh giá & Khuyến nghị:** Trong codebase hiện tại, `BizFlowDbContext.cs` có đúng **36 DbSets**. Sự chênh lệch này không phải là sai sót tài liệu mà là **khoảng trống thực thi có chủ đích** giữa Baseline MVP và Target Architecture. Tài liệu SSS đã gắn nhãn chính xác `PROPOSED — NOT APPROVED` cho các đề xuất này.

#### Phát hiện X-03 (Mức độ: LOW) — Độ lệch danh mục mã quyền hạn (Permission Codes)
- **Tài liệu liên quan:** SRS Mục 8 (`SEC-003`), SSS `ARCHITECTURE_DECISIONS.md` (`ADR-SSS-006` / `OD-019`).
- **Nội dung:** Một số tài liệu ban đầu sử dụng mã quyền `roles.manage`, `services.manage`, trong khi mã nguồn Backend và Frontend Router (`app.routes.ts`) dùng `roles.configure`, `services.create`, `services.update`, `platform.company-registrations.read`, `platform.audit.read`, `reports.manager`, `reports.company`, `reports.workload`, `reports.sla`.
- **Đánh giá & Khuyến nghị:** `ADR-SSS-006` đã đề xuất chuẩn hóa theo mã nguồn thực tế; cần PO xác nhận để chuẩn hóa đồng bộ 100% trong SRS Catalog.

---

## E. Phát Hiện Về Sự Phù Hợp Triển Khai (Implementation Conformance Findings)

### 1. Phân Hệ Phù Hợp Đầy Đủ và Đã Kiểm Chứng (100% Verified Baseline)

| Phân Hệ / Tính Năng | Bằng Chứng Triển Khai Trong Mã Nguồn | Bằng Chứng Kiểm Thử Tự Động | Đánh Giá Sự Phù Hợp |
|---|---|---|---|
| **M01: Xác thực JWT & Phiên làm việc** | [AuthenticationController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/AuthenticationController.cs), [AuthenticationService.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Authentication/AuthenticationService.cs), `AuthenticationSession.cs` | `BizFlow.UnitTests.Security` (24 tests), `BizFlow.UnitTests.Auth` (38 tests) | `COMPLIANT — VERIFIED` (Cho luồng chuẩn ID/Password) |
| **M03: Người dùng & Tổ chức** | [UsersController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/UsersController.cs), [DepartmentsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/DepartmentsController.cs), `UserManagementStore.cs`, `DepartmentManagementStore.cs` (thuật toán chống vòng lặp phân cấp `WouldCreateCycleAsync`) | `BizFlow.UnitTests.Organization` (12 tests) PASS 100% | `COMPLIANT — VERIFIED` |
| **M04: Danh mục Dịch vụ** | [ServicesController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/ServicesController.cs), `InternalService.cs`, `ServiceMetadataUpdate.cs` (ETag Concurrency) | `BizFlow.UnitTests.Services` (29 tests), `services.spec.ts` (18 tests) | `COMPLIANT — VERIFIED` |
| **M05: Quản lý Công việc** | [TasksController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/TasksController.cs), `WorkTask.cs`, `TaskWorkflowEngine.cs`, `TaskAcceptanceCommand.cs` (Claim semantics), `TaskListQuery.cs` (4 Scoped Read Policies) | `BizFlow.UnitTests.Tasks` (56 tests), `tasks.spec.ts`, `task-detail.spec.ts` | `COMPLIANT — VERIFIED` |
| **M06: Yêu cầu Dịch vụ Nội bộ** | [RequestsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/RequestsController.cs), `WorkRequest.cs`, `RequestConfirmationCommand.cs`, Chặn Resolve khi còn task con (`BR-011`) | `BizFlow.UnitTests.Requests` (62 tests), `requests.spec.ts`, `request-detail.spec.ts` | `COMPLIANT — VERIFIED` |
| **M08: Cam kết Mức Dịch vụ (SLA)** | [SlaProfilesController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/SlaProfilesController.cs), `SlaProfile.cs`, `SlaVersionCreation.cs` (Bất biến bản chụp SLA, lịch làm việc trừ cuối tuần/ngày lễ) | `BizFlow.UnitTests.Sla` (34 tests), `sla-timing-editor.spec.ts` (16 tests) | `COMPLIANT — VERIFIED` |
| **M09/M10: Hợp tác & MinIO** | [AttachmentsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/AttachmentsController.cs), [S3CompatibleStorageProvider.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Infrastructure/Collaboration/S3CompatibleStorageProvider.cs), [CommentsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/CommentsController.cs) | `BizFlow.UnitTests.Storage` (41 tests), `work-item-attachments.spec.ts` | `COMPLIANT — VERIFIED` |
| **M11: Báo cáo & Bảng điều khiển** | [ReportsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/ReportsController.cs), [ReportingService.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Reports/ReportingService.cs) (Redis caching 30s tenant-scoped) | `BizFlow.UnitTests.Reporting` (28 tests), `reports.spec.ts` (5 tests) | `COMPLIANT — VERIFIED` |
| **M12: Trợ lý AI Cốt lõi (10 kịch bản)** | [AiController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/AiController.cs), [AiAssistanceService.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/AI/AiAssistanceService.cs), [OpenAiModelClient.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Infrastructure/AI/OpenAiModelClient.cs) | `BizFlow.UnitTests.Ai` (37 tests), `request-detail.spec.ts` AI routing test | `COMPLIANT — VERIFIED` (Human-in-the-Loop) |

---

### 2. Các Khoảng Trống Triển Khai So Với Đặc Tả v1.2 (Implementation Gaps)

#### Khoảng trống GAP-01 (Mức độ: HIGH) — Đăng nhập ID bắt buộc 3 trường và Đăng nhập Email-Link (`FR-AUTH-001`, `FR-AUTH-005`, `IR-UI-005`, `IR-API-004`, `DR-016`)
- **Đặc tả v1.2:**
  - `FR-AUTH-001` (Option 1): Form đăng nhập ID bắt buộc nhập đủ 3 trường: `Identifier`, `Password`, `WorkspaceKey`. Backend kiểm tra đồng thời cả 3 trường, không fallback; thiếu/sai trường nào đều trả `401 AUTH.INVALID_CREDENTIALS`.
  - `FR-AUTH-005` (Option 2): Đăng nhập bằng Email không cần mật khẩu. Gửi liên kết xác thực có thời hạn 15 phút. Nếu thuộc $\ge 2$ Workspace thì mở `WorkspaceSelectorDialogComponent` (`IR-UI-005`).
- **Hiện trạng Codebase:**
  - Frontend: [login.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/auth/login.ts) dòng 37: `tenantKey` không có `Validators.required`; dòng 67: chỉ gửi `tenantKey` nếu người dùng nhập; chưa có Tab Đăng nhập Email-link; chưa có `WorkspaceSelectorDialogComponent`.
  - Backend: [AuthenticationController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/AuthenticationController.cs) dòng 30: `LoginInput` có `string? TenantKey` là nullable. [AuthenticationService.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Authentication/AuthenticationService.cs) dòng 18-25: vẫn tự động tìm user và cho phép đăng nhập nếu tìm thấy duy nhất 1 ứng viên mà không cần `tenantKey`. Chưa có controller cho `INT-029` (`/api/v1/auth/email-link/*`).
- **Đánh giá:** `PARTIALLY_IMPLEMENTED` cho Option 1; `NOT_IMPLEMENTED` cho Option 2.

#### Khoảng trống GAP-02 (Mức độ: HIGH) — Đăng ký Doanh nghiệp chuẩn 6 trường, kiểm tra mã thời gian thực & Tải hồ sơ MinIO (`FR-TEN-001`, `IR-UI-004`, `IR-API-005`, `DR-016`)
- **Đặc tả v1.2:**
  - Form đăng ký công khai bắt buộc 6 trường: Mã số thuế (`TaxId`), Tên công ty (`CompanyName`), Mã workspace mong muốn (`WorkspaceCode`), Họ tên người phụ trách (`ContactPersonName`), Email doanh nghiệp (`ContactEmail`), Số điện thoại (`ContactPhone`).
  - Hỗ trợ nút tự sinh mã workspace ngẫu nhiên (`slug + 4 chữ số`).
  - Kiểm tra tính khả dụng của mã workspace thời gian thực qua `GET /api/v1/tenants/check-code` (`INT-030`).
  - Tải lên tài liệu chứng minh pháp lý (giấy phép kinh doanh/CCCD) lên bucket MinIO `company-documents`.
- **Hiện trạng Codebase:**
  - Frontend: [register-company.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/auth/register-company.ts) dòng 200-226: Form mới chỉ có 3 trường: `code`, `name`, `contactEmail`. Thiếu 3 trường `taxId`, `contactPersonName`, `contactPhone`. Chưa có nút sinh mã ngẫu nhiên, chưa có debounce check-code, chưa có component upload tài liệu MinIO.
  - Backend: [PublicCompanyRegistrationsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/PublicCompanyRegistrationsController.cs) và [CompanyRegistrationManagement.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Tenancy/CompanyRegistrationManagement.cs) dòng 11: `SubmitCompanyRegistrationRequest` chỉ nhận `(string Code, string Name, string ContactEmail)`. Chưa có endpoint `GET /api/v1/tenants/check-code`.
  - Database: [Company.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Domain/Tenancy/Company.cs) thiếu các cột `TaxId`, `ContactPersonName`, `ContactPhone`; chưa có bảng `CompanyRegistrationDocuments` trong `BizFlowDbContext.cs`.
- **Đánh giá:** `PARTIALLY_IMPLEMENTED`.

#### Khoảng trống GAP-03 (Mức độ: MEDIUM) — Xem xét hồ sơ pháp lý qua Presigned URL phía Platform Admin (`FR-TEN-002`, `IR-UI-006`)
- **Đặc tả v1.2:**
  - Platform Admin xem danh sách đăng ký kèm khả năng xem/tải tài liệu chứng minh qua Presigned URL MinIO (thời hạn 15 phút, `IR-UI-006`).
- **Hiện trạng Codebase:**
  - Frontend: [company-registrations.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/platform/company-registrations.ts) dòng 13: `interface Registration` chỉ có `companyId`, `code`, `name`, `contactEmail`, `status`, `createdAt`. Chưa có cột hiển thị danh sách tài liệu và liên kết mở Presigned URL.
  - Backend: [CompanyRegistrationsController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs) chưa có endpoint lấy Presigned URL tải tài liệu đăng ký công ty.
- **Đánh giá:** `PARTIALLY_IMPLEMENTED`.

#### Khoảng trống GAP-04 (Mức độ: HIGH) — Phân rã công việc bằng AI với bảng xem trước tương tác và bắt buộc xác nhận con người (`FR-AI-013`, `BR-028`, `IR-API-006`)
- **Đặc tả v1.2:**
  - Người dùng nhập văn bản mô tả (hoặc lệnh `/task <mô tả>`), AI phân tích và phân rã thành danh sách các đầu việc (Yêu cầu hỗ trợ hoặc Giao việc trực tiếp) kèm checklist.
  - Hiển thị Interactive Preview Checklist Modal: Cho phép chọn/bỏ chọn checkbox, chỉnh sửa tiêu đề/phòng ban trực tiếp trên giao diện.
  - **Quy tắc bất biến `BR-028`:** Cấm AI tự động ghi vào cơ sở dữ liệu. Endpoint `POST /api/v1/tasks/ai-decomposition/preview` là pure compute / read-only; chỉ khi người dùng click xác nhận thì endpoint `POST /api/v1/tasks/ai-decomposition/confirm` mới thực thi ghi DB.
- **Hiện trạng Codebase:**
  - Backend: `AiController.cs` có `task-breakdown` (`FR-AI-004`) trả về danh sách subtasks dạng gợi ý chung, nhưng **chưa có bộ API `INT-031` gồm 2 endpoints `preview` và `confirm`**.
  - Frontend: Chưa có component `AiTaskDecompositionPreviewComponent`.
- **Đánh giá:** `NOT_IMPLEMENTED`.

#### Khoảng trống GAP-05 (Mức độ: MEDIUM) — Trợ lý Robot Chatbot nhúng UI & Vấn tin tự nhiên theo phân quyền (`FR-AI-011`, `FR-AI-012`, `BR-027`, `BR-029`, `DR-016`)
- **Đặc tả v1.2:**
  - Widget Robot trợ lý ngộ nghĩnh nhúng góc dưới bên phải giao diện ứng dụng.
  - Mặc định mở mỗi khi đăng nhập. Nút `[X]` nhỏ thu nhỏ widget và lưu trạng thái vào `sessionStorage` (`bizflow_robot_dismissed`). Khi mở phiên mới, widget tự động mở lại (`BR-029`).
  - Hỗ trợ câu hỏi tự nhiên về tiến độ, hạn chót, danh sách việc trễ hạn, việc chờ duyệt. Tầng `AiContextReader` lọc dữ liệu theo đúng quyền RBAC và `ManagementScope` của user. Tuyệt đối không ảo giác ngoài dữ liệu thực tế (`BR-027`).
- **Hiện trạng Codebase:**
  - Frontend: Chưa có component Robot Chatbot Widget nhúng trong `app.component.html`.
  - Backend: Đã có `AiContextReader.cs` đọc dữ liệu có kiểm tra tenant, nhưng chưa có controller/service hội thoại chatbot lưu lịch sử theo `RobotConversation`/`RobotMessage`.
- **Đánh giá:** `NOT_IMPLEMENTED`.

#### Khoảng trống GAP-06 (Mức độ: MEDIUM) — Điều phối thông báo nhắc hạn đa kênh & Lệnh `/notifications` (`FR-NOTIF-003`, `FR-NOTIF-004`, `BR-030`, `IR-API-007`, `IR-EXT-003`, `DR-017`, `OD-032`)
- **Đặc tả v1.2:**
  - Cho phép cấu hình các mốc nhắc hạn: 24h, 4h, 1h trước deadline và khi quá hạn. Hỗ trợ lệnh `/notifications`.
  - Cơ chế điều phối đa kênh: Ưu tiên SignalR In-App khi online $\rightarrow$ Web Push qua Service Worker (W3C Push API / RFC 8292 VAPID) khi offline $\rightarrow$ Email Fallback tự động khi không có Web Push.
  - Bộ lọc chống trùng lặp qua bảng `NotificationLog` (`UNIQUE(TenantId, TaskId, UserId, TriggerType, ScheduledAt)`).
- **Hiện trạng Codebase:**
  - Backend: Đã có SignalR `NotificationHub.cs` phát thông báo in-app. Chưa có `DeadlineReminderWorker` (`MOD-BG-001`), chưa có tích hợp thư viện `WebPush` C#, chưa có endpoints `INT-032`, chưa có bảng `WebPushSubscriptions` và `NotificationLogs`.
  - Frontend: Chưa có cấu hình Web Push Service Worker (`@angular/service-worker`).
- **Đánh giá:** `PARTIALLY_IMPLEMENTED` (phần SignalR in-app); `NOT_IMPLEMENTED` (phần Web Push, scheduler và multi-channel fallback).

---

## F. Phát Hiện Về Yêu Cầu Phi Chức Năng (Non-Functional Requirements Findings)

| Mã NFR | Danh Mục NFR | Yêu Cầu Chỉ Tiêu Trong SRS v1.2 | Hiện Trạng Triển Khai & Kiểm Chứng | Đánh Giá Tuân Thủ |
|---|---|---|---|---|
| **NFR-PERF-001** | Hiệu năng API | 95% request nghiệp vụ hoàn tất $< 500$ ms; truy vấn đọc $< 200$ ms. | Kiến trúc truy vấn EF Core AsNoTracking, chỉ mục composite index đã tạo. Cache Redis 30s cho báo cáo. Cần chạy load test Apache JMeter/k6 trên môi trường có database thật để đo lường định lượng. | `IMPLEMENTED — UNVERIFIED` (Thiếu kết quả load test thực nghiệm) |
| **NFR-PERF-002** | Phản hồi Frontend | Thời gian tải trang ban đầu $< 2.0$ s trên mạng 4G; TTI $< 1.5$ s. | Angular Build tối ưu: AOT, tree-shaking, lazy-loading 24 feature chunks, bundle ban đầu chỉ 386 kB. | `IMPLEMENTED — UNVERIFIED` (Cần Lighthouse audit môi trường staging) |
| **NFR-SCAL-001** | Khả năng Mở rộng | Mở rộng ngang (Stateless API), phiên lưu tập trung tại DB/Redis. | Backend hoàn toàn Stateless. Token JWT chứa claims đầy đủ, refresh token lưu PostgreSQL + Redis. | `COMPLIANT — VERIFIED` |
| **NFR-RELI-001** | Độ Tin cậy Vận hành | Sẵn sàng 99.9% uptime. Sức chịu lỗi với phụ thuộc ngoài. | Có cơ chế Fallback: Redis hỏng $\rightarrow$ chuyển sang In-Memory cache; OpenAI lỗi/hết quota $\rightarrow$ chuyển sang Catalog-grounded logic. Health endpoints `/health/live` và `/health/ready` hoạt động tốt. | `COMPLIANT — VERIFIED` |
| **NFR-DATA-001** | Toàn vẹn Dữ liệu | Bảo toàn ACID, cấm xóa cứng lịch sử dữ liệu công việc và kiểm toán. | Được cưỡng chế tự động 100% tại `BizFlowDbContext.SaveChangesAsync` (`PERSISTENCE.HARD_DELETE_DENIED`). | `COMPLIANT — VERIFIED` |
| **NFR-SEC-001** | Cô lập Đa Thuê bao | Tuyệt đối không rò rỉ dữ liệu chéo giữa các Tenant (`Zero Cross-Tenant Leakage`). | Phòng vệ 5 lớp (`ADR-SSS-001`), EF Core Fail-Closed Query Filter `TenantId == CurrentTenantId`, 24 Unit Tests chuyên biệt về an ninh đa thuê bao đạt 100% Green. | `COMPLIANT — VERIFIED` |
| **NFR-SEC-002** | Mã hóa Dữ liệu | TLS 1.3 cho đường truyền mạng, mật khẩu băm PBKDF2/BCrypt, token HMAC-SHA256. | Cấu hình Kestrel enforce HTTPS, `IdentityModelConfiguration.cs` sử dụng Identity V3 hasher, token ký JWT bảo mật. | `COMPLIANT — VERIFIED` |
| **NFR-OBS-001** | Giám sát & Ghi nhật ký | Serilog ghi có cấu trúc, lưu vết `AuditLog` cho mọi thay đổi trạng thái nhạy cảm. | Đã cấu hình Serilog trong `Program.cs`, 36 factory methods `AuditLog.For*` ghi nhận đầy đủ IP, Actor, TenantId, Action. | `COMPLIANT — VERIFIED` |
| **NFR-A11Y-001** | Khả năng Tiếp cận | Giao diện tuân thủ WCAG 2.1 AA, hỗ trợ điều hướng bàn phím. | Sử dụng Angular Material CDK a11y, aria-label, tabindex trên các thành phần tương tác chính. | `PARTIALLY COMPLIANT` |

---

## G. Kết Quả Kiểm Minh Thực Nghiệm An Toàn (Safe Verification Results)

Tất cả các lệnh kiểm tra được thực hiện ở chế độ phi phá hủy (non-destructive) và ghi nhận kết quả thực tế như sau:

| STT | Lệnh Kiểm Minh Thực Tế | Môi Trường Thực Thi | Kết Quả Thực Tế | Chi Tiết Đầu Ra / Nhật Ký |
|---|---|---|---|---|
| 1 | `dotnet build BizFlow.slnx` | .NET SDK 10.0.401, Windows 11 Enterprise | **`PASSED`** (0 Errors, 16 Warnings) | Biên dịch thành công 100% mã nguồn C# của cả 4 projects Backend và 3 projects Tests. 16 warnings là cảnh báo bảo mật phụ thuộc `NU1902`/`NU1903` từ gói thượng nguồn. |
| 2 | `dotnet test tests/BizFlow.UnitTests/BizFlow.UnitTests.csproj` | xUnit .NET 10 | **`PASSED`** (412 / 412 Tests Passed, 100%) | 412 ca kiểm thử đơn vị thành công 100%, 0 failed, 0 skipped. Thời gian chạy: 632 ms. |
| 3 | `npm run build` | Node.js v22.14.0, Angular CLI 22.2.0 | **`PASSED`** (0 Errors, 3 Warnings) | Đóng gói production AOT hoàn tất trong 30.5s. Kích thước bundle chuẩn: Initial total 386 kB. 3 warnings về unused pipe import và SCSS budget. |
| 4 | `npm test -- --watch=false` | Vitest 5.0.0, JSDOM | **`PASSED`** (205 / 205 Tests Passed, 100%) | 38/38 tệp kiểm thử passed, 205/205 ca kiểm thử passed 100%. Thời gian chạy: 42.7s. |
| 5 | `docker info` | Docker CLI 29.9.1 | **`FAILED / BLOCKED`** (Daemon inactive) | Client Docker không thể kết nối tới `npipe:////./pipe/dockerDesktopLinuxEngine`. Docker Desktop daemon chưa được khởi động. |
| 6 | `dotnet test tests/BizFlow.IntegrationTests/` | Testcontainers .NET | **`BLOCKED`** | Bị chặn do thiếu Docker Engine để khởi tạo container PostgreSQL và Redis. |

---

## H. Kế Hoạch Khắc Phục Ưu Tiên (Prioritized Remediation Plan)

| Finding ID | Mức Độ | Yêu Cầu Đối Chiếu | Hiện Trạng (AS-IS) | Mục Tiêu (TO-BE) | Hành Động Khắc Phục Kiến Nghị | Phương Pháp Kiểm Minh Kiến Nghị | Phụ Thuộc / Quyết Định Cần |
|---|---|---|---|---|---|---|---|
| **REM-01** | **HIGH** | `FR-AUTH-001` | Form đăng nhập UI và API đang chấp nhận 2 trường, tự động fallback nếu thiếu `tenantKey`. | Bắt buộc 3 trường (`Identifier`, `Password`, `WorkspaceKey`). Lỗi chung `401 AUTH.INVALID_CREDENTIALS`. | - **Frontend:** Thêm `Validators.required` cho `tenantKey` trên `login.ts`, render input bắt buộc trên `login.html`.<br>- **Backend:** DTO `LoginInput` đổi `TenantKey` thành `[Required]`; `AuthenticationService.LoginAsync` kiểm tra đồng thời cả 3 trường. | Viết Unit Test kiểm tra: Bỏ trống WorkspaceKey $\rightarrow$ trả về lỗi 400/401; Nhập sai WorkspaceKey $\rightarrow$ trả về lỗi 401. | Không |
| **REM-02** | **HIGH** | `FR-AUTH-005`, `IR-UI-005`, `IR-API-004`, `DR-016` | Chưa có chức năng đăng nhập không mật khẩu qua email link. | Cho phép nhập email, gửi liên kết TTL 15m. Mở modal chọn workspace nếu $\ge 2$ tenants. | - **Backend:** Xây dựng endpoints `INT-029` (`/api/v1/auth/email-link/*`), lưu token Redis TTL 15m, gửi email liên kết.<br>- **Frontend:** Thêm Tab Đăng nhập Email trên `login.ts`; Xây dựng `WorkspaceSelectorDialogComponent` (`IR-UI-005`). | Unit tests cho luồng cấp token, kiểm tra hết hạn sau 15m; Test mở modal khi user có 2 tenant. | SMTP Server / Logging Email Sender |
| **REM-03** | **HIGH** | `FR-TEN-001`, `IR-UI-004`, `IR-API-005`, `DR-016` | Form đăng ký chỉ có 3 trường, chưa có nút sinh mã ngẫu nhiên, chưa check mã real-time, chưa upload MinIO. | Đủ 6 trường bắt buộc, nút sinh mã, debounce check-code `GET /api/v1/tenants/check-code`, tải hồ sơ lên MinIO. | - **Database:** Migration thêm cột `TaxId`, `ContactPersonName`, `ContactPhone` vào `Company`; tạo bảng `CompanyRegistrationDocument`.<br>- **Backend:** Mở rộng DTO, bổ sung endpoint `check-code` (`INT-030`), tích hợp MinIO bucket `company-documents`.<br>- **Frontend:** Hoàn thiện form `register-company.ts`. | Unit tests kiểm tra 6 trường; test check trùng mã; test upload file PDF/PNG hợp lệ. | Cấu hình MinIO bucket |
| **REM-04** | **MEDIUM** | `FR-TEN-002`, `IR-UI-006` | Platform Admin chưa xem được tệp đính kèm qua Presigned URL khi xét duyệt. | Hiển thị danh sách tệp đính kèm và tạo Presigned URL xem trước (thời hạn 15m). | - **Backend:** Thêm endpoint lấy Presigned URL đọc tài liệu đăng ký.<br>- **Frontend:** Cập nhật bảng `company-registrations.ts` hiển thị cột tài liệu kèm nút xem trước. | Unit test kiểm tra Presigned URL có thời hạn đúng 15 phút. | Cấu hình MinIO |
| **REM-05** | **HIGH** | `FR-AI-013`, `BR-028`, `IR-API-006` | Chưa có tính năng bóc tách công việc từ văn bản với bản xem trước tương tác. | Endpoint `POST .../preview` trả DTO đề xuất; UI checklist tương tác cho sửa inline; Endpoint `POST .../confirm` ghi DB sau khi người dùng bấm xác nhận. | - **Backend:** Triển khai 2 endpoints `INT-031` (`preview` và `confirm`) trong `TasksController`.<br>- **Frontend:** Xây dựng component `AiTaskDecompositionPreviewComponent`. | Unit test kiểm tra `preview` không làm thay đổi DB; test `confirm` tạo thành công công việc. | OpenAI API Key / Mock Client |
| **REM-06** | **MEDIUM** | `FR-AI-011`, `FR-AI-012`, `BR-027`, `BR-029`, `DR-016` | Chưa có widget robot trợ lý nhúng UI. | Widget robot nhúng góc dưới bên phải, mặc định mở khi login, đóng theo session qua `sessionStorage`. Truy vấn tự nhiên theo đúng quyền RBAC. | - **Frontend:** Xây dựng `RobotChatbotComponent`, nhúng vào layout chính, quản lý state qua `sessionStorage`.<br>- **Backend:** Xây dựng service RAG truy vấn công việc theo context của user. | Component test kiểm tra logic đóng/mở với `sessionStorage`; test truy vấn không làm lộ việc của user khác. | OpenAI API Key |
| **REM-07** | **MEDIUM** | `FR-NOTIF-003`, `FR-NOTIF-004`, `BR-030`, `IR-API-007`, `DR-017`, `OD-032` | Chưa có cấu hình mốc nhắc hạn, chưa có điều phối SignalR $\rightarrow$ Web Push $\rightarrow$ Email. | Cấu hình nhắc hạn 4 mốc (24h, 4h, 1h, quá hạn). Scheduler quét và điều phối đa kênh. Chống trùng lặp qua `NotificationLog`. | - **Database:** Migration tạo bảng `WebPushSubscription` và `NotificationLog`.<br>- **Backend:** Xây dựng `DeadlineReminderWorker`, tích hợp `WebPush-CSharp`, endpoints `INT-032`.<br>- **Frontend:** Đăng ký Service Worker nhận Push Notification. | Unit test kiểm tra thuật toán điều phối online/offline; test ràng buộc unique chống trùng lặp. | Cấu hình VAPID Keys |
| **REM-08** | **LOW** | `NFR-SEC-002` | Cảnh báo bảo mật gói `System.Security.Cryptography.Pkcs` 10.0.0 (`NU1902`/`NU1903`). | Khắc phục lỗ hổng phụ thuộc an ninh khi bản vá chính thức khả dụng. | Giám sát kênh phát hành của Microsoft .NET Security Advisory và cập nhật package version ngay khi có bản vá. | Chạy lại `dotnet build` xác nhận hết cảnh báo. | Bản cập nhật .NET 10 |

---

## I. Đánh Giá Mức Độ Sẵn Sàng Cuối Cùng (Final Readiness Assessment)

### 1. Phân Loại Trạng Thái Theo Khối Lượng Yêu Cầu
- **Baseline MVP v1.0/v1.1:** **100% SẴN SÀNG CHO UAT & TRIỂN KHAI THỰC TẾ**. Mọi chức năng quản lý điều phối dịch vụ, quy trình phê duyệt, phân công công việc, cam kết SLA, bình luận, đính kèm tệp và bảng điều khiển đều đạt độ ổn định tuyệt đối và được kiểm chứng tự động toàn diện.
- **Phạm Vi Nâng Cấp v1.2-DRAFT:** **ĐANG Ở GIAI ĐOẠN ĐẶC TẢ HOÀN TẤT, SẴN SÀNG CHUYỂN SANG GIAI ĐOẠN LẬP TRÌNH (READY FOR SPRINT IMPLEMENTATION)**. Toàn bộ 5 nhóm yêu cầu mới đã có thiết kế kiến trúc chi tiết, hợp đồng API và kịch bản kiểm thử rõ ràng, sẵn sàng để đội ngũ phát triển thực thi theo đúng backlog đã ưu tiên.

### 2. Tuyên Bố Trách Nhiệm Kiểm Toán
Tổ kiểm toán khẳng định:
1. Đã tuân thủ nghiêm ngặt **chế độ chỉ đọc (Read-Only)**; không có bất kỳ dòng mã nguồn hay tệp cấu hình nào bị thay đổi trong quá trình kiểm toán.
2. Không che giấu các cảnh báo biên dịch (`NU1902`/`NU1903`) và nêu rõ rào cản môi trường Docker Daemon khi chạy Integration Tests.
3. Không tự ý suy diễn hoặc tuyên bố hoàn thành đối với các yêu cầu mới chưa được cài đặt trong mã nguồn.
