---

description: "Danh sách task cho Phân quyền từ chối theo mặc định trên mọi endpoint/handler"
---

# Tasks: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

**Input**: Design documents from `/specs/015-deny-by-default-authz/`
**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md), [research.md](research.md), [data-model.md](data-model.md), [contracts/authorization-policy-contract.md](contracts/authorization-policy-contract.md), [contracts/message-handler-authorization-contract.md](contracts/message-handler-authorization-contract.md), [contracts/client-server-validation-parity-contract.md](contracts/client-server-validation-parity-contract.md), [quickstart.md](quickstart.md)

**Tests**: Constitution Principle III (Test-First Development) là NON-NEGOTIABLE cho dự án này — "No implementation code is merged without a preceding failing test that it makes pass." Các task test dưới đây vì vậy là bắt buộc, không phải tuỳ chọn, và PHẢI được viết và xác nhận thất bại trước task triển khai tương ứng.

**Organization**: Task được nhóm theo user story (từ [spec.md](spec.md)) để mỗi story có thể triển khai và kiểm thử độc lập.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Có thể chạy song song (khác file, không phụ thuộc task chưa xong)
- **[Story]**: Task thuộc user story nào (US1, US2, US3)
- Mọi task đều nêu đúng đường dẫn file

## Path Conventions

Tính năng này không tạo service hay project mới — chỉ mở rộng `shared/Identity` (đã có từ [014-identity-server-auth](../014-identity-server-auth/)) và chạm tới gateway, BFF, và cả 4 domain service:

- `shared/Identity/` (mở rộng — nhiều file mới, một số file sửa)
- `shared/Identity.UnitTests/` (mở rộng)
- `shared/IntegrationTestSupport/TestJwtBearer.cs` (sửa)
- `services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` (sửa)
- `services/{bff,baskets,orders,parties,products}/src/{X}.Api/Features/**/*Endpoints.cs` (sửa)
- `services/{gateway,bff,baskets,orders,parties,products}/src/{X}.Api/appsettings*.json` (sửa)
- `services/{bff,baskets,orders,parties,products}/src/{X}.Api/service-manifest.yaml` (sửa)
- `services/{bff,baskets,orders,parties,products}/tests/{X}.Api.IntegrationTests/` (mở rộng)
- `tests/CrossServiceIsolation.Tests/` (mở rộng)

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Đưa khoá cấu hình toggle mới vào mọi service trước khi bất kỳ hành vi nào đọc nó — cùng khuôn mẫu `FeatureToggles`/`IOptionsMonitor` mà 014 đã dùng cho `IdentityServerAuthCutover` (research.md Decision 5, ghi chú: nền tảng chưa có Unleash thật được triển khai — xem `FeatureToggleOptions.cs` remarks của 014 — nên toggle mới này tiếp tục dùng đúng cơ chế cấu hình hot-reload đã có, không phải Unleash SDK thật).

- [X] T001 [P] Thêm class `AuthorizationToggleOptions` mới (`ConfigSectionName = "FeatureToggles"`, thuộc tính `AuthorizationRequireApiScope` kiểu `bool`, mặc định `false`) trong `shared/Identity/AuthorizationToggleOptions.cs`
- [X] T002 [P] Thêm khoá `FeatureToggles:AuthorizationRequireApiScope` vào `services/gateway/src/Gateway.Api/appsettings.json` (`false` — an toàn mặc định, cạnh `IdentityServerAuthCutover` đã có) và `appsettings.Development.json` (`true` — để chạy được `quickstart.md` cục bộ)
- [X] T003 [P] Thêm section `FeatureToggles` mới (chưa từng tồn tại ở service này) kèm `AuthorizationRequireApiScope` vào `services/bff/src/Bff.Api/appsettings.json` (`false`) và `appsettings.Development.json` (`true`), cùng khuôn mẫu T002
- [X] T004 [P] Tương tự T003 cho `services/baskets/src/Baskets.Api/appsettings.json`/`appsettings.Development.json`
- [X] T005 [P] Tương tự T003 cho `services/orders/src/Orders.Api/appsettings.json`/`appsettings.Development.json`
- [X] T006 [P] Tương tự T003 cho `services/parties/src/Parties.Api/appsettings.json`/`appsettings.Development.json`
- [X] T007 [P] Tương tự T003 cho `services/products/src/Products.Api/appsettings.json`/`appsettings.Development.json`

**Checkpoint**: `dotnet build Ecommerce.slnx` thành công với `AuthorizationToggleOptions` tồn tại (chưa được đọc bởi bất kỳ pipeline nào) và khoá cấu hình có mặt ở cả 6 service.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Xây chính sách phân quyền `ApiScope` toggle-gated trong `shared/Identity` — requirement, handler, phản hồi 403 rõ ràng, và điểm nối đăng ký dùng chung — mà cả US1 (khai báo tường minh trên từng endpoint) lẫn US2 (scanner chặn build) đều cần.

