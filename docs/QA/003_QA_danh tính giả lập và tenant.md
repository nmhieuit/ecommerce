# QA: Danh tính giả lập với tenant context đã xác định

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US2)

1. Gateway xác định tenant đúng 1 lần (danh tính giả lập), ghi đè `X-Tenant-Id` (không tin giá trị
   client tự gửi), BFF relay nguyên vẹn xuống service; `TenantId` hiện trong log scope ở từng chặng.
2. Truy cập persistence mà chưa có tenant thì thất bại ngay (`MissingTenantContextException`, trước khi
   mở kết nối SQL) — không có tenant mặc định.
3. Thay nguồn phân giải tenant (stub → JWT) chỉ chạm bước phân giải ở gateway.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/003-stub-identity-tenant-context/quickstart.md`](../../specs/003-stub-identity-tenant-context/quickstart.md),
nhưng quickstart gốc **không còn chạy đúng như viết** trên code hiện tại (xem [QA_Debt.md](QA_Debt.md)):
từ spec 014/015, gateway local đã cutover sang JWT thật và mọi domain service trả `401` trước khi tới
cổng tenant. Vì vậy phần thủ công dưới đây chỉ gồm những gì đã kiểm chứng được; phần còn lại kiểm bằng
test tự động (chạy được ngay khi Docker Desktop bật).

> Nếu `localhost` không gọi được dù container `healthy`: khởi động lại hẳn Docker Desktop (lỗi forwarding
> IPv6 loopback `::1` của WSL2), không phải lỗi ứng dụng.

### Thủ công — gửi/bỏ token và header tenant rồi bấm Postman

```bash
cp .env.example .env   # chỉ cần 1 lần
docker compose -f docker-compose.local.yml up -d --wait gateway-api products-api baskets-api orders-api parties-api
```

Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) + [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json), chọn **Ecommerce - Local**, chạy `00 - Xác thực & phân quyền (Get Token) → 01` để có token, rồi các folder bên dưới.
Từ spec 014/015, mọi service trả `401` trước khi tới cổng tenant nếu không có token, nên "công tắc" của spec này là **có/không token** và **có/không header `X-Tenant-Id`** (gọi thẳng service), thay cho việc đổi cấu hình.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Gọi thẳng service, không token, không tenant (quickstart Scenario 3 gốc kỳ vọng `500`) | Không token, không header | `00` → "02 Không có token thì bị chặn (401)"; hoặc `curl http://localhost:5088/products` | `500` (cổng tenant) theo quickstart gốc | **`401`** — deny-by-default (014/015) chặn trước cổng tenant |
| Có `X-Tenant-Id` nhưng không token *(ngoại lệ: Postman gắn token mặc định)* | `curl -H "X-Tenant-Id: contoso" http://localhost:5088/products` | (không có) | Header tenant không thay được token | `401` |
| Có token hợp lệ, KHÔNG có `X-Tenant-Id` (FR-003/004/005, US2) | Token hợp lệ, bỏ header | `Product` → "Thiếu header tenant thì không phục vụ catalog"; `Party` → "Thiếu header tenant thì thất bại rõ ràng"; `Basket` → tương tự; `Order` → "Ghi đơn khi không có tenant thì không tạo gì" | `500` (`MissingTenantContextException`), không tạo dữ liệu | Cả 4 `500` (8–35 ms), thân nêu `MissingTenantContextException … there is deliberately no default`; folder `Product` 6/6, `Party` 5/5, `Basket` 12/12, `Order` 14/14 xanh; `curl` có token + `X-Tenant-Id: contoso` → `200` |
| Tenant không đến từ client (FR-001/002) | Gửi `X-Tenant-Id: evil-tenant` qua gateway kèm token | `Order` → "Tenant khai trong body bị bỏ qua"; hoặc `POST gateway /bff/basket/items` + `/bff/checkout` với header giả *(ngoại lệ: `curl` + `sqlcmd`)* | Tenant lấy từ token, bỏ qua giá trị client | Đơn tạo qua gateway với header `evil-tenant` có `TenantId = contoso` trong `orders-db`; **0** đơn mang `evil-tenant`; `Order` "Tenant khai trong body bị bỏ qua" `201` xanh |
| Token thiếu scope → `403` *(đã biết đỏ, xem QA_Debt mục 014/015)* | Token xin bằng `scope=openid profile` | `00` → "03", "04" | `403` | `04` trả **`401`** (`unauthorized`), không phải `403 forbidden_scope`: token thiếu scope cũng thiếu `aud = ecommerce-api` nên bị từ chối sớm; folder `00` 4/6 assertion xanh |
| Đổi nguồn phân giải tenant stub ↔ JWT (SC-004) | `FEATURE_IDENTITY_SERVER_AUTH_CUTOVER=true/false` trong `.env` rồi tạo lại service | (xem folder `14`) | Chỉ chạm bước phân giải ở gateway | Đã đo ở QA 014 (ma trận 2 công tắc): `false` không trả lại truy cập ẩn danh vì BFF/service vẫn tự xác thực |
| Dọn dẹp | Xoá đơn thử (`DELETE FROM Orders WHERE PlacedAtUtc >= …`) | (không có) | Không dữ liệu dư | Đơn của lượt này sẽ được dọn cuối đợt làm lại 001–007 |

