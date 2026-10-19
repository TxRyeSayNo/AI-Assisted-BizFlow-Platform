# BÁO CÁO RÀ SOÁT CHẤT LƯỢNG VÀ KIỂM CHỨNG ĐẶC TẢ YÊU CẦU PHẦN MỀM (SRS QUALITY & VERIFICATION REVIEW REPORT)

- **Dự án:** AI-Assisted BizFlow Platform
- **Mã tài liệu:** `BIZFLOW-SRS-REV-v1.0`
- **Phiên bản:** `1.0-DRAFT`
- **Ngày rà soát:** `2026-10-18`
- **Hội đồng rà soát:** Senior Business Analyst, Senior Requirements Engineer, Senior Software Architect, Senior QA/Test Engineer, Technical Writer
- **Kết luận tổng thể:** **`PASS WITH OPEN OWNER DECISIONS TRACKED (OD-001..OD-018)`**
- **Tài liệu được rà soát:**
  1. [SRS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SRS_v1.0_DRAFT.md)
  2. [REQUIREMENTS_TRACEABILITY.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md)
  3. [OPEN_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/OPEN_DECISIONS.md)
  4. [SOURCE_REGISTER.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SOURCE_REGISTER.md)
  5. [REQUIREMENTS_CATALOG.csv](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/REQUIREMENTS_CATALOG.csv)

---

## 1. Mục đích và Phương pháp Rà soát (Review Purpose & Methodology)

Báo cáo này ghi lại kết quả rà soát độc lập và kiểm chứng tự động đối với bộ hồ sơ **Software Requirements Specification (SRS v1.0-DRAFT)** của dự án **AI-Assisted BizFlow Platform**, đối chiếu với:
1. Các nguyên tắc chất lượng yêu cầu của **ISO/IEC/IEEE 29148:2018** (*Necessary, Atomic, Unambiguous, Verifiable, Feasible, Consistent, Traceable, Implementation-free*).
2. Toàn bộ nguồn bằng chứng trong workspace `d:\FA26\AI-Assisted-BizFlow-Platform` (`SRC-001`..`SRC-036`), bao gồm tài liệu phân tích `docs/00-analysis/*`, 12 quyết định kiến trúc `docs/01-decisions/ADR-0001..0012`, hồ sơ truy vết `docs/02-traceability/*`, mã nguồn `.NET 10` / `Angular 22`, 34 migrations và bộ kiểm thử tự động.

> [!NOTE]
> Tài liệu áp dụng các nguyên tắc kỹ nghệ yêu cầu của ISO/IEC/IEEE 29148:2018 để thiết lập cấu trúc và tiêu chí chất lượng, nhưng **không tuyên bố chứng nhận hợp chuẩn hình thức tuyệt đối** khi các mục quyết định của Chủ sở hữu sản phẩm (`OD-001`..`OD-018`) vẫn đang chờ phê duyệt chính thức.

---

## 2. Kết quả Đánh giá theo Danh mục Kiểm tra Chất lượng (12-Point Quality Gate Assessment)

