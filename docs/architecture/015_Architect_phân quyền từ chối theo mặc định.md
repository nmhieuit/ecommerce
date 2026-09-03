# Kiến trúc: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira [SCRUM-24](https://nmhieuit.atlassian.net/browse/SCRUM-24) ("[SECURE-3] Deny-by-default
authorization on every endpoint/handler", roadmap.md Phase 3), đặc tả tại
[`specs/015-deny-by-default-authz/`](../../specs/015-deny-by-default-authz/), xây trên nền xác thực
độc lập đã dựng ở [014-identity-server-auth](../../specs/014-identity-server-auth/) (giữ nguyên không
đổi — tính năng này đóng nốt nửa "phân quyền" của constitution Principle VI mà 014 chủ đích để ngỏ).
Bảy quyết định kiến trúc chi tiết: [`research.md`](../../specs/015-deny-by-default-authz/research.md).

**Trạng thái xác minh**: toàn bộ 38/38 task trong `tasks.md` đã hoàn thành. Không chỉ là bộ test tự
động xanh trên từng service — tính năng còn được xác nhận bằng một thử nghiệm chủ động: thêm tạm một
route không khai báo phân quyền, xác nhận scanner thật sự chặn lại, rồi gỡ đi và xác nhận xanh trở
lại (mục 8). Lượt chạy toàn bộ `dotnet test` cuối phiên có một phát hiện quan trọng về môi trường máy
phát triển — không phải hồi quy của tính năng — trình bày trung thực ở mục 8, không che giấu.

## 1. Kiến trúc tổng thể

```
                    shared/Identity — chính sách "ApiScope" dùng chung
        ┌───────────────────────────────────────────────────────────────┐
        │ AuthorizationPolicies        — tên chính sách "ApiScope",      │
        │                                giá trị scope bắt buộc          │
        │                                "ecommerce-api"                 │
        │ AuthorizationToggleOptions   — cờ AuthorizationRequireApiScope │
        │                                (đọc lại mỗi request qua         │
        │                                IOptionsMonitor, không cache)    │
        │ RequireApiScopeRequirement   — yêu cầu đánh dấu (marker)        │
        │ RequireApiScopeAuthorizationHandler                             │
        │   → toggle TẮT: Succeed() vô điều kiện (giữ hành vi trước 015) │
        │   → toggle BẬT: chỉ Succeed() nếu claim "scope" (một claim gộp │
        │     khoảng trắng hoặc nhiều claim rời) chứa "ecommerce-api"    │
        │ ClearForbiddenResponseEvents — 403 kèm thân JSON rõ ràng,      │
        │                                không phải 403 rỗng mặc định     │
        └───────────────────────────┬─────────────────────────────────┘
                                     │ AddIdentityValidation() (BFF + 4 domain
                                     │ service, đăng ký sẵn) — gateway lặp lại
                                     │ cùng nội dung trong
                                     │ ToggleGatedAuthenticationExtensions
                                     │ (không gọi được AddIdentityValidation
                                     │ trực tiếp — lý do ở mục 2.3)
          ┌──────────────────────────┼──────────────────────────────────┐
          ▼                          ▼                                  ▼
 ┌──────────────────┐      ┌───────────────────────┐      ┌───────────────────────┐
 │ Gateway.Api        │      │ Bff.Api                │      │ Baskets/Orders/        │
 │ FallbackPolicy đã   │      │ Mỗi route (5 file       │      │ Parties/Products.Api   │
 │ nâng cấp che duy    │      │ Features/*/Endpoints.cs)│      │ Mỗi route: khai báo    │
 │ nhất route catch-all│      │ khai báo tường minh     │      │ tường minh             │
 │ MapReverseProxy()   │      │ .RequireAuthorization(  │      │ .RequireAuthorization( │
 │ (không có route     │      │ AuthorizationPolicies.  │      │ AuthorizationPolicies. │
 │ riêng để khai báo)  │      │ ApiScope)               │      │ ApiScope)              │
 └──────────────────┘      └───────────────────────┘      └───────────────────────┘

                    tests/CrossServiceIsolation.Tests
        ┌───────────────────────────────────────────────────────────────┐
        │ AuthorizationPolicyDeclaredScanner — đọc mã nguồn tĩnh mọi      │
        │ *Endpoints.cs (trừ HealthCheckEndpoints.cs), xác nhận mỗi       │
        │ Map(Get|Post|Put|Delete|Patch) có .RequireAuthorization(...)   │
        │ hoặc .AllowAnonymous() chained; quét IConsumer<T> toàn repo     │
        │ phòng ngừa (0 handler tồn tại hôm nay — pass rỗng có chủ đích)  │
        └───────────────────────────┬─────────────────────────────────┘
                                     │ chạy trong PR gate hiện có
                                     │ (013-sonarqube-merge-blocker:
                                     │ build → unit → integration → contract
                                     │ → SonarQube, không có đường vòng)
                                     ▼
                    Build/PR THẤT BẠI nếu thiếu khai báo — không merge được
```

