# Implementation Plan: Timeout, retry và circuit breaker cho mọi cuộc gọi ra ngoài (outbound call)

**Branch**: `code/Timeouts-retry-and-circuit-breaker-on-every-outbound-call` | **Date**: 2026-09-09 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/020-timeouts-retry-circuit-breaker/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Rà soát mã nguồn hiện có cho thấy phần lớn phạm vi Principle VIII đã được hiện thực bởi
**002-gateway-bff-routing**: 4 typed client của BFF (Products/Baskets/Orders/Parties) đã dùng
`AddStandardResilienceHandler()` (timeout + retry + circuit breaker), và gateway đã có
`ActivityTimeout` bắt buộc lớn hơn tổng ngân sách của BFF (`ForwardingTimeoutBudgetTests`). Tính
năng này KHÔNG xây lại hạ tầng đó — nó khép kín 5 khoảng hở còn lại mà rà soát phát hiện được:

1. **Gateway → BFF thiếu circuit breaker**: YARP có `ActivityTimeout` nhưng chưa bật passive health
   check trên cluster `bff-cluster`, nên gateway không bao giờ "mở mạch" khi BFF liên tục lỗi.
2. **6 điểm gọi tới identity server (JWKS/discovery) qua JwtBearer backchannel** (gateway +
   4 domain service + BFF, dùng chung `AddIdentityValidation`/`AddToggleGatedIdentity`) đang dựa vào
   `BackchannelTimeout` mặc định của framework — bị chặn (bounded) nhưng không được khai báo tường
   minh, và không có retry/circuit-breaker.
3. **Retry hiện tại của BFF không phân biệt method**: `AddStandardResilienceHandler()` mặc định
   retry mọi lỗi tạm thời bất kể verb, trong khi BFF có hai route ghi dữ liệu không idempotent
   (`POST /basket/items`, `POST /checkout`) — một retry sau khi request thực sự đã tới server có thể
   tạo trùng dòng giỏ hàng/đơn hàng. Đây là khoảng hở tương ứng trực tiếp với spec FR-006.
4. **Sự kiện resilience (timeout/retry/mở mạch) chưa lên được OTel**: `ServiceDefaults` mới đăng ký
   `AddHttpClientInstrumentation`, chưa đăng ký nguồn đo lường riêng mà
   `Microsoft.Extensions.Http.Resilience` phát ra (meter/activity source `"Polly"`) — spec FR-008.
5. **Không có cách rà soát lặp lại được** để xác nhận toàn bộ điểm gọi ra ngoài (không chỉ 4 client
   đã biết) đều đủ ba lớp bảo vệ, và để bắt một điểm gọi mới thêm sau này mà quên gắn — spec FR-007.

Cách tiếp cận: sửa hai điểm cấu hình dùng chung (`shared/Identity`,
`services/gateway/.../ToggleGatedAuthenticationExtensions`, `DownstreamClientRegistrationExtensions`,
`ServiceDefaultsExtensions`, `appsettings.json` của gateway) thay vì sửa từng service, cộng với một dự
án test quét (scanner) mới `tests/ResilienceCoverageTests` theo đúng khuôn mẫu
`ContractCoverageTests`/`ForwardingTimeoutBudgetTests` đã có trong repo. "service → broker" nằm ngoài
phạm vi: `BasketCheckedOutMapper.cs` ghi rõ "the outbox and the publisher... are SCRUM-31's work" —
chưa có cuộc gọi broker nào tồn tại để bọc resilience; hợp đồng chính sách (`contracts/`) được viết
sẵn để SCRUM-31 áp dụng khi publisher đó ra đời.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, khớp constitution và toàn bộ `.csproj` hiện có).

**Primary Dependencies**: `Microsoft.Extensions.Http.Resilience` (đã có trong
`services/bff/src/Bff.Api/Bff.Api.csproj`, sẽ thêm cùng phiên bản vào `Gateway.Api.csproj` và
`shared/Identity` cho backchannel client) — chính là gói cụ thể hiện thực
`Microsoft.Extensions.Resilience` mà constitution Principle VIII nêu tên. Không thêm gói mới nào
khác; YARP (`Yarp.ReverseProxy`) đã có sẵn hỗ trợ passive health check dùng ngay từ cấu hình.

**Storage**: N/A — tính năng không tạo, đọc, hay ghi dữ liệu nghiệp vụ; chỉ có cấu hình
(`appsettings.json`, code đăng ký DI).

