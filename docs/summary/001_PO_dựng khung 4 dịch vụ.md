# Bốn khối nền tảng độc lập — mỗi mảng nghiệp vụ có "nhà riêng", không chia sẻ dữ liệu với ai

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 51/51 hạng mục công việc, xác minh bằng các bài kiểm tra tự động chạy
thật, không phải mô tả lý thuyết.*

## Vấn đề trước đây

Trước tính năng này, chưa có nền móng kỹ thuật nào cho bốn mảng nghiệp vụ cốt lõi của sàn thương mại
điện tử — khách hàng (parties), sản phẩm (products), giỏ hàng (baskets), đơn hàng (orders). Muốn bắt
đầu xây bất kỳ tính năng nào trong bốn mảng đó cũng phải khởi tạo từ con số không, và không có ranh
giới rõ ràng nào ngăn một mảng vô tình đọc/ghi nhầm dữ liệu của mảng khác — một rủi ro âm thầm, khó
phát hiện cho tới khi đã gây hậu quả thật.

## Giải pháp: bốn "khối nền tảng" độc lập, mỗi khối tự đứng vững một mình

Bốn mảng nghiệp vụ trên giờ đây mỗi mảng là một khối riêng biệt, có thể khởi động và hoạt động hoàn
toàn độc lập — không cần ba khối còn lại phải chạy cùng lúc. Quan trọng hơn: **mỗi khối có kho dữ liệu
của riêng mình, không khối nào có bất kỳ cách nào để chạm vào dữ liệu của khối khác** — đây là một
ranh giới cấu trúc, được xây ngay từ đầu, không phải một quy tắc "mọi người tự giác tuân thủ".

## Trải nghiệm thực tế diễn ra như thế nào

1. **Một người phát triển chỉ cần khởi động đúng một khối** (ví dụ chỉ khối "sản phẩm") để bắt đầu
   làm việc — không cần dựng cả bốn khối cùng lúc.
2. **Khối đó tự báo cáo tình trạng thật của mình** — không chỉ "tiến trình đang chạy", mà cả việc kho
   dữ liệu riêng của nó có thực sự kết nối được hay không. Nếu kho dữ liệu chưa sẵn sàng, khối đó báo
   "chưa sẵn sàng" một cách trung thực, không giả vờ khoẻ mạnh.
3. **Mọi mã nguồn liên quan tới một chức năng được gom lại một chỗ**, thay vì rải rác khắp nơi theo
   kiểu phân lớp kỹ thuật — giúp người mới tham gia dự án tìm đúng chỗ cần sửa nhanh hơn.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/001-scaffold-service-shells-flow-nghiep-vu.drawio`](../diagrams/001-scaffold-service-shells-flow-nghiep-vu.drawio))*

## Điều đặc biệt: ranh giới dữ liệu đã được kiểm chứng thật, không chỉ thiết kế trên giấy

Đội đã thử nghiệm cụ thể: cố tình để mã nguồn của một khối tìm cách chạm vào kho dữ liệu của khối
khác — và xác nhận không có bất kỳ đường nào (không thông tin đăng nhập, không kết nối, không thư viện
dùng chung) cho phép điều đó xảy ra. Đây là một đảm bảo cấu trúc, được kiểm tra lặp lại được, không
phải một lời hứa.

## Lợi ích kinh doanh

- **Bốn đội có thể làm việc song song mà không giẫm chân nhau** — mỗi mảng nghiệp vụ là một đơn vị
  triển khai độc lập, không phải chờ đợi lẫn nhau.
- **Rủi ro rò rỉ dữ liệu chéo giữa các mảng gần như bằng không** — vì về mặt cấu trúc, không có
  đường nào để một mảng chạm được dữ liệu của mảng khác, kể cả vô tình.
- **Nền móng vững cho mọi tính năng nghiệp vụ tiếp theo** — mọi công việc sau này (đăng nhập, giỏ
  hàng, đặt hàng...) đều xây trên nền bốn khối này, không phải làm lại từ đầu.

## Giới hạn hiện tại — trung thực cần biết

- Đây thuần tuý là bước **dựng khung** — bốn khối chỉ mới báo cáo được tình trạng của chính mình,
  chưa có bất kỳ chức năng nghiệp vụ thật nào (chưa có sản phẩm để xem, chưa có giỏ hàng để thêm...).
  Các chức năng đó là những bước tiếp theo, xây trên đúng nền móng này.
- Việc xác định "một người dùng thuộc khách hàng doanh nghiệp nào" (để cách ly dữ liệu giữa các khách
  hàng doanh nghiệp khác nhau dùng chung nền tảng) chưa nằm trong phạm vi này — đó là công việc của
  bước tiếp theo trong lộ trình.
