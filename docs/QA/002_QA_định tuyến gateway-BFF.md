# QA: Định tuyến Gateway → BFF cho Products/Baskets/Orders/Parties

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

**Nguồn đối chiếu**:
[`architecture/002`](../architecture/002_Architect_định%20tuyến%20gateway-BFF.md) ·
[`development/002`](../development/002_Development_định%20tuyến%20gateway-BFF.md) ·
[`summary/002`](../summary/002_PO_định%20tuyến%20gateway-BFF.md) ·
[`spec-summary-vi/002`](../spec-summary-vi/002-gateway-bff-routing.json) — đối chiếu chéo cả 4, kèm
xác minh lại với source code thật khi có nghi vấn.

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

### Thủ công — US1 / US2 (SC-001, SC-002)

```bash
cp .env.example .env   # chỉ cần 1 lần cho cả repo
docker compose -f docker-compose.local.yml up -d --wait gateway-api
```

Lệnh trên tự kéo theo `bff-api`, cả 4 domain service, `identity-api` và DB riêng từng service —
`--wait` tự chặn tới khi mọi container khai báo `healthy`.

| Bước | Postman request | Kỳ vọng |
|---|---|---|
| Gateway sống | Folder `Gateway` → "Health live của chính gateway" → **Send** | `200 OK` |
| OpenAPI đi qua gateway | Folder `Gateway` → "Tài liệu OpenAPI đi qua được gateway" → **Send** | `200 OK`; assertion sẵn xác nhận tài liệu trả về đúng là của BFF (có path `/bff/products`) |
| Gateway chuyển tiếp xuống BFF | Folder `Gateway` → "Route phía client được chuyển tiếp xuống BFF" → **Send** | Không phải `404` — tức là request đã chạm được handler của BFF phía sau, không dừng lại ở gateway |
| BFF proxy đúng route sản phẩm (SC-002) | Folder `Common` → "BFF: danh sách sản phẩm, gọi thẳng có header" → **Send** | `200 OK`; body là object bọc ngoài có `items` (mảng), không phải mảng trần — đúng shape đã định hình lại, không phải pass-through thô từ Products |
| Đường dẫn không khớp route nào (FR-007) | Folder `Gateway` → "Đường dẫn không tồn tại" → **Send** | `404`; assertion sẵn xác nhận body không lộ `bff-cluster`, `bff-route`, `products-api` hay số cổng nội bộ, và trả lời không treo |

**Xác nhận US2 bằng review cấu hình (không phải bằng Postman)**: mở
[`docker-compose.yml`](../../docker-compose.yml) (file **mặc định**, không phải `.local.yml` dùng cho
Postman ở trên) — xác nhận chỉ `gateway-api` (và `storefront`) có khai `ports:` publish ra host; 4
domain service + `bff-api` không có `ports:` nào, chỉ reachable qua network nội bộ `backbone`. Đây
chính là cách quickstart gốc của spec (Scenario 2/3) xác minh "single entry point" — dùng
`docker-compose.local.yml` publish đủ cổng chỉ phục vụ mục đích gọi thẳng từng service khi QA cần cô
lập lỗi, không phải để tái hiện topology thật.

### Thủ công — US3 (SC-003)

```bash
docker compose -f docker-compose.local.yml stop products-api
```

| Bước | Postman request | Kỳ vọng |
|---|---|---|
| Downstream không sẵn sàng | Folder `Common` → "BFF: thiếu header thì downstream từ chối" → **Send** (hoặc gọi lại "BFF: danh sách sản phẩm, gọi thẳng có header" sau khi stop `products-api`) | `502` hoặc `504`, không bao giờ `200`; đo thời gian phản hồi bằng tab Postman — phải dưới 5 giây |
| Khôi phục | `docker compose -f docker-compose.local.yml start products-api` | Gọi lại "BFF: danh sách sản phẩm, gọi thẳng có header" → `200` trở lại sau vài giây |
| Dọn dẹp | `docker compose -f docker-compose.local.yml down -v` | Xoá sạch container + volume |

### Thủ công — US1 (SC-004, không chứa nghiệp vụ)

Mở [`services/bff/src/Bff.Api/Features/`](../../services/bff/src/Bff.Api/Features/) — xác nhận mỗi
route handler (`Products`, `Baskets`, `Orders`, `Parties`, `Checkout`) chỉ gọi 1 typed downstream
client rồi map/shape response, không có rule nghiệp vụ miền, không validate ngoài phạm vi hình dạng
request, không tự lưu trữ dữ liệu.

### Tự động — chạy thẳng bộ test đã có, không cần viết mới

Mỗi link mở thẳng đúng dòng khai báo hàm test (comment mỗi hàm đã gắn `Task nguồn: spec 002 ...`).

