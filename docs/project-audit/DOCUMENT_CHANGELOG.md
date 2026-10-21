# Nhật Ký Thay Đổi Tài Liệu (Document Changelog)
## Báo Cáo Chi Tiết Cập Nhật Hồ Sơ Kỹ Thuật v1.2-DRAFT

- **Mã tài liệu:** `BIZFLOW-DOC-CHANGELOG-v1.2`
- **Phiên bản phát hành:** `1.2-DRAFT`
- **Ngày phát hành:** `2026-10-21`
- **Dự án:** AI-Assisted BizFlow Platform
- **Nhóm thực hiện:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Requirements Engineer, Software Architect, Technical Documentation Engineer, System Analyst)
- **Tình trạng:** CHÍNH THỨC BAN HÀNH CHO ĐỢT REVISION v1.2

---

## 1. Tổng quan Đợt Cập nhật v1.2-DRAFT (2026-10-21)

Đợt cập nhật hồ sơ tài liệu phiên bản **v1.2-DRAFT** được thực hiện nhằm mục đích phản ánh chính xác 5 nhóm quyết định nghiệp vụ mới đã được phê duyệt, giải quyết dứt điểm các điểm chưa nhất quán giữa tài liệu yêu cầu (RDS), đặc tả phần mềm (SRS) và đặc tả hệ thống (SSS), đồng thời bảo toàn tính bất biến của mã nguồn trong giai đoạn rà soát.

### 5 Nhóm Yêu cầu Nghiệp vụ Trọng tâm:
1. **Nhóm A (Xác thực & Đăng nhập):** Trang đăng nhập hợp nhất `/login` (`UI-01`) với 2 tùy chọn độc lập: Option 1 bắt buộc đủ 3 trường (`Username/EmployeeCode`, `Password`, `WorkspaceKey`) với thông báo lỗi vô danh `401 AUTH.INVALID_CREDENTIALS`; Option 2 đăng nhập qua Email-link không cần mật khẩu/workspace key, xác thực phân nhánh (0 workspace $\rightarrow$ chuyển hướng đăng ký; 1 workspace $\rightarrow$ vào thẳng; $\ge 2$ workspaces $\rightarrow$ mở modal chọn Workspace `IR-UI-005`).
2. **Nhóm B (Đăng ký Doanh nghiệp):** Trang `/register-company` (`IR-UI-004`) với 6 trường thông tin bắt buộc, nút sinh mã workspace ngẫu nhiên (slug + 4 số), API kiểm tra trùng mã theo thời gian thực (`INT-030`), tái kiểm tra lúc submit, và tải lên tài liệu pháp lý trực tiếp vào MinIO bucket `company-documents` (tối đa 10MB/tệp, 50MB tổng cộng).
3. **Nhóm C (Phê duyệt Doanh nghiệp & Kiểm tra Hồ sơ):** Quản trị viên Nền tảng xem xét hồ sơ và tải/xem tài liệu pháp lý qua Presigned URL (`TTL = 15 phút`), từ chối kèm lý do bắt buộc (`BR-022`), hoặc phê duyệt kích hoạt chuỗi cấp phát nguyên tử 5 bước (`BR-023`, `OD-024`) tạo duy nhất 1 Primary Workspace (`DEC-WS-001`), khởi tạo tài khoản `COMPANY_ADMIN` ban đầu (Rollback 100% nếu có lỗi).
4. **Nhóm D (Phân rã Công việc bằng AI):** Tiếp nhận prompt ngôn ngữ tự nhiên hoặc lệnh `/task <đoạn văn>`, AI phân tích tạo đề xuất có cấu trúc (Yêu cầu hỗ trợ hoặc Phân công thực hiện). **Bắt buộc có sự xác nhận tường minh của con người (`BR-028`)**: AI tuyệt đối không tự ghi vào DB; hiển thị giao diện xem trước dạng checklist tương tác cho phép sửa inline/chọn lọc; chỉ tạo việc khi người dùng bấm "Xác nhận tạo việc".
5. **Nhóm E (Robot Chatbot & Thông báo Đa kênh):** Widget Robot dễ thương nhúng trên màn hình, mặc định tự động mở khi đăng nhập; nút `[X]` thu nhỏ chỉ có hiệu lực trong phiên làm việc hiện tại (`sessionStorage.bizflow_robot_dismissed = true`, `BR-029`), tự động mở lại ở phiên mới; trả lời ngôn ngữ tự nhiên bị giới hạn bởi RBAC/ManagementScope (`BR-027`); hỗ trợ lệnh `/task` và `/notifications`; thông báo nhắc hạn đa kênh (In-app SignalR khi online, Web Push khi offline/granted, Email fallback khi offline/unsubscribed, `BR-030`) với cơ chế chống trùng lặp qua `NotificationLog` (`OD-032`).

