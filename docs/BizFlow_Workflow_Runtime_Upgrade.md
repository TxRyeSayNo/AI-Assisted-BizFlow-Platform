# Nâng cấp vận hành workflow — 25/11/2026

## Phạm vi đã triển khai

Đợt này bổ sung bộ thực thi quy trình cho **yêu cầu dịch vụ**. Quy trình nhiệm vụ và các hồ sơ không gắn workflow tiếp tục dùng cơ chế hiện có.

| Hạng mục | Hành vi hiện tại |
| --- | --- |
| Tiến trình bền vững | Gửi yêu cầu khởi tạo `WorkflowInstance` trong cùng transaction; ghim phiên bản, dữ liệu biểu mẫu và danh sách người xử lý. Tải lại trang hoặc khởi động lại API không làm mất tiến trình. |
| Biểu mẫu theo dịch vụ | Cấu hình trường văn bản, số, ngày, lựa chọn trong bản nháp workflow. Kiểm tra bắt buộc, giới hạn và danh sách giá trị ở cả giao diện và API. Trường định tuyến hệ thống không được ghi đè bằng biểu mẫu. |
| Phê duyệt | Bước xử lý, phê duyệt, xác nhận; chính sách SINGLE/ANY/ALL; chỉ định người, vai trò, hàng đợi phòng ban, người đề nghị hoặc quản lý theo phạm vi quản lý. Người đề nghị không được tự phê duyệt ở bước APPROVAL. |
| Nhánh song song | Cổng tách mở nhiều bước độc lập; cổng gộp chỉ mở bước sau khi đủ nhánh. Validator chặn đồ thị nằm ngoài hợp đồng hỗ trợ. |
| Thời hạn | Lưu hạn từng bước; tùy chọn lịch làm việc, múi giờ và ngày nghỉ khi tính hạn. Không chọn lịch thì tính theo giờ liên tục. |
| Giao diện | Biểu mẫu dịch vụ ở trang tạo yêu cầu; tiến độ và quyết định ở trang chi tiết; hộp việc workflow ở trang phê duyệt. Hồ sơ dùng workflow không hiển thị các nút đổi trạng thái theo cơ chế cũ. |
| Báo cáo | `/reports/workflows`: số bước chờ, hoàn tất, quá hạn, thời gian trung bình và P90 theo phiên bản/bước, trong phạm vi hồ sơ được phép đọc. Thời gian trung bình/P90 là phút thực tế, không phải phút làm việc. |
| AI | Agent hội thoại đọc `workflow_status`/`workflow_inbox` theo quyền, bao gồm kết quả AI trong tiến trình. Bước DEPARTMENT có thể bật Agent REVIEW hoặc AUTO để chuẩn bị bàn giao hoặc tự hoàn tất xử lý nội bộ; không tự phê duyệt/xác nhận thay con người. |
| Thông báo | Tạo thông báo trong ứng dụng cho người nhận bước; phát cập nhật sau commit. Worker nhắc hạn bước và báo quản lý khi quá hạn. Thông báo mới có hàng đợi email dùng chung SMTP với reset mật khẩu; nhắc hạn nhiệm vụ giữ pipeline nhiều kênh hiện có. Web Push nhắc hạn nhiệm vụ có transport mã hóa và nút đăng ký/tắt theo thiết bị. |
| Ủy quyền | Quản trị quy trình chỉ định người thay cho một vị trí chưa quyết định của bước đang chờ, có lý do, hạn tối đa 90 ngày và thu hồi. ALL đếm theo vị trí người gốc; một người không được chiếm hai vị trí. |
| Hạn mức | APPROVAL hỗ trợ ma trận theo trường số bắt buộc trong biểu mẫu. Các tầng tăng dần, cận trên bao gồm giá trị bằng; tầng cuối không giới hạn. Mỗi tầng chọn vai trò và SINGLE/ANY/ALL, dựa trên dữ liệu đã ghim của phiên thực thi. |
| Tạm dừng SLA | Người xử lý chưa quyết định hoặc người có quyền cấu hình quy trình có thể tạm dừng/tiếp tục với lý do. Đóng băng tính quá hạn và chặn quyết định trong lúc dừng. Tiếp tục dịch hạn theo thời gian làm việc của lịch đã chụp, giữ phần trễ trước khi dừng. |
| Bảo vệ dữ liệu | Khóa dòng yêu cầu và revision chống xử lý trùng; quyết định/lịch sử được giữ lại; kiểm tra tenant ở lớp ứng dụng, persistence và trigger PostgreSQL. Kiểm tra quyền báo cáo trước khi đọc cache. |

## Cách nghiệm thu nghiệp vụ

