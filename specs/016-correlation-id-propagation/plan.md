# Implementation Plan: Lan truyền Correlation ID từ Edge đến Frontend

**Branch**: `016-correlation-id-propagation` | **Date**: 2026-09-06 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/016-correlation-id-propagation/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Hạ tầng correlation ID (`shared/ServiceDefaults/CorrelationIdMiddleware.cs`) đã tồn tại và đã wire ở cả 7 service từ trước — sinh/giữ nguyên ID tại gateway, echo trên response, đưa vào scope log và tag OTel. Rà soát mã nguồn cho thấy một bug thật đang tồn tại đúng ở nơi quan trọng nhất: hop **BFF → 4 domain service** (baskets/orders/parties/products) dùng typed `HttpClient`, và không có gì relay `X-Correlation-Id` sang đó — `TenantPropagationHandler` đã relay tenant/subject/`Authorization` nhưng không relay correlation ID. Vì 4 domain service chỉ được gọi qua BFF (Principle IX), đây là con đường của 100% traffic thật: mỗi request tới baskets/orders/parties/products hôm nay tự sinh một correlation ID mới, phá vỡ đúng giá trị cốt lõi mà US1 yêu cầu (research.md Decision 1).

Tính năng này (SCRUM-26) sửa đúng khoảng trống đó — mở rộng `TenantPropagationHandler` để relay thêm `X-Correlation-Id` — và bổ sung 3 phần còn thiếu: (1) validate nhẹ giá trị client cung cấp (chặn ký tự điều khiển/độ dài quá mức, không ép định dạng GUID để không phá vỡ test hợp đồng hiện có — Decision 2); (2) expose `X-Correlation-Id` qua CORS ở gateway và đọc nó vào `ApiError` ở `fetcher.ts`, để SPA đọc được giá trị này bằng code chứ không chỉ qua DevTools thủ công (Decision 5); (3) một test đồng thời mới nhắm đúng vào hop vừa sửa để chứng minh không lẫn correlation ID dưới tải đồng thời (Decision 7). Phần lan truyền qua RabbitMQ/message bất đồng bộ **không** được xây trong tính năng này — hợp đồng dữ liệu (`correlationId` trên `BasketCheckedOutV1`/`OrderPlacedV1`) đã sẵn sàng từ SCRUM-18, nhưng publisher/consumer thật là công việc của SCRUM-31 (chưa bắt đầu, sprint-3/phase-4) — xây nó trong ticket này sẽ lấn phạm vi một ticket khác (Decision 3). Việc kiểm chứng cục bộ dùng log debug của OTel Collector thay vì Kibana/Elastic thật, vì việc dựng Elastic là SCRUM-25 (cũng chưa bắt đầu, Decision 4).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, khớp mọi service khác) cho phần backend; TypeScript strict cho phần frontend (`frontend/packages/api-client`) — constitution Technology Constraints.

**Primary Dependencies**: Không có gói NuGet/npm mới. Mở rộng mã đã có: `TenantPropagationHandler` (`Microsoft.Extensions.Http`, đã dùng), `CorrelationIdMiddleware` (`System.Diagnostics.Activity`/`Microsoft.Extensions.Logging`, đã dùng), `Microsoft.AspNetCore.Cors` (đã dùng ở gateway, chỉ thêm `WithExposedHeaders`), và `fetcher.ts` (Fetch API thuần, không có thư viện HTTP nào).

**Storage**: N/A — không có database mới, không có schema thay đổi ở bất kỳ service nào.

**Testing**: xUnit, khớp mọi service khác. Mở rộng `Gateway.Api.IntegrationTests/CorrelationIdPropagationTests.cs` (ràng buộc hợp lệ mới); thêm `Bff.Api.IntegrationTests/CorrelationPropagationTests.cs` (mới, mirror `TenantPropagationTests.cs`, gồm biến thể đồng thời). Vitest cho frontend: thêm `frontend/apps/web/tests/shared/fetcher.test.ts` (mới, dùng MSW mock đã có ở `tests/msw/server.ts`).

**Target Platform**: Linux containers trên Kubernetes / Docker Compose local (hạ tầng hiện có — không có hạ tầng mới; không cần Elastic/Kibana, xem Constraints).

**Project Type**: web-service + web frontend monorepo — không có service/app mới. Thay đổi có mục tiêu ở `shared/ServiceDefaults` (validate), BFF (`DownstreamClients/TenantPropagationHandler.cs`), gateway (`Program.cs` CORS), và `frontend/packages/api-client`.

**Performance Goals**: Không có ngân sách mới cần khai báo — relay correlation ID là copy một giá trị chuỗi đã có sẵn trong `HttpContext.Items` lên một header của request đã được dựng sẵn, không có lệnh gọi mạng hay truy vấn dữ liệu bổ sung nào trên đường đi của request (constitution Principle VIII).

