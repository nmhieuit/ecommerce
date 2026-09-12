# Kiến trúc: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-25 ("[SECURE-3] OTel traces/metrics/logs via ServiceDefaults to Elastic"),
đặc tả tại [`specs/017-otel-servicedefaults-elastic/`](../../specs/017-otel-servicedefaults-elastic/).
Tám quyết định kiến trúc: [`research.md`](../../specs/017-otel-servicedefaults-elastic/research.md).
Xây trên nền `ServiceDefaults`/`CorrelationIdMiddleware` đã có từ `001` và lan truyền ID đã vá ở `016`.

**Trạng thái xác minh**: 17 task `[X]` trong `tasks.md`. Khác `016`, tính năng này chạy được **thật**
trên `docker-compose.local.yml` đầy đủ, gồm cả `elasticsearch`/`kibana` mới thêm — 3 bằng chứng thật
lấy từ truy vấn Elasticsearch thật, xem [technical-debt.md](technical-debt.md).

## 1. Kiến trúc tổng thể

```
7 service (gateway/bff/4 domain/identity)
   │ builder.AddServiceDefaults() — ĐĂNG KÝ DUY NHẤT 1 NƠI, không service nào tự cấu hình OTel riêng
   │ AddOpenTelemetry().WithTracing()/.WithMetrics().AddOtlpExporter()
   │ builder.Logging.AddOpenTelemetry() — IncludeScopes=true, CorrelationId/Tenant/Subject tự kèm log
   ▼ OTLP gRPC/HTTP (4317/4318)
otel-collector (image pin cứng phiên bản, không dùng :latest)
   │ exporter "elasticsearch" có sẵn của otel-collector-contrib — KHÔNG dựng thêm APM Server
   ▼
Elasticsearch (single-node, xpack security tắt — chỉ local dev)
   ▼
Kibana ← SRE tra cứu theo 1 Correlation ID → thấy trace + metric + log của cả hành trình
```

## 2. Mô tả từng thành phần

### 2.1. `ServiceDefaults` — không sửa mã C# nào (research.md Decision 1)

Điểm bất ngờ nhất của tính năng này: `AddServiceDefaults()`/`UseServiceDefaults()` **đã đúng cấu
trúc từ `001`** — `AddOtlpExporter()` cho traces/metrics, `builder.Logging.AddOpenTelemetry()` cho
log có cấu trúc, `IncludeScopes=true` đã kéo theo `CorrelationId` (016) vào mọi dòng log. Việc còn
thiếu hoàn toàn nằm ở **hạ tầng phía sau** collector, không phải code service — comment gốc của
`ServiceDefaultsExtensions.cs` đã ghi "forwards to the Elastic stack" **từ trước cả khi Elastic tồn
tại**, tức mục tiêu này đã được ngầm định từ sớm, `017` chỉ hiện thực hoá nó.

### 2.2. Elasticsearch + Kibana tham gia `docker-compose.yml` mặc định (research.md Decision 2)

Không tách 1 file `docker-compose.observability.yml` riêng/optional: mọi service đã trỏ cứng
`OTEL_EXPORTER_OTLP_ENDPOINT` vào collector từ `001`, không có Compose profile nào bọc quanh nó — nên
phần "backend" collector cần cũng phải khởi động cùng lúc trong cùng file, đúng tinh thần "chạy được
bằng 1 lệnh duy nhất" của `005`. `docker-compose.local.yml` (self-contained, không override) nhân bản
đầy đủ 2 service này, không chỉ thêm 1 lần ở `docker-compose.yml` rồi trông cậy override.

### 2.3. Exporter `elasticsearch`, không dựng APM Server (research.md Decision 3)

`otel-collector-contrib` (bản `contrib`, không phải `core`, đã dùng từ trước) đóng gói sẵn component
exporter `elasticsearch` (`mapping.mode: otel`) — Elasticsearch 9.x nhận trực tiếp dữ liệu OTel-native
qua exporter này, Kibana đọc thẳng qua ứng dụng Observability. Không cần APM Server làm khâu trung
gian giải mã OTLP — việc collector đã tự làm. Cùng exporter `debug` (đã có từ `005`) vẫn giữ nguyên
trong cùng 3 pipeline (`traces`/`metrics`/`logs`) — `elasticsearch` là exporter **thứ hai**, không
thay thế.

### 2.4. Bảo mật/topology cục bộ, ghim phiên bản image (research.md Decision 5, 7)

`discovery.type: single-node`, `xpack.security.enabled: "false"` — tư thế local-dev giống hệt mọi
datastore khác trong repo (SQL Server `sa`/plaintext, RabbitMQ `guest`/`guest`). `otel-collector` đổi
từ `:latest` sang pin cứng `0.160.0` — vì exporter `elasticsearch` là component tương đối mới, bề mặt
cấu hình đã đổi giữa các bản `contrib`; pin tránh 1 `docker compose pull` âm thầm phá cấu hình.

### 2.5. `vm.max_map_count` trên Windows/Docker Desktop (research.md Decision 6)

Elasticsearch cần `vm.max_map_count >= 262144` — trên Docker Desktop (Windows/macOS), giá trị này
nằm trong VM Linux ẩn phía sau, không sửa được qua `sysctl` của container. Quyết định: **ghi rõ như
điều kiện tiên quyết** trong `quickstart.md`, không cố giải quyết bằng cấu hình Compose.

## 3. Bảng quyết định — ai chịu trách nhiệm phát cái gì

| Thành phần | Chịu trách nhiệm | KHÔNG chịu trách nhiệm |
|---|---|---|
| `ServiceDefaults` (mọi service) | Sinh trace/span, metric, structured log; gắn CorrelationId/Tenant/Service vào tất cả | Biết địa chỉ Elasticsearch, xác thực với nó |
| `otel-collector` | Nhận OTLP, xuất tiếp qua exporter `elasticsearch` + `debug` | Sinh dữ liệu, quyết định service nào phải log gì |
| `Elasticsearch`/`Kibana` | Lưu trữ, index hoá, cho SRE tra cứu | Không lưu dữ liệu nghiệp vụ/tenant — chỉ telemetry nền tảng |

## 4. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/017-otel-servicedefaults-elastic-component.drawio`](../diagrams/017-otel-servicedefaults-elastic-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/017-otel-servicedefaults-elastic-flow-nghiep-vu.drawio`](../diagrams/017-otel-servicedefaults-elastic-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/017-otel-servicedefaults-elastic-sequence.drawio`](../diagrams/017-otel-servicedefaults-elastic-sequence.drawio)

## 5. Tham khảo thêm

Nền tảng `ServiceDefaultsExtensions.cs`/`CorrelationIdMiddleware.cs` đã giải thích ở
[03-giai-doan-1-nen-tang-dich-vu-va-routing.md](../onboarding/03-giai-doan-1-nen-tang-dich-vu-va-routing.md)
— không lặp lại chi tiết cơ chế nền ở đây.

3 bằng chứng thật (truy vấn Elasticsearch, thử nghiệm rút ServiceDefaults 2 chiều, xác nhận không phá
bất biến 004 SC-010) và giới hạn phạm vi (chỉ máy dev, cần chỉnh `vm.max_map_count` thủ công): xem
[technical-debt.md](technical-debt.md).
