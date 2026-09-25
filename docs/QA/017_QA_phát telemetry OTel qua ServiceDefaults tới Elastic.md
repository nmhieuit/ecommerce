# QA: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

*Đối tượng đọc: QA Lead / kỹ sư kiểm thử, cần biết luồng happy-case của spec này có được tài liệu hoá
đúng và nhất quán hay không, trước khi tin tưởng dùng tài liệu để viết test case.*

Spec không sửa dòng C# nào (`git show 06248c5 --stat` — không file `.cs`): chỉ thêm `elasticsearch`/`kibana` vào `docker-compose.yml`
và exporter `elasticsearch` (song song `debug`) trong `otel-collector-config.yaml`; `AddServiceDefaults()`/`UseServiceDefaults()` (từ spec 001) đã sẵn
cấu trúc phát OTel. **Spec cố ý không thêm test tự động** (research.md Decision 1, 8) nên không có bảng "Tự động" với test C#/TS.

## Luồng happy-case đã rà soát

1. Mọi service `builder.AddServiceDefaults()` đăng ký `AddOpenTelemetry().WithTracing()/.WithMetrics().AddOtlpExporter()` và
   `Logging.AddOpenTelemetry(IncludeScopes = true)` — 1 nơi duy nhất ([`ServiceDefaultsExtensions.cs:28-65`](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs#L28)).
2. `otel-collector` (ghim `0.160.0`) nhận OTLP, xuất song song `debug` (cho `scripts/demo.ps1`) và `elasticsearch` (`mapping.mode: otel`) — không cần APM Server.
3. Elasticsearch (`9.4.4`, single-node, không xác thực — tư thế local-dev) lưu `traces-/metrics-/logs-generic.otel-default*`; Kibana đọc qua Observability.
4. `CorrelationIdMiddleware` (016) gắn `correlation.id` vào Activity + logging scope — 1 luồng xử lý tra được bằng đúng 1 giá trị.

## Hướng dẫn kiểm thử happy-case (thủ công + tự động)

Đã tự dựng stack qua `docker-compose.local.yml` (7 service + Elasticsearch + Kibana + RabbitMQ + Redis; bỏ `storefront`), tự đặt 1 đơn hàng thật qua gateway `:5300` và
truy vấn thẳng REST API Elasticsearch `:9200`.

### Thủ công — chạy đúng như spec mô tả

| Bước (quickstart) | Cách làm | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-24)** |
|---|---|---|---|
| Scenario 1 — không còn log chuỗi nội suy | Grep `Log(Information\|Warning\|Error\|Debug\|Critical\|Trace)\(\$"` trong `services/` | 0 dòng | **0 kết quả** |
| Scenario 2 — trace đầy đủ 1 đơn hàng (SC-001) | `POST /bff/checkout` kèm `X-Correlation-Id: $CID`, truy vấn `traces-generic.otel-default*` theo `attributes.correlation.id` | Thấy span mọi hop | **13 span khớp, đủ 5 service**: Baskets 3, Bff 3, Gateway 3, Orders 2, Products 2 — không hop đứt đoạn |
| Scenario 3 — metrics từng service | Aggregation `terms` trên `resource.attributes.service.name` của `metrics-generic.otel-default*` | Đủ 7 service | **Đủ 7**: Bff 309, Products 254, Orders 251, Baskets 241, Identity 238, Gateway 235, Parties 220 (nhưng histogram độ trễ bị collector loại — xem QA_Debt) |
| Scenario 4 — log mang đủ 3 định danh (SC-003) | Truy vấn `logs-generic.otel-default*` theo `attributes.CorrelationId` | Có `service.name`, `TenantId`, `CorrelationId` | **59 log entry**; mẫu đọc đều đủ 3 định danh (`Gateway.Api`, `contoso`, khớp `$CID`) |
| Scenario 5 — gỡ `ServiceDefaults` khỏi `Products.Api` (FR-007/SC-004) | Comment `AddServiceDefaults()`/`UseServiceDefaults()`, `build --no-cache` + `up -d products-api`, gọi qua gateway | Mất traces/metrics/logs của service đó | Health vẫn `healthy`, request `200 OK`; trace theo correlation ID chỉ còn `{Bff, Gateway}` — **0 span `Products.Api`**, **0 metric** trong 3 phút |
| Scenario 5 — khôi phục | `git checkout --` file, rebuild, gọi lại | Telemetry trở lại | Trace mới có đủ `{Bff, Gateway, Products}`; `git status` sạch |

**Kết quả lượt QA này**: cả 5 kịch bản `quickstart.md` chạy được thật trên stack đầy đủ (phiên triển khai gốc không có Docker, chỉ xác nhận gián tiếp).

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã/hạ tầng thật; US1 (trace/metrics/logs đầy đủ 1 đơn hàng, SC-001/002/003) và US3 (`ServiceDefaults` chịu tải thật,
FR-007/SC-004) đã tự xác nhận sống. Ghi chú: (1) **histogram metric — gồm đúng chỉ số độ trễ mà FR-009/SC-005 cần — bị `otel-collector` âm thầm loại bỏ** (lệch temporality
cumulative/delta, thiếu processor chuyển đổi); spec 021 né được bằng cách tính từ span nhưng FR-009 theo nghĩa đen không đạt; (2) tái hiện độc lập sự cố khoá ký `identity-api` đã có ở
mục 026 của `technical-debt.md`; (3) lệch nhỏ "8.x/9.4.4" ở contract; (4) ghi chú phương pháp `--no-cache`. FR-008/SC-006 (không PII) chưa quét định lượng (mẫu 59 log không có PII).
Chi tiết, bằng chứng và hướng vá: [QA_Debt.md](QA_Debt.md) mục 017.
