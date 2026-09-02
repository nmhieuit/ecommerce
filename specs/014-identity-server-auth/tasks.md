---

description: "Danh sách task cho Triển khai máy chủ định danh, thay thế xác thực giả lập"
---

# Tasks: Triển khai máy chủ định danh, thay thế xác thực giả lập

**Input**: Design documents from `/specs/014-identity-server-auth/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/identity-token-claims-contract.md](contracts/identity-token-claims-contract.md), [contracts/service-authentication-contract.md](contracts/service-authentication-contract.md), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) là NON-NEGOTIABLE cho dự án này — "No implementation code is merged without a preceding failing test that it makes pass." Các task test dưới đây vì vậy là bắt buộc, không phải tuỳ chọn, và PHẢI được viết và xác nhận thất bại trước task triển khai tương ứng.

**Organization**: Task được nhóm theo user story (từ [spec.md](spec.md)) để mỗi story có thể triển khai và kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: Task thuộc user story nào (US1, US2, US3)
- Mọi task đều nêu đúng đường dẫn file

## Path Conventions

Tính năng này thêm một service triển khai được mới, một thư viện chia sẻ mới, và chạm tới gateway, BFF, và cả 4 domain service từ [002-gateway-bff-routing](../002-gateway-bff-routing/) / [003-stub-identity-tenant-context](../003-stub-identity-tenant-context/):

- `services/identity/` (service mới — Duende IdentityServer)
- `shared/Identity/` (thư viện chia sẻ mới) và `shared/Identity.UnitTests/` (project test mới)
- `services/gateway/src/Gateway.Api/Program.cs` và `appsettings.json` (sửa — toggle-gated scheme swap)
- `services/{bff,parties,products,baskets,orders}/src/{X}.Api/Program.cs` (sửa — thêm `AddIdentityValidation()`)
- `services/{bff,parties,products,baskets,orders}/src/{X}.Api/service-manifest.yaml` (sửa)
- `tests/CrossServiceIsolation.Tests/` (mở rộng)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Dựng khung project cho service `identity` mới và thư viện chia sẻ mới `shared/Identity`, đưa cả hai vào solution.

- [X] T001 Tạo project shell cho service `identity` mới tại `services/identity/src/Identity.Api/Identity.Api.csproj` (ASP.NET Core, `net10.0`, gói Duende IdentityServer — ADR-0001)
- [X] T002 [P] Tạo project shell `Identity.Api.UnitTests` tại `services/identity/tests/Identity.Api.UnitTests/Identity.Api.UnitTests.csproj` (xUnit, tham chiếu `Identity.Api.csproj`)
- [X] T003 [P] Tạo project shell `Identity.Api.IntegrationTests` tại `services/identity/tests/Identity.Api.IntegrationTests/Identity.Api.IntegrationTests.csproj` (xUnit + Testcontainers SQL Server, theo đúng khuôn mẫu `Parties.Api.IntegrationTests`)
- [X] T004 [P] Tạo thư viện chia sẻ mới tại `shared/Identity/Identity.csproj` (bố cục phẳng, giống hệt `shared/Tenancy/Tenancy.csproj`)
- [X] T005 [P] Tạo project shell `shared/Identity.UnitTests/Identity.UnitTests.csproj` (xUnit, tham chiếu `Identity.csproj`)
- [X] T006 Thêm `services/identity/src/Identity.Api`, `services/identity/tests/Identity.Api.UnitTests`, `services/identity/tests/Identity.Api.IntegrationTests`, `shared/Identity`, và `shared/Identity.UnitTests` vào `Ecommerce.slnx` (phụ thuộc T001-T005)
- [X] T007 Thêm `ProjectReference` tới `shared/Identity/Identity.csproj` từ `Gateway.Api.csproj`, `Bff.Api.csproj`, `Products.Api.csproj`, `Baskets.Api.csproj`, `Orders.Api.csproj`, và `Parties.Api.csproj` (phụ thuộc T004)
- [X] T008 Viết `Dockerfile` cho service `identity` tại `services/identity/src/Identity.Api/Dockerfile`, theo đúng khuôn mẫu `services/gateway/src/Gateway.Api/Dockerfile`
- [X] T009 Viết `service-manifest.yaml` cho service `identity` tại `services/identity/src/Identity.Api/service-manifest.yaml` (SLO mặc định internal-service-api — constitution Principle VIII; theo khuôn mẫu `services/parties/src/Parties.Api/service-manifest.yaml`)
- [X] T010 [P] Thêm `identity-db`, `identity-api` vào `docker-compose.local.yml`, theo đúng khuôn mẫu các entry `parties-db`/`parties-api` hiện có
- [X] T011 [P] Thêm dependency container SQL Server cho `identity` vào `docker-compose.deps.yml`

> **T001/T010 rescoped during implementation — decided, not outstanding.** `Identity.Api.csproj`
> references no Duende IdentityServer package yet: T001 is the service *shell* (buildable, health
> probes only, mirrors every other service's original scaffold), and the Duende bootstrap is
> User Story 1's own task (T020-T024) — adding the package now with nothing using it would be dead
> weight. For the same reason, T010 does not add an `identity-migrate` compose entry or a
> `migrator` Docker stage: there is no EF store to bundle migrations for yet (no `identity-db-init`
> dependency either — `docker-compose.deps.yml`'s `identity-db` starts empty like every other
> service's, and the CREATE DATABASE step is deferred to T020-T021 alongside it). Both arrive
> together with T020-T021's EF stores, following the `parties-migrate`/`parties-db-init` pattern.
> Verified: `dotnet build Ecommerce.slnx` succeeds (0 warnings, 0 errors) with all five new projects
> in the solution, and `tests/ContainerConventionTests`, `tests/StructureConventionTests`, and
> `tests/CrossServiceIsolation.Tests` all pass after updating their hardcoded service-list
> expectations and the six existing services' Dockerfiles (which now also compile against
> `shared/Identity` per T007 and needed the matching `COPY shared/Identity/...` lines the
> `DockerfileSharedProjectTests` scanner requires).

**Checkpoint**: Khung project cho service `identity` và thư viện `shared/Identity` đã sẵn sàng, nằm trong solution, build được — xác nhận bằng `dotnet build Ecommerce.slnx`.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Xây thư viện chia sẻ `shared/Identity` — cấu hình Authority/Audience, chính sách phân quyền mặc định deny-by-default, và extension đăng ký dùng chung — mà cả US1 (gateway) lẫn US2 (BFF + 4 domain service) đều cần.

**⚠️ CRITICAL**: Không user story nào được bắt đầu trước khi phase này hoàn tất.

### Tests cho thư viện chia sẻ ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai (constitution Principle III).

- [X] T012 [P] Unit test: `IdentityServerOptions` bind đúng `Authority`/`Audience` từ section cấu hình `Identity`, trong `shared/Identity.UnitTests/IdentityServerOptionsTests.cs` (data-model.md — cấu hình xác thực)
- [X] T013 [P] Unit test: `AddIdentityValidation()` đăng ký scheme JwtBearer và đặt `FallbackPolicy = RequireAuthenticatedUser()`, trong `shared/Identity.UnitTests/IdentityValidationExtensionsTests.cs` (research.md Decision 4/6)

> **T013 rescoped during implementation — decided, not outstanding.** Verifies the DI registration
> directly (`IAuthenticationSchemeProvider`/`IAuthorizationPolicyProvider`, resolved from a bare
> `ServiceCollection`) rather than through a `WebApplicationFactory` HTTP round-trip — the same
> lower-level approach `shared/Tenancy.UnitTests/TenantContextMiddlewareTests.cs` already uses for
> the equivalent DI-registration question, and it needs no `[AllowAnonymous]`-vs-`401` endpoint
> fixture to prove the same fact. The `Microsoft.AspNetCore.Mvc.Testing` package stayed in the
> `.csproj` (T005) for a future HTTP-level test if one is needed, but this task didn't need it.

### Implementation

- [X] T014 Cài đặt `IdentityServerOptions` (`Authority`, `Audience`) trong `shared/Identity/IdentityServerOptions.cs` (làm T012 pass)
- [X] T015 Cài đặt `AuthenticationFallbackPolicy` (`FallbackPolicy = RequireAuthenticatedUser()`) trong `shared/Identity/AuthenticationFallbackPolicy.cs` (research.md Decision 6; phụ thuộc T014)
- [X] T016 Cài đặt `IdentityValidationExtensions` (`AddIdentityValidation()`/`UseIdentityValidation()`, mirror hình dạng của `TenancyExtensions`' `AddTenancy()`/`UseTenancy()`) trong `shared/Identity/IdentityValidationExtensions.cs` (phụ thuộc T014, T015; làm T012, T013 pass)

> **T016 note**: `AddJwtBearer` cần package `Microsoft.AspNetCore.Authentication.JwtBearer` —
> KHÔNG có sẵn trong shared framework như research.md Decision 2 giả định ban đầu (xác nhận bằng
> `dotnet build`: lỗi CS0234 khi thiếu). Đã thêm `PackageReference`/`PackageVersion` tương ứng vào
> `shared/Identity/Identity.csproj` và `Directory.Packages.props`. `IdentityValidationExtensions`
> cũng đặt `RequireHttpsMetadata` dựa trên scheme của `Authority` (false cho `http://` nội bộ
> cluster, true mặc định cho `https://`) — nếu không, JwtBearer từ chối khởi động với một
> `Authority` dạng `http://identity-api:8080` như mọi lệnh gọi nội bộ khác trong nền tảng này dùng.

