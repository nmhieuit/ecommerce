# Contract: `X-Correlation-Id` Header

Hợp đồng nội bộ giữa mọi hop đồng bộ của nền tảng — gateway, BFF, và 4 domain service. Không phải một API mới; mở rộng/sửa hợp đồng đã ngầm định tồn tại từ `shared/ServiceDefaults/CorrelationIdMiddleware.cs` (dùng chung ở mọi service) và bổ sung phần lan truyền đang bị thiếu ở hop BFF → domain service (research.md Decision 1).

## Header

| | |
|---|---|
| Name | `X-Correlation-Id` |
| Direction | Request VÀ Response — khác `X-Tenant-Id` ([003-stub-identity-tenant-context/contracts/tenant-id-header.md](../../003-stub-identity-tenant-context/contracts/tenant-id-header.md): "unlike X-Correlation-Id, this is never echoed on the response"). Correlation ID được echo lại trên response vì đây chính là thông tin chẩn đoán mà client cần nhận lại (spec US2). |
| Giá trị | Chuỗi mờ. Client có thể cung cấp (được giữ nguyên nếu hợp lệ) hoặc để trống (gateway tự sinh `Guid.NewGuid().ToString("n")`) |
| Ràng buộc hợp lệ | Không chứa ký tự điều khiển (`< 0x20` hoặc `0x7F`); độ dài ≤ 128 ký tự (data-model.md — Correlation ID) |
| Cardinality | Đúng một giá trị cho mỗi request, luôn có mặt (khác `X-Tenant-Id`, vốn có thể Unresolved) |

## Producers

| Hop | Hành vi |
|---|---|
| Gateway (`CorrelationIdMiddleware`, qua `UseServiceDefaults()`) | **Sinh hoặc giữ nguyên.** Nếu request đi vào đã mang giá trị hợp lệ, giữ nguyên; nếu không (thiếu, rỗng, hoặc vi phạm ràng buộc hợp lệ ở trên), sinh giá trị mới. Ghi giá trị lên cả `Request.Headers` (để hop sau lan truyền) lẫn `Response.Headers` (để client nhận lại). |
| Gateway → BFF (YARP reverse proxy) | **Relay tự động.** YARP copy toàn bộ header của request đã bị middleware sửa — không cần mã tường minh (đã đúng từ trước tính năng này). |
| BFF → 4 domain service (`TenantPropagationHandler` mở rộng, research.md Decision 1) | **Relay tường minh, không resolve lại.** Đọc giá trị từ `HttpContext.Items[CorrelationIdMiddleware.HeaderName]` (giá trị `CorrelationIdMiddleware` của chính BFF đã resolve cho request này) và gắn vào mọi outbound `HttpRequestMessage` trước khi gửi — **đây là phần sửa chính của tính năng này**, trước đó không tồn tại. |
| Mỗi domain service (`CorrelationIdMiddleware`, giống gateway) | Nhận header từ BFF, giữ nguyên (không sinh mới trừ khi thiếu/không hợp lệ — chỉ xảy ra nếu bị gọi trực tiếp, bỏ qua BFF, một kịch bản không được hỗ trợ theo Principle IX). |

## Consumers

| Hop | Hành vi |
|---|---|
| Mọi service (`ILogger.BeginScope` qua `CorrelationIdMiddleware`) | Mọi log entry có cấu trúc trong phạm vi request mang `CorrelationId` trong scope (spec FR-005). |
| Mọi service (OpenTelemetry `Activity.Current?.SetTag("correlation.id", ...)`) | Correlation ID gắn vào span/trace hiện tại, xuất qua OTLP tới OTel Collector (`docker/otel-collector-config.yaml`). |
| `Bff.Api.ErrorHandling.DownstreamExceptionHandler` | Đọc lại giá trị từ `HttpContext.Items` để nhúng vào `ProblemDetails.Extensions["correlationId"]` khi một lời gọi downstream thất bại — cho phép SPA/SRE tra cứu ngay từ thân lỗi. |
| SPA (`frontend/packages/api-client/src/http/fetcher.ts`) | Đọc `response.headers.get(HeaderName)`, gắn vào `ApiError.correlationId` (contracts/spa-correlation-visibility-contract.md). |
| Tab mạng của trình duyệt (DevTools) | Hiển thị header phản hồi thô, không phụ thuộc CORS — đã đúng từ trước tính năng này (research.md Decision 5). |

## Trước và sau tính năng này

| | Trước | Sau (tính năng này) |
|---|---|---|
| Gateway → BFF | Đúng (YARP tự relay) | Không đổi |
| BFF → 4 domain service | **Sai** — header bị bỏ mất, mỗi service tự sinh ID mới cho hop này | Đúng — relay tường minh qua `TenantPropagationHandler` mở rộng |
| Log của baskets/orders/parties/products khi tra theo correlation ID gốc (từ gateway) | Không tìm thấy (ID khác) | Tìm thấy đầy đủ |
| Giá trị client-cung-cấp chứa ký tự điều khiển/quá dài | Được chấp nhận nguyên văn (rủi ro log injection) | Bị từ chối, gateway sinh giá trị mới thay thế |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Request không mang header | Gateway sinh giá trị mới; mọi hop sau đó thấy đúng giá trị đó |
| Request mang giá trị hợp lệ (bất kỳ chuỗi nào không vi phạm ràng buộc) | Giữ nguyên xuyên suốt, kể cả không phải định dạng GUID |
| Request mang giá trị chứa ký tự điều khiển hoặc dài hơn 128 ký tự | Bị coi như không có; gateway sinh giá trị mới |
| BFF gọi downstream nhưng `IHttpContextAccessor.HttpContext` là `null` (kịch bản không mong đợi ở request pipeline bình thường) | `Relay(...)` bỏ qua, không set header — downstream tự sinh ID riêng cho hop đó (suy thoái về đúng hành vi trước tính năng này, không throw) |
| Một domain service bị gọi trực tiếp, bỏ qua BFF (không được hỗ trợ theo Principle IX) | Service đó tự sinh ID mới như thể là edge — không có cơ chế phát hiện việc bỏ qua BFF trong phạm vi tính năng này |

## Stability

Hợp đồng nội bộ giữa các service trong cùng một deployment, không phải giao diện client bên ngoài (constitution Principle II versioning không áp dụng). Thay đổi khi SCRUM-25 (Elastic thật) hoặc SCRUM-31 (outbox/publisher thật) hoàn thành — dự kiến là bổ sung (thêm nơi tiêu thụ correlation ID) chứ không phải thay đổi tên/hướng/ràng buộc của header này.
