# Một lệnh duy nhất — cả nền tảng chạy lên, sẵn sàng dùng thử

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 43/43 hạng mục công việc, xác minh bằng cách thực sự chạy lệnh đó nhiều
lần và đo thời gian thật.*

## Vấn đề trước đây

Cho tới lúc này, việc dựng toàn bộ nền tảng để dùng thử trên máy cá nhân là một quy trình thủ công
nhiều bước, không được ghi lại thống nhất ở một nơi — dựng từng cơ sở dữ liệu, chạy nhiều lệnh cập
nhật cấu trúc dữ liệu riêng lẻ, khởi động từng mảng nghiệp vụ, rồi khởi động cả giao diện. Chín bước,
không theo một thứ tự được ghi chép rõ ràng ở một chỗ — nghĩa là mỗi người mới tham gia dự án phải tự
mò mẫm lại từ đầu.

## Giải pháp: một lệnh duy nhất, và không có bước "ẩn" nào khác

Giờ đây, một người mới clone kho mã nguồn về máy chỉ cần đúng hai việc: **sao chép một file cấu hình
mẫu, và chạy một lệnh duy nhất.** Lệnh đó tự lo toàn bộ phần còn lại — dựng mọi cơ sở dữ liệu, tự động
cập nhật cấu trúc dữ liệu, tự nạp sẵn dữ liệu mẫu, khởi động mọi mảng nghiệp vụ, và khởi động cả giao
diện mua sắm — và **chỉ báo "xong" khi mọi thứ thực sự sẵn sàng phục vụ**, không báo thành công non.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Sao chép file cấu hình mẫu**, không cần chỉnh sửa gì.
2. **Chạy đúng một lệnh.** Lệnh đó dựng toàn bộ hệ thống — mọi mảng nghiệp vụ, cơ sở dữ liệu, và các
   thành phần hạ tầng khác nền tảng cần — và chỉ dừng lại khi tất cả đã thực sự sẵn sàng.
3. **Mở đúng một địa chỉ được ghi rõ trong tài liệu** — và toàn bộ luồng mua sắm (xem sản phẩm → giỏ
   hàng → thanh toán → xác nhận) hoạt động ngay, không cần chỉnh sửa hay chạy thêm bất kỳ lệnh nào.
4. **Dừng hệ thống bằng một lệnh khác** khi xong việc trong ngày — không để lại rác, không chiếm cổng
   mạng còn sót lại.
5. **Khởi động lại vào lần sau** — mọi dữ liệu đã tạo trước đó (giỏ hàng, đơn hàng) vẫn còn nguyên,
   không cần dọn dẹp thủ công.
6. **Nếu muốn bắt đầu lại từ đầu hoàn toàn**, có một lệnh riêng để xoá sạch dữ liệu và quay về trạng
   thái như lần chạy đầu tiên.
7. **Nếu thiếu điều kiện cần** (ví dụ chưa cài phần mềm nền tảng cần thiết) hoặc một thành phần nào đó
   không khởi động được, lệnh báo lỗi rõ ràng, nêu đúng tên thành phần gặp vấn đề — không bao giờ báo
   "thành công" trong khi thực ra có thứ gì đó chưa hoạt động.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/005-one-command-local-run-flow-nghiep-vu.drawio`](../diagrams/005-one-command-local-run-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Người mới tham gia dự án lên môi trường làm việc nhanh hơn nhiều** — không còn phải tự mò mẫm chín
  bước rời rạc.
- **Demo cho khách hàng/nội bộ dễ dàng, đáng tin cậy hơn** — một lệnh, một địa chỉ, luồng hoạt động
  đầy đủ.
- **Ít rủi ro "chạy trên máy tôi thì được" hơn** — mọi người dùng đúng cùng một cách dựng hệ thống,
  được ghi lại đầy đủ, không có bước ẩn nào chỉ một người biết.

Bằng chứng đã kiểm chứng thật (số liệu đo thời gian thật) và giới hạn hiện tại (gồm khoảng cách
multi-tenant chưa đóng, bộ nhớ đệm/hàng đợi tin nhắn chưa dùng): xem
[functional-debt.md](functional-debt.md).
