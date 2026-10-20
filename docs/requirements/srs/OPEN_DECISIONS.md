# DANH MỤC QUYẾT ĐỊNH CÒN MỞ VÀ CÂU HỎI KHÁM PHÁ (OPEN DECISIONS & DISCOVERY LOG)

- **Dự án:** AI-Assisted BizFlow Platform
- **Mã tài liệu:** `BIZFLOW-SRS-OD-v1.1`
- **Phiên bản:** `1.1-DRAFT`
- **Ngày lập:** `2026-10-18` (Cập nhật rà soát Onboarding: `2026-10-19`)
- **Trạng thái:** `ACTIVE — AWAITING OWNER SIGN-OFF`
- **Tài liệu liên quan:** [SRS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SRS_v1.0_DRAFT.md), [SOURCE_REGISTER.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SOURCE_REGISTER.md), [REQUIREMENTS_TRACEABILITY.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/REQUIREMENTS_TRACEABILITY.md)

---

## 1. Quy tắc Quản trị Quyết định (Decision Governance Rules)

Theo nguyên tắc kỹ nghệ yêu cầu (Requirements Engineering) của ISO/IEC/IEEE 29148:2018 và quy tắc biên soạn dự án:
1. **Không tự suy diễn phê duyệt:** Mọi mâu thuẫn giữa các nguồn tài liệu hoặc khoảng trống hợp đồng kỹ thuật chưa có ADR phê duyệt đều được giữ ở trạng thái `OPEN`.
2. **Đề xuất chuyên môn minh bạch:** Mỗi mục mở đều kèm theo phân tích các phương án (Options), đánh giá tác động (Impact Analysis) và một khuyến nghị kỹ thuật/nghiệp vụ được gắn nhãn rõ ràng là **`PROPOSED — NOT APPROVED`**.
3. **Không chặn tiến độ đặc tả:** Các yêu cầu trong [SRS_v1.0_DRAFT.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/requirements/srs/SRS_v1.0_DRAFT.md) liên quan đến quyết định mở đều dẫn chiếu trực tiếp tới mã `OD-xxx` tại tài liệu này.

---

## 2. Tóm tắt các Quyết định Đã Xác nhận (Confirmed Decisions Baseline)

Các quyết định dưới đây đã có bằng chứng phê duyệt rõ ràng trong repository và được áp dụng làm chuẩn bắt buộc (`CONFIRMED`) trong SRS:

