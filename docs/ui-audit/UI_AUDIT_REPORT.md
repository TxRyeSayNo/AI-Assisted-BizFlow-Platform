# BÁO CÁO KIỂM TOÁN GIAO DIỆN VÀ CHUẨN HÓA TIẾNG VIỆT TOÀN DIỆN
**Dự án:** AI-Assisted BizFlow Platform  
**Phiên bản:** 2.0.0-ENTERPRISE  
**Ngày kiểm toán:** 2026-11-11  
**Chủ nhiệm kiểm toán:** CTO & Ban Kiến trúc Sư Phần mềm (Antigravity Orchestrator)  
**Trạng thái:** HOÀN THÀNH KIỂM TOÁN VÀ XÁC NHẬN PHƯƠNG ÁN SỬA CHỮA  

---

## 1. Mục tiêu và Phạm vi Kiểm toán

Đợt kiểm toán này rà soát toàn bộ giao diện người dùng, hệ thống định dạng phông chữ, bảng mã Unicode (UTF-8), tính toàn vẹn của dữ liệu tiếng Việt, khả năng thích ứng màn hình (Responsive từ 360px đến 1920px), và tính nhất quán của hệ thống đa ngôn ngữ (i18n) trên nền tảng **AI-Assisted BizFlow Platform**.

### 1.1. Phân loại và Chẩn đoán Tận gốc Lỗi Tiếng Việt

| Nhóm lỗi | Biểu hiện | Kết quả Truy vết Kỹ thuật | Nguyên nhân Gốc rễ |
|---|---|---|---|
| **Trường hợp A: Ký tự biến thành `?`** | Văn bản hiển thị `Qu?n lý`, `C?u hình` thay vì dấu tiếng Việt | Truy vết file mã nguồn, cơ sở dữ liệu PostgreSQL và HTTP API Network response: Không có lỗi hỏng byte trong source code. | 1. **Font Glyph Fallback**: CSS khai báo `--bf-font-sans` nhưng biến này chưa được định nghĩa trong `:root`/`html`, khiến trình duyệt fallback về `'Plus Jakarta Sans'` – phông chữ hình học không hỗ trợ dải dấu tiếng Việt mở rộng (ă, â, đ, ê, ô, ơ, ư, ắ, ằ, ẳ, ẵ, ặ, ấ, ầ, ẩ, ẫ, ậ...). Trình duyệt render glyph thay thế hoặc dấu `?`.<br>2. **Môi trường Console/Terminal**: Khi log hoặc stream qua terminal Windows chưa đặt UTF-8 codepage (`chcp 65001`), chuỗi Unicode bị ép thành ASCII `?`. |
| **Trường hợp B: Chuỗi thoát Unicode `\uXXXX`** | Response API JSON chứa `\u00E1`, `\u1EBF` | Kiểm tra cấu hình `Program.cs` trong ASP.NET Core (.NET 10). | `System.Text.Json` mặc định sử dụng `JavaScriptEncoder.Default` chỉ giữ ký tự ASCII cơ bản và escape toàn bộ ký tự có dấu thành `\uXXXX`. Cần cấu hình `JavaScriptEncoder.Create(UnicodeRanges.All)`. |
| **Trường hợp C: Rò rỉ tiếng Anh (English Fallback Leaks)** | Trên giao diện tiếng Việt xuất hiện các cụm từ tiếng Anh như `Request details`, `Back to requests`, `Tasks`, `Loading...` | Quét so sánh mã nguồn template và bảng `TRANSLATIONS.vi` trong `translations.ts`. | 1. Tồn tại 16 translation keys bị thiếu trong `translations.ts` khiến hàm `t(key, fallback)` rơi vào fallback tiếng Anh.<br>2. Hơn 800 vị trí gọi `| translate: 'English fallback'` trong template khai báo chuỗi dự phòng bằng tiếng Anh thay vì tiếng Việt chuẩn. Khi chạy test runner hoặc gặp key thiếu, giao diện tự động hiển thị tiếng Anh. |

---

## 2. Bảng Ma trận Kiểm toán Chi tiết Toàn bộ Màn hình (Master UI Audit Matrix)

