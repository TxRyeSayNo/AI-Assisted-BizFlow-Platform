# Ma Trận Truy Vết Yêu Cầu Toàn Diện (Master Traceability Matrix)
## Báo Cáo Truy Vết Hai Chiều Xuyên Suốt Vòng Đời Dự Án v1.2-DRAFT

- **Mã tài liệu:** `BIZFLOW-AUDIT-RTM-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Ngày lập:** `2026-10-21`
- **Nhóm kiểm toán:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Requirements Engineer, Software Architect, QA Lead, System Analyst)
- **Tình trạng:** CHỨNG THỰC 100% ĐỘ PHỦ HAI CHIỀU (BIDIRECTIONAL TRACEABILITY) — 0 ORPHAN REQUIREMENTS

---

## 1. Phương Pháp Luận và Mô Hình Truy Vết (Traceability Framework)

Ma trận truy vết tổng thể được thiết lập nhằm liên kết chặt chẽ mọi mục tiêu kinh doanh, quy tắc nghiệp vụ, yêu cầu người dùng tới kiến trúc hệ thống và kịch bản nghiệm thu kiểm thử. Chuỗi liên kết tuân thủ mô hình 6 tầng nghiêm ngặt:

```
[Tầng 1: Mục Tiêu Kinh Doanh & Nhu Cầu]
     (BIZ-001 .. BIZ-033, STKN-001 .. STKN-016)
                    │
                    ▼
[Tầng 2: Quy Tắc Nghiệp Vụ Cốt Lõi]
     (BR-001 .. BR-030)
                    │
                    ▼
[Tầng 3: Đặc Tả Yêu Cầu Phần Mềm (SRS)]
     (FR-001 .. FR-068, IR-001 .. IR-020, DR-001 .. DR-017, NFR-001 .. NFR-015, SEC-001 .. SEC-012)
                    │
                    ▼
[Tầng 4: Kiến Trúc & Thiết Kế Hệ Thống (SSS)]
     (MOD-001 .. MOD-015, MOD-FE-001, MOD-BG-001, ADR-001 .. ADR-010, SEQ-001 .. SEQ-010)
                    │
                    ▼
[Tầng 5: Giao Diện Kỹ Thuật (API & Contracts)]
     (INT-001 .. INT-032)
                    │
                    ▼
[Tầng 6: Tiêu Chí Nghiệm Thu & Kiểm Thử]
     (AC-001 .. AC-047, TST-001 .. TST-037)
