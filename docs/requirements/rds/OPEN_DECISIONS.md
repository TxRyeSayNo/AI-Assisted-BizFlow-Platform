# SỔ ĐĂNG KÝ QUYẾT ĐỊNH CÒN MỞ, KHOẢNG TRỐNG YÊU CẦU VÀ CÂU HỎI KHÁM PHÁ (OPEN DECISIONS & REQUIREMENTS GAPS)

- **Dự án:** AI-Assisted BizFlow Platform
- **Mã tài liệu:** `BIZFLOW-RDS-OD-v1.0`
- **Phiên bản:** `1.0-DRAFT`
- **Ngày lập:** `2026-10-18`
- **Trạng thái:** `ACTIVE — AWAITING STAKEHOLDER SIGN-OFF`
- **Tài liệu liên quan:** [RDS_v1.0_DRAFT.md](./RDS_v1.0_DRAFT.md), [SOURCE_REGISTER.md](./SOURCE_REGISTER.md), [USE_CASES.md](./USE_CASES.md), [REQUIREMENTS_TRACEABILITY.md](./REQUIREMENTS_TRACEABILITY.md), [SRS OPEN_DECISIONS.md](../srs/OPEN_DECISIONS.md)

---

## 1. Nguyên tắc Quản trị Quyết định Yêu cầu (Requirements Decision Governance)

Theo chuẩn mực Kỹ nghệ Yêu cầu (Requirements Engineering — tham chiếu ISO/IEC/IEEE 29148:2018) và quy tắc biên soạn dự án:

1. **Tuyệt đối không tự bịa phê duyệt:** Mọi mâu thuẫn giữa các tài liệu gốc, khoảng trống nghiệp vụ hoặc chỉ tiêu định lượng chưa được Product Owner / Stakeholder xác nhận chính thức đều phải giữ trạng thái `OPEN`.
2. **Tách biệt rõ Sự thật Đã Xác nhận (`CONFIRMED`) và Khuyến nghị Đề xuất (`PROPOSED — NOT APPROVED`):** Mỗi mục mở đều trình bày tối thiểu 2 phương án khả thi, phân tích tác động nghiệp vụ/kỹ thuật và nêu một khuyến nghị chuyên môn được gắn nhãn rõ ràng là **`PROPOSED — NOT APPROVED`**.
3. **Truy vết hai chiều đầy đủ:** Mọi yêu cầu trong [RDS_v1.0_DRAFT.md](./RDS_v1.0_DRAFT.md) và [REQUIREMENTS_CATALOG.csv](./REQUIREMENTS_CATALOG.csv) bị ảnh hưởng bởi quyết định mở đều tham chiếu trực tiếp tới mã `OD-001`..`OD-020` tại tài liệu này.

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

## 3. Bộ 7 Câu hỏi Khám phá Ưu tiên Cao nhất Dành cho Stakeholder (Top 7 High-Priority Discovery Questions)

Bộ 7 câu hỏi dưới đây được chắt lọc theo nguyên tắc **tác động cao nhất tới việc đóng băng baseline yêu cầu (Requirements Baseline Sign-off)** nhưng **ít làm phiền Stakeholder nhất**:

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

---

## 4. Danh mục Chi tiết các Quyết định Còn Mở (`OD-001` đến `OD-020`)

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
