# SỔ ĐĂNG KÝ QUYẾT ĐỊNH CÒN MỞ, KHOẢNG TRỐNG YÊU CẦU VÀ CÂU HỎI KHÁM PHÁ (OPEN DECISIONS & REQUIREMENTS GAPS)

- **Dự án:** AI-Assisted BizFlow Platform
- **Mã tài liệu:** `BIZFLOW-RDS-OD-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Ngày lập:** `2026-10-18` (Cập nhật đồng bộ v1.2: `2026-10-21`)
- **Trạng thái:** `ACTIVE — AWAITING STAKEHOLDER SIGN-OFF`
- **Tài liệu liên quan:** [RDS_v1.0_DRAFT.md](./RDS_v1.0_DRAFT.md), [SOURCE_REGISTER.md](./SOURCE_REGISTER.md), [USE_CASES.md](./USE_CASES.md), [REQUIREMENTS_TRACEABILITY.md](./REQUIREMENTS_TRACEABILITY.md), [SRS OPEN_DECISIONS.md](../srs/OPEN_DECISIONS.md)

---

## 1. Nguyên tắc Quản trị Quyết định Yêu cầu (Requirements Decision Governance)

Theo chuẩn mực Kỹ nghệ Yêu cầu (Requirements Engineering — tham chiếu ISO/IEC/IEEE 29148:2018) và quy tắc biên soạn dự án:

1. **Tuyệt đối không tự bịa phê duyệt:** Mọi mâu thuẫn giữa các tài liệu gốc, khoảng trống nghiệp vụ hoặc chỉ tiêu định lượng chưa được Product Owner / Stakeholder xác nhận chính thức đều phải giữ trạng thái `OPEN`.
2. **Tách biệt rõ Sự thật Đã Xác nhận (`CONFIRMED`) và Khuyến nghị Đề xuất (`PROPOSED — NOT APPROVED`):** Mỗi mục mở đều trình bày tối thiểu 2 phương án khả thi, phân tích tác động nghiệp vụ/kỹ thuật và nêu một khuyến nghị chuyên môn được gắn nhãn rõ ràng là **`PROPOSED — NOT APPROVED`**.
3. **Truy vết hai chiều đầy đủ:** Mọi yêu cầu trong [RDS_v1.0_DRAFT.md](./RDS_v1.0_DRAFT.md) và [REQUIREMENTS_CATALOG.csv](./REQUIREMENTS_CATALOG.csv) bị ảnh hưởng bởi quyết định mở đều tham chiếu trực tiếp tới mã `OD-001`..`OD-032` tại tài liệu này.

---

## 2. Danh mục Quyết định Đã Xác nhận Làm Chuẩn Cơ sở (Confirmed Decisions Baseline)

Các quyết định dưới đây đã có bằng chứng phê duyệt chính thức trong repository và là cơ sở bắt buộc (`CONFIRMED`) cho toàn bộ RDS, SRS và SSS:

| Mã Quyết định | Nội dung Quyết định Đã Xác nhận | Nguồn Phê duyệt | Trạng thái |
|---|---|---|---|
| `DEC-WS-001` | Mô hình Multi-Tenant SaaS trong phạm vi MVP cấp đúng **01 Không gian làm việc / Thuê bao chính (`Primary Workspace / Tenant`)** cho mỗi Doanh nghiệp (`Company`). Khả năng một doanh nghiệp sở hữu nhiều không gian làm việc độc lập thuộc lộ trình **Future Enterprise** (ngoài phạm vi MVP). | Định hướng dự án & [SRS.md](../../../documents/SRS/SRS.md) (`BR-002`) | `CONFIRMED` |
| `DEC-WS-002` | Không đưa các chức năng Tính phí, Thanh toán, Quản lý Gói cước và Hạn ngạch Thương mại (**Billing, Payment, Subscription Management, Commercial Entitlements**) vào phạm vi MVP. | Định hướng dự án & [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md) | `CONFIRMED` |
| `ADR-0001` | Áp dụng hệ thống thiết kế giao diện **GRID (Swiss Grids)** cho toàn bộ 16 màn hình chuẩn và 4 trạng thái hiển thị bắt buộc (`loading`, `empty`, `error`, `populated`). | [ADR-0001](../../01-decisions/ADR-0001-design-system.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0002` | Quản lý phiên xác thực có trạng thái thu hồi và cho phép duy nhất tài khoản Quản trị viên Nền tảng (`PLATFORM_ADMIN`) không gắn với `TenantId` của doanh nghiệp. | [ADR-0002](../../01-decisions/ADR-0002-authentication-persistence.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0003` | Ưu tiên đồ thị trạng thái Công việc (§9.1): công việc quá hạn bắt buộc phải chuyển `OVERDUE -> IN_PROGRESS -> SUBMITTED` (không nhảy thẳng từ `OVERDUE` sang `SUBMITTED`), và khi Quản lý từ chối kết quả tại `SUBMITTED` thì công việc quay lại `IN_PROGRESS` để làm lại. | [ADR-0003](../../01-decisions/ADR-0003-task-transition-precedence.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0004` | Bản chụp Phiên bản SLA (`SLAVersion`) và Lịch làm việc (`BusinessCalendar`) khi đã gắn với hồ sơ vận hành là **bất biến (immutable)** để bảo toàn tính chính xác lịch sử. | [ADR-0004](../../01-decisions/ADR-0004-sla-snapshot-immutability.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0005` | Thời gian cảnh báo sớm (`WarningMinutes`) được tính bằng số phút làm việc đã trôi qua kể từ lúc bắt đầu đồng hồ SLA (`0 <= WarningMinutes < TargetMinutes`). | [ADR-0005](../../01-decisions/ADR-0005-sla-warning-calendar-contract.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0006` | Quy tắc leo thang SLA (`EscalationRules`) tăng dần theo cấp `1..N` sau mốc `OVERDUE`, chỉ phát thông báo một lần cho mỗi cấp và giữ nguyên người chịu trách nhiệm hiện tại. | [ADR-0006](../../01-decisions/ADR-0006-sla-escalation-contract.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0007` | Hỗ trợ tra cứu lịch sử các phiên bản SLA đã phát hành phục vụ kiểm toán và đối soát. | [ADR-0007](../../01-decisions/ADR-0007-sla-version-history.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0008` | Tách quyền cập nhật dịch vụ nội bộ (`service.update`) và bắt buộc kiểm soát tương tranh lạc quan khi chỉnh sửa cấu hình dịch vụ. | [ADR-0008](../../01-decisions/ADR-0008-service-update-permission.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0009` | Chuẩn hóa vòng đời Yêu cầu (`Request`): khi người gửi từ chối kết quả giải quyết tại `RESOLVED`, yêu cầu quay về `IN_PROGRESS`; khi người gửi xác nhận hài lòng, yêu cầu chuyển `RESOLVED -> CONFIRMED -> CLOSED`; khi quá hạn tiếp tục xử lý thì chuyển `OVERDUE -> IN_PROGRESS`. | [ADR-0009](../../01-decisions/ADR-0009-request-transition-clarification.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0010` | Khi một Công việc (`Task`) được giao cho cả một Phòng ban, nhân viên đang hoạt động đầu tiên trong phòng ban bấm **Nhận việc (`accept`)** sẽ chiếm quyền sở hữu cá nhân nguyên tử đối với công việc đó. | [ADR-0010](../../01-decisions/ADR-0010-department-task-acceptance-claim.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0011` | Chính sách quyền xem Công việc phân tầng theo 4 phạm vi: toàn doanh nghiệp (`tenant`), phạm vi phòng ban quản lý (`managed`), công việc tự tạo/giao (`own`), và công việc được giao (`assigned`). | [ADR-0011](../../01-decisions/ADR-0011-scoped-task-read-policy.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0012` | Cho phép tạo Công việc độc lập không bắt buộc gắn sẵn Quy trình/SLA, bắt buộc hạn chót (`Deadline`) ở tương lai, chống tạo trùng lặp và trả về phản hồi không tiết lộ (`404`) đối với hồ sơ ngoài phạm vi quyền hạn. | [ADR-0012](../../01-decisions/ADR-0012-task-creation-and-detail-contract.md) | `CONFIRMED` & `IMPLEMENTED` |

---

## 3. Bộ 10 Câu hỏi Khám phá Ưu tiên Cao nhất Dành cho Stakeholder (Top 10 High-Priority Discovery Questions)

Bộ 10 câu hỏi dưới đây được chắt lọc theo nguyên tắc **tác động cao nhất tới việc đóng băng baseline yêu cầu (Requirements Baseline Sign-off)** nhưng **ít làm phiền Stakeholder nhất**:

1. **[Câu hỏi 1 — Quyền từ chối việc cấp phòng ban & Báo cáo tiến độ khi quá hạn (`OD-001`, `OD-002`)]:**
   Khi một Công việc (`Task`) được giao cho hàng đợi chung của một Phòng ban (chưa giao đích danh cá nhân), một nhân viên bình thường trong phòng ban có được phép bấm **"Từ chối nhận việc"** làm hủy phân công của cả phòng ban không, hay chỉ Quản lý phòng ban / người được giao đích danh mới có quyền từ chối? Ngoài ra, khi công việc đang ở trạng thái `OVERDUE`, người thực hiện có phải bấm **"Tiếp tục xử lý" (`OVERDUE -> IN_PROGRESS`)** trước khi gửi báo cáo tiến độ không?
2. **[Câu hỏi 2 — Phạm vi trạng thái cho phép Hủy Yêu cầu & Phân quyền thao tác Request (`OD-003`, `OD-020`)]:**
   Người gửi hoặc người xử lý có được phép hủy (`CANCELLED`) một Yêu cầu dịch vụ (`Request`) khi yêu cầu đang ở trạng thái **Chờ phê duyệt (`WAITING_APPROVAL`)** hoặc **Quá hạn (`OVERDUE`)** không? Khi Quản trị viên Công ty ngừng kích hoạt một Dịch vụ (`Service`), các Yêu cầu đang mở của dịch vụ đó sẽ tiếp tục chạy đến khi đóng hay bị yêu cầu hủy?
3. **[Câu hỏi 3 — Quản lý nhân sự không có địa chỉ Email & Cưỡng chế đổi mật khẩu tạm (`OD-004`, `OD-007`)]:**
   Để phục vụ nhóm nhân viên vận hành trực tiếp không có email cá nhân/công ty (đăng nhập bằng Mã nhân viên `EmployeeCode` và nhận mật khẩu tạm thời từ Quản trị viên Công ty theo `BIZ-002` / `FR-AUTH-004`), Stakeholder có xác nhận cho phép trường Email của nhân viên là **không bắt buộc** và bắt buộc người dùng phải đổi mật khẩu ngay ở lần đăng nhập đầu tiên không?
4. **[Câu hỏi 4 — Quy trình duyệt doanh nghiệp mới & Quy tắc trùng tên phòng ban (`OD-005`, `OD-006`)]:**
   Khi Quản trị viên Nền tảng (`PLATFORM_ADMIN`) bấm phê duyệt đơn đăng ký công ty, hệ thống sẽ kích hoạt ngay lập tức Không gian làm việc (`PENDING -> ACTIVE`) trong một bước duy nhất hay cần trạng thái chờ khởi tạo trung gian? Đồng thời, tên hiển thị của phòng ban (`Department Name`) có bắt buộc duy nhất trên toàn công ty hay cho phép trùng tên giữa các chi nhánh/khối khác nhau (miễn là Mã phòng ban `Department Code` duy nhất)?
5. **[Câu hỏi 5 — Cơ chế vận hành Quy trình tùy chỉnh (`Workflow`) và Quy tắc hội tụ Phê duyệt nhiều người (`OD-008`, `OD-011`)]:**
   Khi một bước phê duyệt trong quy trình có chỉ định nhiều người phê duyệt, quy tắc mặc định để hoàn tất bước đó là **Tất cả người phê duyệt cùng đồng ý (Unanimous / ALL)** hay **Chỉ cần người đầu tiên đồng ý (First-Responder / ANY)**? Các bước trong phiên bản quy trình sẽ ánh xạ trực tiếp vào tập trạng thái chuẩn của hệ thống để đảm bảo báo cáo thống nhất đúng không?
6. **[Câu hỏi 6 — Quản trị Lịch làm việc mặc định & Độc lập đồng hồ SLA giữa Request và Task con (`OD-010`, `OD-012`)]:**
   Khi một Yêu cầu dịch vụ (`Request`) tạm dừng đồng hồ SLA do chờ người gửi bổ sung thông tin (`WAITING_FOR_INFO`) hoặc chờ phê duyệt (`WAITING_APPROVAL`), đồng hồ SLA của các Công việc nội bộ (`Task`) đã tách ra từ yêu cầu đó sẽ **tiếp tục tính độc lập theo tiến độ từng công việc** hay tạm dừng dây chuyền theo yêu cầu cha?
7. **[Câu hỏi 7 — Xác nhận các Ngưỡng Nghiệm thu Phi chức năng, Giới hạn AI & KPI Kinh doanh (`OD-013`..`OD-016`, `OD-019`)]:**
   Stakeholder có đồng ý phê duyệt bộ chỉ tiêu nghiệm thu đề xuất cho giai đoạn MVP (Độ trễ API thông thường `p95 <= 500 ms`, Báo cáo/Dashboard `p95 <= 2000 ms` ở `100` người dùng đồng thời; AI timeout `15 giây` kèm cơ chế suy giảm an toàn không chặn thao tác thủ công; lưu trữ hồ sơ trực tuyến tối thiểu `365 ngày` không xóa cứng; mục tiêu giảm `>= 30%` thời gian điều phối yêu cầu nội bộ) làm cơ sở nghiệm thu chính thức không?
8. **[Câu hỏi 8 — Mô hình Tiếp nhận Doanh nghiệp (Admin-Gated vs. Self-Service), Xác minh Email, Đăng ký lại và Rollback Khởi tạo Workspace (`OD-021`..`OD-024`)]:**
   Trong phạm vi MVP theo định hướng Multi-Tenant SaaS (`DEC-WS-001`: 1 Doanh nghiệp = 1 Workspace chính), Stakeholder muốn giữ mô hình **`PLATFORM_ADMIN` phê duyệt thủ công** (`CONFIRMED` tại `FR-TEN-001`..`002`) hay chuyển sang/hỗ trợ song song phương án **Tự đăng ký doanh nghiệp (`Self-Service Company Registration` — `PROPOSED — NOT APPROVED`)** tự động khởi tạo Workspace chính và cấp quyền `COMPANY_ADMIN` ngay sau khi người đại diện xác minh email? Đồng thời, xin xác nhận chính sách: (a) thời hạn hiệu lực mã xác minh email (đề xuất `24 giờ`), (b) cho phép người dùng đăng ký lại (`Re-register`) bằng cùng email/mã công ty khi đơn cũ đã bị từ chối (`REJECTED`) hoặc hết hạn xác minh, và (c) thực thi khởi tạo `Company + Primary Tenant/Workspace + Initial COMPANY_ADMIN` trong một giao dịch nguyên tử (rollback 100% nếu bất kỳ bước khởi tạo hay cấp quyền ban đầu nào thất bại).
9. **[Câu hỏi 9 — Phương thức Đăng nhập Độc lập bằng Email-Link & Trải nghiệm Chọn Không gian làm việc (`OD-025`)]:**
   Tại trang Đăng nhập hợp nhất, bên cạnh phương thức đăng nhập bằng ID (bắt buộc nhập đủ 3 trường: ID, Mật khẩu, Mã workspace), hệ thống cung cấp tùy chọn Đăng nhập bằng Email-link (chỉ nhập Email, không cần mật khẩu hay mã workspace trước). Xin Stakeholder xác nhận:
   (a) Thời hạn hiệu lực của Email-link dùng 1 lần (đề xuất: `15 phút`).
   (b) Quy tắc điều hướng: Nếu người dùng thuộc đúng 01 Workspace thì tự động đăng nhập thẳng vào Workspace đó hay vẫn hiển thị màn hình chọn Workspace? Nếu người dùng thuộc $\ge 2$ Workspace thì hiển thị Workspace Selector để chọn. Nếu email chưa thuộc Workspace nào thì hiển thị thông báo từ chối hay điều hướng sang trang Đăng ký doanh nghiệp?
10. **[Câu hỏi 10 — Trải nghiệm Chatbot Robot Mặc định Mở & Cơ chế Thông báo Ngoài Trang Web Push / Email (`OD-028`, `OD-029`, `OD-030`)]:**
   Về giao diện Trợ lý ảo Robot và kênh Thông báo deadline:
   (a) Xin Stakeholder xác nhận cơ chế widget Robot xuất hiện mặc định mở sau khi đăng nhập thành công; nút [X] ở góc trên bên phải đóng widget chỉ có hiệu lực trong phiên làm việc hiện tại (`sessionStorage`) và tự động mở lại ở lần đăng nhập tiếp theo.
   (b) Xác nhận quy trình Phân rã công việc bằng AI (`/task` hoặc NL prompt): Bắt buộc người dùng phải xác nhận rõ ràng trên giao diện (xem lại, chỉnh sửa checklist, bấm nút "Xác nhận tạo việc") thì hệ thống mới được phép ghi vào DB (`BR-028`).
   (c) Xác nhận quy tắc thông báo ngoài trang: Ưu tiên Web Push qua Service Worker trình duyệt khi người dùng cấp quyền; tự động fallback sang gửi Email thông báo khi người dùng offline hoặc chưa cấp quyền Web Push.

---

## 4. Danh mục Chi tiết các Quyết định Còn Mở (`OD-001` đến `OD-032`)

### `OD-001`: Thẩm quyền Từ chối Nhận việc đối với Công việc Giao cho Phòng ban
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BR-005`, `BIZ-009`, `FR-TASK-003`, `ADR-0010`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-009`, `CAP-005`, `UC-005`, `FR-TASK-003`, `AC-010`.
- **Mô tả vấn đề:** `ADR-0010` đã xác nhận bất kỳ nhân viên đang hoạt động nào thuộc phòng ban được giao việc đều có thể bấm **Nhận việc (`accept`)** để nhận trách nhiệm cá nhân nguyên tử. Tuy nhiên, khi công việc vẫn đang nằm ở hàng đợi chung của phòng ban (chưa gán đích danh cá nhân), tài liệu gốc chưa quy định rõ một nhân viên thường trong phòng ban có được phép bấm **Từ chối nhận việc (`reject`)** khiến công việc bị trả về trạng thái `REJECTED` đối với cả phòng ban hay không.
- **Các phương án khả thi:**
  - **Phương án A:** Chỉ cho phép từ chối nhận việc khi công việc được giao đích danh cho cá nhân đó, hoặc do Quản lý (`MANAGER`) có phạm vi quản lý phòng ban đó thực hiện từ chối kèm lý do bắt buộc. Nhân viên thường chỉ có quyền nhận việc (`accept`) từ hàng đợi phòng ban chứ không có quyền từ chối thay cho cả phòng ban.
  - **Phương án B:** Cho phép bất kỳ thành viên nào trong phòng ban từ chối công việc cấp phòng ban.
- **Phân tích tác động:** Phương án B gây rủi ro một nhân viên bấm từ chối làm toàn bộ đồng nghiệp trong cùng phòng ban mất công việc khỏi hàng đợi chung; Phương án A bảo vệ tính toàn vẹn của hàng đợi phòng ban và đúng trách nhiệm quản lý.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-002`: Trạng thái Hợp lệ để Cập nhật Tiến độ và Gửi Báo cáo Tiến độ Công việc
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-010`, `FR-TASK-005`, `FR-TASK-006`, `ADR-0003`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-010`, `CAP-005`, `UC-006`, `FR-TASK-005`, `FR-TASK-006`, `AC-011`.
- **Mô tả vấn đề:** Theo `ADR-0003`, một công việc đang ở trạng thái quá hạn (`OVERDUE`) bắt buộc phải được chuyển về `IN_PROGRESS` trước khi nộp kết quả (`SUBMITTED`). Chưa có xác nhận tương tự cho thao tác cập nhật phần trăm tiến độ / checklist (`FR-TASK-005`) và gửi báo cáo tiến độ (`FR-TASK-006`), cũng như việc gửi báo cáo văn bản có bắt buộc truyền kèm con số phần trăm tiến độ mới hay không.
- **Các phương án khả thi:**
  - **Phương án A:** Chỉ cho phép cập nhật tiến độ và gửi báo cáo khi công việc đang ở trạng thái `IN_PROGRESS` (nếu đang `OVERDUE` thì người xử lý bấm tiếp tục xử lý sang `IN_PROGRESS` trước); cho phép truyền tỷ lệ phần trăm tiến độ tùy chọn khi gửi báo cáo (nếu bỏ trống thì giữ nguyên mức phần trăm hiện tại).
  - **Phương án B:** Cho phép gửi báo cáo tiến độ trực tiếp ngay cả khi trạng thái đang là `OVERDUE`.
- **Phân tích tác động:** Phương án A giữ cho máy trạng thái Công việc (§9.1) nhất quán tuyệt đối với `ADR-0003`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-003`: Tập Trạng thái Cho phép Hủy Yêu cầu Dịch vụ và Phân quyền Chi tiết trên Request
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), [request-lifecycle-and-routing.md](../../02-traceability/request-lifecycle-and-routing.md), `BIZ-013`, `FR-REQ-002`, `FR-REQ-005`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-013`, `CAP-006`, `UC-007`, `UC-008`, `FR-REQ-002`, `FR-REQ-005`, `AC-015`.
- **Mô tả vấn đề:**
  1. Máy trạng thái Yêu cầu (§9.2) ghi chú chuyển sang `CANCELLED` từ "các trạng thái đang hoạt động trước `CLOSED`", nhưng chưa liệt kê tường minh có bao gồm `WAITING_APPROVAL` và `OVERDUE` hay không.
  2. Danh mục quyền chuẩn hiện có `requests.create`, `requests.route`, `requests.process`, `requests.approve`; cần làm rõ thao tác yêu cầu bổ sung thông tin, từ chối và hủy sử dụng quyền `requests.process` (kèm quyền tự hủy của chính người tạo yêu cầu) hay tách mã quyền mới.
- **Các phương án khả thi:**
  - **Phương án A:** Cho phép hủy (`CANCELLED`) từ mọi trạng thái đang mở trước khi giải quyết (`SUBMITTED`, `ROUTED`, `RECEIVED`, `WAITING_FOR_INFO`, `WAITING_APPROVAL`, `IN_PROGRESS`, `OVERDUE`); sử dụng quyền `requests.process` cho bộ phận xử lý và cho phép chính người tạo yêu cầu (`Requester`) tự hủy yêu cầu của mình khi chưa chuyển sang `RESOLVED`.
  - **Phương án B:** Chỉ cho phép hủy ở một số trạng thái hạn hẹp và tách thêm 3 mã quyền mới (`requests.reject`, `requests.cancel`, `requests.information`).
- **Phân tích tác động:** Phương án A đáp ứng đúng nhu cầu thực tế khi nhân viên không còn nhu cầu sử dụng dịch vụ trong lúc đang chờ duyệt, đồng thời giữ bộ quyền RBAC gọn nhẹ, nhất quán với tài liệu gốc.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-004`: Hỗ trợ Nhân viên Không có Địa chỉ Email (`Optional Email for Employee Code Login`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BR-001`, `BIZ-001`, `BIZ-002`, `FR-AUTH-001`, `FR-AUTH-004`.
- **Yêu cầu bị ảnh hưởng:** `STKN-003`, `BIZ-001`, `BIZ-002`, `BIZ-005`, `CAP-001`, `CAP-003`, `UC-002`, `UC-003`, `INFO-002`, `FR-AUTH-004`, `FR-ORG-001`.
- **Mô tả vấn đề:** Yêu cầu nghiệp vụ `BR-001` và `FR-AUTH-004` xác định rõ trong doanh nghiệp có những nhân viên không có địa chỉ email (đăng nhập bằng Mã nhân viên `EmployeeCode` và được `COMPANY_ADMIN` cấp mật khẩu tạm thời). Tuy nhiên, một số bảng biểu kỹ thuật ở tài liệu hệ thống cũ vẫn ghi trường Email là bắt buộc (`NOT NULL`).
- **Các phương án khả thi:**
  - **Phương án A:** Cho phép thông tin Email của người dùng thuộc doanh nghiệp là **tùy chọn (không bắt buộc)**; nếu có nhập Email thì Email đó phải duy nhất trong phạm vi doanh nghiệp. Tài khoản Quản trị viên Nền tảng (`PLATFORM_ADMIN`) vẫn bắt buộc có Email.
  - **Phương án B:** Bắt buộc mọi nhân viên phải có địa chỉ Email mới được tạo tài khoản.
- **Phân tích tác động:** Phương án B mâu thuẫn trực tiếp với `BR-001` và `FR-AUTH-004`; Phương án A giải quyết triệt để bài toán nhân viên hiện trường/vận hành không có hộp thư điện tử.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-005`: Trạng thái Phê duyệt và Khởi tạo Không gian làm việc cho Doanh nghiệp Mới
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-003`, `FR-TEN-002`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-003`, `CAP-002`, `UC-001`, `INFO-001`, `FR-TEN-002`, `AC-001`.
- **Mô tả vấn đề:** Khi `PLATFORM_ADMIN` chấp thuận đơn đăng ký doanh nghiệp (`FR-TEN-002`), hệ thống cần ghi nhận quyết định phê duyệt và khởi tạo Không gian làm việc mặc định (`Primary Tenant`) cùng tài khoản `COMPANY_ADMIN` ban đầu. Cần thống nhất việc phê duyệt và kích hoạt diễn ra nguyên tử trong một bước (`PENDING -> ACTIVE` kèm sự kiện kiểm toán `COMPANY.APPROVED`) hay tách thành trạng thái `APPROVED` trung gian.
- **Các phương án khả thi:**
  - **Phương án A:** Thực hiện phê duyệt và khởi tạo dữ liệu mặc định của Tenant trong **cùng một giao dịch nguyên tử**, chuyển trạng thái doanh nghiệp từ `PENDING` sang `ACTIVE` ngay khi hoàn tất và lưu vết kiểm toán `COMPANY.APPROVED` + `TENANT.PROVISIONED`.
  - **Phương án B:** Tách trạng thái `APPROVED` riêng và chạy tiến trình khởi tạo bất đồng bộ.
- **Phân tích tác động:** Khởi tạo một Tenant mới chỉ gồm các bản ghi cấu hình hạt giống (vai trò chuẩn, lịch mặc định), thực hiện nguyên tử theo Phương án A giúp doanh nghiệp sử dụng được ngay và tránh lỗi kẹt ở trạng thái trung gian.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-006`: Quy tắc Duy nhất đối với Tên Phòng ban trong Doanh nghiệp
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-006`, `FR-ORG-004`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-006`, `CAP-003`, `UC-003`, `INFO-002`, `FR-ORG-004`, `AC-006`.
- **Mô tả vấn đề:** `FR-ORG-004` nêu cả Tên phòng ban (`Name`) và Mã phòng ban (`Code`) đều không được trùng lặp trong phạm vi một công ty, trong khi lược đồ hiện tại mới chỉ cưỡng chế duy nhất trên Mã phòng ban (`Code`).
- **Các phương án khả thi:**
  - **Phương án A:** Bắt buộc cả Mã phòng ban (`Code`) và Tên phòng ban (`Name`) đang hoạt động đều phải duy nhất (không phân biệt hoa thường) trong phạm vi một doanh nghiệp (`Tenant`).
  - **Phương án B:** Chỉ bắt buộc duy nhất theo Mã phòng ban (`Code`), còn Tên phòng ban chỉ cần duy nhất trong cùng một phòng ban cha (`ParentDepartment`).
- **Phân tích tác động:** Phương án A tuân thủ đúng câu chữ của `FR-ORG-004` và tránh nhầm lẫn khi hiển thị danh sách chọn phòng ban trên giao diện định tuyến hoặc giao việc.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-007`: Quy trình Cấp Mật khẩu Tạm thời và Bắt buộc Đổi Mật khẩu Lần đầu
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-12` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-002`, `FR-AUTH-004`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-002`, `CAP-001`, `UC-002`, `FR-AUTH-004`, `AC-004`.
- **Mô tả vấn đề:** Khi `COMPANY_ADMIN` cấp mật khẩu tạm thời cho nhân viên không có email (`FR-AUTH-004`), hệ thống cần cơ chế đánh dấu tài khoản đang dùng mật khẩu tạm và buộc nhân viên phải đổi sang mật khẩu cá nhân mới ngay trong lần đăng nhập đầu tiên trước khi truy cập dữ liệu nghiệp vụ.
- **Các phương án khả thi:**
  - **Phương án A:** Khi Admin tạo/cấp lại mật khẩu tạm thời, hệ thống bật cờ `MustChangePassword = true` và thu hồi mọi phiên cũ; khi người dùng đăng nhập bằng mật khẩu tạm, hệ thống yêu cầu thực hiện bước đổi mật khẩu bắt buộc trước khi cho phép thực hiện các thao tác nghiệp vụ khác.
  - **Phương án B:** Không bắt buộc đổi mật khẩu sau khi nhận mật khẩu tạm.
- **Phân tích tác động:** Phương án A bảo đảm tính bảo mật và trách nhiệm giải trình cá nhân (Quản trị viên không thể biết mật khẩu lâu dài của nhân viên).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-008`: Cơ chế Cập nhật Bản nháp Quy trình (`Workflow Draft`) và Ánh xạ sang Trạng thái Chuẩn
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-06` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-016`, `FR-WF-001`, `FR-WF-002`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-016`, `CAP-007`, `UC-004`, `INFO-007`, `FR-WF-001`, `FR-WF-002`.
- **Mô tả vấn đề:** Cần làm rõ cách Quản trị viên Công ty lưu cấu hình danh sách các bước (`WorkflowStep`) và điều kiện chuyển bước (`WorkflowTransition`) khi phiên bản quy trình đang ở trạng thái nháp (`DRAFT`), cũng như mối quan hệ giữa bước quy trình tùy chỉnh với máy trạng thái chuẩn của Công việc (§9.1) và Yêu cầu (§9.2).
- **Các phương án khả thi:**
  - **Phương án A:** Cho phép cập nhật trọn gói danh sách bước và chuyển tiếp trên phiên bản `DRAFT` trước khi phát hành (`PUBLISHED` là bất biến); mỗi bước quy trình gắn với một giai đoạn chuẩn của vòng đời hồ sơ (`INTAKE`, `APPROVAL`, `EXECUTION`, `CONFIRMATION`, `COMPLETION`) để không làm vỡ máy trạng thái chuẩn §9.1/§9.2 và báo cáo toàn hệ thống.
  - **Phương án B:** Cho phép từng dịch vụ tự định nghĩa tập trạng thái hoàn toàn tự do, thay thế máy trạng thái §9.1/§9.2.
- **Phân tích tác động:** Phương án B sẽ phá vỡ khả năng thống kê đồng nhất của Dashboard/SLA và mâu thuẫn với `ADR-0003`/`ADR-0009`; Phương án A vừa cho phép tùy biến các bước xử lý/phê duyệt vừa giữ vững kỷ luật trạng thái chuẩn.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-009`: Cơ chế Thông báo Đơn đăng ký Doanh nghiệp Mới cho Quản trị viên Nền tảng (`PLATFORM_ADMIN`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-13` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-003`, `FR-TEN-001`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-003`, `CAP-002`, `CAP-009`, `UC-001`, `FR-TEN-001`.
- **Mô tả vấn đề:** Khi một doanh nghiệp mới gửi hồ sơ đăng ký (`FR-TEN-001`), hệ thống phải thông báo cho `PLATFORM_ADMIN`. Do `PLATFORM_ADMIN` hoạt động ở Mặt phẳng Nền tảng (`TenantId = NULL` theo `ADR-0002`), cần xác nhận hộp thư thông báo trong ứng dụng có hỗ trợ bản ghi thông báo cấp nền tảng (`TenantId = NULL`) hay không.
- **Các phương án khả thi:**
  - **Phương án A:** Cho phép bản ghi thông báo (`Notification`) có `TenantId = NULL` dành riêng cho các sự kiện cấp nền tảng gửi tới `PLATFORM_ADMIN`, kết hợp gửi thông báo qua kênh email/hàng đợi quản trị.
  - **Phương án B:** Chỉ gửi email cho `PLATFORM_ADMIN` mà không tạo thông báo trong ứng dụng.
- **Phân tích tác động:** Phương án A giúp trải nghiệm nhận thông báo trên giao diện của `PLATFORM_ADMIN` đồng nhất với các tác nhân khác.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-010`: Quản trị Lịch Làm việc Mặc định của Doanh nghiệp và Liên kết với Phiên bản SLA
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](../../02-traceability/pending-owner-decisions.md), `BIZ-018`, `FR-SLA-001`, `ADR-0004`, `ADR-0005`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-018`, `CAP-008`, `UC-004`, `INFO-008`, `FR-SLA-001`.
- **Mô tả vấn đề:** Theo `ADR-0004`, mỗi khi doanh nghiệp thay đổi lịch làm việc (giờ hành chính, ngày nghỉ lễ), hệ thống tạo một bản chụp `BusinessCalendar` mới bất biến để không làm sai lệch các hồ sơ cũ. Cần xác định cách hệ thống đánh dấu lịch làm việc nào đang là lịch mặc định hiện hành của doanh nghiệp khi phát hành `SLAVersion` mới.
- **Các phương án khả thi:**
  - **Phương án A:** Lưu định danh lịch làm việc mặc định hiện hành trong thiết lập cấu hình của doanh nghiệp (`TenantSetting` với khóa `"sla.default_calendar_id"`), đồng thời mỗi `SLAVersion` khi tạo mới sẽ ghi nhận cứng mã `BusinessCalendar` tại thời điểm đó để đảm bảo tính bất biến theo `ADR-0004`.
  - **Phương án B:** Chỉ dùng một lịch làm việc duy nhất cho mọi phiên bản SLA.
- **Phân tích tác động:** Phương án A thỏa mãn trọn vẹn cả nhu cầu cập nhật lịch nghỉ lễ hàng năm lẫn yêu cầu bất biến lịch sử của `ADR-0004`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-011`: Quy tắc Hội tụ khi Bước Phê duyệt Có Nhiều Người Phê duyệt (`Multi-Approver Quorum`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-05` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), `BR-012`, `BIZ-017`, `FR-WF-003`, `FR-WF-004`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-017`, `CAP-007`, `UC-007`, `UC-008`, `FR-WF-003`, `FR-WF-004`, `AC-018`.
- **Mô tả vấn đề:** Khi một quy tắc phê duyệt chỉ định nhiều người duyệt ở cùng một bước hoặc nhiều bước liên tiếp, cần quy định rõ điều kiện để bước phê duyệt đó được coi là hoàn tất (`APPROVED`) hoặc bị từ chối (`REJECTED`).
- **Các phương án khả thi:**
  - **Phương án A:** Các bước phê duyệt khác thứ tự (`stepOrder = 1, 2, ...`) chạy **tuần tự**; trong cùng một bước nếu có nhiều người phê duyệt thì mặc định yêu cầu **Tất cả cùng đồng ý (`ALL / Unanimous`)** mới chuyển sang bước tiếp theo, và **Bất kỳ người duyệt nào từ chối (`First Rejection`)** sẽ làm toàn bộ phiên phê duyệt bị từ chối ngay lập tức kèm lý do bắt buộc (`BR-022`).
  - **Phương án B:** Chỉ cần người đầu tiên bấm đồng ý (`ANY / First-Responder`) là bước phê duyệt hoàn tất.
- **Phân tích tác động:** Phương án A chặt chẽ về mặt kiểm soát nội bộ và tránh việc một yêu cầu nhạy cảm được duyệt khi người có thẩm quyền đồng cấp khác chưa đồng ý (có thể mở rộng cấu hình `ALL | ANY` với mặc định là `ALL`).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-012`: Mối quan hệ Tạm dừng Đồng hồ SLA giữa Yêu cầu Cha (`Request`) và Công việc Con (`Task`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-09` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), `BR-016`, `BIZ-019`, `FR-SLA-004`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-019`, `CAP-008`, `UC-007`, `UC-008`, `FR-SLA-002`, `FR-SLA-004`, `AC-020`.
- **Mô tả vấn đề:** Theo `BR-016` và `FR-SLA-004`, đồng hồ SLA tạm dừng khi hồ sơ ở trạng thái chờ thông tin (`WAITING_FOR_INFO`) hoặc chờ phê duyệt (`WAITING_APPROVAL`). Khi một `Request` đang có nhiều `Task` con liên kết, nếu `Request` chuyển sang trạng thái chờ thì đồng hồ SLA của các `Task` con có tự động tạm dừng theo hay không?
- **Các phương án khả thi:**
  - **Phương án A:** Mỗi `Request` và mỗi `Task` duy trì đồng hồ SLA (`SLAState`) độc lập theo vòng đời trạng thái của chính hồ sơ đó (`Request` tạm dừng khi `Request` ở `WAITING_FOR_INFO` / `WAITING_APPROVAL`; các `Task` con vẫn tính SLA theo hạn chót và trạng thái thực thi của từng `Task`).
  - **Phương án B:** Tạm dừng đồng loạt mọi `Task` con mỗi khi `Request` cha tạm dừng.
- **Phân tích tác động:** Trong thực tế, một Yêu cầu (ví dụ: Onboarding nhân sự mới) có thể gồm nhiều Task ở các bộ phận khác nhau (cấp máy tính, tạo tài khoản, chuẩn bị chỗ ngồi); việc một hạng mục chờ duyệt không nên làm đóng băng đồng hồ SLA của các hạng mục độc lập khác. Phương án A phản ánh đúng thực tế vận hành.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-013`: Cấu hình Tải Chuẩn và Phân vị Đo lường cho Yêu cầu Hiệu năng (`Workload & Percentile Baseline`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `B-11` trong [05-assumptions-and-ambiguities.md](../../00-analysis/05-assumptions-and-ambiguities.md), `QREQ-001`, `QREQ-002`, `QREQ-003`, `NFR-PERF-001`, `NFR-PERF-002`, `NFR-SCAL-001`.
- **Yêu cầu bị ảnh hưởng:** `QREQ-001`, `QREQ-002`, `QREQ-003`, `NFR-PERF-001`, `NFR-PERF-002`, `NFR-SCAL-001`, `AC-027`.
- **Mô tả vấn đề:** Tài liệu gốc nêu mục tiêu thời gian phản hồi thao tác thông thường `<= 500 ms`, báo cáo/dashboard `<= 2 giây` và chịu tải `100 người dùng đồng thời`, nhưng chưa định nghĩa phân vị thống kê (`p95` hay trung bình) và quy mô dữ liệu thử nghiệm.
- **Các phương án khả thi:**
  - **Phương án A:** Áp dụng ngưỡng đo ở phân vị thứ 95 (**`p95 <= 500 ms`** cho API đọc/ghi nghiệp vụ thường, **`p95 <= 2000 ms`** cho truy vấn Dashboard/Báo cáo) dưới tải **`100 người dùng đồng thời`** (80% đọc, 20% ghi) trên bộ dữ liệu kiểm thử gồm tối thiểu `10 Tenants`, `10.000 Tasks` và `10.000 Requests`.
  - **Phương án B:** Chỉ đo thời gian phản hồi trung bình trên cơ sở dữ liệu rỗng.
- **Phân tích tác động:** Phương án A cung cấp tiêu chí kiểm thử tải (Load Testing) rõ ràng, khách quan và lặp lại được.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-014`: Ngưỡng Thời gian Chờ (Timeout), Thử lại (Retry) và Giới hạn Tốc độ Gọi AI (`AI Rate Limit & Cost Guardrails`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [ai-assisted-operations.md](../../02-traceability/ai-assisted-operations.md), `QREQ-008`, `QREQ-009`, `NFR-AI-001`, `NFR-AI-002`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-021`, `BIZ-022`, `CAP-012`, `UC-010`, `QREQ-008`, `QREQ-009`, `EXT-002`, `FR-AI-001`..`FR-AI-010`, `AC-023`.
- **Mô tả vấn đề:** Việc tích hợp nhà cung cấp mô hình ngôn ngữ lớn bên ngoài (OpenAI API) cần có ngưỡng timeout cứng, số lần retry tối đa và giới hạn tần suất gọi (rate limit) để tránh treo giao diện người dùng hoặc phát sinh chi phí vượt kiểm soát.
- **Các phương án khả thi:**
  - **Phương án A:** Thiết lập timeout tối đa **`15 giây`** cho mỗi yêu cầu AI (mục tiêu `p95 <= 8 giây`), tối đa **`01 lần thử lại`** khi gặp lỗi mạng tạm thời, giới hạn mặc định **`30 lượt gọi AI / phút / người dùng`** và **`300 lượt gọi AI / giờ / doanh nghiệp`**. Khi vượt hạn mức hoặc khi dịch vụ AI gặp sự cố, hệ thống trả về thông báo suy giảm an toàn (graceful degradation) và giữ nguyên 100% khả năng thao tác thủ công của người dùng (`BR-019`).
  - **Phương án B:** Không giới hạn thời gian chờ và số lượt gọi AI.
- **Phân tích tác động:** Phương án A bảo đảm tính sẵn sàng của luồng nghiệp vụ cốt lõi và kiểm soát rủi ro chi phí vận hành.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-015`: Chính sách Thời hạn Lưu trữ Hồ sơ (Data Retention) và Dọn dẹp Phiên Tải tệp Chưa Hoàn tất
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `BR-021`, `BIZ-023`, `BIZ-024`, `INFO-009`, `INFO-011`, `FR-COL-003`, `DR-014`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-023`, `BIZ-024`, `CAP-010`, `CAP-013`, `UC-012`, `INFO-009`, `INFO-011`, `QREQ-007`.
- **Mô tả vấn đề:** Quy tắc `BR-021` cấm xóa cứng hồ sơ nghiệp vụ và nhật ký kiểm toán, nhưng chưa xác định thời gian lưu trữ trực tuyến tối thiểu và thời gian hủy các phiên khởi tạo tải tệp (`Upload Session`) bị bỏ dở.
- **Các phương án khả thi:**
  - **Phương án A:** Lưu trữ trực tuyến toàn bộ hồ sơ công việc, yêu cầu, tệp bằng chứng và nhật ký kiểm toán tối thiểu **365 ngày** trong phạm vi MVP (cấm xóa cứng); các phiên tải tệp lên kho đối tượng chưa hoàn tất bước xác nhận (`PENDING upload sessions`) tự động hết hạn sau **24 giờ**.
  - **Phương án B:** Không quy định thời hạn hết hạn cho phiên tải tệp dở dang.
- **Phân tích tác động:** Phương án A vừa tuân thủ quy tắc bảo toàn lịch sử `BR-021` vừa tránh lãng phí dung lượng lưu trữ MinIO/S3 do các phiên tải lên bị ngắt giữa chừng.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-016`: Chỉ tiêu Độ Sẵn sàng Dịch vụ (Availability SLA) và Khôi phục Thảm họa (`RPO / RTO`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `QREQ-004`, `QREQ-015`, `NFR-REL-001`, `NFR-BCDR-001`.
- **Yêu cầu bị ảnh hưởng:** `QREQ-004`, `QREQ-015`, `NFR-REL-001`, `NFR-BCDR-001`.
- **Mô tả vấn đề:** Tài liệu gốc chưa chốt con số cam kết độ sẵn sàng (Uptime %), Mục tiêu Điểm khôi phục (`RPO`) và Mục tiêu Thời gian khôi phục (`RTO`) cho môi trường triển khai MVP.
- **Các phương án khả thi:**
  - **Phương án A:** Mục tiêu vận hành MVP: Độ sẵn sàng đạt **`>= 99.5%`** trong khung giờ làm việc hành chính; sao lưu tự động hàng ngày đối với cơ sở dữ liệu và kho tệp đính kèm với **`RPO <= 24 giờ`** và **`RTO <= 4 giờ`**.
  - **Phương án B:** Cam kết hạ tầng cụm đa vùng `99.99%` ngay ở bản MVP.
- **Phân tích tác động:** Phương án A phù hợp với kiến trúc Modular Monolith và nguồn lực vận hành thực tế của giai đoạn MVP.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-017`: Phạm vi Ngôn ngữ Hiển thị Giao diện (UI Localization) và Hiển thị Múi giờ trong MVP
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `QREQ-014`, `NFR-I18N-001`, khảo sát giao diện `frontend/src/app`.
- **Yêu cầu bị ảnh hưởng:** `QREQ-010`, `QREQ-014`, `NFR-USAB-001`, `NFR-I18N-001`.
- **Mô tả vấn đề:** Người dùng doanh nghiệp nhập dữ liệu nghiệp vụ bằng tiếng Việt có dấu hoặc tiếng Anh, trong khi nhãn giao diện hiện tại trên 16 màn hình chuẩn sử dụng tiếng Anh. Cần xác nhận phạm vi ngôn ngữ giao diện của bản MVP.
- **Các phương án khả thi:**
  - **Phương án A:** Toàn bộ dữ liệu đầu vào/đầu ra, tìm kiếm, bình luận, tên tệp và nội dung AI hỗ trợ đầy đủ **Unicode UTF-8 (tiếng Việt và tiếng Anh)**; thời gian lưu trữ chuẩn UTC và quy đổi theo múi giờ lịch làm việc của doanh nghiệp (`Asia/Ho_Chi_Minh`); nhãn giao diện chuẩn trong MVP sử dụng bộ thuật ngữ tiếng Anh thống nhất theo `UI_SPECIFICATION_GRID.md` (hỗ trợ đa ngôn ngữ nhãn giao diện ở bản mở rộng).
  - **Phương án B:** Bắt buộc dịch song ngữ toàn bộ nhãn giao diện ngay trong MVP.
- **Phân tích tác động:** Phương án A tương thích 100% với `UI_SPECIFICATION_GRID.md` và bộ kiểm thử giao diện hiện hữu.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-018`: Lộ trình Mở rộng Đa Không gian làm việc cho Một Doanh nghiệp (`Future Enterprise Multi-Workspace`)
- **Trạng thái:** `OPEN` (Dành cho lộ trình Future Enterprise — **Ngoài phạm vi MVP**)
- **Nguồn phát sinh:** `DEC-WS-001`, `BIZ-004`, `INFO-001`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-003`, `BIZ-004`, `CAP-002`, `INFO-001`, `FR-TEN-002`, `FR-TEN-004`.
- **Mô tả vấn đề:** Quyết định `DEC-WS-001` đã chốt trong phạm vi MVP mỗi `Company` có đúng 01 `Primary Workspace / Tenant`. Đối với giai đoạn phát triển tương lai (Future Enterprise), khi một tập đoàn muốn tách nhiều Workspace cho các công ty thành viên, cần định hình nguyên tắc mở rộng mà không phải thiết kế lại mô hình cách ly dữ liệu hiện tại.
- **Các phương án khả thi:**
  - **Phương án A:** Trong MVP, giữ nguyên quy tắc `1 Company = 1 Primary Tenant` ở tầng nghiệp vụ và giao diện; đồng thời giữ cấu trúc tham chiếu `Tenant -> Company` để trong tương lai (Future Enterprise) có thể cho phép 1 `Company` khởi tạo thêm `Tenant` thứ 2..N mà toàn bộ quy tắc cách ly dữ liệu theo `TenantId` trên các phân hệ nghiệp vụ vẫn giữ nguyên không đổi.
  - **Phương án B:** Đưa tính năng quản lý đa workspace vào ngay MVP.
- **Phân tích tác động:** Phương án A tuân thủ nghiêm ngặt `DEC-WS-001` cho MVP và bảo vệ khả năng mở rộng kiến trúc dài hạn.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-019`: Số liệu Đo lường Cơ sở (Baseline Metrics) và Chỉ tiêu Thành công Kinh doanh Định lượng (Target KPIs)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Phần 3 của [RDS_v1.0_DRAFT.md](./RDS_v1.0_DRAFT.md), `BG-001`..`BG-008`.
- **Yêu cầu bị ảnh hưởng:** `BG-001`..`BG-008`, `STKN-001`..`STKN-014`.
- **Mô tả vấn đề:** Các tài liệu gốc trong repository xác định rõ 8 mục tiêu kinh doanh định tính (`BG-001`..`BG-008`), nhưng chưa cung cấp số liệu đo lường hiện trạng trước khi áp dụng hệ thống (AS-IS Baseline numbers) và tỷ lệ phần trăm cải thiện mục tiêu được phê duyệt chính thức (Approved Target KPIs).
- **Các phương án khả thi:**
  - **Phương án A:** Ghi nhận minh bạch các số liệu cơ sở hiện trạng là `TBD (Cần khảo sát tại doanh nghiệp thí điểm)` trong RDS, đồng thời thiết lập bộ chỉ số đo lường chuẩn mà hệ thống phải thu thập tự động (Thời gian định tuyến trung vị, Tỷ lệ hoàn thành đúng hạn SLA, Tỷ lệ hồ sơ bị trả lại do thiếu thông tin, Tỷ lệ chấp nhận gợi ý AI) kèm ngưỡng mục tiêu đề xuất gắn nhãn `PROPOSED — NOT APPROVED`.
  - **Phương án B:** Tự đặt các con số KPI giả định như thể đã được phê duyệt.
- **Phân tích tác động:** Phương án A tuân thủ tuyệt đối nguyên tắc trung thực bằng chứng của Kỹ nghệ Yêu cầu (không bịa số liệu kinh doanh).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-020`: Xử lý các Yêu cầu Đang Mở khi Ngừng Kích hoạt Dịch vụ Nội bộ (`Service Deactivation Impact`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `BIZ-007`, `FR-SVC-001`, `FR-REQ-001`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-007`, `CAP-004`, `UC-004`, `UC-007`, `FR-SVC-001`.
- **Mô tả vấn đề:** Khi `COMPANY_ADMIN` chuyển trạng thái một Dịch vụ nội bộ (`Service`) sang ngừng hoạt động (`INACTIVE`), hệ thống chặn việc tạo Yêu cầu mới cho dịch vụ đó. Tuy nhiên, cần xác định rõ các Yêu cầu (`Request`) đã gửi trước thời điểm ngừng kích hoạt và đang trong quá trình xử lý sẽ được xử lý như thế nào.
- **Các phương án khả thi:**
  - **Phương án A:** Chỉ chặn tạo Yêu cầu mới (`DRAFT` / `SUBMITTED` mới) đối với dịch vụ `INACTIVE`; toàn bộ các Yêu cầu đang mở (`SUBMITTED`, `ROUTED`, `RECEIVED`, `IN_PROGRESS`, `WAITING_APPROVAL`, `WAITING_FOR_INFO`, `RESOLVED`) được phép tiếp tục chạy hết vòng đời theo phiên bản quy trình và SLA đã ghim lúc tạo.
  - **Phương án B:** Tự động hủy toàn bộ Yêu cầu đang mở khi dịch vụ bị ngừng kích hoạt.
- **Phân tích tác động:** Phương án A bảo đảm tính liên tục của hoạt động vận hành và nhất quán với nguyên tắc ghim phiên bản cấu hình (`BR-013`, `BR-014`, `ADR-0004`).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-021`: Mô hình Tiếp nhận Doanh nghiệp (Admin-Approved vs. Self-Service Onboarding) và Điểm vào Đăng ký Công khai (`Landing / Registration Entry Point`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát chuyên sâu Company Registration, Tenant Onboarding & Workspace Provisioning; đối chiếu `SRC-002` (`FR-TEN-001`, `FR-TEN-002`), `SRC-004` (`UI-01`, `UI-02`), `DEC-WS-001`, `frontend/src/app/app.routes.ts`, `frontend/src/app/features/auth/login.html` và `backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs`.
- **Yêu cầu bị ảnh hưởng:** `STKN-001`, `BR-023`, `BR-024`, `BIZ-003`, `BIZ-027`, `BIZ-028`, `CAP-002`, `UC-001`, `UC-013`, `INFO-001`, `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `IR-UI-001`, `IR-UI-004`, `AC-001`, `AC-031`.
- **Mô tả vấn đề:**
  1. **Sự khác biệt giữa Baseline Đã Xác nhận (`CONFIRMED`) và Phương án Đề xuất Multi-Tenant SaaS (`PROPOSED — NOT APPROVED`):** Tài liệu gốc (`SRC-002` `FR-TEN-001`..`002`) xác nhận mô hình tiếp nhận doanh nghiệp có cổng kiểm duyệt bởi `PLATFORM_ADMIN` (đại diện doanh nghiệp gửi đơn $\rightarrow$ trạng thái `PENDING` $\rightarrow$ `PLATFORM_ADMIN` phê duyệt tại `UI-02` $\rightarrow$ khởi tạo `Primary Tenant`). Tuy nhiên, theo định hướng sản phẩm Multi-Tenant SaaS cho MVP, phương án **Tự đăng ký doanh nghiệp (`Self-Service Company Registration`)** — nơi đại diện doanh nghiệp đăng ký tài khoản quản trị cùng thông tin công ty, xác minh địa chỉ email và được hệ thống tự động khởi tạo ngay **01 Không gian làm việc chính (`Primary Workspace / Tenant`)** kèm gán quyền `COMPANY_ADMIN` ban đầu — là phương án đề xuất (`PROPOSED — NOT APPROVED`) giúp loại bỏ điểm nghẽn chờ duyệt thủ công.
  2. **Khoảng trống Điểm vào Đăng ký (`Landing Page / Registration Entry Point`) trong thực tế triển khai:** Trong mã nguồn hiện tại (`app.routes.ts`), đường dẫn gốc `''` chuyển hướng trực tiếp về `/login`, chưa có trang chủ giới thiệu (`Landing Page`) hay tuyến đường công khai `/register-company`; màn hình `/login` (`login.html`) chỉ có dòng chữ tĩnh *"Need access? Contact your company administrator."* mà chưa có liên kết hành động (CTA) dẫn tới biểu mẫu đăng ký doanh nghiệp mới.
- **Các phương án khả thi:**
  - **Phương án A (Giữ nguyên Baseline Đã Xác nhận — Admin-Approved Onboarding):** Bổ sung tuyến đường công khai `/register-company` và nút liên kết từ `/login` (`UI-01`); mọi đơn đăng ký sau khi gửi/xác minh email đều dừng ở trạng thái `PENDING` chờ `PLATFORM_ADMIN` phê duyệt thủ công tại `/platform/companies` (`UI-02`).
  - **Phương án B (Phương án Đề xuất cho MVP — Self-Service Company Registration có Cờ Cấu hình Linh hoạt — `PROPOSED — NOT APPROVED`):** Bổ sung điểm vào công khai `/register-company` (gắn liên kết CTA từ `/login` và trang gốc); đại diện doanh nghiệp đăng ký thông tin công ty (`CompanyName`, `CompanyCode`, `TenantKey`, `TimeZone`) và tài khoản quản trị viên đầu tiên (`FullName`, `Email`, `EmployeeCode`, `Password`). Ngay sau khi người đại diện hoàn tất bước **Xác minh Email (`Email Verification`)**, hệ thống tự động kích hoạt `Company (ACTIVE)`, khởi tạo **01 Không gian làm việc chính (`Primary Tenant`, `DEC-WS-001`)** và gán vai trò `COMPANY_ADMIN` mà không bắt buộc chờ `PLATFORM_ADMIN` duyệt tay (đồng thời hỗ trợ cờ cấu hình hệ thống `Onboarding:ApprovalMode = "SelfService" | "PlatformAdminApproval"` để doanh nghiệp vận hành nền tảng có thể bật lại chế độ duyệt tay khi cần).
- **Phân tích tác động:** Phương án B giữ nguyên vẹn ranh giới `1 Company = 1 Primary Workspace` (`DEC-WS-001`) và không phát sinh Billing/Subscription (`DEC-WS-002`), nhưng rút ngắn thời gian tiếp nhận khách hàng từ hàng giờ/ngày xuống vài giây, đồng thời không xóa bỏ khả năng kiểm duyệt của `PLATFORM_ADMIN` trên `UI-02`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án B** (hoặc giữ song song chế độ cấu hình `SelfService` / `PlatformAdminApproval`), nhưng **không tự ý ghi đè** yêu cầu `CONFIRMED` hiện hành (`BIZ-003` / `FR-TEN-001`..`002`) cho đến khi có phê duyệt chính thức của Product Owner.

---

### `OD-022`: Chính sách Xác minh Email (`Email Verification Policy`), Thời hạn Mã Xác minh và Xử lý Tài khoản / Doanh nghiệp Chưa Xác minh Email
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát luồng đăng ký tài khoản người dùng, tạo doanh nghiệp và xác minh email; đối chiếu `Company.cs`, `UserAccount.cs` và `PasswordResetService.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-024`, `BIZ-003`, `BIZ-027`, `BIZ-028`, `UC-001`, `UC-013`, `INFO-001`, `INFO-002`, `FR-TEN-001`, `FR-TEN-005`, `DR-002`, `DR-003`, `AC-031`, `AC-032`.
- **Mô tả vấn đề:** Trong mã nguồn hiện tại, thực thể `Company` chỉ có `ContactEmail` và `UserAccount` chỉ có `Email` mà chưa có thuộc tính đánh dấu trạng thái đã xác minh email (`EmailVerified` / `EmailVerifiedAt`) hay bảng/phiên lưu mã xác minh email đăng ký. Tài liệu hiện hành chưa quy định rõ:
  1. Khi đại diện doanh nghiệp vừa gửi đơn đăng ký nhưng **chưa xác minh email**, hồ sơ công ty và tài khoản người đại diện nằm ở trạng thái nào?
  2. Người đại diện có được phép đăng nhập vào hệ thống khi email chưa xác minh không?
  3. Thời hạn hiệu lực của liên kết/mã xác minh email là bao lâu và cơ chế gửi lại mã xác minh (`Resend Verification Email`) hoạt động như thế nào?
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Bắt buộc xác minh địa chỉ email của người đại diện khi đăng ký doanh nghiệp mới thông qua mã/liên kết xác minh một lần (băm SHA-256) có thời hạn hiệu lực **`24 giờ`** (cấu hình được).
    2. Trong khi email chưa được xác minh (`EmailVerified = false`), đơn đăng ký ở trạng thái chờ xác minh (`PENDING_VERIFICATION` hoặc `PENDING` với cờ `IsEmailVerified = false`), **chưa khởi tạo Không gian làm việc `ACTIVE`** và **chặn đăng nhập** vào Tenant Plane với thông báo rõ ràng (`AUTH.EMAIL_NOT_VERIFIED`), kèm tùy chọn yêu cầu gửi lại email xác minh (giới hạn tốc độ tối đa `3 lần / 15 phút` để chống spam).
    3. Đối với mô hình Admin-Approved (`BIZ-003`), chỉ những đơn đã xác minh email mới được phép bấm `Approve` (hoặc hiển thị rõ huy hiệu `Email Verified / Unverified` cho `PLATFORM_ADMIN` trên `UI-02`).
  - **Phương án B:** Không thực hiện xác minh email tự động trong MVP; cho phép kích hoạt tài khoản hoặc chờ `PLATFORM_ADMIN` kiểm tra thủ công ngoài hệ thống.
- **Phân tích tác động:** Phương án A ngăn chặn triệt để rủi ro kẻ tấn công đăng ký hàng loạt bằng email giả mạo hoặc email của người khác, đồng thời đảm bảo kênh email liên lạc của `COMPANY_ADMIN` đầu tiên hoạt động thực sự trước khi cấp phát Workspace.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-023`: Chính sách Xử lý Email Trùng, Mã Doanh nghiệp / `TenantKey` Trùng và Quy tắc Người dùng Đăng ký Lại (`Duplicate & Re-registration Governance`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát các trường hợp biên khi đăng ký doanh nghiệp: email trùng, mã công ty/tenant key trùng, người dùng đăng ký lại sau khi thất bại/từ chối/chưa xác minh; đối chiếu `Company.cs` (`CompanyStatus { Pending, Active, Suspended, Inactive }` — hiện chưa có `Rejected`) và `IdentityModelConfiguration.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-024`, `BIZ-003`, `BIZ-027`, `BIZ-028`, `UC-001`, `UC-013`, `INFO-001`, `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `DR-002`, `AC-032`.
- **Mô tả vấn đề:** Cần xác định rõ hành vi của hệ thống khi người dùng gửi biểu mẫu đăng ký doanh nghiệp mới mà `ContactEmail` / `AdminEmail`, `Company.Code` hoặc `Tenant.TenantKey` trùng với dữ liệu đã có trong hệ thống ở các trạng thái vòng đời khác nhau:
  1. Trùng với doanh nghiệp/tài khoản đang hoạt động (`ACTIVE`) hoặc tạm ngưng (`SUSPENDED`).
  2. Trùng với hồ sơ đăng ký đang chờ xác minh email (`UNVERIFIED`) hoặc đang chờ `PLATFORM_ADMIN` duyệt (`PENDING`).
  3. Trùng với hồ sơ đăng ký trước đó đã bị từ chối (`REJECTED`) hoặc đã hết hạn xác minh email (`EXPIRED`) — tức trường hợp **người dùng đăng ký lại (`Re-registration`)**.
  4. Đồng thời, trong mã nguồn `Company.cs` hiện tại, enum `CompanyStatus` chỉ có `{ Pending, Active, Suspended, Inactive }`, thiếu giá trị `Rejected` như đặc tả trong `FR-TEN-002`.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. **Trùng với hồ sơ `ACTIVE` / `SUSPENDED` / `PENDING` (đã xác minh):** Từ chối yêu cầu với mã lỗi xung đột (`409 Conflict` — `COMPANY.CODE_ALREADY_EXISTS`, `TENANT.KEY_ALREADY_EXISTS`, hoặc `REGISTRATION.EMAIL_ALREADY_IN_USE`) mà không ghi đè dữ liệu hiện có.
    2. **Trùng với hồ sơ đang chờ xác minh email (`UNVERIFIED` còn hạn):** Không tạo bản ghi trùng mới; trả về phản hồi hướng dẫn người dùng kiểm tra hộp thư hoặc kích hoạt luồng gửi lại mã xác minh email (`Resend Verification`).
    3. **Trường hợp Người dùng Đăng ký lại (`Re-registration`) sau khi đơn cũ bị `REJECTED` hoặc `EXPIRED`:** Cho phép người dùng đăng ký lại bằng cùng địa chỉ Email, `Company.Code` và `TenantKey` sau khi đã khắc phục lý do bị từ chối. Để tuân thủ quy tắc cấm xóa cứng (`BR-021`), hệ thống bảo toàn lịch sử lần từ chối trong `AuditLog` (hoặc lưu bản ghi đăng ký mới/cập nhật lại trạng thái đơn từ `REJECTED -> PENDING` kèm lịch sử xét duyệt), đồng thời bổ sung giá trị `Rejected` vào enum `CompanyStatus` trong tầng Domain/Persistence.
  - **Phương án B:** Dùng ràng buộc `UNIQUE` cứng không điều kiện trên bảng `Company` và chặn vĩnh viễn việc đăng ký lại với cùng `Code` hoặc `ContactEmail` ngay cả khi đơn trước đó đã bị `REJECTED` hoặc nhập sai/hết hạn.
- **Phân tích tác động:** Phương án B khiến doanh nghiệp hợp lệ bị khóa vĩnh viễn tên mã/email của chính họ nếu lần nộp đơn đầu tiên thiếu thông tin và bị từ chối; Phương án A giải quyết trọn vẹn bài toán đăng ký lại hợp lệ mà vẫn bảo toàn dấu vết kiểm toán bất biến (`BR-021`).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-024`: Tính Nguyên tử Giao dịch (`Transactional Atomicity`), Rollback Khi Khởi tạo Tenant / Workspace Không Hoàn tất và Xử lý Khi Cấp Quyền Owner / Admin Ban đầu Thất bại
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát rủi ro tạo tenant/workspace không hoàn tất và cấp quyền owner/admin ban đầu không thành công; đối chiếu `IdentityModelConfiguration.cs` (`Tenant.CompanyId` unique index, `CK_Role_Scope`) và `BizFlowDbContext.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-023`, `BIZ-003`, `BIZ-027`, `BIZ-028`, `UC-001`, `UC-013`, `INFO-001`..`003`, `FR-TEN-002`, `FR-TEN-005`, `DR-002`..`004`, `AC-001`, `AC-032`.
- **Mô tả vấn đề:** Việc hoàn tất onboarding một doanh nghiệp đòi hỏi chuỗi 5 tác vụ tạo lập dữ liệu liên hoàn:
  1. Chuyển trạng thái `Company` sang `ACTIVE`.
  2. Khởi tạo đúng **01 Không gian làm việc chính (`Primary Tenant / Workspace`)** gắn với `CompanyId` (`Unique(CompanyId)` theo `DEC-WS-001`) và chuyển sang `ACTIVE`.
  3. Khởi tạo bộ cấu hình hạt giống mặc định của Workspace (`TenantSettings`, `BusinessCalendar` mặc định).
  4. Tạo tài khoản người dùng đại diện đầu tiên (`UserAccount` gắn với `TenantId`, mật khẩu băm `Argon2id/BCrypt`, `Status = Active`).
  5. Gán vai trò quản trị cao nhất của doanh nghiệp (`COMPANY_ADMIN` thông qua bản ghi `UserRole` liên kết tới vai trò hệ thống `IsSystem = true`) và ghi nhận `AuditLog`.
  Nếu bước (1)–(2) thành công nhưng bước (3), (4) hoặc bước (5) **cấp quyền `COMPANY_ADMIN` ban đầu thất bại** (do lỗi ràng buộc dữ liệu, thiếu seed role `COMPANY_ADMIN`, xung đột định danh hoặc lỗi kết nối DB), doanh nghiệp sẽ rơi vào trạng thái "Workspace không người quản trị" (Adminless / Orphaned Workspace) hoặc kẹt ở `TenantStatus = Provisioning` không thể đăng nhập hay thử lại.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED` — Giao dịch Nguyên tử Tất-cả-hoặc-Không-gì `All-or-Nothing Atomic Unit of Work`):**
    1. Thực thi toàn bộ 5 bước (`Company Activation` + `Primary Tenant/Workspace Creation` + `Default Settings/Calendar Seeding` + `Initial Admin UserAccount Creation` + `Initial COMPANY_ADMIN Role Assignment` + `AuditLog`) bên trong **một giao dịch cơ sở dữ liệu ACID duy nhất** có khóa chống trùng lặp (`Idempotency-Key`).
    2. **Kiểm tra điều kiện bất biến trước khi Commit (`Post-Provisioning Invariant Check`):** Hệ thống xác minh tồn tại đúng `01 Primary Tenant (ACTIVE)` cho `Company` và tồn tại ít nhất `01 UserAccount (ACTIVE)` thuộc `Tenant` đó được gán vai trò `COMPANY_ADMIN`. Nếu bất kỳ bước nào hoặc điều kiện bất biến nào thất bại, hệ thống **Rollback 100% toàn bộ giao dịch** về trạng thái trước khi khởi tạo (ví dụ: giữ nguyên đơn ở trạng thái `PENDING` chờ duyệt/thử lại), ghi nhật ký lỗi kỹ thuật kèm `CorrelationId` và cho phép thực hiện lại (`Idempotent Retry`) an toàn.
    3. Tách tác vụ gửi email thông báo kích hoạt ra sau bước Commit DB (qua Outbox / hàng đợi thông báo) để lỗi SMTP bên ngoài không làm hỏng giao dịch khởi tạo hay gây rollback nhầm.
  - **Phương án B:** Khởi tạo bất đồng bộ từng phần không dùng chung một giao dịch ACID, giữ `Tenant.Status = Provisioning` khi lỗi và yêu cầu `PLATFORM_ADMIN` chạy công cụ sửa lỗi thủ công.
- **Phân tích tác động:** Phương án A loại bỏ hoàn toàn nguy cơ sinh ra Tenant mồ côi hoặc tài khoản chủ doanh nghiệp không có quyền `COMPANY_ADMIN`, đồng thời rất dễ kiểm thử tự động bằng Integration Test.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-025`: Cơ chế Bảo mật và Thời hạn Token Xác thực qua Email Link (`Magic Link Token Security & Expiration Policy`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Đăng nhập hợp nhất (Group A), `BR-025`, `BIZ-029`, `UC-014`, `AC-033`, `AC-034`.
- **Yêu cầu bị ảnh hưởng:** `BR-025`, `BIZ-029`, `UC-014`, `INFO-013`, `FR-AUTH-005`, `AC-033`, `AC-034`.
- **Mô tả vấn đề:** Phương thức đăng nhập bằng Email-link yêu cầu người dùng chỉ cung cấp địa chỉ Email (không cần mật khẩu hay mã workspace trước). Hệ thống tạo token xác thực và gửi link đăng nhập. Cần xác định: thời hạn token, cơ chế chống replay attack, giới hạn gửi lại (rate limiting), và luồng xử lý khi email liên kết với 0, 1 hoặc nhiều workspace.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Token ngẫu nhiên mật mã (CSPRNG 32 bytes), lưu băm SHA-256 trong DB/Redis với `TTL = 15 phút`, thu hồi ngay sau lần sử dụng đầu tiên (Single-use).
    2. Giới hạn tối đa `3 email / 15 phút` trên mỗi IP và email đích để chống brute-force và spam SMTP.
    3. Khi người dùng click link, backend xác thực token:
       - Nếu email thuộc đúng 01 Workspace đang hoạt động: cấp phát JWT session trực tiếp.
       - Nếu email thuộc $\ge 2$ Workspace: trả về token tạm thời (`SessionSelectToken` có `TTL = 5 phút`) và hiển thị Workspace Selector để người dùng chọn Workspace làm việc.
       - Nếu email chưa liên kết với bất kỳ Workspace nào: thông báo lỗi rõ ràng kèm gợi ý liên kết hoặc đăng ký doanh nghiệp mới.
  - **Phương án B:** Dùng JWT ký sẵn có thời hạn dài (24 giờ), không lưu trạng thái trên server, không có Workspace Selector (buộc người dùng phải nhập workspace code trước khi nhận link).
- **Phân tích tác động:** Phương án A bảo đảm an toàn thông tin cao nhất, tránh lộ token trong logs/lịch sử duyệt web, ngăn chặn replay attack và mang lại trải nghiệm tiện lợi đúng yêu cầu.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-026`: Thuật toán và Định dạng Sinh Mã Workspace Tự động (`Auto-generated Workspace Code Format & Fallback`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Đăng ký doanh nghiệp (Group B), `BR-026`, `BIZ-030`, `UC-001`, `AC-035`, `AC-036`.
- **Yêu cầu bị ảnh hưởng:** `BR-026`, `BIZ-030`, `UC-001`, `INFO-001`, `FR-TEN-001`, `AC-035`, `AC-036`.
- **Mô tả vấn đề:** Người dùng có thể tự nhập mã workspace mong muốn (`DesiredWorkspaceCode`) hoặc bấm nút "Tự động tạo mã" (`Generate Random Code`). Cần xác định quy tắc sinh mã tự động để bảo đảm tính duy nhất, thân thiện với URL/subdomain (`^[a-z0-9-]{3,30}$`) và không trùng lặp trong DB.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Sinh mã tự động theo công thức slug từ Tên công ty (loại bỏ dấu tiếng Việt, ký tự đặc biệt, chuyển chữ thường) kết hợp hậu tố ngẫu nhiên gồm 4 chữ số, ví dụ: `acme-8492`, `bizflow-1024`.
    2. Nếu tên công ty quá dài hoặc chưa nhập, sinh tiền tố mặc định `ws-` kèm chuỗi hex ngẫu nhiên 6 ký tự, ví dụ: `ws-a9b2c3`.
    3. Trước khi điền vào form, frontend gọi API `/api/tenants/check-code` để xác nhận mã khả dụng; khi submit form, backend thực hiện kiểm tra lại (re-check) để chống race condition.
  - **Phương án B:** Sinh chuỗi UUID thuần túy (ví dụ: `c8d19a3b-4f...`), khó đọc và khó nhớ đối với người dùng đăng nhập bằng ID.
- **Phân tích tác động:** Phương án A tạo mã dễ nhớ, chuyên nghiệp và phản ánh thương hiệu doanh nghiệp trong khi vẫn đảm bảo tính duy nhất.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-027`: Chính sách Lưu trữ và Quét Mã độc Tài liệu Hồ sơ Doanh nghiệp trên MinIO/S3 (`Supporting Document Storage & Security Scan`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Đăng ký doanh nghiệp (Group B), `BR-026`, `BIZ-030`, `UC-001`, `AC-037`.
- **Yêu cầu bị ảnh hưởng:** `BR-026`, `BIZ-030`, `UC-001`, `INFO-001`, `FR-TEN-001`, `FR-TEN-002`, `AC-037`.
- **Mô tả vấn đề:** Khi nộp đơn đăng ký, doanh nghiệp phải đính kèm tài liệu hỗ trợ/pháp lý (GPKD, giấy ủy quyền...) tải lên MinIO/S3 bucket `company-documents`. Cần quy định: dung lượng tối đa, định dạng cho phép, cơ chế quét mã độc (Antivirus scan), và thời hạn lưu trữ.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Định dạng hỗ trợ: PDF, PNG, JPG, JPEG. Dung lượng tối đa 10 MB/tệp, tổng không quá 50 MB cho mỗi hồ sơ đăng ký.
    2. Tài liệu được lưu trữ trong bucket riêng biệt `company-documents`, phân vùng theo đường dẫn an toàn: `companies/{registrationId}/docs/{uuid}_{filename}`.
    3. Mã hóa lưu trữ (SSE-S3), kiểm tra định dạng MIME thực tế (Magic bytes) và quét mã độc (ClamAV hoặc async scanner) trước khi chuyển trạng thái tài liệu sang `Available`.
    4. Cung cấp liên kết tải/xem cho `PLATFORM_ADMIN` thông qua Presigned URL có chữ ký tạm thời (`TTL = 15 phút`).
  - **Phương án B:** Lưu file trực tiếp vào PostgreSQL dạng `bytea`, không kiểm tra mã độc.
- **Phân tích tác động:** Phương án A tận dụng hạ tầng MinIO/S3 sẵn có trong dự án, đảm bảo hiệu năng database và bảo mật cấp doanh nghiệp.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-028`: Cơ chế Bắt buộc Xác nhận Rõ ràng trước khi Tạo Công việc từ AI (`Mandatory Human Confirmation Workflow for AI Task Decomposition`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Phân rã công việc bằng AI (Group D), `BR-028`, `BIZ-032`, `UC-016`, `AC-041`, `AC-042`.
- **Yêu cầu bị ảnh hưởng:** `BR-028`, `BIZ-032`, `UC-016`, `INFO-014`, `FR-AI-013`, `AC-041`, `AC-042`.
- **Mô tả vấn đề:** Khi AI phân tích một đoạn văn bản và đề xuất danh sách công việc/yêu cầu, hệ thống tuyệt đối không được tự động ghi vào DB. Cần chuẩn hóa quy trình xem lại, chỉnh sửa (tiêu đề, loại việc Support Request vs Task Assignment, phòng ban tiếp nhận, mô tả chi tiết), chọn lọc từng mục (checkbox) và hành động bấm nút "Xác nhận tạo việc" (`Explicit Confirmation`).
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. AI chỉ trả về cấu trúc JSON gồm danh sách tác vụ dự kiến hiển thị trên giao diện dưới dạng Checklist tương tác.
    2. Mỗi mục trong Checklist hiển thị rõ: Tiêu đề, Loại (`Support Request` hoặc `Task Assignment`), Phòng ban tiếp nhận dự kiến, Mô tả chi tiết, Độ ưu tiên, Deadline dự kiến.
    3. Người dùng có quyền: (a) Bỏ chọn từng mục không cần thiết, (b) Chỉnh sửa trực tiếp tiêu đề, phòng ban, mô tả, hạn chót của từng mục, (c) Thêm mới mục thủ công.
    4. Chỉ khi người dùng bấm nút hành động rõ ràng **"Xác nhận tạo việc ([N] mục đã chọn)"**, frontend mới gửi payload danh sách đã xác nhận lên backend.
    5. Backend kiểm tra quyền hạn của người dùng tại từng phòng ban đích trước khi ghi vào DB. Nếu không có quyền giao việc cho phòng ban nào, báo lỗi cụ thể cho mục đó và không ghi nhận mục đó.
  - **Phương án B:** Cung cấp tùy chọn "Tự động tạo ngay không cần xem lại" cho người dùng có quyền Quản trị.
- **Phân tích tác động:** Phương án B vi phạm nguyên tắc `BR-028` và rủi ro sinh rác dữ liệu do ảo giác AI; Phương án A đảm bảo quyền kiểm soát con người (Human-in-the-loop) tuyệt đối.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-029`: Định dạng Giao diện và Quản lý Trạng thái Thu gọn/Mở rộng của Robot Trợ lý Ảo (`Interactive Robot Widget Lifecycle & Session Storage`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Robot Trợ lý Ảo (Group E), `BR-029`, `BIZ-033`, `UC-015`, `AC-043`, `AC-044`.
- **Yêu cầu bị ảnh hưởng:** `BR-029`, `BIZ-033`, `UC-015`, `FR-AI-011`, `AC-043`, `AC-044`.
- **Mô tả vấn đề:** Widget robot trợ lý thông minh xuất hiện mặc định mở sau khi người dùng đăng nhập. Có nút [X] ở góc trên bên phải để đóng. Cần xác định phạm vi lưu trạng thái đóng của nút [X] để đảm bảo trải nghiệm người dùng nhất quán.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Sau khi người dùng đăng nhập thành công vào Workspace, widget robot hiển thị mặc định ở trạng thái mở tại góc dưới bên phải màn hình.
    2. Khi người dùng bấm nút [X] ở góc trên bên phải của widget, widget thu gọn thành một biểu tượng robot nổi (floating avatar). Trạng thái thu gọn này được lưu trong **`sessionStorage`** của trình duyệt (`bizflow_robot_dismissed = true`).
    3. Trong suốt phiên làm việc (kể cả khi chuyển trang/route), widget giữ nguyên trạng thái thu gọn và chỉ mở lại khi người dùng click vào biểu tượng robot.
    4. Khi người dùng đăng xuất và đăng nhập lại, hoặc phiên làm việc hết hạn/mở tab phiên mới, cờ `sessionStorage` được reset và robot **tự động hiển thị ở trạng thái mở** theo mặc định.
  - **Phương án B:** Lưu cờ đóng vào `localStorage` hoặc cấu hình tài khoản `UserPreferences` trong cơ sở dữ liệu vĩnh viễn.
- **Phân tích tác động:** Phương án B làm mất tính năng "mặc định mở sau mỗi lần đăng nhập"; Phương án A đáp ứng chính xác 100% yêu cầu nghiệp vụ.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-030`: Chiến lược Điều phối Đa kênh cho Thông báo Deadline và Đẩy Tin Ngoài Trang (`Web Push vs. Fallback Email Delivery Strategy`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Thông báo đa kênh (Group E), `BR-030`, `BIZ-033`, `UC-017`, `AC-046`, `AC-047`.
- **Yêu cầu bị ảnh hưởng:** `BR-030`, `BIZ-033`, `UC-017`, `INFO-009`, `FR-NOTIF-004`, `AC-046`, `AC-047`.
- **Mô tả vấn đề:** Thông báo deadline cần gửi cả khi người dùng đang trong ứng dụng (In-app) và ngoài ứng dụng (Off-site). Cần xác định giải pháp đẩy tin ngoài trang: Web Push qua Service Worker (VAPID keys) và fallback sang Email HTML.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. **Khi người dùng trực tuyến (In-app):** Phát thông báo đẩy thời gian thực qua SignalR Hub hiển thị Notification Toast và cập nhật Notification Center.
    2. **Khi người dùng ngoại tuyến (Off-site):**
       - **Kênh chính (Primary Off-site):** Gửi Web Push Notification qua Browser Service Worker (chuẩn W3C Push API / RFC 8292 VAPID). Trình duyệt hiển thị native system notification cho phép bấm vào để mở trực tiếp chi tiết công việc.
       - **Kênh dự phòng (Fallback Off-site):** Nếu người dùng chưa đăng ký nhận Web Push, hoặc đăng ký không hợp lệ/hết hạn, hoặc trình duyệt không hỗ trợ Web Push, hệ thống tự động gửi Email thông báo định dạng HTML qua SMTP/SendGrid.
  - **Phương án B:** Chỉ sử dụng duy nhất một kênh Email cho thông báo ngoài trang, không tích hợp Web Push.
- **Phân tích tác động:** Phương án B gây độ trễ thông báo cao và làm giảm tính hiện đại của nền tảng SaaS; Phương án A mang lại trải nghiệm đa kênh toàn diện.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-031`: Cơ chế Kiểm soát Chi phí Token và Giới hạn Tần suất cho Lệnh AI (`Token Quota, Rate Limiting & Fallback for AI Commands`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu AI Chatbot & Task Decomposition (Group D, E), `BR-027`, `BR-028`, `BR-029`, `AC-039`, `AC-041`.
- **Yêu cầu bị ảnh hưởng:** `BIZ-032`, `BIZ-033`, `CAP-014`, `FR-AI-012`, `FR-AI-013`.
- **Mô tả vấn đề:** Các lệnh AI (`/task`, hỏi đáp tự nhiên) gọi trực tiếp tới OpenAI API phát sinh chi phí token. Cần kiểm soát chi phí cho mỗi tenant và phòng chống tấn công lạm dụng API.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Áp dụng giới hạn tần suất (Rate Limiting) qua Redis: Tối đa `10 yêu cầu AI / phút / người dùng` và hạn ngạch `500 yêu cầu AI / ngày / workspace` cho phiên bản MVP.
    2. Áp dụng cơ chế tóm tắt ngữ cảnh (Context Truncation) và schema JSON nghiêm ngặt để tối ưu lượng token tiêu thụ.
    3. Khi gặp sự cố OpenAI API (timeout > 15s hoặc lỗi 429/500), hệ thống trả về thông báo lịch sự giải thích lỗi và hướng dẫn người dùng thực hiện thao tác thủ công, đảm bảo các chức năng thông thường không bị nghẽn.
  - **Phương án B:** Không áp dụng giới hạn, cho phép người dùng gọi OpenAI API không giới hạn.
- **Phân tích tác động:** Phương án A ngăn chặn rủi ro vượt ngân sách API của dự án và đảm bảo tính sẵn sàng của hạ tầng.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-032`: Chiến lược Khử trùng lặp và Lập lịch Nhắc nhở Deadline Hàng loạt (`Deadline Reminder Scheduling & Deduplication Policy`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Yêu cầu Thông báo deadline (Group E), `BR-030`, `BIZ-033`, `UC-017`, `AC-045`, `AC-046`.
- **Yêu cầu bị ảnh hưởng:** `BR-030`, `BIZ-033`, `UC-017`, `INFO-009`, `FR-NOTIF-003`, `FR-NOTIF-004`, `AC-045`, `AC-046`.
- **Mô tả vấn đề:** Lệnh `/notifications` cho phép người dùng tùy chỉnh lịch nhắc nhở (ví dụ: nhắc trước 24h, 4h, 1h, khi quá hạn). Cần có cơ chế lập lịch tập trung trên backend và khử trùng lặp để tránh việc nhiều worker/job quét lặp gây spam thông báo cho người dùng.
- **Các phương án khả thi:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Lập lịch tập trung bằng Background Worker (`IHostedService`) quét định kỳ mỗi 1 phút.
    2. Khử trùng lặp bằng bảng nhật ký `NotificationLog` với ràng buộc duy nhất composite `(TaskId, UserId, ReminderType, ScheduledTriggerTime)`. Worker chỉ phát thông báo nếu bản ghi chưa được tạo.
    3. Khi một công việc được hoàn thành (`COMPLETED`), hủy bỏ (`CANCELLED`) hoặc đóng (`CLOSED`), hệ thống đánh dấu hủy (`Invalidated`) toàn bộ các lịch nhắc nhở chưa kích hoạt còn lại của công việc đó.
  - **Phương án B:** Khởi tạo `Task.Delay` hoặc bộ đếm giờ riêng lẻ trong bộ nhớ ứng dụng cho từng công việc.
- **Phân tích tác động:** Phương án B mất toàn bộ bộ đếm giờ khi ứng dụng restart/crash; Phương án A bảo đảm tính bền vững và nhất quán trên môi trường phân tán.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.