| Mã Quyết định | Tiêu đề quyết định | Nguồn xác nhận | Trạng thái |
|---|---|---|---|
| `DEC-WS-001` | Mô hình Multi-Tenant SaaS với 1 Workspace/Tenant chính cho mỗi doanh nghiệp trong MVP; đa workspace cho một doanh nghiệp là định hướng Future Enterprise (ngoài phạm vi MVP). | Định hướng dự án & [SRS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/documents/SRS/SRS.md) | `CONFIRMED` |
| `DEC-WS-002` | Không đưa Billing, Payment, Subscription Management và Entitlement thương mại vào phạm vi MVP. | Định hướng dự án & [05-assumptions-and-ambiguities.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/00-analysis/05-assumptions-and-ambiguities.md) | `CONFIRMED` |
| `ADR-0001` | Áp dụng hệ thống thiết kế giao diện GRID (Swiss Grids, Plus Jakarta Sans, JetBrains Mono) cho toàn bộ 16 màn hình chuẩn. | [ADR-0001](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0001-design-system.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0002` | Bổ sung bảng thứ 43 `AuthenticationSession` lưu trữ băm phiên/reset token; cho phép `User.TenantId = NULL` duy nhất cho `PLATFORM_ADMIN`. | [ADR-0002](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0002-authentication-persistence.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0003` | Ưu tiên đồ thị trạng thái Task §9.1: bắt buộc `OVERDUE -> IN_PROGRESS -> SUBMITTED` (không nhảy thẳng `OVERDUE -> SUBMITTED`), và khi từ chối kết quả tại `SUBMITTED` thì chuyển `SUBMITTED -> IN_PROGRESS`. | [ADR-0003](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0003-task-transition-precedence.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0004` | Bản chụp `SLAVersion` và `BusinessCalendar` được tham chiếu là bất biến (immutable) kể từ khi tạo. | [ADR-0004](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0004-sla-snapshot-immutability.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0005` | `WarningMinutes` là số phút làm việc đã trôi qua từ lúc bắt đầu đồng hồ SLA (`0 <= WarningMinutes < TargetMinutes`); chuẩn hóa JSON lịch làm việc và ngày nghỉ. | [ADR-0005](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0005-sla-warning-calendar-contract.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0006` | Chuẩn hóa `EscalationRulesJson` theo cấp `1..N`, `afterMinutes` tăng nghiêm ngặt sau mốc `OVERDUE`, chỉ gửi thông báo một lần mỗi cấp và giữ nguyên người sở hữu. | [ADR-0006](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0006-sla-escalation-contract.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0007` | Bổ sung endpoint `GET /api/v1/sla-profiles/{id}/versions` với quyền `sla.read`. | [ADR-0007](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0007-sla-version-history.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0008` | Tách quyền `service.update` cho `PUT /api/v1/services/{id}` (mặc định cấp cho `COMPANY_ADMIN`) kèm kiểm tra tương tranh `If-Match` ETag. | [ADR-0008](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0008-service-update-permission.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0009` | Chuẩn hóa chuyển tiếp Request: `RESOLVED -> IN_PROGRESS` khi từ chối kết quả, `OVERDUE -> IN_PROGRESS` khi tiếp tục xử lý, và `RESOLVED -> CONFIRMED -> CLOSED` khi người yêu cầu xác nhận. | [ADR-0009](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0009-request-transition-clarification.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0010` | Cơ chế nhận việc nguyên tử một lần (`UserId = actorId`) khi nhân viên đang hoạt động thuộc phòng ban bấm nhận Task được giao cho phòng ban. | [ADR-0010](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0010-department-task-acceptance-claim.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0011` | Chính sách quyền đọc Task phân tầng theo phạm vi: `tasks.read.tenant`, `tasks.read.managed`, `tasks.read.own`, `tasks.read.assigned`. | [ADR-0011](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0011-scoped-task-read-policy.md) | `CONFIRMED` & `IMPLEMENTED` |
| `ADR-0012` | Quy tắc tạo Task chưa gắn Workflow/SLA dùng máy trạng thái §9.1 (`TD-01`), `Deadline` UTC tương lai có offset (`TD-02`), `Idempotency-Key` qua `AuditLog` (`TD-03`), và `GET /api/v1/tasks/{id}` trả `404` không tiết lộ (`TD-04`). | [ADR-0012](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/01-decisions/ADR-0012-task-creation-and-detail-contract.md) | `CONFIRMED` & `IMPLEMENTED` |

---

## 3. Bộ 7 Câu hỏi Khám phá Ưu tiên Cao nhất dành cho Stakeholder (Top 7 High-Impact Discovery Questions)

Dưới đây là 7 câu hỏi cốt lõi nhất mà câu trả lời từ Product Owner / Giảng viên hướng dẫn / Kiến trúc sư trưởng sẽ quyết định trực tiếp việc đóng các điểm mở về quyền hạn, máy trạng thái, lược đồ dữ liệu và tiêu chí nghiệm thu MVP:

1. **[Câu hỏi 1 — Thẩm quyền từ chối nhận việc và báo cáo tiến độ của Task (`OD-001`, `OD-002`)]:**
   Khi một Task được giao cho cả phòng ban (`DepartmentId` có giá trị, `UserId = null`), một nhân viên bất kỳ trong phòng ban có được quyền bấm **"Từ chối nhận việc" (`ASSIGNED -> REJECTED`)** khiến toàn bộ phòng ban mất việc đó không, hay chỉ người được giao đích danh / Quản lý phòng ban mới được từ chối? Đồng thời, người xử lý có được phép gửi báo cáo tiến độ (`FR-TASK-005/006`) khi Task đang ở trạng thái `OVERDUE` hay bắt buộc phải bấm "Tiếp tục xử lý" (`OVERDUE -> IN_PROGRESS`) trước?
2. **[Câu hỏi 2 — Tập trạng thái được phép hủy/quá hạn của Request và mã quyền RBAC (`OD-003`)]:**
   Yêu cầu nội bộ (`Request`) được phép hủy (`CANCELLED`) từ những trạng thái nào (chỉ `SUBMITTED, ROUTED, RECEIVED, WAITING_FOR_INFO, IN_PROGRESS`, hay bao gồm cả `WAITING_APPROVAL` và `OVERDUE`)? Có tách các mã quyền độc lập `requests.reject`, `requests.cancel`, `requests.information` khỏi quyền `requests.process` không?
3. **[Câu hỏi 3 — Hỗ trợ nhân viên không có Email và hợp đồng đổi mật khẩu tạm lần đầu (`OD-004`, `OD-007`)]:**
   Để đáp ứng `FR-AUTH-004` (nhân viên không có địa chỉ email được Admin cấp mật khẩu tạm thời), bạn có phê duyệt chuyển cột `User.Email` sang `NULLABLE` (kèm unique index khi `Email IS NOT NULL`) và bổ sung trường `MustChangePassword` + endpoint đổi mật khẩu bắt buộc ngay sau lần đăng nhập đầu tiên không?
4. **[Câu hỏi 4 — Trạng thái phê duyệt đăng ký doanh nghiệp và tính duy nhất của Tên phòng ban (`OD-005`, `OD-006`)]:**
   Trong quy trình duyệt đăng ký công ty (`FR-TEN-002`), việc phê duyệt và khởi tạo Tenant diễn ra trong **một giao dịch nguyên tử duy nhất** (`PENDING -> ACTIVE` kèm bản ghi kiểm toán `COMPANY.APPROVED`) hay tách thành hai bước có trạng thái `APPROVED` riêng? Ngoài ra, tên phòng ban (`Department.Name`) có bắt buộc duy nhất trong toàn công ty như `FR-ORG-004` nêu, hay chỉ bắt buộc duy nhất theo `Department.Code`?
5. **[Câu hỏi 5 — Cơ chế vận hành Workflow/Approval khi Dịch vụ không gắn hoặc có gắn Quy trình (`OD-008`, `OD-011`)]:**
   Khi một `Service` hoặc `Task` được gắn một `WorkflowVersion` cụ thể (`FR-WF-002`), các bước trong `WorkflowStep` sẽ ánh xạ 1-1 vào các trạng thái chuẩn của hệ thống (§9.1/§9.2) hay chạy song song như một tiến trình bước con? Đối với bước phê duyệt nhiều người (`FR-WF-003`), quy tắc mặc định là **tất cả cùng duyệt (Unanimous)** hay **người đầu tiên duyệt (First-responder)**?
6. **[Câu hỏi 6 — Quản trị Lịch làm việc mặc định và Cách tính SLA khi tạm dừng (`OD-010`, `OD-012`)]:**
   Hồ sơ SLA sẽ tham chiếu Lịch làm việc mặc định của Tenant qua khóa cấu hình `TenantSetting (key = "sla.default_calendar_id")` hay gắn khóa ngoại `BusinessCalendarId` trực tiếp trên từng `SLAVersion`? Khi yêu cầu chuyển sang `WAITING_FOR_INFO` hoặc `WAITING_APPROVAL` (`FR-SLA-004`), hệ thống chỉ tạm dừng cho `Request` hay tự động tạm dừng luôn đồng hồ SLA của các `Task` con liên kết?
7. **[Câu hỏi 7 — Ngưỡng cam kết Phi chức năng (NFR), Giới hạn AI và Chính sách lưu trữ dữ liệu (`OD-013`..`OD-016`)]:**
   Bạn có chấp thuận bộ ngưỡng nghiệm thu đề xuất cho môi trường MVP (API chuẩn `p95 <= 500 ms`, Dashboard/Report `p95 <= 2000 ms` ở mức 100 người dùng đồng thời; AI timeout `15 giây` với fallback không chặn luồng chính; RPO `<= 24h`, RTO `<= 4h`; giữ dữ liệu lịch sử tối thiểu 365 ngày và không xóa cứng) làm tiêu chí nghiệm thu chính thức không?
8. **[Câu hỏi 8 — Mô hình Tiếp nhận Doanh nghiệp (Admin-Gated vs. Self-Service), Xác minh Email, Đăng ký lại và Rollback Khởi tạo Workspace (`OD-021`..`OD-024`)]:**
   Trong phạm vi MVP theo định hướng Multi-Tenant SaaS (`DEC-WS-001`: 1 Doanh nghiệp = 1 Workspace chính), Stakeholder muốn giữ mô hình **`PLATFORM_ADMIN` phê duyệt thủ công** (`CONFIRMED` tại `FR-TEN-001`..`002`) hay chuyển sang/hỗ trợ song song phương án **Tự đăng ký doanh nghiệp (`Self-Service Company Registration` — `PROPOSED — NOT APPROVED`)** tự động khởi tạo Workspace chính và cấp quyền `COMPANY_ADMIN` ngay sau khi người đại diện xác minh email? Đồng thời, xin xác nhận chính sách: (a) thời hạn hiệu lực mã xác minh email (đề xuất `24 giờ`), (b) cho phép người dùng đăng ký lại (`Re-register`) bằng cùng email/mã công ty khi đơn cũ đã bị từ chối (`REJECTED`) hoặc hết hạn xác minh, và (c) thực thi khởi tạo `Company + Primary Tenant/Workspace + Initial COMPANY_ADMIN` trong một giao dịch nguyên tử (rollback 100% nếu bất kỳ bước khởi tạo hay cấp quyền ban đầu nào thất bại).

---

## 4. Sổ Đăng ký Chi tiết các Quyết định Còn Mở (`OD-001` đến `OD-024`)

### `OD-001`: Thẩm quyền từ chối nhận việc đối với Task giao cho Phòng ban
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L5-L18), `FR-TASK-003`, `BR-005`, `ADR-0010`.
- **Yêu cầu bị ảnh hưởng:** `FR-TASK-003`, `BR-005`, `UC-007`, `TST-010`.
- **Mô tả vấn đề:** `ADR-0010` đã chốt quy tắc một thành viên đang hoạt động thuộc phòng ban có thể bấm **Nhận việc** (`accept`) để chiếm quyền sở hữu nguyên tử (`UserId = actorId`). Tuy nhiên, nếu Task đang nằm ở hàng đợi phòng ban (`DepartmentId != null, UserId == null`), chưa rõ một nhân viên bất kỳ trong phòng ban có được quyền bấm **Từ chối nhận việc** (`reject`, đẩy Task sang `REJECTED` đối với toàn bộ phòng ban) hay không.
- **Các phương án:**
  - **Phương án A:** Chỉ cho phép từ chối nhận việc khi Task đã được giao đích danh cho cá nhân (`UserId == actorId`), hoặc do Quản lý của phòng ban đó (`MANAGER` có phạm vi quản lý phòng ban) đại diện từ chối. Nhân viên thường trong phòng ban không thể đơn phương từ chối thay cho cả phòng ban.
  - **Phương án B:** Cho phép bất kỳ nhân viên đang hoạt động nào trong phòng ban từ chối Task chưa có người nhận, đồng thời ghi nhận người từ chối vào `TaskAssignment`.
- **Tác động:** Phương án B tiềm ẩn rủi ro một nhân viên từ chối nhầm làm mất việc của cả phòng ban; Phương án A bảo vệ hàng đợi chung của phòng ban tốt hơn.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** (chỉ người được giao đích danh `UserId == actorId` hoặc `MANAGER` quản lý phòng ban mới được từ chối Task cấp phòng ban).

---

### `OD-002`: Trạng thái hợp lệ để cập nhật tiến độ và gửi báo cáo tiến độ Task
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L19-L36), `FR-TASK-005`, `FR-TASK-006`, `ADR-0003`.
- **Yêu cầu bị ảnh hưởng:** `FR-TASK-005`, `FR-TASK-006`, `DR-007`, `TST-011`.
- **Mô tả vấn đề:**
  1. `FR-TASK-005` và `FR-TASK-006` có cho phép cập nhật % tiến độ / gửi báo cáo khi Task đang ở trạng thái `OVERDUE` không, hay bắt buộc phải chuyển `OVERDUE -> IN_PROGRESS` trước như quy tắc nộp kết quả trong `ADR-0003`?
  2. Endpoint `POST /api/v1/tasks/{id}/progress-reports` (`FR-TASK-006`) có nhận kèm tỷ lệ `progressPercent` mới hay giữ nguyên `ProgressPercent` hiện tại của `Task` nếu người dùng chỉ gửi nội dung văn bản `ReportText`?
- **Các phương án:**
  - **Phương án A:** Chỉ cho phép cập nhật tiến độ và gửi báo cáo khi `Task.Status == IN_PROGRESS` (nhất quán chặt chẽ với `ADR-0003`); `POST /api/v1/tasks/{id}/progress-reports` cho phép truyền `progressPercent` tùy chọn (nếu không truyền thì lấy `Task.ProgressPercent` hiện tại).
  - **Phương án B:** Cho phép gửi báo cáo ở cả `IN_PROGRESS` và `OVERDUE`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để đồng nhất với máy trạng thái §9.1 và `ADR-0003`.

---

### `OD-003`: Tập trạng thái hoạt động của Request cho phép Hủy/Quá hạn và Mã quyền RBAC chi tiết
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L38-L55), [request-lifecycle-and-routing.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/request-lifecycle-and-routing.md), `FR-REQ-002`, `FR-REQ-005`.
- **Yêu cầu bị ảnh hưởng:** `FR-REQ-002`, `FR-REQ-005`, `SEC-003`, `TST-014`.
- **Mô tả vấn đề:**
  1. Trong sơ đồ §9.2, ghi chú `CANCELLED (from active states before CLOSED)` và `OVERDUE (from active processing states)` chưa liệt kê danh sách trạng thái nguồn tường minh. Hiện tại mã nguồn đang cho phép hủy từ `SUBMITTED, ROUTED, RECEIVED, WAITING_FOR_INFO, IN_PROGRESS`. Chưa rõ có cho phép hủy từ `WAITING_APPROVAL` và `OVERDUE` hay không.
  2. Appendix B chỉ có quyền `requests.create`, `requests.route`, `requests.process`, `requests.approve`. Chưa rõ có tách riêng các mã quyền `requests.reject`, `requests.cancel`, `requests.information` hay dùng chung `requests.process` (kèm quyền hủy yêu cầu của chính người gửi).
- **Các phương án:**
  - **Phương án A:** Cho phép hủy (`CANCELLED`) từ `{SUBMITTED, ROUTED, RECEIVED, WAITING_FOR_INFO, WAITING_APPROVAL, IN_PROGRESS, OVERDUE}` (tức mọi trạng thái đang mở trước `RESOLVED/CONFIRMED/CLOSED`); cho phép chuyển sang `OVERDUE` từ `{RECEIVED, IN_PROGRESS}`; giữ bộ quyền chuẩn Appendix B (`requests.process` cho người xử lý/quản lý, cộng quyền chủ sở hữu `RequesterId` tự hủy yêu cầu của mình khi chưa `RESOLVED`).
  - **Phương án B:** Tách thêm 3 mã quyền mới `requests.reject`, `requests.cancel`, `requests.information` trong bảng `Permission`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để tránh làm phình danh mục quyền Appendix B và đảm bảo yêu cầu đang chờ duyệt hoặc quá hạn vẫn có thể được hủy hợp lệ khi không còn nhu cầu.

---

### `OD-004`: Cho phép `User.Email` nhận giá trị `NULL` đối với nhân viên không có Email
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `CONF-07`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L57-L60), `FR-AUTH-004`, `BR-001` vs. SSS Appendix C.
- **Yêu cầu bị ảnh hưởng:** `FR-AUTH-001`, `FR-AUTH-004`, `FR-ORG-001`, `DR-003`.
- **Mô tả vấn đề:** `FR-AUTH-001` và `FR-AUTH-004` quy định nhân viên có thể đăng nhập bằng `EmployeeCode` và nếu không có email thì được `COMPANY_ADMIN` cấp mật khẩu tạm thời. Tuy nhiên, bảng `User` trong Appendix C và migration hiện tại đặt `Email VARCHAR(255) NOT NULL`.
- **Các phương án:**
  - **Phương án A:** Chuyển `User.Email` thành `NULLABLE` đối với người dùng thuộc Tenant (`TenantId IS NOT NULL`), áp dụng chỉ mục duy nhất có điều kiện `UNIQUE (TenantId, Email) WHERE Email IS NOT NULL`; tài khoản `PLATFORM_ADMIN` vẫn bắt buộc có `Email NOT NULL`.
  - **Phương án B:** Giữ `User.Email NOT NULL` và bắt buộc mọi nhân viên phải có email (hoặc sinh email nội bộ giả lập).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để tuân thủ đúng yêu cầu nghiệp vụ `FR-AUTH-004` và `BR-001`.

---

### `OD-005`: Biểu diễn trạng thái Phê duyệt Đăng ký Doanh nghiệp (`Company.Status`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `CONF-08`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L61-L65), `FR-TEN-002` vs. SSS Appendix C.
- **Yêu cầu bị ảnh hưởng:** `FR-TEN-002`, `DR-002`, `TST-003`.
- **Mô tả vấn đề:** `FR-TEN-002` mô tả khi `PLATFORM_ADMIN` chấp thuận đăng ký công ty, hệ thống cập nhật trạng thái thành "Approved" và khởi tạo Tenant mặc định. Nhưng Appendix C định nghĩa `Company.Status` chỉ gồm `{PENDING, ACTIVE, SUSPENDED, REJECTED}`.
- **Các phương án:**
  - **Phương án A:** Thực hiện phê duyệt và khởi tạo Tenant mặc định trong **cùng một giao dịch cơ sở dữ liệu nguyên tử**: chuyển thẳng `Company.Status` từ `PENDING -> ACTIVE`, kích hoạt `Tenant.Status = ACTIVE`, và ghi nhận sự kiện kiểm toán `COMPANY.APPROVED` + `TENANT.PROVISIONED` trong `AuditLog`.
  - **Phương án B:** Mở rộng enum `Company.Status` thêm giá trị `APPROVED` cho giai đoạn trung gian trước khi khởi tạo bất đồng bộ.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** vì quy trình khởi tạo dữ liệu hạt giống cho 1 Tenant rất nhẹ, thực hiện đồng bộ trong một giao dịch giúp tránh trạng thái treo nửa vời.

---

### `OD-006`: Ràng buộc tính duy nhất của Tên Phòng ban (`Department.Name`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `CONF-09`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L66-L69), `FR-ORG-004` vs. SSS Appendix C.
- **Yêu cầu bị ảnh hưởng:** `FR-ORG-004`, `DR-003`.
- **Mô tả vấn đề:** `FR-ORG-004` nêu tên và mã phòng ban phải duy nhất trong công ty, trong khi Appendix C chỉ đặt unique index trên `(TenantId, Code)`.
- **Các phương án:**
  - **Phương án A:** Đặt ràng buộc duy nhất không phân biệt hoa thường trên cả `(TenantId, Code)` và `(TenantId, Name)` đối với các phòng ban đang `ACTIVE`.
  - **Phương án B:** Chỉ bắt buộc duy nhất trên `(TenantId, Code)` để cho phép các phòng ban con ở các nhánh khác nhau có thể trùng tên hiển thị (ví dụ: "Tổ Hành chính" thuộc hai chi nhánh khác nhau).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** (hoặc duy nhất theo `(TenantId, ParentDepartmentId, Name)`) để khớp với câu chữ của `FR-ORG-004`.

---

### `OD-007`: Hợp đồng API cho luồng Admin cấp mật khẩu tạm và buộc đổi mật khẩu lần đầu
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-12`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L70-L72), `FR-AUTH-004`.
- **Yêu cầu bị ảnh hưởng:** `FR-AUTH-004`, `IR-API-001`, `SEC-001`.
- **Mô tả vấn đề:** Appendix C và D chưa định nghĩa cột cờ bắt buộc đổi mật khẩu cũng như endpoint để Admin cấp mật khẩu tạm thời và người dùng đổi mật khẩu bắt buộc.
- **Các phương án:**
  - **Phương án A:** Thêm cột `MustChangePassword BOOLEAN NOT NULL DEFAULT FALSE` vào bảng `User`; bổ sung `POST /api/v1/users/{id}/temporary-password` (quyền `users.manage`) và `POST /api/v1/auth/change-password`; khi `MustChangePassword == true`, phiên đăng nhập chỉ được phép gọi `/api/v1/auth/change-password` hoặc `/api/v1/auth/logout`, mọi API nghiệp vụ khác trả về `403 AUTH.PASSWORD_CHANGE_REQUIRED`.
  - **Phương án B:** Chỉ cho phép Admin đặt lại mật khẩu mà không cưỡng chế đổi mật khẩu ở tầng middleware.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để đảm bảo an toàn bảo mật theo đúng `FR-AUTH-004`.

---

### `OD-008`: Hợp đồng cập nhật phiên bản nháp Workflow và ánh xạ Bước quy trình sang Trạng thái thực thi
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-06`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L73-L75), `FR-WF-001`, `FR-WF-002`.
- **Yêu cầu bị ảnh hưởng:** `FR-WF-001`, `FR-WF-002`, `IR-API-001`, `DR-009`.
- **Mô tả vấn đề:**
  1. Hiện đã có API tạo phiên bản nháp rỗng (`POST /api/v1/workflows/{id}/versions`), nhưng Appendix D chưa mô tả rõ payload của `PUT /api/v1/workflow-versions/{id}` để lưu đồ thị các bước (`WorkflowStep`) và chuyển tiếp (`WorkflowTransition`) trước khi phát hành (`Publish`).
  2. Chưa rõ khi một Task/Request chạy theo `WorkflowVersion`, các bước cấu hình sẽ điều khiển chuyển trạng thái chuẩn (§9.1/§9.2) như thế nào.
