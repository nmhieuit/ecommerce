# Không còn truy vấn "lấy hết dữ liệu" — mọi danh sách đều có giới hạn ngay cả khi không ai yêu cầu

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 25/25 hạng mục công việc, xác minh bằng các bài kiểm tra tự động chạy
thật.*

## Vấn đề trước đây

Một số nơi trong hệ thống, khi trả về 1 danh sách (ví dụ danh sách sản phẩm), lấy về **toàn bộ** dữ
liệu thay vì 1 phần giới hạn — nếu dữ liệu tăng lên hàng trăm, hàng nghìn bản ghi, chỉ 1 lời gọi đơn
giản cũng có thể làm chậm hoặc quá tải hệ thống. Một nơi khác — mỗi khi hiển thị giỏ hàng có nhiều sản
phẩm — lại tải về **toàn bộ danh mục sản phẩm** chỉ để lấy đúng tên của vài sản phẩm cần thiết trong
giỏ. Càng nhiều sản phẩm trong danh mục, cách làm này càng chậm, dù giỏ hàng đó chỉ có vài dòng.

## Giải pháp: mọi danh sách đều tự có giới hạn, và giỏ hàng chỉ lấy đúng thứ cần

- **Mọi danh sách trả về đều tự động giới hạn kích thước** (mặc định 20 bản ghi/trang) ngay cả khi
  không ai truyền tham số yêu cầu — không còn tình huống "quên truyền tham số thì lấy hết".
- **Giới hạn kích thước tối đa được ép buộc ở phía server** — không thể vượt qua chỉ bằng cách tự khai
  1 con số rất lớn khi gọi.
- **Hiển thị giỏ hàng nay chỉ lấy đúng những sản phẩm có trong giỏ**, không còn tải cả danh mục chỉ để
  lấy vài cái tên.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người dùng xem danh sách sản phẩm** — hệ thống tự động trả về 1 trang có giới hạn, không phải
   toàn bộ, kể cả khi danh mục có hàng trăm sản phẩm.
2. **Người dùng mở giỏ hàng có nhiều sản phẩm** — hệ thống chỉ lấy đúng thông tin những sản phẩm có
   trong giỏ, không tải toàn bộ danh mục, dù giỏ hàng đó có bao nhiêu dòng.
3. **Nếu ai đó cố tình yêu cầu 1 số lượng cực lớn** qua tham số gọi — hệ thống vẫn chỉ trả về đúng mức
   giới hạn đã quy định, không phản ánh đúng yêu cầu bất thường đó.
4. **Nếu sau này có ai thêm 1 danh sách mới vào hệ thống mà quên đặt giới hạn** — có 1 bước kiểm tra tự
   động phát hiện và cảnh báo, trước khi tính năng đó được đưa vào sử dụng thật.

## Lợi ích kinh doanh

- **Hệ thống không bị chậm hay quá tải khi dữ liệu tăng trưởng theo thời gian** — rủi ro này được chặn
  trước, không phải chờ sự cố thật mới sửa.
- **Giỏ hàng hiển thị nhanh hơn**, không còn phụ thuộc vào việc danh mục sản phẩm có bao nhiêu mặt hàng.
- **Rủi ro "quên đặt giới hạn" ở tính năng tương lai giảm hẳn** — có cơ chế tự động nhắc, không phải
  trông chờ vào sự cẩn thận của từng người viết code.

Bằng chứng đã kiểm chứng thật và giới hạn hiện tại: xem [functional-debt.md](functional-debt.md).
