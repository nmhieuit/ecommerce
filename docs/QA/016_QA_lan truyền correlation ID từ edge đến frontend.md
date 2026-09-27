# QA: Lan truyền Correlation ID từ Edge đến Frontend

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec không xây cơ chế mới — vá 3 khoảng trống trong `CorrelationIdMiddleware` (từ spec 001): giá trị client tự gửi chưa được validate,
hop BFF → domain service làm rớt correlation ID, SPA không đọc được header bằng mã.

## Luồng happy-case đã rà soát

1. **Gateway sinh/giữ correlation ID có validate**: `CorrelationIdMiddleware.ResolveCorrelationId` giữ giá trị client gửi nếu hợp lệ (chuỗi mờ bất
   kỳ, không ép GUID), sinh `Guid` mới nếu thiếu/không hợp lệ; chỉ loại ký tự điều khiển (kể cả CRLF) và độ dài > 128.
2. **Gateway → BFF**: YARP tự copy header của request đã bị middleware ghi ngược — đã đúng từ spec 002.
3. **BFF → domain service**: `TenantPropagationHandler.SendAsync` đọc `HttpContext.Items[X-Correlation-Id]` tươi mỗi lần gọi (không capture ở constructor) và relay.
4. **Async**: `OrderEndpoints.cs:101` gán `httpContext.Items[…]` vào `OrderPlacedV1.CorrelationId` khi publish qua outbox (spec 024).
5. **CORS + SPA**: `Gateway.Api/Program.cs` `.WithExposedHeaders(X-Correlation-Id)`; `ApiError.correlationId` đọc từ `response.headers`.
6. **Không lẫn khi đồng thời**: rủi ro chỉ ở handler pool của `IHttpClientFactory` — có test tải đồng thời thật (10 request song song).

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

### Thủ công — bấm Postman, tra ID trong Elasticsearch

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api elasticsearch otel-collector` (kéo theo identity-api và DB).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json) (có biến `elasticsearchUrl`), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token`, rồi folder
**`16 - Lan truyền Correlation ID`** (bước 01 → 10) bằng **Runner** để các bước chờ 20 giây (telemetry tới Elasticsearch) có tác dụng; bước 09 tạo 1 đơn thật. Sau khi tạo lại container, gọi thử vài lần trước (đường ghi của checkout nguội, lần đầu thường `504`).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| FR-001 — gateway tự sinh ID | (không có công tắc) | `16` bước 01 | Header `X-Correlation-Id` 32 hex khi request không có | Đúng |
| FR-002 — giữ nguyên giá trị client, kể cả không phải GUID | (không có công tắc) | `16` bước 02 | Echo `caller-supplied-correlation-id` | Đúng |
| FR-009 — quá dài / ký tự điều khiển bị loại | (không có công tắc) | `16` bước 03 (129 ký tự), 04 (TAB) | Không echo, thay bằng GUID mới | Đúng cả 2 (32 hex mới) |
| FR-005/US2 — SPA đọc được header qua CORS | (không có công tắc) | `16` bước 05 (`Origin: {{storefrontOrigin}}`) | `Access-Control-Expose-Headers` có `X-Correlation-Id` | Đúng |
| FR-003/SC-001 — ID xuyên gateway → BFF → products | Bật Elasticsearch + otel-collector | `16` bước 06 → 07 | Span của cả 3 service mang cùng ID | Đúng: `Gateway.Api`, `Bff.Api`, `Products.Api` (mỗi service 3 span/3 request) |
| SC-001 — ID xuyên toàn luồng đặt hàng đồng bộ | Bật Elasticsearch + otel-collector | `16` bước 08 → 09 → 10 | Span của gateway, BFF, baskets, orders cùng ID | Đúng: `Baskets.Api`, `Bff.Api`, `Gateway.Api`, `Orders.Api` (cần làm ấm đường ghi trước, lần đầu `504`) |
| **Toàn folder** | | `16` bước 01 → 10 | Mọi assertion xanh | **13/13 xanh** (lượt đầu 10/13 vì 2 bước ghi `504` nguội + hệ quả ở bước 10) |
| FR-004/US1 AC3 — nhánh bất đồng bộ mang ID | `.env`: `ORDERS_RABBITMQ_CONNECTION=amqp://guest:guest@rabbitmq:5672` (BẬT), tạo lại `orders-api` | Folder `08` bước 03 → 04 | `OrderPlacedV1.correlationId` = `X-Correlation-Id` đã gửi | Đúng: test "correlationId … đúng X-Correlation-Id" xanh trên message thật trong RabbitMQ (2 test khác của folder 08 đỏ vì lỗi kiểu số đã ghi ở QA 008); máy QA này đã có sẵn dòng công tắc trong `.env` nên giữ nguyên |
| Scenario 7 — SPA hiển thị ID *(ngoại lệ: cần trình duyệt/SPA)* | (không có) | (không có) | SPA hiển thị mã tham chiếu khi lỗi | Không chạy lại trong phiên này; xác nhận bằng bước `05` (đủ header để SPA đọc) + test `fetcher.test.ts` |
| Dọn dẹp | Trả compose/`.env` về nguyên trạng | (không có) | Không dữ liệu dư | Đã xoá đơn do QA tạo (giữ đơn gốc QA 017), `.env` không đổi |

