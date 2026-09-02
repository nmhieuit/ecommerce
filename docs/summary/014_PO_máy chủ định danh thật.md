# Máy chủ định danh thật — thay thế "người dùng giả lập" bằng đăng nhập xác thực

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: đã hoàn thành và đang hoạt động thật (không phải bản demo/thử nghiệm) — đã chạy kiểm
chứng trên một môi trường đầy đủ (mọi thành phần của hệ thống cùng chạy), không chỉ trên giấy.*

## Vấn đề trước đây

Từ trước tới nay, mọi bộ phận của hệ thống đều tin vào **một người dùng "giả lập" được gán cứng sẵn**
— không có bước đăng nhập thật, không có việc kiểm tra mật khẩu, và mọi yêu cầu gửi tới hệ thống đều
tự động được coi là "đã xác thực", dù thực ra chưa ai chứng minh được người gửi yêu cầu đó là ai. Đây
là một lựa chọn có chủ đích ở giai đoạn đầu (để các bộ phận khác của hệ thống có thể xây dựng và kiểm
thử song song), nhưng nó có ba điểm yếu không thể chấp nhận về lâu dài:

- **Không ai thực sự "đăng nhập"** — không có mật khẩu, không có cách phân biệt người dùng thật với
  người khác.
- **Mọi yêu cầu đều mặc định được tin tưởng** — không có cơ chế nào để một bộ phận nói "tôi không tin
  yêu cầu này, hãy chứng minh nó hợp lệ".
- **Nếu cổng vào chính của hệ thống bị bỏ qua hoặc cấu hình sai, không có lớp phòng thủ thứ hai** —
  mọi thứ phía sau đều tin tưởng mù quáng vào phán quyết của cổng vào đó.

## Giải pháp: một máy chủ định danh thật, và mỗi bộ phận tự kiểm tra lại — không tin ai cả

Giờ đây, hệ thống có một **máy chủ định danh thật sự** — hoạt động theo đúng nguyên lý mà việc "Đăng
nhập bằng Google/Facebook" đã quen thuộc: người dùng đăng nhập một lần với thông tin thật, và nhận
lại một **"vé thông hành"** (token) có chữ ký điện tử không thể giả mạo. Vé đó mang theo hai thông
tin: "tôi là ai" và "tôi thuộc về khách hàng doanh nghiệp (tenant) nào".

Điểm khác biệt cốt lõi so với việc chỉ thêm một cổng kiểm tra ở lối vào: **mọi bộ phận trong hệ
thống, không chỉ cổng vào chính, đều tự mình kiểm tra lại vé thông hành đó một cách độc lập** — không
bộ phận nào tin tưởng mù quáng rằng "cổng vào đã kiểm tra rồi nên tôi khỏi cần kiểm tra nữa". Đây là
nguyên lý **"không có ranh giới tin cậy duy nhất"**: kể cả khi cổng vào chính bị bỏ qua, bị xâm nhập,
hoặc cấu hình sai, mọi bộ phận phía sau vẫn tự bảo vệ được chính mình.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người dùng đăng nhập** với thông tin thật (không còn là người dùng giả lập gán cứng).
2. **Máy chủ định danh cấp một vé thông hành** mang theo danh tính đã xác minh và tenant của người
   đó.
3. **Vé đó đi theo mọi yêu cầu tiếp theo.** Ở mỗi chặng hệ thống đi qua — cổng vào chính, và từng bộ
   phận xử lý nghiệp vụ phía sau — vé được tự kiểm tra lại độc lập: chữ ký có thật không, còn hạn
   không, có bị chỉnh sửa không.
4. **Nếu vé hợp lệ**: yêu cầu được xử lý bình thường, và thông tin "tôi thuộc tenant nào" được dùng
   đúng như trước — người dùng không thấy khác biệt nào trong trải nghiệm sử dụng.
5. **Nếu vé giả mạo, bị chỉnh sửa, hoặc đã hết hạn**: yêu cầu bị từ chối **ngay tại bộ phận nhận được
   nó**, với một thông báo rõ ràng — kể cả khi ai đó cố tình gửi thẳng yêu cầu tới một bộ phận phía
   sau, bỏ qua cổng vào chính hoàn toàn.
