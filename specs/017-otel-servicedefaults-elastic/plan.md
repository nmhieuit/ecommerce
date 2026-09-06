# Implementation Plan: Phát telemetry OTel (traces/metrics/logs) qua ServiceDefaults tới Elastic

**Branch**: `017-otel-servicedefaults-elastic` | **Date**: 2026-09-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/017-otel-servicedefaults-elastic/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát mã nguồn cho thấy phần lớn hạ tầng OpenTelemetry mà spec yêu cầu **đã tồn tại và đã đúng**: `shared/ServiceDefaults/ServiceDefaultsExtensions.cs` wire traces/metrics/logs qua OTLP giống hệt nhau ở cả 7 service, `CorrelationIdMiddleware`/`Tenancy/TenantContextMiddleware` đã đưa `CorrelationId`/`TenantId` vào scope của mọi log có cấu trúc, `service.name` được gắn tự động qua OTel resource attribute, và rà soát toàn bộ `services/` không tìm thấy log call nào dùng chuỗi nội suy (research.md Decision 1). Chính comment trong `docker/otel-collector-config.yaml` đã tự xác nhận điều còn thiếu: *"Elastic is the backend, and standing it up is Phase 3 work (SCRUM-25)"* — collector hôm nay chỉ có exporter `debug`, không có gì chuyển tiếp dữ liệu tới một backend Elastic thật.

Tính năng này (SCRUM-25) xây đúng phần hạ tầng còn thiếu đó, không sửa một dòng C# nào: (1) thêm service `elasticsearch` và `kibana` (single-node, không xác thực) vào `docker-compose.yml` và nhân bản vào `docker-compose.local.yml` — đúng vị trí `otel-collector` đã sống, vì mọi service phụ thuộc cứng vào collector từ trước (research.md Decision 2); (2) thêm exporter `elasticsearch` (chế độ mapping `otel`, có sẵn trong `otel-collector-contrib` đang dùng) **cạnh** exporter `debug` hiện có trong cả ba pipeline (traces/metrics/logs), không thay thế — vì `scripts/demo.ps1` đang parse chính output của `debug` để chứng minh 5 hop phục vụ một đơn hàng, và việc đó không được phép hỏng (research.md Decision 3, 4); (3) ghim phiên bản image thay vì `:latest` cho cả collector lẫn Elastic/Kibana (research.md Decision 7). Test Scenario 3 của Jira ("gỡ ServiceDefaults khỏi một service để chứng minh nó load-bearing") là một bước runbook thủ công trong `quickstart.md`, không phải một test tự động mới — biến nó thành test tự động nghĩa là phải giữ vĩnh viễn một nhánh mã tắt observability, ngược với chính điều Principle VII yêu cầu (research.md Decision 8).

## Technical Context

**Language/Version**: Không có mã C#/.NET nào bị chạm — thay đổi thuần cấu hình hạ tầng: Docker Compose YAML (`docker-compose.yml`, `docker-compose.local.yml`) và OTel Collector YAML (`docker/otel-collector-config.yaml`, `docker/otel-collector-config.demo.yaml`).

**Primary Dependencies**: Không có gói NuGet/npm mới. Hai image container mới: `docker.elastic.co/elasticsearch/elasticsearch` và `docker.elastic.co/kibana/kibana`, phiên bản 8.x ghim cụ thể (tối thiểu bản hỗ trợ ổn định `mapping.mode: otel` của exporter `elasticsearch` — số hiệu chính xác chốt ở bước triển khai, research.md Decision 7). Exporter `elasticsearch` của `otel-collector-contrib` (image đã dùng, không cần thêm component nào khác — research.md Decision 3).

**Storage**: Elasticsearch là nơi lưu telemetry (traces/metrics/logs), không phải CSDL nghiệp vụ — không service nào đọc/ghi dữ liệu nghiệp vụ vào đây (Principle I không bị chạm). Volume mới: `elasticsearch-data` (`docker-compose.yml`), `local-es-data` (`docker-compose.local.yml`).