Nguyên lý xuyên suốt (constitution Principle VI, nửa "phân quyền"): **mọi endpoint và message handler
phải khai báo tường minh một quyết định phân quyền — không có mặc định ngầm định nào được coi là hợp
lệ.** `FallbackPolicy` (014, đã nâng cấp) vẫn là lưới an toàn cho một endpoint lỡ quên khai báo, nhưng
bản thân sự khai báo tường minh tại từng route mới là điều FR-001 yêu cầu và điều scanner kiểm chứng.

## 2. Mô tả từng thành phần

### 2.1. `shared/Identity` — chính sách `ApiScope` toggle-gated

- **`AuthorizationPolicies.cs`**: hai hằng số — `ApiScope` (tên chính sách mọi route gọi
  `.RequireAuthorization(...)`) và `RequiredApiScopeValue = "ecommerce-api"` (literal, khớp
  `Identity.Api.Config.ApiScopeName` theo hợp đồng chứ không tham chiếu project — cùng lý do
  `TenantClaimsProfileService.TenantClaimType` của 014 là literal).
- **`AuthorizationToggleOptions.cs`**: bind từ section `FeatureToggles` (cùng section
  `IdentityServerAuthCutover` của 014 dùng), thuộc tính `AuthorizationRequireApiScope` (`bool`, mặc
  định `false` — an toàn khi thiếu cấu hình).
- **`RequireApiScopeRequirement.cs`**: một `IAuthorizationRequirement` đánh dấu, không mang dữ liệu.
- **`RequireApiScopeAuthorizationHandler.cs`**: đọc `IOptionsMonitor<AuthorizationToggleOptions>` tại
  mỗi lần đánh giá — không cache ở constructor, để việc gạt toggle có hiệu lực ngay lập tức (constitution
  Principle X). Toggle tắt → `Succeed()` vô điều kiện; toggle bật → chỉ `Succeed()` nếu principal có
  claim `scope` chứa `ecommerce-api` (xử lý cả hai hình dạng: một claim gộp khoảng trắng hoặc nhiều
  claim `scope` riêng lẻ — token thật từ Duende/JwtBearer có thể phát hành theo một trong hai cách tuỳ
  cấu hình claims-mapping).
- **`AuthenticationFallbackPolicy.cs`** (nâng cấp): `Build()` giờ thêm
  `.AddRequirements(new RequireApiScopeRequirement())` bên cạnh `RequireAuthenticatedUser()` đã có từ
  014 — `FallbackPolicy` deliberately ít nhất nghiêm ngặt bằng policy `ApiScope` mà mọi route khai báo
  tường minh, để một endpoint quên khai báo không bao giờ được bảo vệ *kém hơn* một endpoint có khai
  báo.
- **`ClearForbiddenResponseEvents.cs`**: `IAuthorizationMiddlewareResultHandler` tuỳ biến, bọc
  `AuthorizationMiddlewareResultHandler` mặc định — khi `PolicyAuthorizationResult.Forbidden`, ghi thân
  JSON `{"error":"forbidden_scope","message":"..."}` thay vì 403 rỗng mặc định của framework. Đối
  xứng với `ClearUnauthorizedResponseEvents` (401, 014).
