# Data Model: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Tính năng này không thêm bảng hay entity lưu trữ nào — không có database mới, không có schema mới (research.md không đề cập storage mới). "Data model" ở đây là các khái niệm cấu trúc mà chính sách phân quyền và cổng build/review vận hành trên đó, mirror cách [014-identity-server-auth/data-model.md](../014-identity-server-auth/data-model.md) mô tả Token/User dù không có bảng mới ngoài kho của service `identity`.

## Chính sách phân quyền (Authorization Policy)

Một chính sách được đặt tên, được đăng ký một lần trong `shared/Identity` và áp dụng giống hệt ở mọi service (research.md Decision 1/2).

| Trường | Mô tả |
|---|---|
| Tên | `ApiScope` (hằng số `AuthorizationPolicies.ApiScope`, `shared/Identity`) |
| Yêu cầu | Danh tính đã xác thực (`RequireAuthenticatedUser()`, kế thừa từ 014) VÀ claim `scope` chứa giá trị `ecommerce-api` (`Config.ApiScopeName`, `services/identity`) |
| Trạng thái toggle | Bọc trong một toggle Unleash (research.md Decision 5) — khi tắt, chỉ phần "đã xác thực" được thực thi; khi bật, cả hai điều kiện được thực thi |
| Nơi áp dụng | `FallbackPolicy` của mọi service (nâng cấp từ `AuthenticationFallbackPolicy` cũ) VÀ khai báo tường minh tại từng route nghiệp vụ qua `.RequireAuthorization(AuthorizationPolicies.ApiScope)` |

## Quyết định phân quyền theo Endpoint (Endpoint Authorization Declaration)

Trạng thái gắn với từng route trong mỗi `*Endpoints.cs` — đơn vị nhỏ nhất mà `AuthorizationPolicyDeclaredScanner` (research.md Decision 3) kiểm tra.

| Trường | Mô tả |
|---|---|
| Route | Đường dẫn + phương thức HTTP (ví dụ `POST /baskets/current/items`) |
| Loại khai báo | `RequireAuthorization(ApiScope)` (mặc định cho mọi endpoint nghiệp vụ) hoặc `AllowAnonymous()` (chỉ hai health probe mỗi service, kế thừa từ 014) |
| Vòng đời | Được đặt tại thời điểm route được thêm vào `Map*Endpoints`; scanner thất bại nếu vắng mặt cả hai loại khai báo |

## Quyết định tin cậy của Message Handler (Handler Trust Declaration)

Áp dụng cho `IConsumer<T>` — hôm nay chưa có handler nào tồn tại trong repo (research.md Decision 4); entity này định nghĩa hình dạng cho lần đầu tiên một handler được thêm.

| Trường | Mô tả |
|---|---|
| Handler | Kiểu implement `IConsumer<T>` |
| Nguồn được tin cậy | Khai báo tường minh nguồn phát hành sự kiện (ví dụ tên exchange/queue hoặc service phát hành) mà handler chấp nhận xử lý |
| Bắt buộc | `contracts/message-handler-authorization-contract.md`; scanner quét toàn `services/**/*.cs` tìm `IConsumer<` thiếu khai báo |

## Mục đối chiếu Quy tắc nghiệp vụ (Validation Parity Entry)

Một dòng trong `contracts/client-server-validation-parity-contract.md` — ánh xạ một quy tắc SPA kiểm tra phía client tới kiểm tra tương đương phía máy chủ (research.md Decision 7).

| Trường | Mô tả |
|---|---|
| Quy tắc | Mô tả ràng buộc nghiệp vụ (ví dụ "giỏ hàng không được rỗng khi checkout") |
| Vị trí kiểm tra client | File/component SPA thực thi kiểm tra (UX only) |
| Vị trí kiểm tra server | Endpoint và điều kiện thực thi độc lập phía máy chủ |
| Bằng chứng | Integration test gọi trực tiếp API, bỏ qua SPA, xác nhận máy chủ tự từ chối |

## Trạng thái phản hồi từ chối (Rejection Response States)

Không phải entity lưu trữ, nhưng là hợp đồng hành vi quan trọng cần ghi lại vì nó phân biệt hai loại thất bại khác nhau trên cùng một request (research.md Decision 6):

| Trạng thái | Khi nào | Thân phản hồi |
|---|---|---|
| 401 Unauthorized | Chưa xác thực được (không có token / token giả mạo / hết hạn) — không đổi từ 014 | `ClearUnauthorizedResponseEvents` (014, không đổi) |
| 403 Forbidden | Đã xác thực nhưng không thỏa chính sách `ApiScope` (thiếu claim `scope=ecommerce-api`) | Handler mới (research.md Decision 6), thân JSON rõ ràng thay vì mặc định rỗng của framework |
