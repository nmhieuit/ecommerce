# Hai màn hình theo dõi: một để xử lý sự cố đang diễn ra, một để biết hạn mức lỗi của tuần còn bao nhiêu

> **Cập nhật spec 033**: ngân sách lỗi chỉ tính request nghiệp vụ; health check (`/health…`) không còn tính vào mẫu số hay số request xấu. Service không sẵn sàng có cảnh báo riêng `health-failure`. Xem [033 PO](033_PO_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — màn hình hằng ngày cũ đã được tách thành hai màn hình và bị bỏ; cả hai chạy
trên môi trường thử nghiệm với số liệu thật. Một số giới hạn còn lại xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Có **một** màn hình duy nhất tên "SLO vận hành hằng ngày" cho cả hai việc rất khác nhau. Khi một bộ phận
đang lỗi và người trực chọn "xem 15 phút gần nhất", chỉ một nửa màn hình đổi theo: phần còn lại vẫn nhìn
vào cả tuần, cả tháng hoặc "15 phút cố định". Người trực dễ hiểu nhầm đúng lúc cần chính xác nhất. Ngược
lại, người muốn biết "tuần này còn được phép lỗi thêm bao nhiêu" lại phải nhìn lẫn trong màn hình điều tra
và có thể vô tình kéo mất con số tuần chỉ vì đổi khoảng thời gian.

## Giải pháp: hai màn hình, mỗi màn hình một việc

- **Màn hình "Xử lý sự cố"** — *mọi* ô đều đi theo khoảng thời gian người trực chọn (mặc định 1 giờ gần
  nhất, tự làm mới mỗi phút). Từ trên xuống: bộ phận nào đang vượt cam kết và cảnh báo nhanh đang bật;
  lỗi theo phút của từng bộ phận, độ chậm theo phút, lượng truy cập cùng các lượt bị từ chối đăng nhập /
  phân quyền; bộ phận nào đang gọi bộ phận khác mà bị lỗi hoặc chậm; và danh sách 50 dòng nhật ký lỗi gần
  nhất, mỗi dòng bấm một lần là mở được toàn bộ lượt xử lý của yêu cầu đó để lần ra nguyên nhân.
- **Màn hình "Ngân sách lỗi tuần"** — *cố định* theo tuần lịch giờ Việt Nam (00:00 thứ Hai tới 23:59 Chủ
  nhật), không đổi khi người xem kéo khoảng thời gian. Cho thấy mỗi bộ phận đã tiêu bao nhiêu phần trăm
  từng loại ngân sách, còn lại bao nhiêu phần trăm và còn được phép bao nhiêu yêu cầu xấu, tình hình theo
  từng ngày trong tuần và đã tiêu dồn tới đâu từ thứ Hai. Có thể chọn xem **tuần này** hoặc **tối đa 3
  tuần trước**.
- Hai màn hình **có đường dẫn sang nhau** ở đầu trang, để từ "ngân sách sắp cạn" nhảy sang điều tra và
  ngược lại.
- Màn hình cũ **đã bị bỏ**; mọi tài liệu từng trỏ tới nó nay trỏ tới hai màn hình mới.
- Cảnh báo phát hiện nhanh (028) được **đóng khung bằng kiểm thử tự động** như các cảnh báo ngân sách: nếu
  ai đó sửa nhầm ngưỡng hay chu kỳ của nó thì kiểm thử báo đỏ ngay.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Có cảnh báo hoặc người dùng báo chậm** — người trực mở "Xử lý sự cố", thấy ngay bộ phận nào vượt cam
   kết và đang bật cảnh báo nhanh.
2. **Thu hẹp** — nhìn đường lỗi theo phút để biết lỗi bắt đầu lúc nào, xem bảng "bộ phận nào gọi bộ phận
   nào bị lỗi/chậm" để biết gốc nằm ở đâu, rồi đọc nhật ký lỗi gần nhất.
3. **Lần theo một yêu cầu cụ thể** — bấm mã yêu cầu trong dòng nhật ký để xem toàn bộ các bước xử lý của
   chính yêu cầu đó, kèm mã tương quan để đối chiếu với báo cáo của người dùng.
4. **Cân nhắc quyết định** — bấm sang "Ngân sách lỗi tuần" xem bộ phận đó đã tiêu bao nhiêu và còn được
   phép bao nhiêu yêu cầu xấu: gần cạn thì đội cân nhắc dừng nhận tính năng mới (quy tắc của 027).
5. **Xem lại tuần trước** — chọn "Tuần trước" hay "2/3 tuần trước" để so sánh; riêng hai ô "cảnh báo đang
   hoạt động" và "cạn ngân sách" luôn là tình trạng *hiện tại* và được ghi rõ như vậy trên màn hình.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/030-incident-and-weekly-dashboards-flow-nghiep-vu.drawio`](../diagrams/030-incident-and-weekly-dashboards-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Ít hiểu nhầm lúc nóng**: người trực không phải nhớ ô nào bị "đóng băng" ở khoảng thời gian nào.
- **Rút ngắn thời gian tìm nguyên nhân**: từ "bộ phận nào" tới "yêu cầu nào" ngay trên một màn hình.
- **Con số ngân sách tin cậy**: màn hình tuần không đổi theo thao tác của người xem và khớp đúng với điều
  kiện làm cảnh báo bật.
- **Bớt rủi ro sửa nhầm**: ngưỡng cảnh báo nhanh giờ có kiểm thử canh gác, không còn chỉ dựa vào trí nhớ.

Giới hạn hiện tại (chỉ xem lại được tối đa 3 tuần trước; hai ô cảnh báo/cạn chỉ là tình trạng hiện tại;
mở một yêu cầu cụ thể ra màn hình tìm kiếm chung chứ không phải màn hình vẽ cây thời gian chuyên dụng): xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/030_Architect_hai dashboard xử lý sự cố và ngân sách tuần.md`](../architecture/030_Architect_hai%20dashboard%20xử%20lý%20sự%20cố%20và%20ngân%20sách%20tuần.md).