- **`IdentityValidationExtensions.AddIdentityValidation()`** (cập nhật): đăng ký
  `services.Configure<AuthorizationToggleOptions>(...)`, `IAuthorizationHandler`
  (`RequireApiScopeAuthorizationHandler`), `IAuthorizationMiddlewareResultHandler`
  (`ClearForbiddenResponseEvents`), và named policy `ApiScope` bên cạnh `FallbackPolicy` đã nâng cấp.

### 2.2. Gateway (`services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs`)

Không gọi được `AddIdentityValidation()` trực tiếp — 014 đã cho gateway một đăng ký 3-scheme riêng
(`AddPolicyScheme` chọn `JwtBearer`/`StubIdentity` mỗi request) không tương thích với việc gọi thẳng
helper dùng chung. Vì vậy `ToggleGatedAuthenticationExtensions` lặp lại đúng nội dung
`AddIdentityValidation()` vừa thêm (toggle options, handler, result handler, named policy `ApiScope`)
— cùng lý do nó đã phải lặp lại `ClearUnauthorizedResponseEvents`/`AuthenticationFallbackPolicy.Build()`
từ 014.

Gateway không có route nghiệp vụ nào để khai báo `.RequireAuthorization(...)` riêng lẻ — toàn bộ lưu
lượng ngoài health probe đi qua đúng một `MapReverseProxy()` catch-all — nên chính sách của nó là
`FallbackPolicy` (đã nâng cấp) áp dụng đồng nhất, không phải khai báo per-route.

### 2.3. Khai báo tường minh trên từng route (BFF + 4 domain service)

Mọi route trong 9 file `*Endpoints.cs` (4 domain service + 5 file `Features/*/Endpoints.cs` của BFF)
chain thêm `.RequireAuthorization(AuthorizationPolicies.ApiScope)`; hai health probe mỗi service giữ
nguyên `.AllowAnonymous()` đã có từ 014. `service-manifest.yaml` của cả 5 service được cập nhật thêm
trường `authorization:` song song `authentication:` đã có, cho mục đích tài liệu hoá — không phải cơ
chế thực thi (cơ chế thực thi là scanner ở mục 2.4).

### 2.4. `AuthorizationPolicyDeclaredScanner` (`tests/CrossServiceIsolation.Tests`)

Mirror `AuthenticatedByDefaultScanner` (014): đọc mã nguồn tĩnh, không khởi động service nào.
`ScanEndpoints()` trích từng call site `Map(Get|Post|Put|Delete|Patch)(...)`, xác định điểm đóng ngoặc
khớp, rồi quét chuỗi fluent-chain phía sau tới dấu `;` đầu tiên ở paren-depth 0 để tìm
`.RequireAuthorization(`/`.AllowAnonymous()`. `ScanConsumers()` quét toàn `services/**/*.cs` tìm
`IConsumer<T>` thiếu doc-comment "Trusted source:" (contracts/message-handler-authorization-contract.md)
— pass rỗng hôm nay vì không có handler nào tồn tại, nhưng sẽ chặn ngay khi handler đầu tiên được thêm
mà thiếu khai báo. `gateway`/`identity` cố ý không nằm trong `AuthorizingServices` (lý do ở mục 2.2 và
vì `identity` không phục vụ endpoint nghiệp vụ nào — chỉ phát hành token).

## 3. Bảng quyết định — khi nào 401, 403, hay xử lý bình thường

| Tình huống | Trạng thái toggle `AuthorizationRequireApiScope` | Kết quả |
|---|---|---|
| Không có token / token giả mạo / hết hạn | Bất kỳ | `401` (không đổi từ 014) |
| Token hợp lệ, đã xác thực, **thiếu** claim `scope=ecommerce-api` | **Tắt** | Cho qua — giữ đúng hành vi trước tính năng 015 |
| Token hợp lệ, đã xác thực, **thiếu** claim `scope=ecommerce-api` | **Bật** | `403` kèm `{"error":"forbidden_scope","message":"..."}` |
| Token hợp lệ, đã xác thực, **có** claim `scope=ecommerce-api` | Bất kỳ | Xử lý bình thường |
| Route đã đánh dấu `AllowAnonymous()` (health probe) | Bất kỳ | Cho qua, không kiểm tra phân quyền |

## 4. Toggle & rollback không cần redeploy

