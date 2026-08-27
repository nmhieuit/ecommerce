# Chức năng: Khách hàng / Đối tác (Parties)

*Đối tượng đọc: quản lý, người phụ trách nghiệp vụ. Không yêu cầu đọc code.*

## Chức năng này làm gì

Tra cứu thông tin cơ bản của một khách hàng/đối tác theo mã định danh — hiện chỉ trả về mã (Id) và tên hiển thị (DisplayName). Bằng chứng: `services/parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs` (`GET /parties/{partyId}`).

## Trạng thái thật hiện tại — quan trọng nhất cần biết

- **Chưa có màn hình đăng ký hay đăng nhập thật.** Toàn bộ hệ thống hiện dùng **một danh tính giả lập cố định** cho mọi request: tên thuê bao (tenant) cố định là `"contoso"`, người dùng cố định là `"phase1-stub-user"`. Bằng chứng: `services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs`.
- Hệ quả trực tiếp: **hiện tại không thể phân biệt hai khách hàng khác nhau khi demo** — mọi thao tác (xem giỏ hàng, đặt hàng...) đều được gán cho cùng một "khách hàng giả lập" này.
- **Giao diện web (storefront) hiện chưa có màn hình nào gọi tới chức năng tra cứu khách hàng này.** Ba màn hình hiện có của giao diện web là: Sản phẩm, Giỏ hàng, Xác nhận đặt hàng — không có màn hình "thông tin khách hàng". Chức năng này đã sẵn sàng ở tầng API (kể cả đã có sẵn đường gọi qua lớp điều phối BFF) nhưng **chưa được dùng trong luồng mua hàng thực tế trên giao diện**.

## Vì sao thiết kế như vậy

Xây dựng đăng nhập thật (định danh thật, nhiều khách hàng, phân quyền) là một hạng mục lớn, được roadmap xếp vào giai đoạn sau (thay thế "Identity Server thật"). Ở giai đoạn hiện tại, dùng danh tính giả lập giúp luồng mua hàng khung sườn chạy được mà không phải chờ toàn bộ hạ tầng đăng nhập hoàn chỉnh.

## Ai sở hữu dữ liệu này

Dữ liệu khách hàng/đối tác nằm trong cơ sở dữ liệu riêng của dịch vụ **Parties**, tách biệt với Sản phẩm, Giỏ hàng, Đặt hàng.

## Tham chiếu kỹ thuật (cho ai muốn tra cứu)

- Mã nguồn: `services/parties/src/Parties.Api/`
- Endpoint nghiệp vụ: `Features/Parties/PartyEndpoints.cs` (`GET /parties/{partyId:guid}`)
- Cơ chế danh tính giả lập: `services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs`
- Kế hoạch thay thế bằng đăng nhập thật: [`../roadmap.md`](../roadmap.md) (hạng mục Identity Server, Giai đoạn 3)