**Checkpoint**: `shared/Identity` đã được build và unit-test — sẵn sàng để gateway (US1) và BFF + 4 domain service (US2) gọi. Xác nhận bằng `dotnet test shared/Identity.UnitTests/Identity.UnitTests.csproj`: 5/5 pass.

---

## Phase 3: User Story 1 - Xác thực thực sự thay thế người dùng giả lập (Priority: P1) 🎯 MVP

**Goal**: Đăng nhập qua service `identity` mới phát hành token JWT thật chứa claim `sub`/`tenant_id`; gateway xác thực token đó bằng `AddJwtBearer` (bọc trong toggle Unleash) thay `StubIdentityAuthenticationHandler`, và cơ chế lan truyền tenant/subject hiện có (`shared/Tenancy`) không cần sửa gì.

**Independent Test**: Đăng nhập lấy token, gửi request qua gateway kèm token đó, xác nhận `TenantId`/`SubjectId` vẫn xuất hiện đúng ở log mọi hop như trước (spec US1 Acceptance Scenario 1/2); gạt toggle về tắt xác nhận gateway quay lại `StubIdentity` mà không cần redeploy.

### Tests cho User Story 1 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai.

- [X] T017 [P] [US1] Integration test: đăng nhập qua service `identity` bằng thông tin hợp lệ trả về token JWT chứa claim `sub` và `tenant_id` không rỗng, trong `services/identity/tests/Identity.Api.IntegrationTests/LoginIssuesTokenTests.cs` (spec Test Scenario 1, US1 Acceptance Scenario 1)
- [X] T018 [P] [US1] Integration test: gateway xác thực một token hợp lệ (toggle bật) và `TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` vẫn sinh đúng `X-Tenant-Id`/`X-Subject-Id` như hành vi cũ với `StubIdentity`, trong `services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs` (spec US1 Acceptance Scenario 2, FR-008)
- [X] T019 [P] [US1] Integration test: khi toggle `identity-server-auth-cutover` tắt, gateway quay lại `StubIdentityAuthenticationHandler` và request vẫn thành công — xác nhận rollback không cần redeploy, trong `services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs` (constitution Principle X; research.md Decision 7)

