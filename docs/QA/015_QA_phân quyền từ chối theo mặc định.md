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

**Kết quả lượt QA này (2026-09-23)**: 10 `AuthorizationPolicyTests` (5 service) **10/10 PASS**; `AuthorizationPolicyDeclaredScannerTests` **2/4** (2 test liên quan
consumer đỏ); toàn suite `Bff.Api.IntegrationTests` 49/50 (1 đỏ không thuộc 015 — xem QA_Debt).

### Thủ công — đã tự làm sống

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát** |
|---|---|---|---|
| Scenario 3 — thợ xây quên dán biển | Thêm tạm route `/qa-probe-015-temp-route` vào `CatalogEndpoints.cs` không kèm `.RequireAuthorization()`/`.AllowAnonymous()`, chạy `EveryMappedRoute_DeclaresAnAuthorizationDecision` | Đỏ, nêu đích danh route/file | Đỏ đúng; `git checkout --` → xanh, `git status` sạch |
| Scenario 5 — rollback qua toggle | Đọc `appsettings.json` (`false`) và `appsettings.Development.json` (`true`) của `bff`/`baskets` | Khớp mô tả | Khớp; cơ chế `IOptionsMonitor` giống `IdentityServerAuthCutover` (đã xác nhận runtime ở QA 014) |

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán, cấu hình tĩnh (`appsettings*.json`, `service-manifest.yaml`) khớp mô tả, không hồi quy. 12/14 test liên quan
xanh — US1 (mọi route có khai báo), `403 forbidden_scope` phân biệt rõ với `401`/`200`, và cơ chế chặn build (đã tái hiện sống) đúng như tài liệu. Ghi chú:
(1) 2/4 test scanner ở phía `IConsumer<T>` đang đỏ vì consumer đầu tiên trong repo (test helper của spec 024) chưa mang nhãn `Trusted source:` — đúng kịch bản
contract tự dự đoán, còn câu hỏi phạm vi (test helper có thuộc yêu cầu hay không). Chi tiết: [QA_Debt.md](QA_Debt.md) mục 015.
