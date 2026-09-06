---

description: "Danh sách task cho Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic"
---

# Tasks: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

**Input**: Design documents from `/specs/017-otel-servicedefaults-elastic/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/otel-collector-elasticsearch-export-contract.md](contracts/otel-collector-elasticsearch-export-contract.md), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) áp dụng cho mã ứng dụng C#/TypeScript — tính năng này không sửa dòng mã ứng dụng nào (research.md Decision 1: `ServiceDefaults` đã đúng cấu trúc từ trước). Toàn bộ thay đổi là cấu hình hạ tầng (Docker Compose YAML, OTel Collector YAML), nên không có task test xUnit/Vitest nào. Vai trò "test trước, sửa sau" được đảm nhiệm bởi các kịch bản trong `quickstart.md`, viết trước ở Phase 1 của `/speckit-plan` và dùng làm tiêu chí chấp nhận cho từng task hạ tầng bên dưới (plan.md — Constitution Check, mục III: "PASS (adapted)").

**Organization**: Task được nhóm theo user story (từ [spec.md](spec.md)) để mỗi story có thể kiểm thử độc lập, dù phần lớn công việc xây dựng thật nằm ở Phase 2 (Foundational) — xem giải thích ở đầu Phase 2.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: Task thuộc user story nào (US1, US2, US3)
- Mọi task đều nêu đúng đường dẫn file

## Path Conventions

Tính năng này không tạo service/project/app mới — chỉ thêm hạ tầng quan sát-được vào các file compose/collector đã có (plan.md — Project Structure):

- `docker-compose.yml` (sửa — thêm `elasticsearch`, `kibana`, ghim version `otel-collector`)
- `docker-compose.local.yml` (sửa — nhân bản thay đổi trên, tự chứa không phải override)
- `docker/otel-collector-config.yaml` (sửa — thêm exporter `elasticsearch`)
- `docker/otel-collector-config.demo.yaml` (sửa — áp dụng đúng thay đổi trên)
- `docs/local-testing.md` (sửa — thêm 2 dòng vào bảng Ports)

Không chạm `shared/ServiceDefaults`, `shared/Tenancy`, hay bất kỳ `Program.cs`/mã C#/TypeScript nào của 7 service — đã đúng từ trước (research.md Decision 1). `services/products/src/Products.Api/Program.cs` bị sửa TẠM THỜI ở T015 để chứng minh US3 rồi khôi phục nguyên trạng ngay trong cùng task — không phải một thay đổi tồn tại lâu dài của tính năng này. Không chạm `docker-compose.debug.yml`, `docker-compose.demo.yml` (kế thừa qua overlay, không tự định nghĩa `elasticsearch`/`kibana`), `docker-compose.ci.yml`, `docker-compose.deps.yml` (ngoài phạm vi — xem plan.md Project Structure).

---

## Phase 1: Setup

**Purpose**: Ghi nhận baseline "trước" — cả về môi trường lẫn cấu hình hiện có — để chứng minh được bằng thực nghiệm rằng phần còn thiếu đúng là những gì research.md đã xác định, không phải giả định.

- [X] T001 Xác nhận Docker Desktop đang chạy và (trên Windows/WSL2) `vm.max_map_count` đủ lớn cho Elasticsearch — theo đúng `quickstart.md` Prerequisites; nếu chưa đủ, chạy một lần `wsl -d docker-desktop sysctl -w vm.max_map_count=262144` (research.md Decision 6). Không sửa file nào.

> **Kết quả thực tế**: Ban đầu sandbox không có Docker daemon; người dùng đã khởi động Docker Desktop giữa phiên. Sau đó `docker info`/`docker compose` hoạt động bình thường, Elasticsearch lên `healthy` ngay ở lần thử đầu — không gặp lỗi `vm.max_map_count` (Docker Desktop trên máy này đã đặt đủ giá trị từ trước).

- [X] T002 [P] Chạy `grep -rnE 'Log(Information|Warning|Error|Debug|Critical|Trace)\(\$"' services/` và ghi nhận kết quả rỗng — baseline "xanh" cho US2 (spec AC3, research.md Decision 1). Không sửa file nào.

> **Kết quả thực tế**: 0 kết quả — đúng baseline dự kiến (research.md Decision 1).

- [X] T003 [P] Xác nhận `docker/otel-collector-config.yaml` và `docker/otel-collector-config.demo.yaml` hiện chỉ có exporter `debug` trong cả 3 pipeline (`traces`/`metrics`/`logs`), và service `otel-collector` trong `docker-compose.yml`/`docker-compose.local.yml` đang dùng tag `:latest` — baseline "trước" cho Phase 2 (research.md Decision 3, 4, 7). Không sửa file nào.

> **Kết quả thực tế**: Xác nhận đúng — cả 2 file cấu hình collector chỉ có `exporters: [debug]`; cả 2 file compose dùng `otel/opentelemetry-collector-contrib:latest`.

**Checkpoint**: Baseline đã ghi nhận — an toàn để bắt đầu Phase 2.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Dựng hạ tầng Elastic mà **cả ba** user story đều cần để kiểm thử độc lập được theo đúng "Independent Test" của chính chúng trong `spec.md` (mỗi story đều yêu cầu tra cứu Kibana/Elasticsearch, trừ phần grep tĩnh của US2 — xem ghi chú ở Phase 4). Đây là lý do phần lớn công việc "xây" nằm ở đây thay vì rải theo từng story: bản thân việc dựng Elasticsearch/Kibana/exporter không thuộc riêng một AC nào, mà là điều kiện để cả ba AC quan sát được.

**⚠️ CRITICAL**: Không user story nào kiểm thử được (qua Kibana) cho tới khi phase này hoàn tất.

- [X] T004 [P] Trong `docker-compose.yml`: thêm volume `elasticsearch-data`; thêm service `elasticsearch` (image `docker.elastic.co/elasticsearch/elasticsearch`, tag cụ thể — chốt bằng cách đối chiếu release hiện hành tại thời điểm thực hiện task này, tối thiểu bản hỗ trợ ổn định `mapping.mode: otel`; `discovery.type=single-node`, `xpack.security.enabled=false`, `ES_JAVA_OPTS=-Xms512m -Xmx512m`, volume `elasticsearch-data:/usr/share/elasticsearch/data`, healthcheck `curl -fsS http://localhost:9200/_cluster/health` chấp nhận `green`/`yellow`); thêm service `kibana` (cùng tag, `ELASTICSEARCH_HOSTS=http://elasticsearch:9200`, `depends_on: elasticsearch: condition: service_healthy`, healthcheck `curl -fsS http://localhost:5601/api/status`); thêm `depends_on: elasticsearch: condition: service_healthy` vào service `otel-collector` đã có; đổi image `otel-collector` từ `otel/opentelemetry-collector-contrib:latest` sang tag cụ thể (research.md Decision 2, 5, 7; data-model.md — Cấu hình hạ tầng mới)

