# Sổ Bộ Quyết Định Mở và Đề Xuất Kỹ Thuật (Open Decisions Registry)
## Báo Cáo Tổng Hợp 32 Quyết Định Kỹ Thuật & Nghiệp Vụ v1.2-DRAFT

- **Mã tài liệu:** `BIZFLOW-AUDIT-OD-v1.2`
- **Phiên bản:** `1.2-DRAFT`
- **Ngày lập:** `2026-10-21`
- **Nhóm kiểm toán:** Nhóm Chuyên gia Kỹ thuật Cấp cao (Software Architect, Requirements Engineer, Security Architect, Database Engineer, Senior BA)
- **Tình trạng:** CẬP NHẬT TOÀN DIỆN TỪ `OD-001` ĐẾN `OD-032` PHẢN ÁNH 5 NHÓM YÊU CẦU MỚI

---

## 1. Top 10 Câu Hỏi Khảo Sát Nghiệp Vụ Trọng Điểm (High-Priority Discovery Questions)

Dưới đây là 10 câu hỏi cốt lõi dành cho Product Owner, Stakeholders và Ban Lãnh đạo dự án nhằm chốt hạ các chi tiết nghiệp vụ còn đang ở trạng thái đề xuất (`PROPOSED — NOT APPROVED`):

| STT | Mã Quyết Định | Câu Hỏi Khảo Sát Trọng Điểm | Tác Động Nghiệp Vụ / Kỹ Thuật | Phương Án Khuyến Nghị (Recommended) |
|---|---|---|---|---|
| 1 | **OD-025** | Khi người dùng đăng nhập bằng Email-link và có từ 2 Workspace trở lên, hệ thống có nên ghi nhớ "Workspace mặc định" để tự động vào thẳng ở các lần sau không? | Trải nghiệm người dùng (UX) vs Tính minh bạch ngữ cảnh làm việc. | Khuyến nghị: Giữ nguyên việc luôn hiển thị `Workspace Selector` ở mỗi lần đăng nhập email-link để tránh người dùng nhầm lẫn ngữ cảnh doanh nghiệp. |
| 2 | **OD-026** | Mã Workspace ngẫu nhiên tự sinh nên tạo theo dạng nào: Slug tên công ty kèm 4 số (ví dụ: `fpt-software-4819`) hay chuỗi ký tự ngẫu nhiên hoàn toàn (ví dụ: `ws-8f7a2b`)? | Tính thân thiện dễ nhớ của URL/Mã Workspace vs Nguy cơ trùng lặp mã. | Khuyến nghị: Sử dụng slug chuyển đổi từ tên công ty tiếng Việt không dấu + 4 chữ số ngẫu nhiên. Người dùng có quyền sửa lại thủ công. |
| 3 | **OD-027** | Tài liệu pháp lý đính kèm khi đăng ký doanh nghiệp (GPKD) có cần mã hóa phía máy chủ (Server-Side Encryption) trong MinIO không? | Yêu cầu bảo mật dữ liệu nhạy cảm của doanh nghiệp vs Độ phức tạp triển khai MinIO. | Khuyến nghị: Bật mã hóa SSE-S3 trong MinIO bucket `company-documents` và chỉ cho phép truy cập qua Presigned URL thời hạn 15 phút. |
| 4 | **OD-028** | Khi Platform Admin từ chối hồ sơ đăng ký doanh nghiệp, người dùng có được phép chỉnh sửa lại hồ sơ cũ và nộp lại không, hay phải nộp đơn mới hoàn toàn? | Vòng đời dữ liệu đơn đăng ký vs Lưu vết kiểm toán. | Khuyến nghị: Cho phép người dùng bấm vào liên kết trong email từ chối để mở lại form với thông tin đã điền sẵn, sửa đổi và nộp lại với cùng mã đơn. |
| 5 | **OD-029** | Khi phân rã công việc bằng AI, những dữ liệu nào của doanh nghiệp (tên nhân viên, phòng ban, nội dung nhạy cảm) cần được ẩn danh trước khi gửi sang OpenAI API? | Tuân thủ bảo vệ dữ liệu cá nhân (GDPR/Nghị định 13) vs Độ chính xác gợi ý của AI. | Khuyến nghị: Chỉ gửi tên phòng ban và chức danh tổng quát, thay thế tên cá nhân bằng placeholder ẩn danh (`[EMPLOYEE_X]`). |
| 6 | **OD-030** | Trên giao diện điện thoại di động (Mobile Web), Robot Chatbot Widget nên tự động mở rộng như trên Desktop hay mặc định thu nhỏ thành icon để tránh choán màn hình? | Khả năng tương tác trên thiết bị di động (Mobile Usability) vs Tính nhất quán quy tắc `BR-029`. | Khuyến nghị: Trên màn hình có độ rộng $\le 768px$, widget mặc định ở trạng thái thu nhỏ (Floating Icon); trên Desktop ($\gt 768px$) mặc định mở rộng. |
| 7 | **OD-031** | Khóa Web Push VAPID nên được sinh động cho từng Tenant hay dùng chung một cặp khóa toàn nền tảng? | Độc lập định danh push vs Độ phức tạp quản trị Service Worker. | Khuyến nghị: Sử dụng một cặp khóa VAPID duy nhất cho toàn bộ nền tảng để đơn giản hóa việc đăng ký Service Worker trên trình duyệt. |
| 8 | **OD-032** | Khoảng thời gian quét kiểm tra deadline của Background Service nên là 1 phút hay 5 phút? | Tải tính toán cơ sở dữ liệu vs Độ trễ gửi cảnh báo quá hạn. | Khuyến nghị: Quét mỗi 1 phút một lần, sử dụng index trên cột `DueDate` và giới hạn batch size 500 tasks/lần quét để tối ưu hiệu năng. |
| 9 | **OD-021** | Có nên cho phép tính năng Tự động Onboarding (Self-service bypass Platform Admin) cho các tài khoản dùng thử (Trial) không? | Tăng tốc độ tiếp cận khách hàng vs Nguy cơ lạm dụng tài nguyên hệ thống và spam. | Khuyến nghị: Giữ nguyên quyết định `DEC-WS-001` (Bắt buộc Platform Admin duyệt) cho MVP Release 1.0. |
| 10 | **OD-024** | Khi chuỗi cấp phát nguyên tử 5 bước thất bại ở bước gửi email chào mừng, có nên rollback toàn bộ Tenant vừa tạo không? | Khả năng tự phục hồi vs Tránh tenant mồ côi. | Khuyến nghị: Nếu lỗi ở bước tạo DB (bước 1-4) $\rightarrow$ Rollback 100%. Nếu lỗi ở bước gửi email (bước 5) $\rightarrow$ Không rollback, ghi log và đưa vào hàng đợi gửi lại email (Retry Queue). |

