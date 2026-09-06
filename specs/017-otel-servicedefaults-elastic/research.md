# Research: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

**Ngày**: 2026-09-06 | **Spec**: [spec.md](spec.md)

Mục tiêu của Phase 0 là giải quyết mọi mục `NEEDS CLARIFICATION` trong Technical Context của `plan.md`. Phát hiện quan trọng nhất của giai đoạn này: hạ tầng OpenTelemetry đã tồn tại **gần như hoàn chỉnh** ở tầng mã nguồn — `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` đã wire traces/metrics/logs qua OTLP ở cả 7 service (gateway, bff, identity, parties, products, baskets, orders), `CorrelationIdMiddleware`/`Tenancy/TenantContextMiddleware` đã đưa correlation ID và tenant ID vào scope của mọi structured log, và rà soát toàn bộ `services/` không tìm thấy lời gọi log nào dùng chuỗi nội suy. Chính comment trong `docker/otel-collector-config.yaml` đã tự xác nhận: *"This is the collection point, not the backend. Elastic is the backend, and standing it up is Phase 3 work (SCRUM-25)."* — tính năng này build đúng phần còn thiếu đó: hạ tầng Elastic thật (Elasticsearch + Kibana) và đường ống OTLP → Elasticsearch, không phải viết lại phần code đã đúng.

Mỗi quyết định dưới đây rút ra từ việc đọc trực tiếp `shared/ServiceDefaults`, `shared/Tenancy`, `docker-compose.yml`, `docker-compose.local.yml`, `docker-compose.demo.yml`, `docker/otel-collector-config*.yaml`, `scripts/demo.ps1`, và specs của các tính năng liên quan (005, 006, 016).

## Decision 1 — Không sửa mã C# nào; ServiceDefaults đã đúng cấu trúc

**Decision**: Không thay đổi `shared/ServiceDefaults/ServiceDefaultsExtensions.cs`, `CorrelationIdMiddleware.cs`, `shared/Tenancy/TenantContextMiddleware.cs`, hay bất kỳ `Program.cs` nào của 7 service.

**Rationale**: Rà soát xác nhận cả ba Acceptance Criteria của Jira đã đúng ở tầng mã nguồn từ trước:

- **AC1** ("wire OTel via ServiceDefaults, không tự cấu hình riêng"): `ServiceDefaultsExtensions.AddServiceDefaults()`/`UseServiceDefaults()` đã được gọi giống hệt nhau ở cả 7 `Program.cs` (`grep -l "AddServiceDefaults\|UseServiceDefaults" services/*/src/*/Program.cs` → 7/7).
- **AC3** ("log có cấu trúc, mang service/tenant/correlation"): `grep -rE 'Log(Information|Warning|Error|Debug|Critical|Trace)\(\$"' services/` không trả về file nào — mọi lời gọi log đã dùng template có cấu trúc hoặc `[LoggerMessage]` source-generated (ví dụ `CheckoutEndpoints.cs`). `CorrelationIdMiddleware` đưa `CorrelationId` vào `BeginScope`; `TenantContextMiddleware` đưa `TenantId` vào `BeginScope` tương tự; `ConfigureResource(r => r.AddService(serviceName))` gắn `service.name` như một resource attribute của OTel — được exporter OTLP-log tự động đính kèm vào mọi log record, không cần code thêm ở từng service.
- **AC2** ("query Elastic thấy traces/metrics/logs cho mọi hop"): đây là mục **duy nhất** chưa đúng — `AddOtlpExporter()` ở cả traces/metrics/logs đã gửi dữ liệu tới collector, nhưng collector hôm nay chỉ có exporter `debug` (in ra log của chính nó), không có gì chuyển tiếp tới Elasticsearch.

Vì vậy phạm vi thật của SCRUM-25 là hạ tầng (Decision 2–7), không phải một tính năng cần viết code nghiệp vụ mới.

**Alternatives considered**: Viết lại `AddServiceDefaults()` để export "trực tiếp" tới Elasticsearch (bỏ qua collector) — bị loại ở Decision 3.