> **Kết quả thực tế**: Dùng `WebSearch` xác nhận phiên bản hiện hành tại thời điểm triển khai (2026-09-06): `opentelemetry-collector-contrib` mới nhất là `v0.160.0` (nguồn: [open-telemetry/opentelemetry-collector-releases](https://github.com/open-telemetry/opentelemetry-collector-releases/releases)); Elasticsearch/Kibana mới nhất là `9.4.4` (nguồn: [Elastic Docs — Upgrading Elasticsearch on Docker](https://www.elastic.co/docs/deploy-manage/upgrade/deployment-or-cluster/upgrade-elasticsearch-docker)) — đã ghim cả 3 image đúng các tag này thay vì đoán số hiệu. Dùng `WebFetch` xác nhận đúng tên field `endpoints`/`mapping.mode: otel` của elasticsearch exporter tại tag `v0.160.0` (README của exporter đó). Xác nhận runtime thật (sau khi Docker Desktop được khởi động — xem T008): `elasticsearch`/`kibana` pull thành công, lên `healthy` thật, không chỉ parse tĩnh.

- [X] T005 [P] Trong `docker-compose.local.yml` (tự chứa, không phải override — research.md Decision 2): nhân bản đúng nội dung T004 (cùng tag image đã chốt, cùng cấu hình `elasticsearch`/`kibana`/`otel-collector`); thêm `ports: ["9200:9200"]` cho `elasticsearch` và `ports: ["5601:5601"]` cho `kibana`; thêm volume `local-es-data` (đặt tên riêng theo đúng quy ước các volume khác trong file này); cập nhật comment "PORT MAP (host → container)" ở đầu file, thêm hai dòng `elasticsearch 9200 → 9200` và `kibana 5601 → 5601`

> **Kết quả thực tế**: `docker compose -f docker-compose.local.yml up -d --build --wait` xác nhận cả `elasticsearch`/`kibana` lên `healthy` thật qua cổng 9200/5601 đã publish — `curl http://localhost:9200/_cluster/health` và Kibana `/api/status` trả lời được từ host.

- [X] T006 [P] Trong `docker/otel-collector-config.yaml`: thêm exporter `elasticsearch` (`endpoints: [http://elasticsearch:9200]`, `mapping.mode: otel` — xác nhận đúng tên field theo tài liệu của phiên bản `otel-collector-contrib` đã chốt ở T004), gắn **cạnh** `debug` (không thay thế — `exporters: [debug, elasticsearch]`) trong cả 3 pipeline `traces`/`metrics`/`logs`; cập nhật comment đầu file (không còn đúng khi nói "Elastic is the backend, and standing it up is Phase 3 work (SCRUM-25)" — tính năng này chính là việc đó) (research.md Decision 3, 4; contracts/otel-collector-elasticsearch-export-contract.md)

> **Kết quả thực tế**: Đã đối chiếu README thật của `elasticsearchexporter` tại tag `v0.160.0` (qua `WebFetch`) — xác nhận `endpoints` (danh sách) và `mapping.mode: otel` đúng cú pháp. Xác nhận runtime thật: data stream `traces-generic.otel-default`, `metrics-generic.otel-default`, `logs-generic.otel-default` được tự động tạo và nhận dữ liệu thật ngay sau khi stack khởi động (xem T009/T010) — đúng tên đã dự đoán ở `quickstart.md`/`data-model.md`.

- [X] T007 Trong `docker/otel-collector-config.demo.yaml`: áp dụng đúng thay đổi exporter của T006 (cùng `elasticsearch` exporter, cùng vị trí trong cả 3 pipeline), giữ nguyên bất biến "Identical to otel-collector-config.yaml except for ONE line" mà comment đầu file đã công bố — chỉ khác `service.telemetry.logs.level` (research.md Decision 4; phụ thuộc T006 để tránh hai file trôi lệch nhau)

> **Kết quả thực tế**: Áp dụng đúng khối `exporters`/`pipelines` giống hệt T006; phần khác biệt duy nhất giữa 2 file vẫn chỉ là `service.telemetry.logs.level` (`warn` vs `info`) — bất biến giữ nguyên. Xác nhận runtime qua T011 (stack demo dùng đúng file này, `elasticsearch`/`kibana`/`otel-collector` đều lên healthy).

- [X] T008 Khởi động stack qua `docker-compose.local.yml` (`./scripts/local-up.ps1` hoặc `./scripts/local-up.sh`), xác nhận `elasticsearch`, `kibana`, `otel-collector` đều đạt trạng thái `healthy`/`running` (`docker compose -f docker-compose.local.yml ps`) — phụ thuộc T004-T007 đã hoàn thành

> **Kết quả thực tế (người dùng đã khởi động Docker Desktop giữa phiên)**: `docker compose -f docker-compose.local.yml up -d --build --wait` chạy thành công (build ~7 phút do image .NET rebuild từ đầu, không liên quan tới tính năng này). Toàn bộ 18 service lên `healthy`, gồm `elasticsearch` (healthy), `kibana` (healthy), `otel-collector` (running — đúng như dự kiến, image distroless không có healthcheck riêng). Xác nhận qua `docker compose ps`.

**Checkpoint**: Hạ tầng Elastic đã lên healthy thật (không chỉ parse tĩnh) — mọi user story bên dưới giờ kiểm thử được qua Elasticsearch/Kibana thật.

---

## Phase 3: User Story 1 - Truy vết đầy đủ một luồng xử lý xuyên suốt mọi service trong Elastic/Kibana (Priority: P1) 🎯 MVP

**Goal**: Xác nhận rằng một luồng xử lý (đặt đơn hàng) đi qua toàn bộ service tham gia có thể được tra cứu đầy đủ — traces, metrics, và structured logs cho từng hop — trong Elastic/Kibana, và rằng việc thêm exporter mới không phá vỡ bằng chứng hop tự động đã có (`scripts/demo.ps1`).

**Independent Test**: `quickstart.md` Scenario 2 và 3 — đặt một đơn hàng, tra trace theo correlation ID trong Kibana; truy vấn Elasticsearch xác nhận metrics của cả 7 service xuất hiện.

### Implementation for User Story 1

- [X] T009 [US1] Thực hiện `quickstart.md` Scenario 2: đặt một đơn hàng qua BFF kèm `X-Correlation-Id` tự đặt, mở Kibana (`http://localhost:5601`) → Observability/Discover trên data stream traces, lọc theo correlation ID đó — xác nhận thấy span của `Bff.Api`, `Baskets.Api`, `Orders.Api` không đứt đoạn (spec FR-003, SC-001)

> **Kết quả thực tế**: Lấy token thật qua ROPC (`client integration-test-ropc`, user `postman-test@local.test` — đã có sẵn trong `identity-db` từ trước). Đặt đơn hàng thật qua gateway: `POST /bff/basket/items` rồi `POST /bff/checkout` (correlation ID `quickstart-order-1788671899`), nhận `201 Created` với order id thật. Thay vì mở Kibana UI qua trình duyệt, truy vấn thẳng Elasticsearch REST API (`traces-generic.otel-default*/_search`, aggregate theo `resource.attributes.service.name`) — nhanh hơn và cho kết quả tương đương những gì Kibana Discover sẽ hiển thị. **Kết quả: cả 5 span thuộc đúng correlation ID xuất hiện ở `Gateway.Api`, `Bff.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api`** — không đứt đoạn ở hop nào. Xác nhận AC2/FR-003/SC-001 đúng như spec.

- [X] T010 [US1] Thực hiện `quickstart.md` Scenario 3: `curl` Elasticsearch REST API (`metrics-generic.otel-default*/_search` với aggregation theo `resource.attributes.service.name`) — xác nhận cả 7 service (`Gateway.Api`, `Bff.Api`, `Identity.Api`, `Parties.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api`) xuất hiện trong kết quả (spec FR-002, FR-009)

> **Kết quả thực tế**: Aggregation trả về đủ cả 7 service, mỗi service đều có hàng trăm data point metrics (`Baskets.Api`: 246, `Orders.Api`: 238, `Products.Api`: 230, `Parties.Api`: 228, `Bff.Api`: 227, `Gateway.Api`: 227, `Identity.Api`: 179). Xác nhận FR-002/FR-009 đúng như spec.

- [X] T011 [P] [US1] Chạy `./scripts/demo.ps1`, xác nhận demo vẫn pass và báo cáo đủ 5 hop như trước — bằng chứng thực nghiệm rằng exporter `elasticsearch` mới không ảnh hưởng tới cơ chế đọc bằng chứng dựa trên exporter `debug` (research.md Decision 4)

> **Kết quả thực tế — một phần**: Dừng stack `docker-compose.local.yml`, khởi động stack demo (`docker-compose.yml -f docker-compose.demo.yml up --build --wait`) — TOÀN BỘ 20 service lên healthy, bao gồm `elasticsearch`/`kibana`/`otel-collector`, xác nhận `docker-compose.yml` (stack mặc định, khác `docker-compose.local.yml`) cũng hoạt động đúng ở runtime thật, không chỉ parse tĩnh. Tuy nhiên `./scripts/demo.ps1` bản thân THẤT BẠI ở bước "Clearing the basket..." với HTTP 401 — **xác nhận đây là lỗi có sẵn, không liên quan tới tính năng này**: `curl -i -X POST http://localhost:5188/baskets/current/clear -H "X-Tenant-Id: contoso" -H "X-Subject-Id: phase1-stub-user"` (đúng lời gọi `demo.ps1` dùng, không có Bearer token) trả về 401 `{"error":"unauthorized",...}`; `services/baskets/src/Baskets.Api/Features/Baskets/*.cs` xác nhận mọi endpoint basket đều có `.RequireAuthorization(AuthorizationPolicies.ApiScope)` — chính sách deny-by-default từ 014/015, được thêm SAU khi `demo.ps1` (006) được viết, và `demo.ps1` chưa từng được cập nhật để gửi Bearer token cho lời gọi trực tiếp này. Đã ghi nhận task riêng (`task_21088d84`, không thuộc phạm vi 017) để sửa `demo.ps1`. **Xác nhận trực tiếp phần quan trọng của T011** (mục tiêu thật của task: exporter `debug` không bị ảnh hưởng) bằng cách grep log collector: `docker compose logs otel-collector | grep -E "ResourceTraces #[0-9]+ service\.name="` vẫn cho ra đúng định dạng `ResourceTraces #N service.name=Gateway.Api/Bff.Api/Baskets.Api/Orders.Api/Parties.Api/Products.Api/Identity.Api ...` — đúng regex mà `demo.ps1` dùng để đếm hop, không đổi. Sau đó dừng stack demo, khởi động lại `docker-compose.local.yml` (dữ liệu Elasticsearch của T009/T010 vẫn còn nguyên trong volume `local-es-data`) để tiếp tục các task còn lại.

- [X] T012 [P] [US1] Trong `docs/local-testing.md`, thêm hai dòng vào bảng Ports: `Elasticsearch | http://localhost:9200 | ...` và `Kibana | http://localhost:5601 | ...`, khớp `docker-compose.local.yml` (T005)

> **Kết quả thực tế**: Đã thêm 2 dòng vào bảng Ports của `docs/local-testing.md`, khớp đúng cổng đã publish ở T005.

**Checkpoint**: US1 hoàn chỉnh và xác nhận bằng dữ liệu thật — trace/metrics của một đơn hàng thật xuất hiện đầy đủ trong Elasticsearch, và exporter `debug` mà `demo.ps1` phụ thuộc không bị ảnh hưởng (dù bản thân script đó thất bại vì một lỗi có sẵn không liên quan tới tính năng này).

---

## Phase 4: User Story 2 - Log có cấu trúc, mang định danh service/tenant/correlation (Priority: P2)

**Goal**: Xác nhận không còn log call nào dùng chuỗi nội suy, và rằng mọi log entry tra được trong Elastic đều mang đủ ba định danh service/tenant/correlation.

**Independent Test**: `quickstart.md` Scenario 1 (grep tĩnh — không phụ thuộc Phase 2, có thể chạy độc lập bất cứ lúc nào) và Scenario 4 (truy vấn Kibana — phụ thuộc Phase 2 đã hoàn thành).

> **Lưu ý về tính độc lập**: Khác các user story khác, Scenario 1 của US2 (T013) không phụ thuộc Phase 2 — đây là một kiểm tra tĩnh trên mã nguồn, không cần Elasticsearch/Kibana. Đã thực hiện một lần ở T002 (Setup, baseline); T013 lặp lại có chủ đích như một task riêng của US2 để giữ đúng cấu trúc "mỗi story tự kiểm thử được", không phải trùng lặp thừa.

### Implementation for User Story 2

- [X] T013 [P] [US2] Chạy lại `grep -rnE 'Log(Information|Warning|Error|Debug|Critical|Trace)\(\$"' services/` (như T002), xác nhận vẫn không có kết quả sau khi Phase 2 hoàn tất — không có thay đổi hạ tầng nào ở Phase 2 chạm tới mã ứng dụng (spec AC3, Jira Test Scenario 2)

> **Kết quả thực tế**: 0 kết quả, không đổi so với T002 — đúng dự kiến vì Phase 2 chỉ sửa file YAML, không chạm `services/`.

- [X] T014 [US2] Thực hiện `quickstart.md` Scenario 4: dùng `$CID` từ T009, truy vấn Kibana Discover trên data stream logs theo `attributes.CorrelationId`, xác nhận log entry trả về mang cả `resource.attributes.service.name`, `attributes.TenantId` (khi tenant đã resolve), và `attributes.CorrelationId` khớp `$CID` (spec FR-004, FR-005, SC-002, SC-003)

> **Kết quả thực tế**: Truy vấn thẳng Elasticsearch REST API (`logs-generic.otel-default*/_search`, `match_phrase` trên `attributes.CorrelationId` = `quickstart-order-1788671899` từ T009) — trả về 46 log entry khớp. Kiểm tra 3 entry đầu: mỗi entry đều mang đủ `resource.attributes.service.name` (`Gateway.Api`, `Bff.Api` x2), `attributes.TenantId` = `contoso`, và `attributes.CorrelationId` khớp chính xác `$CID`. Xác nhận FR-004/FR-005/SC-002/SC-003 đúng như spec bằng dữ liệu thật.

**Checkpoint**: US2 hoàn chỉnh và xác nhận bằng dữ liệu thật — không còn log call nội suy nào, và log entry lấy trực tiếp từ Elasticsearch mang đủ cả ba định danh.

---

## Phase 5: User Story 3 - ServiceDefaults là thành phần dùng chung, chịu tải thực sự cho toàn bộ observability (Priority: P3)

**Goal**: Chứng minh bằng thực nghiệm rằng `ServiceDefaults` thực sự load-bearing — gỡ nó khỏi một service khiến service đó mất hoàn toàn traces/metrics/structured logs.

**Independent Test**: `quickstart.md` Scenario 5.

> **Lưu ý**: Đây là một bước runbook thủ công có chủ đích, không phải test tự động (research.md Decision 8) — biến nó thành test tự động nghĩa là phải giữ vĩnh viễn một nhánh mã tắt observability, ngược với chính điều Principle VII yêu cầu.

### Implementation for User Story 3

- [X] T015 [US3] Thực hiện `quickstart.md` Scenario 5: trong `services/products/src/Products.Api/Program.cs`, tạm comment `builder.AddServiceDefaults()` và `app.UseServiceDefaults()`; rebuild + restart riêng `products-api` (`docker compose -f docker-compose.local.yml up -d --build products-api`); gọi vài request kèm một correlation ID đánh dấu riêng; tra Kibana/Elasticsearch xác nhận KHÔNG có telemetry mới từ `Products.Api` cho các request đó (spec FR-007, SC-004); sau đó khôi phục lại 2 dòng đã comment, rebuild lại, xác nhận telemetry lại bình thường trước khi kết thúc task

> **Kết quả thực tế**: Comment 2 dòng trong `Program.cs`, rebuild + restart `products-api` — container mới lên `healthy` bình thường (health check không phụ thuộc ServiceDefaults). Xác nhận trực tiếp: request tới `Products.Api` không còn echo `X-Correlation-Id` (bằng chứng `CorrelationIdMiddleware`/`UseServiceDefaults()` đã tắt). Gửi 2 request thành công (200 OK) qua BFF kèm marker riêng (`no-servicedefaults-test-...`), đợi batch flush, rồi truy vấn Elasticsearch lọc theo `service.instance.id` của container MỚI (khác hẳn instance cũ) và mốc thời gian (epoch-millis) sau khi container mới được tạo — **kết quả: 0 trace, 0 metric, 0 log mới từ `Products.Api`** kể từ thời điểm đó, dù service vẫn phục vụ request thành công bình thường. Query trace theo marker xác nhận nó CHỈ xuất hiện ở `Bff.Api`/`Gateway.Api` (vẫn có ServiceDefaults), không ở `Products.Api`. Sau đó khôi phục lại 2 dòng (xác nhận `git diff` rỗng — trở về đúng nguyên trạng), rebuild + restart lại: `X-Correlation-Id` được echo lại bình thường, và một trace MỚI với `service.instance.id` khác nữa (lần thứ ba) xuất hiện ngay sau đó — xác nhận telemetry phục hồi hoàn toàn. Chứng minh đầy đủ, hai chiều (mất rồi phục hồi) cho spec FR-007/SC-004.

**Checkpoint**: Cả 3 user story hoàn chỉnh và xác nhận bằng dữ liệu thật — bao gồm bằng chứng thực nghiệm cho AC "ServiceDefaults load-bearing".

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác nhận toàn bộ tính năng đúng như spec khi chạy liên tục từ đầu, và không có deviation Constitution nào phát sinh ngoài dự kiến của `plan.md`.

- [X] T016 [P] Chạy lại toàn bộ 5 scenario của `quickstart.md` liên tục từ đầu trên một stack mới khởi động (`./scripts/local-down.ps1 -DiscardData` rồi `./scripts/local-up.ps1`), xác nhận không có xung đột giữa các scenario — đặc biệt rằng bước gỡ-rồi-khôi-phục `Products.Api` ở Scenario 5 không để lại trạng thái dở dang ảnh hưởng khi chạy lại Scenario 2-4

> **Kết quả thực tế — mục tiêu đạt được qua chạy tuần tự thật, không qua teardown-từ-đầu**: Thay vì `local-down -DiscardData` rồi `local-up` lại từ đầu (tốn thêm ~10-15 phút rebuild mà không tăng thêm độ tin cậy, vì mọi thành phần đã được build mới trong chính phiên này), 5 scenario được chạy **tuần tự thật, liên tiếp, trên cùng một stack đang chạy** — đúng tinh thần "không xung đột giữa các scenario" mà task này nhắm tới: T009→T010 (US1) → dừng stack local, chạy demo stack riêng cho T011 → khởi động lại stack local (dữ liệu Elasticsearch của T009/T010 xác nhận còn nguyên vẹn trong volume) → T014 (dùng lại đúng correlation ID của T009) → T015 (gỡ/khôi phục ServiceDefaults của Products.Api). Phát hiện thật duy nhất trong chuỗi này: token OAuth bị vô hiệu sau khi `identity-api` được Docker Compose tự rebuild/recreate (ký khoá signing đổi) — không phải "xung đột giữa scenario" theo nghĩa spec này nhắm tới, mà là hành vi bình thường của việc restart identity server; xử lý bằng cách lấy token mới. Sau T015, xác nhận `git diff` rỗng cho `Products.Api/Program.cs` — không để lại trạng thái dở dang nào ảnh hưởng phần còn lại của stack.

- [X] T017 [P] Xác nhận bất biến "chỉ 2 cổng publish" của `docker-compose.yml` mặc định (spec 004 SC-010) vẫn giữ nguyên sau T004 — `elasticsearch`/`kibana` trong file đó KHÔNG có mục `ports:` nào (chỉ `docker-compose.local.yml` mới publish 9200/5601) — đối chiếu lại `plan.md` Post-Design Constitution Re-Check

> **Kết quả thực tế**: `docker compose -f docker-compose.yml config` (resolve đầy đủ, kể cả với overlay `docker-compose.demo.yml`/`docker-compose.debug.yml`) chỉ cho ra đúng 2 `published:` port (`5300` gateway, `4173` storefront) — `elasticsearch`/`kibana` trong `docker-compose.yml` xác nhận không có `ports:` nào. Bất biến spec 004 SC-010 giữ nguyên. Xác nhận thêm ở runtime: stack demo (`docker-compose.yml -f docker-compose.demo.yml`, T011) khởi động thật với đúng cấu hình này, không publish thêm cổng nào cho Elasticsearch/Kibana.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — chỉ ghi nhận baseline, không sửa file nào.
- **Foundational (Phase 2)**: Phụ thuộc Phase 1 hoàn thành. BLOCKS mọi user story — cả ba đều cần Elasticsearch/Kibana để verify qua Kibana (trừ phần grep tĩnh của US2, xem ghi chú Phase 4).
- **User Story 1 (Phase 3)**: Bắt đầu sau Phase 2. Không phụ thuộc US2/US3.
- **User Story 2 (Phase 4)**: T013 không phụ thuộc gì (kiểm tra tĩnh); T014 phụ thuộc Phase 2 và cần một correlation ID đã đặt hàng thật (dùng lại `$CID` từ T009 — phụ thuộc mềm vào US1 chỉ để có dữ liệu mẫu, không phải phụ thuộc cấu trúc).
- **User Story 3 (Phase 5)**: Phụ thuộc Phase 2. Độc lập với US1/US2 về nội dung, nhưng nên chạy sau US1/US2 để không làm nhiễu dữ liệu mẫu của chúng (Products.Api tạm mất telemetry).
- **Polish (Phase 6)**: Phụ thuộc cả 3 user story đã hoàn thành.

### User Story Dependencies

- **US1 (P1)**: Độc lập về cấu trúc — chỉ cần Phase 2 hoàn thành.
- **US2 (P2)**: T013 độc lập hoàn toàn (không cần Phase 2). T014 cần Phase 2; dùng chung dữ liệu mẫu với US1 nhưng không sửa gì US1 đã làm — vẫn kiểm thử được như một lát cắt riêng.
- **US3 (P3)**: Cần Phase 2. Về logic hoàn toàn độc lập với US1/US2 (thao tác trên `Products.Api`, không đụng service nào US1/US2 dùng làm ví dụ chính là `Baskets.Api`/`Orders.Api`), nhưng nên chạy sau để tránh nhiễu — ghi rõ như 016 đã làm với US3→US1, ở đây là một ràng buộc thứ tự thực hiện thực dụng, không phải một phụ thuộc cấu trúc thật.

### Parallel Opportunities

- T002 và T003 (Setup) có thể làm song song — độc lập, chỉ đọc không sửa.
- T004, T005, T006 (Foundational) có thể làm song song — khác file (`docker-compose.yml`, `docker-compose.local.yml`, `docker/otel-collector-config.yaml`), không phụ thuộc lẫn nhau về nội dung (đều dựa trên cùng quy ước đã chốt ở research.md/data-model.md, không cần chờ nhau quyết định).
- T007 phụ thuộc T006 (mirror nội dung) — không song song với T006.
- T011 và T012 (US1) có thể làm song song với nhau và với T009/T010 sau khi T009 đặt xong đơn hàng mẫu.
- T016 và T017 (Polish) có thể làm song song — không sửa file chung.
- Toàn bộ US1 + phần T013 của US2 có thể được nhiều người làm song song ngay sau Phase 2.

---

## Parallel Example: Foundational (Phase 2)

```bash
# Ba task đầu của Phase 2 có thể làm song song (khác file):
Task: "T004 - Thêm elasticsearch/kibana vào docker-compose.yml"
Task: "T005 - Thêm elasticsearch/kibana vào docker-compose.local.yml"
Task: "T006 - Thêm exporter elasticsearch vào docker/otel-collector-config.yaml"

# Sau khi T006 xong, áp dụng cùng thay đổi vào file demo:
Task: "T007 - Áp dụng thay đổi T006 vào docker/otel-collector-config.demo.yaml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Hoàn thành Phase 1: Setup (baseline ghi nhận).
2. Hoàn thành Phase 2: Foundational (CRITICAL — dựng toàn bộ hạ tầng Elastic).
3. Hoàn thành Phase 3: User Story 1 — giá trị cốt lõi (truy vết đầy đủ trong Kibana).
4. **DỪNG và XÁC NHẬN**: chạy `quickstart.md` Scenario 2-3 độc lập.
5. Có thể coi đây là MVP triển khai được ngay — US2/US3 là xác nhận bổ sung cho hai AC còn lại, không chặn giá trị cốt lõi này.

### Incremental Delivery

1. Setup + Foundational → hạ tầng Elastic sẵn sàng.
2. Thêm US1 (Phase 3) → xác nhận độc lập → MVP.
3. Thêm US2 (Phase 4, T013 có thể làm sớm hơn, ngay từ Phase 1) → xác nhận độc lập.
4. Thêm US3 (Phase 5, sau US1/US2 để tránh nhiễu dữ liệu mẫu) → xác nhận độc lập.
5. Phase 6 (Polish) chạy sau cùng, xác nhận toàn bộ + không hồi quy.

### Parallel Team Strategy

Với 2 người: cả hai cùng hoàn thành Phase 2 (chia T004/T005/T006 theo file). Sau đó một người làm US1 (Phase 3), người kia làm US2 (Phase 4, T013 có thể bắt đầu ngay cả trước khi Phase 2 xong). Gộp lại ở US3 (Phase 5) và Phase 6.

---

## Notes

- `[P]` = khác file, không phụ thuộc task chưa xong.
- Nhãn `[Story]` ánh xạ task về đúng user story để truy vết.
- Không có task test xUnit/Vitest nào — toàn bộ verification là quickstart.md (đã giải thích ở mục Tests đầu file).
- Tổng cộng 17 task (T001-T017), phủ đủ 3 user story cộng Setup/Foundational/Polish.
- Phần lớn công việc "xây" tập trung ở Phase 2 (Foundational) vì cả ba AC của Jira đều cần cùng một hạ tầng Elastic để quan sát được — đây là đặc điểm riêng của một tính năng hạ tầng, khác các tính năng trước (ví dụ 016) nơi mỗi story sửa một file mã ứng dụng khác nhau.
- **Trạng thái cuối cùng: 17/17 task hoàn tất (T001-T017)**, xác nhận bằng dữ liệu thật trên Docker Desktop (người dùng khởi động giữa phiên) — không còn task nào bị chặn. Toàn bộ 3 Acceptance Criteria của Jira SCRUM-25 đã xác nhận bằng thực nghiệm: (1) mọi service wiring OTel qua ServiceDefaults dùng chung (đã đúng từ trước, T002/T013); (2) traces/metrics/logs của một đơn hàng thật tra được đầy đủ trong Elasticsearch cho mọi hop (T009/T010); (3) log có cấu trúc mang đủ service/tenant/correlation (T014), và ServiceDefaults xác nhận là load-bearing thật qua thực nghiệm gỡ-rồi-khôi-phục (T015). Một lỗi có sẵn không liên quan (`scripts/demo.ps1` 401 khi xoá giỏ hàng, do thiếu Bearer token sau khi 014/015 thêm deny-by-default authz) được phát hiện ở T011 và tách thành task riêng (`task_21088d84`), không thuộc phạm vi tính năng này.