1. Tạo bản nháp workflow loại REQUEST, khai báo biểu mẫu và các bước. Chọn người/vai trò hợp lệ, thêm hạn và lịch nếu cần. Phát hành rồi gắn vào dịch vụ.
2. Dùng tài khoản người đề nghị tạo yêu cầu thuộc dịch vụ, nhập biểu mẫu và gửi ngay. Thiếu trường hoặc không xác định được người xử lý phải từ chối toàn bộ transaction.
3. Mở chi tiết yêu cầu: phải thấy phiên bản, dữ liệu biểu mẫu, bước chờ, người phụ trách và hạn xử lý. Người đề nghị không có nút duyệt bước APPROVAL của chính mình.
4. Đăng nhập người phụ trách, mở hộp việc phê duyệt, xử lý bước. Với ALL, người thứ nhất xử lý xong vẫn chờ các người còn lại. Hai lần gửi cùng revision chỉ một lần được chấp nhận.
5. Với nhánh song song, hoàn tất một nhánh chưa mở bước sau cổng gộp. Hoàn tất đủ các nhánh mới mở bước tiếp theo. Tải lại trang giữa các bước phải giữ kết quả.
6. Xác nhận bước cuối: yêu cầu đóng và lịch sử quyết định còn nguyên. Từ chối không có đường REJECT kết thúc yêu cầu ở trạng thái bị từ chối; có thể tạo yêu cầu sửa đổi, giữ liên kết và dữ liệu biểu mẫu cũ.
7. Kiểm tra báo cáo bằng quản trị viên và người không có quyền. Tài khoản tenant khác không được đọc hoặc quyết định trên cùng mã hồ sơ.
8. Thử ma trận với trường `budget` bắt buộc: tầng đầu `upperLimit=1000`, vai trò A/ANY; tầng cuối `upperLimit=null`, vai trò B/ALL. Giá trị 1000 phải vào tầng đầu, 1001 vào tầng cuối. Đơn vị và mức duyệt do doanh nghiệp quy định, các con số này chỉ dùng nghiệm thu.
9. Ở bước ALL còn một vị trí chưa duyệt, quản trị viên ủy quyền sang người ngoài danh sách gốc, ghi lý do và hạn. Người gốc mất quyền quyết định trong thời gian đại diện; người thay quyết định đúng một vị trí. Kiểm tra thu hồi/hết hạn khôi phục người gốc nếu họ còn đủ điều kiện, không xóa lịch sử.
10. Tạm dừng một bước vì chờ chứng từ: không được duyệt trong lúc dừng, worker không gửi nhắc. Tiếp tục sau 10 phút làm việc phải dịch hạn thêm 10 phút; nếu bước đã trễ trước khi dừng thì vẫn giữ phần trễ đó. Kiểm tra lịch sử người dừng/người tiếp tục và lý do trong chi tiết yêu cầu.
11. Khi có cấu hình VAPID hợp lệ ở môi trường thử HTTPS, người dùng bật thông báo trong trang Thông báo. Kiểm tra trên thiết bị thật việc xin quyền, nhận nhắc chung, mở hồ sơ qua màn hình đăng nhập và tắt đăng ký. Bước này cần nghiệm thu triển khai riêng; kiểm thử tự động hiện dùng push service giả lập.

## Điều kiện triển khai

Không tự động chuyển đổi yêu cầu cũ sang runtime. Dịch vụ chưa gắn phiên bản phát hành vẫn dùng luồng hiện có; áp dụng thử bằng cách gắn workflow cho một dịch vụ trước, rồi tạo/gửi yêu cầu mới.

Migration mới theo thứ tự: `20261124174442_WorkflowRuntimeFoundation`, `20261124191912_WorkflowStepDelegation`, `20261124193438_WorkflowSlaPauseHistory`, `20261124202000_NotificationEmailOutbox`, `20261124222000_CompanyRegistrationEmail`, `20261125010000_NotificationPreferences`, `20261125020000_WorkflowAgentRuns`. Phải triển khai migration trước khi chạy phiên bản API này. Chúng bổ sung runtime, dữ liệu biểu mẫu, quyết định theo vị trí gốc, ủy quyền, bằng chứng tạm dừng SLA, hàng đợi email, tùy chọn thông báo và hàng đợi/bằng chứng AI; có khóa ngoại, chỉ mục và trigger. Migration delegation backfill quyết định cũ từ ActorId. Không hạ migration khi đã có lịch sử cần giữ; sao lưu và thử trên bản sao dữ liệu trước khi phát hành. Hạ migration email/AI sẽ xóa trạng thái gửi/chờ gửi, bằng chứng AI và thông tin sử dụng link bổ sung hồ sơ.

Lệnh áp dụng theo cơ chế EF của repository, với kết nối do môi trường triển khai cung cấp:

```powershell
dotnet ef database update --project backend/BizFlow.Infrastructure --startup-project backend/BizFlow.Infrastructure --configuration Release
```

Không đưa mật khẩu vào lệnh hoặc source control. Trong phiên làm việc này chưa chạy cập nhật trên cơ sở dữ liệu phát triển/sản xuất và chưa đổi `deploy/.env`.

Email thông báo mặc định kế thừa toàn bộ section `ResetEmail` đang dùng cho reset mật khẩu: `Enabled`, `Host`, `Port`, `SslOnConnect`, `Username`, `Password`, `FromName`, `FromAddress`, `FrontendOrigin`. Nếu mail reset đã hoạt động, không cần khai báo lại thông tin SMTP. Section `NotificationEmail` chỉ ghi đè các khóa được khai báo; `NotificationEmail__Enabled=false` tắt riêng mail thông báo và vẫn giữ mail reset. Docker Compose đã chuyển tiếp tùy chọn này; `.env.example` có hướng dẫn. Không sửa file `.env` thật trong phiên làm việc này.

Sau khi áp dụng migration và khởi động lại API, `NotificationEmailWorker` chạy độc lập với `Features:BackgroundJobs`, bắt đầu sau 10 giây và quét mỗi 30 giây, tối đa 20 thư/tenant/lượt. Khi một `Notification` mới được lưu, trigger tạo bản ghi `NotificationEmailDelivery` trong cùng transaction; rollback nghiệp vụ cũng rollback hàng đợi. Worker lấy người nhận đang hoạt động đúng tenant, gửi tiêu đề/nội dung dạng văn bản và link đến yêu cầu/nhiệm vụ/hộp thông báo qua `ResetEmail__FrontendOrigin`. Chỉ những sự kiện đã tạo thông báo trong ứng dụng mới đi vào hàng đợi này; chưa tự tạo thêm thông báo cho mọi chuyển trạng thái workflow.

Không gửi lại thông báo tồn tại trước migration. Thông báo chờ quá 24 giờ hoặc tài khoản không còn hoạt động được đánh dấu `SKIPPED`. Lỗi SMTP thử tối đa 5 lần, giãn lần lượt 1/5/15/60 phút; hết lượt chuyển `FAILED`, không làm thất bại thao tác nghiệp vụ. Gửi thành công ghi `SENT` cùng thời điểm gửi. Có khóa `FOR UPDATE SKIP LOCKED` chống hai worker gửi đồng thời và Message-ID ổn định theo thông báo. SMTP có thể đã nhận thư nhưng tiến trình dừng trước commit; lần thử lại vẫn có thể tạo thư trùng (giới hạn thông thường của SMTP, không bảo đảm exactly-once).

