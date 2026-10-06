# Tài liệu luyện xử lý sự cố — hướng dẫn từng bước cho từng nhóm lỗi, và gợi ý theo triệu chứng cho lỗi bất ngờ

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã viết xong tài liệu và đã thử từng nhóm lỗi trên hệ thống chạy thật, số liệu trong tài
liệu là số đo thật. Một lượt "người thật làm theo hướng dẫn" do người dùng tự thực hiện sau. Xem
[functional-debt.md](functional-debt.md).*

## Vấn đề trước đây

Công cụ gây lỗi để luyện (tính năng 031) đã có 8 nhóm lỗi, nhưng người luyện vẫn phải tự mò: không có
tài liệu nói *nhìn ở đâu*, *so với gì*, *làm sao biết đã khỏi*. Với lỗi bất ngờ thì chỉ có ba mức gợi ý
rất ngắn, còn tài liệu thì lại ghi sẵn tên nhóm lỗi nên đọc là lộ đáp án. Người mới cũng không có cách
tự biết mình đã luyện đủ một nhóm chưa.

## Giải pháp: một bộ tài liệu nối tiếp chuỗi hướng dẫn quan sát hệ thống

- **Hướng dẫn từng bước cho từng nhóm lỗi** (8 nhóm, mỗi nhóm một tài liệu): dấu hiệu người dùng thấy,
  nhìn ở màn hình theo dõi nào, câu hỏi nào để tự kiểm số liệu, cách khôi phục, và cách biết hệ thống đã
  thật sự ổn định lại.
- **Gợi ý theo triệu chứng cho lỗi bất ngờ**: tra theo điều bạn nhìn thấy (lỗi lan, chậm, đăng nhập hỏng,
  một bộ phận biến mất…), mở dần ba mức — mô tả triệu chứng kèm cách kiểm, rồi tên nhóm lỗi, rồi đáp án —
  nên đọc mức đầu không lộ đáp án.
- **Những bẫy thật đã gặp**: lỗi lan từ bộ phận phía sau lên phía trước, đăng nhập hỏng tạm thời sau khi
  khởi động lại, nhiễu khi hệ thống vừa khởi động, màn hình bị lấp bởi thông báo không liên quan.
- **Bài tập tự làm và tiêu chí "đã đạt"** sau mỗi nhóm: để người luyện tự đánh giá.
- **Kiểm tự động cho chính tài liệu**: mỗi nhóm lỗi đều có hướng dẫn, mọi liên kết đều dẫn tới nơi có
  thật, và phần gợi ý đầu không để lộ tên bộ phận hay loại lỗi.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người luyện chọn cách tập**: tập đúng một nhóm (biết trước), hoặc để hệ thống chọn bí mật.
2. **Biết trước nhóm**: mở tài liệu của nhóm đó và đi từng bước, đối chiếu số liệu mình thấy với số đo
   thật đã ghi sẵn.
3. **Bất ngờ**: nhìn triệu chứng, mở phần gợi ý theo triệu chứng và đọc từng mức một cho tới khi tự tìm ra.
4. **Khôi phục** bằng một lệnh, rồi xác nhận hệ thống ổn định liên tục 15 phút như quy trình diễn tập cũ.
5. **Làm bài tập** của nhóm và tự đánh dấu các tiêu chí "đã đạt".
6. **Lỗi vẫn tính vào ngân sách lỗi** của tuần như sự cố thật.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/032-troubleshoot-practice-docs-flow-nghiep-vu.drawio`](../diagrams/032-troubleshoot-practice-docs-flow-nghiep-vu.drawio))*

## Lợi ích kinh doanh

- **Rút ngắn thời gian làm quen**: người mới luyện được từng nhóm lỗi theo hướng dẫn thay vì tự mò.
- **Luyện bất ngờ mà không bị lộ đáp án**: gợi ý theo triệu chứng, mở dần từng mức.
- **Tránh kết luận sai**: các bẫy thật đã gặp được ghi rõ để người luyện phân biệt lỗi thật với nhiễu.
- **Số liệu đáng tin**: mọi con số đều đã đo thật trên hệ thống chạy thật, kèm cách tự đo lại.

Kết quả thử thật và giới hạn hiện tại: có nhóm lỗi gần như không để lại dấu vết (kiểu nghẽn kết nối), có
nhóm chỉ làm chậm nhẹ nên cảnh báo tự động có thể không bắn (thiếu tài nguyên), có nhóm làm bộ phận biến
mất khỏi màn hình theo dõi thay vì hiện đỏ (đứt mạng, bộ phận chết), và khi luyện nhiều lần liên tiếp
thì cảnh báo của lần trước còn bật nên khó đo thời gian phát hiện của lần sau. Phần gợi ý trong tài liệu
và phần gợi ý của công cụ được viết riêng nên về sau có thể lệch nhau. Xem
[functional-debt.md](functional-debt.md). Chi tiết kỹ thuật đầy đủ dành cho đội kỹ thuật, xem
[`docs/architecture/032_Architect_tài liệu luyện troubleshoot theo nhóm lỗi.md`](../architecture/032_Architect_tài%20liệu%20luyện%20troubleshoot%20theo%20nhóm%20lỗi.md).