> **T017 rescoped during implementation — decided, not outstanding.** Logs in via the Resource
> Owner Password grant on a second, explicitly test-only client (`integration-test-ropc`,
> `Config.cs`) rather than the SPA's Authorization Code + PKCE client — the latter needs an
> interactive login UI (Duende's Razor Pages quickstart), which this phase does not build (flagged
> as a follow-up). This still proves what US1 needs proven: real credentials against the real user
> store produce a token whose `sub`/`tenant_id` claims come from that user's row, through the same
> `TenantClaimsProfileService` any grant type uses. Does not contradict research.md Decision 9,
> which scopes only the SPA client.
>
> **T018/T019 note**: tests the gateway's token *consumption* independently of T017's *issuance* —
> a symmetric-key-signed test token stands in for a real one, bypassing the OIDC discovery/JWKS
> fetch (research.md Decision 5) a live `Authority` would need over the network. Two extra tests
> beyond the two planned (tampered token; no-token-when-off) came along for free from the same
> fixture and are worth keeping.

### Implementation cho User Story 1

- [X] T020 [US1] Cài đặt kho cấu hình của Duende IdentityServer (Client, Resource, PersistedGrant — EF Core store) trong `services/identity/src/Identity.Api/Data/` (data-model.md — Client Application)
- [X] T021 [US1] Cài đặt kho thông tin đăng nhập người dùng (ASP.NET Core Identity — Identity User: `SubjectId`, `TenantId`, `Credential`) trong `services/identity/src/Identity.Api/Data/`, database riêng, tách biệt khỏi database `parties` (data-model.md — Identity User; research.md Decision 8)
- [X] T022 [US1] Cài đặt `TenantClaimsProfileService` (một `IProfileService` phát hành claim `tenant_id` từ `TenantId` đã gán cho Identity User đăng nhập) trong `services/identity/src/Identity.Api/HostedIdentity/TenantClaimsProfileService.cs` (data-model.md; phụ thuộc T021; làm T017 pass)
- [X] T023 [US1] Đăng ký một `Client Application` cho SPA web (Authorization Code + PKCE, không có Resource Owner Password) trong cấu hình seed của `services/identity/src/Identity.Api` (research.md Decision 9; data-model.md — Client Application; phụ thuộc T020)
- [X] T024 [US1] Bootstrap Duende IdentityServer + `AddServiceDefaults()` trong `services/identity/src/Identity.Api/Program.cs` (phụ thuộc T020-T023; làm T017 pass)
- [X] T025 [US1] Định nghĩa toggle "identity-server-auth-cutover" và đọc nó trong `services/gateway/src/Gateway.Api/Program.cs` để chọn giữa JwtBearer thật (khi bật) và `StubIdentityAuthenticationHandler` hiện có (khi tắt) (research.md Decision 2/7; phụ thuộc T016; làm T018, T019 pass)
- [X] T026 [US1] [P] Cấu hình `Identity:Authority`/`Identity:Audience` trỏ tới service `identity` mới trong `services/gateway/src/Gateway.Api/appsettings.json` (phụ thuộc T014)