Thông báo có khóa `reminder:` không vào hàng đợi mới, vì nhắc hạn nhiệm vụ đã gửi qua pipeline in-app → push → email; phần gửi email đó cũng kế thừa cấu hình reset. Điều này tránh gửi thêm một mail từ worker mới cho cùng nhắc hạn. Nội dung mail có thể chứa thông tin nghiệp vụ; người nhận là địa chỉ hiện tại trên tài khoản được nhận thông báo. STARTTLS hoặc TLS bắt buộc ngoài SMTP loopback thử nghiệm; `AllowInsecureLoopback` chỉ dùng kiểm thử cục bộ. Không ghi mật khẩu SMTP hoặc nội dung lỗi nhà cung cấp vào log.

Worker `workflow-step-deadlines` chạy mỗi 5 phút khi bật `Features:BackgroundJobs` và `Features:DeadlineReminders`. Nhắc người xử lý đang có hiệu lực trước hạn 4 giờ/1 giờ hoặc sau hạn; báo người quản lý có quyền đọc đúng phạm vi sau `escalationAfterHours` (mặc định 24, cho phép 1–720 giờ) tính từ hạn. Có khóa dòng và khóa chống trùng theo bước/hạn/loại/người nhận. Bước đang dừng SLA không phát nhắc. Đây là thông báo trong ứng dụng và cảnh báo quản lý, chưa tự giao lại hay tự phê duyệt.

Web Push dùng `WebPush:Enabled`, `VapidPublicKey`, `VapidPrivateKey`, `Subject` (mailto hoặc HTTPS); cặp khóa P-256 dạng base64url phải khớp. Giữ khóa riêng trong secret môi trường. Chỉ cho HTTPS cổng chuẩn ở các nhà cung cấp push đã biết; `AllowedHosts` cho phép quản trị viên bổ sung hostname chính xác. HTTP không theo redirect. Đăng ký 404/410 được xóa đúng tài khoản; lỗi tạm thời giữ lại để lần nhắc sau thử tiếp. Payload chỉ có lời nhắc chung và đường dẫn mở ứng dụng, không gửi tiêu đề/nội dung nghiệp vụ ra màn hình khóa. Giao diện Thông báo chỉ xin quyền khi người dùng bấm bật; cần HTTPS và trình duyệt hỗ trợ. Service worker dùng scope riêng `/bizflow-push/`, không cache hay chặn request của ứng dụng. Không có cấu hình hợp lệ thì không công bố khóa mẫu và không báo gửi thành công.

Ủy quyền chỉ áp dụng một bước hiện tại, không phải lịch ủy quyền nhân sự cho mọi yêu cầu tương lai. Chỉ người có `workflows.configure` được cấp/thu hồi; không ủy quyền bước xác nhận, không ủy quyền phê duyệt về người đề nghị. Khi hết hạn, quyền quyết định trở lại người gốc nếu họ còn đủ điều kiện. Người đã từng được giao vẫn có quyền đọc hồ sơ theo bản ghi tham gia bất biến để đối chiếu lịch sử; thu hồi quyền quyết định không xóa dấu vết tham gia.

Thời gian trung bình/P90 vẫn là thời gian thực tế của bước, bao gồm thời gian tạm dừng; chỉ phép tính quá hạn đóng băng khi tạm dừng. Lịch nghỉ/giờ làm được chụp tại thời điểm dừng để thay đổi lịch sau đó không làm thay đổi cách dịch hạn của lần dừng đó.

## Hợp đồng API bổ sung

### AI Agent thực thi bước workflow

Agent được bật riêng trên từng bước **DEPARTMENT** của quy trình REQUEST, không thay loại nút cũ. Trong trình thiết kế, chọn bước xử lý rồi bật **AI Agent cho bước xử lý**, nhập mục tiêu/đầu ra và chọn các trường biểu mẫu AI được đọc. Cần giữ người/nhóm phụ trách hợp lệ làm đầu mối tiếp nhận khi AI không hoàn thành. Cấu hình đi qua lưu nháp, kiểm tra và phát hành như các thay đổi workflow khác; không tự sửa phiên bản đã ghim của hồ sơ đang chạy.

| Chế độ | Hành vi |
| --- | --- |
| REVIEW, mặc định | AI tạo kết quả bàn giao và bằng chứng; giữ bước PENDING để người phụ trách kiểm tra, sửa ghi chú và quyết định hoàn tất. |
| AUTO, bật rõ trong cấu hình | Khi kết quả đúng hợp đồng, có nguồn khớp và không yêu cầu kiểm tra thêm, hệ thống hoàn tất bước xử lý và chuyển tiếp theo sơ đồ đã ghim. Không ghi quyết định giả danh nhân viên. |
| Cần con người | Thiếu dữ kiện, AI yêu cầu kiểm tra, chưa cấu hình mô hình, hết retry, bước bị tạm dừng trong lúc chạy hoặc không thể chuyển tiếp: giữ bước để người phụ trách tiếp tục. |

Hợp đồng cấu hình trên một nút xử lý:

```json
{
  "agent": {
    "mode": "REVIEW",
    "instructions": "Tóm tắt nhu cầu và dự toán, lập bản bàn giao kèm thông tin cần người duyệt xác minh. Không coi dự toán là ngân sách đã được phê duyệt.",
    "inputFields": ["budget"],
    "includeDescription": false,
    "includePreviousResults": false
  }
}
```

