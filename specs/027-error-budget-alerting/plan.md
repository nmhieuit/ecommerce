# Implementation Plan: Chính sách ngân sách lỗi (error budget) và ngưỡng cảnh báo gắn với SLO từng service

**Branch**: `claude/scrum-35-backlog-export-cefede` | **Date**: 2026-10-01 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/027-error-budget-alerting/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Biến SLO đã khai báo ở 021 thành một ngân sách có hệ quả. Mỗi `service-manifest.yaml` (7 service) có
thêm khối `error-budget-policy`, nêu bằng con số:
- 4 ngân sách: khả dụng 1%, 5xx 1%, độ trễ p95 5%, độ trễ p99 1%;
- cửa sổ tuần lịch theo giờ Việt Nam;
- các mốc 50%/75%/100%, và "cạn" = bất kỳ ngân sách nào đạt 100%;
- hệ quả khi cạn: dừng merge tính năng mới;
- điều kiện hồi phục: 3 ngày đạt SLO.

Kibana chạy 4 rule ES|QL (license Basic), mỗi 5 phút một lần:
- 3 rule mốc tính mức tiêu hao từ telemetry traces có sẵn;
- 1 rule suy ra trạng thái "cạn — ưu tiên độ tin cậy" từ sự kiện cạn ngân sách và kết quả SLO theo
  ngày.

Toàn bộ trạng thái cảnh báo hiện ở nhóm panel trên cùng của dashboard SLO hằng ngày. Một middleware
tiêm lỗi 5xx trong ServiceDefaults (tắt mặc định, hai lớp chặn như 025) cho phép làm cạn ngân sách thật
qua đường OTel để kiểm chứng. Chi tiết quyết định: [research.md](./research.md).

## Technical Context

**Language/Version**: C#/.NET 10 (middleware ServiceDefaults + test); ES|QL (rule, panel Kibana); YAML
(manifest, compose).

**Primary Dependencies**: Kibana/Elasticsearch 9.4.4 (Alerting, rule "Elasticsearch query", Index
connector — đều có ở license Basic); YamlDotNet (đã dùng trong `tests/ServiceManifestSloConventionTests`);
xUnit.

**Storage**: Index mới `slo-error-budget-events` (append-only, do Index connector ghi); alerts-as-data
`.alerts-stack.alerts-default` của Kibana. Không có database nghiệp vụ nào bị chạm.

**Testing**: xUnit — `ErrorBudgetPolicyTests` và `ErrorBudgetRuleDefinitionTests` (dự án convention
test có sẵn), `ChaosFaultInjectionMiddlewareTests` (`shared/ServiceDefaults.UnitTests`). Hành vi trên
Kibana thật được xác thực theo [quickstart.md](./quickstart.md), không chặn PR.

**Target Platform**: Docker Compose local (`docker-compose.yml`, `docker-compose.local.yml`) — nơi duy
nhất chạy Elastic stack; CI Jenkins chạy các test xUnit.

**Project Type**: Bổ sung vào monorepo microservices hiện có (cấu hình quan sát + một middleware dùng
chung + test), không có service mới.

**Performance Goals**: 4 rule × mỗi 5 phút, mỗi lần quét traces từ đầu tuần tới nay trên một máy
local — chấp nhận được với khối lượng demo hiện tại.

**Constraints**: Không đổi hành vi endpoint khi không tiêm lỗi (FR-015); `Chaos:AllowFaultInjection`
không bao giờ commit `true` cho production; khoá mã hoá Kibana không nằm trong file cấu hình commit
(Principle VI); không sửa ngưỡng SLO của 021.

