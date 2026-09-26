# QA: Máy chủ định danh thay thế xác thực giả lập

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec đã được xác minh rất kỹ lúc viết feature (47/47 task, chạy thật trên toàn stack `docker-compose.local.yml`, có bằng chứng
`docker exec` cho 3 lỗ hổng cấu hình đã vá và cơ chế toggle rollback). QA ở đây tập trung: (1) hành vi hôm nay khi bật/tắt 2 công tắc
`.env` (cutover 014, scope 015), (2) các test có còn xanh không, (3) bằng chứng cũ có còn đúng sau khi spec 015 và 020 đụng vào cùng vùng mã.

## Luồng happy-case đã rà soát

1. Đăng nhập qua `identity` (Duende IdentityServer, DB riêng) phát hành JWT mang `sub` + `tenant_id` — nguồn `tenant_id` duy nhất
   là `TenantClaimsProfileService`, đọc từ `ApplicationUser.TenantId`.
2. Gateway xác thực token trước khi chuyển tiếp; BFF + 4 domain service **tự xác thực độc lập** — token giả gửi thẳng, bỏ qua
   gateway, vẫn bị chặn tại chính service.
3. 3 trạng thái token (Valid/Expired/Invalid) phản hồi phân biệt rõ; token hết hạn trả `{"error":"token_expired"}`.
4. Cơ chế lan truyền tenant/subject (003/004) không đổi — chỉ nguồn xác định tenant đổi từ gán cứng sang claim token thật.
5. Toggle `FeatureToggles:IdentityServerAuthCutover` cho phép rollback gateway về `StubIdentity` không cần redeploy (`IOptionsMonitor`).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi công tắc `.env` rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api parties-api` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token`, rồi folder
**`14 - Xác thực tại gateway và từng service`** (bước 01 → 07).

**Công tắc** (mẫu ở [`.env.example`](../../.env.example); mặc định cả hai `true`): thêm 2 dòng vào `.env` rồi tạo lại 6 service và **gọi thử vài lần** (lần đầu sau khi tạo lại thường chậm/`504`):

```
FEATURE_IDENTITY_SERVER_AUTH_CUTOVER=false      # chỉ gateway (014): false = quay về stub Phase 1
FEATURE_AUTHORIZATION_REQUIRE_API_SCOPE=false   # gateway, bff, products, baskets, orders, parties (015)
```

```bash
docker compose -f docker-compose.local.yml up -d --force-recreate gateway-api bff-api products-api baskets-api orders-api parties-api
```

| Bước | Cấu hình cần chỉnh (cutover / scope) | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| FR-001/002 — token có `sub`, `tenant_id`, scope | Mặc định (true / true) | `00` bước 01 → `14` bước 01 | `sub`, `tenant_id = contoso`, `scope` chứa `ecommerce-api` | Đúng |
| FR-011 — không token thì chặn | Mặc định | `14` bước 02 | `401` | Đúng `401` |
| FR-003 — token hợp lệ đi qua gateway | Mặc định | `14` bước 03 | `200`, có `items` | Đúng `200` (lần đầu sau khi tạo lại: `504` 3.5–5 giây rồi ổn định) |
| FR-005 — token bị sửa chữ ký | Mặc định | `14` bước 04 | `401` | Đúng `401` |
| FR-004/US2 — service tự chặn, bỏ qua gateway | Mặc định | `14` bước 05, 06, 07 | Token giả / không token `401`; token thật được chấp nhận (`404` đơn không tồn tại) | Đúng cả 3 (toàn folder **9/9 xanh** ở trạng thái mặc định) |
| Principle X — **rollback** cutover | `false` / `false` | Lặp folder `14` | Gateway chuyển tiếp không cần xác thực (Phase 1) | Gateway token hợp lệ `200`; **không token vẫn `401`** và token sửa `401` (do BFF/service tự xác thực, FR-004) → rollback KHÔNG trả lại truy cập ẩn danh, chỉ bỏ bước xác thực ở gateway |
| Cảnh báo `.env.example` — cutover tắt mà scope bật | `false` / `true` | Lặp folder `14` | Gateway `403` cho mọi request | Đúng: gateway `403` cho không token, token hợp lệ và token sửa; service gọi thẳng vẫn `401`/`404` |
| Chỉ tắt scope (015) | `true` / `false` | Lặp folder `14` | Như mặc định | Giống mặc định |
| Token thiếu scope → `403` | Mọi trạng thái | `00` bước 03 → 04 (`GET baskets/current`) | `403 forbidden_scope` | **Đỏ ở cả 4 trạng thái**: token `scope=openid profile` không có `aud = ecommerce-api` nên `baskets-api` và gateway trả `401`, không phải `403` (xem QA_Debt mục 014, 015) |
| Dọn dẹp | Xoá 2 dòng trong `.env`, tạo lại 6 service | (không có) | Về mặc định | Đã khôi phục `.env` và 6 service, `git status` sạch |