- Chỉ cho phép DEPARTMENT/SINGLE. Không bật Agent trên APPROVAL/CONFIRMATION, quy trình TASK hoặc chính sách ALL/ANY. AUTO không được bỏ qua yêu cầu tệp đính kèm. Việc chuyển tới APPROVAL tiếp theo vẫn chờ người có thẩm quyền.
- Đầu vào chụp khi mở bước, gồm tiêu đề, tên bước, tối đa 20 trường được chọn; mô tả và tóm tắt tối đa 5 bước AI đã hoàn tất trước đó chỉ có khi bật rõ. Không gửi toàn bộ danh bạ hoặc hồ sơ tenant cho mô hình. Giới hạn snapshot là 32000 ký tự; cần giảm trường đọc nếu vượt giới hạn. Mỗi bản chạy có fingerprint SHA-256 và ghim với bước/phiên bản/hồ sơ.
- Agent gọi công cụ trong vòng lặp tối đa 4 lượt: `read_inputs`, `validate_inputs`, `read_previous_results` khi được bật, rồi tạo kết quả JSON. Không có công cụ mạng, shell, gửi mail, sửa ngân sách, tự phân công hoặc phê duyệt trong worker này. Kết quả gồm tóm tắt, nội dung bàn giao, phát hiện, cờ cần con người và trích dẫn. Mỗi trích dẫn phải khớp nguyên văn với nguồn được phép; thiếu cờ kiểm tra hoặc có thuộc tính ngoài hợp đồng sẽ bị từ chối.
- Kiểm tra trích dẫn xác nhận nguồn tồn tại, không chứng minh mọi suy luận của mô hình là đúng. Dùng REVIEW để nghiệm thu mục tiêu mới; AUTO phù hợp với soạn thảo/tóm tắt/chuẩn hóa nội bộ có thể kiểm chứng. Chưa đọc được nội dung tệp, kiểm tra tồn kho/ngân sách thực tế hoặc xác minh với hệ thống ngoài.
- `WorkflowAgentRun` được xếp hàng trong cùng transaction mở bước. Worker riêng chạy sau khi API khởi động khoảng 10 giây, quét mỗi 15 giây sau khi hoàn tất vòng trước; không phụ thuộc Hangfire. Mỗi vòng chọn tối đa 100 tenant có việc đến hạn, ưu tiên thời điểm chờ cũ, một việc mỗi tenant. Mặc định chạy song song tối đa 2 tenant/process, có thể cấu hình 1–8.
- Claim dùng khóa dòng SKIP LOCKED, lease 3 phút; một lần xử lý có ngân sách 120 giây. Lease hết hạn có thể được worker khác phục hồi. Lỗi thử lại sau 1/5 phút, tối đa 3 lần trong một đợt; người có quyền xử lý bước hoặc cấu hình workflow có thể chạy lại với lý do và revision, tổng tối đa 9 lần/bước. Sau 20 bước AI trong cùng phiên thực thi, bước AI mới chuyển sang người phụ trách để chặn chu trình tự chạy kéo dài; chỉ chạy lại khi có chỉ định của người phụ trách.
- Trước khi áp dụng, worker khóa hồ sơ, kiểm tra lại lease, tenant/công ty đang hoạt động, trạng thái bước và người phụ trách còn hợp lệ. Người dùng vẫn có thể hoàn tất thủ công khi AI chạy; kết quả AI đến muộn chỉ lưu lịch sử với `AI.STALE_LEASE`, không thay đổi bước. Tạm dừng trước khi worker claim không gọi mô hình; tạm dừng trong lúc chạy giữ kết quả để người phụ trách kiểm tra.
- Lịch sử lần chạy được ghi nối tiếp, có công cụ đã đọc, kết quả được tiếp nhận, mô hình và token/độ trễ do nhà cung cấp trả về. Input và chủ sở hữu không được sửa; trigger chặn chuyển tenant, xóa/sửa lịch sử. Worker bị dừng đột ngột trước khi trả kết quả có thể để lại lần chạy chưa có bản ghi hoàn tất; bộ đếm và lease vẫn cho phép nhận biết/phục hồi. Audit dùng `AI_AGENT`, không giả danh người phê duyệt. HTTP context thông thường không được ghi audit này.
- Trang chi tiết yêu cầu hiển thị trạng thái Agent, kết quả gần nhất, trích dẫn và lịch sử; có chạy lại kèm lý do. Người được xem hồ sơ mới đọc được lịch sử; API không nhận tenant/user từ body. Agent hội thoại đọc kết quả này qua `workflow_status` và phân biệt kết quả AI với quyết định con người.
- Hoàn tất, cần kiểm tra hoặc hết retry sẽ tạo thông báo cho người phụ trách đang hoạt động; mail theo tùy chọn đã lưu và SMTP hiện có. Chạy lại không tạo mail trực tiếp ngoài outbox.
- Bộ giám sát chủ động đã được sửa để scanner và store dùng context tenant riêng khi không có HTTP claims, kiểm tra công ty còn hoạt động và khóa advisory theo tenant chống quét trùng giữa các replica. Phần phát hiện gần hạn/quá hạn và cân bằng tải vẫn là quy tắc nghiệp vụ tạo khuyến nghị, không phải mô hình tự quyết định phân công.

| API | Quyền/phạm vi |
| --- | --- |
| `GET /api/v1/requests/{id}/workflow` | Thêm thuộc tính `agent` trên mỗi bước: trạng thái, số lần chạy, kết quả, lỗi và mô hình; cùng quyền đọc hồ sơ. |
| `GET /api/v1/requests/{id}/workflow/steps/{stepId}/agent/history` | Lịch sử hoàn tất của tối đa 9 lần chạy, cùng quyền đọc tiến trình. |
| `POST /api/v1/requests/{id}/workflow/steps/{stepId}/agent/retry` | `{ "revision": 4, "reason": "Dịch vụ đã phục hồi" }`; người xử lý hiện tại hoặc người có `workflows.configure`; không chạy lại bước đã đóng/tạm dừng hoặc vượt ngân sách. |

Triển khai migration `20261125020000_WorkflowAgentRuns` trước API mới. Dùng cấu hình OpenAI hiện có; không cần API key riêng cho Agent workflow. `AI__WorkflowAgent__Enabled=false` tắt thực thi nền nhưng vẫn cho xử lý thủ công; mặc định true chỉ có tác dụng khi workflow đã bật Agent. `AI__WorkflowAgent__MaxConcurrency=2` giới hạn song song/process. Docker overlay đã truyền các cấu hình OpenAI và Agent từ môi trường; `.env.example` có hướng dẫn, không chứa key thật. Chưa thay `.env`, chạy migration lên database đang dùng hoặc gọi mô hình thật trong kiểm thử.

### Cấp tài khoản nhân viên, mail họp và tùy chọn nhắc việc