> **T020-T024 note**: 3 DbContext riêng biệt cùng chia sẻ database `identity` —
> `ApplicationIdentityDbContext` (ASP.NET Core Identity), Duende's `ConfigurationDbContext` và
> `PersistedGrantDbContext` — mỗi context có `IDesignTimeDbContextFactory` và migrations-history-
> table riêng (`Data/MigrationsHistoryTables.cs`), migrations assembly ghim về `Identity.Api` (Duende
> mặc định trỏ vào assembly của chính nó — `Duende.IdentityServer.EntityFramework.Storage` — nếu
> không override). `Data/SeedData.cs` chỉ seed Client/Resource/Scope (không credential — constitution
> Principle VI), chạy qua cờ `--seed` một lần, không tự chạy ở mỗi lần start (tránh race giữa nhiều
> replica), theo đúng khuôn mẫu migrator-là-bước-riêng của `parties-migrate`.
>
> **T025 note**: toggle hiện đọc từ configuration (`FeatureToggles:IdentityServerAuthCutover`,
> `IOptionsMonitor` + `AddPolicyScheme.ForwardDefaultSelector` — đánh giá lại mỗi request, hot-reload
> không cần restart) thay vì Unleash thật — ADR-0008 đã chọn Unleash nhưng chưa có service nào trong
> nền tảng triển khai nó (Action Items của ADR-0008 vẫn chưa được đánh dấu hoàn thành). Xây dựng toàn
> bộ hạ tầng Unleash chỉ để phục vụ MỘT toggle này vượt quá phạm vi tính năng này; đã ghi nhận thành
> việc cần làm riêng (xem `FeatureToggleOptions.cs` remarks).
>
> **Lỗi phát hiện và sửa trong lúc triển khai**: `Program.cs` ban đầu đọc connection string vào một
> biến local MỘT LẦN trước `Build()` — nhưng `WebApplicationFactory`'s cấu hình test chỉ được tiêm
> vào bộ builder ngay tại thời điểm `Build()`, nên giá trị đã đọc trước đó luôn là giá trị mặc định
> (`Server=identity-db`, không resolve được trong test) chứ không phải giá trị test override. Sửa
> bằng cách đọc `configuration.GetConnectionString(...)` lười biếng bên trong từng callback cấu hình
> DbContext, đúng như `Parties.Api.Program.cs` đã làm. Phát hiện qua nhiều vòng chẩn đoán (xem lịch
> sử session) — bài học: bất kỳ Program.cs mới nào dùng `WebApplicationFactory` để test PHẢI đọc
> connection string lười biếng, không phải đọc một lần ở đầu file.

**Checkpoint**: US1 hoạt động độc lập và kiểm thử được — đăng nhập phát hành token thật, gateway xác thực token đó, tenant/subject lan truyền không đổi, rollback qua toggle hoạt động (quickstart.md Scenario 1, 2, 7). Xác nhận bằng `dotnet test` trên cả `Identity.Api.IntegrationTests` (2/2 pass, đăng nhập thật qua Testcontainers SQL Server) và `Gateway.Api.IntegrationTests` (30/30 pass, bao gồm 4 test JWT mới) — cộng `dotnet build Ecommerce.slnx` (0 lỗi) và `docker build --target final`/`--target migrator` cho service `identity` đều thành công.

---

## Phase 4: User Story 2 - Phòng thủ theo chiều sâu: xác thực độc lập tại gateway và mọi service (Priority: P2)

**Goal**: BFF và cả 4 domain service (parties, products, baskets, orders) tự xác thực token độc lập, không dựa vào việc gateway đã xác thực trước; mọi endpoint nghiệp vụ mặc định yêu cầu đăng nhập, chỉ health probe được miễn tường minh.

