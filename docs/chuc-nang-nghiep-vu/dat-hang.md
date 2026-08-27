# Chức năng: Đặt hàng (Order / Checkout)

*Đối tượng đọc: quản lý, người phụ trách nghiệp vụ. Không yêu cầu đọc code.*

## Chức năng này làm gì

Đây là bước chốt của luồng mua hàng: khách xác nhận mua, hệ thống chuyển giỏ hàng thành một **đơn hàng chính thức**, rồi dọn sạch giỏ hàng. Đây là chức năng duy nhất trong 4 chức năng nghiệp vụ có tính **điều phối nhiều phần** (không chỉ đọc/ghi trong một dịch vụ).

## Luồng xử lý thật (4 bước, theo đúng thứ tự cố định)

Nguồn: `services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs`, [`docs/adr/0011-checkout-orchestration.md`](../adr/0011-checkout-orchestration.md).

1. Đọc giỏ hàng hiện tại của khách.
2. Nếu giỏ hàng **rỗng** → dừng ngay, báo lỗi cho khách (không tạo đơn hàng).
3. Tạo đơn hàng chính thức từ các dòng trong giỏ (dịch vụ Đặt hàng tự tính lại tổng tiền, không tin số tiền gửi từ nơi khác).
4. Xoá sạch giỏ hàng — **chỉ thực hiện sau khi đơn hàng đã tạo thành công**, không làm trước.

Thứ tự "tạo đơn trước, xoá giỏ sau" là một quyết định thiết kế có chủ đích (không phải ngẫu nhiên) — xem mục rủi ro bên dưới để hiểu vì sao.

## Trạng thái thật hiện tại

- Toàn bộ 4 bước trên chạy **tuần tự và đồng bộ trong một request** — khách phải chờ cả 4 bước xong mới nhận được phản hồi. Hệ thống **chưa dùng hàng đợi sự kiện** (message queue) cho luồng này.
- Đơn hàng lưu: mã đơn, thời điểm đặt, tổng tiền, và nhãn "thuộc khách hàng nào" — nhãn này hiện chỉ mang tính phân loại logic, **chưa phải một ranh giới bảo mật/vật lý thật sự tách dữ liệu giữa các khách hàng** (vì hệ thống mới có một khách hàng giả lập duy nhất, xem [`khach-hang.md`](khach-hang.md)).

## Rủi ro nghiệp vụ đã được ghi nhận có chủ đích (không phải lỗi chưa phát hiện)

Vì bước 3 và bước 4 chạy tuần tự, không có cơ chế "hoàn tác" (compensation) nếu bước 4 thất bại giữa chừng:

- **Nếu tạo đơn hàng thành công (bước 3) nhưng xoá giỏ hàng (bước 4) bị lỗi** (ví dụ lỗi mạng tạm thời), đơn hàng vẫn tồn tại hợp lệ, nhưng giỏ hàng sẽ **không tự động được dọn sạch**.
- Đội phát triển đã lường trước tình huống này và chọn "tạo đơn trước, xoá giỏ sau" (thay vì ngược lại) vì: nếu phải chọn một trong hai lỗi, **có đơn hàng thật nhưng giỏ chưa dọn sạch** vẫn tốt hơn nhiều so với **giỏ hàng đã mất sạch nhưng không có đơn hàng nào để chứng minh khách đã mua** — trường hợp sau là không thể cứu vãn từ phía khách hàng.
- Để tránh khách vô tình đặt hàng 2 lần trong tình huống này, có 2 lớp chặn: (1) giao diện web tự khoá nút "Đặt hàng" trong lúc đang xử lý, (2) dịch vụ Giỏ hàng từ chối (báo lỗi 409) nếu ai đó cố xoá một giỏ hàng đã rỗng sẵn.
- Đây là một **đánh đổi đã được ghi chép chính thức và có kế hoạch khắc phục** (không phải lỗi ẩn), xem [`docs/adr/0011-checkout-orchestration.md`](../adr/0011-checkout-orchestration.md) mục "Consequences" và "Action Items". Kế hoạch khắc phục (dùng cơ chế sự kiện có đảm bảo, thay vì gọi tuần tự) thuộc hạng mục roadmap sau này (mã công việc SCRUM-31), **chưa được xây**.

## Ai sở hữu dữ liệu này

Dữ liệu đơn hàng nằm trong cơ sở dữ liệu riêng của dịch vụ **Orders**. Lớp điều phối (BFF) không tự tính tiền hay lưu trạng thái đơn hàng — nó chỉ chuyển tiếp dữ liệu giữa Giỏ hàng và Đặt hàng theo đúng thứ tự ở trên.

## Tham chiếu kỹ thuật (cho ai muốn tra cứu)

- Mã nguồn điều phối: `services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs`
- Mã nguồn tạo đơn: `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs`
- Quyết định thiết kế: [`docs/adr/0011-checkout-orchestration.md`](../adr/0011-checkout-orchestration.md)
- Sơ đồ luồng đầu-cuối: [`../thiet-ke-he-thong.md`](../thiet-ke-he-thong.md) mục "Luồng quan trọng nhất: Checkout"
- Bằng chứng đã chạy thật: [`../demo-phase-1.md`](../demo-phase-1.md)
