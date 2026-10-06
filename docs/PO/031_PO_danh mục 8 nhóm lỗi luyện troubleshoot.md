# Luyện tìm và sửa lỗi theo từng nhóm — chọn nhóm để tập, hoặc để hệ thống gây lỗi bất ngờ, xin gợi ý theo mức

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành công cụ. Đã thử từng kiểu lỗi trên hệ thống chạy thật, cả khi biết trước lẫn
khi hệ thống tự chọn bí mật. Buổi diễn tập "bí mật" có xin gợi ý theo mức do người vận hành tự chạy
sau. Xem [functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Công cụ diễn tập sự cố của tính năng 028 chỉ gây được 3 kiểu lỗi (trỏ nhầm địa chỉ, nghẽn kết nối,
lỗi theo tỷ lệ). Sự cố thật thì đa dạng hơn nhiều: bộ phận chạy chậm, kho dữ liệu ngừng, đăng nhập
hỏng, thiếu tài nguyên, đứt mạng, hoặc cả một bộ phận chết hẳn. Người luyện cũng chưa có cách tập đúng
một nhóm lỗi mình đang yếu, và khi bế tắc thì chỉ còn cách chờ hoặc mở thẳng đáp án.

Bài tập "giết một bộ phận" của tính năng 025 lại phụ thuộc vào một cụm máy khác mà nay không còn dùng
cho việc diễn tập.

## Giải pháp: một danh mục 8 nhóm lỗi, hai cách tập, gợi ý theo mức

- **Danh mục 8 nhóm lỗi**: trỏ nhầm địa chỉ · nghẽn và lỗi theo tỷ lệ · chậm · kho dữ liệu ngừng ·
  đăng nhập hỏng · thiếu tài nguyên · đứt mạng · bộ phận chết hẳn. Danh mục chỉ mô tả *lỗi gì*, không
  gắn với một nơi chạy cụ thể, nên sau này có thể thêm nơi chạy khác mà không viết lại.
- **Tập có chủ đích**: chọn đúng nhóm và bộ phận cần luyện, một lệnh là có lỗi; có thể hẹn giờ để
  hệ thống tự gỡ, hoặc gỡ bằng một lệnh khi xong.
- **Tập bất ngờ**: hệ thống tự bốc thăm một nhóm, chỉ đưa mã niêm phong, như buổi diễn tập sự cố cũ —
  nhưng nay bốc từ cả 8 nhóm.
- **Gợi ý theo mức khi bế tắc**: mức 1 mô tả triệu chứng, mức 2 nói tên nhóm lỗi, mức 3 là đáp án. Mỗi
  lần xin đều được ghi lại, để biết mình đã cần bao nhiêu trợ giúp.
- **Gỡ lỗi một lệnh**: một lệnh khôi phục chung cho mọi nhóm, và chờ bộ phận khoẻ lại rồi mới báo xong.
- **Dọn phần cũ**: bài tập giết bộ phận trên cụm máy kia được gỡ khỏi tài liệu; phần hạ tầng triển khai
  vẫn nguyên.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người luyện chọn cách tập**: tập đúng một nhóm (biết trước), hoặc để hệ thống chọn bí mật.
2. **Lỗi xuất hiện**: ở cách tập có chủ đích hệ thống nói rõ đã gây gì; ở cách bất ngờ chỉ có mã niêm
   phong.
3. **Người luyện quan sát triệu chứng** trên màn hình theo dõi và tự tìm nguyên nhân.
4. **Bế tắc thì xin gợi ý** từng mức, không bị lộ đáp án quá sớm.
5. **Khôi phục bằng một lệnh**, chờ hệ thống khoẻ lại, rồi mở niêm phong đối chiếu.
6. **Lỗi vẫn tính vào ngân sách lỗi** của tuần như sự cố thật.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/031-error-group-catalog-flow-nghiep-vu.drawio`](../diagrams/031-error-group-catalog-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Luyện đúng chỗ yếu**: có thể tập lặp đi lặp lại một nhóm lỗi, không phải chờ bốc thăm trúng.
- **Phủ nhiều kiểu sự cố thật hơn**, thay vì chỉ 3 kiểu.
- **Biết mình cần bao nhiêu trợ giúp**: nhật ký gợi ý là một số đo cho mức thành thạo.
- **Tài liệu gọn, đúng thực tế**: bỏ hướng dẫn cho một cụm máy không còn dùng.

Kết quả thử thật và giới hạn hiện tại: một nhóm lỗi (thiếu tài nguyên) chỉ làm chậm nhẹ, một kiểu
nghẽn kết nối chưa thấy triệu chứng với lượng khách mô phỏng hiện tại, và với các nhóm không khởi động
lại cả hệ thống thì bộ phận hỏng có thể đoán ra qua trạng thái các bộ phận. Xem [functional-debt.md](functional-debt.md). Chi tiết kỹ thuật
đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/031_Architect_danh mục 8 nhóm lỗi luyện troubleshoot.md`](../architecture/031_Architect_danh%20mục%208%20nhóm%20lỗi%20luyện%20troubleshoot.md).
