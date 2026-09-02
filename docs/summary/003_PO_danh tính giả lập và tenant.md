# Mọi dữ liệu đều biết mình thuộc về ai — không có "kho chung" mơ hồ

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 39/39 hạng mục công việc, xác minh bằng các bài kiểm tra tự động chạy
thật.*

## Vấn đề trước đây

Nền tảng này được xây để phục vụ nhiều khách hàng doanh nghiệp khác nhau dùng chung một hệ thống
(mô hình multi-tenant) — nhưng máy chủ định danh thật (nơi xác thực "ai đang dùng, thuộc doanh nghiệp
nào") chưa tồn tại ở giai đoạn này. Nếu không có cơ chế nào đảm bảo mọi thao tác đọc/ghi dữ liệu đều
biết rõ nó đang phục vụ khách hàng doanh nghiệp nào, rủi ro dữ liệu của khách hàng này bị lẫn sang
khách hàng khác là có thật — và đây là loại lỗi rất khó phát hiện cho tới khi đã xảy ra.

## Giải pháp: xác định "thuộc về ai" một lần ở cửa vào, và bắt buộc mọi nơi phải biết trước khi chạm dữ liệu

Trong lúc chờ máy chủ định danh thật, hệ thống dùng một "danh tính giả lập" tạm thời — nhưng cơ chế
xoay quanh nó được xây **đúng như khi có danh tính thật**: thông tin "thuộc khách hàng doanh nghiệp
nào" được xác định đúng một lần ngay tại cửa vào, sau đó đi kèm rõ ràng qua mọi chặng xử lý tiếp theo.
Điểm quan trọng nhất: **bất kỳ thao tác nào chạm tới dữ liệu lưu trữ đều bắt buộc phải biết thông tin
đó trước** — nếu không biết, thao tác đó bị chặn ngay lập tức, không có "khách hàng mặc định" nào để
dùng tạm.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Một yêu cầu đi vào hệ thống qua cửa vào chính.** Ngay tại đó, hệ thống xác định yêu cầu này
   thuộc khách hàng doanh nghiệp nào.
2. **Thông tin đó đi theo yêu cầu qua mọi chặng xử lý tiếp theo** — không mảng nghiệp vụ nào phải tự
   đoán lại hay suy luận ra thông tin này.
3. **Nếu một chặng nào đó cố truy cập dữ liệu lưu trữ mà chưa biết thông tin này** (ví dụ do cửa vào
   bị bỏ qua, hoặc thông tin bị mất giữa đường), thao tác đó **thất bại ngay lập tức**, không âm thầm
   dùng một khách hàng "mặc định" nào.
4. **Khi máy chủ định danh thật thay thế danh tính giả lập sau này**, chỉ có bước "xác định thông tin
   đó ở đâu ra" thay đổi — toàn bộ cách thông tin đó đi theo yêu cầu và cách dữ liệu được bảo vệ giữ
   nguyên không đổi.

## Điều đặc biệt: đã kiểm chứng bằng cách rà soát toàn bộ mã nguồn, không chỉ vài ví dụ

Đội đã quét toàn bộ mã nguồn để xác nhận **không có một nơi nào** trong hệ thống có thể chạm tới dữ
liệu lưu trữ mà không yêu cầu biết trước thông tin khách hàng doanh nghiệp — không phải kiểm tra vài
trường hợp mẫu, mà là toàn bộ. Kết quả: zero ngoại lệ.

## Lợi ích kinh doanh

- **Rủi ro lẫn dữ liệu giữa các khách hàng doanh nghiệp gần như bằng không** — đây là một đảm bảo cấu
  trúc, không phải một quy tắc mọi người tự giác tuân thủ.
- **Dễ dàng thay máy chủ định danh giả lập bằng máy chủ định danh thật sau này** — vì cơ chế lan
  truyền và thực thi kiểm tra đã được xây đúng chuẩn ngay từ đầu, không phải làm lại (đã được chứng
  minh đúng khi tính năng "014 — Máy chủ định danh thật" triển khai sau này).
- **Dễ dò lỗi khi có sự cố** — thông tin khách hàng doanh nghiệp hiển thị trong nhật ký hệ thống ở
  mọi chặng, giúp truy vết một yêu cầu cụ thể xuyên suốt toàn hệ thống.

## Giới hạn hiện tại — trung thực cần biết

- Đây vẫn là **danh tính giả lập** — chưa có đăng nhập thật, chưa có mật khẩu. Việc thay thế bằng máy
  chủ định danh thật là một tính năng riêng trong lộ trình (đã hoàn thành sau đó, xem tài liệu
  "014 — Máy chủ định danh thật").
- Việc lan truyền thông tin khách hàng doanh nghiệp sang các sự kiện bất đồng bộ (hàng đợi tin nhắn)
  chưa nằm trong phạm vi này, vì hạ tầng đó chưa tồn tại tại thời điểm triển khai — sẽ áp dụng khi hạ
  tầng đó xuất hiện.
