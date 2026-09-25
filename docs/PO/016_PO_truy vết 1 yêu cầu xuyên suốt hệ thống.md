# Truy vết một yêu cầu xuyên suốt hệ thống — từ trình duyệt tới log

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành và đang hoạt động thật — đã kiểm chứng bằng cách gõ 1 đơn hàng thật rồi tra
lại đúng dấu vết của nó xuyên suốt nhiều bộ phận, không chỉ trên giấy.*

## Vấn đề trước đây

Hệ thống được ghép từ 6 bộ phận độc lập (cổng vào, tầng tổng hợp, và 4 bộ phận xử lý nghiệp vụ), mỗi
bộ phận tự ghi nhật ký (log) riêng của mình. Khi 1 yêu cầu của khách hàng đi qua nhiều bộ phận và có
sự cố xảy ra ở đâu đó giữa chừng, **không có cách nào chắc chắn nối lại đúng các dòng log của từng bộ
phận thành 1 câu chuyện duy nhất** — mỗi bộ phận tự đánh số/không đánh số theo cách riêng, không có
"sợi chỉ" nào xuyên suốt. Người điều tra sự cố phải đoán, dựa vào thời gian gần đúng, thay vì biết
chắc "đây chính xác là log của cùng 1 yêu cầu".

Vấn đề còn nặng hơn khi 1 phần xử lý diễn ra **không đồng thời** (ví dụ 1 hành động sẽ kích hoạt 1
việc xử lý nền sau đó) — nếu không có gì nối 2 phần lại, việc điều tra coi như đứt đoạn ngay tại ranh
giới đó.

## Giải pháp: một "mã theo dõi" duy nhất, đi theo trọn vẹn hành trình 1 yêu cầu

Ngay khi 1 yêu cầu vào cổng chính của hệ thống, nó được gắn 1 **mã theo dõi (correlation ID)** — nếu
khách hàng/thiết bị đã tự mang theo 1 mã hợp lệ thì giữ nguyên, nếu chưa có thì hệ thống tự sinh ra
1 mã mới ngay tại đó. Mã này sau đó **đi theo nguyên vẹn** qua mọi bộ phận yêu cầu này chạm tới — kể
cả phần xử lý nền diễn ra sau đó không đồng thời — và được ghi kèm vào MỌI dòng log tại MỌI bộ phận.

Điểm mới quan trọng nhất: mã này giờ còn **hiển thị được ngay trên trình duyệt** của người dùng (qua
công cụ kiểm tra mạng có sẵn của trình duyệt) — không còn là thứ chỉ đội kỹ thuật nhìn thấy được.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Khách hàng thao tác trên trình duyệt** (ví dụ đặt 1 đơn hàng) — yêu cầu rời trình duyệt, vào cổng
   chính của hệ thống.
2. **Cổng chính gắn mã theo dõi** cho yêu cầu đó (sinh mới nếu chưa có).
3. **Mã này đi theo yêu cầu** qua tầng tổng hợp, rồi tới đúng bộ phận nghiệp vụ xử lý nó — mỗi bộ phận
   ghi log của mình kèm đúng mã này.
4. **Nếu hành động đó còn kích hoạt 1 việc xử lý nền** (ví dụ ghi nhận đơn hàng vừa đặt để xử lý sau),
   mã theo dõi vẫn đi theo, không bị "đứt" khi chuyển từ xử lý ngay sang xử lý nền.
5. **Mã này cũng trả về cho trình duyệt** — người dùng hoặc người hỗ trợ có thể nhìn thấy nó ngay trên
   công cụ kiểm tra mạng của trình duyệt.
6. **Khi có sự cố**, người điều tra chỉ cần lấy đúng mã đó (từ trình duyệt hoặc từ báo cáo người dùng)
   và tra trên hệ thống log tập trung — thấy được **toàn bộ hành trình**, đúng thứ tự, không thiếu bộ
   phận nào.

## Lợi ích kinh doanh

- **Rút ngắn đáng kể thời gian chẩn đoán sự cố** — không còn phải đoán hoặc ghép log thủ công từ nhiều
  nguồn, chỉ cần 1 mã tra cứu duy nhất.
- **Đội hỗ trợ khách hàng có thể làm việc trực tiếp với đội kỹ thuật bằng 1 mã cụ thể** — khách hàng
  (hoặc người hỗ trợ) nhìn thấy mã này ngay trên trình duyệt, không cần đội kỹ thuật tự suy đoán yêu
  cầu nào đang gặp vấn đề.
- **Nền tảng bắt buộc cho mọi khả năng quan sát hệ thống sau này** — các bước tiếp theo (xem chi tiết
  hiệu năng, cảnh báo tự động...) đều cần có "sợi chỉ" này làm nền, không thể xây trước nó.

Bằng chứng đã kiểm chứng thật (gõ 1 đơn hàng thật và tra lại dấu vết, phát hiện và vá 1 lỗ hổng thật)
và giới hạn hiện tại: xem [functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho
đội kỹ thuật, xem
[`docs/architecture/016_Architect_lan truyền correlation ID từ edge đến frontend.md`](../architecture/016_Architect_lan%20truyền%20correlation%20ID%20từ%20edge%20đến%20frontend.md).