### Tự động

| Cần xác nhận (FR) | Test case (bấm để mở) | Lệnh chạy riêng test đó |
|---|---|---|
| FR-001/FR-002 — gateway tự sinh/giữ correlation ID | [`CorrelationIdPropagationTests.cs:36`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L36) · [`:71`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L71) (2 test này của spec 002) | `dotnet test services/gateway/tests/Gateway.Api.IntegrationTests --filter FullyQualifiedName~CorrelationIdPropagationTests` |
| FR-009 — loại ký tự điều khiển/độ dài quá mức | [`:108`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L108) · [`:145`](../../services/gateway/tests/Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs#L145) (2 test này do spec 016 thêm) | (lệnh như trên) |
| FR-003 — BFF → domain service relay đúng ID | [`Bff.Api.IntegrationTests/CorrelationPropagationTests.cs:43`](../../services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs#L43) | `dotnet test services/bff/tests/Bff.Api.IntegrationTests --filter FullyQualifiedName~CorrelationPropagationTests` |
| FR-007/US3 — 10 request đồng thời không lẫn ID | [`:80`](../../services/bff/tests/Bff.Api.IntegrationTests/CorrelationPropagationTests.cs#L80) | (lệnh như trên) |
| FR-004 — nhánh async mang correlation ID | không có test tự động (xem QA_Debt); đã kiểm tay ở bảng trên | — |
| FR-006/US2 — SPA đọc correlation ID bằng mã | [`frontend/apps/web/tests/shared/fetcher.test.ts:28`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L28) · [`:55`](../../frontend/apps/web/tests/shared/fetcher.test.ts#L55) | `cd frontend && corepack pnpm --filter web test -- fetcher` (chạy từ `frontend/`, xem QA_Debt) |

**Kết quả lượt QA này (2026-09-26)**: Gateway **4/4**, BFF **2/2**, frontend **5/5** (gồm 3 test kế thừa từ spec 004 cùng file) — không đổi so với lượt trước.

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã thật; trên hệ thống thật cả nhánh đồng bộ (sinh/giữ/loại ID, CORS, ID xuyên gateway → BFF → service, tra được trong Elasticsearch — 13/13 Postman) lẫn nhánh bất đồng bộ (`OrderPlacedV1.correlationId` khớp header) đều đúng; 11 test tự động xanh. Ghi chú:
(1) nhánh async chưa có test tự động, chỉ mới kiểm tay; (2) đường ghi của checkout nguội sau khi tạo lại container (`504` ở lần đầu) làm kịch bản Runner đỏ nếu không làm ấm; (3) CR/LF chỉ được test tích hợp bao phủ (Postman dùng TAB); (4) ghi chú công cụ `pnpm`. Chi tiết: [QA_Debt.md](QA_Debt.md) mục 016.
