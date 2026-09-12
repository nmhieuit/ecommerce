# Implementation Plan: Diễn tập chaos engineering — giết một pod / tiêm độ trễ để kiểm chứng resilience

**Branch**: `code/Chaos-exercise-kill-pod-inject-latency` | **Date**: 2026-09-12 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/025-chaos-pod-kill-latency/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát mã nguồn hiện có cho thấy phần lớn "lưới an toàn" mà SCRUM-34 muốn kiểm chứng **đã tồn tại**
từ các story trước — tính năng này không xây lại chúng, chỉ khép kín 3 khoảng hở để bài tập chaos có
thể chạy lặp lại được:

1. **Kịch bản kill-pod (US1) không thiếu gì cả**: `baskets` đã có resilience đầy đủ ở BFF
   (`AddStandardResilienceHandler()` từ 020: AttemptTimeout 1s/TotalRequestTimeout 3s/CircuitBreaker
   SamplingDuration 10s) và telemetry Polly đã lên OTel (từ 020) → Elastic (từ 017). Nhưng rà soát
   `deploy/ansible/roles/service_deployment/templates/deployment.yaml.j2` phát hiện **không có
   service nào khai báo `replicas`** → mọi service (kể cả `baskets`) hiện chạy 1 replica, nên
   "K8s reschedules it" thực chất là cold-start có gián đoạn thật, không phải failover liền mạch.
   Đây là một phát hiện cần ghi nhận trong bài tập, không phải một khoảng hở cần vá (vá nó — thêm
   HA — sẽ đổi phạm vi từ "diễn tập chaos" sang một tính năng hạ tầng khác, ngoài 9 FR của spec.md).
2. **Kịch bản inject-latency (US2) thiếu một cơ chế tiêm lỗi**: không có công cụ fault-injection nào
   trong repo; tiền lệ duy nhất (021's quickstart Bước 4) là sửa mã nguồn tạm thời — không khả thi
   cho một bài tập lặp lại định kỳ có ghi nhận từng lần (US3). Thêm
   `ChaosLatencyInjectionMiddleware` mới, tối thiểu, cho Orders.Api: tắt hoàn toàn theo mặc định
   (`Chaos:AllowLatencyInjection=false`), khi bật thì đọc độ trễ cần tiêm từ header per-request
   (`X-Chaos-Latency-Ms`, có trần an toàn 30s) — bật/tắt tiêm giữa bài tập không cần redeploy.
   Dashboard SLO đã có (021) tái sử dụng nguyên trạng để quan sát ngân sách bị tiêu hao — không xây
   dashboard mới.
3. **Kịch bản ghi nhận kết quả (US3) chưa có chỗ đặt**: thêm thư mục tài liệu
   `docs/dien-tap-chaos-engineering/` (mẫu bản ghi kết quả + README liệt kê lịch sử chạy) — không
   tích hợp Jira lập trình (không có sẵn trong repo, ngoài phạm vi 9 FR).

## Technical Context

**Language/Version**: C#/.NET 10 cho middleware mới trong Orders.Api (`net10.0`, khớp constitution
và mọi `.csproj` hiện có). Markdown cho tài liệu runbook/bản ghi kết quả mới — không có ngôn ngữ ứng
dụng nào khác.

**Primary Dependencies**: Không thêm gói NuGet/npm mới nào. Middleware dùng
`Microsoft.AspNetCore.Http` (đã có sẵn trong `Orders.Api`) và `Task.Delay` chuẩn của .NET — không cần
Toxiproxy hay công cụ fault-injection ngoài (research.md Quyết định 1).

**Storage**: N/A — không có dữ liệu nghiệp vụ mới. Bản ghi kết quả bài tập là file markdown trong
`docs/`, không phải một bảng CSDL.

**Testing**: Một unit test mới, `ChaosLatencyInjectionMiddlewareTests`, thêm vào dự án đã có
`services/orders/tests/Orders.Api.UnitTests` (research.md Quyết định 4) — dùng `TimeProvider` giả lập
thay vì `Task.Delay` thật để test tất định và nhanh, theo đúng khuôn mẫu unit-test-thuần mà
`RetryMethodPolicyTests` (020) đã dùng cho một mối lo tương tự (một quy tắc cấu hình, không cần hạ
tầng thật). Ba kịch bản kiểm thử của Jira SCRUM-34 (kill pod thật, tiêm độ trễ thật, xác nhận
dashboard) là hành vi động trên cluster thật — tài liệu hoá tại `quickstart.md`, chạy tay/định kỳ,
không chặn PR (cùng logic 019/021 đã dùng).

