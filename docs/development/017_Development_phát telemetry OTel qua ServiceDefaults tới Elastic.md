# Bước 017: Thay đổi nghiệp vụ so với bước 016

## Phạm vi

Bước 017 **không sửa một dòng C# nào**. Điểm bất ngờ chính (research.md Decision 1, đã ghi nhận trong
architecture doc): `AddServiceDefaults()`/`UseServiceDefaults()` đã đúng cấu trúc để phát OTel từ bước
001 — `AddOtlpExporter()`, `builder.Logging.AddOpenTelemetry()`, `IncludeScopes=true` kéo theo
CorrelationId (016) vào log. Phần còn thiếu nằm hoàn toàn ở hạ tầng phía sau collector, không phải
code service. Toàn bộ 017 là thay đổi devops: `docker-compose.yml`, `docker-compose.local.yml`,
`docker/otel-collector-config*.yaml`.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 016: commit `2abe34d`.
- Đặc tả và triển khai bước 017 — mốc hoàn tất, một commit duy nhất: commit `06248c5`.

`git show --stat` xác nhận: không file `.cs`, `.csproj`, hay `Dockerfile` nào bị đổi trong toàn bộ
commit.

## 1. `otel-collector` thêm exporter `elasticsearch`, không thay thế `debug`

[docker/otel-collector-config.yaml](../../docker/otel-collector-config.yaml)

```yaml
exporters:
  debug:
    verbosity: normal

  # 017: thêm bên cạnh debug, không thay thế. mode: otel ghi data stream OTel-native, ánh xạ ECS
  # (traces-generic.otel-default, metrics-generic.otel-default, logs-generic.default) mà ứng dụng
  # Observability của Kibana đọc thẳng — không cần APM Server làm khâu trung gian.
  elasticsearch:
    endpoints: [http://elasticsearch:9200]
    mapping:
      mode: otel

service:
  pipelines:
    traces:
      receivers: [otlp]
      processors: [batch]
      # 017: elasticsearch là exporter THỨ HAI — debug (005) vẫn giữ nguyên, vì scripts/demo.ps1
      # parse chính exporter đó để chứng minh service nào đã phục vụ một lượt chạy.
      exporters: [debug, elasticsearch]
    metrics:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug, elasticsearch]
    logs:
      receivers: [otlp]
      processors: [batch]
      exporters: [debug, elasticsearch]
```

## 2. `docker-compose.yml` thêm `elasticsearch` + `kibana` vào stack mặc định

[docker-compose.yml](../../docker-compose.yml)

```yaml
volumes:
  sqlserver-data:
  rabbitmq-data:
  # 017: telemetry, không phải dữ liệu nghiệp vụ — nhưng vẫn đáng sống sót qua `down` để trạng
  # thái đã lưu của Kibana (data view...) không reset mỗi lần.
  elasticsearch-data:

services:
  # 017: single-node, security tắt — cùng tư thế local-dev như mọi datastore khác trong file này
  # (SQL Server sa/plaintext, RabbitMQ guest/guest, Redis không xác thực). Cluster này chỉ chứa
  # telemetry của nền tảng, không bao giờ chứa dữ liệu tenant/nghiệp vụ.
  elasticsearch:
    image: docker.elastic.co/elasticsearch/elasticsearch:9.4.4
    environment:
      discovery.type: single-node
      xpack.security.enabled: "false"
      # 017: giới hạn để JVM heap không tranh chấp SQL Server/container khác trên máy dev —
      # không phải quyết định sizing cho production.
      ES_JAVA_OPTS: "-Xms512m -Xmx512m"
    volumes:
      - elasticsearch-data:/usr/share/elasticsearch/data
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://localhost:9200/_cluster/health | grep -Eq '\"status\":\"(green|yellow)\"' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 30
      start_period: 60s

  kibana:
    image: docker.elastic.co/kibana/kibana:9.4.4
    environment:
      ELASTICSEARCH_HOSTS: http://elasticsearch:9200
    depends_on:
      elasticsearch:
        condition: service_healthy
    # 017: không publish port ở đây — stack mặc định giữ đúng 2 port công khai (storefront,
    # gateway; spec 004 SC-010). docker-compose.local.yml mới republish 5601 để dùng thủ công.

  otel-collector:
    # 017: pin cứng, trước đó là ':latest' — bề mặt cấu hình của exporter elasticsearch đã đổi
    # giữa các bản contrib; pin tránh một `docker compose pull` âm thầm phá cấu hình.
    image: otel/opentelemetry-collector-contrib:0.160.0
    depends_on:
      elasticsearch:
        condition: service_healthy
```

`docker-compose.local.yml` (self-contained, không override) nhân bản đầy đủ hai service
`elasticsearch`/`kibana` này, đúng tinh thần "chạy được bằng một lệnh duy nhất" của bước 005 — không
tách một file `docker-compose.observability.yml` optional riêng, vì mọi service đã trỏ cứng
`OTEL_EXPORTER_OTLP_ENDPOINT` vào collector từ bước 001, không có Compose profile nào bọc quanh nó.

## Tóm tắt 016 → 017

| Khu vực | Bước 016 | Bước 017 |
|---|---|---|
| Code C# (`ServiceDefaults`) | Sinh trace/metric/log OTel, đã đúng cấu trúc từ 001 | Không đổi — 017 không sửa dòng C# nào |
| Backend nhận OTLP | `otel-collector` chỉ log ra `debug` | Thêm exporter `elasticsearch` (song song `debug`) |
| Hạ tầng Compose | Không có Elasticsearch/Kibana | `elasticsearch` + `kibana` trong `docker-compose.yml` mặc định |
| Version pin | `otel-collector:latest` | Pin cứng `0.160.0` |
| SRE tra cứu theo Correlation ID | Chỉ qua log `debug` của collector | Qua Kibana, thấy trace + metric + log của cả hành trình |
| Port công khai | 2 port (storefront, gateway — 004 SC-010) | Không đổi — Kibana không publish port ở `docker-compose.yml` |

**Kết luận:** bước 017 không thêm nghiệp vụ mua hàng, không sửa một dòng C# nào. Nó hiện thực hoá một
mục tiêu đã ngầm định từ bước 001 (comment gốc của `ServiceDefaultsExtensions.cs` đã ghi "forwards to
the Elastic stack" trước khi Elastic tồn tại trong repo): thêm Elasticsearch/Kibana làm backend thật
cho telemetry đã được mọi service phát ra sẵn, thuần bằng cấu hình Docker Compose và OTel Collector.

## 3. Shared project trong bước 017

Bước 017 không tạo, không sửa, và không cần bất kỳ `ProjectReference` mới nào tới
`ServiceDefaults`/`Tenancy`/`EventContracts`/`Identity`. Đây là bước duy nhất từ 001 đến nay (trừ 007/
009/010/013 vốn đã bị loại vì không có business/devops code) hoàn toàn không chạm shared C# project
nào — toàn bộ tác dụng đến từ việc thêm backend hạ tầng phía sau một cơ chế phát telemetry đã tồn tại
sẵn từ trước.