| Cần xác nhận | Test case (bấm để mở) | Lệnh chạy riêng |
|---|---|---|
| US1/SC-002 — BFF proxy đúng route sản phẩm, trả shape đã định hình (không phải pass-through thô), kể cả catalog rỗng | [`ProductsRouteTests.cs:28`](../../services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs#L28)<br>[`ProductsRouteTests.cs:74`](../../services/bff/tests/Bff.Api.IntegrationTests/ProductsRouteTests.cs#L74) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~ProductsRouteTests"` |
| US1/FR-005/SC-004 — BFF chỉ shaping field-by-field (Products/Baskets/Orders/Parties), không rule nghiệp vụ, không làm tròn tiền | [`ResponseMappingTests.cs:30`](../../services/bff/tests/Bff.Api.UnitTests/ResponseMappingTests.cs#L30) *(7 hàm trong file, cùng 1 mối quan tâm)* | `dotnet test services/bff/tests/Bff.Api.UnitTests --filter "FullyQualifiedName~ResponseMappingTests"` |
| FR-001/US2 — cấu hình YARP chỉ có đúng 1 route/1 cluster trỏ vào BFF, không route thẳng tới service nghiệp vụ nào | [`RouteConfigurationTests.cs:39`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L39) · [`:56`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L56) · [`:73`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L73) · [`:93`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L93) · [`:116`](../../services/gateway/tests/Gateway.Api.UnitTests/RouteConfigurationTests.cs#L116) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~RouteConfigurationTests"` |
| US2 — request thật đi đúng đường qua gateway; health probe của chính gateway không bị route catch-all "nuốt" | [`RoutingTests.cs:26`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L26) · [`:51`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L51) · [`:76`](../../services/gateway/tests/Gateway.Api.IntegrationTests/RoutingTests.cs#L76) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~RoutingTests"` |
| FR-007 — path không khớp route nào → `404` rõ ràng, không treo, không lộ chi tiết định tuyến | [`UnmatchedRouteTests.cs:48`](../../services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs#L48) · [`:73`](../../services/gateway/tests/Gateway.Api.IntegrationTests/UnmatchedRouteTests.cs#L73) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~UnmatchedRouteTests"` |
| US3/SC-003/T054 — downstream (Products/Baskets/Orders/Parties) không sẵn sàng → BFF trả lỗi có cấu trúc, đúng ngân sách thời gian, không lộ topology | [`DownstreamUnavailableTests.cs:50`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L50) · [`:74`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L74) · [`:99`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L99) · [`:132`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L132) · [`:163`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L163) · [`:189`](../../services/bff/tests/Bff.Api.IntegrationTests/DownstreamUnavailableTests.cs#L189) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter "FullyQualifiedName~DownstreamUnavailableTests"` |
| FR-006 — BFF không sẵn sàng → gateway trả lỗi có cấu trúc, gateway tự vẫn khoẻ, không lộ topology | [`DownstreamUnavailableTests.cs:32`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L32) · [`:58`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L58) · [`:79`](../../services/gateway/tests/Gateway.Api.IntegrationTests/DownstreamUnavailableTests.cs#L79) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~DownstreamUnavailableTests"` |
| T058 — timeout forward của gateway luôn ≥ ngân sách timeout của BFF (bất biến xuyên 2 service, T058 trong tasks.md) | [`ForwardingTimeoutBudgetTests.cs:38`](../../services/gateway/tests/Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests.cs#L38) · [`:58`](../../services/gateway/tests/Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests.cs#L58) | `dotnet test services/gateway/tests/Gateway.Api.UnitTests --filter "FullyQualifiedName~ForwardingTimeoutBudgetTests"` |
| Regression — correlation ID còn nguyên xuyên gateway → BFF (bug thật đã tìm & sửa trong chính spec 002, xem "Phase 6 implementation notes" của `tasks.md`) | [`CorrelationIdPropagationTests.cs:36`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L36) · [`:66`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L66) · [`:97`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L97) · [`:129`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L129) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter "FullyQualifiedName~CorrelationIdPropagationTests"` |

**Đã sửa 1 chỗ nhầm lẫn so với bảng cũ**: `services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` **không** phải test của spec 002 — đã xác minh qua `git log` là file này được tạo ở commit của spec **016-correlation-id-propagation** (2026-09-06), muộn hơn hẳn spec 002 (2026-08-15). Bảng cũ liệt nó cùng dòng với `CorrelationIdPropagationTests.cs` (file thật của 002) khiến 2 spec bị lẫn vào nhau — xem [QA_Debt.md](QA_Debt.md).

## Kết luận

**PASS** — cả 4 nguồn mô tả cùng 1 luồng happy-case, không mâu thuẫn nhau về nội dung nghiệp vụ.

Có 1 điểm tài liệu tham chiếu (`specs/002-gateway-bff-routing/quickstart.md`) ghi sai cổng
baskets/orders so với thực tế — không ảnh hưởng nội dung 4 nguồn chính đang được đối chiếu ở đây, xem
[QA_Debt.md](QA_Debt.md).