## Decision 2 — Elasticsearch + Kibana tham gia `docker-compose.yml` (stack mặc định), không phải một file riêng

**Decision**: Thêm hai service mới, `elasticsearch` và `kibana`, trực tiếp vào `docker-compose.yml` (project `ecomerce-stack`) — cùng file đang chứa `otel-collector` — và nhân bản đầy đủ hai service này vào `docker-compose.local.yml` (project `ecomerce-local`, tự chứa, không override).

**Rationale**: `otel-collector` — chính là service mà Elasticsearch phải đứng sau — đã sống trong `docker-compose.yml`, không phải `docker-compose.deps.yml` (file đó chỉ dành cho hạ tầng CSDL theo từng service, 005 research.md Decision 10). Mọi service nghiệp vụ đã unconditionally trỏ `OTEL_EXPORTER_OTLP_ENDPOINT` vào collector này mà không có cơ chế bật/tắt (không có Compose profile nào bọc quanh nó) — nên phần "backend" mà collector cần để chuyển tiếp dữ liệu tới cũng phải khởi động cùng lúc, trong cùng file, vì lý do constitution đã nêu cho "local development": chạy được bằng một lệnh duy nhất. `docker-compose.local.yml` tự nhận là "Self-contained: this file is not an override" — nó đã nhân bản toàn bộ định nghĩa `otel-collector` của riêng nó (dòng 185-192), nên Elasticsearch/Kibana cũng phải được nhân bản y hệt ở đó, không thể chỉ thêm một lần ở `docker-compose.yml` rồi trông cậy override.

**Alternatives considered**: Một file `docker-compose.observability.yml` riêng, optional — bị loại: mọi service đã phụ thuộc cứng vào collector từ trước (không có gate), nên tách Elastic thành một lớp "tuỳ chọn" sẽ để stack khởi động "xanh" trong khi telemetry không có nơi nào để tới — đúng lớp vấn đề nhiễu-log-xuất-lỗi mà Decision 10 của 005 đã sửa một lần cho chính collector.

## Decision 3 — Đường ống OTLP → Elasticsearch: dùng exporter `elasticsearch` có sẵn của `otel-collector-contrib`, không dựng thêm APM Server

**Decision**: Thêm exporter `elasticsearch` (`endpoints: [http://elasticsearch:9200]`, `mapping.mode: otel`) vào `docker/otel-collector-config.yaml` và `docker/otel-collector-config.demo.yaml`, gắn vào **cùng ba pipeline** (`traces`, `metrics`, `logs`) đang tồn tại. Không dựng Elastic APM Server.

**Rationale**: Image `otel/opentelemetry-collector-contrib` (đã dùng, không phải bản `core`) đã đóng gói sẵn component `elasticsearch` exporter — không cần thêm image/process nào khác. Elasticsearch bản 8.x đủ mới hỗ trợ nhận dữ liệu định dạng OTel-native trực tiếp qua exporter này (chế độ mapping `otel`), Kibana đọc thẳng từ đó qua ứng dụng Observability — không cần một APM Server làm khâu trung gian giải mã OTLP hộ collector, việc mà collector tự nó đã làm. Việc chọn component có sẵn trong image đang dùng, thay vì thêm một tiến trình mới, đúng tinh thần "ít mảnh ghép nhất có thể" mà `debug` exporter (component có sẵn khác của cùng image) đã thiết lập từ 005.

**Alternatives considered**: Elastic APM Server làm điểm nhận OTLP trung gian trước Elasticsearch — bị loại: thêm một tiến trình/port/healthcheck phải bảo trì mà không đổi hành vi cục bộ, vì bản thân nó cũng chỉ giải mã OTLP rồi ghi vào Elasticsearch — việc collector đã làm. Cho mỗi service tự export OTLP thẳng tới Elasticsearch, bỏ qua collector — bị loại: mất bộ đệm `batch` (giới hạn bộ nhớ, Decision 10 của 005) và buộc mỗi service phải tự biết địa chỉ/thông tin xác thực Elasticsearch — vi phạm đúng tinh thần "không cấu hình riêng lẻ theo từng service" của Principle VII, chỉ là ở tầng hạ tầng thay vì tầng code.