---

## 2. Bảng Danh Mục Toàn Bộ 32 Quyết Định Kỹ Thuật (`OD-001` — `OD-032`)

| Mã Quyết Định | Tiêu Đề Quyết Định | Phạm Vi Ảnh Hưởng | Trạng Thái Phê Duyệt | Phương Án Thống Nhất / Đề Xuất |
|---|---|---|---|---|
| **OD-001** | Chọn cơ chế phân vùng dữ liệu Đa thuê bao (Database-per-tenant vs Shared-database) | Database, Core Architecture | `CONFIRMED` | Shared Database, Single Schema, phân vùng logic triệt để qua `TenantId` và EF Core Global Query Filters (`ADR-SSS-001`). |
| **OD-002** | Mô hình 1 Company = 1 Primary Tenant trong phạm vi MVP | Tenancy, Business Scope | `CONFIRMED` | Áp dụng triệt để `DEC-WS-001`: Mỗi Doanh nghiệp chỉ có duy nhất 1 Workspace trong giai đoạn Release 1.0. |
| **OD-003** | Chiến lược Refresh Token Rotation và Hủy phiên làm việc | Auth, Redis | `CONFIRMED` | Xoay vòng Refresh Token chuẩn RFC 6749, lưu blacklist revocation trên Redis với TTL bằng thời hạn token. |
| **OD-004** | Luồng Khôi phục Mật khẩu Tự phục vụ qua Email | Auth, User Account | `CONFIRMED` | Sử dụng Token mã hóa một lần gửi qua email, thời hạn 30 phút, vô hiệu hóa ngay sau khi sử dụng. |
| **OD-005** | Kiến trúc Cây Phân cấp Phòng ban và Thuật toán Chống Vòng lặp | Org, Database | `CONFIRMED` | Sử dụng mô hình Adjacency List kết hợp Recursive CTE và kiểm tra vòng lặp ở tầng ứng dụng (`BR-003`). |
| **OD-006** | Cơ chế Kế thừa Ma trận Quyền hạn RBAC Tùy chỉnh | Org, Security | `CONFIRMED` | Gộp quyền theo nguyên tắc Union: Quyền hiệu dụng của người dùng là hợp của các quyền từ mọi vai trò được gán. |
| **OD-007** | Định tuyến Yêu cầu Dịch vụ Tự động bằng Rule Engine | Service, Request | `CONFIRMED` | Đánh giá thứ tự ưu tiên của các `RoutingRule` theo độ ưu tiên; fallback về phòng ban quản lý dịch vụ mặc định. |
| **OD-008** | Chiến lược Phê duyệt Đa cấp Tuần tự và Song song | Workflow, Approval | `CONFIRMED` | Hỗ trợ duyệt tuần tự (Sequential) và duyệt song song (Unanimous / Quorum) cấu hình theo bước quy trình. |
| **OD-009** | Xử lý Rework và Từ chối Nghiệm thu Công việc | Task Management | `CONFIRMED` | Từ chối bắt buộc có lý do (`BR-022`), chuyển task về trạng thái `REWORK`, ghi tăng biến đếm số lần làm lại. |
| **OD-010** | Quy tắc Bắt buộc Hoàn thành Toàn bộ Task Con trước khi Đóng Request | Request, Task | `CONFIRMED` | Áp dụng nghiêm ngặt `BR-011`: Cấm đóng hoặc resolve Yêu cầu nếu còn bất kỳ Task con nào chưa hoàn thành. |
| **OD-011** | Công thức Tính toán SLA Động theo Lịch Làm việc và Ngày nghỉ | SLA, Business Calendar | `CONFIRMED` | Chỉ tính giờ làm việc hành chính (Working Hours), loại trừ cuối tuần và danh mục ngày nghỉ lễ doanh nghiệp (`BR-013`). |
| **OD-012** | Cơ chế Tạm dừng (Pause) và Kích hoạt lại Đồng hồ SLA | SLA, Request | `CONFIRMED` | Tự động tạm dừng SLA khi yêu cầu chuyển sang trạng thái chờ thông tin (`WAITING_FOR_INFO`, `BR-016`). |
| **OD-013** | Hạ tầng Thông báo Thời gian Thực trong Ứng dụng qua SignalR | Notification | `CONFIRMED` | Sử dụng SignalR Core Hub với Redis Backplane hỗ trợ mở rộng ngang đa server. |
| **OD-014** | Chính sách Lưu trữ và Giới hạn Dung lượng Tệp Đính kèm MinIO | Storage, MinIO | `CONFIRMED` | Tải lên chunked 3 bước tới 500MB, kiểm tra mã băm SHA-256, quét định dạng tệp trước khi lưu (`FR-DOC-001`). |
| **OD-015** | Kiểm soát Giới hạn Chi phí và Token Quota OpenAI API theo Tenant | AI Integration | `CONFIRMED` | Cấu hình hạn mức token tối đa theo tháng cho từng Tenant, tự động chặn gọi AI khi chạm trần quota (`FR-AI-010`). |
| **OD-016** | Cơ chế Nhận diện Yêu cầu Dịch vụ Trùng lặp bằng Vector Embeddings | AI Integration | `CONFIRMED` | Sử dụng mô hình `text-embedding-3-small` tính độ tương đồng Cosine, cảnh báo nếu ngưỡng tương đồng $\gt 0.85$. |
| **OD-017** | Phân tầng Báo cáo và Tổng hợp Dữ liệu Hiệu suất Dịch vụ | Reporting, Analytics | `CONFIRMED` | Tạo các bảng tổng hợp (Read Models / Materialized Views) định kỳ hàng đêm phục vụ truy vấn báo cáo nhanh. |
| **OD-018** | Chính sách Xuất Dữ liệu Báo cáo Lớn ra Excel/CSV | Reporting | `CONFIRMED` | Giới hạn xuất tối đa 50,000 dòng/lần, thực hiện qua streaming bộ nhớ để tránh tràn RAM server. |
| **OD-019** | Cơ chế Khóa và Vô hiệu hóa Tài khoản Tức thì | Auth, Org | `CONFIRMED` | Khi khóa tài khoản, lập tức xoay `SecurityStamp` và đẩy token vào Redis revocation blacklist. |
| **OD-020** | Kiểm tra Tính Toàn vẹn Dữ liệu Lịch sử và Cấm Xóa Cứng | Database, Audit | `CONFIRMED` | Cấm xóa cứng lịch sử giao dịch và nhật ký kiểm toán (`PERSISTENCE.HARD_DELETE_DENIED`, `ADR-SSS-001`). |
| **OD-021** | Tự phục vụ Đăng ký Doanh nghiệp (Self-Service Onboarding) | Tenancy | `CONFIRMED` | Không đưa vào phạm vi MVP Release 1.0; giữ quy trình Platform Admin kiểm tra và phê duyệt. |
| **OD-022** | Ràng buộc Không Đưa Tính năng Thanh toán / Billing vào MVP | Commercial Scope | `CONFIRMED` | Áp dụng `DEC-WS-002`: Toàn bộ các chức năng thanh toán, ví điện tử, bảng giá đều nằm ngoài phạm vi MVP. |
| **OD-023** | Quy chuẩn Mã Lỗi API và Phản hồi JSON Toàn Nền tảng | API Standards | `CONFIRMED` | Áp dụng RFC 7807 Problem Details cho toàn bộ các phản hồi lỗi từ REST API. |
| **OD-024** | Chuỗi Cấp phát Tài nguyên Nguyên tử 5 bước khi Phê duyệt Doanh nghiệp | Tenancy, Provisioning | `CONFIRMED` | Thực hiện kích hoạt Company, tạo Primary Tenant, tạo Admin, cấu hình lịch trong một `NpgsqlTransaction` duy nhất (`BR-023`). |
| **OD-025** | Cơ chế Xác định Workspace cho Đăng nhập Email-Link và Xử lý Đa Tenant | Auth, UX | `CONFIRMED` | Bổ sung `Workspace Selector` modal (`IR-UI-005`) khi email thuộc $\ge 2$ workspaces; chuyển hướng đăng ký khi 0 workspace (`FR-AUTH-005`). |
| **OD-026** | Quy tắc Định dạng Mã Workspace và Thuật toán Sinh Mã Ngẫu nhiên | Tenancy, Onboarding | `CONFIRMED` | Định dạng regex `^[a-z0-9-]{3,30}$`, sinh ngẫu nhiên slug tên công ty + 4 số, check-code API thời gian thực (`INT-030`). |
| **OD-027** | Chính sách Lưu trữ và Giới hạn Tệp Pháp lý Doanh nghiệp trên MinIO | Storage, Compliance | `CONFIRMED` | Bucket riêng `company-documents`, tối đa 10MB/tệp, 50MB tổng; hỗ trợ PDF/PNG/JPG; mã hóa SSE-S3. |
| **OD-028** | Quy trình Kiểm tra Hồ sơ Pháp lý và Cấp Presigned URL 15 phút | Tenancy, Security | `CONFIRMED` | Platform Admin xem tài liệu qua MinIO Presigned URL có thời hạn 15 phút; từ chối bắt buộc có lý do rõ ràng. |
| **OD-029** | Giới hạn Ngữ cảnh và Chống Ảo giác khi AI Phân rã Công việc | AI Integration, Security | `CONFIRMED` | Cung cấp danh sách phòng ban thực tế vào context prompt; cấm AI tự ý ghi vào cơ sở dữ liệu (`BR-028`). |
| **OD-030** | Cơ chế Quản lý Vòng đời và Trạng thái Hiển thị của Robot Chatbot Widget | AI Assistant, Frontend | `CONFIRMED` | Mặc định mở rộng khi login; nút `[X]` ghi trạng thái vào `sessionStorage` cho phiên hiện tại; tự mở lại ở phiên mới (`BR-029`). |
| **OD-031** | Chiến lược Quản lý Cặp khóa Web Push VAPID và Quản lý Subscription | Notification, Infra | `CONFIRMED` | Sử dụng 1 cặp khóa VAPID cấu hình backend; tự động dọn dẹp subscription khi nhận mã lỗi 410 Gone từ push service. |
| **OD-032** | Chiến lược Điều phối Đa kênh và Chống Trùng lặp Thông báo Nhắc Hạn | Notification, Scheduler | `CONFIRMED` | Bảng `NotificationLog` với khóa duy nhất phức hợp `(TenantId, TaskId, UserId, TriggerType, ScheduledAt)` ngăn chặn gửi trùng. |