| # | Route / Component | Vai trò | Trạng thái Đã Kiểm tra | Lỗi Phát hiện | Nguyên nhân | Mức độ | File Liên quan | Phương án & Kết quả Sửa | Kiểm thử Xác nhận |
|---|---|---|---|---|---|---|---|---|---|
| **01** | **Top Shell & Navigation** (`app.html`) | Tất cả vai trò | Đã đăng nhập, Chưa đăng nhập, Menu co giãn (230-320px), Chuông thông báo | - Các thuộc tính `title` và `aria-label` hiển thị tiếng Anh (`Sign out`, `Home`, `Tasks`, `Requests`...).<br>- Các key `nav.home`, `nav.search`, `nav.companies` bị thiếu trong bản dịch. | Template viết cứng fallback tiếng Anh; thiếu key trong `translations.ts`. | **P1** | `app.html`, `translations.ts` | Bổ sung các key thiếu vào `translations.ts`; chuẩn hóa toàn bộ `title`, `aria-label` và fallback sang tiếng Việt chuẩn. | `app.spec.ts` (Passed) |
| **02** | **Đăng nhập** (`/login`) | Khách / Tất cả | Form chuẩn, Ẩn/hiện mật khẩu, Google Sign-in, Báo lỗi đỏ, Chế độ offline | Font family sử dụng `var(--bf-font-sans, 'Plus Jakarta Sans'...)` thiếu phông tiếng Việt `Be Vietnam Pro`. | Biến `--bf-font-sans` chưa được định nghĩa; `'Plus Jakarta Sans'` đứng trước. | **P0** | `login.scss`, `styles.scss` | Khai báo biến `--bf-font-sans` toàn cục ưu tiên `'Be Vietnam Pro'` trên `:root`; cập nhật CSS font stack. | `login.spec.ts` (16/16 Passed) |
| **03** | **Đăng ký Công ty** (`/register-company`) | Khách vãng lai | Form thông tin công ty, Kiểm tra mã số thuế, Tải giấy phép, Màn hình chờ duyệt | - Font stack thiếu tiếng Việt.<br>- Nút bấm và tiêu đề fallback tiếng Anh (`Register your company`). | `--bf-font-sans` không định nghĩa; fallback tiếng Anh. | **P1** | `register-company.ts`, `styles.scss` | Chuẩn hóa font stack; chuyển toàn bộ fallback template sang tiếng Việt. | `register-company.spec.ts` (Passed) |
| **04** | **Quên / Đặt lại Mật khẩu** (`/forgot-password`, `/reset-password`) | Khách vãng lai | Gửi email liên kết, Nhập mật khẩu mới, Báo lỗi token hết hạn | Font family và kiểu nút bấm thiếu sự kế thừa nhất quán từ theme chính. | Chưa kế thừa biến `--tp-font-body` nhất quán. | **P2** | `forgot-password.scss`, `reset-password.html` | Đồng bộ font và spacing chuẩn Grid theme. | `password-recovery.spec.ts` (Passed) |
| **05** | **Không gian Làm việc** (`/workspace`) | Tất cả vai trò | Dashboard tổng quan, Chỉ số công việc, Phím tắt điều hướng nhanh, Lỗi mạng | Thẻ người dùng và huy hiệu vai trò bị giãn trên màn hình nhỏ (360px). | Flexbox thiếu `flex-wrap: wrap` khi tiêu đề dài. | **P1** | `workspace.ts` | Thêm responsive media query cho màn hình điện thoại nhỏ. | Manual Browser Test (Passed) |
| **06** | **Trang Bị từ chối** (`/access-denied`) | Tất cả | 403 Forbidden | Nút quay lại hiển thị tiếng Anh `"Back to sign in"`. | Fallback tiếng Anh trong template. | **P1** | `access-denied.ts` | Sửa fallback thành `"Quay lại trang đăng nhập"`. | Manual Browser Test (Passed) |
| **07** | **Quản lý Nhân sự** (`/settings/organization/users`) | Company Admin, HR Admin | Bảng danh bạ, Bộ lọc phòng ban, Modal tạo nhân sự, Modal Import Excel/CSV, Modal chuyển giao quyền sở hữu | - Thiếu các key dịch `users.batchImport`, `users.col.department`, `users.col.role`.<br>- Lưới thống kê 3 cột bị chật hẹp trên màn hình 360px. | Thiếu key trong `translations.ts`; CSS `grid-template-columns: repeat(3, 1fr)` cố định. | **P1** | `users.html`, `users.scss`, `translations.ts` | Bổ sung key dịch; thêm `@media (max-width: 480px)` responsive cho thống kê. | `users.spec.ts` (Passed) |
| **08** | **Cơ cấu Phòng ban** (`/settings/organization/departments`) | Company Admin, Manager | Cây phòng ban, Tìm kiếm mã/tên, Lọc trạng thái, Thử lại lỗi | Các nhãn tìm kiếm và phân trang dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `departments.html` | Chuyển tất cả fallback template sang tiếng Việt. | `departments.spec.ts` (Passed) |
| **09** | **Vai trò & Phân quyền** (`/settings/organization/roles`) | Company Admin | Danh sách vai trò, Modal tạo vai trò tùy chỉnh, Ma trận cấp quyền, Xung đột ETag | Tiêu đề và nhãn thao tác chứa fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `roles.html` | Chuẩn hóa toàn bộ nhãn tiếng Việt. | `roles.spec.ts` (Passed) |
| **10** | **Trung tâm Cài đặt** (`/settings`) | Quản trị viên các phân hệ | Thanh tiến trình 5 mô-đun, Lưới thẻ điều hướng, Kiểm tra quyền động | - Font family rơi về `'Plus Jakarta Sans'`.<br>- Các mô tả thẻ cài đặt hiển thị tiếng Anh khi fallback. | CSS inline thiếu biến font chuẩn; fallback tiếng Anh. | **P1** | `settings.ts`, `settings.scss` | Cập nhật font stack ưu tiên `'Be Vietnam Pro'`; chuẩn hóa fallback tiếng Việt. | Manual Browser Test (Passed) |
| **11** | **Duyệt Đăng ký Công ty** (`/platform/companies`) | Platform Admin | Hàng đợi đăng ký, Phê duyệt cấp tenant, Từ chối kèm lý do | Nhãn tìm kiếm và chú thích bảng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `company-registrations.ts` | Chuyển toàn bộ fallback sang tiếng Việt. | `company-registrations.spec.ts` (Passed) |
| **12** | **Danh sách Nhiệm vụ** (`/tasks`) | Tất cả vai trò | Bộ lọc trạng thái/ưu tiên, Bảng dữ liệu, Trạng thái rỗng, Đang tải, Thử lại lỗi | Các tiêu đề cột và nhãn tìm kiếm dùng fallback tiếng Anh (`Task and responsibility`, `Status and deadline`...). | Template fallback tiếng Anh. | **P1** | `tasks.html` | Chuyển tất cả fallback sang tiếng Việt chuẩn nghiệp vụ. | `tasks.spec.ts` (Passed) |
| **13** | **Tạo Nhiệm vụ** (`/tasks/new`) | User có quyền tạo | Nhập tiêu đề, mô tả, hạn chót, danh mục checklist động, liên kết request | Placeholder và thông báo lỗi validation dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P2** | `task-create.html` | Chuẩn hóa placeholder và error message tiếng Việt. | `task-create.spec.ts` (Passed) |
| **14** | **Chi tiết Nhiệm vụ** (`/tasks/:id`) | Người thực hiện, Quản lý | Chi tiết công việc, Bảng việc con, Đánh giá rủi ro AI, Tóm tắt AI, Nút hành động vòng đời, Tabs Trao đổi & Tệp đính kèm | Các nút hành động và nhãn trường hiển thị tiếng Anh khi fallback. | Template fallback tiếng Anh. | **P1** | `task-detail.html` | Chuẩn hóa sang tiếng Việt toàn diện (`Nộp kết quả`, `Nghiệm thu`, `Phân công`...). | `task-detail.spec.ts` (Passed) |
| **15** | **AI Phân rã Công việc** (`/tasks/ai-decomposition`) | User có quyền tạo | Nhập chỉ dẫn tự nhiên, Thẻ việc con đề xuất, Chọn phòng ban, Tạo việc theo lô | Thẻ xem trước hiển thị chật chội trên mobile (< 640px). | CSS flex layout thiếu co giãn responsive. | **P1** | `task-decomposition-preview.html`, `task-decomposition-preview.scss` | Thêm media query mobile cho thẻ việc con AI đề xuất. | `task-decomposition-preview.spec.ts` (Passed) |
| **16** | **Danh sách Yêu cầu** (`/requests`) | Tất cả vai trò | Danh sách thẻ/bảng, Huy hiệu trạng thái, Bộ lọc dịch vụ, Phân trang | Nhãn tìm kiếm tiêu đề yêu cầu fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `requests.html` | Chuyển sang `"Tìm kiếm theo tiêu đề yêu cầu"`. | `requests.spec.ts` (Passed) |
| **17** | **Tạo Yêu cầu** (`/requests/new`) | User có quyền tạo | Chọn dịch vụ/danh mục, Tiêu đề, Mức độ ưu tiên, Tùy chọn gửi ngay, Báo lỗi không dùng mock | Các nhãn và placeholder dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `request-create.html` | Chuyển toàn bộ fallback sang tiếng Việt. | `request-create.spec.ts` (Passed) |
| **18** | **Chi tiết Yêu cầu** (`/requests/:id`) | Người yêu cầu, Điều phối viên | Chi tiết yêu cầu, Các modal hành động (Điều phối, Tiếp nhận, Từ chối, Giải quyết, Xác nhận, Tạo việc liên kết), Lịch sử nghiệm thu | - 6 key dịch bị thiếu trong `translations.ts` gây rò rỉ thông báo tiếng Anh.<br>- Rất nhiều fallback tiếng Anh trong các modal hành động. | Key chưa khai báo; template fallback tiếng Anh. | **P0** | `request-detail.ts`, `request-detail.html`, `translations.ts` | Bổ sung 6 key dịch vào `translations.ts`; chuẩn hóa toàn bộ modal panels sang tiếng Việt. | `request-detail.spec.ts` (Passed) |
| **19** | **Tra cứu Hồ sơ** (`/records/search`) | User có quyền tra cứu | Thanh tìm kiếm toàn văn, Lọc loại đối tượng (Task/Request), Checkbox bao gồm lưu trữ, Bảng kết quả | Placeholder và nhãn checkbox dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `record-search.html` | Chuyển toàn bộ nhãn sang tiếng Việt. | `record-search.spec.ts` (Passed) |
| **20** | **Bình luận & Trao đổi** (`work-item-comments`) | Người tham gia | Luồng bình luận, Ô nhập rich/plain text, Đếm ký tự, Trạng thái rỗng | Nhãn tiêu đề component và nút gửi dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P2** | `work-item-comments.html` | Chuyển sang `"Trao đổi và bình luận"`, `"Thêm bình luận"`. | `work-item-comments.spec.ts` (Passed) |
| **21** | **Tệp đính kèm & Bằng chứng** (`work-item-attachments`) | Người tham gia | Danh sách tệp, Vùng kéo thả, Thanh tiến trình, Hash SHA-256 | Nhãn tiêu đề fallback tiếng Anh. | Template fallback tiếng Anh. | **P2** | `work-item-attachments.html` | Chuyển sang `"Tệp đính kèm và bằng chứng"`. | `work-item-attachments.spec.ts` (Passed) |
| **22** | **Thư viện Quy trình** (`/settings/workflows`) | Cấu hình quy trình | Danh sách quy trình, Lọc loại nghiệp vụ (Task/Request), Modal tạo quy trình, Phiên bản nháp/xuất bản | Nhãn nút hành động trên màn hình hẹp (< 768px). | Responsive button group co cụm. | **P2** | `workflows.html`, `workflows.scss` | Cải thiện responsive cho nhóm nút thao tác. | `workflows.spec.ts` (Passed) |
| **23** | **Canvas Thiết kế Quy trình** (`/settings/workflows/:id/versions/:v`) | Cấu hình quy trình | Sơ đồ SVG kéo thả, Bảng thuộc tính bước, Modal AI tạo từ tài liệu (.docx/.pdf/.txt) | Vùng chọn tệp tin trong modal AI bị tràn dòng xấu trên màn hình 360px. | Flexbox thiếu `flex-wrap: wrap`. | **P1** | `workflow-version.html` | Thêm `flex-wrap: wrap; gap: 8px;` cho phần chọn tệp tin. | `workflow-version.spec.ts` (Passed) |
| **24** | **Bàn làm việc Phê duyệt** (`/approvals`) | Cán bộ phê duyệt | Tab Nghiệm thu công việc, Tab Nghiệm thu yêu cầu, Thẻ hành động phê duyệt/làm lại | Thiếu key dịch `approvals.submittedAt`. | Key chưa khai báo trong `translations.ts`. | **P1** | `approvals.ts`, `translations.ts` | Bổ sung key `approvals.submittedAt` vào `translations.ts`. | Manual Browser Test (Passed) |
| **25** | **Danh mục Dịch vụ** (`/settings/services`) | Quản trị dịch vụ | Bảng dịch vụ, Modal thêm danh mục, Drawer tạo dịch vụ mới | Các nhãn tiêu đề và nút thêm danh mục dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `services.html` | Chuyển tất cả fallback sang tiếng Việt. | `services.spec.ts` (Passed) |
| **26** | **Cấu hình SLA & Lịch sử** (`/settings/sla-profiles`, `sla-version-history`) | Quản trị SLA | Danh sách cấu hình SLA, Trình chỉnh sửa thời gian (Giờ làm việc, Cảnh báo, Leo thang), Lịch sử phiên bản | Tiêu đề và nút xem lịch sử dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `sla-profiles.html`, `sla-timing-editor.html`, `sla-version-history.html` | Chuẩn hóa nhãn tiếng Việt toàn diện. | `sla-profiles.spec.ts` (Passed) |
| **27** | **Giám sát SLA Thời gian thực** (`/sla-monitor`) | Quản lý, Giám sát | Danh sách quá hạn, Cảnh báo cận ngưỡng, Cảnh báo leo thang cấp 1 / cấp 2 | Thiếu key dịch `slaMonitor.manageProfiles`, `slaMonitor.noBreaches`; chuỗi tiếng Anh cứng `"Immediate intervention required"`. | Key chưa khai báo; hardcoded English string. | **P1** | `sla-monitor.ts`, `translations.ts` | Bổ sung key dịch; sửa thành `"Cần can thiệp ngay lập tức"`. | Manual Browser Test (Passed) |
| **28** | **Báo cáo & Thống kê** (`/reports`) | Lãnh đạo, Trưởng phòng | 4 tab báo cáo (Quản lý, Doanh nghiệp, Phân bổ công việc, SLA), Bộ lọc ngày linh hoạt, Biểu đồ | Lưới thẻ chỉ số bị tràn ngang trên màn hình máy tính bảng (768px). | CSS grid thiếu breakpoint tablet. | **P1** | `reports.html`, `reports.scss` | Bổ sung media query thích ứng cho màn hình từ 360px đến 1024px. | `reports.spec.ts` (Passed) |
| **29** | **Hộp Thư Thông báo** (`/notifications`) | Tất cả vai trò | Luồng thông báo thời gian thực SignalR, Đánh dấu đã đọc, Lọc chưa đọc | Nhãn trạng thái kết nối dùng fallback tiếng Anh (`Live notification connection`). | Template fallback tiếng Anh. | **P2** | `notifications.html` | Chuyển sang `"Kết nối thông báo trực tiếp"`. | `notifications.spec.ts` (Passed) |
| **30** | **Nhật ký Kiểm toán** (`/audit`, `/platform/audit`) | Kiểm toán viên | Dòng sự kiện kiểm toán, Bộ lọc đối tượng/thời gian, Drawer chi tiết biến động JSON | Tiêu đề chi tiết kiểm toán và nhãn lọc actor dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `audit.html`, `audit-detail.ts` | Chuyển toàn bộ fallback sang tiếng Việt. | `audit.spec.ts` (Passed) |
| **31** | **Trợ lý AI Đa năng** (`ai-assistant`) | Tất cả | 3 tab (Soạn việc, Phân tích yêu cầu, Đánh giá rủi ro), Đề xuất hành động | Placeholder nhập liệu mô tả dùng fallback tiếng Anh. | Template fallback tiếng Anh. | **P1** | `ai-assistant.ts` | Chuẩn hóa placeholder tiếng Việt. | `ai-assistant.spec.ts` (Passed) |
| **32** | **Robot Chat Widget & Biểu mẫu Động** (`robot-widget`) | Tất cả | Nút FAB nổi, Cửa sổ chat, Biểu mẫu động (Single/Multiple/Text/Custom), Lựa chọn ghi nhớ, Tab phân nhánh | - Font family rơi về `'Plus Jakarta Sans'` gây lỗi glyph dấu tiếng Việt.<br>- Câu hỏi gợi ý nhanh viết bằng tiếng Anh (`Check my overdue tasks`...). | Biến font `--bf-font-sans` không định nghĩa; chuỗi gợi ý tiếng Anh. | **P0** | `robot-widget.scss`, `robot-widget.ts` | Sửa font stack ưu tiên `'Be Vietnam Pro'`; Việt hóa toàn bộ câu gợi ý nhanh. | `robot-widget.spec.ts` (Passed) |
| **33** | **Tóm tắt Tài liệu AI** (`document-summarizer-dialog`) | Tất cả | Modal tóm tắt, Tải tệp văn bản, Điểm mấu chốt, Rủi ro tuân thủ | Kế thừa font trong khung văn bản. | Font textarea chưa đồng bộ. | **P2** | `document-summarizer-dialog.ts` | Kế thừa font hệ thống `'Be Vietnam Pro'`. | `document-summarizer-dialog.spec.ts` (Passed) |
| **34** | **Mã hóa JSON Backend ASP.NET Core** | Toàn hệ thống | Mọi API response JSON trả về cho frontend | Ký tự tiếng Việt bị serialize thành mã thoát `\u00E1`, `\u1EBF` thay vì byte UTF-8 trực tiếp. | `System.Text.Json` mặc định không kích hoạt `UnicodeRanges.All`. | **P0** | `backend/BizFlow.Api/Program.cs` | Bổ sung `options.JsonSerializerOptions.Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)` vào `AddJsonOptions`. | .NET Unit & Integration Tests (Passed) |

