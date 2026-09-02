# Implementation Plan: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

**Branch**: `015-deny-by-default-authz` | **Date**: 2026-09-02 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/015-deny-by-default-authz/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

[014-identity-server-auth](../014-identity-server-auth/) đóng deviation về **xác thực** (Principle VI, nửa đầu): mọi service tự xác thực token độc lập, `FallbackPolicy = RequireAuthenticatedUser()`. Chính comment trong `shared/Identity/AuthenticationFallbackPolicy.cs` đã dự đoán trước phần còn lại: "Fine-grained RBAC/scope policies are a separate concern layered on top of this default... not part of this feature's scope." Tính năng này (SCRUM-24) chính là lớp đó — nửa sau của Principle VI, **phân quyền** (authorization), không phải xác thực lại.

Rà soát mã nguồn cho thấy một khoảng trống thật đang tồn tại: `FallbackPolicy` hôm nay chỉ kiểm tra "đã đăng nhập", không kiểm tra token có mang đúng scope API (`ecommerce-api`, đã được `services/identity/src/Identity.Api/Config.cs` đăng ký và cấp cho client SPA lẫn client kiểm thử từ 014) hay không. Tính năng này nâng chính sách đó lên `AuthorizationPolicies.ApiScope` (yêu cầu xác thực + claim `scope=ecommerce-api`, research.md Decision 1), bọc trong một toggle Unleash mới để có thể rollback không cần redeploy (Decision 5), khai báo tường minh chính sách đó tại từng route của BFF và 4 domain service (Decision 2), và thêm một scanner test cấu trúc mới — `AuthorizationPolicyDeclaredScanner`, mirror `AuthenticatedByDefaultScanner` của 014 — chặn merge bất kỳ endpoint hay `IConsumer<T>` nào thiếu khai báo (Decision 3/4). US3 không thêm quy tắc nghiệp vụ mới; nó lập một hợp đồng đối chiếu tường minh cho các cặp kiểm tra SPA/máy chủ đã tồn tại (giỏ hàng không rỗng khi checkout, số lượng/đơn giá hợp lệ) và bổ sung integration test gọi thẳng API để chứng minh bằng test tự động, không chỉ bằng lời (Decision 7).

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, khớp mọi service khác; constitution Technology Constraints)

**Primary Dependencies**: Không có gói NuGet mới — mở rộng `Microsoft.AspNetCore.Authorization` (đã có sẵn trong ASP.NET Core, dùng cho `RequireClaim`/custom `IAuthorizationRequirement`) bên trong `shared/Identity` (014). Unleash .NET SDK (ADR-0008), đã dùng ở gateway từ 014, được tái sử dụng cho toggle mới.

**Storage**: N/A — không có database mới, không có schema thay đổi ở bất kỳ service nào.

**Testing**: xUnit, khớp mọi service khác. Mở rộng `tests/CrossServiceIsolation.Tests` với `AuthorizationPolicyDeclaredScannerTests.cs` (mới); thêm `AuthorizationPolicyTests.cs` vào từng `*.Api.IntegrationTests` của bff/baskets/orders/parties/products (mirror `IndependentTokenValidationTests.cs`); mở rộng `IntegrationTestSupport.TestJwtBearer` để phát hành token có/không có claim `scope`; bổ sung test đối chiếu validation ở `Baskets.Api.IntegrationTests`/`Bff.Api.IntegrationTests`/`Orders.Api.IntegrationTests` theo `contracts/client-server-validation-parity-contract.md`.

**Target Platform**: Linux containers trên Kubernetes (hạ tầng hiện có — không có hạ tầng mới).

**Project Type**: web-service — không có service triển khai được mới; thay đổi có mục tiêu ở `shared/Identity` (thư viện chia sẻ đã có), BFF, 4 domain service, gateway (gián tiếp qua `FallbackPolicy` dùng chung), và `tests/CrossServiceIsolation.Tests`.

**Performance Goals**: Không có ngân sách mới cần khai báo — việc kiểm tra claim `scope` là một phép so sánh chuỗi trên `ClaimsPrincipal` đã được JwtBearer phân giải sẵn trong bộ nhớ, không có lệnh gọi mạng hay truy vấn dữ liệu bổ sung nào trên đường đi của request (constitution Principle VIII).

**Constraints**: Cơ chế lan truyền tenant/subject hiện có (`shared/Tenancy`, header `X-Tenant-Id`/`X-Subject-Id`) PHẢI giữ nguyên không đổi (spec Assumptions) — chính vì vậy chính sách phân quyền mới dựa trên claim `scope` đọc trực tiếp từ token, không dựa trên `TenantContext`/`CallerContext` (research.md Decision 1, phương án bị loại). Thứ tự middleware hiện có (`UseIdentityValidation()` trước `UseTenancy()`) không đổi. Việc nâng cấp `FallbackPolicy` PHẢI có khả năng rollback không cần redeploy (constitution Principle X; research.md Decision 5) vì nó chạm tới đường đi của mọi request nghiệp vụ trên mọi service.