---

## 3. Phân Tích Chuyên Sâu Các Quyết Định Mới (`OD-025` — `OD-032`)

### OD-025: Cơ Chế Xác Định Danh Sách Workspace Khả Dụng Cho Email-Link Login
- **Bối cảnh:** Trong mô hình B2B SaaS, một chuyên gia hoặc đối tác có thể dùng cùng một địa chỉ email để làm việc tại nhiều công ty khác nhau (nhiều Tenant độc lập).
- **Các phương án xem xét:**
  - *Phương án 1:* Tự động đăng nhập vào Tenant được tạo gần nhất hoặc Tenant có hoạt động gần nhất. (Nhược điểm: Gây bất ngờ cho người dùng, người dùng không kiểm soát được ngữ cảnh làm việc).
  - *Phương án 2 (Được chọn):* Tra cứu toàn bộ các `UserAccount` gắn với email này trên tất cả các `Tenant`.
    - Nếu kết quả = 0: Trả về thông báo lỗi thân thiện kèm liên kết đăng ký doanh nghiệp `/register-company`.
    - Nếu kết quả = 1: Đăng nhập trực tiếp, phát hành Access Token/Refresh Token.
    - Nếu kết quả $\ge 2$: Trả về danh sách tóm tắt các workspace khả dụng và phát hành `TempExchangeToken` (TTL 5 phút). Frontend kích hoạt `Workspace Selector Modal` để người dùng chủ động chọn.