## Decision 4 — Giữ nguyên exporter `debug`, thêm `elasticsearch` làm exporter thứ hai trong cùng pipeline

**Decision**: Pipeline không đổi từ `exporters: [debug]` thành `exporters: [elasticsearch]`, mà thành `exporters: [debug, elasticsearch]` — cho cả ba pipeline, ở cả hai file cấu hình.

**Rationale**: `scripts/demo.ps1` (006-e2e-order-demo) đã có cơ chế đọc bằng chứng hop **đang chạy thật và được kiểm thử** dựa trên chính output văn bản của exporter `debug` — regex `ResourceTraces #\d+ service\.name=([A-Za-z0-9._-]+)` và `url\.path=(\S+)` áp lên `docker compose logs otel-collector`, dùng để khẳng định `$expectedHops` (Gateway.Api, Bff.Api, Products.Api, Baskets.Api, Orders.Api) đã thực sự phục vụ request trong lần demo đó (FR-011a của 006). Thay `debug` bằng `elasticsearch` sẽ âm thầm phá vỡ cơ chế này. OTel Collector's pipeline chấp nhận một danh sách exporter và fan-out cùng một dữ liệu tới tất cả — thêm `elasticsearch` cạnh `debug` cho tính năng này đúng giá trị mới (một người xem Kibana) mà không đụng tới cơ chế cũ (một script đọc log collector).

**Alternatives considered**: Thay hẳn `debug` bằng `elasticsearch` — bị loại vì lý do trên. Xoá `debug` khỏi config mặc định nhưng giữ ở config demo — bị loại vì hai file này tự mô tả là "Identical to otel-collector-config.yaml except for ONE line" (`docker/otel-collector-config.demo.yaml`), làm chúng khác nhau thêm một chỗ nữa là phá vỡ chính bất biến mà comment đó công bố.

## Decision 5 — Bảo mật/topology cục bộ: Elasticsearch single-node, tắt xpack security, không TLS

**Decision**: `discovery.type=single-node`, `xpack.security.enabled=false` cho cả Elasticsearch lẫn Kibana trong mọi file compose của tính năng này.

**Rationale**: Đúng tư thế cục bộ mà mọi datastore khác trong repo này đã chọn — SQL Server dùng `sa`/mật khẩu ở dạng rõ trong `.env` (gitignored), RabbitMQ dùng `guest`/`guest`, Redis không xác thực. Secret/TLS thật là mối quan tâm của cluster secret store khi triển khai Kubernetes (constitution Technology Constraints; ADR 0007 secrets-delivery) — nằm ngoài phạm vi một stack Docker Compose cho dev. Cụm Elastic này chỉ mang telemetry của chính nền tảng (không có dữ liệu khách hàng/tenant nào đi qua đây dưới dạng bản ghi nghiệp vụ), nên không chạm tới Principle V/VI theo cách một CSDL nghiệp vụ thật sẽ chạm.

**Alternatives considered**: Bật security mặc định — bị loại: thêm bước bootstrap mật khẩu/chứng chỉ vào ràng buộc "một lệnh duy nhất" của local dev mà không mang lại lợi ích cục bộ nào, và vẫn phải tắt lại cho môi trường CI/demo dùng-rồi-bỏ tương tự cách `docker-compose.ci.yml` đã tắt bootstrap check của SonarQube's bundled Elasticsearch (`SONAR_ES_BOOTSTRAP_CHECKS_DISABLE`).

## Decision 6 — `vm.max_map_count` trên Windows/Docker Desktop: ghi rõ như một điều kiện tiên quyết trong `quickstart.md`, không giải quyết bằng cấu hình

**Decision**: Không có flag nào trong `docker-compose.yml`/`docker-compose.local.yml` để bỏ qua kiểm tra này. `quickstart.md` ghi rõ lệnh khắc phục một lần: `wsl -d docker-desktop sysctl -w vm.max_map_count=262144`.