**Testing**: Không có test tự động mới (research.md Decision 1, 8). Kiểm chứng qua `quickstart.md` — grep tĩnh xác nhận không có log call nội suy, truy vấn Kibana/Elasticsearch xác nhận traces/metrics/logs tới nơi, và một bước runbook thủ công (gỡ ServiceDefaults khỏi một service, xem nó mất telemetry). `scripts/demo.ps1` (006) tiếp tục là bằng chứng tự động hiện có, không đổi.

**Target Platform**: Docker Compose local (hai file: `docker-compose.yml` là "deployed shape in miniature", `docker-compose.local.yml` tự chứa, mọi cổng publish). Không có thay đổi nào cho Kubernetes/Ansible trong phạm vi tính năng này — bám theo tiền lệ của mọi tính năng trước (001-016) đều dừng ở Docker Compose local.

**Project Type**: Hạ tầng quan sát-được (observability infrastructure), không phải service/app mới. Không có project C#/TypeScript mới, không có route/endpoint mới.

**Performance Goals**: Không có ngân sách mới cần khai báo cho request path nghiệp vụ — Elasticsearch/Kibana không nằm trên đường đi của bất kỳ request nghiệp vụ nào (chỉ nhận export bất đồng bộ từ collector). Elasticsearch container giới hạn heap (`ES_JAVA_OPTS=-Xms512m -Xmx512m`) để không cạnh tranh tài nguyên với SQL Server/service khác trên máy dev, cùng tinh thần "ngân sách rõ ràng" của Principle VIII dù không phải một SLO nghiệp vụ.

**Constraints**: Exporter `debug` hiện có trong `docker/otel-collector-config.yaml`/`.demo.yaml` PHẢI được giữ nguyên trong cả ba pipeline — `scripts/demo.ps1` phụ thuộc trực tiếp vào output văn bản của nó để xác nhận 5 hop (research.md Decision 4). `docker-compose.yml` mặc định KHÔNG publish cổng Elasticsearch/Kibana ra host — giữ đúng bất biến "chỉ 2 cổng" từ spec 004 SC-010; cổng `9200`/`5601` chỉ publish ở `docker-compose.local.yml`. Elasticsearch/Kibana chạy single-node, `xpack.security.enabled=false`, không TLS — đúng tư thế local-dev mọi datastore khác trong repo đã chọn (research.md Decision 5), không áp dụng cho một triển khai Kubernetes thật.