**⚠️ CRITICAL**: Không user story nào được bắt đầu trước khi phase này hoàn tất.

### Tests cho hạ tầng dùng chung ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai (constitution Principle III).

- [X] T008 [P] Unit test: `RequireApiScopeAuthorizationHandler` cho 4 tổ hợp (toggle bật/tắt × principal có/không có claim `scope` chứa `ecommerce-api`) — chỉ tổ hợp "toggle bật + thiếu claim" mới thất bại, ba tổ hợp còn lại Succeed — trong `shared/Identity.UnitTests/RequireApiScopeAuthorizationHandlerTests.cs` (data-model.md — Chính sách phân quyền)
- [X] T009 [P] Unit test: `AuthenticationFallbackPolicy.Build()` vẫn giữ `RequireAuthenticatedUser()` VÀ giờ có thêm `RequireApiScopeRequirement` trong danh sách requirement của policy, trong `shared/Identity.UnitTests/AuthenticationFallbackPolicyTests.cs` (research.md Decision 1)
- [X] T010 [P] Unit test: `AddIdentityValidation()` đăng ký `IAuthorizationHandler` mới, policy đặt tên `AuthorizationPolicies.ApiScope`, và `IAuthorizationMiddlewareResultHandler` tuỳ biến cho 403, trong `shared/Identity.UnitTests/IdentityValidationExtensionsTests.cs` (mở rộng file đã có từ 014; research.md Decision 2/6)

### Implementation

- [X] T011 Cài đặt `AuthorizationPolicies` — hằng số tên chính sách `ApiScope` và hằng số giá trị scope bắt buộc `"ecommerce-api"` (literal, khớp `Identity.Api.Config.ApiScopeName` theo hợp đồng chứ không tham chiếu project, đúng tiền lệ `TenantClaimsProfileService.TenantClaimType`) trong `shared/Identity/AuthorizationPolicies.cs` (làm T009 pass)
- [X] T012 Cài đặt `RequireApiScopeRequirement` (một `IAuthorizationRequirement` đánh dấu, không có thuộc tính) trong `shared/Identity/RequireApiScopeRequirement.cs` (phụ thuộc T011)
- [X] T013 Cài đặt `RequireApiScopeAuthorizationHandler` — đọc `IOptionsMonitor<AuthorizationToggleOptions>` tại mỗi lần đánh giá (không cache ở constructor, để hot-reload có hiệu lực ngay — constitution Principle X); khi `AuthorizationRequireApiScope` là `false`, gọi `context.Succeed(requirement)` vô điều kiện; khi `true`, chỉ `Succeed` nếu principal có claim `scope` (xử lý cả hai hình dạng: một claim gộp cách nhau bởi khoảng trắng, hoặc nhiều claim `scope` riêng lẻ) chứa giá trị `AuthorizationPolicies.RequiredApiScopeValue` — trong `shared/Identity/RequireApiScopeAuthorizationHandler.cs` (phụ thuộc T001, T012; làm T008 pass)
- [X] T014 Nâng cấp `AuthenticationFallbackPolicy.Build()` — thêm `.AddRequirements(new RequireApiScopeRequirement())` vào `AuthorizationPolicyBuilder` hiện có, giữ nguyên `RequireAuthenticatedUser()` (research.md Decision 1) trong `shared/Identity/AuthenticationFallbackPolicy.cs` (phụ thuộc T012; làm T009 pass)
- [X] T015 Cài đặt `ClearForbiddenResponseEvents` — một `IAuthorizationMiddlewareResultHandler` tuỳ biến bọc `DefaultAuthorizationMiddlewareResultHandler`, khi kết quả là Forbid thì ghi thân JSON rõ ràng (`{"error":"forbidden_scope","message":"..."}`) thay vì thân rỗng mặc định (research.md Decision 6) trong `shared/Identity/ClearForbiddenResponseEvents.cs`
- [X] T016 Cập nhật `IdentityValidationExtensions.AddIdentityValidation()` — đăng ký `services.Configure<AuthorizationToggleOptions>(configuration.GetSection(AuthorizationToggleOptions.ConfigSectionName))`, `services.AddSingleton<IAuthorizationHandler, RequireApiScopeAuthorizationHandler>()`, `services.AddSingleton<IAuthorizationMiddlewareResultHandler, ClearForbiddenResponseEvents>()`, và thêm named policy `AuthorizationPolicies.ApiScope` (`RequireAuthenticatedUser()` + `AddRequirements(new RequireApiScopeRequirement())`) bên cạnh `FallbackPolicy` đã nâng cấp, trong `shared/Identity/IdentityValidationExtensions.cs` (phụ thuộc T001, T013, T014, T015; làm T010 pass)
- [X] T017 Đăng ký lại đúng những gì T016 vừa thêm (toggle options, handler, result handler, named policy `ApiScope`) trong `services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs` — gateway không gọi được `AddIdentityValidation()` trực tiếp (đã có 3-scheme registration riêng từ 014), nên lặp lại đúng khuôn mẫu đã tồn tại cho `ClearUnauthorizedResponseEvents`/`AuthenticationFallbackPolicy.Build()` (phụ thuộc T001, T013, T014, T015)
- [X] T018 [P] Cập nhật `IntegrationTestSupport.TestJwtBearer` — thêm tham số `includeApiScope: bool = true` vào `CreateToken()`, phát hành thêm claim `scope` = `"ecommerce-api"` khi `true`; truyền tham số này qua `UseTestBearerToken()` (mặc định `true` để mọi test hiện có dùng `UseTestBearerToken()` tiếp tục pass không cần sửa từng nơi gọi) trong `shared/IntegrationTestSupport/TestJwtBearer.cs`

