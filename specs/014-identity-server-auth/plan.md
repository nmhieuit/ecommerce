# Implementation Plan: Triển khai máy chủ định danh, thay thế xác thực giả lập

**Branch**: `014-identity-server-auth` | **Date**: 2026-09-01 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `/specs/014-identity-server-auth/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

Đứng lên một máy chủ định danh thực sự — **Duende IdentityServer** (đã chốt ở [ADR-0001](../../docs/adr/0001-identity-provider.md)), triển khai như một service ASP.NET Core mới `services/identity`, đúng khuôn mẫu container/pipeline/Ansible như mọi service khác. Ở gateway, đúng một dòng đăng ký thay đổi: `AddScheme<StubIdentityAuthenticationSchemeOptions, StubIdentityAuthenticationHandler>(...)` được thay bằng `AddJwtBearer(...)` trỏ tới `Authority` của máy chủ định danh mới — như chính comment trong `Program.cs` hiện tại đã dự đoán ("Phase 3 (SCRUM-23) swaps this one registration for AddJwtBearer(...) and nothing downstream changes"). Vì token thật phát hành đúng hai claim mà gateway đã đọc từ lâu (`tenant_id` và `sub`), toàn bộ cơ chế lan truyền hiện có — `TenantHeaderPropagationMiddleware`, `SubjectHeaderPropagationMiddleware`, thư viện chia sẻ `shared/Tenancy` — không cần sửa một dòng nào (spec FR-008).

Phần việc mới thực sự nằm ở lớp phòng thủ theo chiều sâu (spec US2): hiện tại BFF và cả bốn domain service (parties, products, baskets, orders) hoàn toàn không xác thực gì cả — mọi `service-manifest.yaml` đều ghi `authentication: anonymous` với chú thích "no identity server yet". Tính năng này thêm việc xác thực JWT độc lập vào cả năm service đó qua một thư viện chia sẻ mới `shared/Identity` (cùng khuôn mẫu với `shared/Tenancy`), cấu hình xác thực chữ ký cục bộ qua JWKS đã cache — không gọi trực tiếp máy chủ định danh mỗi request — và áp dụng chính sách mặc định "yêu cầu đăng nhập" (deny-by-default) cho mọi endpoint trừ các health probe. Việc chuyển đổi scheme ở gateway được bọc trong một toggle Unleash ([ADR-0008](../../docs/adr/0008-feature-toggle-system.md)) để có thể rollback tức thời nếu cutover phát sinh sự cố.

## Technical Context

**Language/Version**: C# / .NET 10 (`net10.0`, khớp mọi service khác trong hệ thống; constitution Technology Constraints)

**Primary Dependencies**: **Duende IdentityServer** (ADR-0001) cho service `identity` mới · `Microsoft.AspNetCore.Authentication.JwtBearer` (extension point chuẩn của ASP.NET Core, không phải gói tuỳ biến) cho gateway + BFF + 4 domain service · một thư viện chia sẻ mới `shared/Identity` (sibling của `shared/Tenancy`, đóng gói cấu hình `AddJwtBearer`/`FallbackPolicy` dùng chung) · Unleash .NET SDK (ADR-0008) cho toggle cutover ở gateway

**Storage**: SQL Server mới, riêng cho service `identity` (database-per-service, đúng constitution Technology Constraints) — lưu Duende's configuration/operational store (client, resource, persisted grant) và kho thông tin đăng nhập người dùng (ASP.NET Core Identity), **tách biệt** khỏi database của `parties` (research.md Decision 8). Không có database mới nào cho gateway, BFF, hay 4 domain service hiện có.

**Testing**: xUnit, khớp mọi service khác. Coverage mới: `Identity.Api.IntegrationTests` dùng Testcontainers SQL Server thật cho service `identity` (spec Test Scenario 1 — đăng nhập phát hành token hợp lệ); mở rộng integration test hiện có của gateway, BFF, và 4 domain service để xác nhận từ chối token giả mạo/hết hạn/vắng mặt (spec Test Scenario 2/3, US2/US3); một `Identity.UnitTests` cho thư viện chia sẻ mới, theo đúng tiền lệ `Tenancy.UnitTests`.

**Target Platform**: Linux containers trên Kubernetes (hạ tầng hiện có; service `identity` mới triển khai giống hệt mọi service khác — không hạ tầng mới ngoài một database)

**Project Type**: web-service — một service triển khai được mới (`identity`), một thư viện chia sẻ mới (`shared/Identity`), cộng thay đổi có mục tiêu ở gateway, BFF, và 4 domain service hiện có

**Performance Goals**: Ngân sách mặc định cho internal service API (constitution Principle VIII: p95 ≤ 150ms, p99 ≤ 500ms) áp dụng cho endpoint đăng nhập/phát hành token của service `identity`. Việc xác thực JWT thêm vào mỗi service khác là kiểm tra chữ ký cục bộ dựa trên JWKS đã cache (research.md Decision 5) — không có lệnh gọi mạng bổ sung trên đường đi của mỗi request, nên nằm trong ngân sách hiện có của gateway/BFF/từng service, không cần ngân sách mới.

**Constraints**: Chỉ **nguồn xác định tenant** được phép thay đổi (spec FR-007/FR-008) — cơ chế lan truyền `X-Tenant-Id`/`X-Subject-Id` hiện có (`shared/Tenancy`) PHẢI giữ nguyên không đổi. Việc xác thực token PHẢI không yêu cầu gọi trực tiếp máy chủ định danh trên mỗi request (spec Assumptions/Edge Cases — máy chủ định danh tạm ngưng không được làm mất hiệu lực token cũ còn hạn). Mỗi endpoint PHẢI có một quyết định phân quyền tường minh, không có endpoint nào "quên" xác thực (constitution Principle VI). Việc chuyển scheme ở gateway PHẢI có khả năng rollback không cần redeploy (constitution Principle X).

**Scale/Scope**: Chạm tới gateway, BFF, cả 4 domain service (parties, products, baskets, orders) từ [002-gateway-bff-routing](../002-gateway-bff-routing/) và [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/), cộng một service mới (`identity`) và một thư viện chia sẻ mới (`shared/Identity`). Không có công việc SPA/frontend mới — trang đăng nhập tương tác do Duende tự host (research.md Decision 9), không xây trong frontend monorepo.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Status |
|---|---|---|
| I. Service Autonomy and Bounded Context | Service `identity` mới sở hữu database của riêng nó (kho thông tin đăng nhập), tách biệt khỏi database `parties` — hai bên chỉ liên kết qua claim `sub`, không chia sẻ bảng/schema (research.md Decision 8). Không service nào đọc/ghi database của service khác. | PASS |
| II. Contract-First Integration | Hợp đồng claim của token (`contracts/identity-token-claims-contract.md`) và hợp đồng xác thực dịch vụ (`contracts/service-authentication-contract.md`) được viết trước khi triển khai — đây là giao diện duy nhất tính năng này thêm vào. | PASS |
| III. Test-First Development | Test cho US1 (phát hành token), US2 (xác thực độc lập ở từng service), US3 (từ chối token hết hạn) được viết và xác nhận thất bại trước khi triển khai — thực thi ở giai đoạn `/speckit-tasks`/`/speckit-implement`, cùng cách [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/plan.md) đã xử lý gate này. | PASS (deferred to tasks) |
| IV. Event-Driven by Default | N/A — tính năng này không thêm giao tiếp bất đồng bộ nào; đăng nhập và xác thực token là các luồng đồng bộ theo đúng bản chất của OIDC/OAuth2. | PASS (N/A) |
| V. Tenant Isolation Is a Security Boundary | Cơ chế lan truyền tenant hiện có (`shared/Tenancy`, `TenantHeaderPropagationMiddleware`) không đổi (spec FR-008); tính năng này chỉ thay **nguồn** xác định tenant — từ giá trị gán cứng sang claim `tenant_id` đã xác minh trong token thật, một nguồn đáng tin cậy hơn, không kém tin cậy hơn. | PASS |
| VI. Secure by Default | Tính năng này **khép lại** deviation Principle VI mà [003-stub-identity-tenant-context/plan.md](../003-stub-identity-tenant-context/plan.md) đã ghi nhận và theo dõi bởi SCRUM-23: xác thực trung tâm qua máy chủ định danh thật, xác thực độc lập ở gateway VÀ từng service (spec US2), và chính sách phân quyền mặc định "yêu cầu đăng nhập" tường minh cho mọi endpoint trừ health probe (research.md Decision 6) — thoả cả hai vế "xác thực" và "một quyết định phân quyền tường minh" của nguyên tắc. RBAC/scope chi tiết theo vai trò nằm ngoài phạm vi Jira SCRUM-23 và không được nguyên tắc này yêu cầu ở mức tính năng. | PASS |
| VII. Observable by Default | Service `identity` mới dùng `shared/ServiceDefaults` giống mọi service khác — không có wiring quan sát tuỳ biến. | PASS |
| VIII. Performance and Resilience Budgets | Service `identity` khai báo SLO trong `service-manifest.yaml` của chính nó (ngân sách mặc định internal-service-api). Xác thực JWT ở các service khác là một phép kiểm tra chữ ký cục bộ, không có outbound call mới, không có timeout mới cần khai báo. | PASS |
| IX. Frontend Discipline | N/A — không có mã frontend nào trong tính năng này; trang đăng nhập do Duende tự host (research.md Decision 9). | PASS (N/A) |
| X. Toggle-Gated, Reversible Delivery | Việc chuyển scheme xác thực ở gateway (StubIdentity → JwtBearer) được bọc trong một toggle Unleash có chủ sở hữu và ngày gỡ bỏ ghi nhận khi tạo (research.md Decision 7) — khác với [003](../003-stub-identity-tenant-context/plan.md)'s hạ tầng nền tảng không thể tắt, việc bật/tắt giữa hai scheme ở đây khả thi và mang lại rollback thật sự không cần redeploy. | PASS |

Không có deviation nào cần Complexity Tracking. Tính năng này đóng vai trò **khép lại** deviation Principle VI duy nhất mà [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/plan.md) mang theo (theo dõi bởi SCRUM-23), thay vì mang theo một deviation mới.

## Project Structure

### Documentation (this feature)

```text
specs/014-identity-server-auth/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md         # Phase 1 output (/speckit-plan command)
├── quickstart.md         # Phase 1 output (/speckit-plan command)
├── contracts/            # Phase 1 output (/speckit-plan command)
├── checklists/
│   └── requirements.md
└── tasks.md              # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