- **Các phương án:**
  - **Phương án A:** `PUT /api/v1/workflow-versions/{id}` thay thế toàn bộ danh sách `steps` và `transitions` của phiên bản `DRAFT` (kèm kiểm tra `If-Match`); mỗi `WorkflowStep` ánh xạ tới một loại bước chuẩn (`INTAKE`, `EXECUTION`, `APPROVAL`, `CONFIRMATION`, `COMPLETION`) tương ứng với các trạng thái hợp lệ trong §9.1/§9.2.
  - **Phương án B:** Cho phép người dùng tự định nghĩa tên trạng thái tùy ý ngoài danh mục trạng thái §9.1/§9.2.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để bảo toàn tính toàn vẹn của máy trạng thái chuẩn §9.1/§9.2 và các báo cáo hệ thống.

---

### `OD-009`: Hỗ trợ Thông báo trong ứng dụng cho Quản trị viên Nền tảng (`PLATFORM_ADMIN`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-13`, [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L76-L79), `FR-TEN-001`.
- **Yêu cầu bị ảnh hưởng:** `FR-TEN-001`, `IR-NOTIF-001`, `DR-011`.
- **Mô tả vấn đề:** Khi có doanh nghiệp mới gửi đơn đăng ký (`FR-TEN-001`), hệ thống cần thông báo cho `PLATFORM_ADMIN`. Tuy nhiên, bảng `Notification` hiện có `TenantId UUID NOT NULL`, trong khi tài khoản `PLATFORM_ADMIN` có `TenantId = NULL` (`ADR-0002`).
- **Các phương án:**
  - **Phương án A:** Cho phép `Notification.TenantId` nhận giá trị `NULL` đối với các thông báo cấp nền tảng gửi cho `PLATFORM_ADMIN` (`Recipient.IsPlatformAdministrator == true`).
  - **Phương án B:** Không lưu bản ghi `Notification` cho `PLATFORM_ADMIN` mà chỉ hiển thị số lượng đơn `PENDING` trực tiếp trên hàng đợi `GET /api/v1/platform/company-registrations` và gửi email.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để hộp thư thông báo hoạt động thống nhất cho cả hai mặt phẳng Platform và Tenant.

