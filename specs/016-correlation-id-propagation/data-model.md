# Data Model: Lan truyền Correlation ID từ Edge đến Frontend

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Tính năng này không thêm bảng hay entity lưu trữ nào — không có database mới, không có schema mới (research.md không đề cập storage mới). "Data model" ở đây là các khái niệm cấu trúc mà cơ chế lan truyền correlation ID vận hành trên đó, mirror cách [003-stub-identity-tenant-context/data-model.md](../003-stub-identity-tenant-context/data-model.md) mô tả `TenantContext` dù không có bảng nào.

## Correlation ID (giá trị header)

| Trường | Mô tả |
|---|---|
| Header | `X-Correlation-Id` (hằng số `CorrelationIdMiddleware.HeaderName`, `shared/ServiceDefaults`) |
| Nguồn sinh | Gateway (edge), khi request đi vào không mang sẵn giá trị hợp lệ — `Guid.NewGuid().ToString("n")` |
| Định dạng chấp nhận từ client | Chuỗi mờ (opaque string) bất kỳ, KHÔNG bắt buộc định dạng GUID (research.md Decision 2) |
| Ràng buộc hợp lệ (mới, Decision 2) | Không chứa ký tự điều khiển (code point < `0x20` hoặc `0x7F`); độ dài ≤ 128 ký tự. Vi phạm → bị coi như không có, gateway sinh giá trị mới thay thế. |
| Vòng đời | Sinh một lần tại gateway cho mỗi request; bất biến trong suốt luồng xử lý của request/luồng sự kiện bất đồng bộ phát sinh từ nó |

## Hop lan truyền (Correlation Propagation Hop)

Trạng thái của từng hop trên đường đi của request, trước và sau tính năng này. Đây là bảng trung tâm mà `contracts/correlation-id-header-contract.md` mô tả chi tiết hơn.

| Hop | Cơ chế | Trạng thái trước tính năng này | Trạng thái sau tính năng này |
|---|---|---|---|
| Client → Gateway | `CorrelationIdMiddleware` (sinh hoặc giữ nguyên) | Đúng | Không đổi |
| Gateway → BFF | YARP reverse proxy (tự copy header của request đã bị middleware sửa) | Đúng (có test) | Không đổi |
| BFF → 4 domain service (baskets/orders/parties/products) | Typed `HttpClient` qua `IHttpClientFactory` | **Sai** — không có gì relay header; mỗi service tự sinh ID mới (research.md Decision 1) | Đúng — `TenantPropagationHandler` mở rộng relay `X-Correlation-Id` |
| Mỗi domain service → log có cấu trúc | `CorrelationIdMiddleware` + `logging.IncludeScopes = true` | Đúng về mặt cơ chế, nhưng mang giá trị SAI (ID tự sinh cục bộ, không phải ID gốc) do hop trên bị lỗi | Đúng cả cơ chế lẫn giá trị |
| Gateway/BFF → phản hồi HTTP (response header) | `CorrelationIdMiddleware` (`Response.OnStarting`) | Đúng | Không đổi |
| Domain service → event bất đồng bộ (RabbitMQ) | Chưa tồn tại (chưa có publisher/consumer thật — SCRUM-31) | N/A | Không đổi trong phạm vi tính năng này — chỉ hợp đồng dữ liệu (mục dưới) đã sẵn sàng (research.md Decision 3) |

## Trường Correlation trên Event Contract (Event Correlation Field)

Áp dụng cho mọi integration event trong `shared/EventContracts` — đã tồn tại từ SCRUM-18, không thay đổi bởi tính năng này.

| Trường | Mô tả |
|---|---|
| Event | `BasketCheckedOutV1`, `OrderPlacedV1` |
| Trường mang correlation | `CorrelationId` (bắt buộc, `string`, JSON property `correlationId`) |
| Nguồn giá trị | Correlation ID của request đã tạo ra sự kiện đó (ví dụ request đặt hàng), được truyền vào tại thời điểm dựng payload (`BasketCheckedOutMapper.ToEvent(...)`) |
| Trạng thái transport | Hợp đồng dữ liệu đã sẵn sàng; việc thật sự publish/consume qua RabbitMQ chưa tồn tại (research.md Decision 3 — SCRUM-31) |

## Khả năng truy cập của SPA (SPA Correlation Access)

Hình dạng mới ở phía frontend (research.md Decision 5) — không phải entity lưu trữ, mà là hợp đồng dữ liệu giữa `fetcher.ts` và mã gọi nó.

| Trường | Mô tả |
|---|---|
| Nguồn | Header `X-Correlation-Id` trên phản hồi HTTP từ gateway, đọc qua `response.headers.get(...)` |
| Điều kiện tiên quyết | Gateway CORS policy phải khai báo header này trong `WithExposedHeaders(...)` (nếu không, JS không đọc được dù DevTools vẫn thấy) |
| Nơi mang giá trị | Trường mới `correlationId` trên `ApiError` (`frontend/packages/api-client/src/http/fetcher.ts`), luôn có mặt khi phản hồi mang header này — kể cả trên lỗi lẫn thành công (giá trị trả về qua `bffFetch` khi cần) |
| Khi vắng mặt | Lỗi mạng xảy ra trước khi có phản hồi (ví dụ mất kết nối) — không có response nên không có correlation ID để đọc; đây là giới hạn cố hữu, không phải lỗi cần xử lý thêm |

## Bảo đảm không lẫn lộn dưới tải đồng thời (Concurrency Isolation Guarantee)

Không phải entity, nhưng là một thuộc tính hành vi quan trọng cần ghi lại vì US3/FR-007 yêu cầu nó được chứng minh, không chỉ giả định (research.md Decision 7).

| Thành phần | Cơ chế đảm bảo cô lập |
|---|---|
| `CorrelationIdMiddleware` (sinh + `BeginScope`) | Mỗi `HttpContext` độc lập theo request; `ILogger.BeginScope` dựa trên `AsyncLocal` của .NET — không có state chia sẻ giữa các request đồng thời |
| Phần mở rộng relay ở `TenantPropagationHandler` (Decision 1) | Đọc `IHttpContextAccessor.HttpContext` tươi mỗi lần `SendAsync`, không capture giá trị ở constructor của handler — tránh lỗi "pooled handler mang state của request đã tạo ra nó" mà chính codebase đã cảnh báo cho tenant |