Cùng cơ chế cấu hình `FeatureToggles`/`IOptionsMonitor` mà 014 đã dùng cho `IdentityServerAuthCutover`
(ADR-0008 chọn Unleash ở cấp kiến trúc, nhưng chưa service nào triển khai hạ tầng Unleash thật — xem
014's `FeatureToggleOptions.cs` remarks; xây riêng hạ tầng đó chỉ để phục vụ một toggle của tính năng
này vượt phạm vi hợp lý). Mặc định `false` ở `appsettings.json` (an toàn — trạng thái rollback), `true`
ở `appsettings.Development.json` (để chạy được `quickstart.md` cục bộ).

**Điểm quan trọng khác với toggle của 014**: khai báo tường minh `.RequireAuthorization(ApiScope)` tại
từng route **không phụ thuộc** trạng thái toggle — nó luôn tồn tại trong mã nguồn, luôn được scanner
kiểm chứng. Toggle chỉ chi phối **nội dung** mà `RequireApiScopeAuthorizationHandler` đánh giá bên
trong policy đó. Nói cách khác: gạt toggle về tắt không làm "biến mất" quyết định phân quyền của một
route — nó chỉ làm quyết định đó tạm thời nới lỏng về đúng mức "chỉ cần đã xác thực" như trước 015.

## 5. Giới hạn phạm vi đã biết

- **US3 không thêm quy tắc nghiệp vụ mới.** Rà soát mã nguồn khi lập kế hoạch (research.md Decision 7)
  phát hiện các kiểm tra phía máy chủ tương ứng đã tồn tại từ trước (`CheckoutEndpoints.cs` — 409 khi
  giỏ hàng rỗng; `BasketEndpoints.cs` — 400 khi `Quantity < 1`/`UnitPrice < 0`; `OrderEndpoints.cs` —
  400 khi `Items` rỗng). Công việc thực tế của US3 là kiểm kê và bổ sung đúng một test còn thiếu bằng
  chứng (`CurrentBasketTests.AddItem_Rejects_ANegativeUnitPrice`) — chi tiết đầy đủ ở
  [`tasks.md`](../../specs/015-deny-by-default-authz/tasks.md) ghi chú T033-T035.
- **`gateway`/`identity` nằm ngoài `AuthorizationPolicyDeclaredScanner.AuthorizingServices`** — lý do:
  gateway chỉ có một route catch-all không có granularity để khai báo riêng; `identity` phát hành
  token, không phục vụ endpoint nghiệp vụ nào cần chính sách `ApiScope`.
- **Chưa có phân quyền theo vai trò (RBAC) chi tiết** — chính sách `ApiScope` hiện là nhị phân (có/
  không đúng scope), khớp đúng phạm vi Jira SCRUM-24. `AddIdentity<ApplicationUser, IdentityRole>()`
  đã sẵn có ở service `identity` (014) nhưng chưa seed vai trò nào — một mở rộng tương lai, không phải
  khoảng trống của tính năng này.
- **Không có message handler nào tồn tại để kiểm chứng `ScanConsumers()` thật sự chặn được vi phạm** —
  guard hiện chỉ chứng minh được nó "chạy qua toàn bộ services" (structural), chưa chứng minh được nó
  "bắt được vi phạm thật" như `ScanEndpoints()` đã được chứng minh (mục 8) — vì chưa có handler nào để
  thử nghiệm.

## 6. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/015-deny-by-default-authz-component.drawio`](../diagrams/015-deny-by-default-authz-component.drawio)
- Sơ đồ luồng nghiệp vụ (phi kỹ thuật): [`docs/diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio`](../diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio)
- Sơ đồ trình tự kỹ thuật: [`docs/diagrams/015-deny-by-default-authz-sequence.drawio`](../diagrams/015-deny-by-default-authz-sequence.drawio)

## 7. Sanity check thật đã thực hiện trên scanner (không chỉ lý thuyết)