**Target Platform**: Container Linux trên Kubernetes tự vận hành (self-hosted, theo constitution) —
không đổi so với hiện tại. Middleware chạy trong chính pod Orders.Api hiện có, không cần hạ tầng mới.

**Project Type**: Bổ sung một middleware nhỏ vào một service backend hiện có (Orders.Api) + tài liệu
runbook — không phải service runtime mới, không có "frontend" liên quan tới tính năng này.

**Performance Goals**: Không có mục tiêu hiệu năng mới cho hệ thống — bản thân tính năng là công cụ
*gây ra* độ trễ/gián đoạn có kiểm soát để kiểm chứng ngân sách hiệu năng đã khai báo ở nơi khác
(021), không phải một route nghiệp vụ cần đạt SLO.

**Constraints**: `Chaos:AllowLatencyInjection` KHÔNG BAO GIỜ được commit là `true` trong bất kỳ file
cấu hình nào đại diện cho production. Độ trễ tiêm vào PHẢI có trần an toàn (30s — spec Edge Case 2:
"độ trễ được tiêm vượt xa timeout đã cấu hình... circuit breaker có mở đúng theo ngưỡng"). Middleware
KHÔNG được thay đổi hợp đồng phản hồi (status/headers/body) của bất kỳ route nào của Orders.Api.
KHÔNG được thêm replica hay sửa hành vi reschedule của Kubernetes cho `baskets` — bài tập quan sát
hành vi hiện tại (1 replica), không thay đổi nó (ngoài phạm vi 9 FR).