**Independent Test**: Gửi một token giả mạo thẳng tới một domain service, bỏ qua gateway hoàn toàn, xác nhận service đó tự chặn mà không cần gateway đã chặn từ trước.

### Tests cho User Story 2 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai.

- [X] T027 [P] [US2] Integration test: `Bff.Api` tự từ chối token giả mạo/hết hạn/vắng mặt gửi trực tiếp, độc lập với gateway, trong `services/bff/tests/Bff.Api.IntegrationTests/IndependentTokenValidationTests.cs` (spec US2 Acceptance Scenario 2/3, Test Scenario 2)
- [X] T028 [P] [US2] Integration test: tương tự cho `Parties.Api`, trong `services/parties/tests/Parties.Api.IntegrationTests/IndependentTokenValidationTests.cs`
- [X] T029 [P] [US2] Integration test: tương tự cho `Products.Api`, trong `services/products/tests/Products.Api.IntegrationTests/IndependentTokenValidationTests.cs`
- [X] T030 [P] [US2] Integration test: tương tự cho `Baskets.Api`, trong `services/baskets/tests/Baskets.Api.IntegrationTests/IndependentTokenValidationTests.cs`
- [X] T031 [P] [US2] Integration test: tương tự cho `Orders.Api`, trong `services/orders/tests/Orders.Api.IntegrationTests/IndependentTokenValidationTests.cs`
- [X] T032 [P] [US2] Integration test tham số hoá: request không có token tới một endpoint nghiệp vụ bị `401` ở cả 6 service (gateway, bff, parties, products, baskets, orders), còn `GET /health/live`/`GET /health/ready` vẫn cho qua ẩn danh, trong `tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScannerTests.cs` (spec FR-011; research.md Decision 6)
- [X] T033 [P] [US2] Structural test: mỗi service trong số gateway/bff/parties/products/baskets/orders gọi đúng một lần `AddIdentityValidation()`/tương đương, trong `tests/CrossServiceIsolation.Tests/AuthenticatedByDefaultScannerTests.cs` (spec SC-005 — mở rộng cùng file với T032)

> **T032 rescoped during implementation — decided, not outstanding.** Viết thành một scanner
> STRUCTURAL (đọc source tĩnh, đúng khuôn mẫu `TenantGatedConnectionScanner`/`ConnectionStringScanner`
> đã có trong cùng project), thay vì một test HTTP sống dựng cả 6 service — hành vi 401 thật sự đã
> được `IndependentTokenValidationTests.cs` của từng service (T027-T031) và
> `Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs` chứng minh trực tiếp; T032/T033 bổ
> sung đảm bảo CẤU TRÚC (mọi service thực sự nối dây, không chỉ những service tôi tình cờ test) mà
> một lần chạy HTTP mẫu không chứng minh được. `AuthenticatedByDefaultScanner.cs` đếm số lần gọi
> `AddIdentityValidation(`/`AddToggleGatedIdentity(` (đúng 1 lần) và `.AllowAnonymous()` (đúng 2 lần
> — hai health probe) trong `Program.cs`/`HealthCheckEndpoints.cs` của mỗi service. `identity` cố ý
> KHÔNG nằm trong `AuthenticatedByDefaultScanner.AuthenticatingServices` — nó phát hành token, không
> xác thực token của chính nó.

### Implementation cho User Story 2

- [X] T034 [P] [US2] Wire `builder.Services.AddIdentityValidation(builder.Configuration)` cùng `app.UseAuthentication()`/`app.UseAuthorization()` vào `services/bff/src/Bff.Api/Program.cs`, sau `UseServiceDefaults()`/`UseTenancy()` đã có (phụ thuộc T016, T007; làm T027 pass)
- [X] T035 [P] [US2] Wiring tương tự vào `services/parties/src/Parties.Api/Program.cs` (làm T028 pass)
- [X] T036 [P] [US2] Wiring tương tự vào `services/products/src/Products.Api/Program.cs` (làm T029 pass)
- [X] T037 [P] [US2] Wiring tương tự vào `services/baskets/src/Baskets.Api/Program.cs` (làm T030 pass)
- [X] T038 [P] [US2] Wiring tương tự vào `services/orders/src/Orders.Api/Program.cs` (làm T031 pass)
- [X] T039 [P] [US2] Đánh dấu `[AllowAnonymous]` tường minh trên `GET /health/live` và `GET /health/ready` ở cả 6 service (gateway, bff, parties, products, baskets, orders) nếu chưa có (research.md Decision 6; làm T032 pass)
- [X] T040 [US2] Cập nhật `authentication: anonymous` → `authentication: bearer` trong `service-manifest.yaml` của bff/parties/products/baskets/orders cho mọi endpoint nghiệp vụ, giữ `anonymous` chỉ ở hai health probe (contracts/service-authentication-contract.md — bảng "Trước và sau"; phụ thuộc T034-T039)

