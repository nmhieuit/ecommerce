# Chức năng: Giỏ hàng (Basket)

*Đối tượng đọc: quản lý, người phụ trách nghiệp vụ. Không yêu cầu đọc code.*

## Chức năng này làm gì

Cho phép người mua giữ tạm các sản phẩm muốn mua, trước khi xác nhận đặt hàng chính thức. Gồm 3 thao tác:

1. Xem giỏ hàng hiện tại của mình.
2. Thêm một sản phẩm vào giỏ.
3. Xoá sạch giỏ (chỉ xảy ra tự động sau khi đặt hàng thành công — xem [`dat-hang.md`](dat-hang.md)).

Bằng chứng: `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` (`GET /baskets/current`, `POST /baskets/current/items`, `POST /baskets/current/clear`).

## Trạng thái thật hiện tại

- **Giỏ hàng gắn theo "người gọi" (caller), không gắn theo tài khoản đăng nhập thật** — vì hệ thống đăng nhập hiện đang giả lập (xem [`khach-hang.md`](khach-hang.md)). Điều này có một hệ quả cần lưu ý: **trong môi trường demo hiện tại, mọi người test trên cùng một máy đang dùng chung một danh tính giả lập cố định, nên sẽ nhìn thấy chung một giỏ hàng** — đây không phải lỗi, mà là hệ quả tất yếu của việc đăng nhập chưa làm thật.
- Chưa hỗ trợ: sửa số lượng một dòng đã có trong giỏ, xoá riêng một dòng (chỉ có xoá sạch toàn bộ giỏ).
- Tổng tiền của giỏ hàng được **tính lại tại chỗ mỗi lần đọc**, không lưu sẵn một con số tổng có thể bị lệch.
- Không giới hạn nào về việc bỏ trùng cùng một sản phẩm nhiều lần (mỗi lần thêm là một dòng mới) — cần xác nhận thêm với đội phát triển nếu nghiệp vụ mong muốn gộp dòng trùng sản phẩm.

## Vì sao thiết kế như vậy

Đủ để khách "chuẩn bị đơn hàng" trước khi xác nhận — đúng bước tối thiểu của luồng mua hàng khung sườn (walking skeleton).

## Ai sở hữu dữ liệu này

Dữ liệu giỏ hàng nằm trong cơ sở dữ liệu riêng của dịch vụ **Baskets**. Dịch vụ **Đặt hàng (Orders)** không đọc thẳng vào đây — khi đặt hàng, lớp điều phối (BFF) đọc giỏ hàng qua API rồi mới gửi dữ liệu đó sang Orders (xem luồng chi tiết tại [`../thiet-ke-he-thong.md`](../thiet-ke-he-thong.md)).

## Một điểm kỹ thuật đáng chú ý cho quản lý (chưa phải rủi ro, nhưng nên biết)

Có sẵn đoạn code chuẩn bị dữ liệu để "báo tin" cho phần khác của hệ thống biết là một giỏ hàng vừa được chốt mua (`Features/Checkout/BasketCheckedOutMapper.cs`), nhưng theo đúng ghi chú của chính đoạn code đó: **hiện chưa có ai thực sự gửi đi tin báo này** — cơ chế hàng đợi và bên nhận tin chưa được xây (việc đó thuộc một hạng mục roadmap sau này, mã công việc SCRUM-31). Nói cách khác: đây là phần móng đã đặt sẵn cho tương lai, chưa phải phần đang chạy.

## Tham chiếu kỹ thuật (cho ai muốn tra cứu)

- Mã nguồn: `services/baskets/src/Baskets.Api/`
- Endpoint nghiệp vụ: `Features/Baskets/BasketEndpoints.cs`
- Đoạn chuẩn bị sự kiện (chưa dùng thật): `Features/Checkout/BasketCheckedOutMapper.cs`
