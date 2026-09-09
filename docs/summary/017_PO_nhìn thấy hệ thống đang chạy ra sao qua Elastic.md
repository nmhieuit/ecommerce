# Nhìn thấy hệ thống đang chạy ra sao — quan sát tập trung qua Elastic

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành và đang hoạt động thật ở môi trường phát triển — xem phần "Giới hạn hiện
tại" để biết rõ ranh giới giữa "đã hoạt động" và "đã triển khai lên môi trường vận hành thật".*

## Vấn đề trước đây

Hệ thống gồm 6 bộ phận độc lập, mỗi bộ phận tự ghi nhật ký (log) và số liệu vận hành của riêng mình.
Muốn biết "hệ thống có đang khoẻ mạnh không", đội vận hành phải **vào từng bộ phận riêng lẻ để xem**
— không có 1 nơi duy nhất tổng hợp lại bức tranh toàn cảnh. Việc này vừa chậm, vừa dễ bỏ sót, vừa
không thể trả lời nhanh câu hỏi "1 đơn hàng cụ thể đã đi qua những đâu, mất bao lâu ở mỗi bước, và
chậm ở đâu nhất".

## Giải pháp: mọi bộ phận tự động gửi "báo cáo sức khoẻ" về 1 nơi duy nhất

Mỗi bộ phận của hệ thống giờ tự động gửi 3 loại thông tin quan sát về 1 kho tập trung (Elastic), thông
qua **1 thành phần dùng chung** thay vì mỗi bộ phận tự cấu hình riêng lẻ (tránh 6 cách làm khác nhau,
dễ trôi dạt theo thời gian):

- **Dấu vết hành trình (traces)** — 1 yêu cầu đi qua bao nhiêu bộ phận, theo thứ tự nào, mỗi bước mất
  bao lâu.
- **Số liệu vận hành (metrics)** — độ trễ, tỷ lệ lỗi, lưu lượng xử lý của từng bộ phận theo thời gian.
- **Nhật ký có cấu trúc (structured logs)** — không còn là các dòng chữ tự do khó tra cứu, mà là dữ
  liệu có cấu trúc, tra được theo bộ phận, theo khách hàng doanh nghiệp (tenant), theo mã theo dõi 1
  yêu cầu cụ thể (đã nói ở tính năng "Truy vết 1 yêu cầu xuyên suốt hệ thống").

## Trải nghiệm thực tế diễn ra như thế nào

1. **Khách hàng thao tác** (ví dụ đặt 1 đơn hàng) — yêu cầu đi qua các bộ phận như bình thường, người
   dùng không thấy khác biệt gì.
2. **Mỗi bộ phận tự động gửi báo cáo** (dấu vết, số liệu, nhật ký) về kho tập trung ngay trong lúc xử
   lý, không cần thao tác thêm.
3. **Đội vận hành mở 1 màn hình duy nhất** (Kibana, giao diện xem của kho Elastic), tra theo mã theo
   dõi của đơn hàng đó — thấy được **toàn bộ hành trình**, đủ cả 3 loại thông tin, không cần vào từng
   bộ phận riêng lẻ.
4. **Nếu có dấu hiệu bất thường** (1 bộ phận chậm hẳn, tỷ lệ lỗi tăng), đội vận hành thấy được ngay
   trên số liệu tổng hợp, không phải chờ người dùng báo cáo trước.

## Điều đặc biệt: đã kiểm chứng thật, không chỉ thiết kế trên giấy

- **Đặt 1 đơn hàng thật, tìm thấy đủ dấu vết trên Kibana.** Đội đã đặt 1 đơn hàng qua toàn hệ thống
  rồi tra trên Kibana — xác nhận thấy đủ dấu vết hành trình xuyên suốt các bộ phận tham gia, kèm số
  liệu và nhật ký tương ứng.
- **Rà soát và xác nhận không còn cách ghi log kiểu cũ.** Toàn bộ mã nguồn được rà lại để đảm bảo
  không còn dòng log nào ghi theo kiểu "câu chữ tự do lắp ráp" (khó tra cứu tự động) — chỉ còn cách
  ghi có cấu trúc.
- **Thử nghiệm "rút thành phần dùng chung ra xem có sao không".** Đội đã tạm thời gỡ bỏ thành phần
  quan sát dùng chung khỏi 1 bộ phận rồi xác nhận: bộ phận đó **thực sự mất khả năng gửi báo cáo** —
  chứng minh thành phần dùng chung này thực sự cần thiết cho việc quan sát hoạt động, không phải một
  lớp cấu hình trang trí có thể bỏ qua mà không ảnh hưởng gì.

## Lợi ích kinh doanh

- **1 nơi duy nhất để biết "hệ thống có đang ổn không"** — không cần đội kỹ thuật lần lượt kiểm tra
  từng bộ phận.
- **Chẩn đoán sự cố nhanh hơn nhiều** — thấy ngay bước nào chậm/lỗi trong 1 hành trình, thay vì phải
  đoán rồi kiểm tra từng nơi.
- **Cách ghi log thống nhất, không trôi dạt theo thời gian** — vì mọi bộ phận dùng chung 1 thành phần,
  không phải 6 cách làm khác nhau do 6 người viết riêng.
- **Không rò rỉ dữ liệu nhạy cảm vào log** — 1 trong các tiêu chí xác nhận của tính năng này là nhật ký
  không chứa thông tin cá nhân nhạy cảm dưới bất kỳ hình thức nào.

## Giới hạn hiện tại — trung thực cần biết

- **Kho Elastic/Kibana hiện chỉ chạy trên máy phát triển** (thông qua Docker, cùng cách toàn bộ hệ
  thống được chạy thử ở giai đoạn này) — **chưa phải 1 hạ tầng vận hành thật, luôn sẵn sàng** cho môi
  trường sản phẩm chính thức. Việc dựng hạ tầng quan sát thật cho môi trường vận hành là công việc
  hạ tầng riêng, chưa nằm trong phạm vi đã hoàn thành ở đây.
- Chi tiết kỹ thuật (cách thành phần dùng chung được nối tới kho Elastic, cấu hình cụ thể) dành cho
  đội kỹ thuật, xem
  [`docs/architecture/017_Architect_phát telemetry OTel qua ServiceDefaults tới Elastic.md`](../architecture/017_Architect_phát%20telemetry%20OTel%20qua%20ServiceDefaults%20tới%20Elastic.md).
