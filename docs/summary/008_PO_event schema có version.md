# Hợp đồng cho những "tin nhắn" giữa các mảng nghiệp vụ — có phiên bản, không âm thầm thay đổi

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 21 hạng mục công việc, xác minh bằng kiểm tra tự động.*

## Vấn đề trước đây

Khi các mảng nghiệp vụ khác nhau (ví dụ giỏ hàng và đơn hàng) cần báo cho nhau biết "vừa có một sự
kiện xảy ra" (ví dụ "đơn hàng vừa được đặt"), hình dạng của thông báo đó cần được thống nhất rõ ràng —
nếu không, mảng phát ra thông báo có thể vô tình thay đổi hình dạng của nó theo thời gian, làm hỏng
mọi mảng khác đang lắng nghe mà không hề hay biết.

## Giải pháp: một hợp đồng chính thức cho mỗi loại thông báo, có đánh số phiên bản rõ ràng

Hai loại thông báo quan trọng nhất — "đơn hàng vừa được đặt" và "giỏ hàng vừa được thanh toán" — giờ
đây có một hợp đồng chính thức, sống ở một nơi duy nhất mà mọi bên liên quan đều tham chiếu tới, không
phải do mảng phát ra tự định nghĩa riêng. Nếu cần thay đổi hợp đồng theo cách có thể làm hỏng bên đang
lắng nghe, thay đổi đó **bắt buộc phải mang một số phiên bản mới**, và phiên bản cũ vẫn tiếp tục hoạt
động trong một khoảng thời gian đã công bố trước — không ai bị "cắt cầu" đột ngột.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Hợp đồng của mỗi loại thông báo sống ở đúng một nơi** — không mảng nghiệp vụ nào tự định nghĩa
   riêng một bản sao của chính nó.
2. **Nếu ai đó cố thay đổi hợp đồng theo cách có thể phá vỡ bên đang lắng nghe** (ví dụ thêm một
   thông tin bắt buộc phải có) mà quên đánh số phiên bản mới, hệ thống **tự động chặn thay đổi đó lại**
   trước khi nó được đưa vào — không cần ai nhớ để tự kiểm tra bằng mắt.
3. **Khi một phiên bản mới thay thế phiên bản cũ**, phiên bản cũ vẫn tiếp tục hoạt động đúng như tài
   liệu đã công bố, trong khoảng thời gian đã định — bên nào chưa kịp cập nhật vẫn không bị gián đoạn.
4. **Nếu một bên lắng nghe nhận được thông báo có thêm thông tin mới mà nó chưa biết tới**, nó vẫn xử
   lý bình thường, không bị lỗi hay bỏ sót những thông tin nó đã hiểu — chỉ đơn giản bỏ qua phần lạ.

## Lợi ích kinh doanh

- **Các mảng nghiệp vụ có thể phát triển độc lập, không sợ làm hỏng nhau** — mỗi bên chỉ cần tuân
  theo đúng hợp đồng đã công bố.
- **Không có sự cố "thay đổi âm thầm làm hỏng bên khác"** — mọi thay đổi có rủi ro đều bị chặn tự động
  nếu chưa đánh số phiên bản đúng cách.
- **Nền tảng sẵn sàng cho việc giao tiếp bất đồng bộ giữa các mảng nghiệp vụ trong tương lai** — khi
  cơ chế truyền thông điệp thật sự được kết nối, hai hợp đồng này đã sẵn sàng để dùng ngay.

## Giới hạn hiện tại — trung thực cần biết

- Đây **chỉ là hợp đồng** — chưa có cơ chế truyền thông điệp thật sự nào được kết nối ở bước này. Hai
  loại thông báo này chưa thực sự được gửi/nhận qua hàng đợi tin nhắn trong hệ thống đang chạy; đó là
  công việc của một tính năng riêng trong lộ trình.
- Phạm vi chỉ giới hạn ở đúng hai loại thông báo quan trọng nhất ("đơn hàng vừa đặt" và "giỏ hàng vừa
  thanh toán") — các loại thông báo khác trong tương lai sẽ cần lặp lại đúng khuôn mẫu này riêng.
