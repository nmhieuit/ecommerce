# Contract: Claim của Access Token

Hợp đồng giữa service `identity` (bên phát hành) và mọi bên tiêu thụ token (gateway, và mới từ tính năng này: BFF + 4 domain service). Đây là giao diện bên ngoài duy nhất tính năng này thêm vào ở tầng xác thực — khác với [contracts/tenant-id-header.md](../003-stub-identity-tenant-context/contracts/tenant-id-header.md) của [003](../003-stub-identity-tenant-context/), vốn vẫn không đổi và không lặp lại ở đây.

## Token

| | |
|---|---|
| Loại | JWT (JSON Web Token), ký RS256 |
| Vận chuyển | HTTP header `Authorization: Bearer <token>` |
| Cardinality | Đúng một token mỗi request đã xác thực, hoặc vắng mặt (request ẩn danh) |

## Claims

| Claim | Bắt buộc | Mô tả | Ánh xạ phía tiêu thụ |
|---|---|---|---|
| `sub` | Có | Định danh người dùng đã xác thực | ASP.NET Core tự ánh xạ vào `ClaimTypes.NameIdentifier` (mặc định `MapInboundClaims=true`) — giống hệt claim `StubIdentityAuthenticationHandler` từng đặt (research.md Decision 3) |
| `tenant_id` | Có | Định danh tenant đã xác minh của người dùng | Đọc trực tiếp bởi tên `"tenant_id"` — claim tuỳ biến, không bị đổi tên — bởi `TenantHeaderPropagationMiddleware` ở gateway |
| `iss` | Có | URL của service `identity` đã phát hành token | Kiểm tra khớp `Authority`/`ValidIssuer` cấu hình ở từng service |
| `aud` | Có | Đối tượng token nhắm tới | Kiểm tra bởi `ValidateAudience` |
| `exp` | Có | Thời điểm hết hạn (Unix timestamp) | Kiểm tra tự động bởi `JwtBearerHandler` |

## Producers

| Nguồn | Hành vi |
|---|---|
| Service `identity` (`TenantClaimsProfileService`, một `IProfileService` của Duende) | **Phát hành duy nhất.** Chỉ nguồn duy nhất được phép tạo token hợp lệ — chữ ký RS256 dùng khoá riêng chỉ service này giữ. Mọi claim `tenant_id` phát hành ra khớp đúng `TenantId` đã gán cho `Identity User` đăng nhập (data-model.md) — không có luồng nào cho phép người dùng tự chọn tenant của mình. |

## Consumers

| Hop | Hành vi |
|---|---|
| Gateway (`AddJwtBearer`, thay `StubIdentityAuthenticationHandler` — research.md Decision 2, bọc trong toggle Unleash) | **Xác thực và chuyển đổi.** Xác minh chữ ký/`iss`/`aud`/`exp` bằng JWKS đã cache (research.md Decision 5); nếu hợp lệ, `TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` (không đổi) chuyển `tenant_id`/`sub` thành `X-Tenant-Id`/`X-Subject-Id` cho các hop phía sau — đúng hành vi đã có từ [003](../003-stub-identity-tenant-context/). |
| BFF, Parties, Products, Baskets, Orders (`AddIdentityValidation()`, thư viện `shared/Identity` — MỚI, research.md Decision 4) | **Xác thực độc lập.** Mỗi service tự xác minh lại chính token gốc (Authorization header được YARP chuyển tiếp mặc định, không bị gateway lột bỏ) bằng JWKS đã cache của riêng nó — không tin việc gateway đã xác thực xong (spec FR-004/FR-005). Việc đọc `X-Tenant-Id`/`X-Subject-Id` để lấy tenant/subject context vẫn qua `shared/Tenancy` như cũ; việc xác thực token là một lớp kiểm tra bổ sung, độc lập, song song. |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Token giả mạo (chữ ký sai) gửi thẳng tới một domain service, bỏ qua gateway | Service đó tự từ chối (401) — không cần gateway đã chặn từ trước (spec Test Scenario 2, US2) |
| Token hết hạn (`exp` đã qua) | Từ chối rõ ràng — 401, không phải một thất bại chung chung/âm thầm (spec Test Scenario 3, US3) |
| Token thiếu `tenant_id`, hoặc giá trị không phân tích được | Xác thực chữ ký có thể vẫn qua (token hợp lệ về mặt OIDC), nhưng `tenant_id` được coi như chưa xác định — `shared/Tenancy` xử lý y hệt trường hợp header `X-Tenant-Id` vắng mặt (data-model.md — không có trạng thái "resolved to a default") |
| Không có token nào (`Authorization` header vắng mặt) | Bị từ chối giống một token Invalid — `FallbackPolicy = RequireAuthenticatedUser()` chặn ở mọi endpoint trừ health probe (spec FR-011) |

## Stability

Đây là hợp đồng nội bộ giữa các service trong cùng một hệ thống triển khai, không phải hợp đồng công khai cho client bên ngoài theo nghĩa versioning của constitution Principle II (không có consumer bên ngoài, không cần cửa sổ deprecation). Việc thêm claim mới trong tương lai (ví dụ scope/role cho RBAC chi tiết hơn) là bổ sung (additive) — không đổi tên hay bỏ `sub`/`tenant_id` hiện có, vì toàn bộ `shared/Tenancy` phía dưới phụ thuộc vào chúng.