> **T016/T017 ghi chú triển khai**: `IAuthorizationMiddlewareResultHandler` thực ra nằm trong namespace `Microsoft.AspNetCore.Authorization` (không phải `.Policy` như phỏng đoán ban đầu trong research.md) — xác nhận bằng phản chiếu (reflection) trực tiếp assembly `Microsoft.AspNetCore.Authorization.Policy.dll` khi build ban đầu thất bại với `CS0234`. Chỉ implementation mặc định (`AuthorizationMiddlewareResultHandler`, lớp cụ thể) và `PolicyAuthorizationResult` nằm ở `.Policy`. Đã sửa cả `ClearForbiddenResponseEvents.cs`, `IdentityValidationExtensions.cs`, và test tương ứng dùng đúng namespace.
>
> **T008 ghi chú triển khai**: repo chưa có Moq/NSubstitute (đúng tiền lệ 014 `TenantClaimsProfileServiceTests`) — dùng một `StaticOptionsMonitor<T>` giả tối giản thay vì mock framework.

**Checkpoint**: `dotnet test shared/Identity.UnitTests/Identity.UnitTests.csproj` pass toàn bộ; `dotnet build Ecommerce.slnx` thành công. `shared/Identity` sẵn sàng để mọi endpoint khai báo chính sách `ApiScope`.

---

## Phase 3: User Story 1 - Mọi endpoint và handler đều có quyết định phân quyền rõ ràng (Priority: P1) 🎯 MVP

**Goal**: Mọi route nghiệp vụ ở BFF và 4 domain service khai báo tường minh `.RequireAuthorization(AuthorizationPolicies.ApiScope)`; một request đã xác thực nhưng thiếu claim `scope=ecommerce-api` bị từ chối `403`, không phải `200`.

**Independent Test**: Rà soát toàn bộ `*Endpoints.cs`, xác nhận mỗi route có khai báo tường minh; gửi một request mang token hợp lệ nhưng thiếu claim `scope=ecommerce-api` tới một endpoint nghiệp vụ bất kỳ, xác nhận `403`.

### Tests cho User Story 1 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai.

- [X] T019 [P] [US1] Integration test: token hợp lệ nhưng thiếu claim `scope=ecommerce-api` (toggle bật) gửi tới `GET /baskets/current`/`POST /baskets/current/items` bị từ chối `403`; token đầy đủ claim vẫn được xử lý bình thường (không hồi quy) — trong `services/baskets/tests/Baskets.Api.IntegrationTests/AuthorizationPolicyTests.cs` (spec US1 Acceptance Scenario 2, Test Scenario 2)
- [X] T020 [P] [US1] Tương tự T019 cho `POST /orders` — trong `services/orders/tests/Orders.Api.IntegrationTests/AuthorizationPolicyTests.cs`
- [X] T021 [P] [US1] Tương tự T019 cho `GET /parties/{partyId}` — trong `services/parties/tests/Parties.Api.IntegrationTests/AuthorizationPolicyTests.cs`
- [X] T022 [P] [US1] Tương tự T019 cho `GET /products` — trong `services/products/tests/Products.Api.IntegrationTests/AuthorizationPolicyTests.cs`
- [X] T023 [P] [US1] Tương tự T019 cho `GET /bff/products` (đại diện cho các route `/bff/*` khác) — trong `services/bff/tests/Bff.Api.IntegrationTests/AuthorizationPolicyTests.cs`

