# Demo đặt hàng đầu-cuối — bằng chứng không thể tranh cãi cho việc "Phase 1 đã xong"

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: gần như hoàn thành hoàn toàn — 41/42 hạng mục công việc, xác minh bằng số liệu thật.
Một hạng mục duy nhất còn lại cần một người thực hiện thủ công (xem "Giới hạn hiện tại" bên dưới).*

## Vấn đề trước đây

Đội đã tuyên bố "Phase 1 — nền tảng đi bộ đầu tiên (walking skeleton)" hoàn thành, nhưng chưa có một
bằng chứng cụ thể, có thể xem lại, chứng minh điều đó — chỉ có lời khẳng định. Nếu không có ai tận mắt
chứng kiến (hoặc xem lại) một đơn hàng thật đi trọn vẹn từ đầu tới cuối, tuyên bố "đã xong" vẫn chỉ là
một lời hứa, dễ bị tranh cãi lại sau này.

## Giải pháp: một buổi demo được ghi lại, lặp lại được, và có bằng chứng đi kèm

Tính năng này dựng một quy trình demo chuẩn hoá: khởi động nền tảng, đặt một đơn hàng thật từ đầu tới
cuối qua giao diện thật, và **lưu lại bằng chứng** — không chỉ là "đã chạy được một lần", mà là một quy
trình có thể chạy lại nhiều lần cho ra cùng một kết quả, có ảnh chụp từng bước, và có xác nhận rõ ràng
đơn hàng đó thuộc đúng khách hàng doanh nghiệp nào.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Khởi động nền tảng** — hệ thống tự báo khi đã sẵn sàng để bắt đầu demo, không bắt đầu quá sớm khi
   một phần còn đang khởi động.
2. **Đi qua đúng luồng mua sắm thật**: xem sản phẩm → thêm vào giỏ → thanh toán → nhận xác nhận — không
   có bước can thiệp thủ công nào (không sửa dữ liệu, không khởi động lại dịch vụ nào giữa chừng).
3. **Xác nhận đơn hàng đã được lưu thật**, đọc lại được, và tổng tiền khớp chính xác với màn hình xác
   nhận vừa thấy.
4. **Xác nhận đơn hàng thuộc đúng khách hàng doanh nghiệp** — có một bước riêng đọc lại thông tin này,
   dễ hiểu cho cả người không tham gia xây dựng hệ thống. Nếu cố đặt hàng mà không xác định được khách
   hàng doanh nghiệp nào, hệ thống từ chối ngay, không tạo ra đơn hàng "mồ côi".
5. **Chạy lại nhiều lần cho kết quả nhất quán** — hai lần chạy liên tiếp tạo ra hai đơn hàng riêng
   biệt, không lẫn lộn hay ảnh hưởng lẫn nhau.
6. **Để lại bằng chứng lâu dài**: một bài viết tường thuật từng bước kèm ảnh chụp, được lưu trong kho
   mã nguồn để bất kỳ ai sau này cũng xem lại được mà không cần yêu cầu chạy lại buổi demo.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio`](../diagrams/006-e2e-order-demo-flow-nghiep-vu.drawio))*

## Điều đặc biệt: đã tìm và sửa ba lỗi thật ngay trong lúc chuẩn bị demo

Trong lúc tự động hoá buổi demo, đội phát hiện và sửa ba vấn đề thật, không phải giả định:
- Kịch bản chạy demo tự động từng dừng lại nhầm chỗ vì hiểu sai một cảnh báo vô hại là một lỗi nghiêm
  trọng.
- Cơ chế "bằng chứng mỗi thành phần hệ thống đã thực sự tham gia xử lý" ban đầu bị nhiễu bởi các tín
  hiệu kiểm tra sức khoẻ định kỳ (không phải hoạt động thật) — đã được lọc lại chính xác.
- Một trong bốn ảnh chụp minh hoạ ban đầu **giống hệt ảnh trước đó** — tức là nó không thực sự chứng
  minh điều nó được cho là chứng minh — đã bị phát hiện và thay bằng một ảnh khác, đúng nghĩa.

Đây chính là giá trị của việc tự động hoá kiểm chứng: những lỗi tinh vi này gần như không thể phát
hiện được nếu chỉ nhìn bằng mắt một lần.

## Lợi ích kinh doanh

- **Có bằng chứng cụ thể, xem lại được, cho việc "Phase 1 đã hoàn thành"** — không còn là lời khẳng
  định suông.
- **Demo có thể lặp lại bất cứ lúc nào** cho khách hàng, nhà đầu tư, hay thành viên mới, không cần
  chuẩn bị lại từ đầu mỗi lần.
- **Có tài liệu ánh xạ rõ ràng**: mỗi tiêu chí "hoàn thành Phase 1" được đánh dấu là đã chứng minh
  bằng buổi demo này, hoặc được ghi rõ là để dành cho giai đoạn sau — không có sự mơ hồ nào.

## Giới hạn hiện tại — trung thực cần biết

- **Một bước cuối cùng vẫn cần một người thực hiện thủ công**: đính kèm video ghi lại buổi demo vào hệ
  thống quản lý công việc (Jira). Đây không phải một khiếm khuyết kỹ thuật — công cụ tự động hiện có
  không hỗ trợ đính kèm file vào Jira, và việc đăng nội dung công khai cho cả đội xem là việc cần một
  người chủ động thực hiện, không nên tự động làm thay. Toàn bộ nội dung cần thiết đã được chuẩn bị sẵn
  trong tài liệu tường thuật, chỉ còn thao tác tải file lên.
- Demo hiện chạy trên môi trường thử nghiệm cục bộ (xem tính năng "005 — Một lệnh duy nhất"), chưa
  phải trên hạ tầng vận hành chính thức.
- Demo vẫn dùng danh tính giả lập (một khách hàng doanh nghiệp duy nhất) — chưa chứng minh việc tách
  biệt dữ liệu giữa nhiều khách hàng doanh nghiệp khác nhau cùng lúc, vì Phase 1 chỉ có một khách hàng
  doanh nghiệp.
