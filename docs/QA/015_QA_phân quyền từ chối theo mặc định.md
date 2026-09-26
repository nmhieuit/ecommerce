# QA: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec này đóng nốt nửa "phân quyền" của Principle VI mà 014 để ngỏ, xây trên nền `shared/Identity`. Chính sách `ApiScope` khai báo ở đây
là nguyên nhân gốc của các phát hiện ở 011 và 014 — QA lần này rà lại cơ chế gốc đó.

## Luồng happy-case đã rà soát

1. Mọi route trong 9 file `*Endpoints.cs` (BFF + 4 domain service) tự khai `.RequireAuthorization(AuthorizationPolicies.ApiScope)`
   hoặc `.AllowAnonymous()` (2 health probe) — không route nào để trống.
2. Token đã xác thực nhưng thiếu claim `scope=ecommerce-api` bị `403 forbidden_scope` (không phải `401`/`200`) khi toggle
   `AuthorizationRequireApiScope` bật.
3. `AuthorizationPolicyDeclaredScanner` (test tĩnh) quét mọi route + mọi `IConsumer<T>` — chặn build nếu thiếu khai báo (FR-004).
4. `FallbackPolicy` nghiêm ngặt ≥ policy `ApiScope` — route quên khai báo không bao giờ được bảo vệ kém hơn route có khai báo.
5. Quy tắc nghiệp vụ SPA đã kiểm phía client (giỏ rỗng khi checkout, số lượng < 1) vẫn bị server tự chặn độc lập khi gọi thẳng API.
6. Toggle tắt (mặc định Production) đưa chính sách về "chỉ cần đã xác thực" như trước 015 — rollback không cần redeploy.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — đổi công tắc `.env` rồi bấm Postman

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api parties-api` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token`, rồi folder
**`15 - Phân quyền từ chối theo mặc định`** (bước 01 → 11).

**Công tắc** ([`.env.example`](../../.env.example), mặc định cả hai `true`): thêm 2 dòng vào `.env`, tạo lại 6 service, gọi thử vài lần (lần đầu sau khi tạo lại thường chậm/`504`):

```
FEATURE_IDENTITY_SERVER_AUTH_CUTOVER=false      # chỉ gateway (014)
FEATURE_AUTHORIZATION_REQUIRE_API_SCOPE=true    # gateway, bff, products, baskets, orders, parties (015)
```

```bash
docker compose -f docker-compose.local.yml up -d --force-recreate gateway-api bff-api products-api baskets-api orders-api parties-api
```

| Bước | Cấu hình cần chỉnh (cutover / scope) | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| US1 — health probe không cần token; route nghiệp vụ không token bị chặn | Mặc định (true / true) | `15` bước 01 → 10 (5 service gọi thẳng) | Health `200`; route nghiệp vụ `401 unauthorized` | Đúng cả 5 service; toàn folder **13/13 xanh** |
| FR-003 — token hợp lệ đi qua gateway | Mặc định | `15` bước 11 | `200` | `200` |
| **Bật** `ApiScope` với stub (thấy `403 forbidden_scope`) | `false` / `true` | `15` bước 11 (+ thử không token) | `403 forbidden_scope` | `403 {"error":"forbidden_scope","message":"Authentication succeeded, but the token does not carry the required scope."}` cho token hợp lệ **và cả không token** (stub không phát claim scope) — đúng cảnh báo trong `.env.example` |
| **Tắt** `ApiScope` — rollback về "chỉ cần xác thực" | `false` / `false` | `15` bước 11 | `200` với token | Token hợp lệ `200`, không token `401` (BFF/service tự xác thực); lần đo đầu sau khi tạo lại có `504` nguội |
| Chỉ tắt `ApiScope` | `true` / `false` | `15` bước 11 | `200` | `200`; không token `401` |
| Token thiếu scope thật bị `403` | Mọi trạng thái | `00` bước 03 → 04 | `403 forbidden_scope` | **`401`**, không phải `403`: token `scope=openid profile` không có `aud = ecommerce-api` nên bị từ chối ở bước xác thực (xem QA_Debt mục 014, 015) |
| Scenario 3 — thợ xây quên dán biển *(ngoại lệ: sửa mã)* | Thêm route `/qa-probe-015-temp-route` vào `CatalogEndpoints.cs` không kèm `.RequireAuthorization()`/`.AllowAnonymous()`, dựng lại `products-api`; xong `git checkout --` | (không có) — chạy `EveryMappedRoute_DeclaresAnAuthorizationDecision`, gọi route thử | Scanner đỏ nêu route/file; runtime vẫn được bảo vệ | Đỏ đúng (nêu `products … .MapGet("/qa-probe-015-temp-route", …) … declares neither RequireAuthorization…`); runtime: không token `401`, có token `200`; khôi phục → xanh, `git status` sạch |
| Dọn dẹp | Xoá 2 dòng trong `.env`, tạo lại 6 service; xoá image thử | (không có) | Về mặc định | Đã khôi phục `.env`, 6 service và `products-api` từ mã sạch |

