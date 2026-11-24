# Hồ sơ cá nhân

## Chức năng

- `/profile` dành cho mọi tài khoản đã đăng nhập, bao gồm quản trị nền tảng.
- Bấm tên tài khoản mở hồ sơ; nút mũi tên riêng mở menu trên điện thoại.
- Xem mã tài khoản, email, doanh nghiệp/workspace, phòng ban, vai trò và ngày tạo.
- Chỉ sửa biệt danh; họ tên chính thức/email/phòng ban/vai trò không sửa qua hồ sơ cá nhân.
- Biệt danh mặc định bằng họ tên. Migration `20261124142115_UserNickname` khởi tạo
  biệt danh cho tài khoản hiện có; tài khoản mới lấy giá trị ban đầu từ họ tên.
- Chat AI và phần thảo luận hiển thị biệt danh + vai trò, tooltip là họ tên chính thức.
  Dữ liệu nhiệm vụ/yêu cầu (người tạo, người nhận, người yêu cầu) vẫn dùng `FullName`.
- Hồ sơ tự thử lại một lần khi lỗi mạng/máy chủ tạm thời; mở trang sẽ tải lại sau
  một lỗi cũ. Yêu cầu tải có giới hạn thời gian và bị hủy khi đổi tài khoản.
- Chọn/xóa ảnh PNG, JPEG hoặc WebP tối đa 5 MB. Trình duyệt chuẩn hóa thành PNG
  tối đa 512px; máy chủ kiểm tra cấu trúc PNG, CRC và kích thước giải nén.
- Ảnh lưu qua object storage, tham chiếu bằng `User.AvatarObjectKey`; chỉ chủ tài khoản
  đọc ảnh qua API được xác thực. Blob URL trên trình duyệt được thu hồi khi đổi ảnh/đăng xuất.
- Đổi mật khẩu yêu cầu mật khẩu hiện tại, tuân theo chính sách mật khẩu khôi phục,
  thay security stamp và thu hồi toàn bộ phiên đăng nhập trong cùng giao dịch.
- Sai mật khẩu hiện tại áp dụng chính sách khóa đăng nhập hiện có.
- Ghi audit thay đổi hồ sơ, đổi mật khẩu thành công/thất bại; không ghi mật khẩu.

## Cơ sở dữ liệu

Migration `20261124110256_PersonalProfileAvatar` chỉ thêm cột nullable
`User.AvatarObjectKey`. Đã áp dụng trên môi trường local. Các môi trường khác cần
áp dụng migration theo quy trình triển khai trước khi chạy bản backend mới.

## Kiểm tra

- `PersonalProfileTests` dùng PostgreSQL dùng một lần: xác thực, cách ly tài khoản,
  ngăn sửa họ tên/email/quyền qua payload, ảnh giả/hỏng, thay/xóa ảnh, khóa do sai mật khẩu,
  thu hồi access/refresh token và đăng nhập bằng mật khẩu mới, tài khoản platform.
- Kiểm tra hồi quy `PasswordResetTests`.
- `personal-profile.spec.ts`: máy tính, điện thoại 390px và 320px; cập nhật tên,
  chuyển ảnh, xử lý lỗi mật khẩu, xóa ảnh, đăng xuất và khách chưa đăng nhập;
  hồi quy phục hồi hồ sơ sau lỗi tải và biệt danh/tooltip trong chat.
  Các thao tác ghi trên trình duyệt được intercept; không đổi mật khẩu tài khoản thật.
- `mobile-account-menu.spec.ts`: điều hướng, đóng menu, bàn phím, phân quyền và đăng xuất.
- Build frontend production thành công.

## Sửa lỗi xác thực tải hồ sơ (2026-11-24)

Tái hiện bằng trình duyệt gọi API thật: `GET /api/v1/auth/profile` trả 401,
request không có `Authorization`. Interceptor trước đây bỏ qua toàn bộ
`/api/v1/auth/*`, vô tình bỏ qua cả các API hồ sơ có yêu cầu đăng nhập.
Các kiểm tra giao diện dùng profile mock và các probe tự gắn bearer trước đó
không phát hiện được lỗi tích hợp này.

Interceptor hiện chỉ bỏ qua danh sách cụ thể các API công khai đăng nhập,
refresh, khôi phục mật khẩu và email link. API hồ sơ, ảnh và đổi mật khẩu
được gắn bearer và áp dụng luồng refresh khi access token bị từ chối.

- `auth.interceptor.spec.ts` và `personal-profile.service.spec.ts`: 14 kiểm tra đạt;
  gồm bearer cho mọi thao tác hồ sơ, không gửi token tới credential endpoint,
  và thử lại profile sau refresh.
- `e2e/personal-profile-live.spec.ts` dùng API thật, không intercept: đăng nhập qua
  giao diện và tải lại hồ sơ với ADMIN001 (desktop/mobile), EMP001 và SUPERADMIN;
  thêm kiểm tra token bị từ chối → refresh → tải hồ sơ thành công.
- Các kiểm tra live chỉ đọc hồ sơ và refresh phiên thử nghiệm; không sửa hồ sơ,
  ảnh hoặc mật khẩu tài khoản hiện có.