---

## 2. Bảng Tổng Hợp Thay Đổi Theo Tệp Tài Liệu

| STT | Đường dẫn Tệp | Phiên bản Cũ | Phiên bản Mới | Trọng tâm Thay đổi |
|---|---|---|---|---|
| 1 | `docs/requirements/rds/RDS_v1.0_DRAFT.md` | v1.0-DRAFT (2026-10-19) | v1.2-DRAFT (2026-10-21) | Cập nhật Scope §7.1, 7.3, 7.8; bổ sung Business Rules `BR-025`..`BR-030`; bổ sung Nhu cầu các bên liên quan `STKN-015`, `STKN-016`; bổ sung Business Requirements `BIZ-029`..`BIZ-033`; bổ sung Năng lực `CAP-014`, `CAP-015`; bổ sung Yêu cầu Thông tin `INFO-013`, `INFO-014`; bổ sung Tiêu chí nghiệm thu `AC-033`..`AC-047`; cập nhật Quyết định mở `OD-025`..`OD-032`; đồng bộ Ma trận Truy vết. |
| 2 | `docs/requirements/rds/REQUIREMENTS_CATALOG.csv` | 119 mục | 128 mục (128 dòng dữ liệu) | Thêm 9 mục mới: `STKN-015`, `STKN-016`, `BR-025`..`BR-030`, `BIZ-029`..`BIZ-033`, `CAP-014`, `CAP-015`, `INFO-013`, `INFO-014`. Cập nhật trạng thái và mức độ ưu tiên P0. |
| 3 | `docs/requirements/rds/USE_CASES.md` | v1.0-DRAFT (13 UCs) | v1.2-DRAFT (17 UCs) | Nâng cấp `UC-001` (6 trường, check code, sinh mã, upload MinIO); Thêm 4 use cases mới: `UC-014` (Đăng nhập Hợp nhất & Email-link), `UC-015` (Robot Chatbot Hỏi đáp & Lệnh tắt), `UC-016` (Phân rã Công việc bằng AI kèm Xác nhận Tường minh), `UC-017` (Nhắc hạn & Điều phối Thông báo Đa kênh). Cập nhật Section 3 Coverage Matrix. |
| 4 | `docs/requirements/rds/OPEN_DECISIONS.md` | 24 quyết định (`OD-001`..`OD-024`) | 32 quyết định (`OD-001`..`OD-032`) | Bổ sung Top 10 Câu hỏi Khảo sát Trọng điểm; Phân tích chi tiết phương án và khuyến nghị cho `OD-025` (Email-link & Workspace Selector), `OD-026` (Quy tắc mã Workspace), `OD-027` (Lưu trữ hồ sơ pháp lý MinIO), `OD-028` (Kiểm tra hồ sơ Platform Admin), `OD-029` (Bảo mật AI phân rã việc), `OD-030` (Vòng đời Robot widget), `OD-031` (VAPID Keys & Push Subscription), `OD-032` (Chống trùng lặp thông báo đa kênh). |
| 5 | `docs/requirements/rds/REQUIREMENTS_TRACEABILITY.md` | v1.0-DRAFT | v1.2-DRAFT | Đồng bộ hóa 100% hai chiều cho toàn bộ 128 mục trong Catalog RDS sang SRS và SSS. Không còn orphan hay dangling requirement. |
| 6 | `docs/requirements/srs/SRS_v1.0_DRAFT.md` | v1.0-DRAFT (119 items) | v1.2-DRAFT (132 items) | Cập nhật §3.1.1 (`FR-AUTH-001` 3 trường bắt buộc, thêm `FR-AUTH-005` Email-link); Cập nhật §3.1.2 (`FR-TEN-001` 6 trường + check-code + MinIO upload, `FR-TEN-002` duyệt hồ sơ pháp lý); Thêm §3.1.9 (`FR-NOTIF-003`, `FR-NOTIF-004`); Thêm §3.1.10 (`FR-AI-011`, `FR-AI-012`, `FR-AI-013`); Thêm giao diện `IR-UI-005`, `IR-UI-006`, `IR-API-004`..`007`, `IR-EXT-003`; Thêm dữ liệu `DR-016`, `DR-017`; Thêm kịch bản kiểm thử `TST-032`..`TST-037`; Cập nhật Mục 13.1 Bảng tổng kết và Mục 14.4 `OD-001`..`OD-032`. |
| 7 | `docs/requirements/srs/OPEN_DECISIONS.md` | 24 quyết định | 32 quyết định (`OD-001`..`OD-032`) | Đồng bộ hoàn toàn nội dung phân tích chi tiết của 8 quyết định mở kỹ thuật `OD-025`..`OD-032` với tài liệu RDS và SSS. |
| 8 | `docs/requirements/srs/REQUIREMENTS_CATALOG.csv` | 119 dòng dữ liệu | 132 dòng dữ liệu | Cập nhật chính xác 132 yêu cầu (68 FR, 20 IR, 17 DR, 15 NFR, 12 SEC), định nghĩa rõ Module ID, Source, Priority và Verification Method. |
| 9 | `docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md` | v1.0-DRAFT | v1.2-DRAFT | Cập nhật ma trận truy vết SRS sang RDS và SSS với 100% tỷ lệ phủ kín hai chiều. |
| 10 | `docs/specifications/sss/ARCHITECTURE_DECISIONS.md` | 7 ADRs (`ADR-SSS-001`..`007`) | 10 ADRs (`ADR-SSS-001`..`010`) | Bổ sung 3 Quyết định Kiến trúc quan trọng: `ADR-SSS-008` (Cơ chế Đăng nhập Đa phương thức Hợp nhất & Passwordless Email-Link), `ADR-SSS-009` (Robot Chatbot Nhúng & Quy trình Phân rã Việc AI có Con người Phê duyệt Tường minh), `ADR-SSS-010` (Hạ tầng Điều phối Thông báo Nhắc hạn Đa kênh với Bộ lọc Chống Trùng lặp). |
| 11 | `docs/specifications/sss/MODULE_CATALOG.md` | 15 mô-đun | 17 mô-đun logic | Nâng cấp `MOD-001` (hỗ trợ Option 1 3 trường và Option 2 Email-link); Nâng cấp `MOD-002` (hỗ trợ 6 trường, check code, sinh mã, upload MinIO); Bổ sung đầy đủ thông số cho 2 mô-đun mới: `MOD-014` (Robot Chatbot & Natural Language Assistance) và `MOD-015` (Deadline Reminder & Multi-Channel Notification Dispatcher). |
| 12 | `docs/specifications/sss/INTERFACE_CATALOG.md` | 28 interfaces (`INT-001`..`028`) | 32 interfaces (`INT-001`..`032`) | Bổ sung đặc tả API chi tiết cho: `INT-029` (Email-link Login API Suite), `INT-030` (Workspace Code Availability Suite), `INT-031` (AI Task Decomposition Batch Confirmation API), và `INT-032` (Deadline Reminder & Push Subscription Suite). |
| 13 | `docs/specifications/sss/diagrams/04-sequence-workflows.md` | 7 biểu đồ trình tự | 10 biểu đồ trình tự chuẩn Mermaid | Bổ sung 3 biểu đồ trình tự chi tiết: `SEQ-008` (Đăng nhập Hợp nhất và Luồng Email-Link kèm Modal Chọn Workspace), `SEQ-009` (Trợ lý Robot và Phân rã Công việc AI kèm Xác nhận Con người Tường minh), `SEQ-010` (Vòng đời Nhắc hạn, Web Push Service Worker và Điều phối Đa kênh Chống Trùng lặp). |
| 14 | `docs/specifications/sss/SSS_v1.0_DRAFT.md` | v1.0-DRAFT (2026-10-19) | v1.2-DRAFT (2026-10-21) | Cập nhật Tổng quan hệ thống; Nâng số lượng mô-đun lên 17 (`MOD-001`..`MOD-015`, `MOD-FE-001`, `MOD-BG-001`); Nâng số lượng Interface lên 32; Cập nhật 10 Sequence Workflows; Cập nhật Phân tích rủi ro; Đồng bộ 32 Open Decisions; Hoàn thiện Ma trận truy vết kiến trúc. |
| 15 | `docs/specifications/sss/SSS_TRACEABILITY.md` | v1.0-DRAFT | v1.2-DRAFT | Xác lập ánh xạ hai chiều hoàn chỉnh giữa 132 yêu cầu SRS, 17 mô-đun, 32 interfaces, 10 sequence diagrams và 37 kịch bản kiểm thử. |