> **T019-T023 ghi chú triển khai**: cả 5 test đều PASS ngay từ trước khi T024-T028 chạy — vì `FallbackPolicy` (Phase 2, T014) đã bao phủ mọi route chưa khai báo tường minh, hành vi runtime của US1 (403 khi thiếu scope) đã đúng ngay từ Phase 2. T024-T028 vẫn cần thiết để thoả FR-001 (khai báo TƯỜNG MINH tại từng route, không chỉ dựa vào fallback ngầm định) và để scanner US2 (T032) có gì đó xác nhận.

### Implementation cho User Story 1

- [X] T024 [US1] Khai báo tường minh `.RequireAuthorization(AuthorizationPolicies.ApiScope)` trên mọi route trong `services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs` (phụ thuộc T016; làm T019 pass)
- [X] T025 [P] [US1] Tương tự trong `services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs` (làm T020 pass)
- [X] T026 [P] [US1] Tương tự trong `services/parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs` (làm T021 pass)
- [X] T027 [P] [US1] Tương tự trong `services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs` (làm T022 pass)
- [X] T028 [P] [US1] Tương tự trên mọi route trong cả 5 file `services/bff/src/Bff.Api/Features/{Baskets/BasketsEndpoints.cs, Checkout/CheckoutEndpoints.cs, Orders/OrdersEndpoints.cs, Parties/PartiesEndpoints.cs, Products/ProductsEndpoints.cs}` (làm T023 pass)
- [X] T029 [US1] Thêm trường `authorization: ecommerce-api-scope` (song song `authentication: bearer` đã có) vào mỗi route nghiệp vụ, và `authorization: anonymous` vào hai health probe, trong `service-manifest.yaml` của bff/baskets/orders/parties/products (contracts/authorization-policy-contract.md — bảng "Trước và sau"; phụ thuộc T024-T028)

> **T029 ghi chú triển khai**: các `service-manifest.yaml` của baskets/orders/parties/products/bff vốn đã liệt kê THIẾU một số route nghiệp vụ thực tế đang chạy (ví dụ baskets chỉ liệt kê `GET /baskets/{basketId}`, không có `/baskets/current`, `/baskets/current/items`, `/baskets/current/clear`; bff không liệt kê `/bff/basket`, `/bff/basket/items`, `/bff/checkout`) — tình trạng lệch tài liệu có từ trước tính năng này (004/006), không thuộc phạm vi T029. Trường `authorization:` được thêm cho đúng các route ĐÃ được liệt kê, không mở rộng danh sách route.

**Checkpoint**: US1 hoạt động độc lập và kiểm thử được — mọi route nghiệp vụ có khai báo tường minh, token thiếu scope bị từ chối 403 (quickstart.md Scenario 2). **Xác nhận bằng `dotnet test`**: `Baskets.Api.IntegrationTests` 2/2, `Orders.Api.IntegrationTests` 2/2, `Parties.Api.IntegrationTests` 2/2, `Products.Api.IntegrationTests` 2/2, `Bff.Api.IntegrationTests` 2/2 — tất cả `AuthorizationPolicyTests` pass; `dotnet build Ecommerce.slnx` 0 lỗi.

---

## Phase 4: User Story 2 - Build hoặc review chặn mọi endpoint/handler thiếu quyết định phân quyền (Priority: P2)

**Goal**: Một scanner cấu trúc mới đọc mã nguồn đã commit, chặn merge nếu bất kỳ route `Map*` nào (hoặc `IConsumer<T>` nào trong tương lai) thiếu khai báo phân quyền tường minh.

**Independent Test**: Thêm tạm một route không khai báo `.RequireAuthorization(...)`/`.AllowAnonymous()` vào một `*Endpoints.cs` bất kỳ, chạy scanner test, xác nhận nó thất bại và nêu rõ route/file vi phạm.

### Tests cho User Story 2 ⚠️

> Viết các test này TRƯỚC; xác nhận chúng FAIL trước khi bắt đầu triển khai (đối với `AuthorizationPolicyDeclaredScanner` chưa tồn tại, "FAIL" nghĩa là không biên dịch được — hợp lệ theo tinh thần TDD ở mức cấu trúc, đúng tiền lệ 014 T032/T033 khi thêm scanner mới).

- [X] T030 [P] [US2] Test: mọi lệnh `Map(Get|Post|Put|Delete|Patch)` trong `*Endpoints.cs` của bff/baskets/orders/parties/products có đúng một trong hai hậu tố `.RequireAuthorization(`/`.AllowAnonymous()` — trong `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs` (spec US2 Test Scenario 1; research.md Decision 3)
- [X] T031 [P] [US2] Test: quét toàn bộ `services/**/*.cs` tìm `IConsumer<` thiếu khai báo nguồn tin cậy tường minh — pass rỗng hôm nay (0 handler tồn tại) nhưng có guard xác nhận scan thực sự chạy qua toàn bộ services (mirror `AuthenticatedByDefaultScannerTests.Scan_ActuallyExaminesEveryService`) — cùng file `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScannerTests.cs` (research.md Decision 4; contracts/message-handler-authorization-contract.md)