**Constraints**: Cơ chế relay tenant/subject/`Authorization` hiện có trong `TenantPropagationHandler` PHẢI giữ nguyên hành vi (chỉ thêm, không sửa 3 dòng `Relay(...)` đã có). Ràng buộc hợp lệ mới cho correlation ID do client cung cấp PHẢI tương thích với `ACallerSuppliedCorrelationId_IsPreservedEndToEnd` hiện có (giá trị `"caller-supplied-correlation-id"`, không phải GUID, phải tiếp tục được giữ nguyên — research.md Decision 2). Không xây publisher/consumer RabbitMQ thật (ranh giới với SCRUM-31, Decision 3). Không cần Elastic/Kibana thật để kiểm chứng (ranh giới với SCRUM-25, Decision 4). Không cần feature toggle mới — các thay đổi không đổi mã trạng thái/thân phản hồi nghiệp vụ nào (Decision 6).

**Scale/Scope**: Chạm tới `shared/ServiceDefaults/CorrelationIdMiddleware.cs` (thêm validate), `services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs` (thêm 1 dòng relay), `services/gateway/src/Gateway.Api/Program.cs` (thêm `WithExposedHeaders`), `frontend/packages/api-client/src/http/fetcher.ts` (đọc header, mở rộng `ApiError`). Không có service/route/schema mới. Test mới ở `Gateway.Api.IntegrationTests`, `Bff.Api.IntegrationTests`, và `frontend/apps/web/tests/shared`.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không service nào đọc/ghi database của service khác; không có database/service mới. Thay đổi duy nhất ở tầng cross-cutting đã có (`ServiceDefaults`, `TenantPropagationHandler`) — không thêm ceremony kiến trúc mới. | PASS |
| II. Contract-First Integration | Ba hợp đồng viết trước khi triển khai: `contracts/correlation-id-header-contract.md`, `contracts/event-correlation-field-contract.md`, `contracts/spa-correlation-visibility-contract.md`. | PASS |
| III. Test-First Development | Test mới (`CorrelationPropagationTests`, biến thể đồng thời, `fetcher.test.ts`, mở rộng `CorrelationIdPropagationTests`) được viết và xác nhận thất bại trước khi sửa `TenantPropagationHandler`/`CorrelationIdMiddleware`/`fetcher.ts` — thực thi ở giai đoạn `/speckit-tasks`/`/speckit-implement`, cùng cách 015 đã xử lý gate này. | PASS (deferred to tasks) |
| IV. Event-Driven by Default | Không thêm publisher/consumer/outbox thật nào — nằm ngoài phạm vi tính năng này, thuộc SCRUM-31 (research.md Decision 3). Hợp đồng dữ liệu (`correlationId` trên event) đã đúng từ SCRUM-18, không đổi. | PASS (N/A/deferred, ranh giới ghi rõ ở contracts/event-correlation-field-contract.md) |
| V. Tenant Isolation Is a Security Boundary | Cơ chế lan truyền tenant hiện có trong `TenantPropagationHandler` không đổi — correlation ID được relay bằng một dòng `Relay(...)` độc lập, không chạm tới logic tenant/subject đã có. | PASS |
| VI. Secure by Default | Correlation ID không phải PII/secret (spec Assumptions) nên an toàn khi expose qua CORS. Ràng buộc hợp lệ mới (Decision 2) đóng một lỗ hổng log injection thật (input từ client đi thẳng vào log có cấu trúc mà không qua kiểm tra ký tự) — đúng tinh thần "OWASP Top 10 mitigations apply to every externally reachable surface" cho bề mặt gateway. Không đổi authn/authz. | PASS |
| VII. Observable by Default | Đây CHÍNH LÀ tính năng hiện thực hoá trực tiếp câu "A correlation ID MUST be generated at the edge and propagated across every synchronous call and every message, including through the frontend" — sửa đúng khoảng trống khiến câu đó chưa đúng ở hop BFF → domain service. | PASS (core) |
| VIII. Performance and Resilience Budgets | Không có network call mới, không có ngân sách mới cần khai báo (Technical Context — Performance Goals). | PASS |
| IX. Frontend Discipline | Thay đổi duy nhất ở `packages/api-client` — điểm gọi API chung duy nhất của SPA (đúng nguyên tắc "một generated API client dùng chung"). Không có màn hình nào tự gọi `fetch` riêng, không có state server nào bị copy ra ngoài TanStack Query. | PASS |
| X. Toggle-Gated, Reversible Delivery | Không toggle mới — các thay đổi không đổi mã trạng thái/thân phản hồi nghiệp vụ nào, trường hợp lỗi đã được `Relay(...)` xử lý an toàn (suy thoái về đúng hành vi hôm nay, không throw); đây là loại thay đổi thuần quan sát được (observability-only), không phải loại "non-trivial" mà Principle X nhắm tới (research.md Decision 6, có so sánh tường minh với quyết định toggle của 015). | PASS (justified) |

