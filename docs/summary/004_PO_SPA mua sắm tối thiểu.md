# Mua sắm tối thiểu — từ xem sản phẩm tới nhận xác nhận đơn hàng, đầu-cuối thật

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 71/71 hạng mục công việc, xác minh bằng các bài kiểm tra tự động chạy
thật (bao gồm cả kiểm thử trình duyệt thật, không chỉ kiểm thử mã nguồn).*

## Vấn đề trước đây

Cho tới lúc này, nền tảng mới chỉ có phần "khung xương" phía sau (bốn mảng nghiệp vụ, cổng vào, cơ
chế xác định khách hàng doanh nghiệp) — chưa hề có một màn hình nào để một người mua sắm thực sự nhìn
thấy và thao tác. Không có sản phẩm nào hiển thị, không có giỏ hàng, không có cách nào để hoàn tất một
đơn hàng.

## Giải pháp: luồng mua sắm tối thiểu nhưng đầy đủ, đi từ trình duyệt tới tận nơi lưu đơn hàng

Tính năng này dựng màn hình mua sắm đầu tiên của nền tảng, đi trọn vẹn bốn bước: **xem sản phẩm → thêm
vào giỏ → thanh toán → nhận xác nhận**. Đây không phải một bản demo giả — giỏ hàng thật sự được lưu ở
phía hệ thống (không mất khi tải lại trang hay đóng trình duyệt), và đơn hàng thật sự được tạo ra khi
thanh toán.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người mua sắm mở trang** và thấy ngay danh sách sản phẩm kèm tên và giá — nếu chưa có sản phẩm
   nào, trang hiển thị rõ ràng "chưa có sản phẩm" thay vì một trang trống khó hiểu.
2. **Thêm sản phẩm vào giỏ hàng.** Nếu thêm cùng một sản phẩm nhiều lần, số lượng tăng lên đúng —
   không tạo ra nhiều dòng trùng lặp cho cùng một sản phẩm.
3. **Giỏ hàng được giữ lại** kể cả khi tải lại trang hoặc đóng-mở lại trình duyệt — người mua sắm quay
   lại vẫn thấy đúng những gì họ đã chọn.
4. **Bấm thanh toán** — nếu giỏ hàng đang trống, hệ thống chặn ngay tại giao diện, không gửi yêu cầu
   nào tới phía sau. Nếu bấm thanh toán liên tiếp nhiều lần (ví dụ do bấm nhầm), chỉ đúng một đơn hàng
   được tạo ra, không bị nhân đôi.
5. **Nhận màn hình xác nhận** hiển thị đúng mã đơn hàng đã tạo và tổng tiền — và giỏ hàng sau đó trở
   về trạng thái trống.
6. **Nếu hệ thống phía sau gặp sự cố** ở bất kỳ bước nào, người mua sắm luôn thấy thông báo lỗi rõ
   ràng trong vòng vài giây, không bao giờ bị treo màn hình hay gặp lỗi khó hiểu.

## Điều đặc biệt: đã kiểm chứng bằng kiểm thử trình duyệt thật, không chỉ kiểm thử mã nguồn

Toàn bộ luồng bốn bước trên đã được kiểm thử bằng cách **điều khiển một trình duyệt thật** để đi qua
đúng hành trình một người mua sắm thật sẽ làm — không có lỗi nào hiện ra trong bảng điều khiển của
trình duyệt trong suốt hành trình đó. Đội cũng đã đo thời lượng tải trang thực tế và đặt giới hạn tự
động: nếu một bản cập nhật sau này vô tình làm trang tải chậm hơn ngưỡng cho phép, việc phát hành bản
đó sẽ tự động bị chặn lại.

## Lợi ích kinh doanh

- **Có một luồng mua sắm hoàn chỉnh, chứng minh được, để demo cho các bên liên quan** — không còn là
  một lời hứa trên giấy.
- **Trải nghiệm ổn định trước những tình huống thực tế dễ gặp**: tải lại trang giữa chừng, bấm nhầm
  nút nhiều lần, mạng chậm — tất cả đều đã được tính tới và xử lý rõ ràng.
- **Có thể dùng bàn phím để thao tác toàn bộ luồng** — một bước quan trọng cho khả năng tiếp cận
  (accessibility), không chỉ dành cho người dùng chuột/cảm ứng.
- **Có cơ chế tự động ngăn trang web ngày càng nặng hơn** theo thời gian — bảo vệ trải nghiệm người
  dùng trên thiết bị di động về lâu dài.

## Giới hạn hiện tại — trung thực cần biết

- Đây là luồng **tối thiểu** — chưa có thanh toán thật, chưa có địa chỉ giao hàng, chưa có thuế/giảm
  giá, chưa có việc giữ chỗ tồn kho. Tính năng này chứng minh đường đi hoạt động được, không phải quy
  tắc kinh doanh đầy đủ của việc bán hàng thật.
- Chưa có việc xoá bớt sản phẩm khỏi giỏ hàng hay sửa số lượng trực tiếp — người mua sắm chỉ thêm và
  thanh toán ở giai đoạn này.
- Chưa có lịch sử đơn hàng — màn hình xác nhận chỉ hiện ra một lần ngay sau khi thanh toán, không xem
  lại được sau đó.
- Hiện tại chỉ có đúng một khách hàng doanh nghiệp và một người mua sắm giả lập (chưa có đăng nhập
  thật) — nền tảng multi-tenant thật sự (nhiều khách hàng doanh nghiệp dùng chung, dữ liệu tách biệt
  ở cấp lưu trữ) đã được ĐẶC TẢ nhưng **chưa được triển khai đầy đủ** ở bước này — một khoảng cách đã
  được ghi nhận rõ ràng, chờ quyết định ở bước tiếp theo, không phải bị bỏ sót trong im lặng.
