# Kiểm thử tải trên đúng luồng khách hàng thật đi — không còn phải "cảm nhận" hệ thống có chậm hay không

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 18/18 hạng mục công việc. Lần chạy thật đầu tiên trên môi trường đầy đủ
đã phát hiện ngay 1 khoảng trống thật của toàn hệ thống — xem [functional-debt.md](functional-debt.md),
đây chính là giá trị của việc kiểm thử thật thay vì chỉ tin vào thiết kế.*

## Vấn đề trước đây

Mỗi bộ phận của hệ thống đã có sẵn 1 cam kết cụ thể về tốc độ phản hồi. Nhưng chưa có cách nào để biết
chắc **toàn bộ hành trình mua sắm thật** (xem sản phẩm → thêm giỏ hàng → thanh toán → xem lại đơn hàng)
có thực sự đạt đúng những cam kết đó khi có nhiều người dùng cùng lúc hay không — mọi đánh giá trước
đây chỉ dựa vào cảm nhận hoặc từng phần riêng lẻ, không phải một phép đo thật trên toàn bộ hành trình.

## Giải pháp: 1 bài kiểm thử tải chạy đúng hành trình khách hàng thật, tự động chặn khi vượt cam kết

- **Mô phỏng nhiều người dùng cùng lúc đi qua đúng 4 bước** của hành trình mua sắm thật, đo tốc độ
  phản hồi riêng cho từng bước.
- **Đối chiếu trực tiếp với cam kết đã công bố** cho từng bước — biết ngay bước nào đang đạt, bước nào
  đang vượt.
- **Nếu có bước nào vượt cam kết, cả lần kiểm thử bị đánh dấu THẤT BẠI rõ ràng** — không phải một ghi
  chú cảnh báo mà không ai buộc phải xử lý.
- **Có thể chạy lại bất cứ lúc nào**, tách biệt khỏi quy trình kiểm tra thông thường mỗi lần thay đổi
  mã nguồn, để không làm chậm công việc hằng ngày của đội phát triển.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Hệ thống tự động mô phỏng nhiều người mua sắm cùng lúc**, đi qua đúng hành trình thật: xem sản
   phẩm, thêm vào giỏ, thanh toán, xem lại đơn hàng.
2. **Mỗi bước được đo tốc độ phản hồi riêng**, đối chiếu ngay với cam kết đã công bố cho bước đó.
3. **Nếu mọi bước đều đạt cam kết** — lần kiểm thử thành công, số đo được lưu lại làm mốc so sánh cho
   lần sau.
4. **Nếu có bước nào vượt cam kết** — lần kiểm thử thất bại rõ ràng, nêu đúng bước nào và vượt bao
   nhiêu, không để lọt qua như một cảnh báo bị bỏ qua.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/026-load-performance-test-budgets-flow-nghiep-vu.drawio`](../diagrams/026-load-performance-test-budgets-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Biết chắc hành trình mua sắm thật có đạt tốc độ đã cam kết hay không** — không còn dựa vào cảm
  nhận hay đo từng phần rời rạc.
- **Một hồi quy hiệu năng luôn có hậu quả cụ thể** (chặn phát hành) thay vì chìm vào một dashboard đỏ
  mà không ai xử lý.
- **Kiểm tra được lặp lại bất cứ lúc nào**, không tốn công chuẩn bị thủ công mỗi lần cần đánh giá.

Phát hiện thật quan trọng khi chạy lần đầu (1 khoảng trống xác thực của toàn hệ thống khiến lần chạy
đầu tiên thất bại) và giới hạn hiện tại: xem [functional-debt.md](functional-debt.md). Chi tiết kỹ
thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/026_Architect_kiểm thử tải hiệu năng luồng nghiệp vụ trọng yếu.md`](../architecture/026_Architect_kiểm%20thử%20tải%20hiệu%20năng%20luồng%20nghiệp%20vụ%20trọng%20yếu.md).