### Tự động — chạy thẳng bộ test đã có

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/US1 — mọi route đã khai báo quyết định phân quyền | [`AuthorizationPolicyDeclaredScannerTests.cs:19`](../../tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs#L19) — `EveryMappedRoute_DeclaresAnAuthorizationDecision` | `dotnet test tests/CrossServiceIsolation.Tests --filter EveryMappedRoute_DeclaresAnAuthorizationDecision` |
| Scanner tự bảo vệ — quét đủ mọi service | [`:42`](../../tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs#L42) — `ScanEndpoints_ActuallyExaminesEveryAuthorizingService` | `dotnet test tests/CrossServiceIsolation.Tests --filter ScanEndpoints_ActuallyExaminesEveryAuthorizingService` |
| FR-002 — mọi `IConsumer<T>` khai báo nguồn tin cậy (**hiện ĐỎ**, xem QA_Debt) | [`:72`](../../tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs#L72) — `EveryMessageConsumer_DeclaresATrustedSource` | `dotnet test tests/CrossServiceIsolation.Tests --filter EveryMessageConsumer_DeclaresATrustedSource` |
| Scanner tự bảo vệ — quét đủ mọi service (**hiện ĐỎ**, xem QA_Debt) | [`:97`](../../tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs#L97) — `ScanConsumers_ActuallyExaminesEveryService` | `dotnet test tests/CrossServiceIsolation.Tests --filter ScanConsumers_ActuallyExaminesEveryService` |
| FR-003/US1 Test Scenario 2 — thiếu scope bị `403` (baskets) | [`AuthorizationPolicyTests.cs:24`](../../services/baskets/tests/Baskets.Api.IntegrationTests/AuthorizationPolicyTests.cs#L24) · [`:47`](../../services/baskets/tests/Baskets.Api.IntegrationTests/AuthorizationPolicyTests.cs#L47) (regression guard) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationPolicyTests` |
| FR-003 — thiếu scope bị `403` (BFF) | [`AuthorizationPolicyTests.cs:23`](../../services/bff/tests/Bff.Api.IntegrationTests/AuthorizationPolicyTests.cs#L23) · [`:46`](../../services/bff/tests/Bff.Api.IntegrationTests/AuthorizationPolicyTests.cs#L46) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationPolicyTests` |
| FR-003 — thiếu scope bị `403` (orders) | [`AuthorizationPolicyTests.cs:22`](../../services/orders/tests/Orders.Api.IntegrationTests/AuthorizationPolicyTests.cs#L22) · [`:45`](../../services/orders/tests/Orders.Api.IntegrationTests/AuthorizationPolicyTests.cs#L45) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationPolicyTests` |
| FR-003 — thiếu scope bị `403` (parties) | [`AuthorizationPolicyTests.cs:22`](../../services/parties/tests/Parties.Api.IntegrationTests/AuthorizationPolicyTests.cs#L22) · [`:45`](../../services/parties/tests/Parties.Api.IntegrationTests/AuthorizationPolicyTests.cs#L45) | `dotnet test services/parties/tests/Parties.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationPolicyTests` |
| FR-003 — thiếu scope bị `403` (products) | [`AuthorizationPolicyTests.cs:20`](../../services/products/tests/Products.Api.IntegrationTests/AuthorizationPolicyTests.cs#L20) · [`:43`](../../services/products/tests/Products.Api.IntegrationTests/AuthorizationPolicyTests.cs#L43) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter FullyQualifiedName~AuthorizationPolicyTests` |
| US3/FR-006/FR-007 — server tự chặn dữ liệu SPA đã kiểm ở client | đã rà soát ở QA 004/006 — `CheckoutTests.Checkout_ReturnsConflict_WhenTheBasketIsEmpty`, `CurrentBasketTests.AddItem_Rejects_AQuantityBelowOne` | (xem [004_QA](004_QA_SPA%20mua%20sắm%20tối%20thiểu.md)/[006_QA](006_QA_demo%20đặt%20hàng%20end-to-end.md)) |

**Kết quả lượt QA này (2026-09-26)**: 10 `AuthorizationPolicyTests` (5 service, mỗi service 2 test) **10/10 PASS**; `AuthorizationPolicyDeclaredScannerTests` **2/4** (2 test liên quan consumer đỏ, không đổi so với lượt trước).

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán; trên hệ thống thật ở trạng thái mặc định 13/13 assertion Postman xanh (health probe ẩn danh, route nghiệp vụ `401` không token, route quên khai báo vẫn bị chặn) và 12/14 test liên quan xanh. Toggle hoạt động đúng như tài liệu:
bật `ApiScope` với stub → `403 forbidden_scope`, tắt thì `200`. Ghi chú: (1) 2/4 test scanner phía `IConsumer<T>` đỏ vì consumer test helper của spec 024 chưa mang nhãn `Trusted source:` (câu hỏi phạm vi: test helper có thuộc yêu cầu không);
(2) `403 forbidden_scope` không đạt được bằng token thật từ `identity` (thiếu scope → thiếu `aud` → `401`), nên request Postman `00 → 04` đỏ và nhánh 403 chỉ lộ ra với stub/token dựng tay; (3) lần đầu sau khi tạo lại container chậm/`504`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 015.