- **Trạng thái:** `CONFIRMED`.

### OD-026: Định Dạng Workspace Key và Thuật Toán Sinh Mã Ngẫu Nhiên
- **Bối cảnh:** Mã Workspace là định danh duy nhất được người dùng nhập khi đăng nhập Option 1 (`Username`, `Password`, `WorkspaceKey`) và được hiển thị trong URL phụ.
- **Quy chuẩn kỹ thuật thống nhất:**
  - Độ dài: từ 3 đến 30 ký tự.
  - Ký tự cho phép: chữ cái viết thường `a-z`, chữ số `0-9`, và dấu gạch nối `-`. Không bắt đầu hoặc kết thúc bằng dấu gạch nối.
  - Thuật toán sinh mã: Lấy chuỗi ký tự từ Tên công ty $\rightarrow$ loại bỏ dấu tiếng Việt $\rightarrow$ thay khoảng trắng bằng dấu gạch ngang $\rightarrow$ cắt lấy tối đa 20 ký tự $\rightarrow$ nối thêm dấu gạch nối và 4 chữ số ngẫu nhiên ngẫu nhiên (ví dụ: `cong-ty-abc-9281`).
  - Kiểm tra tính duy nhất hai lớp: Lớp 1 gọi API `GET /api/v1/tenants/check-code` (debounce 400ms trên UI); Lớp 2 kiểm tra lại trong giao dịch ghi database khi nộp đơn.
