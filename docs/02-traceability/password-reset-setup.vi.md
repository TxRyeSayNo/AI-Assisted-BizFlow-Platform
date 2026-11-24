# Cấu hình email đặt lại mật khẩu BizFlow

Luồng dành cho tài khoản doanh nghiệp: nhập **workspace-key + mã tài khoản hoặc
email** → gửi liên kết tới email đang liên kết trong workspace đó → mở thư → nhập
workspace-key, tài khoản/email và mật khẩu mới. Không có ô nhập một email nhận thư
khác. Thông báo tiếp nhận giống nhau khi thông tin không khớp để tránh dò tài khoản.

## Hiện trạng cần bàn giao

Mã nguồn có SMTP qua TLS, hàng đợi PostgreSQL/Hangfire, liên kết một lần, mặc định
hết hạn sau 15 phút và thu hồi các phiên đăng nhập sau khi đổi mật khẩu. Trên máy
hiện tại, chủ tài khoản đã điền App Password vào `.env`; kết nối TLS và xác thực
thật đi vào inbox của tài khoản thử nghiệm. Với môi trường mới, giữ
`ResetEmail__Enabled=false` tới khi cấu hình đủ.

Cần chuẩn bị:

1. Nhà cung cấp gửi thư; địa chỉ email người gửi được phép sử dụng.
2. SMTP host, port, kiểu bảo mật và username/password ứng dụng nếu nhà cung cấp yêu cầu.
3. Địa chỉ website người nhận sẽ mở: local là `http://localhost:4200`, triển khai
   Docker demo mặc định là `http://localhost:8080`, môi trường public cần HTTPS.
4. Một tài khoản thử nghiệm: workspace-key, mã tài khoản và **email đã lưu trên tài khoản**.
5. Xác nhận backend có quyền tạo/sử dụng schema `hangfire` trong PostgreSQL; mạng
   cho phép kết nối SMTP outbound. Mặc định dùng cùng kết nối PostgreSQL của ứng dụng.

## Ví dụ Gmail / Google Workspace

