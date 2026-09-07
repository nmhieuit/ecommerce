# Kiến trúc: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-25 ("[SECURE-3] OTel traces/metrics/logs via ServiceDefaults to Elastic"),
đặc tả tại [`specs/017-otel-servicedefaults-elastic/`](../../specs/017-otel-servicedefaults-elastic/).
Tám quyết định kiến trúc: [`research.md`](../../specs/017-otel-servicedefaults-elastic/research.md).
Xây trên nền `ServiceDefaults`/`CorrelationIdMiddleware` đã có từ `001` và lan truyền ID đã vá ở `016`.

**Trạng thái xác minh**: 17 task `[X]` trong `tasks.md`. Khác `016`, tính năng này chạy được **thật**
trên `docker-compose.local.yml` đầy đủ, gồm cả `elasticsearch`/`kibana` mới thêm — bằng chứng dưới đây
lấy từ truy vấn Elasticsearch thật, không phải log console.

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
điều kiện tiên quyết** trong `quickstart.md`, không cố giải quyết bằng cấu hình Compose (không có cách
nào set giá trị này từ trong `docker-compose.yml` khi chạy trên Docker Desktop).

## 3. Bảng quyết định — ai chịu trách nhiệm phát cái gì

| Thành phần | Chịu trách nhiệm | KHÔNG chịu trách nhiệm |
|---|---|---|
| `ServiceDefaults` (mọi service) | Sinh trace/span, metric, structured log; gắn CorrelationId/Tenant/Service vào tất cả | Biết địa chỉ Elasticsearch, xác thực với nó |
| `otel-collector` | Nhận OTLP, xuất tiếp qua exporter `elasticsearch` + `debug` | Sinh dữ liệu, quyết định service nào phải log gì |
| `Elasticsearch`/`Kibana` | Lưu trữ, index hoá, cho SRE tra cứu | Không lưu dữ liệu nghiệp vụ/tenant — chỉ telemetry nền tảng |

## 4. Kết quả thực tế lúc triển khai (trích tasks.md, không suy đoán)

> **Truy vấn Elasticsearch thật** (không phải Kibana UI, gọi thẳng REST API
> `logs-generic.otel-default*/_search`, lọc `match_phrase` trên `attributes.CorrelationId`): trả về
> 46 log entry khớp cho 1 correlation ID mẫu. Kiểm tra 3 entry đầu: mỗi entry đều mang đủ
> `resource.attributes.service.name` (`Gateway.Api`, `Bff.Api`...), `attributes.TenantId = contoso`,
> và `attributes.CorrelationId` khớp chính xác — xác nhận trực tiếp FR-004/FR-005/SC-002/SC-003 bằng
> dữ liệu thật, không phải kỳ vọng lý thuyết.

**Thử nghiệm "rút ServiceDefaults ra xem có sao không" (FR-007/SC-004), chứng minh 2 chiều:**
> Comment 2 dòng đăng ký `ServiceDefaults` trong `Products.Api/Program.cs`, rebuild + restart container
> — health check vẫn `healthy` (không phụ thuộc ServiceDefaults). Gửi 2 request `200 OK` qua BFF kèm
> marker riêng, đợi batch flush, truy vấn Elasticsearch lọc theo `service.instance.id` của container
> MỚI và mốc thời gian sau khi container được tạo — **kết quả: 0 trace, 0 metric, 0 log mới từ
> `Products.Api`**, dù service vẫn phục vụ request thành công bình thường. Trace theo marker chỉ xuất
> hiện ở `Bff.Api`/`Gateway.Api` (vẫn có ServiceDefaults). Khôi phục lại 2 dòng (`git diff` rỗng —
> đúng nguyên trạng), rebuild + restart lại: telemetry phục hồi hoàn toàn, `service.instance.id` mới
> (lần thứ ba) xuất hiện ngay. Chứng minh đầy đủ, hai chiều — không chỉ "gỡ ra thấy mất", mà còn "lắp
> lại thấy có lại".

**Xác nhận không phá bất biến `004 SC-010` (chỉ 2 port publish ra ngoài ở stack mặc định):**
> `docker compose -f docker-compose.yml config` (resolve đầy đủ, kể cả overlay demo/debug) chỉ cho ra
> đúng 2 `published:` port (`5300` gateway, `4173` storefront) — `elasticsearch`/`kibana` trong
> `docker-compose.yml` xác nhận **không có `ports:` nào**. Bất biến giữ nguyên; `docker-compose.local.yml`
> mới là nơi republish 9200/5601 để dùng thủ công.

## 5. Giới hạn phạm vi đã biết

- **Chỉ chạy trên máy phát triển qua Docker Compose** — chưa phải hạ tầng vận hành thật cho môi trường
  sản phẩm chính thức (giống ranh giới đã nêu ở [017_PO_*.md](../summary/017_PO_nhìn%20thấy%20hệ%20thống%20đang%20chạy%20ra%20sao%20qua%20Elastic.md)).
- **Docker Desktop trên Windows/macOS cần chỉnh `vm.max_map_count` thủ công 1 lần** trước khi
  Elasticsearch khởi động được — ghi trong `quickstart.md`, không tự động hoá được qua Compose.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/017-otel-servicedefaults-elastic-component.drawio`](../diagrams/017-otel-servicedefaults-elastic-component.drawio)
- Sơ đồ luồng nghiệp vụ: [`docs/diagrams/017-otel-servicedefaults-elastic-flow-nghiep-vu.drawio`](../diagrams/017-otel-servicedefaults-elastic-flow-nghiep-vu.drawio)
- Sơ đồ trình tự: [`docs/diagrams/017-otel-servicedefaults-elastic-sequence.drawio`](../diagrams/017-otel-servicedefaults-elastic-sequence.drawio)

## 7. Tham khảo thêm

Nền tảng `ServiceDefaultsExtensions.cs`/`CorrelationIdMiddleware.cs` đã giải thích ở
[03-giai-doan-1-nen-tang-dich-vu-va-routing.md](../onboarding/03-giai-doan-1-nen-tang-dich-vu-va-routing.md)
— không lặp lại chi tiết cơ chế nền ở đây.