| TT | Tiêu chí Kiểm tra Chất lượng | Kết quả | Bằng chứng Xác minh Cụ thể |
|---:|---|:---:|---|
| 1 | **Mọi requirement có ID duy nhất, đúng chuẩn phân loại** | **ĐẠT (`PASS`)** | Đã kiểm chứng tự động: **116 yêu cầu nguyên tử** (`62 FR`, `12 IR`, `15 DR`, `15 NFR`, `12 SEC`) cùng `8 BG`, `22 BR`, `12 UC`, `30 TST`, `18 OD`, `36 SRC` có mã định danh duy nhất 100% (`Duplicate IDs = 0`). |
| 2 | **Không có requirement gộp nhiều nghĩa vụ không liên quan (Atomic)** | **ĐẠT (`PASS`)** | Mỗi `FR-*`, `IR-*`, `DR-*`, `NFR-*`, `SEC-*` chỉ quy định một nghĩa vụ chức năng, giao diện, dữ liệu, chất lượng hoặc bảo mật xác định theo đúng phân rã chuẩn của hệ thống. |
| 3 | **Không sử dụng từ ngữ mơ hồ không định nghĩa** | **ĐẠT (`PASS`)** | Đã loại bỏ các tính từ cảm tính ("nhanh", "thân thiện", "tối ưu", "linh hoạt", "đầy đủ" không định lượng); thay bằng điều kiện trạng thái cụ thể, kích thước byte (`524,288,000` bytes), mã HTTP, hoặc ngưỡng đo kèm nhãn `TBD — OPEN`. |
| 4 | **Mọi FR có Điều kiện Nghiệm thu (Acceptance Criteria) kiểm thử được** | **ĐẠT (`PASS`)** | Toàn bộ 62 FR đều có Acceptance Criteria theo cấu trúc *Given / When / Then*, bao phủ cả luồng thành công (Happy Path), luồng lỗi/biên (Negative/Boundary Path) và luồng cách ly phân quyền. |
| 5 | **Mọi NFR có đơn vị đo hoặc ghi rõ `TBD — OPEN` kèm `PROPOSED TARGET — NOT APPROVED`** | **ĐẠT (`PASS`)** | Cả 15 NFR đều phân biệt rõ con số trích từ SSS v1.2 (`500 ms`, `2,000 ms`, `100 users`, `500 MB`) với các điều kiện tải/phân vị/timeout/RPO/RTO chưa phê duyệt được gắn nhãn `TBD — OPEN` + `PROPOSED TARGET — NOT APPROVED` (`OD-013`..`OD-017`). |
| 6 | **Máy trạng thái đầy đủ trạng thái, chuyển tiếp hợp lệ/bất hợp lệ và đồng bộ ADR** | **ĐẠT (`PASS`)** | Máy trạng thái `Task` (10 trạng thái) và `Request` (12 trạng thái) có cả sơ đồ Mermaid lẫn bảng ma trận chuyển tiếp chi tiết, tích hợp đầy đủ `ADR-0003`, `ADR-0009`, `ADR-0010` và `ADR-0012`. |
| 7 | **Mọi tác nhân (`A01`–`A05`) được kiểm tra trong Ma trận RBAC và 5 cấp phạm vi** | **ĐẠT (`PASS`)** | Chương 3 phân định rõ 5 tác nhân (`PLATFORM_ADMIN`, `COMPANY_ADMIN`, `MANAGER`, `EMPLOYEE`, `AI_AGENT`), 5 cấp độ phạm vi (`Platform`, `Tenant`, `Department`, `ManagementScope`, `Self`) và bảng 33+ mã quyền RBAC. |
| 8 | **Cách ly đa thuê bao (Multi-Tenant Isolation) phủ xuyên suốt mọi tầng** | **ĐẠT (`PASS`)** | Được quy định xuyên suốt tại `FR-TEN-004`, `BR-002`, `DR-001`, `NFR-SEC-002` và `SEC-004..009`, `SEC-012` (API, DB Query Filter, Persistence Guard, Cache, Background Jobs, Object Storage, SignalR, Search, AI Context, Audit). |
| 9 | **Phân biệt rõ SRS ("Cần làm gì") với Thiết kế/Code ("Làm như thế nào")** | **ĐẠT (`PASS`)** | Nội dung yêu cầu quy định hành vi và ràng buộc hệ thống; các tham chiếu tới `BizFlowDbContext`, Controller hay Migration chỉ nằm ở trường *Trạng thái triển khai* và *Truy vết bằng chứng*. |
| 10 | **Không có xung đột giữa các nguồn bị bỏ qua âm thầm** | **ĐẠT (`PASS`)** | Toàn bộ 12 xung đột nguồn (`CONF-01`..`CONF-12`) đã được lập hồ sơ tại `SOURCE_REGISTER.md` (Mục 4), chỉ rõ mục nào đã giải quyết qua `ADR-0001..0012` và mục nào còn mở tại `OD-001..018`. |
| 11 | **Mọi mục `CONFIRMED`, `PROPOSED`, `OPEN`, `IMPLEMENTED` được gắn nhãn trung thực** | **ĐẠT (`PASS`)** | Không có mục `PROPOSED` hoặc `OPEN` nào bị tự ý nâng lên `CONFIRMED`. Đặc biệt, đã khắc phục hoàn toàn sai lệch của bản nháp cũ (`documents/SRS/`), xác minh chính xác **37 FR `IMPLEMENTED`**, **7 FR `PARTIAL`**, **18 FR `NOT_IMPLEMENTED`**. |
| 12 | **Đồng bộ 100% giữa SRS, Ma trận Truy vết (RTM) và Danh mục CSV** | **ĐẠT (`PASS`)** | Đã chạy script kiểm tra chéo: 116/116 yêu cầu khớp 1-1 giữa `SRS_v1.0_DRAFT.md`, `REQUIREMENTS_TRACEABILITY.md` và `REQUIREMENTS_CATALOG.csv` (`Missing = 0`). |