---

### `OD-010`: Cơ chế chọn và quản trị Lịch làm việc mặc định của Tenant (`BusinessCalendar`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** [pending-owner-decisions.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/pending-owner-decisions.md#L80-L83), `FR-SLA-001`, `ADR-0004`, `ADR-0005`.
- **Yêu cầu bị ảnh hưởng:** `FR-SLA-001`, `FR-SLA-002`, `DR-010`.
- **Mô tả vấn đề:** Một Tenant có thể có nhiều bản ghi `BusinessCalendar` (theo `ADR-0004`, mỗi lần sửa lịch làm việc sẽ tạo bản ghi bất biến mới). Cần xác định cách hệ thống biết lịch làm việc nào đang là lịch hiện hành và cách `SLAVersion` gắn kết với lịch đó.
- **Các phương án:**
  - **Phương án A:** Lưu `BusinessCalendarId` hiện hành trong `TenantSetting` với khóa `"sla.default_calendar_id"`, đồng thời khi tạo `SLAVersion` mới sẽ chụp cứng (`BusinessCalendarId`) vào `SLAVersion` để đảm bảo tính bất biến theo `ADR-0004`.
  - **Phương án B:** Thêm cột `IsDefault BOOLEAN` trên bảng `BusinessCalendar`.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** (kết hợp khóa `"sla.default_calendar_id"` đã có sẵn trong migration seed và khóa ngoại bất biến trên `SLAVersion`).

