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

Spec **cố ý không thêm test tự động** (research.md Decision 1, 8) nên không có bảng "Tự động"; toàn bộ kiểm chứng làm tay bằng Postman trên stack thật.

### Thủ công — bấm Postman, bật/tắt collector

Dựng stack: `docker compose -f docker-compose.local.yml up -d --wait gateway-api bff-api products-api baskets-api orders-api parties-api elasticsearch otel-collector kibana` (kéo theo identity-api và DB; Kibana khởi động chậm ~3 phút).
Postman: import [`postman/ecommerce.postman_collection.v2.json`](../../postman/ecommerce.postman_collection.v2.json) và
[`postman/local.postman_environment.v2.json`](../../postman/local.postman_environment.v2.json) (có `elasticsearchUrl`, `kibanaUrl`), chọn environment **Ecommerce - Local**; chạy `00 - Xác thực & phân quyền → 01 Lấy access token`, rồi folder
**`17 - Telemetry OTel tới Elasticsearch`** (bước 01 → 07) bằng **Runner** (bước 05 chờ 20 giây cho log tới Elasticsearch).

**Công tắc** (hạ tầng, không sửa mã): `docker compose -f docker-compose.local.yml stop otel-collector` (TẮT telemetry) / `start otel-collector` (BẬT).

| Bước | Cấu hình cần chỉnh | Request Postman | Kỳ vọng theo tài liệu | **Đã quan sát (2026-09-26)** |
|---|---|---|---|---|
| Elasticsearch + Kibana sống | Mặc định | `17` bước 01, 02 | `green`/`yellow`; Kibana `available` | Đúng (Elasticsearch 9.4.4, Kibana 9.4.4 `available`) |
| FR-001 — traces của mọi service | Collector BẬT | `17` bước 03 | Span của cả 7 service trong 2 phút gần nhất | Đúng (health probe tạo span đều đặn) |
| FR-002 — metrics của mọi service | Collector BẬT | `17` bước 04 | Metrics của cả 7 service | Đúng |
| FR-003/SC-003 — log mang 3 định danh | Collector BẬT | `17` bước 05 → 06 | `service.name`, `TenantId`, `CorrelationId` trên log của ≥ 2 service cho ID vừa gửi | Đúng (log của gateway + BFF cho ID `qa017-…`) |
| FR-009/SC-005 — histogram độ trễ HTTP | Mặc định | `17` bước 07 | ≥ 1 document `metrics.http.server.request.duration` | **Đỏ: 0 document** — collector bỏ mọi histogram cumulative (xem QA_Debt) |
| **TẮT** collector — service vẫn chạy (FR-007) | `stop otel-collector`, chờ 150 giây | `17` bước 03, 04, 06 | Telemetry ngừng, service không hỏng | Bước 03/04/06 **đỏ** (không còn dữ liệu trong 2 phút) nhưng `health/live` 4–10 ms và `GET /bff/products` 50 ms — service không chậm/không hỏng |
| **BẬT** lại collector | `start otel-collector`, chờ 60 giây | `17` bước 03 → 06 | Telemetry phục hồi | Traces và log xanh lại; metrics thiếu `Identity.Api` trong cửa sổ 2 phút (tạm thời) |
| Hướng vá histogram *(thử cấu hình: sửa `docker/otel-collector-config.yaml`, hoàn tác sau)* | Thêm `cumulativetodelta:` vào `processors` và `metrics.processors: [cumulativetodelta, batch]`, `restart otel-collector`, chờ ~150 giây | `17` bước 07 | Có histogram | **59 document** histogram; folder **9/9 xanh**; hoàn tác cấu hình + khởi động lại collector từ file sạch, `git status` sạch |
| Scenario 1 — không log chuỗi nội suy *(ngoại lệ: quét mã, không có runtime)* | (không có) | (không có) — tìm `Log*($"` trong `services/**/*.cs` | 0 kết quả | 0 kết quả |
| Scenario 5 — gỡ `ServiceDefaults` khỏi `Products.Api` *(ngoại lệ: sửa mã, cần `build --no-cache`)* | (không có) | (không có) | Mất telemetry của service đó, service vẫn chạy | Không lặp lại lượt này; kết quả 2026-09-24 (0 span/0 metric của `Products.Api`, `200 OK`) giữ nguyên |
| Dọn dẹp | Trả collector về file cấu hình gốc | (không có) | Không dữ liệu dư | Đã hoàn tác `docker/otel-collector-config.yaml`; Kibana và Elasticsearch để chạy |

### Tự động

Không có (spec cố ý không thêm test; xem đoạn đầu mục này).

## Kết luận

**PASS kèm ghi chú.** 4 nguồn nhất quán với nhau và với mã/hạ tầng thật; US1 (trace/metrics/logs đầy đủ 1 đơn hàng, SC-001/002/003) và US3 (`ServiceDefaults` chịu tải thật,
FR-007/SC-004) đã tự xác nhận sống. Ghi chú: (1) **histogram metric — gồm đúng chỉ số độ trễ mà FR-009/SC-005 cần — bị `otel-collector` âm thầm loại bỏ** (lệch temporality
cumulative/delta, thiếu processor chuyển đổi — đã thử thêm `cumulativetodelta` và có 59 document histogram, folder Postman 9/9 xanh); spec 021 né được bằng cách tính từ span nhưng FR-009 theo nghĩa đen không đạt; (2) tái hiện độc lập sự cố khoá ký `identity-api` đã có ở
mục 026 của `technical-debt.md`; (3) lệch nhỏ "8.x/9.4.4" ở contract; (4) ghi chú phương pháp `--no-cache`; (5) tắt collector không làm service chậm/hỏng, nhưng `dotnet test` trên máy đang bật stack đẩy telemetry của test vào cùng Elasticsearch. FR-008/SC-006 (không PII) chưa quét định lượng (mẫu 59 log không có PII).
Chi tiết, bằng chứng và hướng vá: [QA_Debt.md](QA_Debt.md) mục 017.
