# Contract: OTel Collector → Elasticsearch Export

Hợp đồng nội bộ giữa OTel Collector và cụm Elasticsearch/Kibana mới — không phải một API nghiệp vụ, mà là điểm nối hạ tầng mà mọi tính năng quan sát-được sau này (dashboard Kibana, cảnh báo, SLO) sẽ dựa vào. Trước tính năng này, collector chỉ có exporter `debug`; đây là hợp đồng cho exporter thứ hai mới, `elasticsearch` (research.md Decision 3, 4).

## Điểm nhận (Endpoint)

| | |
|---|---|
| Host | `elasticsearch` (tên service trong network `backbone` của Compose — không phải `localhost`) |
| Port | `9200` (REST API mặc định của Elasticsearch) |
| Giao thức | HTTP thuần, không TLS, không xác thực (research.md Decision 5 — cụm chỉ mang telemetry nội bộ, không phải CSDL nghiệp vụ) |
| Chế độ mapping | `otel` — dữ liệu OTLP được ghi theo cấu trúc OTel-native, không cần một APM Server trung gian giải mã lại (research.md Decision 3) |

## Producers

| Nguồn | Hành vi |
|---|---|
| Pipeline `traces` (`docker/otel-collector-config*.yaml`) | Nhận từ `receivers.otlp`, qua `processors.batch`, xuất tới CẢ `debug` LẪN `elasticsearch` (research.md Decision 4 — không thay thế `debug`) |
| Pipeline `metrics` | Tương tự — cùng cặp exporter |
| Pipeline `logs` | Tương tự — cùng cặp exporter; mỗi log record đã mang `service.name` (resource attribute), `CorrelationId`, và (khi đã resolve) `TenantId` từ trước khi tới collector (data-model.md — Định danh mang theo) |

## Consumers

| Nguồn | Hành vi |
|---|---|
| Kibana (service mới, cùng cụm) | Trỏ `ELASTICSEARCH_HOSTS=http://elasticsearch:9200`; đọc dữ liệu qua ứng dụng Observability (Discover/APM) để trả lời Success Criteria SC-001 (tìm trace đầy đủ của một đơn hàng) |
| `scripts/demo.ps1` | KHÔNG đổi — vẫn đọc `docker compose logs otel-collector` (exporter `debug`), không phụ thuộc vào exporter `elasticsearch` mới (research.md Decision 4) |
| Một người vận hành tra cứu thủ công | Truy vấn thẳng REST API Elasticsearch (`curl http://localhost:9200/...` qua cổng publish của `docker-compose.local.yml`) khi cần xác nhận dữ liệu đã tới mà chưa cần mở Kibana |

## Trước và sau tính năng này

| | Trước | Sau (tính năng này) |
|---|---|---|
| `exporters` khai báo trong config | `debug` | `debug`, `elasticsearch` |
| Nơi traces/metrics/logs "đi tới" ngoài log của chính collector | Không đâu cả | Elasticsearch (data stream tạo tự động theo chế độ mapping `otel`) |
| Cách xác nhận một hop đã phục vụ request | Regex trên `docker compose logs otel-collector` (vẫn còn, không đổi) | Thêm một cách thứ hai: truy vấn Kibana/Elasticsearch theo `correlation.id` |
| Service `elasticsearch`/`kibana` trong `docker-compose*.yml` | Không tồn tại | Mới — single-node, không xác thực, ghim phiên bản (research.md Decision 2, 5, 7) |

## Failure Modes

| Tình huống | Hành vi |
|---|---|
| Elasticsearch chưa sẵn sàng (đang khởi động) khi collector cố export | Collector tự retry theo hành vi mặc định của exporter — không rơi dữ liệu vĩnh viễn miễn Elasticsearch lên trước khi hàng đợi nội bộ của collector đầy; không có gì thay đổi ở phía service nghiệp vụ (chúng không biết, không phụ thuộc trực tiếp vào Elasticsearch) |
| Elasticsearch không lên được (ví dụ do `vm.max_map_count` trên host chưa đủ, research.md Decision 6) | `kibana` không `service_healthy` cho tới khi service đó lên được; các service nghiệp vụ khác KHÔNG bị chặn khởi động bởi Elasticsearch — chỉ `otel-collector` phụ thuộc `elasticsearch: condition: service_healthy` |
| Kibana không đọc được data stream mong đợi | Kiểm tra chế độ mapping của exporter (`mapping.mode: otel`) khớp phiên bản Elasticsearch/Kibana đã ghim — đây là lý do research.md Decision 7 yêu cầu ghim cùng một tag 8.x cho cả ba image (collector, Elasticsearch, Kibana) thay vì để trôi độc lập |

## Stability

Hợp đồng nội bộ giữa các thành phần hạ tầng trong cùng một deployment, không phải giao diện bên ngoài (constitution Principle II versioning không áp dụng trực tiếp). Tên data stream/chế độ mapping có thể đổi khi phiên bản `otel-collector-contrib`/Elasticsearch được nâng cấp trong tương lai — bất kỳ tính năng sau này dựng dashboard hay cảnh báo trên dữ liệu này nên xác nhận lại chế độ mapping đang dùng tại thời điểm đó thay vì giả định cố định.