Chỉ dùng cách này nếu tài khoản được phép tạo App Password. Theo
[Google Help](https://support.google.com/mail/answer/185833), cần bật xác minh hai
bước trước khi tạo mật khẩu ứng dụng; một số tài khoản tổ chức không có tùy chọn này.

1. Đăng nhập tài khoản dùng để **gửi** email, bật xác minh hai bước trong phần Bảo mật.
2. Mở [App Passwords](https://myaccount.google.com/apppasswords), tạo mật khẩu cho
   ứng dụng `BizFlow`. Nếu mục này bị quản trị viên chặn, bàn giao chính sách SMTP
   của tổ chức thay vì dùng mật khẩu đăng nhập thông thường.
3. Điền trực tiếp trong `deploy/.env` trên máy của bạn, bỏ dấu `#` trước các biến cần dùng:

```dotenv
ResetEmail__Enabled=true
ResetEmail__Host=smtp.gmail.com
ResetEmail__Port=587
ResetEmail__SslOnConnect=false
ResetEmail__Username=EMAIL_NGUOI_GUI
ResetEmail__Password=APP_PASSWORD_DA_TAO
ResetEmail__FromName=BizFlow
ResetEmail__FromAddress=EMAIL_NGUOI_GUI
ResetEmail__FrontendOrigin=http://localhost:4200
```

SMTP Gmail dùng `587` với STARTTLS, hoặc `465` với TLS ngay khi kết nối; cấu hình
`465` phải đặt `ResetEmail__SslOnConnect=true`.
[Tài liệu SMTP chính thức](https://developers.google.com/workspace/gmail/imap/imap-smtp).
Nếu dùng SMTP riêng, thay host/port/tài khoản theo nhà cung cấp. Adapter hiện có
hỗ trợ username/password SMTP qua TLS; chưa triển khai cấp/refresh OAuth2 token.
Nếu nhà cung cấp bắt buộc OAuth2, cần bàn giao thông tin tích hợp OAuth2 trước.

Mật khẩu/khóa bí mật điền trong `.env` hoặc secret manager; **không dán vào chat,
issue hay commit Git**. Không bật `AllowInsecureLoopback` cho SMTP thật.

## Tên người gửi email

`ResetEmail__FromName=BizFlow` đặt tên hiển thị trong thư mới thành **BizFlow**.
Để đổi tên sau này, sửa giá trị này trong `deploy/.env`, rồi khởi động lại backend
(hoặc tạo lại service `api` khi dùng Docker theo lệnh bên dưới).
`ResetEmail__FromAddress` vẫn là địa chỉ Gmail đã cấu hình, không cần đổi App Password.
Thư đã gửi trước đó giữ nguyên tên cũ; kiểm tra bằng một yêu cầu khôi phục mới.
Ứng dụng đọc thư có thể ưu tiên tên đã lưu trong danh bạ của người nhận.

## Kiểm tra và khởi động dịch vụ

Từ thư mục gốc:

```powershell
./scripts/Test-PasswordResetConfiguration.ps1
# Kiểm tra thêm khả năng mở cổng SMTP, không gửi thư:
./scripts/Test-PasswordResetConfiguration.ps1 -CheckConnection
```

Script chỉ phát hiện thiếu cấu hình/kết nối TCP, không chứng minh SMTP đã xác thực
hay thư đã vào inbox. Backend còn kiểm tra cấu hình khi khởi động.

Chạy backend trực tiếp: khởi động lại tiến trình `dotnet run --project backend/BizFlow.Api`.
Đảm bảo môi trường hiện tại không có biến `ResetEmail__...` cũ ghi đè `.env`.

Chạy Docker: các biến đã được truyền từ `.env` vào service `api`. Khi website dùng
cổng khác 4200, chỉnh `FrontendOrigin` trước. Dùng lệnh sau từ thư mục gốc; nó chỉ
tạo lại service API theo cấu hình mới, không xóa database/volume:

```powershell
docker compose --env-file deploy/.env -f deploy/docker-compose.yml -f deploy/docker-compose.app.yml up -d --build api
```

Sau khi khởi động lại, mở `/forgot-password`, nhập workspace-key và tài khoản thử
nghiệm, kiểm tra Inbox/Spam, mở liên kết, đổi mật khẩu rồi đăng nhập lại. Kiểm tra
liên kết cũ và mật khẩu cũ không dùng lại được. Liên kết được giữ trong bộ nhớ trang;
nếu tải lại trang đặt mật khẩu, mở lại liên kết từ email.

## Bàn giao lại cho tôi

Gửi thông tin **không bí mật** sau:

```text
Nhà cung cấp SMTP:
Email người gửi:
Host / port / STARTTLS hay TLS:
Website đang chạy ở URL nào:
Chạy backend trực tiếp hay Docker:
Đã điền credential vào deploy/.env: có/chưa
Workspace-key và tài khoản thử nghiệm:
Email liên kết của tài khoản thử nghiệm:
Cho phép gửi email kiểm thử tới chính hộp thư này: có/chưa
```

Tôi sẽ kiểm tra cấu hình, khởi động lại dịch vụ, kiểm tra hàng đợi, gửi yêu cầu thử
đến hộp thư được chỉ định và kiểm tra luồng đổi mật khẩu sau khi nhận đủ thông tin.
Không đổi mật khẩu tài khoản đang sử dụng để thử nghiệm nếu chưa được chỉ định.

## Phạm vi

Luồng public này yêu cầu workspace của tài khoản doanh nghiệp. Tài khoản quản trị
nền tảng không thuộc workspace không dùng biểu mẫu này. Chưa bổ sung giao diện
quản trị viên cấp mật khẩu tạm thời. Kết quả SMTP kiểm thử cục bộ không đồng nghĩa
đã kiểm chứng gửi thư qua nhà cung cấp thật.