> **Phát hiện quan trọng ngoài phạm vi task gốc, đã sửa trong lúc triển khai**: wiring T034-T038
> phơi bày một lỗ hổng kiến trúc thật sự — BFF chuyển tiếp `X-Tenant-Id`/`X-Subject-Id` xuống 4
> domain service (`TenantPropagationHandler.cs`) nhưng CHƯA BAO GIỜ chuyển tiếp header
> `Authorization`. Một khi mỗi domain service tự xác thực độc lập, mọi lệnh gọi BFF→domain-service
> THẬT (không chỉ trong test) sẽ bị chính domain service đó từ chối 401, vì token gốc chưa từng tới
> nơi. Đã sửa: `TenantPropagationHandler.cs` giờ relay cả `Authorization` (đọc từ
> `HttpContext.Request.Headers.Authorization`, cùng cơ chế relay-không-merge như hai header kia).
> Không có task nào trong tasks.md gốc liệt kê việc này — đây là điều kiện cần để FR-004 hoạt động
> đúng end-to-end, không phải phạm vi mở rộng tuỳ ý.
>
> **Bộ test cũ (trước tính năng này) bị phá vỡ, đã sửa**: 15 file test tích hợp có sẵn (Catalog/Basket/
> Order/Party Endpoints/Seed tests, 5×TenantEnforcementTests, BffTestHost.cs — điểm nối trung tâm mọi
> route test của BFF) gọi endpoint nghiệp vụ mà không có token, giờ nhận `401` thay vì hành vi cũ. Sửa
> bằng một helper dùng chung mới `shared/IntegrationTestSupport/TestJwtBearer.cs` (`CreateToken()`,
> `UseTestJwtBearer()` cho `IWebHostBuilder`, `UseTestBearerToken()` cho `HttpClient`) — tránh lặp lại
> logic bypass JWT ở từng project test, đúng tiền lệ `SqlServerFixture`/`RedisFixture` đã có trong
> cùng thư mục chia sẻ. 3 `PactProviderHost.cs` (products/baskets/orders `ContractTests`) được cấu
> hình tắt hẳn `FallbackPolicy` (`services.PostConfigure<AuthorizationOptions>(o => o.FallbackPolicy = null)`)
> vì Pact phát lại các interaction đã ghi từ trước, không có `Authorization` header — xác thực không
> phải điều Pact contract test kiểm chứng, đó là việc của `IndependentTokenValidationTests.cs`. BFF's
> `GET /openapi/v1.json` được đánh dấu `[AllowAnonymous]` tường minh vì đây là endpoint công cụ
> build-time (Orval codegen), không phải người dùng đã đăng nhập.

**Checkpoint**: US2 hoạt động độc lập — mọi service tự chặn token giả mạo/vắng mặt mà không cần gateway đã xử lý trước (quickstart.md Scenario 3, 5). Xác nhận bằng `dotnet test` trên `Products.Api.IntegrationTests` (15/15 pass) và `tests/CrossServiceIsolation.Tests` (17/17 pass, bao gồm 3 test mới); Baskets/Orders/Parties/BFF đang chạy để xác nhận tương tự.

---

## Phase 5: User Story 3 - Token hết hạn bị từ chối rõ ràng, không âm thầm thất bại (Priority: P3)

**Goal**: Một token hết hạn bị từ chối bằng một phản hồi "không được phép" rõ ràng ở cả gateway lẫn từng service — không phải một lỗi chung chung hay một thất bại im lặng.

**Independent Test**: Gửi một token hết hạn tới gateway và, riêng biệt, thẳng tới một domain service, xác nhận cả hai trả về `401` với thông điệp rõ ràng.

### Tests cho User Story 3 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai.

- [X] T041 [P] [US3] Integration test: gateway từ chối token hết hạn bằng `401` kèm thông điệp rõ ràng, phân biệt được với các lỗi xác thực khác, trong `services/gateway/tests/Gateway.Api.IntegrationTests/JwtBearerAuthenticationTests.cs` (spec US3 Acceptance Scenario 1/2, Test Scenario 3) — verified: `Gateway.Api.IntegrationTests` 31/31 passed sau khi vá `GatewayTestHost.CreateBff()` (thiếu `.UseTestJwtBearer()`, một lỗ hổng test-harness lộ ra bởi Phase 4, không liên quan T041)
- [X] T042 [P] [US3] Integration test: tương tự cho một domain service gọi trực tiếp (`Products.Api`), trong `services/products/tests/Products.Api.IntegrationTests/IndependentTokenValidationTests.cs` (mở rộng file đã tạo ở T029) — verified: `Products.Api.IntegrationTests` 16/16 passed