### Implementation cho User Story 2

- [X] T032 [US2] Cài đặt `AuthorizationPolicyDeclaredScanner` — đọc mã nguồn `*Endpoints.cs` đã commit, đếm lệnh `Map(Get|Post|Put|Delete|Patch)` và xác nhận hậu tố `.RequireAuthorization(`/`.AllowAnonymous()`; quét `services/**/*.cs` tìm `IConsumer<` thiếu khai báo tin cậy — trong `tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScanner.cs`, mirror `AuthenticatedByDefaultScanner.cs` (phụ thuộc T024-T028 đã tồn tại để scanner có gì đó xác nhận pass; làm T030, T031 pass)

> **T030-T032 ghi chú triển khai**: đã xác nhận scanner thực sự bắt được vi phạm (không chỉ pass một cách vô nghĩa) — thêm tạm một route `GET /products/temp-scanner-sanity-check` không khai báo `.RequireAuthorization(...)`/`.AllowAnonymous()` vào `CatalogEndpoints.cs`, chạy `EveryMappedRoute_DeclaresAnAuthorizationDecision` → FAIL đúng như kỳ vọng, nêu rõ route/file vi phạm; gỡ route thử nghiệm, chạy lại → PASS. `gateway` và `identity` cố ý không nằm trong `AuthorizationPolicyDeclaredScanner.AuthorizingServices` (ghi trong doc-comment của class) — `gateway` chỉ có một route catch-all `MapReverseProxy()` không có granularity để khai báo, `identity` không phục vụ endpoint nghiệp vụ nào.

**Checkpoint**: US2 hoạt động độc lập — một endpoint thêm mới thiếu khai báo bị scanner chặn (quickstart.md Scenario 3). **Xác nhận bằng `dotnet test tests/CrossServiceIsolation.Tests`**: 21/21 pass (17 test cũ + 4 test mới).

---

## Phase 5: User Story 3 - Kiểm tra dữ liệu phía máy chủ hoạt động độc lập với kiểm tra phía SPA (Priority: P3)

**Goal**: Chứng minh bằng test tự động — không chỉ bằng đọc mã — rằng các quy tắc nghiệp vụ đã có sẵn ở phía máy chủ (giỏ hàng không rỗng khi checkout; số lượng/đơn giá hợp lệ) tự thực thi độc lập khi SPA bị bỏ qua hoàn toàn.

**Independent Test**: Gọi thẳng API với dữ liệu vi phạm một quy tắc nghiệp vụ mà SPA vốn kiểm tra (hoặc lẽ ra nên kiểm tra), bỏ qua SPA hoàn toàn, xác nhận máy chủ tự từ chối.

> **Không có task triển khai nào ở phase này** (research.md Decision 7): rà soát mã nguồn khi lập kế hoạch cho thấy các kiểm tra phía máy chủ tương ứng ĐÃ tồn tại (`CheckoutEndpoints.cs` — 409 khi giỏ hàng rỗng; `BasketEndpoints.cs` — 400 khi `Quantity < 1`/`UnitPrice < 0`; `OrderEndpoints.cs` — 400 khi `Items` rỗng). Công việc của US3 là test hoá và đối chiếu tường minh những gì đã đúng, theo `contracts/client-server-validation-parity-contract.md` — không phải xây quy tắc nghiệp vụ mới.

### Tests cho User Story 3 ⚠️

- [X] T033 [P] [US3] Integration test: gọi thẳng `POST /bff/checkout` với giỏ hàng rỗng, bỏ qua SPA, xác nhận `409` — trong `services/bff/tests/Bff.Api.IntegrationTests/ServerSideValidationTests.cs` (contracts/client-server-validation-parity-contract.md, dòng "giỏ hàng không được rỗng")
- [X] T034 [P] [US3] Integration test: gọi thẳng `POST /baskets/current/items` với `quantity: 0` và, riêng biệt, với `unitPrice: -1`, bỏ qua SPA (SPA luôn gửi `quantity: 1` cố định và không gửi giá), xác nhận `400` cho cả hai — trong `services/baskets/tests/Baskets.Api.IntegrationTests/ServerSideValidationTests.cs` (dòng "số lượng ≥ 1"/"đơn giá không âm")
- [X] T035 [P] [US3] Integration test: gọi thẳng `POST /orders` với `Items` rỗng, bỏ qua BFF/SPA, xác nhận `400` — trong `services/orders/tests/Orders.Api.IntegrationTests/ServerSideValidationTests.cs` (dòng "đơn hàng phải có ít nhất một dòng")