```text
shared/
├── ServiceDefaults/              # existing — unchanged
├── Tenancy/                      # existing — unchanged; vẫn đọc đúng claim tenant_id/sub (research.md Decision 3)
├── Identity/                     # NEW — sibling của Tenancy, cùng bố cục phẳng
│   ├── Identity.csproj
│   ├── IdentityValidationExtensions.cs   # AddIdentityValidation() / UseIdentityValidation() — mirrors AddTenancy()/UseTenancy()
│   ├── IdentityServerOptions.cs          # Authority, Audience — bind từ appsettings
│   └── AuthenticationFallbackPolicy.cs   # FallbackPolicy = RequireAuthenticatedUser() (research.md Decision 6)
└── Identity.UnitTests/           # NEW — cấu hình/binding của IdentityServerOptions, theo tiền lệ Tenancy.UnitTests

services/
├── identity/                          # NEW service — Duende IdentityServer
│   ├── src/Identity.Api/
│   │   ├── Program.cs                       # AddServiceDefaults(), Duende bootstrap, EF stores
│   │   ├── HostedIdentity/
│   │   │   └── TenantClaimsProfileService.cs   # IProfileService — phát hành claim tenant_id (research.md Decision 3)
│   │   ├── Data/                            # EF configuration/operational/user-credential stores
│   │   ├── Dockerfile
│   │   ├── appsettings.json
│   │   └── service-manifest.yaml
│   └── tests/
│       ├── Identity.Api.UnitTests/
│       └── Identity.Api.IntegrationTests/   # Testcontainers SQL Server — spec Test Scenario 1
│
├── gateway/src/Gateway.Api/
│   ├── Program.cs                           # AddAuthentication(...).AddJwtBearer(...) thay AddScheme<StubIdentity...>,
│   │                                         # bọc trong toggle Unleash (research.md Decision 7)
│   └── Identity/
│       ├── TenantHeaderPropagationMiddleware.cs   # KHÔNG đổi — vẫn đọc claim "tenant_id"
│       └── SubjectHeaderPropagationMiddleware.cs  # KHÔNG đổi
│   # StubIdentityAuthenticationHandler.cs / StubIdentityAuthenticationSchemeOptions.cs — gỡ bỏ sau khi
│   # toggle cutover được xác nhận ổn định (không gỡ ngay trong tính năng này — xem toggle removal date)
│
├── bff/src/Bff.Api/Program.cs               # + AddIdentityValidation() / UseAuthentication()+UseAuthorization() — MỚI
├── parties/src/Parties.Api/Program.cs       # + AddIdentityValidation() / UseAuthentication()+UseAuthorization() — MỚI
├── baskets/src/Baskets.Api/Program.cs       # same
├── orders/src/Orders.Api/Program.cs         # same
└── products/src/Products.Api/Program.cs     # same

tests/
└── CrossServiceIsolation.Tests/
    └── AuthenticatedByDefaultScannerTests.cs   # NEW — xác nhận mỗi service đăng ký AddIdentityValidation()
                                                  # và mọi endpoint ngoài health probe yêu cầu xác thực
                                                  # (mirrors TenantGatedConnectionScanner từ 003)
```