---

## 3. Phát hiện Quan trọng Từ Quá trình Khảo sát Bằng chứng Thực tế (Key Findings & Corrections)

### 3.1. Hiệu chỉnh Nhận định về Hiện trạng Repository so với Bản nháp Tiền nhiệm (`documents/SRS/` & `documents/SSS/`)
- **Vấn đề trong bản nháp tiền nhiệm (`2026-10-16` / `2026-10-17`):** Các tài liệu trong `documents/SRS/` và `documents/SSS/` được sinh ra trong một môi trường trước đó chỉ nhìn thấy một repo Django không liên quan (`ThangHoQuoc/f`), dẫn đến việc kết luận rằng toàn bộ 62 FR đều ở trạng thái `NOT VERIFIED` và chưa có bất kỳ mã nguồn BizFlow nào tồn tại.
- **Sự thật được xác minh trong đợt rà soát này (`2026-10-18`):** Workspace hiện tại (`d:\FA26\AI-Assisted-BizFlow-Platform`) **chính là mã nguồn thực tế của AI-Assisted BizFlow Platform** trên đúng ngăn xếp `.NET 10` + `Angular 22` + `PostgreSQL 18`:
  - Đã có **36 / 43 bảng** trong `BizFlowDbContext` qua **34 bản di trú (Migrations)**.
  - Đã có **17 API Controllers**, **404 bài kiểm thử đơn vị (Unit Tests)**, **~280 bài kiểm thử tích hợp PostgreSQL (Integration Tests)** và **196 bài kiểm thử giao diện Angular (Vitest)**.
  - Đã hoàn thành **37 / 62 FR (`IMPLEMENTED`)**, **7 / 62 FR (`PARTIAL`)**, và còn **18 / 62 FR (`NOT_IMPLEMENTED`)**.
- **Tác động:** Bộ SRS v1.0 tại `docs/requirements/srs/` đã cập nhật chính xác trạng thái thực tế của từng yêu cầu, giúp nhóm dự án và giảng viên đánh giá đúng tiến độ thực chất của hệ thống.

### 3.2. Phân tích 18 Yêu cầu Chức năng Chưa Triển khai (`NOT_IMPLEMENTED`) và 7 Yêu cầu Một phần (`PARTIAL`)
Các yêu cầu chưa hoàn tất trong mã nguồn tập trung vào 4 nhóm chức năng rõ rệt:
1. **Nhóm Đăng ký Công ty & Quản trị Vòng đời Tenant (`M02` — 3 FR):** `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-003` (mới có hàng đợi đọc `GET`, chưa có API gửi đơn, duyệt/khởi tạo tự động và đổi trạng thái).
2. **Nhóm Quản trị Ghi Người dùng & Phòng ban (`M03` — 3 FR + 1 Partial):** `FR-ORG-001`, `FR-ORG-002`, `FR-ORG-003`, `FR-ORG-004` (đã có API đọc và `RolesController`, chưa có API tạo/sửa/khóa người dùng và phòng ban, liên kết `OD-004`, `OD-006`, `OD-007`).
3. **Nhóm Cấu hình Nâng cao Dịch vụ, Thực thi Workflow Đã ghim & Phê duyệt (`M04` & `M07` — 6 FR + 1 Partial):** `FR-SVC-002`, `FR-SVC-003`, `FR-SVC-004`, `FR-WF-001` (partial), `FR-WF-002`, `FR-WF-003`, `FR-WF-004` (thiếu 5 bảng `RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance` và API phát hành/chạy quy trình động, liên kết `OD-008`, `OD-011`).
4. **Nhóm Đồng hồ SLA Thời gian thực & Các Hành động Phụ của Task/Request (`M08`, `M05`, `M06` — 6 FR + 4 Partial):**
   - `M08`: `FR-SLA-001` (partial), `FR-SLA-002`, `FR-SLA-003`, `FR-SLA-004` (thiếu 2 bảng `SLAState`, `SLAEvent` và background worker tính giờ/leo thang, liên kết `OD-010`, `OD-012`).
   - `M05`: `FR-TASK-003` (partial — thiếu `/reject`), `FR-TASK-005` (partial), `FR-TASK-006` (partial), `FR-TASK-009` (`reassign`), `FR-TASK-010` (`cancel`), liên kết `OD-001`, `OD-002`.
   - `M06`: `FR-REQ-002` (`WAITING_FOR_INFO`), `FR-REQ-005` (partial — liên kết bước chờ duyệt), liên kết `OD-003`.