**Testing**: xUnit, theo khuôn mẫu quy ước (convention test) đã có trong repo
(`tests/ContractCoverageTests`, `services/gateway/tests/Gateway.Api.UnitTests/ForwardingTimeoutBudgetTests.cs`).
Dự án mới `tests/ResilienceCoverageTests` quét mã nguồn/cấu hình theo một danh sách tường minh các
điểm gọi ra ngoài kỳ vọng (không tự khám phá qua reflection/filesystem glob), giống hệt cách
`ContractCoverageScanner` liệt kê 4 boundary bằng tay. Bổ sung unit test cho quy tắc "chỉ retry
method an toàn" (`RetryMethodPolicyTests`) và integration test tái sử dụng khuôn mẫu
`DownstreamUnavailableTests` đã có ở BFF/gateway để xác nhận circuit breaker mới của gateway mở đúng
lúc.

**Target Platform**: Container Linux trên Kubernetes (theo constitution) — không đổi so với hiện tại.

**Project Type**: Bổ sung vào monorepo backend nhiều service hiện có — không phải service runtime
mới; không có "frontend" liên quan tới tính năng này.

**Performance Goals**: Không thêm ngân sách hiệu năng mới; giữ nguyên các budget đã có ở
`DownstreamClientRegistrationExtensions` (attempt 1 s / tổng 3 s / lấy mẫu circuit breaker 10 s) và
`ActivityTimeout` 10 s của gateway. Circuit breaker mới ở gateway và JwtBearer backchannel dùng
ngưỡng lấy mẫu tương tự cấp độ hiện có (không cần một ngân sách SLO riêng — đây là hạ tầng bảo vệ,
không phải một route nghiệp vụ mới).

**Constraints**: KHÔNG được thay đổi hợp đồng phản hồi hiện có của bất kỳ route nào trong điều kiện
downstream khỏe mạnh (spec FR-009). KHÔNG được thêm retry cho `POST /basket/items` và
`POST /checkout` (spec FR-006, Edge Case 1). KHÔNG được sửa 4 typed client của BFF theo cách phá vỡ
`DownstreamUnavailableTests` hiện có. Gateway forwarding timeout PHẢI tiếp tục ≥ tổng ngân sách BFF
(`ForwardingTimeoutBudgetTests` không được đổi kỳ vọng).

**Scale/Scope**: 5 khoảng hở liệt kê ở Summary, trải trên 2 file cấu hình dùng chung
(`shared/Identity/IdentityValidationExtensions.cs`,
`services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs`), 1 file cấu hình
gateway (`appsettings.json`), 1 file đăng ký client của BFF
(`DownstreamClientRegistrationExtensions.cs`), 1 file `ServiceDefaultsExtensions.cs`, và 1 dự án test
mới. "service → broker" được ghi nhận là ngoài phạm vi, không đếm vào 5 khoảng hở.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Áp dụng thế nào | Trạng thái |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không đổi quyền sở hữu dữ liệu; mọi thay đổi nằm trong cấu hình client gọi ra ngoài của từng service, không đụng tới DB của service khác. | PASS |
| II. Contract-First Integration | Không có hợp đồng HTTP/event mới hay thay đổi hợp đồng hiện có (constraint ở trên); `contracts/` của tính năng này mô tả một hợp đồng cấu hình nội bộ (resilience policy), không phải API. | PASS |
| III. Test-First Development | `tests/ResilienceCoverageTests` và các test method-policy/circuit-breaker mới được viết trước ở dạng thất bại (chưa có cấu hình), rồi mới thêm cấu hình để pass — chi tiết ở `tasks.md`. | PASS (kế hoạch ở Phase 2/tasks) |
| IV. Event-Driven by Default | "service → broker" — nhánh duy nhất Principle IV chi phối trực tiếp — chưa tồn tại cuộc gọi nào (checkout hiện là đồng bộ qua BFF, outbox là việc của SCRUM-31); tính năng này không xây dựng messaging, chỉ để lại `contracts/resilience-policy-contract.md` cho SCRUM-31 áp dụng khi publisher ra đời. | N/A cho phạm vi hiện tại (ghi nhận, không vi phạm) |
| V. Tenant Isolation Is a Security Boundary | Không liên quan tới đường dẫn tenant; JwtBearer backchannel gọi tới identity server để lấy khóa ký, không mang dữ liệu tenant. | N/A |
| VI. Secure by Default | Backchannel HTTP client mới cho JwtBearer vẫn tôn trọng `RequireHttpsMetadata` hiện có; không đổi cách xác thực token, chỉ đổi cách cấu hình timeout/resilience của việc lấy khóa ký. | PASS |
| VII. Observable by Default | Đây là một phần trực tiếp của khoảng hở 4 (Summary) — thêm `AddSource("Polly")`/`AddMeter("Polly")` vào `ServiceDefaultsExtensions` để sự kiện resilience lên OTel như mọi service khác, không cấu hình riêng lẻ. | PASS (là một phần mục tiêu chính) |
| VIII. Performance and Resilience Budgets | Đây chính là Principle VIII — tính năng khép kín 5 khoảng hở còn lại so với yêu cầu "mọi cuộc gọi ra ngoài PHẢI có timeout tường minh, retry, circuit breaker". Việc giới hạn retry ở method an toàn (khoảng hở 3) là áp dụng đúng tinh thần nguyên tắc — retry mù trên method ghi dữ liệu tự nó vi phạm yêu cầu "unbounded waits/side effects cannot exist", nên đây không phải một ngoại lệ cần biện minh mà là cách hiện thực đúng. | PASS (là mục tiêu chính) |
| IX. Frontend Discipline | Không liên quan — tính năng không đụng frontend. | N/A |
| X. Toggle-Gated, Reversible Delivery | Cả 5 thay đổi đều là siết chặt cấu hình hạ tầng đã tồn tại (thêm timeout tường minh, giới hạn retry, bật passive health check) chứ không phải hành vi nghiệp vụ mới hướng tới người dùng cuối; rollback không cần feature toggle — revert commit cấu hình và redeploy là đủ, giống cách 002 tự thân cũng không dùng toggle cho `AddStandardResilienceHandler` ban đầu. Nếu circuit breaker mới ở gateway gây false-positive ngoài dự kiến trong vận hành thực tế, ngưỡng (`SamplingDuration`, `FailureRatio`) là tham số cấu hình có thể chỉnh không cần đổi mã. | PASS (lý giải, không phải vi phạm) |