- Tạo nhân viên đơn lẻ, nhập CSV hoặc Excel đều tạo mật khẩu ngẫu nhiên đã băm và bắt buộc tự đặt mật khẩu. Các trường mật khẩu cũ vẫn được API chấp nhận nhưng bị bỏ qua; giao diện đã bỏ mật khẩu dùng chung. Dry run không tạo tài khoản hoặc thư. Kết quả nhập không trả token kích hoạt.
- Thông báo cấp tài khoản được ghi trong cùng transaction với tài khoản, quyền và audit. Hộp thông báo chứa workspace-key, mã tài khoản, họ tên và email; link đặt mật khẩu chỉ có trong mail gửi riêng cho chủ tài khoản. Token dùng cơ chế reset hiện có, tạo lúc gửi, mặc định hạn 15 phút và dùng một lần. Nếu hết hạn, người nhận dùng Quên mật khẩu. Không tự gửi lại thư cho tài khoản đã có trước nâng cấp.
- Mail mời họp dùng sự kiện `MeetingInvitation`, có nội dung, thời gian, địa điểm và hậu cần. Đã bỏ gửi SMTP trực tiếp trong coordinator; chỉ gửi qua outbox sau commit để tránh một lần thao tác tạo hai email. Kết quả thao tác trả `emailsSent=0`, `emailsQueued` là số thư đã xếp hàng; tùy chọn nhận mail và SMTP quyết định việc gửi thực tế. Phòng ban không có đại diện dùng COMPANY_ADMIN trong tenant làm người nhận dự phòng.
- Màn hình **Thông báo → Cài đặt thông báo và tổng hợp công việc** cho phép bật/tắt mail công việc và họp, chọn mail nhắc hạn `Always`, `AwayOnly` hoặc `Off`. Mặc định nhận mail, nhắc hạn `AwayOnly`, tổng hợp hằng ngày tắt. Mail cấp tài khoản và reset mật khẩu luôn độc lập với tùy chọn tắt mail nghiệp vụ. Thông báo trong ứng dụng vẫn được giữ.
- Nhắc hạn theo nhiệm vụ hỗ trợ mốc nguyên 0–43200 phút, tối đa 20 mốc khác nhau. Mốc 0 là quá hạn; các mốc 24h/4h/1h giữ tên cũ, mốc khác dùng `Custom:<phút>` và tính đúng thời điểm trước hạn. Người dùng chỉ cấu hình công việc mình tạo hoặc được giao trực tiếp; chỉ đọc/hủy lịch nhắc của chính mình.
- Với nhắc hạn nhiệm vụ, `Always` gửi mail dù đang trực tuyến; `AwayOnly` gửi khi vắng mặt và Web Push không chuyển được; `Off` không gửi mail nhắc hạn. Tắt nhận mail nghiệp vụ chặn cả hai chế độ gửi mail. Workflow deadline dùng cùng tùy chọn hiện diện trong outbox. Luồng nhắc hạn nhiệm vụ hiện ghi log kết quả từng kênh nhưng không có retry SMTP như outbox; thư công việc, họp, cấp tài khoản và tổng hợp dùng cơ chế retry của outbox.
- Bản tổng hợp chỉ lấy nhiệm vụ chưa hoàn tất, chưa xóa, có hạn trong khoảng ngày và có phân công trực tiếp còn hiệu lực cho người nhận. Không bao gồm nhiệm vụ giao chung phòng ban. Ngày được hiểu theo múi giờ doanh nghiệp; hiển thị tối đa 100 dòng cùng tổng số thực tế.
- Có thể tạo tổng hợp thủ công cho khoảng tối đa 31 ngày, bao gồm cả hai ngày đầu/cuối. Cùng một người và khoảng ngày chỉ tạo một bản chụp, kể cả khi gọi đồng thời; không tự cập nhật bản đã tạo. Kết quả nằm trong hộp thông báo và gửi mail nếu đã bật nhận mail lẫn mail tổng hợp. Khoảng không có nhiệm vụ vẫn có thông báo xác nhận.
- Tổng hợp hằng ngày cần `Features:BackgroundJobs=true`; Hangfire kiểm tra mỗi 5 phút, sau giờ người dùng chọn theo múi giờ doanh nghiệp, tối đa một bản/ngày. Có thể chọn 1–30 ngày tính từ hôm nay. Sau thời gian gián đoạn chỉ tạo bản của ngày hiện tại, không phát lại các ngày đã bỏ lỡ. Không cần bật tổng hợp hằng ngày để dùng tổng hợp thủ công.
- Migration mới: `20261125010000_NotificationPreferences`, lưu tùy chọn theo người dùng/tenant và có trigger chặn chuyển chủ sở hữu. Cần áp dụng trước khi chạy API mới. SMTP tiếp tục dùng cấu hình `ResetEmail`, có thể ghi đè bằng `NotificationEmail`. Chưa thay đổi `deploy/.env`, chưa cập nhật database đang chạy và chưa gửi qua SMTP doanh nghiệp thật.

| API | Mục đích |
| --- | --- |
| `GET /api/v1/notifications/preferences` | Đọc tùy chọn của chính người đang đăng nhập; chưa lưu thì trả mặc định. |
| `PUT /api/v1/notifications/preferences` | Lưu `emailEnabled`, `deadlineEmailMode`, `dailyDigestEnabled`, `digestHour` (0–23), `digestDays` (1–30), `digestEmail`. |
| `POST /api/v1/notifications/digests` | Body `{ "from": "2026-11-25", "to": "2026-11-27" }`; trả `created` và `result` gồm notificationId/taskCount khi tạo mới. |

### Email đăng ký doanh nghiệp