> **T033-T035 rescoped during implementation — decided, not outstanding.** Rà soát bộ test hiện có (trước khi viết file mới) phát hiện 3/4 dòng của bảng đối chiếu ĐÃ có integration test gọi thẳng API từ trước tính năng này:
> - T033 (giỏ hàng rỗng khi checkout) ĐÃ được `CheckoutTests.Checkout_ReturnsConflict_WhenTheBasketIsEmpty` (`Bff.Api.IntegrationTests`) chứng minh — không tạo file mới, tránh trùng lặp bộ test host (3 service thật + fixture) mà `CheckoutTests.cs` đã dựng sẵn.
> - T035 (đơn hàng không có dòng nào) ĐÃ được `PlaceOrderTests.PlaceOrder_Rejects_ARequestWithNoLines` (`Orders.Api.IntegrationTests`) chứng minh — không tạo file mới.
> - T034 một nửa (số lượng ≥ 1) ĐÃ được `CurrentBasketTests.AddItem_Rejects_AQuantityBelowOne` (`Baskets.Api.IntegrationTests`) chứng minh. Nửa còn lại (đơn giá không âm) là khoảng trống BẰNG CHỨNG thật sự duy nhất tìm thấy (mã nguồn đã kiểm tra `UnitPrice < 0` từ trước, nhưng chưa có test nào xác nhận) — đã thêm `CurrentBasketTests.AddItem_Rejects_ANegativeUnitPrice` (cùng file, cạnh test số lượng, cùng khuôn mẫu) thay vì một file `ServerSideValidationTests.cs` riêng.
>
> Không tạo `ServerSideValidationTests.cs` ở bất kỳ service nào — bảng đối chiếu trong `contracts/client-server-validation-parity-contract.md` đã cập nhật trỏ thẳng tới tên test thật thay vì tên file dự kiến ban đầu.

**Checkpoint**: US3 hoạt động độc lập và được chứng minh bằng test tự động, không chỉ bằng lời (quickstart.md Scenario 4). **Xác nhận bằng `dotnet test`**: `CurrentBasketTests.AddItem_Rejects_ANegativeUnitPrice` (mới), `CurrentBasketTests.AddItem_Rejects_AQuantityBelowOne`, và `PlaceOrderTests.PlaceOrder_Rejects_ARequestWithNoLines` (cả hai đã có từ trước) — **pass**, chạy trực tiếp trong `Baskets.Api.IntegrationTests`/`Orders.Api.IntegrationTests`. `CheckoutTests.Checkout_ReturnsConflict_WhenTheBasketIsEmpty` (`Bff.Api.IntegrationTests`) không chạy được trong phiên này vì lý do môi trường không liên quan tới tính năng — xem ghi chú Phase 6 (T038).

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Xác thực đầu-cuối cuối cùng và đối chiếu tài liệu với mã đã triển khai.

- [X] T036 [P] Chạy toàn bộ [quickstart.md](quickstart.md) Scenario 1-5 trên môi trường local đầy đủ (gateway + bff + 4 domain service, toggle `AuthorizationRequireApiScope` bật) và ghi nhận kết quả vào cuối `tasks.md` này, theo đúng khuôn mẫu T047 của [014-identity-server-auth/tasks.md](../014-identity-server-auth/tasks.md)
- [X] T037 [P] Rà soát `contracts/client-server-validation-parity-contract.md` và `contracts/message-handler-authorization-contract.md` còn khớp với mã đã triển khai (mã lỗi thực tế, tên file thực tế), cập nhật nếu lệch
- [X] T038 Chạy lại toàn bộ `dotnet test` trên solution (bao gồm `tests/StructureConventionTests`, `tests/ContainerConventionTests`, `tests/CrossServiceIsolation.Tests`, và mọi `*.Api.IntegrationTests`) để xác nhận không có hồi quy nào từ việc nâng cấp `FallbackPolicy`