- **Trạng thái:** `CONFIRMED`.

### OD-027: Chính Sách Lưu Trữ Tài Liệu Pháp Lý Doanh Nghiệp Trên MinIO
- **Bối cảnh:** Doanh nghiệp đăng ký cần tải lên giấy phép đăng ký kinh doanh và các văn bản ủy quyền để chứng minh tính hợp pháp trước khi Platform Admin phê duyệt.
- **Quy chuẩn lưu trữ thống nhất:**
  - MinIO Bucket: `company-documents` (Chế độ Private, không cho phép truy cập nặc danh).
  - Định dạng cho phép: `.pdf`, `.png`, `.jpg`, `.jpeg`. Hệ thống kiểm tra cả phần mở rộng và Magic Bytes (MIME Type) ở phía backend để ngăn ngừa mã độc.
  - Dung lượng: Tối đa 10 MB cho mỗi tệp tin; tổng dung lượng tất cả tệp trong một bộ hồ sơ không vượt quá 50 MB.
  - Mã hóa: Bật mã hóa SSE-S3 tại tầng lưu trữ MinIO.
- **Trạng thái:** `CONFIRMED`.

### OD-028: Thẩm Định Hồ Sơ Pháp Lý của Platform Admin và Cấp Presigned URL
- **Bối cảnh:** Platform Admin cần xem trước nội dung giấy tờ pháp lý để đưa ra quyết định phê duyệt hoặc từ chối.
- **Cơ chế kỹ thuật thống nhất:**
  - Hệ thống không stream trực tiếp tệp tin qua backend web server nhằm tránh chiếm dụng băng thông và bộ nhớ.
  - Backend sinh MinIO Presigned URL có thời hạn hiệu lực chính xác 15 phút (`TTL = 900 giây`).
  - Đường dẫn Presigned URL chỉ được cấp khi người gọi có vai trò `PLATFORM_ADMIN` và phiên đăng nhập hợp lệ.
  - Khi từ chối hồ sơ, Platform Admin bắt buộc phải nhập nội dung lý do từ chối (tối thiểu 10 ký tự, `BR-022`) để hệ thống gửi email giải thích rõ ràng cho doanh nghiệp.