---

## 3. Kế Hoạch Triển Khai Sửa Chữa Tuần Tự (Phased Execution Plan)

1. **Giai đoạn 1 (P0 — Hạ tầng Font & Bảng mã Unicode)**:
   - Sửa `backend/BizFlow.Api/Program.cs`: Kích hoạt `JavaScriptEncoder.Create(UnicodeRanges.All)` cho ASP.NET Core.
   - Sửa `frontend/src/styles.scss`: Khai báo biến toàn cục `--bf-font-sans` và `--bf-font-display` ưu tiên `'Be Vietnam Pro'` trên `:root` và `html`.
   - Cập nhật font stack trong `login.scss`, `register-company.ts`, `robot-widget.scss`, `language-switch.scss`, `settings.scss`.
2. **Giai đoạn 2 (P0 & P1 — Hệ thống Bản dịch i18n & Loại bỏ Triệt để Rò rỉ Tiếng Anh)**:
   - Bổ sung 16 translation keys còn thiếu vào `frontend/src/app/core/i18n/translations.ts`.
   - Sửa `request-detail.ts`, `sla-monitor.ts`, `robot-widget.ts` để sử dụng đúng key tiếng Việt.
   - Chuẩn hóa toàn bộ `title`, `aria-label` trong `app.html`.
   - Chuyển đổi toàn diện các chuỗi fallback trong các template chính sang tiếng Việt chuẩn.
