# Phase 1 Data Model: Triển khai máy chủ định danh, thay thế xác thực giả lập

Hai nhóm thực thể: những gì service `identity` mới **lưu trữ** (kho thông tin đăng nhập, đăng ký client), và những gì **đi trên dây** giữa các hop (token, claim) — xem `spec.md` Key Entities cho mô tả ở tầm nghiệp vụ.

## Identity User (lưu trữ, thuộc service `identity`)

Tài khoản đăng nhập — nguồn phát hành `sub` và `tenant_id` khi đăng nhập thành công.

| Field | Description | Notes |
|---|---|---|
| SubjectId (`sub`) | Định danh duy nhất, bất biến của tài khoản | Trở thành claim `sub` trong mọi token phát hành cho tài khoản này; là giá trị `SubjectHeaderPropagationMiddleware` đọc để sinh `X-Subject-Id` (không đổi so với Phase 1 — research.md Decision 3). |
| TenantId | Tenant duy nhất tài khoản này thuộc về | Theo spec Assumptions: một tài khoản gắn với đúng một tenant. Nguồn của claim `tenant_id` do `TenantClaimsProfileService` phát hành. |
| Credential | Thông tin xác thực đăng nhập (password hash, v.v.) | Sở hữu độc quyền bởi service `identity` (research.md Decision 8) — `parties` không bao giờ đọc/ghi trường này. |

**Validation rules**: Một `Identity User` PHẢI có đúng một `TenantId` không rỗng tại thời điểm đăng nhập thành công — không có tài khoản "chưa gán tenant" nào được phép phát hành token (spec FR-002).

## Client Application (lưu trữ, thuộc service `identity`)

Đăng ký OIDC cho từng ứng dụng gọi tới máy chủ định danh (Duende `Client`).

| Field | Description | Notes |
|---|---|---|
| ClientId | Định danh ứng dụng | Ví dụ: SPA web, SPA mobile-web (constitution: "React SPA web and mobile-web clients"). |
| AllowedGrantTypes | Luồng OIDC được phép | Authorization Code + PKCE (research.md Decision 9) — không có Resource Owner Password. |
| RedirectUris | URI callback hợp lệ sau đăng nhập | Theo domain của từng ứng dụng frontend. |
| AllowedScopes | Phạm vi token được cấp | Tối thiểu đủ để mang `openid`, `profile`, và claim `tenant_id` tuỳ biến. |

**Validation rules**: Không có grant type nào ngoài Authorization Code + PKCE được đăng ký cho client SPA (research.md Decision 9) — loại bỏ khả năng thu thập mật khẩu trực tiếp trong frontend.

## Token (trên dây, không lưu trữ bởi bên tiêu thụ)

JWT access token — được service `identity` phát hành, được gateway và (mới, từ tính năng này) BFF + 4 domain service xác thực độc lập.

| Field | Description | Notes |
|---|---|---|
| `sub` | Định danh người dùng đã xác thực | Chuẩn OIDC; ASP.NET Core tự ánh xạ vào `ClaimTypes.NameIdentifier` (research.md Decision 3) — không đổi so với những gì `StubIdentityAuthenticationHandler` từng đặt. |
| `tenant_id` | Định danh tenant đã xác minh | Claim tuỳ biến, cùng tên với `StubIdentityAuthenticationHandler.TenantClaimType` — đi qua nguyên vẹn, không bị ASP.NET Core đổi tên (research.md Decision 3). Đây là nguồn tenant MỚI thay cho giá trị gán cứng cũ (spec FR-007). |
| `iss` | Máy chủ định danh phát hành token | Kiểm tra khớp `Authority` cấu hình ở mỗi service xác thực nó. |
| `aud` | Đối tượng token nhắm tới | Kiểm tra bởi `TokenValidationParameters.ValidateAudience`. |
| `exp` | Thời điểm hết hạn | Kiểm tra tự động bởi `JwtBearerHandler` (`ValidateLifetime = true`); hết hạn → 401 rõ ràng, không âm thầm thất bại (spec FR-006, US3). |

**State machine**: Một token, tại thời điểm một service xác thực nó, có đúng ba trạng thái:

1. **Valid** — chữ ký hợp lệ (khớp JWKS đã cache — research.md Decision 5), `iss`/`aud` khớp cấu hình, `exp` chưa qua. Request được xử lý với `sub`/`tenant_id` đã xác minh.
2. **Expired** — mọi điều kiện khác đúng nhưng `exp` đã qua. Bị từ chối với phản hồi "không được phép" rõ ràng (spec FR-006, US3 Acceptance Scenario 1) — không phải một thất bại chung chung.
3. **Invalid** — chữ ký sai (giả mạo), `iss`/`aud` không khớp, hoặc thiếu/malformed. Bị từ chối như trường hợp không có token (spec FR-005/FR-010/FR-011).

