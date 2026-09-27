# QA: Định tuyến Gateway → BFF cho Products/Baskets/Orders/Parties

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

## Luồng happy-case đã rà soát (US1 → US3)

1. Gateway chỉ định tuyến tới BFF (một cụm duy nhất); BFF gọi 4 service downstream qua `HttpClient`
   có resilience handler, tổng hợp và định hình lại response — không tự gọi thẳng tới 4 service từ
   gateway.
2. Client chỉ cần biết một địa chỉ duy nhất (gateway); path không khớp route nào bị từ chối rõ ràng
   (404), không lộ tên cluster/route/service nội bộ.
3. BFF không chứa nghiệp vụ ngoài tổng hợp/định hình response (không rule miền, không lưu trữ).
4. Khi 1 service downstream không sẵn sàng, bên gọi nhận lỗi có cấu trúc (`ProblemDetails`, kèm
   `correlationId`) trong dưới 5 giây — không treo, không lộ stack trace thô.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Dựa trên [`specs/002-gateway-bff-routing/quickstart.md`](../../specs/002-gateway-bff-routing/quickstart.md)
(quickstart gốc dùng `dotnet run` cục bộ) nhưng đổi toàn bộ sang chạy qua **Docker Desktop**
(`docker-compose.local.yml`) thay vì chạy tiến trình cục bộ — không cần cài .NET SDK trên máy QA. Gọi
request bằng **Postman collection có sẵn của repo** (folder `Gateway` + phần liên quan trong folder
`Common`) thay vì `curl` tay.

> **Cổng đúng theo `docker-compose.local.yml`/Postman environment (không phải cổng ghi trong
> `specs/002-gateway-bff-routing/quickstart.md`, xem [QA_Debt.md](QA_Debt.md))**: gateway `5300`, BFF
> `5301`, products `5088`, baskets `5188`, orders `5041`, parties `5204`.

> Nếu `localhost` không gọi được dù container đang `healthy` (request treo rồi timeout) trên Docker
> Desktop for Windows: khởi động lại hẳn Docker Desktop — lỗi forwarding IPv6 loopback (`::1`) của
> WSL2 backend, không phải lỗi ứng dụng.

**Chuẩn bị Postman**: dùng lại collection + environment đã import cho spec 001 (xem
[001_QA](001_QA_dựng%20khung%204%20dịch%20vụ.md)); không cần chạy folder "00 - Xác thực & phân quyền"
cho các bước dưới đây — mọi request health/route trong `Gateway`/`Common` dùng ở bảng này không cần
token.

### Thủ công — tắt/bật service phía sau rồi bấm Postman (US1, US2, US3)

```bash
cp .env.example .env   # chỉ cần 1 lần cho cả repo
docker compose -f docker-compose.local.yml up -d --wait gateway-api
```

