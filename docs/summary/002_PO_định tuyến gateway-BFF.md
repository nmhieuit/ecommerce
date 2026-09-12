# Một cửa vào duy nhất — ứng dụng khách hàng không cần biết hệ thống có bao nhiêu mảnh ghép

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 65/65 hạng mục công việc, xác minh bằng các bài kiểm tra tự động chạy
thật.*

## Vấn đề trước đây

Nền tảng có bốn mảng nghiệp vụ tách biệt (khách hàng, sản phẩm, giỏ hàng, đơn hàng — xem tính năng
"001 — Bốn khối nền tảng độc lập"), nhưng chưa có cách nào để ứng dụng dành cho khách hàng (website
mua sắm) nói chuyện với chúng một cách gọn gàng. Nếu để nguyên như vậy, ứng dụng khách hàng sẽ phải
tự biết địa chỉ của từng mảng riêng lẻ, tự gọi nhiều nơi, và tự ghép dữ liệu lại — một cách làm dễ vỡ
mỗi khi cấu trúc nội bộ hệ thống thay đổi.

## Giải pháp: một "cửa vào" duy nhất, phía sau tự lo việc ghép nối

Giờ đây ứng dụng khách hàng chỉ cần biết **một địa chỉ duy nhất** để gọi tới. Đằng sau cửa vào đó,
hệ thống tự động:
- **Định tuyến** mỗi yêu cầu tới đúng mảng nghiệp vụ cần xử lý nó — ứng dụng khách hàng không cần
  biết mảng nào nằm ở đâu.
- **Ghép nối dữ liệu** khi một màn hình cần thông tin từ nhiều mảng cùng lúc, trả về đúng một câu trả
  lời gọn gàng, thay vì bắt ứng dụng khách hàng tự gọi nhiều lần rồi tự ghép.
- **Báo lỗi rõ ràng, nhanh chóng** nếu một mảng nào đó đang gặp sự cố — thay vì để người dùng chờ đợi
  vô thời hạn không biết chuyện gì đang xảy ra.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Ứng dụng khách hàng gửi một yêu cầu** (ví dụ: "cho tôi xem danh sách sản phẩm") tới đúng một địa
   chỉ duy nhất.
2. **Hệ thống tự định tuyến** yêu cầu đó tới đúng mảng nghiệp vụ phụ trách, không cần ứng dụng khách
   hàng biết gì thêm.
3. **Nếu cần dữ liệu từ nhiều mảng cùng lúc**, hệ thống tự gọi tới từng mảng liên quan và ghép kết
   quả lại thành một câu trả lời duy nhất.
4. **Nếu một mảng đang gặp sự cố** (ví dụ tạm thời ngừng hoạt động), người dùng nhận được một thông
   báo lỗi rõ ràng trong vòng vài giây — không phải một màn hình treo không phản hồi.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/002-gateway-bff-routing-flow-nghiep-vu.drawio`](../diagrams/002-gateway-bff-routing-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Ứng dụng khách hàng đơn giản hơn, ổn định hơn** — không cần biết cấu trúc nội bộ hệ thống, nên
  thay đổi bên trong hệ thống không làm hỏng ứng dụng khách hàng.
- **Trải nghiệm người dùng nhất quán khi có sự cố** — luôn có thông báo rõ ràng trong thời gian giới
  hạn, không có tình trạng "treo máy" không rõ nguyên nhân.
- **Dễ dò lỗi khi có sự cố thật** — mã số theo dõi được giữ nguyên xuyên suốt, giúp đội vận hành tra
  cứu đúng và nhanh.

Bằng chứng đã kiểm chứng thật (gồm 1 lỗi thật bắt được lúc kiểm chứng) và giới hạn hiện tại: xem
[functional-debt.md](functional-debt.md).