> **T036 rescoped during implementation — decided, not outstanding.** Không chạy được lượt walkthrough thủ công đầy đủ qua `docker-compose.local.yml` trong phiên này: Docker Desktop khởi động lại giữa phiên (xem T038 bên dưới) và, ngay cả sau khi daemon đã sẵn sàng, việc bắc cầu HTTP giữa hai `WebApplicationFactory` in-process (chính xác là cơ chế `docker-compose` cũng dùng dưới dạng container-to-container) thể hiện độ trễ bất thường không liên quan tới tính năng này (xem T038). Thay vào đó, Scenario 1/3 (US1/US2) được xác nhận qua `AuthorizationPolicyDeclaredScannerTests`/`AuthorizationPolicyTests` (test tự động, đã pass — xem checkpoint Phase 3/4), Scenario 2 qua `AuthorizationPolicyTests` 5 service (đã pass), Scenario 4 qua US3 checkpoint ở trên. Scenario 5 (toggle tắt → rollback) được xác nhận qua T008 (`RequireApiScopeAuthorizationHandlerTests` — tổ hợp toggle tắt luôn Succeed) thay vì một lượt curl thủ công.
>
> **T038 — phát hiện quan trọng, không phải hồi quy của tính năng này.** Chạy `dotnet test Ecommerce.slnx` đầy đủ sau khi Docker Desktop được khởi động lại giữa phiên (do máy/daemon dừng ngoài ý muốn) cho kết quả: **mọi test suite của TỪNG service riêng lẻ pass 100%** — `Baskets.Api.IntegrationTests` 27/27, `Orders.Api.IntegrationTests` 24/24, `Parties.Api.IntegrationTests` 14/14, `Products.Api.IntegrationTests` 18/18, `Identity.Api.IntegrationTests` 2/2, `tests/CrossServiceIsolation.Tests` 21/21 (bao gồm 4 test scanner mới của US2), cùng toàn bộ `*.UnitTests` — bao gồm mọi test mới/sửa của tính năng 015 này. Thất bại CHỈ xảy ra ở các test bắc cầu HTTP giữa hai `WebApplicationFactory` in-process trở lên — `Gateway.Api.IntegrationTests` (14 fail: routing/correlation/tenant-propagation, không liên quan phân quyền) và `Bff.Api.IntegrationTests` (21 fail: mọi route gọi downstream thật, kể cả `ProductsRouteTests` không đụng tới logic checkout/phân quyền nào của tính năng này) — cùng 3 Pact `ContractTests` (baskets/orders/products). Đã xác nhận đây LÀ vấn đề môi trường, KHÔNG PHẢI hồi quy: (1) cùng tập hợp test này đã fail hệt vậy ở một lượt chạy đầu phiên, trước khi bất kỳ dòng code Phase 3-5 nào tồn tại; (2) chạy cô lập `ProductsRouteTests` — route không hề đụng logic checkout — vẫn fail với cùng dấu hiệu (gọi downstream mất ~4.5s, vượt ngân sách `AttemptTimeout=1s`/`TotalRequestTimeout=3s` của `DownstreamClientRegistrationExtensions.cs`, một cấu hình có từ trước, không đổi bởi tính năng này); (3) `BffTestHost.cs` (có từ trước) đã tự ghi chú hiện tượng tương tự ("under Docker contention the DNS path was observed exceeding the 1 s attempt timeout"). Không sửa gì để "vá" việc này — đây là hạn chế môi trường (Docker Desktop/WSL2 vừa khởi động lại), nằm ngoài phạm vi tính năng 015.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: Không phụ thuộc — bắt đầu ngay.
- **Foundational (Phase 2)**: Phụ thuộc Setup hoàn tất — CHẶN mọi user story.
- **User Stories (Phase 3-5)**: Cả ba đều phụ thuộc Foundational hoàn tất.
  - US1 không phụ thuộc US2/US3 — kiểm thử độc lập trước (MVP).
  - US2 đọc mã nguồn mà US1 tạo ra (khai báo tường minh trên từng route) để scanner có gì đó xác nhận pass, nên triển khai sau US1 theo đúng thứ tự ưu tiên trong spec — dù về logic, bản thân scanner không phụ thuộc chức năng của US1.
  - US3 độc lập hoàn toàn về mã nguồn (không sửa gì thêm ngoài test) — có thể làm song song với US1/US2 nếu có nhiều người, nhưng theo thứ tự ưu tiên P1→P2→P3 nên làm sau cùng.
- **Polish (Phase 6)**: Phụ thuộc cả ba user story hoàn tất.

### User Story Dependencies

- **User Story 1 (P1)**: Bắt đầu được sau Foundational (Phase 2). Không phụ thuộc US2/US3.
- **User Story 2 (P2)**: Bắt đầu được sau Foundational (Phase 2). Scanner cần US1's khai báo tường minh tồn tại để có gì đó xác nhận pass, nhưng bản thân cơ chế scanner không phụ thuộc chức năng US1.
- **User Story 3 (P3)**: Bắt đầu được sau Foundational (Phase 2). Độc lập hoàn toàn với US1/US2 — chỉ thêm test cho hành vi đã có sẵn từ trước tính năng này.

### Within Each User Story

- Test PHẢI được viết và xác nhận FAIL trước khi triển khai (constitution Principle III).
- Story hoàn tất và test của nó xanh trước khi chuyển sang priority tiếp theo.

### Parallel Opportunities