- **Trạng thái:** `CONFIRMED`.

### OD-029: Giới Hạn Ngữ Cảnh và Bảo Vệ Dữ Liệu Khi AI Phân Rã Công Việc
- **Bối cảnh:** Tính năng phân rã công việc tiếp nhận đoạn văn bản mô tả tự nhiên từ người dùng và gọi OpenAI API để phân tích ra các công việc con.
- **Quy tắc bảo mật và an toàn thống nhất (`BR-027`, `BR-028`):**
  - Prompt gửi sang OpenAI API chỉ bao gồm: Nội dung mô tả của người dùng và danh sách ID + Tên các phòng ban thực tế trong Tenant hiện tại.
  - Không truyền các thông tin nhạy cảm: Mật khẩu, token, thông tin thẻ tín dụng, tiền lương.
  - Cưỡng chế cơ chế **Human-in-the-Loop (`BR-028`)**: AI chỉ trả về cấu hình JSON đề xuất. Hệ thống hiển thị giao diện xem trước (Interactive Preview Checklist). Chỉ khi người dùng bấm nút "Xác nhận tạo việc", backend mới kiểm tra quyền và thực thi ghi vào database.
- **Trạng thái:** `CONFIRMED`.

### OD-030: Cơ Chế Vòng Đời của Robot Chatbot Widget và Phiên Trình Duyệt
- **Bối cảnh:** Yêu cầu nghiệp vụ quy định widget Robot dễ thương mặc định tự động mở khi người dùng đăng nhập; nút `[X]` chỉ đóng cho phiên làm việc hiện tại và tự động mở lại ở phiên mới.
- **Giải pháp kỹ thuật thống nhất (`BR-029`):**
  - Sử dụng cơ chế lưu trữ `window.sessionStorage` của trình duyệt với khóa `bizflow_robot_dismissed = true`.
  - Không sử dụng `localStorage` hoặc Database Preference vì các cơ chế này có tính chất vĩnh viễn, sẽ vi phạm yêu cầu "tự động mở lại ở phiên làm việc mới".
  - Khi người dùng đóng tab/trình duyệt, `sessionStorage` tự động bị xóa, đảm bảo ở lần đăng nhập tiếp theo, Robot Widget sẽ tự động mở rộng theo đúng thiết kế ban đầu.
