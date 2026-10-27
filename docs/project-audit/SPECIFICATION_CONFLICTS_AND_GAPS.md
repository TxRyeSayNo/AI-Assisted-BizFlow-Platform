# Báo Cáo Xung Đột Đặc Tả, Khoảng Trống Và Quyết Định Chưa Giải Quyết
## (Specification Conflicts, Gaps and Unresolved Decisions Report)
### AI-Assisted BizFlow Platform — Phiên Bản Đối Soát v1.2-FINAL

- **Mã tài liệu:** `BIZFLOW-AUDIT-GAPS-v1.2`
- **Phiên bản:** `1.2-FINAL`
- **Ngày lập:** `2026-10-21`
- **Tổ kiểm toán kỹ thuật:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Senior BA, Senior Requirements Engineer, Senior Software Architect, QA Lead, Technical Documentation Reviewer)
- **Mục đích:** Ghi nhận minh bạch mọi điểm xung đột giữa các tài liệu đặc tả (RDS $\leftrightarrow$ SRS $\leftrightarrow$ SSS), khoảng trống giữa đặc tả và mã nguồn thực tế, các yêu cầu mơ hồ hoặc thiếu tiêu chí nghiệm thu định lượng, và danh mục các quyết định cần sự tham gia của Chủ sở hữu Sản phẩm (Product Owner).

---

## 1. Xung Đột Giữa Các Tài Liệu Đặc Tả (Cross-Specification Conflicts)

### Xung Đột CONF-01: Mô hình Onboarding Tự phục vụ (`Self-Service Onboarding`) so với Ràng buộc Duyệt qua Quản trị viên (`Admin-Approved Baseline`)
- **Tài liệu trích dẫn:**
  - **RDS §7 (`BIZ-027` & `UC-013`):** *"Doanh nghiệp có thể tự đăng ký tài khoản, tự chọn gói dịch vụ và thanh toán qua thẻ tín dụng để tự động khởi tạo không gian làm việc trong vòng 30 giây mà không cần sự can thiệp thủ công của Quản trị viên nền tảng."* (Được gắn nhãn `PROPOSED — NOT APPROVED` dưới `OD-021`).
  - **SRS §4.2 (`FR-TEN-005`):** *"Hệ thống có thể hỗ trợ luồng tự cấp phát Tenant tự động khi tích hợp cổng thanh toán... Trạng thái: PROPOSED — NOT APPROVED / OUT OF SCOPE FOR MVP."*
  - **SSS §21 (`DEC-WS-001` & `DEC-WS-002`):** *"Loại trừ thanh toán, gói cước và quản lý thuê bao thương mại khỏi phạm vi MVP. Mọi hoạt động cấp phát Tenant phải thông qua phê duyệt của Platform Super Admin."*
- **Bản chất xung đột:** Sự tồn tại đồng thời của một luồng nghiệp vụ tự động thanh toán thẻ tín dụng (`UC-013`, `FR-TEN-005`) bên cạnh luồng phê duyệt thủ công của Platform Admin (`UC-001`, `FR-TEN-002`) gây hiểu lầm về phạm vi MVP nếu không đọc kỹ nhãn trạng thái.
- **Tác động đến triển khai & kiểm thử:** Đội ngũ kiểm thử có thể nhầm lẫn rằng việc thiếu cổng thanh toán thẻ tín dụng và cấp phát tự động là lỗi mã nguồn (Defect), trong khi thực tế đó là tính năng nằm ngoài phạm vi MVP.
- **Khuyến nghị giải quyết:**
  - Product Owner ký duyệt chính thức đóng `OD-021`.
  - Di chuyển `BIZ-027`, `UC-013` và `FR-TEN-005` vào phụ lục "Lộ Trình Tương Lai (Future Enterprise Roadmap)", loại bỏ hoàn toàn khỏi danh mục yêu cầu bắt buộc của Release 1.0.

---

