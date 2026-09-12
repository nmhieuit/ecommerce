# Hệ thống không còn "treo vô thời hạn" khi 1 bộ phận khác gặp sự cố

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

## Vấn đề trước đây

Khi 1 bộ phận của hệ thống gọi sang 1 bộ phận khác (ví dụ trang web gọi tới bộ phận xử lý giỏ hàng),
nếu bộ phận được gọi bị chậm hoặc ngừng phản hồi, có 2 rủi ro cùng lúc:

- **Chờ đợi vô thời hạn** — nếu không ai quy định trước "chờ tối đa bao lâu", request có thể treo rất
  lâu, làm cạn tài nguyên xử lý của bộ phận đang chờ, rồi lan sự cố ngược lên tới tận người dùng cuối.
- **Vẫn tiếp tục cố gắng gọi lại**, kể cả khi bộ phận kia đã rõ ràng đang gặp sự cố — vô tình dội thêm
  tải vào đúng lúc nó cần được để yên để tự phục hồi.

Có 1 khoảng hở kỹ thuật cụ thể đã âm thầm tồn tại: cơ chế "thử lại tự động" hiện có áp dụng cho **mọi**
loại yêu cầu, kể cả yêu cầu **tạo dữ liệu mới** (thêm hàng vào giỏ, đặt đơn hàng) — nếu yêu cầu đó thực
ra đã thành công nhưng phản hồi bị thất lạc trên đường về, việc thử lại có thể vô tình **tạo ra đơn
hàng trùng lặp**.

## Giải pháp: mọi lời gọi giữa các bộ phận đều có 3 lớp bảo vệ

- **Giới hạn thời gian chờ rõ ràng** — không còn lời gọi nào "chờ mãi", kể cả những lời gọi ít khi được
  để ý tới (ví dụ bộ phận xác thực gọi ra ngoài để kiểm tra định danh).
- **Tự động "ngắt mạch"** khi 1 bộ phận liên tục không phản hồi — các yêu cầu tiếp theo bị từ chối
  ngay lập tức thay vì chờ hết thời gian mỗi lần, giúp hệ thống phản ứng nhanh và không dồn ứ.
- **Tự động thử lại có chọn lọc** khi gặp sự cố thoáng qua — nhưng **chỉ với các yêu cầu an toàn để thử
  lại** (ví dụ xem danh sách sản phẩm), không còn áp dụng cho các yêu cầu tạo dữ liệu mới (thêm giỏ
  hàng, đặt đơn) — đóng đúng khoảng hở có thể gây trùng lặp đã nêu ở trên.

## Trải nghiệm thực tế diễn ra như thế nào

1. **1 bộ phận phía dưới bị chậm hoặc ngừng phản hồi** — bộ phận gọi nó không còn chờ vô thời hạn, mà
   nhận được phản hồi lỗi rõ ràng trong 1 khoảng thời gian đã định trước.
2. **Sự cố kéo dài** — hệ thống tự động "ngắt mạch", các yêu cầu tiếp theo bị từ chối ngay (không còn
   cố gắng kết nối thật mỗi lần), giúp phần còn lại của hệ thống vẫn phục vụ bình thường cho các chức
   năng không liên quan.
3. **Sự cố tự phục hồi** — hệ thống tự động thử kết nối lại sau 1 khoảng thời gian, không cần ai can
   thiệp thủ công để "mở mạch" trở lại.
4. **1 lỗi thoáng qua** (ví dụ mất kết nối tạm thời) khi xem thông tin — hệ thống tự thử lại và người
   dùng có thể không hề nhận ra đã có trục trặc. Nhưng khi **đặt hàng hoặc thêm giỏ hàng**, hệ thống
   không tự thử lại — tránh nguy cơ tạo dữ liệu trùng lặp.

## Lợi ích kinh doanh

- **Không còn nguy cơ 1 bộ phận gặp sự cố kéo sập cả hệ thống theo dây chuyền** — sự cố được khoanh
  vùng đúng ở bộ phận gặp vấn đề.
- **Giảm rủi ro đơn hàng/giỏ hàng bị trùng lặp** do lỗi kỹ thuật thoáng qua — bảo vệ trực tiếp trải
  nghiệm mua hàng và dữ liệu kinh doanh.
- **Tự phục hồi khỏi sự cố thoáng qua** mà không cần người vận hành can thiệp thủ công cho từng lần.

Bằng chứng đã kiểm chứng thật (2 lỗi thật tìm được, 1 đã tồn tại sẵn và 1 tiềm ẩn từ trước) và giới
hạn hiện tại: xem [functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ
thuật, xem
[`docs/architecture/020_Architect_timeout retry circuit breaker cho cuộc gọi ra ngoài.md`](../architecture/020_Architect_timeout%20retry%20circuit%20breaker%20cho%20cuộc%20gọi%20ra%20ngoài.md).