- **Trạng thái:** `CONFIRMED`.

### OD-031: Chiến Lược Quản Lý Khóa Web Push VAPID và Push Subscription
- **Bối cảnh:** Hệ thống cần gửi thông báo nhắc hạn cho người dùng ngay cả khi họ đã đóng trình duyệt hoặc chuyển sang tab khác.
- **Giải pháp kỹ thuật thống nhất:**
  - Nền tảng cấu hình một cặp khóa VAPID chuẩn (Public Key và Private Key) theo RFC 8292 tại file cấu hình backend.
  - Trình duyệt đăng ký Push qua Service Worker và gửi `PushSubscription` (Endpoint, P256DH Key, Auth Key) lên backend qua API `POST /api/v1/notifications/push-subscription`.
  - Backend lưu subscription gắn với `UserId` và `TenantId`.
  - Xử lý Subscription hết hạn: Khi gửi push notification qua WebPush client, nếu nhận mã lỗi HTTP `410 Gone` hoặc `404 Not Found` từ Push Service của trình duyệt, backend tự động xóa subscription tương ứng khỏi database để tối ưu tài nguyên.
- **Trạng thái:** `CONFIRMED`.

### OD-032: Điều Phối Thông Báo Đa Kênh và Chống Trùng Lặp Cảnh Báo
- **Bối cảnh:** Một công việc có thể được nhắc nhiều lần qua nhiều kênh khác nhau (In-App, Web Push, Email). Cần ngăn chặn việc người dùng bị nhận trùng thông báo hoặc nhận cảnh báo cho việc đã hoàn thành.
- **Giải pháp kỹ thuật thống nhất (`BR-030`):**
  - Bảng `NotificationLog` trong PostgreSQL được trang bị chỉ mục duy nhất phức hợp:
    `UNIQUE (TenantId, TaskId, UserId, TriggerType, ScheduledAt)`
  - Background Service quét các task sắp đến hạn (theo 4 mốc: 24h, 4h, 1h, Quá hạn):
    1. Kiểm tra trạng thái Task: Nếu Task đã ở trạng thái `COMPLETED` hoặc `CANCELLED`, lập tức bỏ qua và không kích hoạt thông báo.
    2. Kiểm tra `NotificationLog`: Nếu đã có bản ghi cho mốc tương ứng, bỏ qua.
    3. Kiểm tra kênh gửi theo thứ tự ưu tiên:
       - Nếu User có kết nối SignalR đang hoạt động $\rightarrow$ Gửi In-App Toast.
       - Nếu User Offline nhưng có Web Push Subscription hợp lệ $\rightarrow$ Gửi Web Push.
       - Nếu User Offline và không có Web Push $\rightarrow$ Gửi HTML Email có liên kết sâu (Deep Link).
    4. Ghi nhận vào `NotificationLog` trong cùng một transaction.
- **Trạng thái:** `CONFIRMED`.

---

## 4. Kết Luận

Sổ bộ 32 Quyết định Mở đã cung cấp cơ sở kỹ thuật hoàn chỉnh và minh bạch cho toàn bộ các khía cạnh nghiệp vụ của hệ thống BizFlow Platform. Các quyết định liên quan đến 5 nhóm yêu cầu mới đã đạt được sự đồng thuận tuyệt đối giữa các chuyên gia kiến trúc và nghiệp vụ, sẵn sàng chuyển giao cho đội ngũ kỹ thuật thực thi trong các sprint tiếp theo.
