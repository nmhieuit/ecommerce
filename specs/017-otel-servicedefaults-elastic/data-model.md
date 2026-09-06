# Data Model: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

**Spec**: [spec.md](spec.md) | **Research**: [research.md](research.md)

Tính năng này không thêm bảng hay entity lưu trữ nghiệp vụ nào — không có database mới, không có schema thay đổi ở bất kỳ service nào (research.md Decision 1). "Data model" ở đây là hình dạng của đường ống telemetry mà tính năng này hoàn thiện, mirror cách [016-correlation-id-propagation/data-model.md](../016-correlation-id-propagation/data-model.md) mô tả các hop lan truyền dù không có bảng nào.

## Đường ống Telemetry (Telemetry Pipeline)

Trạng thái từng chặng trên đường đi của một tín hiệu OTel (trace/metric/log), trước và sau tính năng này.

| Chặng | Cơ chế | Trạng thái trước tính năng này | Trạng thái sau tính năng này |
|---|---|---|---|
| Service → OTLP export | `ServiceDefaultsExtensions.AddServiceDefaults()` (`AddOtlpExporter()` cho traces/metrics; `logging.AddOtlpExporter()` cho logs) | Đúng, đã wire ở cả 7 service | Không đổi |
| OTLP export → OTel Collector | `OTEL_EXPORTER_OTLP_ENDPOINT=http://otel-collector:4317` (env var mỗi service trong `docker-compose*.yml`) | Đúng | Không đổi |
| Collector: nhận + xử lý | `receivers.otlp` (gRPC 4317 / HTTP 4318) → `processors.batch` (`docker/otel-collector-config.yaml`) | Đúng | Không đổi |
| Collector → Exporter `debug` | In ra log của chính collector — đủ để `scripts/demo.ps1` regex-parse bằng chứng hop (006) | Đúng, đang được một script thật dựa vào | Không đổi — giữ nguyên (research.md Decision 4) |
| Collector → Exporter `elasticsearch` | Không tồn tại | **Mới** — `endpoints: [http://elasticsearch:9200]`, `mapping.mode: otel`, gắn vào cả 3 pipeline (traces/metrics/logs) | |
| Elasticsearch: lưu trữ + đánh index | Không tồn tại (không có service `elasticsearch` nào trong bất kỳ file compose nào) | **Mới** — single-node, nhận dữ liệu OTel-native, tự tạo data stream theo chế độ mapping `otel` | |
| Kibana: truy vấn + hiển thị | Không tồn tại | **Mới** — trỏ vào Elasticsearch cùng cụm, dùng ứng dụng Observability để xem trace/log theo `correlation.id`/`service.name` | |

## Định danh mang theo trên mỗi tín hiệu (Carried Identifiers)

Không đổi bởi tính năng này — đã đúng từ trước (research.md Decision 1) — nhưng là điều kiện để Success Criteria SC-002/SC-003 kiểm chứng được một khi dữ liệu tới nơi Elasticsearch/Kibana có thể truy vấn.

| Định danh | Nguồn | Cơ chế mang theo |
|---|---|---|
| `service.name` | `ConfigureResource(r => r.AddService(builder.Environment.ApplicationName))` | OTel resource attribute — tự động đính kèm vào mọi trace/metric/log record bởi SDK, không cần code ở từng call site |
| `CorrelationId` | `CorrelationIdMiddleware` (`shared/ServiceDefaults`) | `Activity.Current?.SetTag("correlation.id", ...)` (trace) + `ILogger.BeginScope(...)` (log, nhờ `logging.IncludeScopes = true`) |
| `TenantId` | `TenantContextMiddleware` (`shared/Tenancy`) | `ILogger.BeginScope(...)` — chỉ khi header `X-Tenant-Id` đã được resolve (Unresolved thì không mở scope, xem doc-comment gốc) |

## Cấu hình hạ tầng mới (Infrastructure Configuration Surface)

Không phải entity, nhưng là bề mặt cấu hình mà tính năng này thêm vào — liệt kê ở đây để `contracts/` và `quickstart.md` tham chiếu thay vì lặp lại.

| Thành phần | Nơi khai báo | Ghi chú |
|---|---|---|
| `elasticsearch` (service) | `docker-compose.yml`, `docker-compose.local.yml` | Single-node, `xpack.security.enabled=false`, volume riêng `elasticsearch-data`/`local-es-data` (research.md Decision 5) |
| `kibana` (service) | `docker-compose.yml`, `docker-compose.local.yml` | `depends_on: elasticsearch (service_healthy)`, không xác thực |
| Exporter `elasticsearch` | `docker/otel-collector-config.yaml`, `docker/otel-collector-config.demo.yaml` | Thêm cạnh `debug`, không thay thế (research.md Decision 3, 4) |
| Ghim phiên bản image | `docker-compose.yml`, `docker-compose.local.yml` (collector), cả hai file compose (Elasticsearch/Kibana) | `otel-collector-contrib` đổi từ `:latest` sang tag cụ thể; Elasticsearch/Kibana dùng cùng một tag 8.x cụ thể (research.md Decision 7) |
| Cổng publish mới (chỉ ở stack "thử bằng tay") | `docker-compose.local.yml` | `9200:9200` (Elasticsearch REST), `5601:5601` (Kibana UI) — `docker-compose.yml` mặc định KHÔNG publish, giữ đúng bất biến "chỉ 2 cổng" của spec 004 SC-010 |