---

### `OD-011`: Quy tắc hội tụ khi Bước phê duyệt có nhiều Người phê duyệt (Multi-Approver Quorum Rule)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-05` trong [05-assumptions-and-ambiguities.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/00-analysis/05-assumptions-and-ambiguities.md), `FR-WF-003`, `FR-WF-004`.
- **Yêu cầu bị ảnh hưởng:** `FR-WF-003`, `FR-WF-004`, `UC-009`, `TST-017`.
- **Mô tả vấn đề:** Một bước phê duyệt có thể chỉ định một hoặc nhiều người duyệt (`ApprovalStepInstance`). Tài liệu gốc chưa nêu rõ nếu một bước có nhiều người duyệt thì cần tất cả cùng đồng ý hay chỉ cần một người đầu tiên đồng ý.
- **Các phương án:**
  - **Phương án A:** Tại một bước phê duyệt đơn lẻ (`stepOrder`), nếu chỉ định nhiều người duyệt thì mặc định áp dụng quy tắc **Tất cả phải đồng ý (Unanimous / ALL)** để hoàn thành bước, và **Bất kỳ ai từ chối (First Rejection)** sẽ làm bước phê duyệt bị từ chối ngay lập tức; các bước khác nhau (`stepOrder = 1, 2, ...`) chạy tuần tự.
  - **Phương án B:** Chỉ cần người đầu tiên bấm duyệt (`First-Responder / ANY`) là hoàn tất bước.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** (hoặc cho phép cấu hình `approvalMode: "ALL" | "ANY"` trong JSON quy tắc phê duyệt với mặc định là `"ALL"`).

---

### `OD-012`: Điều kiện Tạm dừng và Tiếp tục đồng hồ SLA giữa Request và các Task liên kết
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `A-09` trong [05-assumptions-and-ambiguities.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/00-analysis/05-assumptions-and-ambiguities.md), `FR-SLA-004`, `BR-016`.
- **Yêu cầu bị ảnh hưởng:** `FR-SLA-002`, `FR-SLA-004`, `BR-016`, `UC-010`.
- **Mô tả vấn đề:** `FR-SLA-004` quy định đồng hồ SLA được tạm dừng khi chờ phản hồi từ người gửi (`WAITING_FOR_INFO`) hoặc chờ phê duyệt (`WAITING_APPROVAL`). Chưa rõ khi `Request` cha tạm dừng SLA thì các `Task` con liên kết có tự động tạm dừng SLA theo hay mỗi đối tượng (`Request`, `Task`) có đồng hồ `SLAState` độc lập.
- **Các phương án:**
  - **Phương án A:** Mỗi `Request` và mỗi `Task` quản lý một bản ghi `SLAState` độc lập theo trạng thái của chính nó (`Request` tạm dừng khi `Request.Status IN (WAITING_FOR_INFO, WAITING_APPROVAL)`; `Task` có SLA riêng tính theo vòng đời của `Task`).
  - **Phương án B:** Tạm dừng dây chuyền toàn bộ `Task` con khi `Request` cha tạm dừng.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** vì các công việc nội bộ khác của cùng Request vẫn có thể đang được thực thi bình thường trong lúc chờ phê duyệt một hạng mục.

---

### `OD-013`: Định nghĩa Tải chuẩn (Workload Profile) để nghiệm thu Hiệu năng API và Báo cáo (`NFR-PERF-001`, `NFR-PERF-002`, `NFR-SCAL-001`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `B-11` trong [05-assumptions-and-ambiguities.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/00-analysis/05-assumptions-and-ambiguities.md), `NFR-001`, `NFR-002`, `NFR-003`.
- **Yêu cầu bị ảnh hưởng:** `NFR-PERF-001`, `NFR-PERF-002`, `NFR-SCAL-001`, `TST-025`.
- **Mô tả vấn đề:** SSS v1.2 nêu mục tiêu độ trễ API thông thường `<= 500 ms`, báo cáo/dashboard `<= 2 giây` và hỗ trợ `100 người dùng đồng thời`, nhưng chưa định nghĩa phân vị đo (`p95` hay `p99`), quy mô dữ liệu thử nghiệm (số lượng bản ghi/tenant) và cấu hình phần cứng chuẩn.
- **Các phương án:**
  - **Phương án A (Đề xuất cho nghiệm thu MVP):** Đo tại phân vị **`p95`** (và `p99 <= 2x p95`) trên môi trường container chuẩn (2 vCPU, 4 GB RAM cho API; 2 vCPU, 4 GB RAM cho PostgreSQL 18), với dữ liệu hạt giống tối thiểu `10 Tenants`, `10,000 Tasks`, `10,000 Requests`, ở mức tải `100 virtual users` đồng thời (80% đọc, 20% ghi).
  - **Phương án B:** Đo theo giá trị trung bình (Average latency) không phân vị.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để phép thử hiệu năng có tính lặp lại và đo lường khách quan.

---

### `OD-014`: Ngưỡng Timeout, Retry, Giới hạn Tốc độ (Rate Limit) và Ngân sách Token cho AI (`NFR-AI-001`, `NFR-AI-002`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `NFR-008`, [ai-assisted-operations.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/docs/02-traceability/ai-assisted-operations.md), [OPEN_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/documents/SSS/OPEN_DECISIONS.md).
- **Yêu cầu bị ảnh hưởng:** `FR-AI-001`..`FR-AI-010`, `NFR-AI-001`, `NFR-AI-002`, `SEC-008`, `SEC-009`.
- **Mô tả vấn đề:** Cần xác định ngưỡng thời gian chờ tối đa cho một lệnh gọi LLM bên ngoài, số lần thử lại khi lỗi mạng tạm thời, và giới hạn số lượt gọi AI trên mỗi người dùng/tenant để tránh vượt chi phí OpenAI API.
- **Các phương án:**
  - **Phương án A:** Timeout cứng `15 giây` cho mỗi yêu cầu AI (`p95 <= 8 giây`); tối đa `1 lần retry` cho lỗi mạng/5xx tạm thời; giới hạn tốc độ mặc định `30 yêu cầu AI / phút / người dùng` và `300 yêu cầu AI / giờ / tenant` (có thể cấu hình qua `TenantSetting`); khi vượt ngưỡng hoặc nhà cung cấp AI lỗi, hệ thống trả về thông báo suy giảm an toàn (graceful degradation) và cho phép người dùng thao tác thủ công 100%.
  - **Phương án B:** Không giới hạn tốc độ gọi AI trong MVP.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để bảo vệ ngân sách API và tính ổn định của hệ thống.

