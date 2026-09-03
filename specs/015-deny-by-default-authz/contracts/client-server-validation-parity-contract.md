# Contract: Đối chiếu quy tắc nghiệp vụ SPA ↔ máy chủ

Hợp đồng kiểm kê (không phải cơ chế thực thi thời gian chạy): liệt kê từng quy tắc nghiệp vụ mà SPA kiểm tra ở phía client, và kiểm tra tương đương bắt buộc phải tồn tại độc lập ở phía máy chủ (spec US3, FR-006/FR-007). Cập nhật tài liệu này là một phần bắt buộc của bất kỳ PR nào thêm kiểm tra phía client mới vào SPA (constitution Principle VI).

## Bảng đối chiếu (cập nhật sau khi rà soát bộ test hiện có — 015-deny-by-default-authz)

| Quy tắc nghiệp vụ | Kiểm tra phía client (SPA) | Kiểm tra phía máy chủ | Bằng chứng (integration test) | Trạng thái |
|---|---|---|---|---|
| Giỏ hàng không được rỗng khi checkout | `CheckoutButton.tsx` — nút bị vô hiệu hóa khi `itemCount === 0` | `CheckoutEndpoints.cs` (`bff`) — trả về 409 khi giỏ hàng rỗng | `CheckoutTests.Checkout_ReturnsConflict_WhenTheBasketIsEmpty` (`Bff.Api.IntegrationTests`, đã có sẵn) | Đã có kiểm tra độc lập ở cả hai phía, đã có test tự động gọi thẳng API — không cần bổ sung |
| Số lượng thêm vào giỏ phải ≥ 1 | Không có ô nhập số lượng ở SPA hôm nay — `AddToBasketButton.tsx` luôn gửi `quantity: 1` cố định | `BasketEndpoints.cs` (`baskets`) — trả về 400 khi `Quantity < 1` | `CurrentBasketTests.AddItem_Rejects_AQuantityBelowOne` (`Baskets.Api.IntegrationTests`, đã có sẵn) | Máy chủ nghiêm ngặt hơn client (client không cho phép người dùng nhập giá trị sai); không có khoảng trống |
| Đơn giá không được âm | Không áp dụng — SPA không gửi giá, giá được BFF/baskets resolve từ catalog | `BasketEndpoints.cs` (`baskets`) — trả về 400 khi `UnitPrice < 0` | `CurrentBasketTests.AddItem_Rejects_ANegativeUnitPrice` (`Baskets.Api.IntegrationTests`, **thêm mới bởi 015** — đây là khoảng trống bằng chứng duy nhất tìm thấy khi rà soát) | Chỉ có kiểm tra phía máy chủ — đúng theo thiết kế, vì client không phải là nguồn của giá trị này |
| Đơn hàng phải có ít nhất một dòng | Không áp dụng trực tiếp ở SPA (checkout luôn xuất phát từ giỏ hàng đã có dòng, được đảm bảo bởi quy tắc "giỏ không rỗng" ở trên) | `OrderEndpoints.cs` (`orders`) — trả về 400 khi `Items` rỗng | `PlaceOrderTests.PlaceOrder_Rejects_ARequestWithNoLines` (`Orders.Api.IntegrationTests`, đã có sẵn) | Máy chủ tự bảo vệ độc lập với đường gọi (BFF hay trực tiếp) |

## Quy trình cập nhật

1. Khi một PR thêm một kiểm tra phía client mới vào SPA (validate input, disable một control, v.v.), PR đó PHẢI thêm một dòng tương ứng vào bảng trên, trỏ tới kiểm tra phía máy chủ độc lập đã tồn tại hoặc được thêm cùng PR.
2. Nếu kiểm tra phía máy chủ tương đương chưa tồn tại, PR PHẢI bổ sung nó — không được chỉ dựa vào kiểm tra phía client (constitution Principle VI: "client-side validation is UX only").
3. Mỗi dòng trong bảng PHẢI có ít nhất một integration test gọi trực tiếp API, bỏ qua SPA, chứng minh máy chủ tự từ chối dữ liệu vi phạm (xem `quickstart.md` — Scenario 4).

## Stability

Tài liệu sống — cập nhật liên tục khi SPA hoặc API thay đổi. Không phải một giao diện client bên ngoài theo nghĩa versioning của constitution Principle II.