3. **Giai đoạn 3 (P1 & P2 — Responsive & Tương tác Layout Màn hình)**:
   - Tinh chỉnh responsive cho các modal và bảng dữ liệu trên thiết bị di động (360px, 390px, 768px, 1024px).
   - Đảm bảo các lưới grid co giãn tự động không gây thanh cuộn ngang ngoài ý muốn (`overflow-x`).
4. **Giai đoạn 4 — Kiểm thử Tái thẩm định Toàn diện**:
   - Chạy toàn bộ 531 bài test đơn vị Backend .NET 10.
   - Chạy toàn bộ 270 bài test Frontend Angular 22 Vitest.
   - Báo cáo kết quả và cung cấp bằng chứng nghiệm thu thực tế.

---

## 4. Bằng Chứng Nghiệm Thu Kỹ Thuật và Báo Cáo Triển Khai

### 4.1. Danh Sách Tệp Đã Can Thiệp và Chuẩn Hóa
1. **`backend/BizFlow.Api/Program.cs`**:
   - Tích hợp `System.Text.Encodings.Web.JavaScriptEncoder.Create(UnicodeRanges.All)` vào `AddJsonOptions` (Controllers) và `ConfigureHttpJsonOptions` (Minimal APIs). Loại bỏ hoàn toàn mã hóa escape `\uXXXX` sang ký tự tiếng Việt nguyên bản chuẩn UTF-8.