---

### `OD-015`: Chính sách Thời gian Lưu trữ Dữ liệu (Retention), Lưu trữ Lạnh và Xuất dữ liệu khi Chấm dứt Tenant
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `FR-COL-003`, `BR-021`, `DR-014`, `NFR-DATA-001`.
- **Yêu cầu bị ảnh hưởng:** `FR-COL-003`, `DR-013`, `DR-014`, `SEC-007`, `SEC-010`.
- **Mô tả vấn đề:** Hệ thống cấm xóa cứng dữ liệu nghiệp vụ và nhật ký kiểm toán (`PERSISTENCE.HARD_DELETE_DENIED`), nhưng chưa có quy định về thời hạn lưu trữ tối thiểu cho `AuditLog`, `Attachment`, `Completed/Closed Records` và quy trình xuất/ẩn danh dữ liệu khi một doanh nghiệp ngừng sử dụng dịch vụ.
- **Các phương án:**
  - **Phương án A:** Giữ trực tuyến toàn bộ hồ sơ nghiệp vụ, tệp đính kèm và `AuditLog` tối thiểu **365 ngày** trong phạm vi MVP; phiên tải lên tệp chưa hoàn tất (`Pending Upload Session`) tự động hết hạn và dọn dẹp sau **24 giờ**; khi Tenant bị `SUSPENDED`, khóa quyền ghi nhưng giữ nguyên dữ liệu để tra cứu kiểm toán.
  - **Phương án B:** Cho phép xóa cứng dữ liệu sau 90 ngày.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** (khớp với cơ chế chặn xóa cứng hiện có trong `BizFlowDbContext`).

---

### `OD-016`: Mục tiêu Khôi phục Thảm họa (RPO / RTO) và Độ sẵn sàng Dịch vụ (`NFR-REL-001`, `NFR-BCDR-001`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `NFR-004`, [OPEN_DECISIONS.md](file:///d:/FA26/AI-Assisted-BizFlow-Platform/documents/SSS/OPEN_DECISIONS.md).
- **Yêu cầu bị ảnh hưởng:** `NFR-REL-001`, `NFR-BCDR-001`.
- **Mô tả vấn đề:** Tài liệu gốc chưa ấn định con số cam kết SLA hạ tầng (Uptime %, RPO, RTO) cho bản triển khai MVP.
- **Các phương án:**
  - **Phương án A:** Mục tiêu vận hành MVP: Độ sẵn sàng `>= 99.5%` trong giờ hành chính; sao lưu tự động PostgreSQL và MinIO hàng ngày với **RPO `<= 24 giờ`** (hoặc `<= 1 giờ` nếu bật WAL archiving) và **RTO `<= 4 giờ`**.
  - **Phương án B:** Cam kết High Availability đa vùng `99.99%` với RPO = 0.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để phù hợp với kiến trúc Modular Monolith và chi phí hạ tầng giai đoạn MVP.

---

### `OD-017`: Phạm vi Ngôn ngữ Giao diện (i18n) và Định dạng Múi giờ trong MVP (`NFR-I18N-001`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Khảo sát giao diện Frontend (`frontend/src/app`) và tài liệu đặc tả.
- **Yêu cầu bị ảnh hưởng:** `NFR-I18N-001`, `IR-UI-001`, `DR-015`.
- **Mô tả vấn đề:** Mã nguồn giao diện hiện tại sử dụng nhãn tiếng Anh chuẩn hóa, trong khi người dùng doanh nghiệp tại Việt Nam có thể nhập dữ liệu tiếng Việt UTF-8 và làm việc theo múi giờ địa phương (`Asia/Ho_Chi_Minh`).
- **Các phương án:**
  - **Phương án A:** Toàn bộ dữ liệu văn bản hỗ trợ đầy đủ Unicode UTF-8 (tiếng Việt và tiếng Anh); mọi mốc thời gian lưu trữ và truyền qua API ở chuẩn UTC ISO-8601 (`DR-015`), hiển thị theo múi giờ cấu hình của Tenant/trình duyệt; giao diện MVP mặc định tiếng Anh kỹ thuật (sẵn sàng mở rộng đa ngôn ngữ vi/en ở phiên bản tiếp theo).
  - **Phương án B:** Bắt buộc bộ chuyển đổi đa ngôn ngữ đầy đủ trên toàn bộ 16 màn hình ngay trong MVP.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** để khớp với hiện trạng triển khai 196 bài kiểm thử giao diện hiện có.

---

### `OD-018`: Lộ trình Nâng cấp Đa Không gian làm việc cho Một Doanh nghiệp (Future Enterprise Multi-Workspace Architecture)
- **Trạng thái:** `OPEN` (Dành cho giai đoạn Future Enterprise — **Ngoài phạm vi MVP**)
- **Nguồn phát sinh:** `DEC-WS-001`, Định hướng sản phẩm dài hạn.
- **Yêu cầu bị ảnh hưởng:** `FR-TEN-002`, `FR-TEN-004`, `DR-001`, `DR-002`.
- **Mô tả vấn đề:** Trong MVP, mỗi `Company` được gắn với 1 `Tenant` (Workspace chính) duy nhất. Khi nâng cấp lên gói Enterprise trong tương lai để cho phép 1 `Company` sở hữu nhiều `Workspace` độc lập (ví dụ theo công ty con hoặc chi nhánh độc lập), cần xác định ranh giới chia sẻ danh tính người dùng (`UserAccount`).
- **Các phương án:**
  - **Phương án A (Giữ nguyên thiết kế hiện tại cho MVP, mở rộng ở Enterprise):** Giữ cột khóa ngoại `Tenant.CompanyId -> Company.Id` (vốn đã là quan hệ `1 Company : N Tenants` trong lược đồ cơ sở dữ liệu hiện tại!) nhưng trong MVP chỉ khởi tạo 1 `Tenant` mặc định cho mỗi `Company`. Ở giai đoạn Enterprise, bổ sung màn hình chuyển đổi Workspace (Workspace Switcher) và bảng ánh xạ thành viên đa workspace mà không phải phá vỡ cấu trúc `TenantId` của 36+ bảng nghiệp vụ.
  - **Phương án B:** Đưa tính năng quản lý đa workspace vào ngay MVP.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A** — tuân thủ nghiêm ngặt quyết định `DEC-WS-001` (MVP = 1 workspace chính/doanh nghiệp), đồng thời tận dụng lợi thế kiến trúc hiện tại là bảng `Tenant` đã tách riêng khỏi bảng `Company` qua khóa ngoại `CompanyId`.

---