---

## 3. Nhật Ký Chi Tiết Từng Nhóm Yêu Cầu và Mục Đã Chỉnh Sửa

### 3.1. Nhóm A — Đăng nhập Hợp nhất & Email-Link
- **RDS:**
  - Bổ sung `BR-025`: Quy tắc Đăng nhập Hợp nhất và Xác thực Đa chế độ.
  - Bổ sung `BIZ-029`: Nâng cao Trải nghiệm Tiếp cận Nền tảng qua Đăng nhập Linh hoạt.
  - Bổ sung `AC-033`, `AC-034`, `AC-035`: Tiêu chí nghiệm thu cho Đăng nhập 3 trường ID, Xác thực Email-link 1 Tenant, và Xác thực Email-link Đa Tenant qua Workspace Selector.
  - Bổ sung `UC-014`: Đăng nhập Hợp nhất và Đăng nhập qua Liên kết Email.
- **SRS:**
  - Sửa `FR-AUTH-001`: Yêu cầu bắt buộc đủ 3 trường (`Username/EmployeeCode`, `Password`, `WorkspaceKey`), phản hồi `401 AUTH.INVALID_CREDENTIALS` nếu sai.
  - Bổ sung `FR-AUTH-005`: Đăng nhập không cần mật khẩu qua Email-link và Hộp thoại chọn Workspace (`Workspace Selector`).
  - Bổ sung `IR-UI-005`: Hộp thoại Chọn Không gian Làm việc (Workspace Selector Dialog).
  - Bổ sung `IR-API-004`: Bộ API Đăng nhập qua Email-link (`INT-029`).
  - Bổ sung `DR-016`: Dữ liệu Phiên Tạm thời và Token Đăng nhập Không Mật khẩu.
  - Bổ sung `TST-032`: Kiểm thử Đăng nhập Hợp nhất và Xác thực Email-link.