### Thủ công — SC-004 (đổi nguồn phân giải chỉ chạm 1 bước)

Review code: dưới `StubIdentityAuthenticationHandler` (nay được bọc bởi `AddToggleGatedIdentity`, có JwtBearer thay thế qua toggle `FeatureToggles:IdentityServerAuthCutover`), `TenantHeaderPropagationMiddleware`, `Tenancy/TenantContextMiddleware`, `TenantPropagationHandler` của BFF chỉ đọc claim/header `tenant_id`, không tham chiếu handler stub.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm test (comment mỗi hàm đã gắn `Task nguồn: spec 003 ...`, xem lại tại đó nếu cần biết test ứng với US/task nào). Test hậu tố `IntegrationTests` dùng Testcontainers → cần Docker Desktop đang chạy.

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/FR-001,002 — gateway phân giải 1 lần, ghi đè `X-Tenant-Id` client tự gửi, mọi route đều mang tenant tới BFF | [`TenantPropagationTests.cs:36`](../../services/gateway/tests/Gateway.Api.IntegrationTests/TenantPropagationTests.cs#L36) · [`:62`](../../services/gateway/tests/Gateway.Api.IntegrationTests/TenantPropagationTests.cs#L62) · [`:97`](../../services/gateway/tests/Gateway.Api.IntegrationTests/TenantPropagationTests.cs#L97) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~TenantPropagationTests"` |
| US1/FR-002 — BFF relay tenant xuống downstream; không bịa tenant khi chính nó không có | [`TenantPropagationTests.cs:35`](../../services/bff/tests/Bff.Api.IntegrationTests/TenantPropagationTests.cs#L35) · [`:66`](../../services/bff/tests/Bff.Api.IntegrationTests/TenantPropagationTests.cs#L66) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~TenantPropagationTests"` |
| FR-003/004/005, US2/SC-002 — Products: không tenant thì DbContext không dựng được, request lỗi `500` | [`TenantEnforcementTests.cs:34`](../../services/products/tests/Products.Api.IntegrationTests/TenantEnforcementTests.cs#L34) · [`:55`](../../services/products/tests/Products.Api.IntegrationTests/TenantEnforcementTests.cs#L55) | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter "FullyQualifiedName~TenantEnforcementTests"` |
| FR-003/004/005, US2/SC-002 — Baskets: như trên | [`TenantEnforcementTests.cs:35`](../../services/baskets/tests/Baskets.Api.IntegrationTests/TenantEnforcementTests.cs#L35) · [`:56`](../../services/baskets/tests/Baskets.Api.IntegrationTests/TenantEnforcementTests.cs#L56) | `dotnet test services/baskets/tests/Baskets.Api.IntegrationTests --filter "FullyQualifiedName~TenantEnforcementTests"` |
| FR-003/004/005, US2/SC-002 — Parties: như trên | [`TenantEnforcementTests.cs:35`](../../services/parties/tests/Parties.Api.IntegrationTests/TenantEnforcementTests.cs#L35) · [`:56`](../../services/parties/tests/Parties.Api.IntegrationTests/TenantEnforcementTests.cs#L56) | `dotnet test services/parties/tests/Parties.Api.IntegrationTests --filter "FullyQualifiedName~TenantEnforcementTests"` |
| FR-004/005, US2/SC-002 — Orders: cổng đã dời xuống call site (spec 024) nên DbContext dựng được, nhưng request và ghi không tenant vẫn thất bại và không tạo đơn | [`TenantEnforcementTests.cs:46`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L46) · [`:69`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L69) · [`:92`](../../services/orders/tests/Orders.Api.IntegrationTests/TenantEnforcementTests.cs#L92) | `dotnet test services/orders/tests/Orders.Api.IntegrationTests --filter "FullyQualifiedName~TenantEnforcementTests"` |
| FR-004/005/006 — `TenantContext`: chỉ có Resolved/Unresolved, tenant rỗng cũng là Unresolved, guard ném exception | [`TenantContextTests.cs:18`](../../shared/Tenancy.UnitTests/TenantContextTests.cs#L18) · [`:34`](../../shared/Tenancy.UnitTests/TenantContextTests.cs#L34) · [`:51`](../../shared/Tenancy.UnitTests/TenantContextTests.cs#L51) · [`:72`](../../shared/Tenancy.UnitTests/TenantContextTests.cs#L72) | `dotnet test shared/Tenancy.UnitTests --filter "FullyQualifiedName~TenantContextTests"` |
| FR-003/006 — middleware đọc `X-Tenant-Id`, mở logging scope `TenantId`, không tự chặn pipeline | [`TenantContextMiddlewareTests.cs:20`](../../shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs#L20) · [`:45`](../../shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs#L45) · [`:72`](../../shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs#L72) · [`:96`](../../shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs#L96) · [`:118`](../../shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs#L118) | `dotnet test shared/Tenancy.UnitTests --filter "FullyQualifiedName~TenantContextMiddlewareTests"` |
| FR-001/007 — danh tính giả lập ở gateway: cấp claim `tenant_id`/subject, thất bại khi chưa cấu hình tenant | [`StubIdentityAuthenticationHandlerTests.cs:30`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L30) · [`:47`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L47) · [`:67`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L67) · [`:86`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L86) · [`:116`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L116) · [`:135`](../../services/gateway/tests/Gateway.Api.UnitTests/StubIdentityAuthenticationHandlerTests.cs#L135) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~StubIdentityAuthenticationHandlerTests"` |
| **SC-003** — mọi `AddDbContext` của service sở hữu database đều gate theo tenant (**đang FAIL ở `orders`**, xem QA_Debt) | [`TenantGatedConnectionTests.cs:41`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L41) · [`:72`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L72) · [`:97`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L97) · [`:120`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L120) · [`:141`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L141) · [`:172`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L172) · [`:204`](../../tests/CrossServiceIsolation.Tests/TenantGatedConnectionTests.cs#L204) | `dotnet test tests/CrossServiceIsolation.Tests --filter "FullyQualifiedName~TenantGatedConnectionTests"` |

**Kết quả lượt QA này (2026-09-27)**: gateway `TenantPropagationTests` **4/4** · BFF `TenantPropagationTests` **2/2** · `TenantEnforcementTests` products **2/2**, baskets **2/2**, parties **2/2**, orders **3/3** · `Tenancy.UnitTests` `TenantContext*` **14/14** · `StubIdentityAuthenticationHandlerTests` **8/8** ·
`TenantGatedConnectionTests` **6/7 — ĐỎ** `EveryDbContextRegistration_IsGatedOnAResolvedTenant`: `orders` kỳ vọng 1 call site gated, thực tế 0 (không đổi, xem QA_Debt).

## Kết luận

**PASS kèm ghi chú** — 4 nguồn mô tả cùng 1 luồng happy-case (resolve 1 lần ở gateway → relay → gate trước persistence); đo sống bằng Postman: có token nhưng thiếu `X-Tenant-Id` → `500` không tạo dữ liệu ở cả 4 service, tenant giả của client bị bỏ qua (đơn vẫn mang `contoso`), các test hành vi xanh. Nhưng **SC-003 vẫn ĐỎ trên `master`**
(test tự động của chính spec này đỏ do spec 024 chuyển cổng tenant của Orders), quickstart gốc chỉ tái hiện được khi có token (không token → `401`), và request Postman `00 → 04` (`403`) đang trả `401`. Xem [QA_Debt.md](QA_Debt.md).