**Scale/Scope**: Chạm tới `shared/Identity`, BFF (5 file `*Endpoints.cs`), 4 domain service (mỗi service 1 file `*Endpoints.cs` nghiệp vụ), gateway (chỉ qua `FallbackPolicy` dùng chung, không có route nghiệp vụ riêng để sửa), và `tests/CrossServiceIsolation.Tests`. Không có công việc SPA/frontend mới (US3 chỉ lập tài liệu đối chiếu và test phía backend cho các quy tắc SPA đã tồn tại sẵn — research.md Decision 7).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | Không service nào đọc/ghi database của service khác; không có database mới. `shared/Identity` vẫn là nơi duy nhất định nghĩa chính sách dùng chung — mỗi service chỉ gọi, không tự định nghĩa lại (mirror 014 Decision 4). | PASS |
| II. Contract-First Integration | Ba hợp đồng viết trước khi triển khai: `contracts/authorization-policy-contract.md`, `contracts/message-handler-authorization-contract.md`, `contracts/client-server-validation-parity-contract.md`. | PASS |
| III. Test-First Development | Scanner test mới (`AuthorizationPolicyDeclaredScannerTests`) và integration test mới (`AuthorizationPolicyTests`, test đối chiếu validation) được viết và xác nhận thất bại trước khi triển khai chính sách — thực thi ở giai đoạn `/speckit-tasks`/`/speckit-implement`, cùng cách 014 đã xử lý gate này. | PASS (deferred to tasks) |
| IV. Event-Driven by Default | N/A cho hành vi runtime — tính năng này không thêm giao tiếp bất đồng bộ nào; `contracts/message-handler-authorization-contract.md` chỉ là một guard cấu trúc phòng ngừa cho công việc hướng sự kiện tương lai (research.md Decision 4). | PASS (N/A) |
| V. Tenant Isolation Is a Security Boundary | Cơ chế lan truyền tenant hiện có không đổi (Technical Context — Constraints); chính sách phân quyền mới cố ý không đọc `TenantContext`/`CallerContext` để tránh đảo thứ tự middleware đã ổn định (research.md Decision 1). | PASS |
| VI. Secure by Default | Tính năng này **khép lại** nửa sau của deviation Principle VI mà `AuthenticationFallbackPolicy.cs` (014) đã ghi chú và để ngỏ: chính sách phân quyền tường minh theo claim `scope`, khai báo tường minh tại từng endpoint (FR-001/FR-002), cổng build/review chặn endpoint/handler thiếu khai báo (FR-004/FR-008), và kiểm tra dữ liệu phía máy chủ độc lập được xác nhận bằng test tự động thay vì chỉ bằng lời (FR-006/FR-007, US3). | PASS |
| VII. Observable by Default | Không có wiring quan sát tuỳ biến mới; phản hồi 403 mới (research.md Decision 6) là một cải thiện observability nhỏ, nhất quán với `ClearUnauthorizedResponseEvents` đã có. | PASS |
| VIII. Performance and Resilience Budgets | Không có outbound call mới, không có ngân sách mới cần khai báo (Technical Context — Performance Goals). | PASS |
| IX. Frontend Discipline | N/A — không có mã frontend nào thay đổi; US3 chỉ đọc và lập tài liệu đối chiếu với mã SPA đã có, không sửa nó (research.md Decision 7). | PASS (N/A) |
| X. Toggle-Gated, Reversible Delivery | Việc nâng cấp `FallbackPolicy`/chính sách `ApiScope` được bọc trong một toggle Unleash mới, có chủ sở hữu và ngày gỡ bỏ ghi nhận khi tạo (research.md Decision 5) — cùng khuôn mẫu 014 đã dùng cho việc chuyển scheme ở gateway. Bản thân sự khai báo tường minh tại từng route (FR-001) KHÔNG phụ thuộc toggle — chỉ nội dung nghiêm ngặt của chính sách mới bị toggle chi phối. | PASS |

Không có deviation nào cần Complexity Tracking. Tính năng này đóng vai trò **khép lại** nửa còn lại của deviation Principle VI mà 014 đã để ngỏ có chủ đích (theo dõi bởi SCRUM-24), thay vì mang theo một deviation mới.

## Project Structure

### Documentation (this feature)

