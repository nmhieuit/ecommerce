# QA: Dựng khung 4 service Parties/Products/Baskets/Orders

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. Khởi động 1 service độc lập (không cần 3 service kia) — readiness phản ánh đúng khả năng kết nối
   database thật, không chỉ tiến trình còn sống.
2. Không service nào có đường nào chạm được dữ liệu của service khác.
3. Mã nguồn mỗi service tổ chức theo tính năng (vertical-slice), không theo lớp kỹ thuật.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/001-scaffold-service-shells/quickstart.md`](../../specs/001-scaffold-service-shells/quickstart.md)
(quickstart gốc dùng `dotnet run` cục bộ) nhưng đổi toàn bộ sang chạy qua **Docker Desktop**
(`docker-compose.local.yml` — file tự chứa, publish cổng riêng từng service, đúng cổng quickstart gốc
đã ghi) thay vì chạy tiến trình cục bộ — không cần cài .NET SDK trên máy QA. Gọi request bằng **Postman
collection có sẵn của repo** thay vì `curl` tay — đã có sẵn request đúng tên, đúng assertion cho từng
bước bên dưới, không cần soạn lại.

> Nếu `localhost` không gọi được dù container đang `healthy` (request treo rồi timeout) trên Docker
> Desktop for Windows: khởi động lại hẳn Docker Desktop (không chỉ container) — đây là lỗi forwarding
> IPv6 loopback (`::1`) khá hay gặp của WSL2 backend, không phải lỗi ứng dụng.

**Chuẩn bị Postman (1 lần)**:

1. Import collection [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json).
2. Import environment [`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json)
   và chọn nó làm environment đang active — đã khai sẵn `partiesUrl=http://localhost:5204`,
   `productsUrl=http://localhost:5088`, `basketsUrl=http://localhost:5188`, `ordersUrl=http://localhost:5041`,
   đúng cổng `docker-compose.local.yml` publish.
3. **Không cần** chạy folder "00 - Xác thực & phân quyền" trước — 2 request Health live/Health ready
   trong mỗi folder `Party`/`Product`/`Basket`/`Order` không cần token (health check là endpoint hạ
   tầng, tự nêu rõ trong mô tả từng request).

### Thủ công — tắt/bật database rồi bấm Postman (US1, SC-001, SC-002)

Lặp lại cho **từng** service (folder Postman `Party` / `Product` / `Basket` / `Order`; cổng `5204` / `5088` / `5188` / `5041`), hoặc chạy gọn cả 8 request bằng folder đánh số **`01 - Dựng khung 4 dịch vụ`** (bản sao đúng 2 request Health live/ready của mỗi service, không cần token). Dựng riêng từng service:

```bash
cp .env.example .env   # chỉ cần 1 lần cho cả repo
docker compose -f docker-compose.local.yml up -d --wait <service>-api
```

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop <service>-db` / `start <service>-db`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Liveness + readiness khi DB khoẻ | Mặc định (DB và service đều bật) | "Health live — tiến trình còn sống", "Health ready — kết nối tới database riêng" | Cả hai `200` | `parties`, `products`, `baskets`, `orders`: live `200` (49–80 ms), ready `200` (7–20 ms) |
| Readiness khi DB chết, liveness vẫn sống (US1 — bước quan trọng nhất) | `stop <service>-db`, chờ ~12 giây | như trên | Ready `503` (check `self-database` = `Unhealthy`), live vẫn `200` | Ready **`503`** cả 4 service (parties 8022 ms, products 7290 ms, baskets 6 ms, orders 8018 ms); live `200` (49–58 ms); `RestartCount` 0 → 0 cả 4; các request cần DB trong folder đỏ đúng như kỳ vọng (parties 2/6, products 3/7, baskets 7/13, orders 9/15 assertion) |
| Khôi phục | `start <service>-db` | "Health ready" | `200` sau vài giây, không restart | `200` sau **22 s / 41 s / 31 s / 25 s** (parties / products / baskets / orders — thời gian SQL Server khởi động); folder xanh lại hoàn toàn (5/5, 6/6, 12/12, 14/14) |
| SC-001 — dựng 1 service dưới 5 phút *(ngoại lệ: không dùng `down -v` để khỏi xoá cả stack)* | `docker compose -f docker-compose.local.yml rm -sf parties-api parties-db parties-migrate` rồi `up -d --wait parties-api` (bấm giờ) | "Health ready" | Dưới 5 phút | `parties-api` healthy sau **33 giây** (image đã dựng sẵn, volume DB còn); ready `200` |
| SC-002 — độc lập | Chỉ dựng `parties-api`; `products`/`baskets`/`orders` còn chạy sẵn nhưng không được gọi | "Health ready" của `Party` | `200` không cần 3 service kia | Đúng — chỉ phụ thuộc `parties-db`, `parties-migrate` và cụm `identity-*` (xác thực JWT từ spec 014) |
| Dọn dẹp | `start` mọi DB đã tắt | (không có) | Stack về mặc định | Đã bật lại 4 DB; `parties-*` dựng lại đúng cấu hình compose |

Bước "readiness DB chết" là bước quan trọng nhất — đây chính là bằng chứng phân biệt "tiến trình sống" với "thật sự phục vụ được". Kịch bản y hệt cho cả 4 service cùng lúc đã được đo với số liệu cụ thể
tại [`docs/local-testing.md`](../local-testing.md), Kịch bản 1.

> Nếu `localhost` không gọi được dù container đang `healthy` (request treo rồi timeout) trên Docker Desktop for Windows: khởi động lại hẳn Docker Desktop — lỗi forwarding IPv6 loopback (`::1`) của WSL2, không phải lỗi ứng dụng.

### Thủ công — US2 / US3 (SC-003, SC-004)

| Bước | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|
| SC-003 — mỗi service chỉ trỏ đúng DB của mình | `docker exec ecomerce-local-<service>-api-1 printenv \| grep ^ConnectionStrings__` (che `User Id`/`Password`) | Chỉ có `Server=<service>-db;Database=<service>` của chính nó | `parties`→`parties-db/parties`, `products`→`products-db/products`, `baskets`→`baskets-db/baskets`, `orders`→`orders-db/orders` (+ `RabbitMq`, không phải DB); `bff` và `gateway`: **0** chuỗi kết nối |
| SC-004 — tổ chức theo tính năng | `ls services/<service>/src/<Service>.Api/` và `…/Features/` | Handler, route, health-check chung 1 thư mục tính năng; không có `Controllers/`/`Services/`/`Repositories/` | Mỗi service có `Features/` với `HealthCheck` + tính năng riêng (`Parties`; `Catalog`; `Baskets`, `Checkout`; `Orders`, `Chaos`); cấp trên chỉ có `Data`, `Features`, `Migrations`, `Properties` |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi dòng bấm thẳng vào tên file để mở đúng hàm test (đã gắn `Task nguồn: spec 001 ...` ngay trong
comment của từng hàm, xem lại tại đó nếu cần biết test ứng với US/task nào).

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| US1 — liveness luôn `200`, không chạm database (mock qua `WebApplicationFactory`) | [`HealthCheckTests.cs:31`](../../services/products/tests/Products.Api.UnitTests/HealthCheckTests.cs#L31) — `HealthLive_ReturnsOk` | `ConnectionStrings__ProductsDb="Server=x;Database=y;User Id=sa;Password=p;TrustServerCertificate=true" dotnet test services/products/tests/Products.Api.UnitTests --filter HealthLive_ReturnsOk` (bash; **bắt buộc** có biến môi trường chứa `Password=`, xem [QA_Debt.md](QA_Debt.md)) |
| US1 — readiness phản ánh đúng khả năng kết nối tới SQL Server thật (Testcontainers) | [`ReadinessTests.cs:30`](../../services/products/tests/Products.Api.IntegrationTests/ReadinessTests.cs#L30) — `HealthReady_ReturnsOk_WhenDatabaseReachable`<br>[`ReadinessTests.cs:50`](../../services/products/tests/Products.Api.IntegrationTests/ReadinessTests.cs#L50) — `HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable` | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter "FullyQualifiedName~HealthReady_ReturnsOk_WhenDatabaseReachable\|FullyQualifiedName~HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable"` |
| US2 — readiness KHÔNG fallback sang database của service khác khi database riêng chết | [`ReadinessTests.cs:78`](../../services/products/tests/Products.Api.IntegrationTests/ReadinessTests.cs#L78) — `HealthReady_DoesNotFallBackToAnotherServicesDatabase_WhenOwnDatabaseUnreachable` | `dotnet test services/products/tests/Products.Api.IntegrationTests --filter HealthReady_DoesNotFallBackToAnotherServicesDatabase_WhenOwnDatabaseUnreachable` |
| US2 — không service nào (toàn repo) có connection string trỏ sang database của service khác | [`ConnectionStringIsolationTests.cs:42`](../../tests/CrossServiceIsolation.Tests/ConnectionStringIsolationTests.cs#L42) — `NoServiceConfiguration_NamesAnotherServicesDatabase` | `dotnet test tests/CrossServiceIsolation.Tests --filter NoServiceConfiguration_NamesAnotherServicesDatabase` |
| US2 — service không sở hữu database (bff/gateway) không được khai connection string nào | [`ConnectionStringIsolationTests.cs:89`](../../tests/CrossServiceIsolation.Tests/ConnectionStringIsolationTests.cs#L89) — `NoStatelessService_DeclaresAConnectionString` | `dotnet test tests/CrossServiceIsolation.Tests --filter NoStatelessService_DeclaresAConnectionString` |
| US3 — không service nào (toàn repo) có thư mục lớp kỹ thuật ở cấp cao nhất | [`VerticalSliceStructureTests.cs:28`](../../tests/StructureConventionTests/VerticalSliceStructureTests.cs#L28) — `NoService_HasATopLevelTechnicalLayerFolder` | `dotnet test tests/StructureConventionTests --filter NoService_HasATopLevelTechnicalLayerFolder` |
| US3 — mỗi service tổ chức ít nhất 1 capability dưới `Features/` | [`VerticalSliceStructureTests.cs:66`](../../tests/StructureConventionTests/VerticalSliceStructureTests.cs#L66) — `EveryService_OrganisesAtLeastOneCapabilityUnderFeatures` | `dotnet test tests/StructureConventionTests --filter EveryService_OrganisesAtLeastOneCapabilityUnderFeatures` |

2 dòng US1 dùng `Products.Api.*Tests` làm ví dụ — đổi `products` thành `parties`/`baskets`/`orders`
trong đường dẫn project để lặp lại cho từng service còn lại (mỗi service có `HealthCheckTests.cs` +
`ReadinessTests.cs` riêng, cùng tên hàm). `CrossServiceIsolation.Tests` và `StructureConventionTests`
quét **toàn repo 1 lần** (dựa trên scanner
[`ConnectionStringScanner.cs`](../../tests/CrossServiceIsolation.Tests/ConnectionStringScanner.cs) /
[`VerticalSliceStructureScanner.cs`](../../tests/StructureConventionTests/VerticalSliceStructureScanner.cs)),
không cần chạy riêng theo từng service — 2 hàm còn lại của mỗi file (`Scan_ActuallyExamines...`,
`Scan_Flags...`, `Scan_Allows...`) là test tự bảo vệ cho scanner, không trực tiếp map vào 1 US/SC nào
nhưng vẫn nên chạy cùng cả class.

**Kết quả lượt QA này (2026-09-27)**: `HealthLive_ReturnsOk` (products) **1/1** khi có biến môi trường chứa `Password=`, **đỏ** `OptionsValidationException: Missing required secret(s): ConnectionStrings:ProductsDb` khi không có; `Products.Api.IntegrationTests` `ReadinessTests` **3/3** (Testcontainers);
`CrossServiceIsolation.Tests` `ConnectionStringIsolationTests` **7/7** (toàn project 18/21 — 3 đỏ đã biết ở spec 003/015, xem QA_Debt); `StructureConventionTests` **9/9**.

## Kết luận

**PASS** — cả 4 nguồn mô tả cùng 1 luồng happy-case; cả 4 service đo sống bằng Postman + công tắc `stop`/`start` database: readiness `503` khi DB chết, liveness vẫn `200`, không restart, tự phục hồi sau 22–41 giây; SC-001 (33 giây), SC-003, SC-004 đều đạt; các test của spec xanh.
Có phát hiện đáng chú ý (tài liệu kiến trúc mô tả sai cơ chế kiểm DB, test đơn vị cần biến môi trường chưa nêu, Postman folder `Product` từng lỗi thời sau spec 023 — đã sửa) — xem [QA_Debt.md](QA_Debt.md).
