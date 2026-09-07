# Bỏ hẳn mật khẩu viết cứng trong code — chuẩn bị cho kho bí mật của cluster

*Viết cho: người quản lý sản phẩm, stakeholder không trực tiếp code. Không yêu cầu đọc code hay biết
tên bất kỳ công cụ kỹ thuật nào.*

*Trạng thái: phần ứng dụng đã hoàn thành và đang hoạt động thật. Phần hạ tầng lưu trữ bí mật thật sự
(kho bí mật trung tâm của cluster) CHƯA được dựng — xem "Giới hạn hiện tại" để hiểu rõ ranh giới này,
đây là điểm quan trọng nhất của tài liệu này.*

## Vấn đề trước đây

Mỗi bộ phận của hệ thống cần 1 mật khẩu để kết nối tới cơ sở dữ liệu riêng của nó. Trước đây, các mật
khẩu này (dù chỉ dùng cho máy phát triển cục bộ, không phải mật khẩu thật của môi trường vận hành)
**nằm thẳng trong file cấu hình đã được lưu vào lịch sử của kho mã nguồn**. Đây là 1 rủi ro bảo mật cơ
bản cần loại bỏ: bất kỳ ai có quyền xem lịch sử kho mã nguồn cũng nhìn thấy được các giá trị đó, và
thói quen "viết mật khẩu thẳng vào file" nếu không bị chặn lại sẽ dễ lặp lại sai ở môi trường thật sau
này.

## Giải pháp: không mật khẩu nào nằm trong file đã lưu, và bộ phận nào thiếu thì từ chối chạy

Hai thay đổi song song:

1. **Xoá sạch mọi giá trị nhạy cảm khỏi file cấu hình đã lưu vào kho mã nguồn.** Mỗi bộ phận giờ đọc
   mật khẩu của nó từ 1 nguồn được cấp riêng lúc khởi động, không phải từ file đã commit.
2. **Nếu 1 bộ phận khởi động mà không nhận được mật khẩu nó cần, nó TỪ CHỐI CHẠY ngay lập tức** — thay
   vì âm thầm khởi động và chỉ lộ ra lỗi khi có người dùng đầu tiên gặp phải. Đây là thay đổi quan
   trọng về triết lý: thà dừng hẳn và báo lỗi rõ ràng ngay từ đầu, còn hơn chạy "nửa vời" rồi gây lỗi
   khó hiểu cho người dùng sau này.

Song song đó, hệ thống có thêm 2 lớp kiểm tra tự động chạy trên MỌI thay đổi mã nguồn: 1 lớp quét toàn
bộ lịch sử kho mã nguồn tìm dấu vết mật khẩu bị lộ, và 1 lớp quét bên trong chính image đã đóng gói
sẵn của từng bộ phận để đảm bảo không có mật khẩu nào vô tình bị "nướng" vào đó.

## Trải nghiệm thực tế diễn ra như thế nào

1. **Người phát triển chạy hệ thống trên máy mình** — nếu dùng cách chạy chuẩn (script có sẵn), mật
   khẩu được tự động cấp, không cần thao tác gì thêm.
2. **Nếu người phát triển chạy 1 bộ phận riêng lẻ mà quên cấp mật khẩu** — bộ phận đó dừng ngay lúc
   khởi động, kèm thông báo rõ ràng "thiếu mật khẩu tên gì", thay vì chạy lên rồi lỗi mơ hồ khi có
   request đầu tiên.
3. **Mỗi lần có thay đổi mã nguồn gửi lên hệ thống kiểm tra tự động**, 2 lớp quét mật khẩu chạy tự
   động — nếu phát hiện 1 mật khẩu bị lộ (mới hoặc cũ), quá trình kiểm tra báo lỗi ngay, không chờ
   tới lúc triển khai.

## Điều đặc biệt: đã kiểm chứng thật, không chỉ thiết kế trên giấy

- **Chạy công cụ quét bí mật thật trên toàn bộ lịch sử kho mã nguồn** — không chỉ quét thay đổi mới
  nhất. Các mật khẩu cũ (đã bị xoá khỏi phiên bản hiện tại nhưng còn dấu vết trong lịch sử — việc viết
  lại lịch sử để xoá hẳn bị coi là hành động rủi ro cao, không làm) được ghi nhận là "đã biết, đã khắc
  phục" 1 lần — công cụ quét từ nay chỉ báo động cho phát hiện MỚI, không lặp lại cảnh báo cho những
  gì đã biết.
- **Chuẩn bị sẵn "hợp đồng"** cho từng bộ phận cần cấp mật khẩu gì, đặt tên gì — để khi kho bí mật
  trung tâm thật sự được dựng (xem phần giới hạn dưới đây), việc kết nối vào chỉ còn là áp dụng đúng
  hợp đồng đã có sẵn, không phải thiết kế lại từ đầu.

## Lợi ích kinh doanh

- **Giảm hẳn rủi ro rò rỉ mật khẩu qua kho mã nguồn** — 1 trong những nguyên nhân rò rỉ dữ liệu phổ
  biến nhất trong ngành phần mềm.
- **Phát hiện lỗi cấu hình ngay khi khởi động, không phải khi khách hàng đang dùng** — giảm thời gian
  gián đoạn dịch vụ do thiếu cấu hình.
- **Sẵn sàng cho việc đổi mật khẩu định kỳ không cần dừng hệ thống** — khi hạ tầng thật được dựng, đổi
  1 mật khẩu không còn cần phải triển khai lại toàn bộ hệ thống.

## Giới hạn hiện tại — trung thực cần biết, đây là phần quan trọng nhất

**Kho bí mật trung tâm thật của cluster (HashiCorp Vault, và bộ phận đồng bộ nó vào hệ thống hạ tầng)
CHƯA được dựng ở đâu cả** — đây không phải sơ suất, mà là 1 quyết định phạm vi có chủ đích: phần này
chỉ hoàn thành **nửa ứng dụng** (mã nguồn đã sẵn sàng nhận mật khẩu đúng cách, đã có "hợp đồng" rõ
ràng cho từng bộ phận cần gì), còn **nửa hạ tầng** (dựng kho bí mật thật, nối nó vào hệ thống) vẫn là
công việc còn để ngỏ, thuộc về đội hạ tầng, chưa có deadline trong phạm vi tài liệu này. Nói cách khác:
ứng dụng đã "sẵn sàng chờ", nhưng chưa có gì thật sự "cấp phát" cho nó ở môi trường một cluster thật.

Chi tiết kỹ thuật đầy đủ (bao gồm quyết định kiến trúc gốc và ghi chú cập nhật về việc phần hạ tầng
này vẫn còn để ngỏ) dành cho đội kỹ thuật, xem
[`docs/architecture/018_Architect_secrets qua cluster secret store.md`](../architecture/018_Architect_secrets%20qua%20cluster%20secret%20store.md).