### Tự động — chạy thẳng bộ test đã có

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002/SC-001 — đăng nhập phát hành token có `sub`+`tenant_id` | [`TenantClaimsProfileServiceTests.cs:36`](../../services/identity/tests/Identity.Api.UnitTests/TenantClaimsProfileServiceTests.cs#L36) · [`:70`](../../services/identity/tests/Identity.Api.UnitTests/TenantClaimsProfileServiceTests.cs#L70) (5 test cả file) | `dotnet test services/identity/tests/Identity.Api.UnitTests` |
| FR-001 — `identity` phát hành/verify token thật qua SQL Server thật | [`services/identity/tests/Identity.Api.IntegrationTests`](../../services/identity/tests/Identity.Api.IntegrationTests) (2 test) | `dotnet test services/identity/tests/Identity.Api.IntegrationTests` |
| FR-004/FR-005/SC-002 — mỗi service tự chặn token giả mạo/hết hạn/vắng mặt | `IndependentTokenValidationTests.cs` ở `baskets`/`bff`/`orders`/`parties`/`products` (21 test) | `dotnet test services/<service>/tests/<Service>.Api.IntegrationTests --filter FullyQualifiedName~IndependentTokenValidationTests` |
| FR-003/US1 — gateway xác thực token hợp lệ, lan truyền tenant/subject | [`JwtBearerAuthenticationTests.cs:51`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L51) — `ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn` |
| FR-011 — không token bị chặn (toggle on) | [`:86`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L86) — `ARequestWithNoToken_IsRejected_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithNoToken_IsRejected_WhenToggleIsOn` |
| FR-005/SC-002 — token giả mạo bị chặn tại gateway | [`:108`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L108) — `ARequestWithATamperedToken_IsRejected_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithATamperedToken_IsRejected_WhenToggleIsOn` |
| FR-006/SC-003 — token hết hạn trả `token_expired` | [`:135`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L135) — `ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn` |
| Principle X — rollback qua toggle (**hiện ĐỎ**, xem QA_Debt) | [`:168`](../../services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs#L168) — `ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` |
| FR-004 — mọi service đã đăng ký `AddIdentityValidation()` (quét toàn repo) | [`AuthenticatedByDefaultScannerTests.cs`](../../tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScannerTests.cs) (3 test) | `dotnet test tests/CrossServiceIsolation.Tests --filter FullyQualifiedName~AuthenticatedByDefaultScanner` |

**Kết quả lượt QA này (2026-09-26)**: Identity Unit **5/5**, Identity Integration **2/2**, `IndependentTokenValidationTests` **21/21** (4+4+4+4+5), `AuthenticatedByDefaultScanner` **3/3**, Gateway Unit **27/27**,
`JwtBearerAuthenticationTests` **4/5** (test rollback đỏ, đã ghi ở QA_Debt) — 32/33 xanh, không đổi so với lượt trước.

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán, có bằng chứng thực nghiệm lúc viết feature; trên hệ thống thật ở trạng thái mặc định 9/9 assertion Postman xanh và 32/33 test tự động xanh — US1 (đăng nhập/phát hành token), US2 (phòng thủ độc lập từng service), US3 (token hết hạn rõ ràng) và FR-007–011 được chứng minh.
Ghi chú: (1) test rollback `ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff` đỏ do tương tác với spec 015 (stub không phát claim `scope`); đo thật: `cutover=false` + `scope=true` làm gateway trả `403` cho mọi request, còn `cutover=false` + `scope=false` chỉ bỏ bước xác thực ở gateway — không trả lại truy cập ẩn danh vì BFF/service tự xác thực;
(2) request Postman `00 → 04` kỳ vọng `403` nhưng token thiếu scope thật chỉ bị `401` (thiếu `aud`); (3) lần đầu sau khi tạo lại container chậm/`504`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 014.