**Scale/Scope**: Chạm `docker-compose.yml` (+2 service, +1 volume, cập nhật `otel-collector` thêm `depends_on`/ghim version), `docker-compose.local.yml` (+2 service nhân bản, +1 volume, +2 cổng publish, cập nhật port map comment, ghim version `otel-collector`), `docker/otel-collector-config.yaml` và `docker/otel-collector-config.demo.yaml` (+1 exporter, cập nhật 3 pipeline mỗi file). Không service/route/schema C# nào mới.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không service nào đọc/ghi CSDL của service khác; Elasticsearch không phải CSDL nghiệp vụ của bất kỳ service nào. Không thêm ceremony kiến trúc — chỉ hoàn thiện một pipeline hạ tầng cross-cutting đã có (collector) bằng cách thêm điểm đến cho dữ liệu nó đã nhận sẵn. | PASS |
| II. Contract-First Integration | Hợp đồng `contracts/otel-collector-elasticsearch-export-contract.md` viết trước khi cấu hình exporter mới được thêm vào `docker/otel-collector-config*.yaml`. Đây là hợp đồng hạ tầng nội bộ, không phải API/event nghiệp vụ, nên không cần versioning kiểu client-generated. | PASS |
| III. Test-First Development | Không có mã ứng dụng nào được viết, nên không có "test trước, code sau" theo nghĩa TDD C#/TypeScript áp dụng (research.md Decision 1). Thay vào đó, `quickstart.md` định nghĩa trước các kịch bản kiểm chứng (grep tĩnh, truy vấn Kibana, runbook thủ công) mà thay đổi hạ tầng phải thoả — đóng vai trò tương đương "failing test trước" cho một thay đổi thuần cấu hình. | PASS (adapted — không có test C#/TS nào bị bỏ sót) |
| IV. Event-Driven by Default | Không chạm — không có publisher/consumer/event nào trong phạm vi tính năng này. | N/A |
| V. Tenant Isolation Is a Security Boundary | Không chạm cơ chế resolve/lan truyền tenant. `TenantId` tiếp tục chỉ được đưa vào log scope bởi `TenantContextMiddleware` đã có, không đổi. Elasticsearch không lưu dữ liệu nghiệp vụ theo tenant — chỉ lưu telemetry của chính nền tảng. | PASS |
| VI. Secure by Default | Traces/metrics/logs không chứa PII theo assumption của spec — tính năng này không thêm trường dữ liệu mới vào bất kỳ tín hiệu nào, chỉ thêm nơi các tín hiệu đã có đi tới. Elasticsearch/Kibana không xác thực là một quyết định cục bộ-dev có chủ đích (research.md Decision 5), nhất quán với mọi datastore khác trong `docker-compose.yml`/`.local.yml` (SQL Server, RabbitMQ, Redis) — không phải một ngoại lệ mới. | PASS (justified for local dev, xem Constraints) |
| VII. Observable by Default | Đây CHÍNH LÀ tính năng hiện thực hoá trực tiếp câu "Every service MUST emit OpenTelemetry traces, metrics, and structured logs to the Elastic stack through a shared ServiceDefaults component" — phần `ServiceDefaults` đã đúng từ trước (research.md Decision 1), tính năng này hoàn thiện phần "to the Elastic stack" còn thiếu. | PASS (core) |
| VIII. Performance and Resilience Budgets | Không network call mới trên đường đi request nghiệp vụ. Elasticsearch giới hạn heap tường minh (Constraints) — không phải một SLO nghiệp vụ nhưng cùng tinh thần "ngân sách khai báo rõ, không phải aspiration". | PASS |
| IX. Frontend Discipline | Không chạm frontend. | N/A |
| X. Toggle-Gated, Reversible Delivery | Không cần toggle mới — thay đổi không ảnh hưởng hành vi quan sát được của bất kỳ actor nghiệp vụ nào (không service nào phụ thuộc Elasticsearch để trả lời request), rollback chỉ là dừng hai container mới mà không service nghiệp vụ nào nhận biết được sự vắng mặt đó (chúng chỉ export "vào khoảng không" như trước tính năng này nếu Elasticsearch bị gỡ, đúng hành vi exporter OTLP retry-rồi-drop hiện có). Cùng loại "observability-only, không cần toggle" mà 016 đã lập luận. | PASS (justified) |

Không có deviation nào cần Complexity Tracking. Tính năng này là một **bước hoàn thiện hạ tầng có chủ đích, khoanh vùng hẹp** trên nền `ServiceDefaults` đã đúng từ trước (Principle VII), không mang theo deviation mới.

## Project Structure

### Documentation (this feature)

```text
specs/017-otel-servicedefaults-elastic/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   └── otel-collector-elasticsearch-export-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
docker-compose.yml
├── volumes: elasticsearch-data                     # MỚI
└── services:
    ├── elasticsearch                                # MỚI — single-node, xpack security tắt (research.md Decision 2, 5)
    ├── kibana                                        # MỚI — depends_on elasticsearch (service_healthy)
    └── otel-collector                                # cập nhật: depends_on elasticsearch; ghim image version (research.md Decision 7)

docker-compose.local.yml
├── volumes: local-es-data                            # MỚI
├── port map comment (đầu file)                       # cập nhật: +9200 (Elasticsearch), +5601 (Kibana)
└── services:
    ├── elasticsearch                                 # MỚI — nhân bản từ docker-compose.yml, + ports 9200:9200
    ├── kibana                                        # MỚI — nhân bản, + ports 5601:5601
    └── otel-collector                                 # cập nhật: cùng thay đổi như docker-compose.yml

docker/
├── otel-collector-config.yaml                        # cập nhật: + exporter `elasticsearch`, gắn vào cả 3 pipeline cạnh `debug`
└── otel-collector-config.demo.yaml                    # cập nhật: cùng thay đổi (giữ bất biến "identical except log level")

# docker-compose.demo.yml: không đổi — kế thừa elasticsearch/kibana từ docker-compose.yml qua overlay,
# không tự định nghĩa service nào cho chúng.
# docker-compose.debug.yml, docker-compose.ci.yml, docker-compose.deps.yml: không chạm — ngoài phạm vi
# (ci.yml chứa Elasticsearch riêng của SonarQube, không liên quan).

# shared/ServiceDefaults, mọi services/*/src/*/Program.cs: KHÔNG sửa — đã đúng từ trước (research.md
# Decision 1).
```

