# Biết ngay service nào đang "lố ngân sách" hiệu năng đã cam kết — không phải đợi khách hàng than phiền

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

## Vấn đề trước đây

Mỗi bộ phận của hệ thống thực ra **đã có sẵn** 1 bản cam kết bằng số cụ thể về hiệu năng của mình — ví
dụ "phải phản hồi trong X mili-giây với 95% số lần gọi", "tỷ lệ lỗi phải dưới 0,1%", "phải hoạt động
99,9% thời gian trong tháng". Nhưng bản cam kết đó **không có gì bảo vệ**: không có gì ngăn 1 bộ phận
mới được thêm vào mà quên khai báo, hay 1 con số bị sửa lệch đi mà không ai để ý — và quan trọng hơn,
cũng không có cách nào **tra cứu nhanh** xem thực tế có đang đúng như đã cam kết hay không, ngoài việc
tự thu thập số liệu thủ công mỗi lần cần biết.

## Giải pháp: cam kết được bảo vệ tự động + có 1 nơi tra cứu thực tế duy nhất

- **Kiểm tra tự động** đảm bảo mọi bộ phận đều có đủ bản cam kết hiệu năng, đúng khuôn mẫu chuẩn của
  nền tảng — không sót bộ phận nào, kể cả bộ phận mới thêm sau này.
- **Bất kỳ sai khác nào so với chuẩn chung đều phải có lý do ghi rõ ngay tại chỗ** — không còn tình
  trạng "1 bộ phận âm thầm có tiêu chuẩn khác mà không ai biết vì sao".
- **1 nơi tra cứu duy nhất** cho biết thực tế mỗi bộ phận đang hoạt động ra sao **ngay lúc này**, đối
  chiếu trực tiếp với con số đã cam kết — không cần tự thu thập số liệu mỗi lần muốn biết.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Ai đó thêm 1 bộ phận mới vào hệ thống** — hệ thống tự nhắc nếu bộ phận đó quên khai báo cam kết
   hiệu năng, không cần chờ tới lúc có sự cố mới phát hiện ra.
2. **1 bộ phận muốn có tiêu chuẩn khác với số đông** (ví dụ vì bản chất công việc của nó khác) — bắt
   buộc phải ghi rõ lý do ngay tại chỗ khai báo, ai đọc cũng thấy được, không phải 1 sai khác âm thầm.
3. **Muốn biết ngay bây giờ bộ phận nào đang hoạt động tốt, bộ phận nào đang đuối** — mở đúng 1 nơi
   tra cứu, thấy ngay số liệu thực tế cạnh số đã cam kết, không cần hỏi ai hay tự tổng hợp số liệu.
4. **1 bộ phận đang gặp vấn đề thật** (ví dụ chậm bất thường) — con số thực tế thể hiện rõ ngay trong
   ngày phát sinh, kịp phát hiện trước khi trở thành sự cố lớn hoặc khách hàng than phiền.

## Điều đặc biệt (đã kiểm chứng thật)

- **Việc viết bài kiểm tra TRƯỚC khi biết kết quả đã tự sửa 2 điều đội ngũ tưởng nhầm** — không phải
  kiểm tra hình thức để hợp thức hoá kết luận có sẵn:
  1. Ban đầu tưởng "mọi bộ phận đã khai báo đầy đủ, đúng hết rồi" — chạy kiểm tra thật phát hiện đúng
     **1 bộ phận thiếu 1 trường bắt buộc** (phần phân loại của nó) — đã sửa ngay.
  2. Ban đầu tưởng "1 bộ phận cụ thể (lớp tổng hợp phía trước) đang là ngoại lệ, cần giải trình lý do"
     — chạy kiểm tra thật cho thấy điều đó **không đúng**: bộ phận đó chỉ đơn giản thuộc 1 nhóm tiêu
     chuẩn khác, tương đương chứ không "nới lỏng hơn" nhóm còn lại. Đã sửa lại đúng ghi chép, **không**
     bịa ra 1 lý do giả chỉ để có ví dụ — giữ đúng tinh thần "cam kết là thật, không phải hình thức".
- **Nơi tra cứu thực tế không phải xây mới — tận dụng đúng công cụ giám sát đã dựng trước đó**, đã đối
  chiếu khớp với dữ liệu gốc.
- **Đã tự tạo 1 đợt tải giả lập thật** để chứng minh nơi tra cứu phản ánh đúng khi có vấn đề: độ trễ đo
  được **tăng khoảng 95 lần** so với bình thường ngay khi tạo tải cao, thể hiện rõ ràng ngay trong cùng
  khoảng thời gian quan sát — không phải 1 con số tĩnh không bao giờ đổi.
- **Đã xác nhận phân biệt đúng "không có dữ liệu" với "không có lỗi"** — tránh tình trạng 1 bộ phận
  không hề có traffic lại hiển thị nhầm thành "đang hoạt động hoàn hảo".

## Lợi ích kinh doanh

- **Phát hiện sớm vấn đề hiệu năng** trước khi khách hàng phải lên tiếng than phiền.
- **Không còn tình trạng "tiêu chuẩn ngầm"** — mọi sai khác so với chuẩn chung đều minh bạch, có lý do
  ghi rõ, dễ rà soát lại theo thời gian.
- **Tiết kiệm thời gian tra cứu** — 1 nơi duy nhất, tra được trong vài giây, thay vì phải tự tổng hợp
  số liệu mỗi lần cần đánh giá.

## Giới hạn hiện tại

- **Chưa có cảnh báo tự động** — hệ thống hiện chỉ hỗ trợ "tra cứu khi cần", chưa tự động báo động
  ngay khi 1 bộ phận vượt ngân sách. Đây là 1 hạng mục riêng, sắp tới.
- **Chưa có cơ chế kiểm tra tự động chạy liên tục để đảm bảo nơi tra cứu luôn khớp đúng dữ liệu gốc** —
  việc đối chiếu hiện làm định kỳ/thủ công, không phải mỗi lần có thay đổi hệ thống.
- **1 chỉ tiêu (độ trễ ở mức hiếm gặp nhất, p99) của bộ phận xử lý đơn hàng hiện đo được khá gần với
  ngưỡng đã cam kết** ngay cả trong điều kiện hoạt động bình thường — đáng theo dõi tiếp, chưa phải vấn
  đề cần xử lý gấp.

Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`](../architecture/021_Architect_khai%20báo%20và%20đo%20SLO%20theo%20từng%20service.md).
