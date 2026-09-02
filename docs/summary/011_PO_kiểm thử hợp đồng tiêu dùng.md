# Bên "nói" phải tự kiểm tra mình trước — không để bên "nghe" phát hiện lỗi giùm

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành, xác minh bằng kiểm tra tự động.*

## Vấn đề trước đây

Khi một mảng nghiệp vụ (ví dụ "sản phẩm") thay đổi hình dạng dữ liệu nó trả về, và một mảng khác (ví
dụ cửa vào chung của hệ thống) đang dựa vào hình dạng cũ đó, ai là người phát hiện ra sự cố này trước
— và phát hiện ở đâu? Nếu chỉ phát hiện được khi bên đang dựa vào nó gặp lỗi (hoặc tệ hơn, khi người
dùng thật gặp lỗi), thì đã quá muộn.

## Giải pháp: bên phát ra dữ liệu tự kiểm tra chính mình, ngay trong công đoạn xây dựng của chính nó

Với ba đường giao tiếp quan trọng nhất (giữa cửa vào chung và ba mảng nghiệp vụ: sản phẩm, giỏ hàng,
đơn hàng) và một cặp thông báo nội bộ tiêu biểu, mỗi bên **phát ra** dữ liệu giờ đây tự kiểm tra lại
chính dữ liệu thật nó tạo ra, so với đúng những gì bên **nhận** đã công bố là cần. Nếu có sai lệch,
**chính công đoạn xây dựng của bên phát ra** sẽ báo lỗi và dừng lại — không phải công đoạn xây dựng
của bên nhận, và càng không phải khi hệ thống đã chạy thật.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Mỗi mảng nghiệp vụ phát dữ liệu tự kiểm tra dữ liệu THẬT nó tạo ra** — không phải một bản mô
   phỏng viết tay, mà là hành vi thật của chính nó.
2. **Nếu một thay đổi làm sai lệch những gì bên nhận cần**, công đoạn xây dựng của chính bên phát hiện
   ra và chặn lại **trước khi thay đổi đó được đưa vào hệ thống chính** — không cần chờ bên nhận báo
   lỗi.
3. **Nếu chỉ đơn giản là thêm một thông tin mới** mà bên nhận chưa biết tới (không phá vỡ gì cả), bài
   kiểm tra vẫn cho qua bình thường — đúng nguyên tắc "chấp nhận thông tin lạ mà không sập" đã áp dụng
   xuyên suốt nền tảng.
4. **Có thể liệt kê đầy đủ mọi đường giao tiếp quan trọng và xác nhận từng đường đã có bài kiểm tra
   bảo vệ** trong vòng vài phút, không cần đọc mã nguồn của từng mảng nghiệp vụ.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/011-consumer-contract-tests-flow-nghiep-vu.drawio`](../diagrams/011-consumer-contract-tests-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Lỗi được phát hiện sớm nhất có thể, đúng ngay tại nguồn gây ra nó** — không đợi tới khi lan sang
  bên khác hoặc tới tay người dùng thật.
- **Các đội có thể phát triển độc lập, tự tin hơn** — mỗi đội biết chắc nếu mình vô tình phá vỡ một
  cam kết, chính công đoạn xây dựng của đội mình sẽ báo ngay, không phải đợi đội khác phàn nàn.
- **Có cách kiểm tra nhanh xem còn "lỗ hổng" nào chưa được bảo vệ hay không** — dễ dàng rà soát định
  kỳ, không phải đoán mò.

## Giới hạn hiện tại — trung thực cần biết

- Phạm vi hiện tại chỉ dừng ở đúng bốn đường giao tiếp quan trọng nhất trong luồng nghiệp vụ cốt lõi —
  mở rộng ra các đường giao tiếp khác trong tương lai là công việc riêng, chưa nằm trong phạm vi này.
- Với cặp thông báo nội bộ (event), việc kiểm tra hiện chỉ so sánh dữ liệu được tạo ra với những gì
  bên nhận cần — **chưa có hệ thống hàng đợi tin nhắn thật nào đang chạy** để gửi/nhận thông báo đó
  trong môi trường thực tế; đây là bước thí điểm đi trước, chuẩn bị cho khi hạ tầng đó được kết nối.
