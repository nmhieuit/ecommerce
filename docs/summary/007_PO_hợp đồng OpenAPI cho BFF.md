# Hợp đồng làm luật — giao diện người dùng không thể "trôi lệch" khỏi những gì hệ thống thật sự trả về

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 15/15 hạng mục công việc, xác minh bằng kiểm tra tự động.*

## Vấn đề trước đây

Khi giao diện người dùng (SPA) và hệ thống phía sau (BFF) được xây bởi những người khác nhau, có một
rủi ro âm thầm: nếu ai đó viết tay mã gọi API thay vì để nó được sinh ra tự động từ một "hợp đồng"
chính thức, mã đó có thể dần dần lệch khỏi thực tế mà hệ thống phía sau thực sự trả về — một lỗi rất
khó phát hiện cho tới khi người dùng gặp sự cố thật.

## Giải pháp: một hợp đồng chính thức, và mã giao diện được sinh ra từ đúng hợp đồng đó — không viết tay

Với ba mảng nghiệp vụ (sản phẩm, giỏ hàng, đơn hàng), mọi đường giao tiếp giữa giao diện và hệ thống
phía sau đều dựa trên một tài liệu hợp đồng chính thức, mô tả chính xác hình dạng dữ liệu ra/vào. Mã
giao diện gọi tới các đường giao tiếp này **không do con người viết tay** — nó được sinh ra tự động từ
chính hợp đồng đó, nên không có cách nào để nó lệch khỏi thực tế.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Mọi thay đổi ở một đường giao tiếp phải đi kèm cập nhật hợp đồng** — không có chuyện code chạy
   trước, hợp đồng viết sau (hoặc quên viết).
2. **Khi hợp đồng thay đổi, mã giao diện được sinh lại tự động** bằng một lệnh duy nhất, trong chưa
   tới một phút — không ai phải tự tay sửa mã đã sinh ra.
3. **Nếu hệ thống phía sau trả về thêm một thông tin mới mà giao diện chưa biết tới**, giao diện vẫn
   hoạt động bình thường, không bị lỗi hay sập — đây là điều kiện quan trọng để có thể bổ sung tính
   năng mới ở phía sau mà không làm hỏng giao diện đang chạy.

## Lợi ích kinh doanh

- **Giảm hẳn một loại lỗi rất khó phát hiện** — "giao diện gọi sai vì hiểu nhầm hình dạng dữ liệu" —
  vì mã gọi không còn do con người tự gõ tay.
- **Thêm tính năng mới ở phía sau an toàn hơn** — nhờ khả năng "chấp nhận thông tin lạ mà không sập",
  đội phía sau có thể bổ sung dữ liệu mới mà không lo phá vỡ giao diện đang chạy.
- **Tốc độ làm việc nhanh hơn** — sinh lại toàn bộ mã giao diện từ hợp đồng chỉ mất chưa tới một phút,
  thay vì phải tự tay rà soát và sửa từng chỗ.

## Giới hạn hiện tại — trung thực cần biết

- Phạm vi tính năng này chỉ gồm ba mảng nghiệp vụ (sản phẩm, giỏ hàng, đơn hàng) — mảng khách hàng,
  luồng thanh toán, và các đường kiểm tra sức khoẻ hệ thống chưa áp dụng cơ chế này, theo đúng phạm vi
  đã thống nhất từ đầu.
- Đây chủ yếu là một bước **xác nhận và củng cố** một thực hành đã có sẵn từ trước (không phải xây từ
  số 0) — phần việc mới thực sự duy nhất là đảm bảo giao diện không sập khi gặp dữ liệu lạ, phần còn
  lại là kiểm chứng lại những gì đã đúng từ trước vẫn còn đúng.