### Xung Đột CONF-02: Số Lượng Thực Thể Dữ Liệu Mục Tiêu So Với Thực Thể Hiện Hữu trong DbContext
- **Tài liệu trích dẫn:**
  - **SSS `MODULE_CATALOG.md` & `ARCHITECTURE_DECISIONS.md` (`ADR-SSS-003`, `ADR-SSS-004`):** Đề xuất bổ sung 5 bảng quản lý quy trình phê duyệt (`RoutingRule`, `ApprovalRule`, `ApprovalRuleVersion`, `ApprovalInstance`, `ApprovalStepInstance`) và 2 bảng SLA runtime (`SLAStates`, `SLAEvents`) để khép kín `OD-001..004`.
  - **Mã nguồn thực tế (`BizFlowDbContext.cs`):** Chỉ khai báo đúng **36 DbSets**. Quy trình phê duyệt hiện đang vận hành thông qua `WorkflowTransitions` và `TaskProgressReports`, còn SLA tính toán trực tiếp từ `BusinessCalendar` và `CreatedAt`.
- **Bản chất xung đột:** SSS v1.0 tiền nhiệm ghi nhận 43 bảng, trong khi mã nguồn chỉ có 36 bảng. Tài liệu SSS v1.2 đã đính chính bằng cách gắn nhãn `PROPOSED — NOT APPROVED` cho 7 bảng mở rộng này.
- **Tác động đến triển khai & kiểm thử:** Nếu không đối chiếu với `BizFlowDbContext.cs`, kiến trúc sư có thể thiết kế các truy vấn phụ thuộc vào các bảng chưa tồn tại.
- **Khuyến nghị giải quyết:**
  - Thống nhất duy trì thiết kế 36 bảng hiện tại cho bản phát hành MVP.
  - Các bảng `ApprovalInstance` và `SLAStates` chỉ được tạo migration mới khi có yêu cầu nghiệp vụ chuyên biệt về máy trạng thái phức tạp.

---

### Xung Đột CONF-03: Tập Từ Vựng Mã Quyền Hạn (Permission Codes)
- **Tài liệu trích dẫn:**
  - **SRS v1.0 cũ:** Sử dụng các mã quyền đơn lẻ như `roles.manage`, `services.manage`, `requests.manage`.
  - **Mã nguồn Backend (`SeedDevDatabase.cs`) và Frontend Router (`app.routes.ts`):** Sử dụng các mã quyền cụ thể: `roles.configure`, `workflows.read`, `services.create`, `services.update`, `platform.company-registrations.read`, `platform.audit.read`, `reports.manager`, `reports.company`, `reports.workload`, `reports.sla`.
  - **SSS `ARCHITECTURE_DECISIONS.md` (`ADR-SSS-006` / `OD-019`):** Đề xuất chính thức đồng bộ tài liệu theo tập mã quyền đang chạy trong mã nguồn.
- **Bản chất xung đột:** Độ lệch danh mục quyền giữa tài liệu cũ và mã nguồn thực tế.
- **Khuyến nghị giải quyết:**
  - Chấp thuận chính thức `ADR-SSS-006` và đóng `OD-019`. Chuẩn hóa toàn bộ SRS theo tập mã quyền thực tế của mã nguồn.

---

## 2. Xung Đột Giữa Đặc Tả v1.2 và Mã Nguồn Hiện Hành (Spec vs. Code Discrepancies)

### Xung Đột IMPL-01: Ràng Buộc Đăng Nhập Bằng ID (`FR-AUTH-001`)
- **Đặc tả v1.2:**
  - Bắt buộc đủ 3 trường: `Identifier`, `Password`, `WorkspaceKey`.
  - Backend phải kiểm tra đồng thời cả 3 trường; không được tự động đoán hoặc fallback tìm tenant nếu người dùng không nhập `WorkspaceKey`.
  - Thiếu hoặc sai bất kỳ trường nào đều phải trả về lỗi `401 AUTH.INVALID_CREDENTIALS`.
- **Mã nguồn hiện tại:**
  - Frontend [login.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/auth/login.ts) dòng 37: `tenantKey` là tùy chọn, không có `Validators.required`.
  - Backend [AuthenticationController.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Api/Controllers/AuthenticationController.cs) dòng 30: `LoginInput` có `string? TenantKey` nullable.
  - Backend [AuthenticationService.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Authentication/AuthenticationService.cs) dòng 18-25: Vẫn tìm ứng viên theo `Identifier` trước; nếu chỉ có 1 tenant thì cho phép đăng nhập thành công mà không cần `tenantKey`.