Không có vi phạm nào cần biện minh tại Complexity Tracking.

## Project Structure

### Documentation (this feature)

```text
specs/020-timeouts-retry-circuit-breaker/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
└── Identity/
    └── IdentityValidationExtensions.cs   # [SỬA] Backchannel HttpClient tường minh (timeout+retry+CB) cho AddIdentityValidation

services/
├── gateway/
│   └── src/Gateway.Api/
│       ├── appsettings.json              # [SỬA] Thêm HealthCheck:Passive cho cluster bff-cluster
│       └── Identity/
│           └── ToggleGatedAuthenticationExtensions.cs  # [SỬA] Cùng backchannel HttpClient tường minh cho JwtBearer scheme của gateway
│       Gateway.Api.csproj                # [SỬA] Thêm PackageReference Microsoft.Extensions.Http.Resilience
│   └── tests/Gateway.Api.UnitTests/
│       └── PassiveHealthCheckConfigurationTests.cs  # [MỚI] Xác nhận bff-cluster có bật passive health check với ngưỡng hợp lệ
│
└── bff/
    └── src/Bff.Api/DownstreamClients/
        └── DownstreamClientRegistrationExtensions.cs  # [SỬA] Giới hạn Retry.ShouldHandle theo HttpMethod an toàn (GET/HEAD)
    └── tests/Bff.Api.UnitTests/            # [MỚI nếu chưa có] dự án UnitTests cho BFF, nếu chưa tồn tại
        └── RetryMethodPolicyTests.cs        # [MỚI] Xác nhận POST không được retry, GET vẫn được retry

shared/ServiceDefaults/
└── ServiceDefaultsExtensions.cs           # [SỬA] AddSource("Polly") + AddMeter("Polly") trong AddServiceDefaults

tests/
└── ResilienceCoverageTests/                # [MỚI] dự án xUnit quét, theo khuôn mẫu ContractCoverageTests
    ├── ResilienceCoverageTests.csproj
    ├── ResilienceCoverageScanner.cs         # Danh sách tường minh mọi điểm gọi ra ngoài + file/marker chứng minh đã bọc resilience
    └── ResilienceCoverageTests.cs
```

**Structure Decision**: Không tạo service mới. Toàn bộ thay đổi rơi vào các điểm cấu hình dùng chung
đã tồn tại (`shared/Identity`, `shared/ServiceDefaults`, `DownstreamClientRegistrationExtensions`
của BFF, `appsettings.json` của gateway) — đúng nguyên tắc "cross-cutting concern dùng chung, không
hand-roll từng service" mà `IdentityValidationExtensions` và `ServiceDefaultsExtensions` đã thiết
lập. Dự án quét mới đặt tại `tests/ResilienceCoverageTests`, song song với
`tests/ContractCoverageTests` đã có, giữ đúng khuôn mẫu "scanner filesystem-only, danh sách kỳ vọng
viết tay, không tự khám phá" mà `ContractCoverageScanner` đang dùng.

## Complexity Tracking

> Không có vi phạm Constitution Check nào cần biện minh — bảng này để trống có chủ đích.