- Tiếp nhận: gửi đến `ContactEmail`, gồm tên doanh nghiệp và mã hồ sơ. Chưa cấp workspace tại thời điểm tiếp nhận. Hồ sơ gửi lại cũng nhận xác nhận tiếp nhận.
- Phê duyệt: tạo workspace và COMPANY_ADMIN, nhưng tài khoản bắt buộc tự đặt mật khẩu trước khi đăng nhập. Mật khẩu khởi tạo được tạo ngẫu nhiên và băm; trường `AdminPassword` cũ vẫn được chấp nhận để tương thích API nhưng bị bỏ qua. Giao diện nền tảng đã bỏ ô mật khẩu và mật khẩu mẫu.
- Mail quản trị viên chứa workspace-key, họ tên, mã tài khoản/email đăng nhập và link `/reset-password#token=...&tenantKey=...&identifier=...`. Dùng token reset hiện có, hạn theo `PasswordReset:TokenLifetimeMinutes` (mặc định 15 phút), vô hiệu sau khi đặt mật khẩu. Màn hình tự điền workspace/tài khoản và xóa fragment khỏi lịch sử. Khi hết hạn, dùng Quên mật khẩu. Cần bật/cấu hình `ResetEmail` để luồng Quên mật khẩu hoạt động.
- Nếu email người đăng ký khác email quản trị viên, gửi thêm xác nhận phê duyệt đến người đăng ký kèm workspace/thông tin tài khoản, **không kèm token đặt mật khẩu**. Link kích hoạt chỉ đến chủ tài khoản.
- Từ chối: gửi nguyên văn lý do admin nhập và link bổ sung hồ sơ có chữ ký, hạn 7 ngày. Token có audience/type riêng, ràng buộc mã doanh nghiệp, email, phiên bản hồ sơ và lần từ chối cụ thể. Không dùng được để đặt mật khẩu hay xác thực đăng nhập.
- Mở `/register-company#resubmit=...` sẽ lấy hồ sơ bằng POST `/api/v1/company-registrations/resubmission` với `{token}`. Giao diện cho sửa thông tin và thay tài liệu; mã doanh nghiệp và email liên hệ bị khóa. Gửi lại vào API đăng ký hiện có với `resubmissionToken`. Server kiểm tra lại chữ ký, thời hạn, hồ sơ chưa có tenant và token chưa dùng; khóa dòng chống hai lần gửi đồng thời. Giữ CompanyId, lý do từ chối trong audit và các mail cũ, chuyển về PENDING để admin xét duyệt lại.
- Link bổ sung bị vô hiệu sau lần gửi thành công hoặc khi có lần từ chối mới. Nếu hết hạn, cần liên hệ quản trị viên nền tảng; hiện chưa có nút tự cấp lại link trên giao diện.
- Hàng đợi `CompanyRegistrationEmail` được ghi cùng transaction tiếp nhận/phê duyệt/từ chối. Worker mail hiện có xử lý riêng cả giai đoạn chưa có tenant; dùng chung SMTP `ResetEmail`/ghi đè `NotificationEmail`, khóa dòng, tối đa 5 lần thử và backoff 1/5/15/60 phút. Không lưu token đặt mật khẩu hoặc token bổ sung trong hàng đợi/log; token được tạo khi gửi. Những quyết định đã lỗi thời không phát lại email mời/bổ sung.
- Không tự gửi mail cho hồ sơ đã đăng ký/phê duyệt trước nâng cấp. Chưa sửa `.env`, áp dụng migration lên database đang chạy hoặc gửi qua SMTP thật. Migration mới phải được áp dụng trước khi khởi động API mới; kiểm thử chỉ dùng PostgreSQL Docker tạm và SMTP loopback.

| API | Mục đích |
| --- | --- |
| `GET /api/v1/services/{id}/form` | Schema biểu mẫu của phiên bản đang gắn vào dịch vụ trong tenant. |
| `POST /api/v1/requests` | Thêm thuộc tính `formData` là object; khi gửi ngay sẽ kiểm tra schema và mở runtime trong cùng transaction. |
| `GET /api/v1/requests/{id}/workflow` | Phiên bản đã ghim, dữ liệu biểu mẫu, bước, người xử lý, hạn và lịch sử quyết định; quyền đọc theo hồ sơ. |
| `POST /api/v1/requests/{id}/workflow/start` | Người đề nghị mở runtime cho yêu cầu đã gửi phù hợp; gọi lại trả phiên đã có. |
| `POST /api/v1/requests/{id}/workflow/steps/{stepId}/decisions` | Body `{ "revision": 1, "outcome": "APPROVE", "note": "Đã kiểm tra" }`. COMPLETE cho bước xử lý, APPROVE/REJECT cho phê duyệt/xác nhận. |
| `POST /api/v1/requests/{id}/workflow/steps/{stepId}/delegations` | Body `{ "revision": 1, "principalId": "...", "delegateId": "...", "expiresAt": "...", "reason": "Nghỉ phép" }`; cần `workflows.configure`. |
| `POST /api/v1/requests/{id}/workflow/delegations/{delegationId}/revoke` | Thu hồi quyền đại diện với body `{ "revision": 2 }`; giữ toàn bộ lịch sử. |
| `POST /api/v1/requests/{id}/workflow/steps/{stepId}/sla/pause` | Body `{ "revision": 3, "reason": "Chờ chứng từ" }`; chỉ bước chờ có hạn SLA và người có quyền phù hợp. |
| `POST /api/v1/requests/{id}/workflow/steps/{stepId}/sla/resume` | Body `{ "revision": 4 }`; tiếp tục và tính hạn mới từ bằng chứng lịch đã chụp. |
| `GET /api/v1/workflow-inbox?page=1&pageSize=25` | Danh sách bước đang chờ của người dùng hiện tại, có phân trang. |
| `GET /api/v1/workflow-calendars` | Danh sách lịch trong tenant để cấu hình workflow, yêu cầu `workflows.read`. |
| `GET /api/v1/workflow-metrics?days=30` | Thống kê bước trong phạm vi hồ sơ được đọc, yêu cầu `reports.manager`; từ 1–366 ngày và bao gồm bước còn chờ từ trước kỳ báo cáo. |

Quyết định cũ, trùng hoặc trên bước đã hoàn tất trả 409; cấu hình hoặc biểu mẫu không hợp lệ trả 422. Thuộc tenant khác hoặc không được xem hồ sơ trả 404. Client phải dùng revision mới nhận để gửi quyết định tiếp theo.