**Scale/Scope**: 7 manifest, 4 rule, 1 connector, 1 index mới, 3 panel mới trên 1 dashboard có sẵn,
1 middleware, 3 lớp test.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy | Mỗi service tự mang chính sách trong manifest riêng; không thêm phụ thuộc giữa service. | PASS |
| II. Contract-First | 3 hợp đồng (`contracts/`) viết trước khi sửa manifest, rule hay middleware. | PASS |
| III. Test-First | `ErrorBudgetPolicyTests`, `ErrorBudgetRuleDefinitionTests`, `ChaosFaultInjectionMiddlewareTests` viết trước, chạy thấy đỏ, rồi mới sửa manifest/thêm rule/middleware. Không có phụ thuộc hạ tầng mới cần Testcontainers. | PASS (kế hoạch, tách ở tasks.md) |
| IV. Event-Driven | Không thêm giao tiếp giữa service. | N/A |
| V. Tenant Isolation | Không chạm đường dữ liệu tenant; middleware trả 500 trước khi tới logic nghiệp vụ. | N/A |
| VI. Secure by Default | Khoá mã hoá Kibana qua `.env` (Vùng 2), không ghi trong compose. Tiêm lỗi có hai lớp chặn, mặc định tắt; middleware đặt trước auth nhưng chỉ có thể làm request thất bại, không mở được quyền truy cập nào. | PASS |
| VII. Observable by Default | Dùng telemetry OTel đã có; 5xx tiêm vào đi qua đúng đường OTel thật. | PASS |
| VIII. Performance and Resilience Budgets | Đây chính là câu "Sustained SLO breach consumes the error budget... reliability work takes priority" của Principle VIII. | PASS (mục tiêu chính) |
| IX. Frontend Discipline | Không liên quan. | N/A |
| X. Toggle-Gated, Reversible | Tiêm lỗi gated bởi `Chaos:AllowFaultInjection` (công cụ SRE lâu dài như 025, không phải toggle rollout). Rule Kibana tắt/bật được trên UI không cần redeploy; khối manifest chỉ là tài liệu cấu hình. | PASS (lý giải như 025) |

Không có vi phạm cần biện minh.

**Re-check sau Phase 1**: thiết kế ở data-model/contracts không thêm service, không thêm secret vào
file commit, không đổi hợp đồng HTTP/event → kết quả giữ nguyên.

## Project Structure

### Documentation (this feature)

```text
specs/027-error-budget-alerting/
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── error-budget-policy-manifest-shape.md
│   ├── error-budget-alert-rules-contract.md
│   └── chaos-fault-injection-contract.md
└── tasks.md             # /speckit-tasks — chưa tạo
```

### Source Code (repository root)

```text
services/*/src/*.Api/service-manifest.yaml        # sửa (7 file): thêm khối error-budget-policy

shared/ServiceDefaults/
├── ChaosFaultInjectionMiddleware.cs               # mới
├── ChaosFaultOptions.cs                           # mới — section "Chaos", AllowFaultInjection
└── ServiceDefaultsExtensions.cs                   # sửa: đăng ký options + UseMiddleware sau CorrelationId

shared/ServiceDefaults.UnitTests/
└── ChaosFaultInjectionMiddlewareTests.cs          # mới

tests/ServiceManifestSloConventionTests/
├── ServiceManifestModel.cs                        # sửa: thêm ErrorBudgetPolicySection
├── ErrorBudgetPolicyTests.cs                      # mới
└── ErrorBudgetRuleDefinitionTests.cs              # mới

docs/kibana-quan-sat-he-thong/
├── 07-canh-bao-ngan-sach-loi.md                   # mới — cách dựng/vận hành rule, connector, panel
├── alerts/error-budget-rules.ndjson               # mới — export 4 rule + Index connector
└── dashboards/slo-van-hanh-hang-ngay.ndjson       # sửa — thêm nhóm panel "Ngân sách lỗi tuần này"

docker-compose.yml, docker-compose.local.yml       # sửa: Kibana nhận khoá mã hoá; 7 service nhận Chaos__AllowFaultInjection
.env.example                                       # sửa: KIBANA_ENCRYPTION_KEY (Vùng 2), CHAOS_ALLOW_FAULT_INJECTION (Vùng 1)
```

**Structure Decision**: Không tạo dự án mới. Mọi thay đổi nằm cạnh các thành phần cùng loại đã có:
- middleware cạnh `CorrelationIdMiddleware`;
- test trong hai dự án test sẵn có;
- tài liệu và export Kibana dưới `docs/kibana-quan-sat-he-thong/`, nối tiếp file `06`.

## Điểm dừng bắt buộc khi triển khai

[research.md](./research.md) mục "Điểm phải xác minh" (V1–V5) phải được kiểm chứng trên Kibana 9.4.4
thật ở task đầu tiên. V1 và V2 sai thì **dừng và hỏi lại người dùng**, không tự đổi phương án.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
