# Contract: Chính sách phân quyền tường minh trên từng endpoint

Hợp đồng hành vi giữa `shared/Identity` (mở rộng từ 014-identity-server-auth) và mỗi service HTTP công khai (gateway, bff, parties, products, baskets, orders). Thay thế/nâng cấp `contracts/service-authentication-contract.md` (014) ở đúng phần "Stability" đã dự đoán trước: "thêm policy chi tiết hơn theo scope/role cho RBAC... là bổ sung trên nền `FallbackPolicy` này."

## Cam kết

| | |
|---|---|
| Áp dụng cho | Mọi HTTP endpoint nghiệp vụ ở BFF, Parties, Products, Baskets, Orders. Gateway áp dụng gián tiếp qua `FallbackPolicy` dùng chung (route catch-all duy nhất, không có route nghiệp vụ riêng biệt để khai báo từng cái). |
| Chính sách | `AuthorizationPolicies.ApiScope` — `RequireAuthenticatedUser()` + claim `scope` chứa `ecommerce-api` (data-model.md — Chính sách phân quyền) |
| Khai báo tường minh | Mỗi route nghiệp vụ gọi `.RequireAuthorization(AuthorizationPolicies.ApiScope)`; mỗi health probe gọi `.AllowAnonymous()`. Không có route nào không gọi một trong hai. |
| Toggle | Bọc trong một cờ Unleash mới (research.md Decision 5) — tắt thì chỉ còn yêu cầu xác thực (hành vi y hệt trước tính năng này), bật thì thêm yêu cầu scope. |

## Trước và sau tính năng này

| | Trước (014) | Sau (tính năng này) |
|---|---|---|
| `FallbackPolicy` | `RequireAuthenticatedUser()` — không kiểm tra token được cấp cho mục đích gì | `RequireAuthenticatedUser()` + `RequireClaim("scope", "ecommerce-api")` (toggle-gated) |
| Từng route nghiệp vụ | Không khai báo gì — bảo vệ hoàn toàn ngầm định qua `FallbackPolicy` | Khai báo tường minh `.RequireAuthorization(ApiScope)` tại chính route đó |
| Token thiếu scope `ecommerce-api` nhưng đã xác thực | Được chấp nhận (200) — khoảng trống chưa được phát hiện trước tính năng này | Bị từ chối (403) khi toggle bật |
| `service-manifest.yaml` | Trường `authentication: bearer\|anonymous` | Thêm trường `authorization: ecommerce-api-scope\|anonymous` song song, cho mục đích tài liệu hóa (không phải cơ chế thực thi — xem Producers) |

## Producers

| Nguồn | Hành vi |
|---|---|
| `shared/Identity` (`AuthorizationPolicies`, `AuthenticationFallbackPolicy` nâng cấp) | Định nghĩa và đăng ký chính sách `ApiScope` dùng chung — mỗi service chỉ gọi `AddIdentityValidation()` như đã có, không tự định nghĩa lại chính sách (đúng khuôn mẫu research.md Decision 4 của 014). |
| Mỗi `*Endpoints.cs` (bff, baskets, orders, parties, products) | Khai báo `.RequireAuthorization(AuthorizationPolicies.ApiScope)` trên từng route nghiệp vụ mới hoặc đã có. |

## Consumers

| Hop | Hành vi |
|---|---|
| `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScanner` | Đọc mã nguồn đã commit, xác nhận mọi `Map*` có đúng một trong hai khai báo — chặn merge nếu không (research.md Decision 3). |
| Integration test của từng service (`AuthorizationPolicyTests.cs` mới, mirror `IndependentTokenValidationTests.cs`) | Gửi một token hợp lệ nhưng thiếu claim `scope=ecommerce-api`, xác nhận 403; gửi token đầy đủ, xác nhận 200/201 như trước. |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Không có token | 401 — không đổi từ 014 |
| Token hợp lệ, đủ claim `scope=ecommerce-api` | Yêu cầu được xử lý bình thường |
| Token hợp lệ, đã xác thực, nhưng thiếu claim `scope=ecommerce-api` | 403 — thân phản hồi rõ ràng (research.md Decision 6), khi toggle bật |
| Toggle tắt | Hành vi rơi về đúng như 014 (chỉ yêu cầu xác thực) — không có request nào bị chặn thêm |
| Một endpoint mới được thêm mà không gọi `.RequireAuthorization(...)` hay `.AllowAnonymous()` | `FallbackPolicy` (đã nâng cấp) vẫn bảo vệ nó ở runtime, nhưng scanner ở trên chặn merge do thiếu khai báo tường minh — hai lớp phòng thủ độc lập |

## Stability

Hợp đồng nội bộ, không phải giao diện client bên ngoài (constitution Principle II versioning không áp dụng). Client SPA (`ecommerce-web-spa`) và client kiểm thử (`integration-test-ropc`) đã được cấp scope `ecommerce-api` từ 014 — không cần thay đổi đăng ký client nào ở `services/identity/src/Identity.Api/Config.cs`.
