# Báo cáo đối chiếu dự án

- **Ngày cập nhật:** 01/11/2026; mã nguồn tại commit `61cca5c`.
- **Phạm vi:** đối chiếu tĩnh các luồng, cấu hình và kiểm thử trọng yếu. [Đường cơ sở triển khai](CURRENT_IMPLEMENTATION_BASELINE.md) chứa bằng chứng theo chức năng và giới hạn chi tiết.
- **Lưu ý:** báo cáo không chứng nhận an toàn, hoàn thành MVP hay 100% nghiệm thu. Các bản audit trước phản ánh snapshot lịch sử; không được dùng kết quả của chúng như kết quả test hiện tại.

## Hiện trạng

Hệ thống dùng Modular Monolith .NET 10, Angular 22, PostgreSQL, Redis và S3-compatible storage. Có mã cho xác thực/tenant, tổ chức, Task, Request, danh mục, workflow/SLA, thông báo, cộng tác, báo cáo và AI. Tại commit đối chiếu: 26 file controller, 40 DbSet và 37 file migration không tính designer/snapshot; đây không phải số bảng hoặc tỷ lệ hoàn thành.

Các endpoint đăng ký, thẩm định company, tạo/sửa phòng ban và người dùng hiện đã có mã, khác báo cáo ngày 19/10. Trạng thái của từng yêu cầu vẫn là `PARTIAL` khi chưa chứng minh đầy đủ tiêu chí đầu-cuối, phân quyền và rollback. Workflow phê duyệt nhiều cấp, đồng hồ SLA runtime và cơ chế nhắc hạn tự động chưa có đủ bằng chứng hoàn chỉnh.

Robot đã có chat, phân rã, xem/xóa nhánh và xác nhận gửi lẻ/hàng loạt. RAG lưu JSONB và tính cosine trong ứng dụng; có daemon chủ động và self-reflection. Các giới hạn đáng chú ý là phạm vi đọc ở một số đường AI, dispatcher ghi trực tiếp, claim AI không đồng nhất, daemon thiếu ngữ cảnh, fallback dữ liệu mẫu và lưu lịch sử chat chưa phân vùng tenant/user. Các điểm này là quan sát tĩnh, không thay thế kiểm thử khai thác hoặc E2E.

## Kiểm thử

Ngày 01/11/2026: unit test backend `dotnet test tests/BizFlow.UnitTests/BizFlow.UnitTests.csproj --no-restore --verbosity quiet` đạt **495/495**, 0 bỏ qua. Chưa chạy integration PostgreSQL, frontend hay E2E trong lượt cập nhật tài liệu. Các test AI dùng InMemory không xác minh trigger hoặc daemon ngoài HTTP.

## Tài liệu và trạng thái quyết định

- [SRS](../requirements/srs/SRS_v1.0_DRAFT.md) giữ yêu cầu và tiêu chí nghiệm thu; trạng thái được điều chỉnh khi đã đối chiếu.
- [SSS](../specifications/sss/SSS_v1.0_DRAFT.md) mô tả kiến trúc và các bất biến dữ liệu.
- [Ma trận truy vết](REQUIREMENT_TRACEABILITY_MATRIX.md) ánh xạ yêu cầu với bằng chứng, không coi liên kết là bằng chứng test đạt.
- Các quyết định `PROPOSED — NOT APPROVED` và `OPEN` không tự chuyển thành đã duyệt khi code tồn tại. `git push` bị cấm theo chỉ dẫn dự án.