- **SSS:**
  - Bổ sung `ADR-SSS-008`: Kiến trúc Đăng nhập Đa chế độ và Quản lý Phiên Tạm thời.
  - Cập nhật `MOD-001`: Thêm xử lý `EmailLinkAuthenticationHandler` và `WorkspaceResolutionService`.
  - Bổ sung `INT-029`: Định nghĩa chi tiết DTO, request/response, mã lỗi cho luồng email-link.
  - Bổ sung `SEQ-008`: Biểu đồ tuần tự Mermaid luồng đăng nhập hợp nhất và email-link.

### 3.2. Nhóm B — Đăng ký Doanh nghiệp Công khai & Tải Hồ sơ MinIO
- **RDS:**
  - Bổ sung `BR-026`: Quy tắc Tiếp nhận và Thẩm định Mã Doanh nghiệp Đăng ký.
  - Bổ sung `BIZ-030`: Tự phục vụ Đăng ký Doanh nghiệp Minh bạch và An toàn.
  - Bổ sung `INFO-013`: Hồ sơ Pháp lý và Tài liệu Đính kèm Doanh nghiệp.
  - Bổ sung `AC-036`, `AC-037`: Tiêu chí nghiệm thu 6 trường bắt buộc, sinh mã ngẫu nhiên, check code thời gian thực, upload MinIO.
  - Cập nhật `UC-001`: Mở rộng luồng đăng ký với 6 trường và kiểm tra mã tự động.
