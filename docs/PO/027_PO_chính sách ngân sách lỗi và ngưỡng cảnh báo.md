# Lố "ngân sách lỗi" thì có hệ quả thật — không còn là màn hình đỏ không ai hành động

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành phần chính — chính sách viết sẵn cho cả 7 bộ phận, cảnh báo tự động chạy
thật, đã diễn tập làm "cạn ngân sách" 1 bộ phận và thấy cảnh báo hiện đúng mốc. Phần "tự hồi phục sau 3
ngày" mới kiểm bằng dữ liệu thử, chưa quan sát qua nhiều ngày thật — xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Mỗi bộ phận đã cam kết bằng con số (độ trễ, tỷ lệ lỗi, độ khả dụng) và đã có 1 màn hình tra cứu thực tế
(tính năng 021). Nhưng khi thực tế **lố** cam kết thì **không có gì xảy ra cả**: không ai được báo, không
có định nghĩa "lố tới mức nào thì phải dừng lại", và cũng không ai biết khi nào được làm tiếp như bình
thường. Màn hình có đỏ thì cũng chỉ đỏ đó, chờ ai đó tình cờ mở ra xem.

## Giải pháp: biến cam kết thành 1 "ngân sách" có mốc cảnh báo và hệ quả rõ ràng

- **Mỗi bộ phận có 1 ngân sách lỗi theo tháng** (tháng lịch, giờ Việt Nam): được phép bao nhiêu yêu cầu
  lỗi hoặc quá chậm trước khi coi là vi phạm. Ngân sách đặt lại vào đầu mỗi tháng.
- **Cảnh báo tự động ở 3 mốc 50%, 75%, 100%** — hiện ngay trên màn hình người vận hành vẫn mở mỗi ngày,
  không cần ai tự đi soi số liệu.
- **"Cạn" có định nghĩa bằng con số và có hệ quả**: chỉ cần 1 ngân sách chạm 100% là bộ phận đó dừng
  nhận tính năng mới, chỉ làm việc nâng độ tin cậy — cho tới khi đạt cam kết **3 ngày liên tiếp**.
- **Chính sách viết sẵn ngay cạnh cam kết của từng bộ phận** — ai đọc cũng trả lời được "ai dừng, dừng
  cái gì, khi nào được tiếp tục" mà không phải hỏi lại.

## Trải nghiệm thực tế diễn ra như thế nào

1. **1 bộ phận bắt đầu lỗi nhiều hơn bình thường** — mỗi lỗi "tiêu" 1 phần ngân sách tháng; tới 50% thì
   màn hình hằng ngày hiện cảnh báo đầu tiên, tới 75% hiện cảnh báo thứ hai.
2. **Ngân sách chạm 100%** — màn hình hiện thêm bộ phận đó trong danh sách "cạn ngân sách — ưu tiên độ
   tin cậy"; đội dừng đưa tính năng mới vào bộ phận đó, tập trung sửa cho ổn định.
3. **Bộ phận ổn định trở lại** — đủ 3 ngày liên tiếp đạt cam kết thì tự rời khỏi danh sách "cạn", được
   làm tính năng mới trở lại, kể cả khi tháng chưa kết thúc.
4. **Sang tháng mới mà bộ phận vẫn chưa ổn** — ngân sách đặt lại đầy đủ nhưng bộ phận **không** tự được
   "xoá án": vẫn phải đủ 3 ngày đạt cam kết mới hồi phục.
5. **Muốn chắc cảnh báo hoạt động thật** — đội có công cụ chủ động gây lỗi có kiểm soát (bật/tắt được
   ngay, mặc định tắt) để diễn tập, đã thử thật: cảnh báo hiện sau khoảng 4–5 phút.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio`](../diagrams/027-error-budget-alerting-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Vi phạm kéo dài có hệ quả thật** — không còn tình trạng "biết là đỏ nhưng không ai làm gì".
- **Quyết định ưu tiên không còn phải tranh luận lại mỗi lần**: con số "cạn" và điều kiện hồi phục đã
  được thống nhất và viết ra từ trước.
- **Phát hiện sớm qua cảnh báo theo mốc** (50%, 75%) — còn thời gian xử lý trước khi cạn hẳn.
- **Cân bằng tốc độ và độ ổn định**: bộ phận ổn định cứ ra tính năng bình thường, chỉ bộ phận đang có vấn
  đề mới phải dừng lại.

Kết quả diễn tập thật và giới hạn hiện tại (lưu lượng thử nghiệm thấp nên cảnh báo bật rất nhanh; việc
"dừng tính năng mới" là cam kết quy trình, chưa có cơ chế tự chặn): xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/027_Architect_chính sách ngân sách lỗi và ngưỡng cảnh báo.md`](../architecture/027_Architect_chính%20sách%20ngân%20sách%20lỗi%20và%20ngưỡng%20cảnh%20báo.md).