### Implementation cho User Story 3

- [X] T043 [US3] Cấu hình `JwtBearerEvents.OnAuthenticationFailed`/`OnChallenge` trong `shared/Identity/IdentityValidationExtensions.cs` để trả về một phản hồi `401` rõ ràng, phân biệt "token hết hạn" khỏi các lỗi xác thực khác (data-model.md — Token, trạng thái Expired; phụ thuộc T016; làm T042 pass) — verified qua T042
- [X] T044 [US3] Xác nhận hành vi tương tự áp dụng ở gateway qua đường toggle đã nối ở T025 (không cần code mới ngoài T043 vì gateway tái sử dụng `AddIdentityValidation()` khi toggle bật) — chỉ bổ sung assertion vào test T041 (phụ thuộc T025, T043; làm T041 pass) — verified qua T041

**Checkpoint**: Cả ba user story hoạt động độc lập — token hết hạn bị từ chối rõ ràng ở mọi nơi (quickstart.md Scenario 4).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Coverage cô lập bổ sung và xác thực đầu-cuối cuối cùng.

- [ ] T045 [P] Unit test bổ sung: `TenantClaimsProfileService` phát hành đúng claim `tenant_id` cho từng trường hợp Identity User có/không có `TenantId` hợp lệ, cô lập khỏi luồng đăng nhập đầy đủ ở T017, trong `services/identity/tests/Identity.Api.UnitTests/TenantClaimsProfileServiceTests.cs`
- [ ] T046 [P] Cập nhật `docs/adr/0001-identity-provider.md` — đánh dấu Action Item 2 ("Design the tenant → client/claim mapping model") đã hoàn thành, tham chiếu `data-model.md` của tính năng này
- [ ] T047 Chạy toàn bộ [quickstart.md](quickstart.md) Scenario 1-7 trên môi trường local đầy đủ (gateway + bff + 4 domain service + service `identity` mới) và ghi nhận kết quả vào cuối `tasks.md` này, theo đúng khuôn mẫu T039 của [003-stub-identity-tenant-context/tasks.md](../003-stub-identity-tenant-context/tasks.md)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay.
- **Foundational (Phase 2)**: Phụ thuộc Setup hoàn tất — CHẶN mọi user story.
- **User Stories (Phase 3-5)**: Cả ba đều phụ thuộc Foundational hoàn tất.
  - US1 không phụ thuộc US2/US3 — kiểm thử độc lập trước (MVP).
  - US2 độc lập về chức năng với US1 (xác thực độc lập không cần code gateway của US1 tồn tại), nhưng cả hai cùng dùng `shared/Identity` (Foundational) nên làm sau US1 để tránh đụng `Program.cs` của cùng service hai lần trong một số trường hợp wiring liên quan tới toggle.
  - US3 xây trên cơ chế `AddIdentityValidation()` mà cả US1 (gateway, qua toggle) và US2 (BFF + domain service) đã nối — nên triển khai sau cả hai để `OnAuthenticationFailed`/`OnChallenge` áp dụng đồng nhất mọi nơi.
- **Polish (Phase 6)**: Phụ thuộc cả ba user story hoàn tất.

### User Story Dependencies

- **User Story 1 (P1)**: Bắt đầu được sau Foundational (Phase 2). Không phụ thuộc US2/US3.
- **User Story 2 (P2)**: Bắt đầu được sau Foundational (Phase 2). Độc lập về chức năng với US1, nhưng triển khai sau US1 theo thứ tự ưu tiên trong spec.
- **User Story 3 (P3)**: Bắt đầu được sau Foundational (Phase 2); về thực thi nên làm sau US1+US2 vì nó hoàn thiện hành vi `OnAuthenticationFailed` mà cả hai đã dùng chung qua `shared/Identity`.

### Within Each User Story

- Test PHẢI được viết và xác nhận FAIL trước khi triển khai (constitution Principle III).
- Story hoàn tất và test của nó xanh trước khi chuyển sang priority tiếp theo.

### Parallel Opportunities