Không có trạng thái thứ tư "hợp lệ nhưng thiếu `tenant_id`" được coi là một tenant nào đó — theo spec Edge Cases và FR-010, một token thiếu `tenant_id` hoặc có `tenant_id` không phân tích được PHẢI được xem như chưa xác định tenant (tương đương trạng thái Unresolved của `TenantContext`, không đổi so với [003](../003-stub-identity-tenant-context/data-model.md)).

**Validation rules**:
- Không có token nào được service `identity` phát hành mà thiếu `tenant_id` (Identity User validation rule ở trên đảm bảo điều này ngay từ nguồn) — nhưng mỗi service tiêu thụ vẫn PHẢI tự kiểm tra claim này tồn tại và không rỗng trước khi tin nó, vì token có thể đến từ bất kỳ đâu, không chỉ từ luồng đăng nhập hợp lệ (spec Edge Cases — token bị giả mạo/chỉnh sửa).
- Một request không mang token nào PHẢI bị xử lý giống hệt một token Invalid — không có "danh tính mặc định" (spec FR-011).

## Xác thực độc lập ở từng service (hành vi mới, không lưu trữ)

Hợp đồng hành vi tính năng này thêm vào mỗi service — xem `contracts/service-authentication-contract.md` cho hợp đồng đầy đủ.

| Field | Description |
|---|---|
| Cơ chế | `AddIdentityValidation()` (thư viện `shared/Identity` — research.md Decision 4), gọi ở gateway, BFF, và cả 4 domain service |
| Mặc định | `FallbackPolicy = RequireAuthenticatedUser()` — mọi endpoint yêu cầu token Valid trừ khi đánh dấu `[AllowAnonymous]` tường minh (research.md Decision 6) |
| Ngoại lệ tường minh | `/health/live`, `/health/ready` ở mọi service — probe không thể mang token |
| Độc lập với gateway | Mỗi service tự xác thực bằng JWKS đã cache của chính nó, không tin bất kỳ header nào gateway đã "xử lý xong" hộ (spec FR-004/FR-005 — gateway không phải trust boundary) |

## Feature Toggle (cấu hình, thuộc Unleash — ADR-0008)

| Field | Description | Notes |
|---|---|---|
| Tên | ví dụ `identity-server-auth-cutover` | Bọc quanh việc gateway đăng ký `AddJwtBearer` thay vì `AddScheme<StubIdentity...>` (research.md Decision 7) |
| Chủ sở hữu | Platform maintainers | Ghi nhận khi tạo toggle, theo Principle X |
| Ngày gỡ bỏ | Đặt khi tạo, sau khi cutover được xác nhận ổn định trong môi trường production | Toggle quá hạn là nợ kỹ thuật (constitution Principle X) — CI kiểm tra theo ADR-0008 |

## Relationships

```text
Đăng nhập (Authorization Code + PKCE, trang do Duende tự host)
  └─ Identity User (đã xác thực bằng credential) → Token
       ├─ claim sub        (= SubjectId)
       └─ claim tenant_id  (= TenantId đã gán cho user)
            └─ Gateway: AddJwtBearer xác thực chữ ký (toggle Unleash — research.md Decision 7)
                 └─ TenantHeaderPropagationMiddleware đọc claim tenant_id → X-Tenant-Id   (KHÔNG ĐỔI)
                 └─ SubjectHeaderPropagationMiddleware đọc claim sub → X-Subject-Id         (KHÔNG ĐỔI)
                      └─ YARP forward (headers included) →

BFF, Parties, Products, Baskets, Orders (MỚI — mỗi service tự xác thực độc lập)
  └─ AddIdentityValidation() (shared/Identity) xác thực lại chính token gốc, không tin gateway
       ├─ Valid   → FallbackPolicy pass → xử lý request; shared/Tenancy đọc X-Tenant-Id/X-Subject-Id như cũ
       ├─ Expired → 401 rõ ràng (spec US3)
       └─ Invalid/absent → 401, không danh tính mặc định (spec FR-005/FR-011)
```

## State Transitions

Không có trạng thái mới nào ngoài trạng thái ba-giá-trị của Token ở trên và trạng thái hai-giá-trị của `TenantContext` đã có từ [003](../003-stub-identity-tenant-context/data-model.md) (không đổi). `Identity User` và `Client Application` là cấu hình/dữ liệu có vòng đời riêng trong database của service `identity`, quản lý qua các quy trình vận hành bình thường (tạo tài khoản, đăng ký client) — nằm ngoài luồng request của tính năng này.