Trong lúc triển khai US2 (tasks.md ghi chú T030-T032), đội đã thêm tạm một route
`GET /products/temp-scanner-sanity-check` vào `CatalogEndpoints.cs` **không** khai báo
`.RequireAuthorization(...)`/`.AllowAnonymous()`, chạy
`AuthorizationPolicyDeclaredScannerTests.EveryMappedRoute_DeclaresAnAuthorizationDecision` → **FAIL**
đúng như kỳ vọng, thông báo nêu rõ route và file vi phạm. Gỡ route thử nghiệm, chạy lại → **PASS**.
Đây là bằng chứng scanner thật sự bắt được vi phạm, không chỉ pass một cách vô nghĩa vì quét nhầm thư
mục hay không tìm thấy file nào.

## 8. Trạng thái xác minh đầy đủ theo từng project test

Số liệu dưới đây lấy từ các lượt `dotnet test` chạy thật trong phiên triển khai, không suy đoán.

| Project | Kết quả |
|---|---|
| `Identity.UnitTests` (`shared/`, mở rộng — 8 test mới của 015) | 15/15 pass |
| `Baskets.Api.IntegrationTests` (gồm `AuthorizationPolicyTests` mới + `AddItem_Rejects_ANegativeUnitPrice` mới) | 27/27 pass |
| `Orders.Api.IntegrationTests` (gồm `AuthorizationPolicyTests` mới) | 24/24 pass |
| `Parties.Api.IntegrationTests` (gồm `AuthorizationPolicyTests` mới) | 14/14 pass |
| `Products.Api.IntegrationTests` (gồm `AuthorizationPolicyTests` mới) | 18/18 pass |
| `Identity.Api.IntegrationTests` (không đổi bởi 015) | 2/2 pass |
| `tests/CrossServiceIsolation.Tests` (gồm 4 test `AuthorizationPolicyDeclaredScannerTests` mới) | 21/21 pass |
| Mọi `*.Api.UnitTests` (Baskets/Bff/Gateway/Orders/Parties/Products), `Tenancy.UnitTests`, `EventContracts.UnitTests`, `ContainerConventionTests`, `ContractCoverageTests`, `StructureConventionTests`, `IntegrationTestSupport.Tests` | Tất cả pass 100% |
| `Gateway.Api.IntegrationTests` | 17/31 pass — 14 fail |
| `Bff.Api.IntegrationTests` | 27/48 pass — 21 fail |
| `Baskets.Api.ContractTests` / `Orders.Api.ContractTests` / `Products.Api.ContractTests` (Pact) | 1 fail mỗi project |

**Về các dòng fail cuối bảng — kết luận: vấn đề môi trường, không phải hồi quy của tính năng 015.**
Docker Desktop trên máy phát triển bị dừng và phải khởi động lại giữa phiên làm việc; ngay cả sau khi
daemon đã sẵn sàng, việc bắc cầu HTTP giữa hai `WebApplicationFactory` in-process trở lên (đúng cơ chế
`Gateway.Api.IntegrationTests` và `Bff.Api.IntegrationTests` dùng để gọi sang service khác) thể hiện độ
trễ ~4.5s một cách nhất quán, vượt ngân sách `AttemptTimeout=1s`/`TotalRequestTimeout=3s` khai báo tại
`services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs` (cấu hình có từ
trước, không đổi bởi 015). Ba bằng chứng cụ thể:

1. Cùng tập hợp test này đã fail hệt vậy ở một lượt `dotnet test` chạy đầu phiên làm việc, **trước khi
   bất kỳ dòng code Phase 3-5 nào của 015 tồn tại**.
2. Chạy cô lập `Bff.Api.IntegrationTests.ProductsRouteTests` — route hoàn toàn không đụng tới logic
   checkout hay bất kỳ đường phân quyền nào 015 thay đổi — vẫn fail với cùng dấu hiệu độ trễ ~4.5s.
3. `BffTestHost.cs` (có từ trước 015) đã tự ghi chú hiện tượng tương tự trong doc-comment của nó: "under
   Docker contention the DNS path was observed exceeding the 1 s attempt timeout".

Không có thay đổi nào được thực hiện để "vá" hiện tượng này — đây là hạn chế môi trường tại thời điểm
kiểm chứng, nằm ngoài phạm vi tính năng 015. Chi tiết đầy đủ: [`tasks.md`](../../specs/015-deny-by-default-authz/tasks.md)
ghi chú T038.