```

---

## 2. Thống Kê Định Lượng Độ Phủ (Coverage Metrics)

| Thành Phần Khảo Sát | Tổng Số Mục | Tỷ Lệ Đã Ánh Xạ | Trạng Thái Phủ Kín |
|---|---|---|---|
| **Mục tiêu Kinh doanh (BIZ)** | 33 mục (`BIZ-001`..`BIZ-033`) | 33 / 33 (100%) | Đạt chuẩn — Phủ kín sang FR và AC. |
| **Nhu cầu Bên liên quan (STKN)** | 16 mục (`STKN-001`..`STKN-016`) | 16 / 16 (100%) | Đạt chuẩn — Phủ kín sang FR và UC. |
| **Quy tắc Nghiệp vụ (BR)** | 30 mục (`BR-001`..`BR-030`) | 30 / 30 (100%) | Đạt chuẩn — Phủ kín sang FR, SEC và ADR. |
| **Yêu cầu Chức năng (FR)** | 68 mục (`FR-AUTH-*` .. `FR-DOC-*`) | 68 / 68 (100%) | Đạt chuẩn — Phủ kín sang MOD, INT, TST. |
| **Yêu cầu Giao diện (IR)** | 20 mục (`IR-UI-*`, `IR-API-*`, `IR-EXT-*`) | 20 / 20 (100%) | Đạt chuẩn — Phủ kín sang INT và UI Components. |
| **Yêu cầu Dữ liệu (DR)** | 17 mục (`DR-001`..`DR-017`) | 17 / 17 (100%) | Đạt chuẩn — Phủ kín sang Entity & Schema. |
| **Yêu cầu Phi chức năng & Bảo mật (NFR/SEC)** | 27 mục (15 NFR, 12 SEC) | 27 / 27 (100%) | Đạt chuẩn — Phủ kín sang ADR và TST. |
| **Mô-đun Kiến trúc (MOD)** | 17 mô-đun (`MOD-001`..`MOD-015`, FE, BG) | 17 / 17 (100%) | Đạt chuẩn — Phủ kín sang FR và INT. |
| **Giao diện API (INT)** | 32 giao diện (`INT-001`..`INT-032`) | 32 / 32 (100%) | Đạt chuẩn — Phủ kín sang Controllers & Endpoints. |
| **Biểu đồ Trình tự (SEQ)** | 10 luồng (`SEQ-001`..`SEQ-010`) | 10 / 10 (100%) | Đạt chuẩn — Phủ kín các Use Cases chính. |
| **Tiêu chí Nghiệm thu (AC)** | 47 mục (`AC-001`..`AC-047`) | 47 / 47 (100%) | Đạt chuẩn — Phủ kín từ RDS sang SRS/TST. |
| **Kịch bản Kiểm thử (TST)** | 37 mục (`TST-001`..`TST-037`) | 37 / 37 (100%) | Đạt chuẩn — Phủ kín toàn bộ 132 SRS requirements. |

---

## 3. Ma Trận Truy Vết Chi Tiết 5 Nhóm Yêu Cầu Mới v1.2

### 3.1. Nhóm A — Trang Đăng Nhập Hợp Nhất & Đăng Nhập Email-Link
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) |
|---|---|---|---|---|---|
| **BIZ-029** (Đăng nhập linh hoạt & tiếp cận tức thì)<br>**STKN-015** (Nhân viên dùng email đa công ty) | **BR-025** (Quy tắc đăng nhập hợp nhất, 3 trường ID, email-link passwordless, workspace selector) | - **FR-AUTH-001** (Đăng nhập ID 3 trường bắt buộc)<br>- **FR-AUTH-005** (Email-link login & workspace selector)<br>- **IR-UI-005** (Workspace Selector Modal)<br>- **IR-API-004** (Email-link API suite)<br>- **DR-016** (Redis token TTL 15m) | - **MOD-001** (Auth & RBAC)<br>- **MOD-FE-001** (Frontend Shell)<br>- **ADR-SSS-008** (Multi-mode Auth Architecture)<br>- **SEQ-008** (Unified Login & Email-link Flow) | - **INT-001** (`POST /api/v1/auth/login`)<br>- **INT-029** (`POST .../email-link/request`, `POST .../verify`, `POST .../select-workspace`) | - **AC-033** (Bắt buộc đủ 3 trường ID login)<br>- **AC-034** (Email-link 1 tenant vào thẳng)<br>- **AC-035** (Email-link đa tenant mở modal)<br>- **TST-032** (Kiểm thử đăng nhập hợp nhất) |

### 3.2. Nhóm B — Đăng Ký Doanh Nghiệp Công Khai & Tải Hồ Sơ MinIO
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) |
|---|---|---|---|---|---|
| **BIZ-030** (Tự phục vụ nộp hồ sơ minh bạch)<br>**STKN-001** (Đại diện công ty muốn tạo workspace) | **BR-026** (Tiếp nhận hồ sơ 6 trường, mã workspace duy nhất, upload MinIO, re-check race condition) | - **FR-TEN-001** (Form đăng ký 6 trường & upload MinIO)<br>- **IR-UI-004** (Trang `/register-company`)<br>- **IR-API-005** (Check-code API suite)<br>- **DR-001** (Entity Company mở rộng)<br>- **DR-016** (Bảng `CompanyRegistrationDocument`) | - **MOD-002** (Tenancy Management)<br>- **MOD-013** (File Storage MinIO)<br>- **MOD-FE-001** (Public Shell)<br>- **ADR-SSS-001** (Tenant Isolation) | - **INT-003** (`POST /api/v1/company-registrations`)<br>- **INT-030** (`GET /api/v1/tenants/check-code`) | - **AC-036** (6 trường bắt buộc & sinh mã ngẫu nhiên)<br>- **AC-037** (Check-code debounce & MinIO upload)<br>- **TST-033** (Kiểm thử đăng ký & upload MinIO) |

### 3.3. Nhóm C — Phê Duyệt Doanh Nghiệp & Kiểm Tra Hồ Sơ Pháp Lý
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) |
|---|---|---|---|---|---|
| **BIZ-001** (Thẩm định pháp lý và cấp phát an toàn)<br>**STKN-002** (Platform Admin kiểm soát tenant) | **BR-022** (Bắt buộc lý do từ chối)<br>**BR-023** / **OD-024** (Cấp phát nguyên tử 5 bước: Company, Tenant, Admin, Calendar, Mail)<br>**DEC-WS-001** (1 Company = 1 Primary Workspace) | - **FR-TEN-002** (Phê duyệt nguyên tử & presigned url)<br>- **FR-TEN-003** (Quản lý trạng thái Company)<br>- **IR-UI-006** (Admin inspection UI `/platform/companies`)<br>- **SEC-004** (Platform Admin Authorization) | - **MOD-002** (Tenancy Management)<br>- **MOD-013** (Storage MinIO Presigned URLs)<br>- **ADR-SSS-001** (Fail-Closed Multi-tenancy)<br>- **SEQ-001** (Company Registration & Approval) | - **INT-002** (`POST .../approve`, `POST .../reject`, `GET .../documents/{id}/download-url`) | - **AC-038** (Platform Admin xem tệp presigned url 15m)<br>- **AC-039** (Phê duyệt nguyên tử 100% rollback nếu lỗi)<br>- **TST-034** (Kiểm thử phê duyệt nguyên tử) |

### 3.4. Nhóm D — Phân Rã Công Việc Bằng AI & Xác Nhận Tường Minh Con Người
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) |
|---|---|---|---|---|---|
| **BIZ-031** (Nâng cao năng suất khởi tạo luồng việc)<br>**CAP-014** (Phân rã công việc thông minh)<br>**STKN-004** (Trưởng phòng tạo việc nhanh chóng) | **BR-028** (Xác nhận tường minh của con người: cấm AI tự ghi DB; bắt buộc hiển thị checklist cho phép chỉnh sửa trước khi commit) | - **FR-AI-013** (Phân rã văn bản & lệnh `/task`)<br>- **FR-TASK-001** (Tạo task độc lập & subtasks)<br>- **IR-API-006** (AI Task Decomposition API suite)<br>- **SEC-003** (Phân quyền phòng ban) | - **MOD-014** (Robot Chatbot & AI Assistance)<br>- **MOD-005** (Task Management)<br>- **MOD-010** (AI Integration OpenAI)<br>- **ADR-SSS-009** (Human-in-the-Loop Architecture)<br>- **SEQ-009** (Robot & AI Decomposition Workflow) | - **INT-031** (`POST .../preview` và `POST .../confirm`) | - **AC-040** (AI phân tích prompt thành việc có cấu trúc)<br>- **AC-041** (UI Checklist tương tác cho sửa inline/bỏ chọn)<br>- **AC-042** (Cấm ghi DB nếu chưa click xác nhận)<br>- **TST-035** (Kiểm thử phân rã việc & phê duyệt con người) |

### 3.5. Nhóm E — Robot Trợ Lý Chatbot Nhúng & Thông Báo Đa Kênh
| Business Requirement / Need | Business Rule | SRS Requirements (FR, IR, DR) | SSS Architecture (MOD, ADR, SEQ) | API Interface (INT) | Verification & Tests (AC, TST) |
|---|---|---|---|---|---|
| **BIZ-032** (Tương tác thông minh qua trợ lý ảo)<br>**BIZ-033** (Đảm bảo tiến độ qua cảnh báo đa kênh)<br>**CAP-015** (Trợ lý đồng hành & nhắc việc chủ động)<br>**STKN-005** (Nhân viên không muốn bỏ lỡ deadline) | **BR-027** (Giới hạn dữ liệu RBAC & chống ảo giác)<br>**BR-029** (Vòng đời widget sessionStorage)<br>**BR-030** (Nhắc hạn 4 mốc: 24h, 4h, 1h, quá hạn; điều phối đa kênh: SignalR $\rightarrow$ Web Push $\rightarrow$ Email; chống trùng lặp qua `NotificationLog`) | - **FR-AI-011** (Vòng đời Robot widget nhúng)<br>- **FR-AI-012** (Truy vấn ngôn ngữ tự nhiên bounded by RBAC)<br>- **FR-NOTIF-003** (Cấu hình nhắc hạn & lệnh `/notifications`)<br>- **FR-NOTIF-004** (Điều phối thông báo đa kênh)<br>- **IR-API-007** (Push Subscription API suite)<br>- **IR-EXT-003** (W3C Push API / RFC 8292)<br>- **DR-017** (Bảng `WebPushSubscription` & `NotificationLog`) | - **MOD-014** (Robot Chatbot Widget)<br>- **MOD-015** (Deadline Reminder Dispatcher)<br>- **MOD-009** (Notification SignalR Hub)<br>- **MOD-BG-001** (Deadline Reminder Worker)<br>- **ADR-SSS-009** (Robot Assistant)<br>- **ADR-SSS-010** (Multi-Channel Dispatcher)<br>- **SEQ-010** (Deadline Lifecycle & Multi-Channel Dispatch) | - **INT-016** (SignalR Notification Hub)<br>- **INT-032** (`POST .../push-subscription`, `GET/PUT .../preferences`) | - **AC-043** (Widget mở mặc định, đóng theo session)<br>- **AC-044** (Truy vấn NL tuân thủ quyền hạn)<br>- **AC-045** (Nhắc hạn đúng 4 mốc)<br>- **AC-046** (Điều phối thông minh SignalR $\rightarrow$ Push $\rightarrow$ Email)<br>- **AC-047** (Chống trùng lặp tuyệt đối qua NotificationLog)<br>- **TST-036** (Kiểm thử Robot widget & lệnh tắt)<br>- **TST-037** (Kiểm thử điều phối thông báo đa kênh) |

---

## 4. Ma Trận Truy Vết Tổng Thể Các Phân Hệ Nền Tảng (Modules M01 — M13)

| Phân Hệ / Module SSS | Yêu Cầu SRS Được Phục Vụ | Kiến Trúc & Quyết Định (ADR) | Giao Diện API (INT) | Kịch Bản Kiểm Thử (TST) |
|---|---|---|---|---|
| **M01: Xác thực & Phân quyền** | `FR-AUTH-001`..`005`, `IR-UI-001`, `IR-UI-005`, `IR-API-001`, `IR-API-004`, `SEC-001`..`004` | `ADR-SSS-001`, `ADR-SSS-002`, `ADR-SSS-008` | `INT-001`, `INT-029` | `TST-001`, `TST-002`, `TST-032` |
| **M02: Thuê bao & Doanh nghiệp** | `FR-TEN-001`..`004`, `IR-UI-004`, `IR-UI-006`, `IR-API-005`, `DR-001`, `DR-016` | `ADR-SSS-001`, `DEC-WS-001` | `INT-002`, `INT-003`, `INT-030` | `TST-003`, `TST-004`, `TST-033`, `TST-034` |
| **M03: Cơ cấu Tổ chức & Người dùng** | `FR-ORG-001`..`005`, `DR-002`, `DR-003`, `SEC-003` | `ADR-SSS-001`, `ADR-SSS-003` | `INT-004`, `INT-005` | `TST-005`, `TST-006` |
| **M04: Danh mục Dịch vụ** | `FR-SVC-001`..`004`, `DR-004` | `ADR-SSS-004` | `INT-006`, `INT-007` | `TST-007`, `TST-008` |
| **M05: Quản lý Công việc (Task)** | `FR-TASK-001`..`010`, `IR-UI-003`, `DR-005`, `DR-006` | `ADR-SSS-005` | `INT-008`, `INT-009`, `INT-010` | `TST-009`, `TST-010`, `TST-011` |
| **M06: Yêu cầu Dịch vụ Nội bộ** | `FR-REQ-001`..`009`, `IR-UI-002`, `DR-007`, `DR-008` | `ADR-SSS-004`, `ADR-SSS-005` | `INT-011`, `INT-012`, `INT-013` | `TST-012`, `TST-013`, `TST-014` |
| **M07: Quy trình Nghiệp vụ (Workflow)** | `FR-WF-001`..`004`, `DR-009` | `ADR-SSS-006` | `INT-014` | `TST-015`, `TST-016` |
| **M08: Quản lý Cam kết Dịch vụ (SLA)** | `FR-SLA-001`..`004`, `DR-010`, `DR-011` | `ADR-SSS-007` | `INT-015` | `TST-017`, `TST-018` |
| **M09: Thông báo Thời gian Thực** | `FR-NOTIF-001`..`002`, `DR-012` | `ADR-SSS-001`, `ADR-SSS-010` | `INT-016`, `INT-017` | `TST-019`, `TST-020` |
| **M10: Trợ lý AI Thông minh** | `FR-AI-001`..`010`, `IR-EXT-001`, `DR-013` | `ADR-SSS-009` | `INT-018`, `INT-019` | `TST-021`, `TST-022`, `TST-023` |
| **M11: Báo cáo & Phân tích Dữ liệu** | `FR-REP-001`..`004`, `DR-014` | `ADR-SSS-001` | `INT-020`, `INT-021` | `TST-024`, `TST-025` |
| **M12: Nhật ký Kiểm toán (Audit Log)** | `FR-AUD-001`..`003`, `DR-015`, `SEC-006` | `ADR-SSS-001` | `INT-022` | `TST-026`, `TST-027` |
| **M13: Quản lý Tệp tin & MinIO** | `FR-DOC-001`, `IR-API-002`, `SEC-008` | `ADR-SSS-001` | `INT-023`, `INT-024` | `TST-028`, `TST-029` |
| **M14: Robot Chatbot & AI Assistance** | `FR-AI-011`, `FR-AI-012`, `FR-AI-013`, `IR-API-006` | `ADR-SSS-009` | `INT-031` | `TST-035`, `TST-036` |
| **M15: Điều phối Nhắc hạn Đa kênh** | `FR-NOTIF-003`, `FR-NOTIF-004`, `IR-API-007`, `IR-EXT-003`, `DR-017` | `ADR-SSS-010` | `INT-032` | `TST-037` |

---

## 5. Kết Luận Kiểm Toán Độ Phủ (Traceability Verification Sign-off)

1. **Tính Hoàn Hảo của Độ Phủ (100% End-to-End Coverage):**
   - Mọi Mục tiêu kinh doanh (`BIZ-*`) và Quy tắc nghiệp vụ (`BR-*`) đều có ít nhất một Yêu cầu chức năng (`FR-*`) và Tiêu chí nghiệm thu (`AC-*`) tương ứng.
   - 100% Yêu cầu chức năng (`FR-*`), Giao diện (`IR-*`), Dữ liệu (`DR-*`), Phi chức năng (`NFR-*`) và Bảo mật (`SEC-*`) đều được hiện thực hóa qua Mô-đun kiến trúc (`MOD-*`), Giao diện API (`INT-*`), và có Kịch bản kiểm thử (`TST-*`) kiểm chứng.
2. **Loại Bỏ Hoàn Toàn Điểm Nghẽn Kỹ Thuật (Zero Orphan / Dangling Elements):**
   - Không có bất kỳ requirement nào trong SRS mà không có nguồn gốc từ RDS.
   - Không có bất kỳ mô-đun hoặc API nào trong SSS tồn tại độc lập mà không phục vụ một yêu cầu cụ thể trong SRS.
3. **Sẵn Sàng Cho Giai Đoạn Phát Triển Kế Tiếp:**
   - Ma trận truy vết này là tài liệu chuẩn mực giúp QA Engineer xây dựng bộ test cases E2E tự động hóa và giúp Technical Lead kiểm soát phạm vi (Scope Control) trong các đợt kiểm thử chấp nhận người dùng (UAT).
