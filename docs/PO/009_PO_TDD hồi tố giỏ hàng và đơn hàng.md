# Kiểm chứng lại các quy tắc tính tiền — và một phát hiện bất ngờ: mọi thứ đã đúng từ trước

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành, xác minh bằng kiểm tra tự động.*

## Vấn đề trước đây

Trong giai đoạn đầu xây dựng nền tảng, có lo ngại rằng các quy tắc tính tiền quan trọng nhất — "không
cho thêm số lượng âm hoặc bằng không vào giỏ hàng", "giữ nguyên giá đã chốt khi thêm lại một sản phẩm",
"không tạo đơn hàng rỗng", "tổng tiền phải do hệ thống tự tính, không được ai gửi lên tuỳ ý" — có thể
đã được xây vội trong giai đoạn đầu mà chưa được kiểm chứng đầy đủ theo đúng kỷ luật bắt buộc của dự
án (viết bài kiểm tra thất bại trước, viết mã sau).

## Giải pháp: rà soát lại toàn bộ, và chứng minh bằng cách cố tình phá vỡ từng quy tắc

Đội đã rà soát kỹ toàn bộ các quy tắc tính tiền quan trọng nhất trong giỏ hàng và đơn hàng. Với mỗi
quy tắc, đội **cố tình tạm thời bỏ nó đi** để xác nhận có đúng một bài kiểm tra tự động phát hiện ra
ngay và báo lỗi — sau đó khôi phục lại quy tắc đó về đúng như cũ. Đây là cách chứng minh chắc chắn
nhất rằng một quy tắc thực sự được bảo vệ, không chỉ "trông có vẻ đúng".

## Trải nghiệm thực tế diễn ra như thế nào

1. **Rà soát từng quy tắc tính tiền một**: không cho thêm số lượng âm/bằng không, giữ nguyên giá đã
   chốt khi thêm lại sản phẩm, gộp số lượng thay vì tạo dòng trùng, không tạo đơn hàng rỗng, không
   chấp nhận sản phẩm với số lượng/giá không hợp lệ, tổng tiền luôn do hệ thống tự tính.
2. **Với mỗi quy tắc, cố tình tạm gỡ bỏ nó** để xem có đúng một bài kiểm tra tự động bắt được ngay
   lập tức hay không — sau đó khôi phục lại đúng như cũ.
3. **Xác nhận không có gì bị bỏ sót**: mọi quy tắc quan trọng đều có đúng một bài kiểm tra bảo vệ, sẵn
   sàng bắt lỗi ngay nếu ai đó vô tình làm hỏng quy tắc đó trong tương lai.
4. **Ghi lại thành văn bản kỷ luật làm việc đi tiếp**: từ nay, mọi thay đổi tới các quy tắc này phải
   viết bài kiểm tra thất bại trước, sửa mã sau — không còn ngoại lệ.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/009-retrofit-tdd-basket-order-flow-nghiep-vu.drawio`](../diagrams/009-retrofit-tdd-basket-order-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Có bằng chứng thực nghiệm, không phải phỏng đoán**, rằng các quy tắc tính tiền quan trọng nhất
  đang được bảo vệ đúng cách — rủi ro tính sai tiền, tạo đơn hàng lỗi gần như bằng không.
- **Không tốn công sức và rủi ro viết lại mã đang hoạt động tốt** — đội đã tránh được một việc làm
  không cần thiết, tập trung nguồn lực vào nơi thực sự cần.
- **Có kỷ luật làm việc rõ ràng cho tương lai**: mọi thay đổi sau này tới các quy tắc quan trọng này
  đều phải qua đúng quy trình kiểm chứng trước khi viết mã.

Điều bất ngờ phát hiện được và giới hạn hiện tại: xem [functional-debt.md](functional-debt.md).