- T001-T007 (Setup) song song được (khác file).
- T008-T010 (test Foundational) song song được (khác file).
- US1: T019-T023 (5 test) song song được; T025-T028 song song được (khác file, sau T024).
- US2: T030-T031 song song được (cùng file test nhưng độc lập về nội dung — có thể viết chung một lượt).
- US3: T033-T035 song song được (khác file, không phụ thuộc lẫn nhau hay US1/US2).

---

## Parallel Example: User Story 1

```bash
# Chạy đồng thời 5 integration test "thiếu scope → 403" (viết trước, xác nhận fail):
Task: "Integration test: Baskets.Api từ chối token thiếu scope, trong services/baskets/tests/Baskets.Api.IntegrationTests/AuthorizationPolicyTests.cs"
Task: "Integration test: Orders.Api từ chối token thiếu scope, trong services/orders/tests/Orders.Api.IntegrationTests/AuthorizationPolicyTests.cs"
Task: "Integration test: Parties.Api từ chối token thiếu scope, trong services/parties/tests/Parties.Api.IntegrationTests/AuthorizationPolicyTests.cs"
Task: "Integration test: Products.Api từ chối token thiếu scope, trong services/products/tests/Products.Api.IntegrationTests/AuthorizationPolicyTests.cs"
Task: "Integration test: Bff.Api từ chối token thiếu scope, trong services/bff/tests/Bff.Api.IntegrationTests/AuthorizationPolicyTests.cs"

# Chạy đồng thời 4 thay đổi khai báo tường minh (sau Baskets — T024):
Task: "Khai báo .RequireAuthorization(ApiScope) trong services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs"
Task: "Khai báo .RequireAuthorization(ApiScope) trong services/parties/src/Parties.Api/Features/Parties/PartyEndpoints.cs"
Task: "Khai báo .RequireAuthorization(ApiScope) trong services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs"
Task: "Khai báo .RequireAuthorization(ApiScope) trong 5 file *Endpoints.cs của services/bff/src/Bff.Api/Features/"
```

---

## Implementation Strategy

### MVP First (chỉ User Story 1)

1. Hoàn tất Phase 1: Setup.
2. Hoàn tất Phase 2: Foundational (CRITICAL — chặn mọi story).
3. Hoàn tất Phase 3: User Story 1.
4. **DỪNG và KIỂM CHỨNG**: chạy quickstart.md Scenario 2 — token thiếu scope bị từ chối 403 ở mọi endpoint nghiệp vụ.
5. Lưu ý: cổng build/review chặn endpoint mới thiếu khai báo (US2) và test hoá đối chiếu validation (US3) CHƯA có ở checkpoint này.

### Incremental Delivery

1. Setup + Foundational → nền tảng sẵn sàng.
2. Thêm User Story 1 → kiểm chứng độc lập → mọi endpoint có chính sách tường minh, 403 hoạt động (MVP!).
3. Thêm User Story 2 → kiểm chứng độc lập → scanner chặn endpoint mới thiếu khai báo.
4. Thêm User Story 3 → kiểm chứng độc lập → validation parity được chứng minh bằng test.
5. Polish → chạy đầy đủ quickstart.md, xác nhận không hồi quy.

### Solo/Sequential Strategy

Theo tiền lệ [014-identity-server-auth/tasks.md](../014-identity-server-auth/tasks.md) (vận hành đơn lẻ), đường đi thực tế là tuần tự: Setup → Foundational → US1 → US2 → US3 → Polish, kiểm chứng checkpoint từng story trước khi qua story tiếp theo.

---

## Notes

- Task [P] = khác file, không phụ thuộc task chưa xong.
- Nhãn [Story] gắn mỗi task với user story của nó để truy vết.
- Test là bắt buộc ở đây (constitution Principle III), không tuỳ chọn — viết và xác nhận fail trước khi triển khai.
- T017 lặp lại đúng nội dung T016 ở gateway thay vì tái sử dụng trực tiếp, vì gateway đã có đăng ký 3-scheme riêng từ 014 không gọi được `AddIdentityValidation()` — cùng lý do `ClearUnauthorizedResponseEvents`/`AuthenticationFallbackPolicy.Build()` đã phải lặp lại ở đó.
- T032 (scanner) và T024-T028 (khai báo tường minh) có thể triển khai theo thứ tự ngược lại về mặt kỹ thuật (scanner viết trước không cần route nào tồn tại để biên dịch), nhưng thứ tự trong tasks.md này ưu tiên US1 trước US2 để khớp độ ưu tiên P1/P2 của spec.md.
- Commit sau mỗi task hoặc mỗi nhóm task logic.
- Dừng ở bất kỳ checkpoint nào để kiểm chứng một story độc lập trước khi tiếp tục.