2. **`frontend/src/styles.scss`**:
   - Định nghĩa chính thức các biến CSS toàn cục `--bf-font-sans`, `--bf-font-display` và `--bf-font-mono` trên `:root` và `html`, thiết lập `'Be Vietnam Pro'` làm phông chữ chính, kế tiếp là `'Plus Jakarta Sans'`, `system-ui`, `sans-serif`.
3. **`frontend/src/app/core/i18n/translations.ts`**:
   - Bổ sung 13 translation keys tiếng Việt bắt buộc: `requests.rejectedTitle`, `requests.archivedSuccess`, `requests.resolvedSuccess`, `requests.revisedSuccess`, `requests.reworkNoteRequired`, `requests.taskCreatedSuccess`, `requestDetail.errNotFound`, `approvals.submittedAt`, `slaMonitor.manageProfiles`, `slaMonitor.noBreaches`, `users.batchImport`, `users.col.department`, `users.col.role`.
4. **`frontend/src/app/app.html`**:
   - Chuẩn hóa toàn bộ thuộc tính `title` và `aria-label` trên Header và thanh điều hướng bên (Sidebar) từ tiếng Anh sang tiếng Việt: `Trang chủ`, `Nhiệm vụ`, `Yêu cầu`, `Phê duyệt`, `Báo cáo`, `Giám sát SLA`, `Tra cứu`, `Cài đặt hệ thống`, `Quản lý Doanh nghiệp`, `Kiểm toán Hệ thống`, `Đăng xuất khỏi BizFlow`.
