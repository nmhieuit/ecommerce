# Đơn hàng không bao giờ "biến mất" dù hệ thống sập đúng lúc đang xử lý

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành và đã chạy thật — 23/23 hạng mục công việc, xác minh bằng test tự động chạy
thật qua cơ sở dữ liệu và hàng đợi tin nhắn thật, cộng chạy dịch vụ thật kết nối hàng đợi tin nhắn thật.*

## Vấn đề trước đây

Khi hệ thống ghi nhận 1 đơn hàng thành công, nó cũng cần báo cho các bộ phận khác biết "có 1 đơn hàng
vừa được đặt" — để sau này phục vụ những việc như thông báo hay xử lý vận chuyển. Nhưng giữa lúc "ghi
nhận đơn hàng" và lúc "gửi thông báo đó ra ngoài" luôn có 1 khoảng thời gian rất ngắn. Nếu hệ thống gặp
sự cố đúng vào khoảng đó (ví dụ tiến trình bị dừng đột ngột), đơn hàng đã được ghi nhận nhưng thông báo
có thể **bị mất vĩnh viễn** — các bộ phận khác không bao giờ biết đơn hàng đó tồn tại.

## Giải pháp: ghi nhận và chuẩn bị thông báo luôn đi cùng nhau, không bao giờ "một nửa"

Việc ghi nhận đơn hàng và việc chuẩn bị gửi thông báo giờ đây luôn xảy ra cùng lúc, trong cùng 1 thao
tác không thể tách rời — hoặc cả hai cùng thành công, hoặc cả hai cùng không xảy ra. Nếu hệ thống sập
ngay sau khi ghi nhận nhưng trước khi gửi thông báo, khi khởi động lại nó **tự động phát hiện và gửi
nốt** thông báo còn thiếu — không cần ai can thiệp thủ công. Và nếu vì lý do nào đó cùng 1 thông báo bị
gửi 2 lần, bên nhận biết cách bỏ qua lần trùng lặp, không xử lý 2 lần.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Khách hàng đặt 1 đơn hàng thành công** — việc ghi nhận đơn hàng và việc chuẩn bị thông báo luôn đi
   cùng nhau, không tách rời.
2. **Nếu việc tạo đơn hàng thất bại** vì bất kỳ lý do gì — không có thông báo "ma" nào được tạo ra cho
   1 đơn hàng không hề tồn tại.
3. **Nếu hệ thống gặp sự cố đúng vào khoảnh khắc giữa ghi nhận và gửi thông báo** — khi khởi động lại,
   thông báo còn thiếu tự động được gửi tiếp, không cần ai phát hiện hay can thiệp thủ công.
4. **Nếu cùng 1 thông báo vô tình được gửi 2 lần** — bên nhận tự nhận ra và bỏ qua lần thứ hai, không
   tạo ra kết quả bị nhân đôi.

## Lợi ích kinh doanh

- **Không còn rủi ro "đơn hàng đã đặt nhưng hệ thống phía sau không hề hay biết"** chỉ vì 1 sự cố kỹ
  thuật thoáng qua.
- **Khả năng phục hồi tự động sau sự cố** — không cần người vận hành thức đêm can thiệp thủ công.
- **Nền tảng đáng tin cậy hơn cho các tính năng tương lai** cần biết "có đơn hàng mới" (ví dụ thông báo
  khách hàng, xử lý vận chuyển).

Phát hiện thật khi triển khai và giới hạn hiện tại (chỉ áp dụng cho việc đặt đơn hàng, chưa mở rộng
sang việc dọn giỏ hàng): xem [functional-debt.md](functional-debt.md).
