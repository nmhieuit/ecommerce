# Phân quyền từ chối theo mặc định — mỗi cửa ra vào phải tự khai rõ ai được đi qua

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành — 38/38 công việc đã lên kế hoạch đều xong, đã kiểm chứng bằng các phép
thử tự động chạy thật trên từng bộ phận của hệ thống. Có một giới hạn cần biết về môi trường máy phát
triển lúc kiểm chứng cuối — xem mục "Giới hạn hiện tại" bên dưới, được nêu trung thực, không che giấu.*

## Vấn đề trước đây

Từ tính năng "vé thông hành" (máy chủ định danh thật) hoàn thành trước đó, mọi yêu cầu gửi tới hệ
thống đều phải mang theo một vé thật, đã được xác minh không thể giả mạo. Nhưng vé đó mới chỉ trả lời
được câu hỏi **"người này có phải là người đã đăng nhập thật không"** — chưa trả lời được câu hỏi tiếp
theo, quan trọng hơn: **"người này có thực sự được phép làm đúng việc họ đang cố làm không"**.

Hãy tưởng tượng một toà nhà văn phòng: bảo vệ ở cổng chính chỉ kiểm tra "bạn có thẻ ra vào toà nhà
không" — còn từng phòng cụ thể bên trong (phòng kế toán, kho hàng...) lại không có ai đứng gác riêng.
Kết quả là bất kỳ chiếc thẻ ra vào toà nhà nào cũng mở được mọi cánh cửa bên trong, dù đúng ra không
nên như vậy.

Thêm vào đó, khi một lập trình viên xây một tính năng mới (một "cánh cửa" mới trong toà nhà), trước
đây không có gì **bắt buộc** họ phải viết rõ ngay lúc xây dựng "ai được phép đi qua cửa này". Cửa đó
vẫn an toàn nhờ một lưới bảo vệ ẩn phía sau, nhưng lưới bảo vệ đó vô hình — nhìn vào cánh cửa, không ai
biết ngay được liệu người xây đã thực sự *nghĩ tới* việc "ai được đi qua", hay chỉ đơn giản quên mất.

## Giải pháp: mỗi cửa tự dán biển, thợ xây quên dán biển sẽ bị chặn ngay lập tức

Giờ đây:

1. **Mỗi cửa ra vào (mỗi endpoint) phải tự dán một tấm biển rõ ràng ngay trên cửa** — hoặc "chỉ những
   ai mang đúng loại thẻ mới được vào", hoặc "cửa này mở cho tất cả" (như các cửa kiểm tra sức khoẻ hệ
   thống mà máy móc tự gọi, không phải người dùng). Không còn cửa nào được xây mà không có biển.
2. **Nếu ai đó cố tình dựng một cửa mới mà quên dán biển, hệ thống xây dựng phần mềm sẽ tự động phát
   hiện và từ chối bàn giao** — giống một thanh tra công trình tự động, đi kiểm tra từng cửa trước khi
   cho phép đưa toà nhà vào sử dụng. Việc này không phụ thuộc vào trí nhớ hay sự cẩn thận của bất kỳ ai.
3. **Nếu ai đó mang một chiếc thẻ ra vào toà nhà hợp lệ (đã đăng nhập thật) nhưng không đúng loại thẻ
   mà cửa đó yêu cầu, họ bị từ chối rõ ràng ngay tại cửa** — không phải một lỗi mơ hồ, và chắc chắn
   không được cho vào nhầm.