Kết quả tạo/gửi/sửa đổi là biên nhận của thao tác, giữ trạng thái SUBMITTED/DRAFT ổn định khi gửi lại cùng khóa idempotency. Lấy chi tiết hồ sơ hoặc runtime để đọc trạng thái hiện tại, vì quy trình có thể chuyển sang IN_PROGRESS hoặc kết thúc ngay trong transaction đó.

## Giới hạn và phần còn lại của lộ trình

Không coi đợt này là hoàn thành toàn bộ lộ trình doanh nghiệp:

- Nhánh song song hiện hỗ trợ các nhóm tách/gộp không chu trình, mỗi nhánh một bước người xử lý đi trực tiếp tới cùng cổng gộp. Chưa có nhánh lồng nhau, nhánh nhiều bước hoặc vòng lặp kết hợp song song.
- ALL ghim danh sách người xử lý tại thời điểm mở bước và kiểm tra lại quyền/người dùng khi quyết định. Khi một người không còn đủ điều kiện, quản trị quy trình có thể cấp người thay theo bước; chưa tự động tìm người thay.
- Chưa có lịch ủy quyền toàn doanh nghiệp cho yêu cầu tương lai, push cho mọi sự kiện workflow, lịch việc định kỳ và giao diện mô phỏng trước phát hành. Email đã bao phủ các sự kiện tạo thông báo trong ứng dụng và có tùy chọn nhận theo từng người dùng; chưa có trình sửa mẫu thư cho từng loại.
- Tích hợp Teams/email nghiệp vụ, HRM/ERP và webhook được **tạm hoãn theo lựa chọn của người dùng** vì chưa xác định hệ thống đích. Teams/email dùng để nhận tin và mở hồ sơ; HRM/ERP đồng bộ nhân sự/phòng ban/ngân sách; webhook gửi sự kiện duyệt/hoàn tất. Trước khi triển khai cần chọn hệ thống, dữ liệu, chiều đồng bộ và chủ sở hữu vận hành. Việc hoãn không ảnh hưởng runtime nội bộ.
- AI tự động xử lý bước DEPARTMENT đã bật Agent, với đầu vào và công cụ đọc giới hạn theo phiên bản phát hành. Chưa thực thi nghiệp vụ trên ERP/HRM, đọc nội dung tệp đính kèm, tự thay phân công hoặc tự phê duyệt; chưa nghiệm thu chất lượng đầu ra với mô hình thật và dữ liệu doanh nghiệp.
- Bản sửa đổi kế thừa dữ liệu biểu mẫu cũ. Khi dịch vụ đổi sang schema mới không tương thích, gửi bản sửa đổi có thể bị từ chối và cần tạo yêu cầu mới với biểu mẫu mới.

## Kiểm chứng

Kết quả chốt ngày 25/11/2026 trên mã hiện tại:

| Kiểm tra | Kết quả |
| --- | --- |
| Build toàn bộ solution, Release | Thành công, 0 cảnh báo, 0 lỗi |
| Backend unit test | 725/725 qua, không bỏ qua; thêm 18 ca policy, bằng chứng, công cụ và đầu vào cho Agent workflow |
| Toàn bộ integration test tại mốc workflow trước bổ sung email | 426/426 qua, không bỏ qua; 11 phút 41 giây |
| Integration liên quan sau bổ sung email | 63/63 qua: thông báo, SMTP, workflow runtime, reset mật khẩu và readiness; PostgreSQL Docker tạm |
| Integration sau bổ sung mail đăng ký doanh nghiệp | 57/57 qua: tenancy, thông báo, SMTP, reset mật khẩu và readiness; gồm 2 ca toàn luồng mới |
| Integration sau hoàn thiện thông báo nhân viên/họp/tổng hợp | 95/95 qua, không bỏ qua; xác thực, tổ chức, nhắc hạn, SMTP/outbox, mail đăng ký và 9 ca mới; 2 phút 54 giây |
| Integration hồi quy khi bổ sung Agent workflow | 148/148 qua, không bỏ qua; AI, workflow, thông báo, xác thực, persistence và readiness; 4 phút 12 giây |
| Runtime/Agent ở lượt trước kiểm tra cuối | 20/20 qua, gồm 8 ca Agent/giám sát mới; 2 phút 12 giây |
| Kiểm tra integration chốt sau bổ sung 2 ca worker | Chưa xác nhận: Docker không tạo được container, 22 ca lỗi khởi tạo fixture trước khi chạy nghiệp vụ; chạy riêng 2 ca mới cũng gặp lỗi tạo PostgreSQL container |
| Frontend unit test | 381/381 qua, 68 file; thêm cấu hình Agent, kết quả/bằng chứng, lịch sử và chạy lại có revision |
| Frontend production build sau thay đổi cuối | Thành công |
| Workflow UI trên Chromium desktop/mobile | 2/2 qua, tiến trình test kết thúc thành công |
| EF model so với migration | Không có thay đổi model chưa được ghi vào migration |
| `git diff --check` | Không có lỗi khoảng trắng |

Các ca trình duyệt dùng API mock. Kiểm thử ghi/đọc, phân quyền, tranh chấp quyết định, SMTP loopback và trigger dùng PostgreSQL thật trong các database/container tạm, không dùng cơ sở dữ liệu đang chạy của người phát triển.

Lượt tích hợp trước có một ca readiness vượt giới hạn thời gian (423/424 qua). Chạy riêng nhóm readiness 4/4 qua, nhóm Web Push/readiness 6/6 qua và lượt toàn bộ cuối 426/426 qua. Không nới thời hạn readiness của ứng dụng hoặc thêm retry để che kết quả. Web Push chưa được nghiệm thu với nhà cung cấp/thiết bị thật; kiểm thử tự động dùng HTTP giả lập và xác minh mã hóa, xử lý lỗi, quyền sở hữu đăng ký.

Các nhóm kiểm tra liên quan:

- Unit: kiểm tra schema/giá trị biểu mẫu, biên hạn mức, giờ làm việc/ngày nghỉ/DST, tạm dừng SLA và các quy tắc nghiệp vụ có sẵn.
- Integration PostgreSQL thật trong Docker tạm: tự khởi tạo và rollback/backfill migration, ALL/ANY, ủy quyền/thu hồi/hết hạn, quyết định trùng, nhánh song song, quyền tenant, bằng chứng SLA, worker đồng thời, báo cáo và bảo vệ lịch sử. SMTP dùng loopback; Web Push dùng HTTP giả lập và giải mã RFC 8291 để đối chiếu payload, không liên hệ nhà cung cấp push thật.
- Frontend: toàn bộ unit test Angular và production build.
- Trình duyệt: `workflow-runtime-ui.spec.ts` kiểm tra hiển thị biểu mẫu, xung đột revision, quyết định và tải lại trên desktop/mobile. API trong test trình duyệt được mock; không thay thế kiểm thử persistence ở trên.

Bổ sung email: 8/8 kiểm thử SMTP/reset/outbox qua trên PostgreSQL Docker tạm và SMTP loopback, gồm nhận mail thực, thừa kế cấu hình, worker đồng thời, rollback, tắt riêng email, lọc tenant, backoff/max retry và bỏ qua thông báo cũ/tài khoản ngừng hoạt động. Sau đó chạy 63/63 ca integration liên quan thông báo/workflow/reset/readiness đều qua. Kiểm tra cú pháp Docker Compose dùng `.env.example` đã qua. Chưa gửi mail bằng tài khoản SMTP thật của doanh nghiệp và chưa áp dụng migration lên database đang dùng.

Bổ sung mail đăng ký doanh nghiệp: 57/57 ca liên quan qua sau thay đổi cuối backend. Hai ca mới dùng API, PostgreSQL Docker và SMTP loopback để kiểm tra tiếp nhận → phê duyệt → mail đến đúng người → đặt mật khẩu → đăng nhập; và từ chối → nhận đủ lý do → xác thực link → sửa/gửi lại → chờ duyệt. Có kiểm tra token bị sửa, hết hạn, dùng lại, lần từ chối mới, đổi email trái phép và hai lần gửi đồng thời; không lưu token/password trong payload hàng đợi. Test frontend xác nhận khóa mã/email khi bổ sung, giữ tài liệu cũ, gửi token trong body và xóa fragment khỏi URL. Chưa nghiệm thu SMTP nhà cung cấp thật.

Bổ sung Agent workflow: 18 unit mới kiểm tra giới hạn policy, ngăn tự phê duyệt/bỏ qua tệp bắt buộc, nguồn bằng chứng khớp, trường bắt buộc trong JSON, công cụ bị cấm, giới hạn vòng lặp và thiếu dữ liệu buộc người xử lý. Nhóm runtime 20/20 và nhóm hồi quy mở rộng 148/148 đã qua trên PostgreSQL Docker; Agent dùng mô hình giả lập để kiểm tra giao thức, trạng thái, quyền, tranh chấp, lease, retry, lịch sử và không tự vượt qua bước phê duyệt. Sau đó bổ sung giới hạn song song, lọc người nhận còn hoạt động và hai ca worker chọn tenant/công tắc tắt; build solution cuối vẫn qua 0 cảnh báo/lỗi, nhưng lượt integration chốt bị Docker timeout tại CreateContainerAsync. Các ca này chưa vào phần xác minh nghiệp vụ, không được coi là đã qua trên mã cuối. Cần khôi phục Docker rồi chạy lại nhóm WorkflowRuntimeTests trước khi nghiệm thu triển khai.

Một lượt kiểm tra cuối chạy nhiều tiến trình cùng lúc gặp cạn bộ nhớ Windows (Sass/esbuild và trình duyệt), khiến lượt frontend/browser không hoàn tất. Đã chạy lại tuần tự với `NG_BUILD_MAX_WORKERS=2`: frontend 381/381 và Chromium desktop/mobile 2/2 qua, gồm đọc kết quả Agent, bằng chứng, lịch sử, quyết định và tải lại. Không nới assertion hoặc thời hạn để làm test qua. Docker vẫn trả được version/list nhưng tạo/xóa container kiểm thử bị treo; không khởi động lại Docker vì có thể ảnh hưởng database đang dùng. Thử tắt Resource Reaper tạm thời trong tiến trình test riêng cũng lỗi ở tạo PostgreSQL container, xác nhận không chỉ lỗi reaper. Không thay cấu hình Testcontainers trong repository, không áp migration lên database người dùng và chưa gọi mô hình thật.

Hoàn thiện thông báo: nhóm 95 ca integration qua sau thay đổi backend cuối. Kiểm thử mới xác nhận tạo nhân viên đơn lẻ/nhập hàng loạt, dry run không ghi, nhận thư đúng chủ tài khoản, workspace-key và link reset dùng một lần, không dùng mật khẩu chung và không lộ token trong hộp thông báo. Kiểm tra mail họp chỉ gửi sau commit, đầy đủ địa điểm/hậu cần và tôn trọng tắt mail; kiểm tra 5 tổ hợp tùy chọn/hiện diện cho nhắc hạn. Tổng hợp kiểm tra ranh giới ngày theo múi giờ doanh nghiệp, chỉ việc được giao trực tiếp chưa hoàn tất, không lấy việc người khác/tenant khác, giờ chạy và chạy lại không trùng, hai yêu cầu khoảng ngày đồng thời, giới hạn ngày/giờ và trigger bảo vệ chủ sở hữu. Có kiểm thử SMTP thực trên loopback cho tổng hợp, bật/tắt mail riêng vẫn giữ hộp thông báo. Build solution Release 0 cảnh báo/lỗi; 707 unit backend, 377 unit frontend và production build đều qua; EF không có thay đổi model chưa ghi migration. Không chạy lại toàn bộ integration trong đợt thông báo này; mốc 426 là kết quả lịch sử trước các bổ sung email.

Lệnh tái kiểm tra:

```powershell
dotnet test tests/BizFlow.UnitTests/BizFlow.UnitTests.csproj --configuration Release
dotnet test tests/BizFlow.IntegrationTests/BizFlow.IntegrationTests.csproj --configuration Release
cd frontend
npm test
npm run build
cd ../tests/BizFlow.E2ETests
npx playwright test specs/workflow-runtime-ui.spec.ts --workers=1
```
