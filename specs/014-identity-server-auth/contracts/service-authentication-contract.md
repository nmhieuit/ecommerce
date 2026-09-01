# Contract: Chính sách xác thực mặc định của từng service

Hợp đồng hành vi giữa `shared/Identity` (thư viện chia sẻ mới — research.md Decision 4) và mỗi service áp dụng nó. Đây là điều thay thế giá trị `authentication: anonymous` hiện đang ghi trong mọi `service-manifest.yaml` (gateway, bff, parties, products, baskets, orders) kèm chú thích "no identity server yet — see plan.md Constitution Check (VI)".

## Cam kết

| | |
|---|---|
| Áp dụng cho | Gateway, BFF, Parties, Products, Baskets, Orders — mọi service có endpoint HTTP công khai |
| Cơ chế | `AddIdentityValidation()` (đăng ký `AddJwtBearer` + `FallbackPolicy`) từ `shared/Identity`, gọi trong `Program.cs` của từng service |
| Mặc định | `FallbackPolicy = RequireAuthenticatedUser()` — mọi endpoint yêu cầu token Valid (xem `identity-token-claims-contract.md`) trừ khi được đánh dấu ngoại lệ tường minh |
| Ngoại lệ | `[AllowAnonymous]` tường minh, chỉ áp dụng cho health probe (`GET /health/live`, `GET /health/ready`) — probe của Kubernetes không thể mang token |

## Trước và sau tính năng này

| | Trước (Phase 1/2) | Sau (tính năng này) |
|---|---|---|
| `service-manifest.yaml` | `authentication: anonymous` ở mọi endpoint | `authentication: bearer` (hoặc tương đương) ở mọi endpoint nghiệp vụ; `authentication: anonymous` chỉ còn ở hai health probe |
| Gateway | Xác thực bằng `StubIdentityAuthenticationHandler` (luôn thành công, không xác thực thật) | Xác thực bằng `AddJwtBearer` thật, bọc trong toggle Unleash (research.md Decision 7) |
| BFF, Parties, Products, Baskets, Orders | Không xác thực gì — tin tưởng ngầm định các header `X-Tenant-Id`/`X-Subject-Id` do gateway đặt | Tự xác thực token độc lập qua `AddIdentityValidation()`, không phụ thuộc gateway đã xác thực xong (spec FR-004) |

## Producers

| Nguồn | Hành vi |
|---|---|
| `shared/Identity` (`IdentityValidationExtensions.AddIdentityValidation`/`UseIdentityValidation`) | Cung cấp cấu hình `AddJwtBearer`/`FallbackPolicy` dùng chung — mỗi service chỉ gọi, không tự cấu hình lại (research.md Decision 4), đúng khuôn mẫu `AddTenancy()`/`UseTenancy()` của `shared/Tenancy`. |

## Consumers

| Hop | Hành vi |
|---|---|
| Mỗi service (gateway, bff, 4 domain service) | Gọi `AddIdentityValidation()` trong `Program.cs`, ngay cạnh `AddServiceDefaults()`/`AddTenancy()` đã có. Endpoint nghiệp vụ mới thêm vào sau này tự động rơi vào `FallbackPolicy` — không cần khai báo gì thêm để được bảo vệ; chỉ endpoint muốn ẩn danh mới cần khai báo tường minh. |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Request không có token, gọi một endpoint nghiệp vụ | 401 — `FallbackPolicy` chặn trước khi handler chạy (spec FR-011) |
| Request có token hết hạn | 401 rõ ràng, không phải lỗi chung chung (spec FR-006, US3) |
| Request có token giả mạo, gửi thẳng tới một domain service, bỏ qua gateway | Service đó tự chặn — không phụ thuộc gateway đã xác thực trước (spec Test Scenario 2, US2) |
| Request gọi health probe, không có token | Cho qua bình thường — `[AllowAnonymous]` tường minh, không phải một lỗ hổng "quên" |
| Một service thêm endpoint mới, không khai báo gì về xác thực | Endpoint đó tự động yêu cầu token Valid (an toàn theo mặc định — trái với việc quên đánh dấu `[Authorize]` trong mô hình allow-by-default cũ) |

## Stability

Hợp đồng nội bộ, không phải giao diện client bên ngoài theo nghĩa versioning của constitution Principle II. Thay đổi dự kiến trong tương lai — thêm policy chi tiết hơn theo scope/role cho RBAC — là bổ sung trên nền `FallbackPolicy` này (ví dụ endpoint cụ thể override bằng `[Authorize(Policy = "...")]` nghiêm ngặt hơn), không phải thay đổi phá vỡ mặc định "yêu cầu đăng nhập" đã thiết lập ở đây.
