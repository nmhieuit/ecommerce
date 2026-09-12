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

## Lợi ích kinh doanh

- **Phát hiện sớm vấn đề hiệu năng** trước khi khách hàng phải lên tiếng than phiền.
- **Không còn tình trạng "tiêu chuẩn ngầm"** — mọi sai khác so với chuẩn chung đều minh bạch, có lý do
  ghi rõ, dễ rà soát lại theo thời gian.
- **Tiết kiệm thời gian tra cứu** — 1 nơi duy nhất, tra được trong vài giây, thay vì phải tự tổng hợp
  số liệu mỗi lần cần đánh giá.

Bằng chứng đã kiểm chứng thật (2 điều đội ngũ tưởng nhầm được viết-test-trước tự sửa, đợt tải giả lập
chứng minh nơi tra cứu phản ánh đúng) và giới hạn hiện tại: xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/021_Architect_khai báo và đo SLO theo từng service.md`](../architecture/021_Architect_khai%20báo%20và%20đo%20SLO%20theo%20từng%20service.md).