- **SRS:**
  - Cập nhật `FR-TEN-001`: Cưỡng chế 6 trường bắt buộc, sinh mã ngẫu nhiên slug + 4 số, real-time code availability check, upload MinIO `company-documents`.
  - Bổ sung `IR-UI-004`: Biểu mẫu Đăng ký Doanh nghiệp Mới Công khai.
  - Bổ sung `IR-API-005`: API Kiểm tra Tính Khả dụng của Mã Workspace (`INT-030`).
  - Bổ sung `DR-001` & `DR-016`: Bổ sung thực thể `CompanyRegistrationDocument` và các trường `TaxId`, `ContactPersonName`, `ContactPhone`.
  - Bổ sung `TST-033`: Kiểm thử Tiếp nhận Đăng ký Doanh nghiệp và Tải tệp MinIO.
- **SSS:**
  - Cập nhật `MOD-002`: Bổ sung `CompanyCodeAvailabilityService` và `LegalDocumentStorageHandler`.
  - Bổ sung `INT-030`: Chi tiết API kiểm tra mã workspace và sinh mã ngẫu nhiên.

### 3.3. Nhóm C — Phê duyệt Doanh nghiệp & Kiểm tra Hồ sơ Pháp lý
- **RDS:**
  - Cập nhật `BR-022`: Bắt buộc nhập lý do từ chối đăng ký doanh nghiệp.
  - Cập nhật `BR-023` & `OD-024`: Chuỗi cấp phát nguyên tử 5 bước cho Company + Tenant + Initial Company Admin.
  - Bổ sung `AC-038`, `AC-039`: Nghiệm thu luồng xem tài liệu presigned url và phê duyệt nguyên tử.
- **SRS:**
  - Cập nhật `FR-TEN-002`: Platform Admin thẩm định hồ sơ, cấp presigned url 15 phút, phê duyệt nguyên tử hoặc từ chối kèm lý do.
  - Bổ sung `IR-UI-006`: Giao diện Xem xét và Phê duyệt Hồ sơ Đăng ký của Platform Admin.
  - Bổ sung `TST-034`: Kiểm thử Phê duyệt Doanh nghiệp Nguyên tử và Xử lý Tài liệu Pháp lý.
- **SSS:**
  - Cập nhật `MOD-002`: Bổ sung `DocumentPresignedUrlGenerator` và đảm bảo giao dịch nguyên tử `NpgsqlTransaction` trong `CompanyProvisioningStore`.

### 3.4. Nhóm D — Phân rã Công việc bằng AI & Xác nhận Tường minh Con người
- **RDS:**
  - Bổ sung `BR-028`: Quy tắc Phân rã Công việc bằng AI và Phê duyệt Tường minh của Con người.
  - Bổ sung `CAP-014`: Năng lực Phân rã Công việc Thông minh bằng AI.
  - Bổ sung `BIZ-031`: Tối ưu hóa Năng suất Khởi tạo Luồng việc qua Trí tuệ Nhân tạo.
  - Bổ sung `AC-040`, `AC-041`, `AC-042`: Tiêu chí nghiệm thu phân tích ngữ nghĩa, giao diện preview checklist, và cam kết không ghi DB trước khi xác nhận.
  - Bổ sung `UC-016`: Phân rã Công việc bằng Trợ lý AI và Xác nhận Tường minh.
- **SRS:**
  - Bổ sung `FR-AI-013`: Phân rã Công việc từ Ngôn ngữ Tự nhiên và Lệnh `/task` kèm Xác nhận Tường minh.
  - Bổ sung `IR-API-006`: API Phân rã Công việc bằng AI và Xác nhận Hàng loạt (`INT-031`).
  - Bổ sung `TST-035`: Kiểm thử Phân rã Công việc AI và Phê duyệt Con người Tường minh.
- **SSS:**
  - Bổ sung `ADR-SSS-009`: Kiến trúc Trợ lý Robot Chatbot và Cơ chế Human-in-the-Loop trong Phân rã Công việc.
  - Bổ sung `MOD-014`: Mô-đun Trợ lý Robot Chatbot và Xử lý Ngôn ngữ Tự nhiên (`MOD-014`).
  - Bổ sung `INT-031`: API tách biệt giữa `POST .../preview` (chỉ trả DTO dự kiến) và `POST .../confirm` (thẩm định quyền và ghi DB).
  - Bổ sung `SEQ-009`: Biểu đồ tuần tự Mermaid luồng tương tác AI phân rã việc.