Lệnh trên tự kéo theo `bff-api`, cả 4 domain service, `identity-api` và DB riêng từng service — `--wait` chặn tới khi mọi container `healthy`.
Postman: chạy `00 - Xác thực & phân quyền (Get Token) → 01 Lấy access token` trước (từ spec 014 mọi route qua gateway cần token), rồi folder `Gateway` và `Common`.

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop products-api` / `start products-api`; `stop bff-api` / `start bff-api`.

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-27)** |
|---|---|---|---|---|
| Gateway sống, OpenAPI đi qua gateway (US1) | Mặc định | Folder `Gateway` → "Health live của chính gateway", "Tài liệu OpenAPI đi qua được gateway" | `200`; tài liệu là của BFF (có `/bff/products`) | `200` (160 ms); `200` (414 ms; lần đầu sau khi tạo lại container 4.8 s — nguội) |
| Gateway chuyển tiếp xuống BFF (SC-002) | Mặc định, kèm token | `Gateway` → "Route phía client được chuyển tiếp xuống BFF"; `Common` → "BFF: danh sách sản phẩm, gọi thẳng có header" | Không phải `404`; body có `items` (phong bì, không phải mảng trần) | `200` (138 ms), body `{ items, page, pageSize, totalCount }`. Lần chạy đầu ngay sau khi tạo lại container: `504` sau 3.3 s (nguội) — vẫn không phải `404` |
| Đường dẫn không khớp route (FR-007) | Mặc định | `Gateway` → "Đường dẫn không tồn tại" | `404`, không lộ `bff-cluster`/`products-api`/cổng, không treo | `404` (29–88 ms), assertion không lộ xanh |
| Toàn bộ folder `Gateway` + `Common` | Mặc định | Cả 2 folder | Xanh | `Gateway` 11/11 assertion, `Common` 18/18 (kể cả `BFF: thiếu header thì downstream từ chối` → `502`) |
| US3/SC-003 — downstream (products) chết | `stop products-api` | `Common` → "BFF: danh sách sản phẩm…" hoặc `GET gateway /bff/products` | `502`/`504` có cấu trúc, dưới 5 giây, không lộ topology | `504` `ProblemDetails` (`downstream-timeout`, kèm `correlationId`, `traceId`) sau **3.0 giây** ×3; gateway health live `200` |
| Khôi phục | `start products-api` | như trên | `200` sau vài giây | Lần đầu `504` (3.0 s), lần sau `200` (230 ms → 24–60 ms) |
| FR-006 — BFF chết, gateway phải trả lỗi có cấu trúc trong < 5 giây | `stop bff-api` | `GET gateway /bff/products` | `502`/`503`/`504` có cấu trúc, dưới 5 giây; gateway vẫn sống | **`502` thân RỖNG** (`Content-Length: 0`, chỉ có `X-Correlation-Id`) sau **8.0 giây** ×3 — vượt 5 giây (SC-003) và không có `ProblemDetails`; gateway health live `200`. Bật lại `bff-api` → `200` (~35 s sau, hết thời gian chờ health-check thụ động) — xem QA_Debt |
| US2 — cổng công bố ra host *(ngoại lệ: review cấu hình, không có công tắc)* | Mở [`docker-compose.yml`](../../docker-compose.yml) (file mặc định) | (không có) | Chỉ `gateway-api` (và `storefront`) publish cổng | Có **3** dòng `ports:` — `gateway-api` `5300`, `storefront` `4173` **và `identity-api` `5205`** (thêm từ spec 004 để trình duyệt đăng nhập); 4 domain service + `bff-api` không publish — xem QA_Debt |
| US1/SC-004 — BFF không chứa nghiệp vụ *(ngoại lệ: đọc mã)* | `ls services/bff/src/Bff.Api/Features/` | (không có) | Mỗi handler gọi 1 typed client rồi map | `Baskets`, `Checkout`, `HealthCheck`, `Orders`, `Parties`, `Products` — đúng 5 tính năng + health |
| Dọn dẹp | `start` mọi service đã tắt; giỏ dọn bằng `Common` → "Dọn giỏ sau khi thử xong" | (không có) | Stack về mặc định | Đã bật lại; giỏ về rỗng (`204`) |

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm test (comment mỗi hàm đã gắn `Task nguồn: spec 002 ...`).

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/SC-002 — BFF proxy đúng route sản phẩm, trả shape đã định hình (không phải pass-through thô), kể cả catalog rỗng | [`ProductsRouteTests.cs:28`](../../services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs#L28)<br>[`ProductsRouteTests.cs:78`](../../services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs#L78) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~ProductsRouteTests"` |
| US1/FR-005/SC-004 — BFF chỉ shaping field-by-field (Products/Baskets/Orders/Parties), không rule nghiệp vụ, không làm tròn tiền | [`ResponseMappingTests.cs:30`](../../services/bff/tests/Bff.Api.UnitTests/ResponseMappingTests.cs#L30) *(7 hàm trong file, cùng 1 mối quan tâm)* | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter "FullyQualifiedName~ResponseMappingTests"` |
| FR-001/US2 — cấu hình YARP chỉ có đúng 1 route/1 cluster trỏ vào BFF, không route thẳng tới service nghiệp vụ nào | [`RouteConfigurationTests.cs:39`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L39) · [`:63`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L63) · [`:85`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L85) · [`:111`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L111) · [`:137`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L137) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~RouteConfigurationTests"` |
| US2 — request thật đi đúng đường qua gateway; health probe của chính gateway không bị route catch-all "nuốt" | [`RoutingTests.cs:27`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L27) · [`:62`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L62) · [`:90`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L90) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~RoutingTests"` |
| FR-007 — path không khớp route nào → `404` rõ ràng, không treo, không lộ chi tiết định tuyến | [`UnmatchedRouteTests.cs:49`](../../services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs#L49) · [`:78`](../../services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs#L78) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~UnmatchedRouteTests"` |
| US3/SC-003/T054 — downstream (Products/Baskets/Orders/Parties) không sẵn sàng → BFF trả lỗi có cấu trúc, đúng ngân sách thời gian, không lộ topology | [`DownstreamUnavailableTests.cs:50`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L50) · [`:78`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L78) · [`:108`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L108) · [`:143`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L143) · [`:178`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L178) · [`:208`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L208) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~DownstreamUnavailableTests"` |
| FR-006 — BFF không sẵn sàng → gateway trả lỗi có cấu trúc, gateway tự vẫn khoẻ, không lộ topology | [`DownstreamUnavailableTests.cs:33`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L33) · [`:62`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L62) · [`:88`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L88) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~DownstreamUnavailableTests"` |
| T058 — timeout forward của gateway luôn ≥ ngân sách timeout của BFF (bất biến xuyên 2 service, T058 trong tasks.md) | [`ForwardingTimeoutBudgetTests.cs:38`](../../services/gateway/tests/Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests.cs#L38) · [`:61`](../../services/gateway/tests/Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests.cs#L61) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~ForwardingTimeoutBudgetTests"` |
| Regression — correlation ID còn nguyên xuyên gateway → BFF (bug thật đã tìm & sửa trong chính spec 002, xem "Phase 6 implementation notes" của `tasks.md`) | [`CorrelationIdPropagationTests.cs:36`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L36) · [`:71`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L71) · [`:107`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L107) · [`:143`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L143) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~CorrelationIdPropagationTests"` |

**Đã sửa 1 chỗ nhầm lẫn so với bảng cũ**: `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` **không** phải test của spec 002 — đã xác minh qua `git log` là file này được tạo ở commit của spec **016-correlation-id-propagation** (2026-09-06), muộn hơn hẳn spec 002 (2026-08-15). Bảng cũ liệt nó cùng dòng với `CorrelationIdPropagationTests.cs` (file thật của 002) khiến 2 spec bị lẫn vào nhau — xem [QA_Debt.md](QA_Debt.md).

**Kết quả lượt QA này (2026-09-27)**: `ProductsRouteTests` **2/2**; `ResponseMappingTests` **9/9**; `RouteConfigurationTests` **5/5**; `RoutingTests` **4/4**; `UnmatchedRouteTests` **4/4**; `DownstreamUnavailableTests` (bff) **8/8**, (gateway) **3/3**; `ForwardingTimeoutBudgetTests` **2/2**; `CorrelationIdPropagationTests` **4/4**.

## Kết luận

**PASS kèm ghi chú.** Cả 4 nguồn nhất quán với nhau và với mã thật; gateway → BFF → 4 service chạy đúng bằng Postman; khi `products-api` chết BFF trả `504` có cấu trúc sau 3 giây, gateway vẫn sống. Ghi chú: (1) khi **BFF** chết, gateway trả `502` thân rỗng sau 8 giây — vượt SC-003 (< 5 giây) và không có `ProblemDetails`; (2) `docker-compose.yml` mặc định publish thêm cổng `identity-api` (`5205`) nên "chỉ gateway/storefront" không còn đúng;
(3) `specs/002-gateway-bff-routing/quickstart.md` ghi sai cổng baskets/orders, 2 test gateway "xanh giả" không gắn token; (4) Postman folder `Common`/`Smoke Flow` từng dọn nhầm giỏ (subject giả thay vì `sub` của token) — đã sửa. Chi tiết: [QA_Debt.md](QA_Debt.md).