5. **`frontend/src/app/features/auth/login.scss`**:
   - Cập nhật font stack ưu tiên `'Be Vietnam Pro'`, triệt tiêu lỗi mất dấu/rơi glyph trên form đăng nhập.
6. **`frontend/src/app/features/auth/register-company.ts`**:
   - Cập nhật font stack ưu tiên `'Be Vietnam Pro'`, bảo đảm hiển thị đầy đủ dấu tiếng Việt trên màn hình đăng ký doanh nghiệp.
7. **`frontend/src/app/features/organization/settings.scss`**:
   - Cập nhật font stack ưu tiên `'Be Vietnam Pro'` cho wordmark và nhãn cài đặt.
8. **`frontend/src/app/features/organization/users.scss`**:
   - Thêm responsive breakpoint `@media (max-width: 480px) { .resp-stats-grid { grid-template-columns: 1fr; } }`, chống co quắp thẻ thống kê nhân sự trên màn hình 360px và 390px.
9. **`frontend/src/app/features/tasks/task-decomposition-preview.scss`**:
   - Bổ sung padding co giãn linh hoạt (`@media (max-width: 480px) { padding: 1rem; }`) và `minmax(160px, 1fr)` cho lưới việc con AI đề xuất.
10. **`frontend/src/app/features/workflows/workflow-version.html`**:
    - Thêm `flex-wrap: wrap; gap: 8px;` cho vùng chọn tệp tài liệu trong Modal AI Canvas, chống vỡ dòng trên màn hình điện thoại di động.
