# Tập xử lý sự cố như thật — không biết trước chỗ hỏng, phát hiện nhờ cảnh báo, chỉ đóng khi số đo xác nhận

> **Cập nhật spec 033**: ngân sách lỗi chỉ tính request nghiệp vụ; health check (`/health…`) không còn tính vào mẫu số hay số request xấu. Service không sẵn sàng có cảnh báo riêng `health-failure`. Xem [033 PO](033_PO_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành công cụ và quy trình. Đã thử từng kiểu hỏng hóc trên hệ thống chạy thật ở
chế độ biết trước. Buổi diễn tập "bí mật" đầu tiên (người vận hành không biết trước chỗ hỏng) chưa
chạy. Xem [functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Hệ thống đã có cam kết chất lượng cho từng bộ phận, có màn hình theo dõi hằng ngày, và có "ngân sách
lỗi" theo tuần (tính năng 027). Nhưng việc **xử lý sự cố** mới chỉ được mô tả trên giấy, chưa ai
thực sự làm:
- Khi có sự cố, ai phát hiện?
- Đánh giá mức độ nghiêm trọng ra sao, báo tình hình cho ai, ở đâu?
- Khi nào được coi là đã xong?

Các bài tập trước (tính năng 025) luôn biết trước chỗ hỏng, nên chỉ kiểm tra "lưới an toàn" chứ chưa
kiểm tra **con người và quy trình**.

## Giải pháp: một buổi diễn tập mà chính người vận hành không biết trước chỗ hỏng

- **Sự cố bí mật**: một công cụ tự bốc thăm bộ phận nào hỏng, hỏng kiểu gì, và lúc nào (trong vòng 30
  phút). Lựa chọn được "niêm phong" kèm một mã kiểm tra, nên không ai sửa lại được sau đó. Chỉ mở ra sau
  khi sự cố đã xử lý xong, để đối chiếu.
- **Hỏng thật, nhưng không đụng vào phần mềm**: chỉ cấu hình sai (trỏ nhầm địa chỉ, giới hạn kết nối quá
  nhỏ) hoặc cố ý tạo lỗi theo một tỷ lệ. Tất cả được khoá bằng một công tắc, mặc định tắt.
- **Phát hiện nhờ cảnh báo, không phải tình cờ**: có thêm một cảnh báo nhìn 5 phút gần nhất (cảnh báo
  ngân sách tuần của 027 thì quá chậm cho một sự cố). Cảnh báo hiện ngay trên màn hình người vận hành
  mở mỗi ngày.
- **Quy trình xử lý được viết sẵn**: phát hiện → đánh giá mức độ (3 mức) → báo tình hình trên một phiếu
  sự cố, tại mỗi mốc và mỗi 30 phút → giảm thiểu bằng một thay đổi phòng ngừa → xác nhận đã ổn.
- **"Xong" phải có số đo**: chỉ đóng sự cố khi bộ phận đạt cam kết **liên tục 15 phút**, không phải
  "trông có vẻ ổn".

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người vận hành bật công tắc và khởi động buổi diễn tập**, nhận về một mã niêm phong. Hệ thống vẫn
   chạy bình thường với lượng khách mô phỏng.
2. **Trong vòng 30 phút, một bộ phận bắt đầu hỏng.** Mọi bộ phận đều được khởi động lại cùng lúc, nên
   nhìn bề ngoài không đoán ra được bộ phận nào.
3. **Cảnh báo hiện trên màn hình hằng ngày.** Cảnh báo kéo dài qua ít nhất 2 lần kiểm tra (5 phút một
   lần) mới được coi là sự cố. Người vận hành mở phiếu sự cố, chấm mức độ, và ghi từng mốc kèm giờ.
4. **Tìm nguyên nhân rồi giảm thiểu**: đưa vào một thay đổi phòng ngừa, rồi khởi động lại hệ thống về
   cấu hình đúng.
5. **Chờ số đo xác nhận** 15 phút liên tục đạt cam kết, rồi đóng phiếu. Sau đó mới mở niêm phong để
   đối chiếu: nguyên nhân mình tìm ra có đúng không.
6. **Lưu bản ghi sự cố** với các mốc tách bạch: lúc hỏng, lúc cảnh báo, lúc phát hiện, lúc giảm thiểu,
   lúc xong. Bản ghi này là đầu vào cho buổi rút kinh nghiệm (tính năng sau).

*(Xem sơ đồ minh hoạ: [`docs/diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio`](../diagrams/028-incident-oncall-drill-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Quy trình xử lý sự cố được thử thật**, không chỉ nằm trên giấy. Lỗ hổng lộ ra khi diễn tập, không
  phải lúc khách hàng đang chịu ảnh hưởng.
- **Có con số để cải thiện**: thời gian từ lúc có cảnh báo tới lúc có hành động giảm thiểu được ghi lại
  mỗi buổi, làm mốc so sánh cho các lần sau.
- **"Đã xong" có nghĩa rõ ràng**, giống nhau với mọi người: 15 phút liên tục đạt cam kết.
- **Bản ghi sự cố đầy đủ mốc thời gian** là nền cho việc rút kinh nghiệm không đổ lỗi về sau.

Kết quả thử thật và giới hạn hiện tại: môi trường thử nghiệm chậm theo từng đợt, nên cảnh báo đôi khi
bật cho cả bộ phận không hỏng; hỏng một bộ phận còn kéo lỗi sang các bộ phận phụ thuộc. Xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/028_Architect_diễn tập sự cố thật và phản ứng on-call.md`](../architecture/028_Architect_diễn%20tập%20sự%20cố%20thật%20và%20phản%20ứng%20on-call.md).