- **Mức độ nghiêm trọng:** **HIGH**.
- **Hành động khắc phục:** Cần sửa mã nguồn Frontend và Backend theo mục `REM-01` trong giai đoạn phát triển kế tiếp.

---

### Xung Đột IMPL-02: Bộ Trường Bắt Buộc Khi Đăng Ký Doanh Nghiệp Công Khai (`FR-TEN-001`)
- **Đặc tả v1.2:**
  - Form đăng ký công khai bắt buộc 6 trường: `TaxId`, `CompanyName`, `WorkspaceCode`, `ContactPersonName`, `ContactEmail`, `ContactPhone`.
  - Cung cấp nút tự sinh mã workspace ngẫu nhiên (`slug + 4 chữ số`).
  - Kiểm tra tính khả dụng mã workspace thời gian thực (`GET /api/v1/tenants/check-code`, `INT-030`).
  - Cho phép đính kèm tệp chứng minh pháp lý tải lên MinIO bucket `company-documents` (`DR-016`).
- **Mã nguồn hiện tại:**
  - Frontend [register-company.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/auth/register-company.ts) chỉ có 3 trường: `code`, `name`, `contactEmail`.
  - Backend [CompanyRegistrationManagement.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Application/Tenancy/CompanyRegistrationManagement.cs) chỉ nhận 3 trường, chưa có endpoint check-code, chưa upload MinIO.
  - Database [Company.cs](file:///d:/FA26/AI-Assisted-BizFlow-Platform/backend/BizFlow.Domain/Tenancy/Company.cs) thiếu các cột thông tin mới.
- **Mức độ nghiêm trọng:** **HIGH**.
- **Hành động khắc phục:** Triển khai theo `REM-03`.

---

### Xung Đột IMPL-03: Thẩm Định Hồ Sơ Đăng Ký Phía Platform Admin (`FR-TEN-002`, `IR-UI-006`)
- **Đặc tả v1.2:**
  - Platform Admin xem danh sách đăng ký kèm liên kết xem/tải tài liệu chứng minh qua Presigned URL MinIO (thời hạn 15 phút).
- **Mã nguồn hiện tại:**
  - Giao diện [company-registrations.ts](file:///d:/FA26/AI-Assisted-BizFlow-Platform/frontend/src/app/features/platform/company-registrations.ts) chưa có cột tài liệu đính kèm và chưa có API lấy Presigned URL.
- **Mức độ nghiêm trọng:** **MEDIUM**.
- **Hành động khắc phục:** Triển khai theo `REM-04`.

---

### Xung Đột IMPL-04: Luồng Bóc Tách Việc Bằng AI Có Xác Nhận Con Người Bắt Buộc (`FR-AI-013`, `BR-028`)
- **Đặc tả v1.2:**
  - Bắt buộc có 2 endpoints: `POST /api/v1/tasks/ai-decomposition/preview` (pure compute / DTO preview) và `POST /api/v1/tasks/ai-decomposition/confirm` (thẩm định quyền và ghi DB).
  - Cấm AI tự ý ghi vào cơ sở dữ liệu khi chưa có sự xác nhận của người dùng (`BR-028`).
  - Frontend hiển thị bảng xem trước tương tác (Interactive Checklist Preview) cho phép chỉnh sửa inline tiêu đề/phòng ban trước khi bấm Xác nhận.
- **Mã nguồn hiện tại:**
  - Backend chưa có bộ API `INT-031` (`preview` và `confirm`).
  - Frontend chưa có component xem trước tương tác.
- **Mức độ nghiêm trọng:** **HIGH**.
- **Hành động khắc phục:** Triển khai theo `REM-05`.

---

### Xung Đột IMPL-05: Robot Trợ Lý Widget Nhúng UI & Điều Phối Nhắc Hạn Đa Kênh (`FR-AI-011`, `FR-NOTIF-004`)
- **Đặc tả v1.2:**
  - Robot widget nhúng góc dưới bên phải màn hình, mặc định mở khi đăng nhập, đóng theo session qua `sessionStorage` (`BR-029`).
  - Hỗ trợ lệnh `/task` và `/notifications`.
  - Bộ điều phối thông báo đa kênh: SignalR $\rightarrow$ Web Push $\rightarrow$ Email với bộ lọc chống trùng lặp qua bảng `NotificationLog`.
- **Mã nguồn hiện tại:**
  - Frontend chưa có component Robot Widget.
  - Backend chưa có `DeadlineReminderWorker`, chưa có Web Push VAPID và chưa có bảng `NotificationLog`.
- **Mức độ nghiêm trọng:** **MEDIUM**.
- **Hành động khắc phục:** Triển khai theo `REM-06` và `REM-07`.

---

## 3. Các Yêu Cầu Còn Mơ Hồ Hoặc Thiếu Tiêu Chí Nghiệm Thu Định Lượng

| Mã Yêu Cầu | Tên Yêu Cầu | Điểm Mơ Hồ / Thiếu Định Lượng Trong Tài Liệu | Rủi Ro Kỹ Thuật | Đề Xuất Chuẩn Hóa Tiêu Chí Nghiệm Thu |
|---|---|---|---|---|
| **NFR-PERF-001** | Thời gian đáp ứng API | SRS ghi: *"95% request nghiệp vụ hoàn tất dưới 500 ms"*. Chưa xác định rõ cấu hình phần cứng thử nghiệm (RAM, CPU, băng thông mạng) và số lượng người dùng đồng thời (Concurrency Level). | Không thể xác minh khách quan khi chuyển giao nghiệm thu. | Bổ sung phụ lục kịch bản tải: *"Thực hiện với công cụ k6/JMeter, 100 Virtual Users đồng thời trong 10 phút trên server 4 vCPU / 8GB RAM, tỷ lệ lỗi < 0.1%"*. |
| **BR-027** | Chống ảo giác AI (Anti-Hallucination) | Quy tắc ghi: *"AI tuyệt đối không được suy diễn số liệu ngoài ngữ cảnh được phân quyền"*. Chưa có độ đo kiểm thử tự động (Metric / Ground Truth Dataset) để đánh giá tỷ lệ vi phạm. | Khó viết automated test để đánh giá hành vi mô hình ngôn ngữ lớn (LLM). | Xây dựng bộ test benchmark 20 câu hỏi bẫy (Adversarial Prompts): Nếu AI phản hồi chứa thông tin ngoài context $\rightarrow$ Đánh giá FAILED. |
| **OD-032** | Bộ lọc chống trùng lặp thông báo | Tài liệu chưa chỉ rõ cửa sổ thời gian (Time Window) cho phép tái phát thông báo khi deadline được dời sang mốc mới (Rescheduled Deadline). | Nếu deadline của Task được dời lại, hệ thống có thể chặn nhầm thông báo mới vì đã có bản ghi trong `NotificationLog`. | Quy định rõ: Khi `WorkTask.Deadline` thay đổi, hệ thống hủy các bản ghi nhắc hạn cũ và tạo bản ghi mới với `TriggerType` và `ScheduledAt` tương ứng. |

---

## 4. Các Khoảng Trống Kiểm Thử (Testing & Verification Gaps)

1. **Kiểm thử Tích hợp Đa Container (Integration Tests Gap):**
   - Bộ `BizFlow.IntegrationTests` đòi hỏi Docker Desktop Daemon hoạt động để khởi tạo testcontainers. Do daemon đang tắt, 100% các ca kiểm thử tích hợp truy vấn PostgreSQL và Redis thật chưa được chạy trong đợt kiểm toán này.
   - **Hành động cần thiết:** Bật Docker Desktop trên máy chủ kiểm thử trước khi tiến hành nghiệm thu UAT.
2. **Kiểm thử Tải Thực Tế (Load & Stress Testing Gap):**
   - Chưa có bộ kịch bản tự động hóa đo lường hiệu năng đỉnh của các API phức tạp (như tổng hợp báo cáo phân tích khối lượng công việc nhiều phòng ban).
   - **Hành động cần thiết:** Xây dựng bộ test script bằng `k6` trong thư mục `tests/load-tests/`.
3. **Kiểm thử Đa Trình Duyệt cho Web Push (Push Notification Testing Gap):**
   - Web Push phụ thuộc vào Service Worker và các cổng dịch vụ bên thứ ba (Google FCM, Apple APNs, Mozilla Autopush).
   - **Hành động cần thiết:** Cần môi trường HTTPS hợp lệ và chứng thư SSL để kiểm thử Service Worker trên trình duyệt thật.

---

## 5. Danh Mục Quyết Định Mở Cần Chủ Sở Hữu Sản Phẩm (PO) Xác Nhận

Dưới đây là các quyết định mở có tác động lớn nhất cần sự tham gia của Product Owner và Hội đồng Kỹ thuật:

| Mã OD | Chủ Đề Quyết Định | Tùy Chọn 1 (Khuyến Nghị Của Nhóm Kỹ Thuật) | Tùy Chọn 2 (Phương Án Thay Thế) | Tác Động Dự Kiến | Cấp Thẩm Quyền |
|---|---|---|---|---|---|
| **OD-021** | Phạm vi Onboarding Doanh nghiệp MVP | **Baseline:** Chỉ duy trì luồng Platform Admin phê duyệt đơn đăng ký (`FR-TEN-001` + `FR-TEN-002`). Đóng `OD-021` và hoãn `FR-TEN-005` sang Phase 2. | Triển khai ngay cổng thanh toán Stripe/VNPay và cấp phát tự động trong MVP. | Giữ nguyên phạm vi cam kết, tránh kéo dài thời gian phát triển MVP thêm 4-6 tuần. | **Product Owner** |
| **OD-025** | Cơ chế Đăng nhập Hợp nhất v1.2 | **Bắt buộc đủ 3 trường:** `Identifier`, `Password`, `WorkspaceKey` cho Option 1 để bảo vệ triệt để đa thuê bao và chống brute-force. | Giữ cơ chế tự động tìm Tenant nếu bỏ trống WorkspaceKey (như mã nguồn cũ). | Tùy chọn 1 an toàn hơn về an ninh nhưng đòi hỏi người dùng phải nhớ Workspace Key. | **Product Owner & Architect** |
| **OD-028** | Vòng đời Robot Widget Nhúng UI | **Mặc định mở** khi đăng nhập, cho phép bấm [X] đóng cho session hiện tại qua `sessionStorage` (`BR-029`). | Mặc định thu nhỏ dạng bong bóng chat tròn (Floating Action Button), bấm mới mở rộng. | Tùy chọn 1 gây ấn tượng mạnh về tính năng AI trợ giúp nhưng có thể chiếm diện tích màn hình trên thiết bị nhỏ. | **Product Owner & UI/UX Designer** |
| **OD-031** | Kênh Ưu Tiên Cho Thông Báo Nhắc Hạn | **Điều phối thông minh 3 lớp:** SignalR In-app $\rightarrow$ Web Push $\rightarrow$ Email Fallback (`BR-030`). | Chỉ dùng SignalR in-app và Email, bỏ qua Web Push để đơn giản hóa kiến trúc MVP. | Tùy chọn 1 tiếp cận người dùng tốt hơn khi đóng trình duyệt, nhưng đòi hỏi cấu hình Service Worker và VAPID keys. | **Product Owner & Technical Lead** |
| **OD-019** | Chuẩn hóa Tập Mã Quyền Hạn (Permission Codes) | **Chọn tập mã quyền đang chạy thực tế trong mã nguồn** (`roles.configure`, `services.create`, v.v.) làm chuẩn duy nhất trong tài liệu. | Refactor mã nguồn C# và Angular để đổi tên quyền theo tài liệu cũ. | Tùy chọn 1 tiết kiệm công sức, không gây rủi ro hồi quy mã nguồn. | **Architect & Lead Developer** |

---

## 6. Kết Luận
Báo cáo này làm rõ ranh giới giữa những gì **đã hoàn thành vững chắc** trong mã nguồn (Baseline MVP) và những gì **đang được định nghĩa ở cấp đặc tả v1.2** để định hướng cho các Sprint tiếp theo. Không có bất kỳ khoảng trống nào bị che giấu hoặc giả mạo bằng chứng.
