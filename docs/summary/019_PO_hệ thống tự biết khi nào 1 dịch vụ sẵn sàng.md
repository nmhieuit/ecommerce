# Hệ thống tự biết khi nào 1 bộ phận "sẵn sàng" và khi nào "bị treo"

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành ở mức "bản thiết kế đã kiểm chứng kỹ" (template triển khai + kiểm tra tự
động), CHƯA từng được áp dụng lên 1 hệ thống Kubernetes thật đang chạy — xem "Giới hạn hiện tại".*

## Vấn đề trước đây (và sẽ gặp phải khi triển khai lên Kubernetes thật trong tương lai)

Khi hệ thống được vận hành trên nền tảng điều phối tự động (Kubernetes) trong tương lai, nền tảng đó
cần tự biết 2 điều về mỗi bộ phận: **"nó còn sống không"** và **"nó đã sẵn sàng nhận khách chưa"**.
Nếu không khai báo rõ 2 điều này:

- 1 bộ phận vừa khởi động (còn đang kết nối cơ sở dữ liệu, chưa sẵn sàng) vẫn có thể bị nhận request
  ngay — người dùng gặp lỗi dù bộ phận đó "sắp" hoạt động bình thường.
- 1 bộ phận bị treo (không phản hồi nữa, nhưng tiến trình chưa chết hẳn) sẽ **không ai tự phát hiện**
  để khởi động lại — cần người trực theo dõi thủ công 24/7.

## Giải pháp: mỗi bộ phận tự trả lời 2 câu hỏi sức khoẻ riêng biệt, nền tảng tự hành động theo đó

Mỗi bộ phận của hệ thống giờ khai báo rõ 2 "câu hỏi" mà nền tảng điều phối có thể tự hỏi định kỳ:

- **"Mày còn sống không?"** (liveness) — nếu trả lời sai liên tục vượt quá ngưỡng cho phép, nền tảng
  hiểu là bộ phận đó đã treo/deadlock, và **tự khởi động lại nó** — không cần người can thiệp.
- **"Mày đã sẵn sàng nhận khách chưa?"** (readiness) — nếu chưa sẵn sàng (ví dụ đang chờ kết nối cơ
  sở dữ liệu), nền tảng **tạm thời không gửi request nào** tới bộ phận đó, cho tới khi nó tự báo "sẵn
  sàng rồi".

Điểm quan trọng: 2 câu hỏi này **tách biệt hoàn toàn** — 1 bộ phận có thể "còn sống" (không bị khởi
động lại) nhưng "chưa sẵn sàng" (tạm không nhận khách) cùng lúc, ví dụ khi cơ sở dữ liệu nó phụ thuộc
đang gặp sự cố tạm thời. Nếu trộn lẫn 2 câu hỏi này, 1 sự cố tạm thời của cơ sở dữ liệu có thể khiến
nền tảng hiểu nhầm là "bộ phận đã chết" và khởi động lại nó — trong khi thực ra chính bộ phận đó vẫn
hoàn toàn khoẻ mạnh, chỉ đang chờ 1 dependency bên ngoài.

## Trải nghiệm thực tế diễn ra như thế nào (khi triển khai lên Kubernetes thật)

1. **1 bộ phận mới được triển khai hoặc khởi động lại** — nền tảng chưa cho nó nhận bất kỳ request
   nào cho tới khi nó tự báo "sẵn sàng".
2. **Trong lúc cập nhật phiên bản mới** (rolling update) — bản cũ vẫn tiếp tục phục vụ khách hàng cho
   tới khi bản mới thật sự sẵn sàng, không có khoảnh khắc nào "không ai phục vụ cả".
3. **Nếu 1 bộ phận bị treo giữa chừng** — nền tảng tự phát hiện qua câu hỏi "còn sống không" và tự
   khởi động lại nó, không cần người trực đêm can thiệp thủ công.
4. **Nếu cơ sở dữ liệu tạm thời gặp sự cố** — bộ phận phụ thuộc nó chỉ tạm "không nhận khách" (qua câu
   hỏi sẵn sàng), KHÔNG bị khởi động lại nhầm — vì bản thân tiến trình vẫn khoẻ mạnh, chỉ đang chờ.

## Điều đặc biệt: đã kiểm chứng thật, không chỉ thiết kế trên giấy

- **Ngưỡng thời gian không phải số tự nghĩ ra** — thời gian chờ/số lần thử lại cho các bộ phận có cơ
  sở dữ liệu được lấy đúng từ số liệu đã kiểm chứng qua vận hành thật của hệ thống (thời gian cơ sở dữ
  liệu cần để phục hồi sau khi khởi động lại), không phải ước lượng cảm tính.
- **Có bộ kiểm tra tự động xác nhận đúng 4 quy tắc cho MỌI bộ phận, không sót cái nào**: cả 2 câu hỏi
  sức khoẻ đều được khai báo; đường dẫn kiểm tra đúng, không lẫn lộn 2 câu hỏi với nhau; nhóm bộ phận
  có cơ sở dữ liệu có ngưỡng chờ dài hơn nhóm không có, đúng thực tế khởi động của chúng; và khi cập
  nhật phiên bản, không có khoảnh khắc nào bộ phận cũ bị rút đi trước khi bộ phận mới sẵn sàng.
- **Có lớp kiểm tra thứ hai, độc lập** — không chỉ tin vào 1 bộ kiểm tra tự viết, mà còn dùng đúng
  công cụ chuẩn của Kubernetes để xác nhận bản thiết kế triển khai hợp lệ về mặt kỹ thuật, không chỉ
  đúng quy ước nghiệp vụ.

## Lợi ích kinh doanh

- **Không còn downtime khi triển khai phiên bản mới** — khách hàng không bao giờ bị dội vào 1 bộ phận
  chưa sẵn sàng.
- **Tự phục hồi khi có sự cố treo tiến trình** — giảm thời gian gián đoạn dịch vụ, giảm áp lực trực
  đêm cho đội vận hành.
- **Không khởi động lại nhầm** khi chỉ 1 dependency bên ngoài (như cơ sở dữ liệu) gặp sự cố tạm thời —
  tránh làm sự cố lan rộng hơn mức cần thiết.

## Giới hạn hiện tại — trung thực cần biết, đây là phần quan trọng nhất

**Chưa có 1 hệ thống Kubernetes thật nào đang chạy hệ thống này** — những gì đã hoàn thành là **bản
thiết kế triển khai đã kiểm chứng kỹ** (khai báo đúng, đã lint bằng công cụ chuẩn của ngành, có bộ
test tự động canh giữ quy ước), sẵn sàng để áp dụng lên 1 cluster thật ngay khi cluster đó tồn tại —
nhưng bản thân việc "áp dụng lên 1 hệ thống Kubernetes thật" vẫn chưa xảy ra. Đây là công việc hạ tầng
riêng, đi cùng nhịp với việc dựng kho bí mật trung tâm đã nhắc ở tính năng "Bỏ hẳn mật khẩu viết cứng
trong code".

Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/019_Architect_liveness readiness probe cho mọi service.md`](../architecture/019_Architect_liveness%20readiness%20probe%20cho%20mọi%20service.md).
