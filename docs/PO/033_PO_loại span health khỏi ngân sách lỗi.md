# Ngân sách lỗi chỉ tính yêu cầu thật của khách; kiểm tra sức khoẻ có cảnh báo riêng

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành trên môi trường thử nghiệm với số liệu thật. Giới hạn còn lại xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Hệ thống tự hỏi từng bộ phận "bạn còn khoẻ không?" cứ 5 giây một lần. Những lượt hỏi này được đếm chung với
yêu cầu thật của khách khi tính "ngân sách lỗi" (số lỗi hoặc số lần chậm được phép trong tuần). Hậu quả:
(1) khi chưa có khách nào dùng, ngân sách vẫn nhích và có lúc báo hao tới 14–18% chỉ vì lượt hỏi đầu tiên
sau khi khởi động lại chậm; (2) con số ngân sách phản ánh sức khoẻ của chính hệ thống chứ không phải trải
nghiệm của khách.

## Giải pháp

- **Ngân sách chỉ tính yêu cầu thật**: mọi lượt kiểm tra sức khoẻ (đường dẫn bắt đầu bằng `/health`) bị bỏ
  khỏi công thức ở cả năm cảnh báo (mốc 50%, 75%, 100%, đóng băng, phát hiện nhanh) và ở cả hai màn hình
  theo dõi. Mỗi bộ phận khai báo tiền tố bị bỏ trong hồ sơ cam kết của mình, có kiểm tra tự động canh giữ.
- **Kiểm tra sức khoẻ có cảnh báo riêng**: khi từ 50% lượt kiểm tra sức khoẻ của một bộ phận trả lỗi trong 5
  phút, một cảnh báo mới `health-failure` bật và màn hình "Xử lý sự cố" có ô `Health lỗi theo service`.
  Cảnh báo này không đụng tới ngân sách.
- **Diễn tập lỗi dùng đường dẫn thật**: các bước diễn tập (Postman) nay tạo lỗi và độ trễ ở đường dẫn
  nghiệp vụ, vì tạo lỗi ở đường dẫn sức khoẻ không còn làm hao ngân sách.

## Điều người dùng cần biết

- Khi mới có ít yêu cầu thật, chỉ một vài yêu cầu chậm hoặc lỗi cũng có thể làm phần trăm ngân sách vọt
  cao: mẫu số nhỏ. Điều này đúng với ý nghĩa của ngân sách, không phải lỗi hiển thị.
- Một bộ phận chỉ có lượt kiểm tra sức khoẻ (chưa có yêu cầu thật) không hiện dòng trong bảng ngân sách.
- Kiểm tra sức khoẻ **chậm** (không lỗi) không được báo ở đâu; chỉ lỗi 5xx mới có cảnh báo.
- Đường dẫn nghiệp vụ trùng tiền tố `/health` (nếu sau này có) cũng bị bỏ.

Chi tiết kỹ thuật: [Kiến trúc 033](../architecture/033_Architect_loại%20span%20health%20khỏi%20ngân%20sách%20lỗi.md).