### 3.5. Nhóm E — Robot Chatbot Nhúng & Thông báo Đa kênh
- **RDS:**
  - Bổ sung `BR-027`: Quy tắc Giới hạn Phạm vi Dữ liệu và Chống Ảo giác của Trợ lý AI.
  - Bổ sung `BR-029`: Quy tắc Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget.
  - Bổ sung `BR-030`: Quy tắc Điều phối Thông báo Nhắc Hạn Đa kênh và Chống Trùng lặp.
  - Bổ sung `CAP-015`: Năng lực Trợ lý Ảo Đồng hành và Cảnh báo Đa kênh Chủ động.
  - Bổ sung `BIZ-032`, `BIZ-033`: Tương tác Thông minh Nội bộ và Đảm bảo Tiến độ qua Cảnh báo Đa kênh.
  - Bổ sung `AC-043`..`AC-047`: Tiêu chí nghiệm thu trạng thái widget sessionStorage, giới hạn quyền hạn NL query, nhắc hạn 4 mốc thời gian, điều phối đa kênh, và chống trùng lặp.
  - Bổ sung `UC-015`: Tương tác với Trợ lý Robot Chatbot Thông minh.
  - Bổ sung `UC-017`: Tự động Nhắc hạn và Điều phối Thông báo Đa kênh.
- **SRS:**
  - Bổ sung `FR-AI-011`: Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget.
  - Bổ sung `FR-AI-012`: Truy vấn Ngôn ngữ Tự nhiên Giới hạn theo Quyền hạn và Chống Ảo giác.
  - Bổ sung `FR-NOTIF-003`: Cấu hình Lịch Nhắc Hạn Cá nhân và Lệnh `/notifications`.
  - Bổ sung `FR-NOTIF-004`: Điều phối Thông báo Nhắc Hạn Đa kênh (In-app, Web Push, Email Fallback).
  - Bổ sung `IR-API-007`: API Đăng ký Web Push và Cấu hình Nhắc Hạn (`INT-032`).
  - Bổ sung `IR-EXT-003`: Cổng Dịch vụ Web Push Trình duyệt (W3C Push API / RFC 8292).
  - Bổ sung `DR-017`: Dữ liệu Đăng ký Web Push và Lịch Nhắc Hạn Công việc.
  - Bổ sung `TST-036`, `TST-037`: Kiểm thử Robot Chatbot và Điều phối Thông báo Đa kênh.
- **SSS:**
  - Bổ sung `ADR-SSS-010`: Kiến trúc Hệ thống Nhắc Hạn Tự động và Điều phối Đa kênh Chống Trùng lặp.
  - Bổ sung `MOD-015`: Mô-đun Điều phối Nhắc hạn và Thông báo Đa kênh.
  - Bổ sung `INT-032`: Chi tiết API đăng ký Web Push Subscription và Notification Dispatcher.
  - Bổ sung `SEQ-010`: Biểu đồ tuần tự Mermaid luồng quét deadline background service, Web Push và email fallback.

---

## 4. Cam kết Kiểm toán & Tính Toàn vẹn (Integrity Sign-off)

1. **Tuân thủ Nguyên tắc An toàn Tuyệt đối:** Trong suốt đợt cập nhật tài liệu v1.2, **không có bất kỳ tệp mã nguồn nào** thuộc các thư mục `backend/`, `frontend/`, `tests/`, `deploy/` bị chỉnh sửa hay can thiệp.
2. **Tính Nhất Quán Tuyệt Đối:** Mọi thuật ngữ, mã định danh (`FR-*`, `BR-*`, `UC-*`, `MOD-*`, `INT-*`, `ADR-*`, `TST-*`), đường dẫn file, tham số API và DTO đều đồng nhất 100% giữa RDS, SRS, SSS và các báo cáo Audit.
3. **Phê duyệt Phát hành:** Bộ tài liệu `v1.2-DRAFT` được phê duyệt làm cơ sở kỹ thuật chính thức và duy nhất cho các đợt phát triển tiếp theo của dự án.