**Structure Decision**: Không có service/project C#/TypeScript mới. Toàn bộ thay đổi là hạ tầng Docker Compose + cấu hình OTel Collector, thêm vào đúng vị trí `otel-collector` đã sống (`docker-compose.yml` cho stack mặc định, nhân bản ở `docker-compose.local.yml` vì file đó tự chứa — không phải override). Đây là khuôn mẫu "hoàn thiện một pipeline cross-cutting đã có" giống 016, chỉ khác ở tầng hạ tầng thay vì tầng code.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Không có violation nào trong Constitution Check ở trên — bảng này để trống có chủ đích.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

Thiết kế Phase 1 không thêm violation mới:

- **Contract-first (II)** được thoả bởi `contracts/otel-collector-elasticsearch-export-contract.md`, viết trước cấu hình exporter thật — ghi rõ Producers/Consumers/Failure Modes/Stability, và tường minh cảnh báo rằng tên data stream/chế độ mapping có thể đổi theo phiên bản, để tính năng sau (dashboard, cảnh báo) không giả định sai một hợp đồng chưa được version hoá chính thức.
- **Không phá vỡ bằng chứng tự động đã có (VII; research.md Decision 4)** được thoả cấu trúc bởi `data-model.md`'s bảng "Đường ống Telemetry" — chỉ rõ `debug` giữ nguyên, `elasticsearch` là exporter **thêm vào**, không phải thay thế; `contracts/`'s mục "Consumers" xác nhận `scripts/demo.ps1` không đổi.
- **Elasticsearch không phải một bề mặt bảo mật mới cần lo (VI)** được thoả bởi research.md Decision 5's so sánh tường minh với tư thế local-dev đã có của SQL Server/RabbitMQ/Redis, và bởi việc cụm này không mang dữ liệu nghiệp vụ/tenant nào — chỉ telemetry của chính nền tảng.
- **`vm.max_map_count` không phải một lỗ hổng ẩn trong "một lệnh duy nhất" (constraint hạ tầng cục bộ)** được thoả bởi research.md Decision 6 và `quickstart.md`'s Prerequisites — ghi rõ triệu chứng và lệnh khắc phục thay vì để người dùng tự đoán tại sao Elasticsearch thoát ngay.
- **Ghim phiên bản thay vì `latest` (rủi ro trôi dạt cấu hình, research.md Decision 7)** được thoả bởi `data-model.md`'s mục "Cấu hình hạ tầng mới" liệt kê rõ những nơi cần ghim, dù số hiệu chính xác cố ý để ngỏ cho bước triển khai — tránh việc kế hoạch này tự đóng đinh một con số có thể đã lỗi thời.
- **Test Scenario 3 của Jira không trở thành ceremony sai chỗ (VII; research.md Decision 8)** được thoả bởi `quickstart.md` Scenario 5 — một bước runbook thủ công đầy đủ (comment code, rebuild, quan sát, khôi phục), thay vì một test tự động phải giữ vĩnh viễn một nhánh mã tắt observability.
- Không có deviation nào được mang tiếp từ tính năng này sang tính năng sau.

Gate: **PASS** (không có deviation nào cần theo dõi tiếp).