```text
specs/015-deny-by-default-authz/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
│   ├── authorization-policy-contract.md
│   ├── message-handler-authorization-contract.md
│   └── client-server-validation-parity-contract.md
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
├── Identity/                              # existing (014) — extended, not replaced
│   ├── AuthenticationFallbackPolicy.cs        # nâng cấp: + RequireClaim("scope", ApiScopeName), toggle-gated
│   ├── AuthorizationPolicies.cs               # NEW — hằng số tên chính sách ("ApiScope") dùng chung mọi service
│   ├── ClearForbiddenResponseEvents.cs         # NEW — thân JSON rõ ràng cho 403 (research.md Decision 6), mirror ClearUnauthorizedResponseEvents
│   └── IdentityValidationExtensions.cs         # cập nhật: đăng ký ClearForbiddenResponseEvents + policy ApiScope
└── IntegrationTestSupport/
    └── TestJwtBearer.cs                    # cập nhật: CreateToken(includeApiScope: true mặc định) — giữ test hiện có không đổi hành vi

services/
├── gateway/src/Gateway.Api/Identity/
│   └── ToggleGatedAuthenticationExtensions.cs   # dùng lại AuthenticationFallbackPolicy.Build() đã nâng cấp — không cần sửa route (chỉ có 1 catch-all proxy + 2 health probe đã AllowAnonymous)
│
├── bff/src/Bff.Api/Features/
│   ├── Baskets/BasketsEndpoints.cs         # + .RequireAuthorization(AuthorizationPolicies.ApiScope) mỗi route
│   ├── Checkout/CheckoutEndpoints.cs       # same
│   ├── Orders/OrdersEndpoints.cs           # same
│   ├── Parties/PartiesEndpoints.cs         # same
│   └── Products/ProductsEndpoints.cs       # same
├── baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs   # same
├── orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs       # same
├── parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs    # same
└── products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs # same
# */Features/HealthCheck/HealthCheckEndpoints.cs ở mọi service — không đổi, đã .AllowAnonymous() từ 014

tests/
└── CrossServiceIsolation.Tests/
    ├── AuthorizationPolicyDeclaredScanner.cs        # NEW — mirror AuthenticatedByDefaultScanner.cs
    └── AuthorizationPolicyDeclaredScannerTests.cs   # NEW — mirror AuthenticatedByDefaultScannerTests.cs

# Mỗi service's *.Api.IntegrationTests/ (baskets, bff, orders, parties, products):
#   AuthorizationPolicyTests.cs      # NEW — mirror IndependentTokenValidationTests.cs, dùng TestJwtBearer đã cập nhật
# Baskets.Api.IntegrationTests/, Bff.Api.IntegrationTests/, Orders.Api.IntegrationTests/:
#   test bổ sung theo contracts/client-server-validation-parity-contract.md (một số quy tắc đã có test — bổ sung phần thiếu)
```

**Structure Decision**: Không có project/service mới. Mọi thay đổi là bổ sung trên nền `shared/Identity` (014) và các `*Endpoints.cs` đã tồn tại từ [002-gateway-bff-routing](../002-gateway-bff-routing/)/[004-minimal-shopping-spa](../004-minimal-shopping-spa/)/[006-e2e-order-demo](../006-e2e-order-demo/) — đúng khuôn mẫu "mối quan tâm xuyên suốt, dùng giống hệt ở mọi nơi" mà 014 đã thiết lập cho `shared/Identity`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Không có violation nào trong Constitution Check ở trên — bảng này để trống có chủ đích.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

Thiết kế Phase 1 không thêm violation mới:

- **Contract-first (II)** được thoả bởi ba hợp đồng trong `contracts/` tồn tại trước khi triển khai, ghi lại producer/consumer/failure-mode của cả chính sách endpoint, quyết định tin cậy handler, lẫn đối chiếu validation.
- **Không endpoint/handler nào thiếu quyết định phân quyền (VI; spec FR-001/FR-002/FR-004)** được thoả cấu trúc bởi `data-model.md`'s Endpoint Authorization Declaration/Handler Trust Declaration và bởi `research.md` Decision 3/4 — scanner test đọc mã nguồn tĩnh, không phụ thuộc kỷ luật thủ công của người viết code.
- **Chỉ nội dung chính sách bị toggle chi phối, không phải sự khai báo (X; research.md Decision 5)** được thoả bởi thiết kế `AuthorizationPolicies.ApiScope`: khai báo `.RequireAuthorization(ApiScope)` tại route là tĩnh, không đổi theo toggle; toggle chỉ ảnh hưởng bên trong `AuthenticationFallbackPolicy.Build()`/policy handler khi đánh giá yêu cầu — đúng như `quickstart.md` Scenario 5 kiểm chứng được.
- **Cơ chế lan truyền tenant/subject không bị chạm tới (V; spec Assumptions)** được thoả bởi `research.md` Decision 1 — chính sách mới đọc claim `scope` trực tiếp từ `ClaimsPrincipal` đã xác thực, không đọc `TenantContext`/`CallerContext`, nên thứ tự middleware hiện có không cần đảo.
- **Kiểm tra dữ liệu phía máy chủ độc lập được chứng minh, không chỉ khẳng định (VI; spec US3)** được thoả bởi `contracts/client-server-validation-parity-contract.md`'s bảng đối chiếu cộng yêu cầu integration test bắt buộc cho mỗi dòng — `quickstart.md` Scenario 4 là ví dụ chạy được, không phải mô tả suông.
- Deviation Principle VI (nửa "phân quyền") mà [014-identity-server-auth](../014-identity-server-auth/plan.md) để ngỏ có chủ đích được khép lại hoàn toàn bởi thiết kế này — không có deviation nào được mang tiếp.

Gate: **PASS** (không có deviation nào cần theo dõi tiếp).