**Scale/Scope**: 1 middleware mới + 1 cấu hình mới trong Orders.Api; 1 dự án test đã có (thêm test
mới, không tạo dự án mới); 1 thư mục tài liệu mới (`docs/dien-tap-chaos-engineering/`, 3 file: mẫu +
README + thư mục `ket-qua/` ban đầu rỗng). Không sửa `services/baskets`, không sửa BFF, không sửa
Ansible/K8s manifest, không xây dashboard mới.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Middleware mới sống hoàn toàn trong Orders.Api, không đụng ranh giới dữ liệu của service khác; không đổi quyền sở hữu gì. | PASS |
| II. Contract-First Integration | Không có hợp đồng HTTP/event nghiệp vụ nào thay đổi; hợp đồng mới duy nhất (`contracts/chaos-latency-injection-contract.md`) là một hợp đồng an toàn nội bộ, được viết trước khi hiện thực middleware. | PASS |
| III. Test-First Development (NON-NEGOTIABLE) | `ChaosLatencyInjectionMiddlewareTests` PHẢI được viết trước, ở dạng thất bại (middleware chưa tồn tại), rồi mới thêm middleware để pass — chi tiết tách việc ở `tasks.md`. Không cần Testcontainers vì middleware không có phụ thuộc ngoài (research.md Quyết định 4). | PASS (kế hoạch ở Phase 2/tasks) |
| IV. Event-Driven by Default | Không liên quan — tính năng không thêm giao tiếp giữa service, không publish/consume sự kiện nào. | N/A |
| V. Tenant Isolation Is a Security Boundary | Middleware đặt trước `UseTenancy()`/`UseIdentityValidation()` trong pipeline, không đọc/ghi gì liên quan tới tenant hay CSDL — không có đường dẫn dữ liệu tenant nào bị chạm tới. | N/A |
| VI. Secure by Default | Middleware không thêm endpoint, không thêm secret. `Chaos:AllowLatencyInjection` mặc định `false` là tư thế an toàn (fail-safe) — một request không thể tự bật cơ chế này chỉ bằng cách gửi header; header chỉ có hiệu lực khi cấu hình môi trường đã bật trước (hai lớp gate, research.md Quyết định 1). | PASS |
| VII. Observable by Default | Tính năng này KHÔNG thêm instrumentation mới — nó cố tình tái sử dụng nguyên trạng telemetry Polly (020) và pipeline OTel/Elastic (017) đã có để quan sát circuit breaker/retry, đúng tinh thần "không nhân bản observability đã có". | PASS (tái sử dụng, không mở rộng) |
| VIII. Performance and Resilience Budgets | Đây chính là mục đích của SCRUM-34 — kiểm chứng bằng thực nghiệm rằng ngân sách resilience (020) và SLO (021) hoạt động đúng dưới sự cố thật, không chỉ đúng trên giấy. Trần an toàn 30s của middleware tự thân cũng tôn trọng nguyên tắc "unbounded waits cannot exist" ngay trong công cụ dùng để kiểm chứng nguyên tắc đó. | PASS (là mục tiêu chính) |
| IX. Frontend Discipline | Không liên quan — tính năng không đụng frontend. | N/A |
| X. Toggle-Gated, Reversible Delivery | `Chaos:AllowLatencyInjection` không phải một toggle rollout có ngày gỡ bỏ theo nghĩa thông thường của nguyên tắc này (không có "hoàn tất rollout" để gỡ) — đây là một công cụ vận hành thường trực, mặc định tắt, tương tự cách `livenessProbe`/`readinessProbe` là hạ tầng thường trực chứ không phải một tính năng cần "hoàn thành rồi gỡ cờ". Tài liệu hoá rõ chủ sở hữu (đội SRE-hat-wearer, theo đúng vai trong spec.md) và lý do không có ngày gỡ ngay tại comment `//Chaos` trong `appsettings.json`, cùng quy ước `//FeatureToggles` đã có. Việc bật/tắt *tiêm độ trễ* trong một bài tập cụ thể (qua header per-request) hoàn toàn không cần code change/redeploy — đúng tinh thần cốt lõi của nguyên tắc. | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/025-chaos-pod-kill-latency/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── chaos-latency-injection-contract.md
│   └── exercise-outcome-writeup-contract.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
services/orders/src/Orders.Api/
├── appsettings.json                          # [SỬA] Thêm khối "Chaos": { "AllowLatencyInjection": false }, comment "//Chaos" theo quy ước "//FeatureToggles" đã có
├── Program.cs                                 # [SỬA] Đăng ký ChaosLatencyInjectionMiddleware ngay sau app.UseServiceDefaults(), trước app.UseIdentityValidation()
└── Features/Chaos/
    ├── ChaosOptions.cs                        # [MỚI] AllowLatencyInjection (bool) + hằng số MaxInjectedLatencyMs = 30000
    └── ChaosLatencyInjectionMiddleware.cs      # [MỚI] Đọc header X-Chaos-Latency-Ms, kẹp trần, Task.Delay khi được phép

services/orders/tests/Orders.Api.UnitTests/
└── Features/Chaos/
    └── ChaosLatencyInjectionMiddlewareTests.cs # [MỚI] 4 bất biến của contracts/chaos-latency-injection-contract.md, TimeProvider giả lập

docs/dien-tap-chaos-engineering/
├── README.md                                  # [MỚI] Runbook tổng quan + liệt kê (index) các bản ghi kết quả trong ket-qua/
├── mau-ket-qua.md                             # [MỚI] Mẫu bản ghi kết quả (data-model.md mục 3)
└── ket-qua/                                   # [MỚI] Thư mục chứa bản ghi kết quả mỗi lần chạy (rỗng lúc merge, điền dần theo thời gian)
```

**Structure Decision**: Không tạo service mới, không sửa `services/baskets` hay BFF (US1 dùng nguyên
trạng resilience đã có — research.md Quyết định 0). Toàn bộ mã ứng dụng mới nằm gọn trong một
vertical slice `Features/Chaos/` của Orders.Api, đúng khuôn mẫu `Features/HealthCheck/` đã thiết lập
(mỗi năng lực là một thư mục tự chứa: registration + middleware/endpoint + options). Tài liệu vận
hành đặt tại `docs/dien-tap-chaos-engineering/` (không phải `specs/025.../`) vì đây là artifact sống,
tích luỹ qua nhiều lần chạy về sau — cùng lý do `docs/kibana-quan-sat-he-thong/` là nơi 017 đặt tài
liệu vận hành của nó, không phải `specs/017.../`.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
