# Ngân sách lỗi tính theo tuần — sự cố trong tuần được nhìn thấy và xử lý ngay trong tuần

> **Cập nhật spec 033**: ngân sách lỗi chỉ tính request nghiệp vụ; health check (`/health…`) không còn tính vào mẫu số hay số request xấu. Service không sẵn sàng có cảnh báo riêng `health-failure`. Xem [033 PO](033_PO_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — cả 7 bộ phận chuyển sang ngân sách theo tuần, cảnh báo và màn hình hằng
ngày đã tính theo tuần, đã diễn tập "đốt" ngân sách của cả 7 bộ phận và thấy cảnh báo hiện đúng mốc.
Khoảnh khắc chuyển tuần thật (00:00 thứ Hai) chưa quan sát được — xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Tính năng 027 đã cho mỗi bộ phận một "ngân sách lỗi" — nhưng tính theo **tháng**. Một sự cố vài giờ bị
pha loãng trong cả tháng: đến cuối tháng mới thấy rõ, và một khi đã "cạn" thì phải chờ rất lâu mới tới
lượt ngân sách đặt lại. Nhịp làm việc của đội lại theo **tuần**, nên con số theo tháng không khớp với lúc
đội ngồi lại quyết định ưu tiên.

## Giải pháp: chuyển ngân sách sang tuần lịch, và điều chỉnh cam kết cho khớp

- **Ngân sách tính theo tuần lịch giờ Việt Nam** — từ 00:00 thứ Hai tới 23:59 Chủ nhật, đặt lại đầy đủ
  vào mỗi sáng thứ Hai. Áp dụng cho cả 4 loại ngân sách (khả dụng, lỗi, chậm vừa, chậm nhiều).
- **Cam kết chung của nền tảng được điều chỉnh**: độ khả dụng **99% mỗi tuần**, tỷ lệ lỗi **dưới 1%**
  (trước là 99.9% mỗi tháng và dưới 0.1%). Cam kết về độ chậm giữ nguyên. Đây là thay đổi "luật chơi" của
  cả nền tảng nên được ghi vào văn bản quy tắc chung của dự án (phiên bản 2.0.0).
- **Mọi quy tắc khác giữ nguyên**: cảnh báo ở 3 mốc 50% / 75% / 100%; chạm 100% là bộ phận dừng nhận
  tính năng mới, chỉ làm việc nâng độ ổn định; được làm tiếp khi phần ngân sách đã tiêu trong tuần
  **xuống dưới 75%** *(đổi 2026-10-09; trước đây: 3 ngày liên tiếp đạt cam kết)*; sang tuần mới không tự động "xoá án".
- **Diễn tập vẫn tính như sự cố thật** — lỗi do chủ động gây ra để luyện tập cũng tiêu ngân sách.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Thứ Hai 00:00** — ngân sách của mọi bộ phận đầy lại; cảnh báo mốc của tuần trước tắt.
2. **Một bộ phận lỗi nhiều trong tuần** — tới 50% rồi 75% ngân sách tuần, màn hình hằng ngày hiện cảnh
   báo, đội biết sớm ngay trong tuần thay vì cuối tháng.
3. **Chạm 100%** — bộ phận vào danh sách "cạn ngân sách — ưu tiên độ tin cậy", đội dừng đưa tính năng
   mới vào bộ phận đó.
4. **Sang tuần mới mà chưa ổn** — ngân sách đầy lại nhưng bộ phận vẫn ở trạng thái "cạn" (đang hồi phục) cho
   tới khi tuần mới có yêu cầu thật và phần đã tiêu dưới 75%.
5. **Muốn chắc cảnh báo hoạt động thật** — đội đã diễn tập gây lỗi có kiểm soát cho cả 7 bộ phận cùng
   lúc: cảnh báo hiện sau khoảng 2–5 phút ở từng mốc, màn hình và cảnh báo khớp con số.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/029-error-budget-weekly-flow-nghiep-vu.drawio`](../diagrams/029-error-budget-weekly-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Phản ứng nhanh hơn**: sự cố trong tuần hiện rõ ngay trong tuần, khớp nhịp họp ưu tiên hằng tuần.
- **Đóng băng ngắn hơn khi đã ổn định lại**: ngân sách đầy lại mỗi tuần, không phải chờ hết tháng.
- **Cam kết thực tế hơn với quy mô hiện tại**: con số 99% / 1% vừa sức môi trường đang vận hành, nên
  cảnh báo có ý nghĩa thay vì đỏ liên tục.
- **Một nguồn sự thật**: quy tắc chung, cam kết từng bộ phận, cảnh báo và màn hình cùng nói một con số.

Giới hạn hiện tại (lưu lượng thử nghiệm thấp nên vài lỗi đã đủ vượt mốc; một bộ phận "cạn" kéo dài hơn
2 tuần có thể tự rời danh sách; khoảnh khắc chuyển tuần thật chưa quan sát): xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/029_Architect_ngân sách lỗi theo tuần lịch.md`](../architecture/029_Architect_ngân%20sách%20lỗi%20theo%20tuần%20lịch.md).