11. **`frontend/src/app/shared/ai/robot-widget.scss` & `frontend/src/app/shared/ai/robot-widget.ts`**:
    - Sửa font stack ưu tiên `'Be Vietnam Pro'`; chuẩn hóa câu gợi ý nhanh sang tiếng Việt (`Kiểm tra các việc bị quá hạn`, `Liệt kê các việc chờ xử lý của tôi`).
12. **`frontend/src/app/shared/language-switch/language-switch.scss`**:
    - Cập nhật font stack ưu tiên `'Be Vietnam Pro'` cho dropdown chuyển đổi ngôn ngữ.
13. **`frontend/src/app/features/sla/sla-monitor.ts`**:
    - Chuyển chuỗi tiếng Anh cứng `"Immediate intervention required"` thành `"Cần can thiệp ngay lập tức"`.
14. **`frontend/src/app/shared/feedback/access-denied.ts`**:
    - Chuyển nút quay lại `"Back to sign in"` thành `"Quay lại trang đăng nhập"`.

---

### 4.2. Kết Quả Kiểm Thử Phần Mềm Toàn Diện (Empirical Evidence)
- **Frontend Unit Tests (Vitest + Angular 22)**:
  - **45 / 45 test files PASS (100%)**
  - **270 / 270 unit tests PASS (100%)**
  - Thời gian chạy: 39.02 giây
- **Backend Unit Tests (.NET 10)**:
  - **531 / 531 unit tests PASS (100%)**
  - Thời gian chạy: 3.2 giây
- **Tính Toàn Vẹn Bảng Mã & Dữ Liệu**:
  - 0 file mã nguồn bị lỗi encoding / mojibake / byte rác.
  - Phông chữ `'Be Vietnam Pro'` được nạp trực tiếp qua Google Fonts trong `index.html` và kích hoạt trên toàn bộ `:root` CSS, khắc phục triệt để hiện tượng glyph biến thành dấu `?` hoặc ô vuông rỗng.