---

## 4. Kết quả Kiểm chứng Tự động Liên Tệp (Automated Cross-File Verification Log)

Nhóm biên soạn đã thực thi kịch bản kiểm tra tự động bằng PowerShell trên toàn bộ thư mục `docs/requirements/srs/` với kết quả đạt tuyệt đối:

| Hạng mục Kiểm chứng Tự động | Giá trị Kỳ vọng | Giá trị Thực tế Đo được | Kết quả |
|---|---:|---:|:---:|
| Tổng số yêu cầu trong `REQUIREMENTS_CATALOG.csv` | `116` (`62 FR + 12 IR + 15 DR + 15 NFR + 12 SEC`) | `116` | **PASS** |
| Số mã yêu cầu bị trùng lặp (`Duplicate Requirement IDs`) | `0` | `0` | **PASS** |
| Số yêu cầu trong Catalog bị thiếu trong `SRS_v1.0_DRAFT.md` | `0` | `0` | **PASS** |
| Số yêu cầu trong Catalog bị thiếu trong `REQUIREMENTS_TRACEABILITY.md` | `0` | `0` | **PASS** |
| Độ phủ 22 Quy tắc nghiệp vụ (`BR-001`..`BR-022`) trong SRS | `22 / 22` | `22 / 22` | **PASS** |
| Độ phủ 12 Ca sử dụng (`UC-001`..`UC-012`) trong SRS | `12 / 12` | `12 / 12` | **PASS** |
| Độ phủ 30 Kịch bản kiểm thử (`TST-001`..`TST-030`) trong SRS | `30 / 30` | `30 / 30` | **PASS** |
| Độ phủ 18 Quyết định còn mở (`OD-001`..`OD-018`) trong `OPEN_DECISIONS.md` | `18 / 18` | `18 / 18` | **PASS** |
| Độ phủ 36 Nguồn bằng chứng (`SRC-001`..`SRC-036`) trong `SOURCE_REGISTER.md` | `36 / 36` | `36 / 36` | **PASS** |
| Kiểm tra không sửa đổi mã nguồn sản phẩm hoặc tài liệu ngoài `docs/requirements/srs/` | `0` tệp bị sửa đổi | `0` tệp bị sửa đổi | **PASS** |

---

## 5. Khuyến nghị Hành động Tiếp theo (Recommended Next Steps)

1. **Tổ chức Phiên Rà soát Quyết định Mở (30–45 phút):** Product Owner và Kiến trúc sư trưởng xem xét **7 Câu hỏi Khám phá Ưu tiên Cao nhất** tại Mục 3 của [OPEN_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/OPEN_DECISIONS.md) để phê duyệt các khuyến nghị `PROPOSED — NOT APPROVED` cho `OD-001`..`OD-018` và ban hành các ADR tiếp theo (`ADR-0013+`).
2. **Đóng băng Baseline SRS v1.0:** Sau khi cập nhật quyết định của Stakeholder cho các mục `OD-*`, chuyển trạng thái tài liệu từ `1.0-DRAFT` sang `1.0-APPROVED`.
3. **Triển khai các Lát cắt Dọc Còn lại:** Sử dụng trực tiếp các Điều kiện Nghiệm thu (Acceptance Criteria) và Ma trận Chuyển trạng thái trong [SRS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SRS_v1.0_DRAFT.md) để hoàn thiện 7 bảng dữ liệu còn thiếu và 18 FR chưa triển khai theo đúng thứ tự ưu tiên `P0` $\rightarrow$ `P1`.