**Structure Decision**: Một service triển khai được mới (`identity`) theo đúng khuôn mẫu mọi service hiện có, và một thư viện chia sẻ mới (`shared/Identity`) theo đúng tiền lệ `shared/Tenancy` đã lập cho "mối quan tâm xuyên suốt, dùng giống hệt ở mọi nơi, không cấu hình tay từng service". Mọi thay đổi khác là bổ sung trong các dự án đã có từ [001-scaffold-service-shells](../001-scaffold-service-shells/), [002-gateway-bff-routing](../002-gateway-bff-routing/), và [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/) — không có database mới ngoài database riêng của `identity`.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

Không có violation nào trong Constitution Check ở trên — bảng này để trống có chủ đích.

## Post-Design Constitution Re-Check

*Re-evaluated after Phase 1 (research.md, data-model.md, contracts/, quickstart.md) were produced.*

Thiết kế Phase 1 không thêm violation mới:

- **Contract-first (II)** được thoả bởi `contracts/identity-token-claims-contract.md` và `contracts/service-authentication-contract.md` tồn tại trước khi triển khai, ghi lại producer/consumer/failure-mode của cả claim token lẫn hành vi xác thực từng service.
- **Không nguồn xác thực nào bị bỏ qua (VI; spec FR-004/FR-005)** được thoả cấu trúc bởi `data-model.md`'s trạng thái token và bởi `research.md` Decision 6 — `FallbackPolicy = RequireAuthenticatedUser()` nghĩa là không endpoint mới nào có thể "quên" yêu cầu xác thực; chỉ những endpoint đã đánh dấu `[AllowAnonymous]` tường minh (health probe) mới được bỏ qua.
- **Chỉ nguồn thay đổi, cơ chế lan truyền giữ nguyên (FR-008)** được thoả bởi `research.md` Decision 3: ánh xạ claim mặc định của JwtBearer trùng khớp chính xác những gì `TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` đã đọc từ `StubIdentityAuthenticationHandler`, nên hai middleware đó không cần sửa — điều `quickstart.md` Scenario 5 kiểm chứng được, không chỉ là khẳng định suông.
- **Không gọi trực tiếp máy chủ định danh mỗi request (Assumptions/Edge Cases)** được thoả bởi `research.md` Decision 5 — xác thực dựa trên JWKS đã cache, không phải introspection endpoint.
- **Rollback không cần redeploy (X)** được thoả bởi `research.md` Decision 7 — toggle Unleash bọc quanh việc đăng ký scheme ở gateway.
- Deviation Principle VI duy nhất mà [003](../003-stub-identity-tenant-context/plan.md) mang theo được khép lại hoàn toàn bởi thiết kế này — không có deviation nào được mang tiếp.

Gate: **PASS** (không có deviation nào cần theo dõi tiếp).