Không có deviation nào cần Complexity Tracking. Tính năng này là một **bugfix có chủ đích, khoanh vùng hẹp** trên nền hạ tầng observability đã có (Principle VII), không mang theo deviation mới, và giữ ranh giới rõ ràng với hai ticket liên quan chưa bắt đầu (SCRUM-25, SCRUM-31).

## Project Structure

### Documentation (this feature)

```text
specs/016-correlation-id-propagation/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── correlation-id-header-contract.md
│   ├── event-correlation-field-contract.md
│   └── spa-correlation-visibility-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
└── ServiceDefaults/
    └── CorrelationIdMiddleware.cs        # cập nhật: ResolveCorrelationId thêm ràng buộc hợp lệ (research.md Decision 2)

services/
├── gateway/src/Gateway.Api/
│   └── Program.cs                        # cập nhật: CORS policy + .WithExposedHeaders(CorrelationIdMiddleware.HeaderName)
├── gateway/tests/Gateway.Api.IntegrationTests/
│   └── CorrelationIdPropagationTests.cs  # mở rộng: test ràng buộc hợp lệ mới (ký tự điều khiển/độ dài)
│
├── bff/src/Bff.Api/DownstreamClients/
│   └── TenantPropagationHandler.cs       # cập nhật: + Relay(request, CorrelationIdMiddleware.HeaderName, ...) (research.md Decision 1)
└── bff/tests/Bff.Api.IntegrationTests/
    └── CorrelationPropagationTests.cs    # NEW — mirror TenantPropagationTests.cs, + biến thể đồng thời (research.md Decision 7)

# baskets/orders/parties/products: không sửa gì — CorrelationIdMiddleware của mỗi service đã đúng
# sẵn, chỉ cần header thật sự tới nơi (đã sửa ở BFF)

frontend/
└── packages/api-client/src/http/
    └── fetcher.ts                        # cập nhật: đọc response header, ApiError.correlationId mới (research.md Decision 5)

frontend/apps/web/tests/shared/
└── fetcher.test.ts                       # NEW — xác nhận ApiError.correlationId qua MSW mock
```

**Structure Decision**: Không có project/service/app mới. Mọi thay đổi là bổ sung có mục tiêu trên nền `shared/ServiceDefaults`, `services/bff/src/Bff.Api/DownstreamClients` (đã tồn tại từ [002-gateway-bff-routing](../002-gateway-bff-routing/)), và `frontend/packages/api-client` (đã tồn tại từ [004-minimal-shopping-spa](../004-minimal-shopping-spa/)) — đúng khuôn mẫu "sửa một khoảng trống trong một cơ chế cross-cutting đã có", không phải xây một cơ chế mới.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Không có violation nào trong Constitution Check ở trên — bảng này để trống có chủ đích.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

Thiết kế Phase 1 không thêm violation mới:

- **Contract-first (II)** được thoả bởi ba hợp đồng trong `contracts/`, mỗi hợp đồng ghi rõ Producers/Consumers/Failure Modes/Stability, tồn tại trước khi triển khai.
- **Correlation ID lan truyền đúng qua mọi hop đồng bộ (VII; spec FR-001 → FR-005)** được thoả cấu trúc bởi `data-model.md`'s bảng "Hop lan truyền" — chỉ ra chính xác một hop từng sai (BFF → domain service) và cách nó được sửa, không phải một khẳng định chung chung.
- **Không phá vỡ hợp đồng đã kiểm thử khi thêm validate (VI; research.md Decision 2)** được thoả bởi việc chọn ràng buộc "chặn ký tự điều khiển + giới hạn độ dài" thay vì "ép định dạng GUID" — `quickstart.md` Scenario 4 kiểm chứng được giá trị không-phải-GUID vẫn sống sót, Scenario 5 kiểm chứng giá trị độc hại bị chặn.
- **Ranh giới rõ ràng với SCRUM-31/SCRUM-25 (IV; research.md Decision 3/4)** được thoả bởi `contracts/event-correlation-field-contract.md`'s bảng "Ranh giới phạm vi" — phân định tường minh việc gì thuộc tính năng này, việc gì thuộc ticket khác, thay vì để ngỏ mơ hồ.
- **SPA đọc được correlation ID bằng code, không chỉ qua quan sát thủ công (IX; research.md Decision 5)** được thoả bởi `contracts/spa-correlation-visibility-contract.md` và test tự động mới `fetcher.test.ts` — `quickstart.md` Scenario 7 có cả phần thủ công (DevTools) lẫn phần tự động, không chỉ mô tả suông.
- **Không cần toggle mới (X; research.md Decision 6)** được thoả bởi việc mọi thay đổi đều là bổ sung an toàn (fail-safe qua `Relay(...)` đã có), không đổi hành vi quan sát được của bất kỳ actor nghiệp vụ nào — khác hẳn loại thay đổi mà 015 phải toggle-gate.
- Không có deviation nào được mang tiếp từ tính năng này sang tính năng sau.

Gate: **PASS** (không có deviation nào cần theo dõi tiếp).