6. **Nếu vé đã hết hạn**: người dùng nhận được thông báo rõ ràng "cần đăng nhập lại", không phải một
   lỗi khó hiểu hay một sự im lặng bất thường.

*(Xem sơ đồ minh hoạ: [`docs/diagrams/014-identity-server-flow-nghiep-vu.drawio`](../diagrams/014-identity-server-flow-nghiep-vu.drawio))*

## Điều đặc biệt: đã kiểm chứng thật, không chỉ thiết kế trên giấy

Hai điều dưới đây đã được **thử nghiệm thật**, không chỉ là mô tả lý thuyết:

- **Đi vòng qua cổng vào chính vẫn bị chặn.** Đội đã thử gửi thẳng một vé giả mạo tới một bộ phận xử
  lý nghiệp vụ phía sau, cố tình bỏ qua hoàn toàn cổng vào chính — bộ phận đó tự phát hiện và từ chối
  ngay, không cần ai "báo trước" cho nó.
- **Công tắc khẩn cấp hoạt động thật, không cần khởi động lại hệ thống.** Đội đã thử gạt công tắc
  "quay về chế độ cũ" ngay trên một hệ thống đang chạy (không dừng, không triển khai lại) và xác nhận
  cổng vào chính lập tức ngừng tự đòi vé thông hành — trong khi các bộ phận phía sau, đúng theo thiết
  kế "không tin ai cả", vẫn tiếp tục tự đòi vé của riêng chúng. Điều này chứng minh công tắc khẩn cấp
  chỉ kiểm soát đúng lớp mà nó được thiết kế để kiểm soát, không âm thầm tắt luôn lớp phòng thủ ở phía
  sau.

## Lợi ích kinh doanh

- **An toàn hơn thật sự, không phải an toàn "trên giấy"**: một điểm yếu ở cổng vào chính không còn có
  thể làm sụp đổ toàn bộ hệ thống phòng thủ — mỗi bộ phận tự bảo vệ chính nó.
- **Có đường lùi khẩn cấp**: nếu máy chủ định danh mới gặp sự cố khi vừa triển khai, có một công tắc
  quay về trạng thái cũ ngay lập tức, không cần chờ một đợt triển khai mới.
- **Không thay đổi trải nghiệm người dùng ở những phần đã hoạt động tốt**: cách hệ thống nhận biết
  "người dùng này thuộc khách hàng doanh nghiệp nào" giữ nguyên hoàn toàn — chỉ có nguồn thông tin đó
  đến từ đâu là thay đổi (từ giả lập sang xác thực thật).
- **Nền tảng cho các bước tiếp theo**: mọi tính năng liên quan tới tài khoản người dùng thật (đăng
  ký, phân quyền chi tiết theo vai trò...) sau này đều xây trên nền móng này, thay vì phải làm lại từ
  đầu.

## Giới hạn hiện tại — trung thực cần biết

- **Màn hình đăng nhập tương tác (giao diện người dùng thật để nhập tên đăng nhập/mật khẩu) chưa được
  xây trong phần này** — đây là một công việc riêng, đã được ghi nhận để làm tiếp, không nằm trong
  phạm vi đã hoàn thành. Toàn bộ phần "cấp vé, kiểm tra vé, từ chối vé giả/hết hạn" đã hoạt động và
  được kiểm chứng thật; chỉ riêng "màn hình để người dùng gõ mật khẩu" là phần còn thiếu.
- **Trong lúc kiểm chứng lần chạy thử cuối cùng trên một môi trường đầy đủ**, đội đã phát hiện và vá
  luôn ba lỗ hổng cấu hình thật — những lỗi mà không có bài kiểm tra tự động nào bắt được trước đó, vì
  chúng chỉ lộ ra khi chạy đúng như một hệ thống thật sẽ chạy. Đây chính là lý do việc chạy thử trên
  môi trường đầy đủ, thay vì chỉ tin vào các bài kiểm tra tự động, là một bước bắt buộc trước khi coi
  một tính năng là "đã xong" — chi tiết kỹ thuật dành cho đội kỹ thuật, xem
  [`docs/architecture/014_Architect_máy chủ định danh thật.md`](../architecture/014_Architect_máy%20chủ%20định%20danh%20thật.md).