- T002-T005 song song được (khác file); T006 chờ T001-T005; T007 chờ T004.
- T010, T011 song song được (khác file compose).
- Test Foundational T012-T013 song song được (khác file).
- US1: T017-T019 (test) song song được; T026 song song với các implementation task khác của US1 (không phụ thuộc T020-T025).
- US2: T027-T033 (7 test) song song được (khác file); T034-T039 (wiring 5 service + đánh dấu AllowAnonymous) song song được.
- US3: T041, T042 song song được (khác file).

---

## Parallel Example: User Story 2

```bash
# Chạy đồng thời 5 integration test "xác thực độc lập" (viết trước, xác nhận fail):
Task: "Integration test: Bff.Api tự từ chối token giả mạo, trong services/bff/tests/Bff.Api.IntegrationTests/IndependentTokenValidationTests.cs"
Task: "Integration test: Parties.Api tự từ chối token giả mạo, trong services/parties/tests/Parties.Api.IntegrationTests/IndependentTokenValidationTests.cs"
Task: "Integration test: Products.Api tự từ chối token giả mạo, trong services/products/tests/Products.Api.IntegrationTests/IndependentTokenValidationTests.cs"
Task: "Integration test: Baskets.Api tự từ chối token giả mạo, trong services/baskets/tests/Baskets.Api.IntegrationTests/IndependentTokenValidationTests.cs"
Task: "Integration test: Orders.Api tự từ chối token giả mạo, trong services/orders/tests/Orders.Api.IntegrationTests/IndependentTokenValidationTests.cs"

# Chạy đồng thời 5 thay đổi wiring:
Task: "Wire AddIdentityValidation() vào services/bff/src/Bff.Api/Program.cs"
Task: "Wire AddIdentityValidation() vào services/parties/src/Parties.Api/Program.cs"
Task: "Wire AddIdentityValidation() vào services/products/src/Products.Api/Program.cs"
Task: "Wire AddIdentityValidation() vào services/baskets/src/Baskets.Api/Program.cs"
Task: "Wire AddIdentityValidation() vào services/orders/src/Orders.Api/Program.cs"
```

---

## Implementation Strategy

### MVP First (chỉ User Story 1)

1. Hoàn tất Phase 1: Setup.
2. Hoàn tất Phase 2: Foundational (CRITICAL — chặn mọi story).
3. Hoàn tất Phase 3: User Story 1.
4. **DỪNG và KIỂM CHỨNG**: chạy quickstart.md Scenario 1, 2, 7 — đăng nhập phát hành token thật, gateway xác thực nó, rollback qua toggle hoạt động.
5. Lưu ý: xác thực độc lập ở BFF/domain service (US2) và hành vi từ chối token hết hạn rõ ràng ở mọi nơi (US3) CHƯA có ở checkpoint này.

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng.
2. Thêm User Story 1 → kiểm chứng độc lập → đăng nhập + xác thực ở gateway hoạt động đầu-cuối (MVP!).
3. Thêm User Story 2 → kiểm chứng độc lập → mọi service tự chặn token giả mạo/vắng mặt.
4. Thêm User Story 3 → kiểm chứng độc lập → token hết hạn bị từ chối rõ ràng mọi nơi.
5. Polish → coverage bổ sung, chạy đầy đủ quickstart.md.

### Solo/Sequential Strategy

Theo tiền lệ [003-stub-identity-tenant-context/tasks.md](../003-stub-identity-tenant-context/tasks.md) (vận hành đơn lẻ), đường đi thực tế là tuần tự: Setup → Foundational → US1 → US2 → US3 → Polish, kiểm chứng checkpoint từng story trước khi qua story tiếp theo.

---

## Notes

- Task [P] = khác file, không phụ thuộc task chưa xong.
- Nhãn [Story] gắn mỗi task với user story của nó để truy vết.
- Test là bắt buộc ở đây (constitution Principle III), không tuỳ chọn — viết và xác nhận fail trước khi triển khai.
- T025/T044 là điểm nối giữa US1 và US3: gateway không có code `OnAuthenticationFailed` riêng — nó thừa hưởng hành vi từ `shared/Identity` qua toggle đã nối ở US1, nên T044 chỉ là một assertion bổ sung, không phải triển khai mới.
- Gỡ bỏ `StubIdentityAuthenticationHandler.cs`/`StubIdentityAuthenticationSchemeOptions.cs` KHÔNG nằm trong phạm vi tasks.md này — theo research.md Decision 7, việc đó chỉ thực hiện sau khi toggle `identity-server-auth-cutover` đã ổn định trong production và tới ngày gỡ bỏ đã ghi nhận, không phải một phần của "hoàn thành" tính năng này.
- Commit sau mỗi task hoặc mỗi nhóm task logic.
- Dừng ở bất kỳ checkpoint nào để kiểm chứng một story độc lập trước khi tiếp tục.