### `OD-019`: Số liệu Đo lường Cơ sở (Baseline Metrics) và Chỉ tiêu Định lượng Thành công Nghiệp vụ
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `BG-001`..`BG-008`, `RDS_v1.0_DRAFT.md` §3, `SRS_v1.0_DRAFT.md` §3.
- **Yêu cầu bị ảnh hưởng:** `BG-001`..`BG-008`, `STKN-001`..`STKN-014`.
- **Mô tả vấn đề:** Các tài liệu gốc xác định rõ 8 mục tiêu định tính (`BG-001`..`BG-008`), nhưng chưa có số liệu đo lường hiện trạng trước khi áp dụng hệ thống (AS-IS Baseline) và tỷ lệ phần trăm cải thiện mục tiêu được phê duyệt chính thức (Target KPIs).
- **Các phương án:**
  - **Phương án A:** Ghi nhận các số liệu cơ sở là `TBD (Cần khảo sát tại doanh nghiệp thí điểm)` và thiết lập bộ chỉ số chuẩn hệ thống tự động thu thập (Thời gian định tuyến trung vị, Tỷ lệ hoàn thành đúng hạn SLA, Tỷ lệ hồ sơ trả lại do thiếu thông tin, Tỷ lệ chấp nhận gợi ý AI) kèm ngưỡng đề xuất `PROPOSED — NOT APPROVED`.
  - **Phương án B:** Tự đặt các con số KPI giả định.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-020`: Xử lý các Yêu cầu Đang Mở khi Ngừng Kích hoạt Dịch vụ Nội bộ (`Service Deactivation Impact`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** `FR-SVC-001`, `FR-REQ-001`, `BR-013`, `BR-014`.
- **Yêu cầu bị ảnh hưởng:** `FR-SVC-001`, `FR-REQ-001`, `UC-004`, `UC-006`.
- **Mô tả vấn đề:** Khi `COMPANY_ADMIN` chuyển trạng thái một Dịch vụ (`Service`) sang `INACTIVE`, hệ thống chặn tạo yêu cầu mới. Cần xác định rõ hành vi đối với các yêu cầu đã gửi trước đó và đang trong quá trình xử lý.
- **Các phương án:**
  - **Phương án A:** Chỉ chặn tạo mới (`DRAFT` / `SUBMITTED` mới) đối với dịch vụ `INACTIVE`; toàn bộ yêu cầu đang mở được phép tiếp tục chạy hết vòng đời theo phiên bản quy trình và SLA đã ghim lúc tạo (`BR-013`, `BR-014`, `ADR-0004`).
  - **Phương án B:** Tự động hủy toàn bộ yêu cầu đang mở khi dịch vụ ngừng kích hoạt.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-021`: Mô hình Tiếp nhận Doanh nghiệp (Admin-Approved vs. Self-Service Onboarding) và Điểm vào Đăng ký Công khai (`Landing / Registration Entry Point`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát Onboarding & Workspace Provisioning; đối chiếu `SRC-002` (`FR-TEN-001`, `FR-TEN-002`), `SRC-004` (`UI-01`, `UI-02`), `DEC-WS-001`, `frontend/src/app/app.routes.ts`, `frontend/src/app/pages/login/login.html` và `backend/BizFlow.Api/Controllers/CompanyRegistrationsController.cs`.
- **Yêu cầu bị ảnh hưởng:** `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `IR-UI-001`, `IR-UI-004`, `BR-023`, `BR-024`, `UC-001`, `UC-013`, `TST-004`, `TST-031`.
- **Mô tả vấn đề:**
  1. **Sự khác biệt giữa Baseline Đã Xác nhận (`CONFIRMED`) và Phương án Đề xuất Multi-Tenant SaaS (`PROPOSED — NOT APPROVED`):** Tài liệu gốc (`SRC-002` `FR-TEN-001`..`002`) xác nhận mô hình có cổng phê duyệt bởi `PLATFORM_ADMIN` (`PENDING -> UI-02 Approve -> ACTIVE Tenant`). Tuy nhiên, theo định hướng Multi-Tenant SaaS cho MVP, phương án **Tự đăng ký doanh nghiệp (`Self-Service Company Registration` — `FR-TEN-005`)** — nơi đại diện đăng ký tài khoản + thông tin công ty, xác minh email và được tự động kích hoạt **01 Primary Tenant / Workspace (`DEC-WS-001`)** kèm quyền `COMPANY_ADMIN` — là phương án đề xuất (`PROPOSED — NOT APPROVED`) giúp loại bỏ điểm nghẽn chờ duyệt thủ công.
  2. **Khoảng trống Điểm vào Đăng ký (`Landing / Registration Entry Point`) trong thực tế triển khai:** `frontend/src/app/app.routes.ts` chuyển hướng root `''` về `/login`; màn hình `/login` chỉ có dòng chữ tĩnh *"Need access? Contact your company administrator."* mà chưa có tuyến đường `/register-company` hay liên kết CTA công khai.
- **Các phương án:**
  - **Phương án A (Giữ nguyên Baseline Đã Xác nhận — Admin-Approved Onboarding):** Bổ sung tuyến đường công khai `/register-company` và liên kết CTA từ `/login` (`UI-01`); đơn đăng ký sau khi gửi/xác minh email dừng ở trạng thái `PENDING` chờ `PLATFORM_ADMIN` phê duyệt tại `/platform/companies` (`UI-02`).
  - **Phương án B (Phương án Đề xuất cho MVP — Self-Service Onboarding có Cờ Cấu hình Linh hoạt — `PROPOSED — NOT APPROVED`):** Bổ sung điểm vào `/register-company` (CTA từ `/login`); đại diện doanh nghiệp đăng ký thông tin công ty và tài khoản quản trị viên đầu tiên. Ngay sau khi hoàn tất **Xác minh Email (`Email Verification`)**, hệ thống tự động kích hoạt `Company (ACTIVE)`, khởi tạo **01 Không gian làm việc chính (`Primary Tenant`, `DEC-WS-001`)** và gán vai trò `COMPANY_ADMIN` (hỗ trợ cờ cấu hình `Onboarding:ApprovalMode = "SelfService" | "PlatformAdminApproval"`).
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án B**, nhưng **không tự ý ghi đè** yêu cầu `CONFIRMED` hiện hành (`FR-TEN-001`..`002`) cho đến khi có phê duyệt chính thức của Product Owner.

---

### `OD-022`: Chính sách Xác minh Email (`Email Verification Policy`), Thời hạn Mã Xác minh và Xử lý Tài khoản / Doanh nghiệp Chưa Xác minh Email
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát luồng đăng ký tài khoản, tạo doanh nghiệp và xác minh email; đối chiếu `Company.cs`, `UserAccount.cs` và `PasswordResetService.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-024`, `FR-TEN-001`, `FR-TEN-005`, `IR-EXT-002`, `IR-UI-004`, `DR-002`, `DR-003`, `UC-001`, `UC-013`, `TST-004`, `TST-031`.
- **Mô tả vấn đề:** Trong mã nguồn hiện tại, thực thể `Company` và `UserAccount` chưa có trường `EmailVerified` / `EmailVerifiedAt` hay cơ chế lưu token xác minh email đăng ký. Cần xác định:
  1. Khi chưa xác minh email, hồ sơ công ty và tài khoản người đại diện ở trạng thái nào?
  2. Người đại diện có được phép đăng nhập khi email chưa xác minh không?
  3. Thời hạn hiệu lực của mã xác minh và giới hạn gửi lại mã (`Resend Verification`).
- **Các phương án:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Bắt buộc xác minh địa chỉ email người đại diện qua token một lần (băm SHA-256) có thời hạn hiệu lực **`24 giờ`**.
    2. Khi `EmailVerified = false`, đơn đăng ký ở trạng thái chờ xác minh, **chưa kích hoạt Primary Tenant** và **chặn đăng nhập** với mã lỗi `403 AUTH.EMAIL_NOT_VERIFIED`, kèm tùy chọn gửi lại email xác minh (rate limit tối đa `3 lần / 15 phút`).
    3. Với mô hình Admin-Approved, chỉ đơn đã xác minh email mới được phép `Approve` trên `UI-02`.
  - **Phương án B:** Không thực hiện xác minh email tự động trong MVP.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-023`: Chính sách Xử lý Email Trùng, Mã Doanh nghiệp / `TenantKey` Trùng và Quy tắc Người dùng Đăng ký Lại (`Duplicate & Re-registration Governance`)
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát các trường hợp biên khi đăng ký: trùng email, trùng code/tenant key, đăng ký lại sau khi thất bại/từ chối; đối chiếu `Company.cs` (enum `CompanyStatus` thiếu `Rejected`) và `IdentityModelConfiguration.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-024`, `FR-TEN-001`, `FR-TEN-002`, `FR-TEN-005`, `DR-002`, `UC-001`, `UC-013`, `TST-004`, `TST-031`.
- **Mô tả vấn đề:** Cần xác định rõ hành vi khi biểu mẫu đăng ký mới có thông tin trùng với dữ liệu đã có:
  1. Trùng với doanh nghiệp/tài khoản đang hoạt động (`ACTIVE`) hoặc tạm ngưng (`SUSPENDED`).
  2. Trùng với hồ sơ đăng ký đang chờ xác minh email (`UNVERIFIED`) hoặc chờ duyệt (`PENDING`).
  3. Trùng với hồ sơ đã bị từ chối (`REJECTED`) hoặc hết hạn xác minh (`EXPIRED`) — trường hợp **người dùng đăng ký lại (`Re-registration`)**.
  4. Enum `CompanyStatus` trong mã nguồn hiện chỉ có `{ Pending, Active, Suspended, Inactive }`, thiếu giá trị `Rejected` (`FR-TEN-002`).
- **Các phương án:**
  - **Phương án A (`PROPOSED — NOT APPROVED`):**
    1. Trùng với hồ sơ `ACTIVE` / `SUSPENDED` / `PENDING` (đã xác minh): Trả về `409 Conflict` (`COMPANY.DUPLICATE_CODE_OR_KEY`, `IDENTITY.DUPLICATE_EMAIL`).
    2. Trùng với hồ sơ `UNVERIFIED` còn hạn: Không tạo bản ghi trùng mới; trả thông báo hướng dẫn kiểm tra email hoặc bấm gửi lại mã xác minh.
    3. Đăng ký lại sau khi bị `REJECTED` hoặc `EXPIRED`: Cho phép người dùng đăng ký lại bằng cùng email/mã công ty sau khi sửa sai; bảo toàn lịch sử từ chối trong `AuditLog` (`BR-021`); bổ sung giá trị `Rejected` vào enum `CompanyStatus`.
  - **Phương án B:** Dùng ràng buộc `UNIQUE` cứng không điều kiện trên bảng `Company` và chặn vĩnh viễn việc đăng ký lại.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

---

### `OD-024`: Tính Nguyên tử Giao dịch (`Transactional Atomicity`), Rollback Khi Khởi tạo Tenant / Workspace Không Hoàn tất và Xử lý Khi Cấp Quyền Owner / Admin Ban đầu Thất bại
- **Trạng thái:** `OPEN`
- **Nguồn phát sinh:** Rà soát rủi ro tạo tenant/workspace không hoàn tất và cấp quyền owner/admin ban đầu không thành công; đối chiếu `IdentityModelConfiguration.cs` (`Tenant.CompanyId` unique index, `CK_Role_Scope`) và `BizFlowDbContext.cs`.
- **Yêu cầu bị ảnh hưởng:** `BR-023`, `FR-TEN-002`, `FR-TEN-005`, `DR-002`, `DR-004`, `UC-001`, `UC-013`, `TST-004`, `TST-031`.
- **Mô tả vấn đề:** Chuỗi 5 tác vụ provisioning liên hoàn:
  1. Chuyển `Company` sang `ACTIVE`.
  2. Tạo đúng **01 Không gian làm việc chính (`Primary Tenant / Workspace`)** gắn với `CompanyId` (`Unique(CompanyId)` theo `DEC-WS-001`) và chuyển sang `ACTIVE`.
  3. Khởi tạo cấu hình hạt giống (`TenantSettings`, `BusinessCalendar`).
  4. Tạo tài khoản người đại diện (`UserAccount` gắn `TenantId`, `Status = Active`).
  5. Gán vai trò `COMPANY_ADMIN` (`UserRole` liên kết vai trò hệ thống `IsSystem = true`) và ghi `AuditLog`.
  Nếu bước (1)-(2) thành công nhưng bước (3), (4) hoặc bước (5) **cấp quyền `COMPANY_ADMIN` thất bại**, hệ thống rơi vào trạng thái "Workspace không người quản trị" (Adminless Workspace) hoặc kẹt ở `TenantStatus = Provisioning`.
- **Các phương án:**
  - **Phương án A (`PROPOSED — NOT APPROVED` — Giao dịch Nguyên tử Tất-cả-hoặc-Không-gì `All-or-Nothing Atomic Unit of Work`):**
    1. Thực thi toàn bộ 5 bước trong **một giao dịch cơ sở dữ liệu ACID duy nhất** có khóa chống trùng lặp (`Idempotency-Key`).
    2. **Kiểm tra điều kiện bất biến trước khi Commit (`Post-Provisioning Invariant Check`):** Xác minh tồn tại đúng `01 Primary Tenant (ACTIVE)` cho `Company` và tồn tại ít nhất `01 UserAccount (ACTIVE)` thuộc `Tenant` đó được gán vai trò `COMPANY_ADMIN`. Nếu bất kỳ bước nào thất bại, **Rollback 100% toàn bộ giao dịch** về trạng thái trước khi khởi tạo (giữ nguyên đơn `PENDING`), ghi nhật ký lỗi kèm `CorrelationId` và cho phép thử lại (`Idempotent Retry`) an toàn.
    3. Tách gửi email thông báo kích hoạt ra sau bước Commit DB (qua Outbox/hàng đợi) để lỗi SMTP không làm rollback giao dịch hợp lệ.
  - **Phương án B:** Khởi tạo bất đồng bộ từng phần không dùng chung giao dịch ACID, giữ `Tenant.Status = Provisioning` khi lỗi và yêu cầu `PLATFORM_ADMIN` can thiệp thủ công.
- **Khuyến nghị (`PROPOSED — NOT APPROVED`):** Chọn **Phương án A**.

