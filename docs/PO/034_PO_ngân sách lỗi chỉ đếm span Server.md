# Ngân sách lỗi chỉ tính yêu cầu mà chính bộ phận đó nhận, không tính lời gọi đi tiếp

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành trên môi trường thử nghiệm với số liệu thật. Giới hạn còn lại xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Một yêu cầu của khách đi qua nhiều bộ phận nối tiếp nhau (cổng vào, bộ phận ghép dữ liệu, bộ phận sản phẩm).
Khi tính "ngân sách lỗi" (số lỗi hoặc số lần chậm được phép trong tuần), hệ thống đếm **cả** những lần một bộ
phận gọi tiếp sang bộ phận sau lẫn việc gửi thông điệp đi. Hậu quả: (1) một lỗi ở bộ phận cuối làm hao ngân
sách của cả ba bộ phận; (2) ở bộ phận ghép dữ liệu, cùng một lỗi bị đếm hai lần (một lần khi nhận yêu cầu, một
lần khi gọi tiếp). Trong tuần 05–11/10, trong 319 lượt được tính của bộ phận ghép dữ liệu có 201 lượt là "gọi
tiếp", và 12 trong 21 lỗi thuộc loại đó.

## Giải pháp

- **Ngân sách chỉ tính yêu cầu mà chính bộ phận đó nhận**: bốn cảnh báo ngân sách (mốc 50%, 75%, 100%, đóng
  băng) và cảnh báo phát hiện nhanh chỉ đếm các lượt nhận yêu cầu của chính bộ phận; lời gọi đi tiếp và việc
  gửi thông điệp không còn bị tính. Phần kiểm tra sức khoẻ vẫn bị bỏ như trước.
- **Hai màn hình theo dõi khớp với cảnh báo**: các ô ngân sách và bảng SLO chỉ đếm cùng tập yêu cầu; ô "lỗi gọi
  hạ lưu" vẫn cho thấy lỗi ở lời gọi đi tiếp, để biết bộ phận nào đang làm hỏng bộ phận khác.
- **Có kiểm tra tự động canh giữ**: nếu ai đó bỏ điều kiện này khỏi một cảnh báo, bộ kiểm tra báo đỏ.
- **Dọn trạng thái "cạn" cũ**: các sự kiện cạn sinh ra từ cách đếm cũ được xoá sau khi chuyển và cảnh báo mốc
  100% được khởi động lại, để số mới không lẫn với số cũ.

## Điều người dùng cần biết

- Mẫu số nhỏ vẫn làm phần trăm ngân sách vọt cao: tuần đầu hoặc khi ít yêu cầu thật, chỉ vài lỗi đã làm bộ
  phận vượt 100%. Đây là giới hạn đã biết, không thêm ngưỡng số yêu cầu tối thiểu.
- Một lỗi chỉ xảy ra ở lời gọi đi tiếp mà bộ phận gọi tự xử lý (không trả lỗi cho khách) không còn làm hao ngân
  sách của bộ phận gọi; nó vẫn thấy được ở ô "lỗi gọi hạ lưu".
- Bộ phận chỉ có lời gọi đi tiếp (không nhận yêu cầu nào) không hiện dòng trong bảng ngân sách.
- Việc sửa hiến chương cho khớp ngưỡng độ trễ mới (PR #75) là việc riêng, chưa làm trong lần này.

Chi tiết kỹ thuật: [Kiến trúc 034](../architecture/034_Architect_ngân%20sách%20lỗi%20chỉ%20đếm%20span%20Server.md).
