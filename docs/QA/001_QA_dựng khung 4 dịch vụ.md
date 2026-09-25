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

### Thủ công — US1 (SC-001, SC-002)

Lặp lại cho **từng** service (`parties-api` cổng `5204`, `products-api` cổng `5088`,
`baskets-api` cổng `5188`, `orders-api` cổng `5041`):

```bash
cp .env.example .env   # chỉ cần 1 lần cho cả repo
docker compose -f docker-compose.local.yml up -d --wait <service>-api
```

**Lưu ý**: lệnh trên tự kéo theo `<service>-db`, `<service>-migrate`, và cả `identity-db`/
`identity-migrate`/`identity-api` (mọi domain service đều cần xác thực JWT thật từ spec 014, ra đời
sau 001) — đây là phụ thuộc hạ tầng xác thực, **không phải** phụ thuộc vào 1 trong 3 service nghiệp vụ
còn lại, nên vẫn đúng tinh thần "độc lập" mà SC-002 yêu cầu. `--wait` tự chặn cho tới khi container
khai báo `healthy`.

| Bước | Postman request (folder trùng tên service, vd. `Party`) | Kỳ vọng |
|---|---|---|
| Liveness | "Health live — tiến trình còn sống" → **Send** | `200 OK` — tab Tests đã có sẵn assertion, chạy Send là thấy PASS/FAIL ngay |
| Readiness (DB khoẻ) | "Health ready — kết nối tới database riêng" → **Send** | `200 OK`; assertion sẵn chấp nhận cả 200 lẫn 503 nên PASS ở cả 2 bước dưới đây |
| Readiness (DB chết) | `docker compose -f docker-compose.local.yml stop <service>-db`, Send lại "Health ready" | `503`; script test tự đọc body, khẳng định check `self-database` = `Unhealthy` — **nhưng** Send lại "Health live" vẫn `200` |
| Khôi phục | `docker compose -f docker-compose.local.yml start <service>-db` | Send lại "Health ready" → `200` sau vài giây, không cần restart service |
| Thời gian (SC-001) | Bấm giờ từ lúc chạy lệnh `docker compose up` tới khi "Health ready" trả `200` lần đầu | Dưới 5 phút |
| Dọn dẹp | `docker compose -f docker-compose.local.yml down -v` | Xoá sạch container + volume, sẵn sàng cho lượt test tiếp theo/service tiếp theo |

Bước "readiness DB chết" là bước quan trọng nhất — đây chính là bằng chứng phân biệt "tiến trình sống"
với "thật sự phục vụ được", đúng yêu cầu cốt lõi của US1. Tên service trong lệnh `stop`/`start` là tên
khai trong `docker-compose.local.yml` (vd. `parties-db`), không phải tên container đầy đủ.

Kịch bản y hệt (dùng chung 1 stack đầy đủ khởi động bằng `./scripts/local-up.ps1` thay vì từng service
riêng) đã được đo thật với số liệu cụ thể — 503 xuất hiện sau 4.10s, 3 service anh em vẫn `200` xuyên
suốt — tại [`docs/local-testing.md`](../local-testing.md), Kịch bản 1. Dùng bản đó nếu muốn kiểm thử cả
4 service cùng lúc thay vì từng service độc lập như bảng trên.

### Thủ công — US2 / US3 (SC-003, SC-004)

- **SC-003**: đọc connection string của 1 service (`appsettings*.json` + biến môi trường thật lúc
  chạy) — xác nhận chỉ trỏ đúng database của chính nó, không có credential/route nào tới database của
  service khác.
- **SC-004**: mở `services/<service>/src/<Service>.Api/Features/` — xác nhận handler, đăng ký route,
  và mọi code liên quan tới health-check nằm chung 1 thư mục, không tách theo kiểu
  `Controllers/`/`Services/`/`Repositories/`.

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

## Kết luận

**PASS** — cả 4 nguồn mô tả cùng 1 luồng happy-case, không mâu thuẫn nhau về nội dung nghiệp vụ.

Có phát hiện đáng chú ý (1 chỗ tài liệu mô tả sai cơ chế kỹ thuật so với code hiện tại — không ảnh
hưởng kết quả happy-case) và vài điểm nên bổ sung, không bắt buộc sửa ngay — xem
[QA_Debt.md](QA_Debt.md).