**Rationale**: Khác Elasticsearch nhúng sẵn trong image SonarQube (có biến `SONAR_ES_BOOTSTRAP_CHECKS_DISABLE` để bỏ qua đúng kiểm tra này — thấy trong `docker-compose.ci.yml`), Elasticsearch thật của Elastic không có lối tắt tương đương cho riêng kiểm tra `vm.max_map_count` — đây là bootstrap check bắt buộc từ Elasticsearch 5.x trở đi trên mọi nền tảng dùng `mmap` storage. Docker Desktop bản mới (WSL2 backend) thường đã đặt giá trị này đủ lớn sẵn, nhưng không phải luôn luôn — ghi rõ triệu chứng (Elasticsearch container thoát ngay với log "max virtual memory areas vm.max_map_count [...] is too low") và lệnh khắc phục để "một lệnh duy nhất" không âm thầm thất bại theo cách khó chẩn đoán.

**Alternatives considered**: Không có — đây là điều kiện tiên quyết thật của hạ tầng, không phải một lựa chọn thiết kế có phương án thay thế ở tầng code/compose.

## Decision 7 — Ghim phiên bản image thay vì `latest`, cho cả collector lẫn Elastic/Kibana

**Decision**: Đổi `otel-collector` từ `otel/opentelemetry-collector-contrib:latest` sang một tag cụ thể; `elasticsearch`/`kibana` dùng cùng một tag 8.x cụ thể cho cả hai (tối thiểu bản đã hỗ trợ ổn định `mapping.mode: otel` của elasticsearch exporter). Số hiệu phiên bản chính xác được chốt ở bước triển khai (`/speckit-tasks`/`/speckit-implement`), bằng cách đối chiếu release hiện hành tại thời điểm đó — tài liệu kế hoạch này cố tình không đóng đinh một con số có thể đã lỗi thời khi triển khai.

**Rationale**: Bề mặt cấu hình của exporter `elasticsearch` (tên field, chế độ mapping, tên data stream mặc định) đã đổi qua nhiều bản phát hành của `opentelemetry-collector-contrib`; để `latest` nghĩa là file cấu hình viết hôm nay có thể lặng lẽ hỏng ở lần `docker compose pull` sau. Việc ghim phiên bản NuGet ở `Directory.Packages.props` (`OpenTelemetry.*` đều ghim `1.17.0`, không thả nổi) đã là tiền lệ của chính repo này cho đúng loại rủi ro này — áp dụng lại cho image, đúng vào chỗ tính năng này vừa thêm.

**Alternatives considered**: Giữ `latest` — bị loại vì không nhất quán với cách mọi dependency đã ghim khác trong nền tảng, và rủi ro cụ thể nhất lại rơi đúng vào exporter mà tính năng này mới thêm.

## Decision 8 — "Gỡ ServiceDefaults khỏi một service" (Jira Test Scenario 3) là một bước quickstart thủ công, không phải test tự động mới

**Decision**: `quickstart.md` mô tả các bước thủ công (comment tạm `AddServiceDefaults()`/`UseServiceDefaults()` ở `Program.cs` của một service, rebuild, quan sát Kibana) — không thêm test tích hợp mới nào giả lập việc này.

**Rationale**: Kịch bản này về bản chất là tắt khả năng quan sát của một service — biến nó thành một assertion tự động nghĩa là phải giữ vĩnh viễn một nhánh mã có observability bị vô hiệu hoá, ngược đúng với điều Principle VII yêu cầu. Đây là một phép thử "phải" (sanity check) một lần để chứng minh thành phần dùng chung thực sự chịu tải — phù hợp là một bước runbook mà SRE/reviewer tự tay chạy, không phải một dòng trong bộ hồi quy tự động.

**Alternatives considered**: Một integration test dùng reflection để gỡ `AddServiceDefaults()` lúc runtime rồi assert mất telemetry — bị loại vì lễ nghi không tương xứng với một phép chứng minh một-lần, và test một nhánh mã không bao giờ tồn tại ở bất kỳ triển khai thật nào.