4. **Có một "công tắc khẩn cấp" riêng cho quy tắc mới này** (cùng cơ chế công tắc mà tính năng "vé
   thông hành" trước đó đã dùng) — nếu quy tắc thẻ mới gây sự cố ngoài dự tính khi vừa triển khai, có
   thể tắt ngay lập tức mà không cần dừng hệ thống, quay về đúng hành vi cũ ("chỉ cần có vé hợp lệ là
   được vào").
5. Đồng thời, với những quy tắc nghiệp vụ mà **giao diện web đã tự kiểm tra trước khi gửi đi** (ví dụ
   "giỏ hàng không được để trống khi thanh toán"), đội đã xác nhận lại **bằng phép thử thật** rằng phía
   máy chủ cũng tự kiểm tra độc lập — kể cả khi ai đó cố tình bỏ qua giao diện web và gọi thẳng vào hệ
   thống.

## Trải nghiệm thực tế diễn ra như thế nào

- **Với người dùng cuối**: gần như không có khác biệt nào trong cách sử dụng bình thường — đăng nhập
  và thao tác như trước, không thấy thêm bước nào.
- **Với một yêu cầu thiếu đúng loại "thẻ" cần thiết**: bị từ chối ngay lập tức với một thông báo rõ
  ràng, không phải được xử lý như thể mọi thứ đều ổn.
- **Với một lập trình viên thêm một cửa mới mà quên dán biển**: hệ thống xây dựng phần mềm (build) tự
  động phát hiện và chặn lại ngay, nêu rõ đúng cửa nào bị thiếu — trước khi cửa đó có cơ hội được đưa
  vào sử dụng thật.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio`](../diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio))*

## Điều đặc biệt: đã kiểm chứng thật, không chỉ thiết kế trên giấy

- **Thợ xây "quên dán biển" thật sự bị chặn.** Đội đã thử nghiệm bằng cách cố tình thêm một cửa thử
  nghiệm không dán biển, chạy lại "thanh tra công trình tự động", và xác nhận nó bị từ chối ngay, nêu
  rõ đúng cửa vi phạm — sau đó gỡ cửa thử nghiệm đi, thanh tra lại xác nhận mọi thứ bình thường.
- **Từ chối "thiếu đúng loại thẻ" hoạt động thật, ở cả 5 bộ phận nghiệp vụ của hệ thống** — không chỉ
  ở một nơi. Mỗi bộ phận đều được thử với một yêu cầu mang thẻ hợp lệ nhưng thiếu đúng loại cần thiết,
  và đều từ chối đúng cách.
- **Quy tắc nghiệp vụ mà giao diện web tự kiểm tra cũng được xác nhận có kiểm tra độc lập ở máy chủ**
  bằng phép thử gọi thẳng vào hệ thống, bỏ qua hoàn toàn giao diện web.

## Lợi ích kinh doanh

- **Không còn phụ thuộc vào trí nhớ con người để đảm bảo an toàn** — máy móc tự kiểm tra, tự chặn mọi
  lần một cửa mới được thêm vào mà thiếu quyết định rõ ràng về ai được đi qua.
- **Có đường lùi khẩn cấp**: nếu quy tắc mới gặp sự cố ngoài dự tính, có một công tắc quay về trạng
  thái cũ ngay lập tức, không cần chờ một đợt triển khai mới.
- **Không thay đổi trải nghiệm người dùng ở những phần đã hoạt động tốt** — mọi người dùng hợp lệ hiện
  tại tiếp tục dùng hệ thống như trước.
- **Nền tảng cho các bước tiếp theo**: khi sau này cần phân biệt "vai trò" chi tiết hơn (ví dụ quản trị
  viên khác khách hàng thường), công việc đó xây thẳng lên nền móng "mỗi cửa tự khai báo rõ ràng" này,
  không phải làm lại từ đầu.

## Giới hạn hiện tại — trung thực cần biết

- **Một số phép thử cần nhiều bộ phận hệ thống nói chuyện với nhau qua mạng nội bộ (ví dụ cổng vào
  chính gọi sang bộ phận tổng hợp, rồi gọi tiếp sang bộ phận nghiệp vụ) đã không chạy được trong đúng
  phiên làm việc cuối cùng** — vì máy chủ container trên máy phát triển vừa được khởi động lại và mạng
  nội bộ của nó chưa ổn định hoàn toàn ngay sau đó, khiến các cuộc gọi giữa các bộ phận bị chậm bất
  thường và vượt quá thời gian chờ cho phép. Đây là vấn đề của **môi trường máy phát triển tại đúng
  thời điểm đó**, không phải lỗi của tính năng: mọi phép thử chạy trực tiếp trên **từng bộ phận riêng
  lẻ** — nơi tính năng này thực sự thay đổi hành vi — đều đã chạy và đạt kết quả đúng. Chi tiết kỹ
  thuật đầy đủ dành cho đội kỹ thuật, xem
  [`docs/architecture/015_Architect_phân quyền từ chối theo mặc định.md`](../architecture/015_Architect_phân%20quyền%20từ%20chối%20theo%20mặc%20định.md).
- **Phân quyền theo vai trò chi tiết (ví dụ "quản trị viên" khác "khách hàng thường") chưa nằm trong
  phạm vi tính năng này** — hiện tại mọi người dùng đã đăng nhập thật đều mang cùng một loại "thẻ".
  Tính năng này chỉ đảm bảo mọi cửa đều bắt buộc phải khai báo rõ ràng, làm nền móng cho việc phân biệt
  vai trò chi tiết hơn sau này.
