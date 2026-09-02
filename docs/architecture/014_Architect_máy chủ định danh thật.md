# Kiến trúc: Máy chủ định danh thay thế xác thực giả lập

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-23 ("[SECURE-3] Stand up identity server, replace stubbed auth"), đặc tả
tại [`specs/014-identity-server-auth/`](../../specs/014-identity-server-auth/), xây trên nền lan
truyền tenant/subject đã dựng ở
[`specs/003-stub-identity-tenant-context`](../../specs/003-stub-identity-tenant-context/) (giữ
nguyên không đổi — chỉ nguồn xác định tenant thay đổi). Quyết định kiến trúc gốc:
[ADR-0001](../adr/0001-identity-provider.md) (chọn Duende IdentityServer) và
[ADR-0008](../adr/0008-feature-toggle-system.md) (hệ thống feature toggle — xem mục 4 về một điểm
AMENDED quan trọng so với ADR gốc).

**Trạng thái xác minh**: toàn bộ 47/47 task trong `tasks.md` đã hoàn thành. Không chỉ là bộ test tự
động xanh (xem số liệu đầy đủ ở mục 8) — tính năng này đã được chạy **thật** trên một môi trường đầy
đủ (service `identity` + 4 domain service + BFF + gateway, mỗi service một SQL Server container
riêng, qua `docker-compose.local.yml`), thực hiện đăng nhập thật, lấy token thật, và gửi request thật
qua toàn bộ đường đi — kết quả đầy đủ ghi ở khối **"T047 results"** cuối
[`tasks.md`](../../specs/014-identity-server-auth/tasks.md). Lượt chạy đó phát hiện và vá ba lỗ hổng
cấu hình thật mà không bộ test tự động nào bắt được (mục 5) — đây không phải mô tả lý thuyết.

## 1. Kiến trúc tổng thể

```
                     ┌─────────────────────────────────────────────────┐
                     │  service `identity` (Duende IdentityServer)      │
                     │  DB riêng — tách biệt hoàn toàn khỏi DB `parties`│
                     │  /connect/token, JWKS, OpenID discovery          │
                     └───────────────────────┬───────────────────────┘
                                              │ mọi service tự fetch JWKS
                                              │ và tự validate — KHÔNG service
                                              │ nào coi service khác là nguồn
                                              │ tin cậy trung gian
                       ┌──────────────────────┼───────────────────────┐
                       │                      │                       │
                       ▼                      ▼                       ▼
              ┌─────────────────┐   ┌──────────────────┐   ┌──────────────────────┐
              │ Gateway.Api      │   │ Bff.Api           │   │ Parties/Products/     │
              │ toggle: JwtBearer│──▶│ AddIdentityValid- │──▶│ Baskets/Orders.Api    │
              │ hoặc StubIdentity│   │ ation() (tự xác    │   │ AddIdentityValidation │
              │ (research.md     │   │ thực độc lập,      │   │ () — tự xác thực độc  │
              │ Decision 7)      │   │ không tin gateway) │   │ lập, không tin BFF    │
              └─────────────────┘   └──────────────────┘   └──────────────────────┘
```

Nguyên lý xuyên suốt (constitution Principle VI): **gateway không phải là ranh giới tin cậy các
service khác dựa vào**. Việc gateway xác thực token trước khi chuyển tiếp chỉ là một lớp phòng thủ
đầu tiên, không phải điều kiện đủ — mỗi service phía sau (BFF, và cả 4 domain service) tự fetch JWKS
của chính mình từ `identity` và tự xác minh lại chữ ký/`iss`/`aud`/`exp` của cùng một token gốc, dù
request có đi qua gateway hay không.

## 2. Mô tả từng thành phần

### 2.1. Service `identity` (`services/identity/src/Identity.Api`)

- Duende IdentityServer 8.0.6 chạy như một ASP.NET Core service bình thường — cùng khuôn mẫu
  container/pipeline/Ansible với mọi service khác trong hạm đội (ADR-0001, research.md Decision 1).
- Lưu trữ thông tin đăng nhập (username/password hash) qua ASP.NET Core Identity, trong **database
  riêng của chính nó** — tách biệt hoàn toàn khỏi database của `parties` (hồ sơ khách hàng: tên, địa
  chỉ). Hai bên chỉ liên kết logic qua claim `sub`, không chia sẻ bảng/schema (research.md
  Decision 8, constitution Principle I).
- `TenantClaimsProfileService` (`IProfileService` của Duende) là **nguồn phát hành `tenant_id` duy
  nhất** — đọc trực tiếp từ `ApplicationUser.TenantId` của Identity User vừa đăng nhập. Có unit test
  cô lập (`Identity.Api.UnitTests/TenantClaimsProfileServiceTests.cs`, 5 test) xác nhận hành vi cho
  cả trường hợp `TenantId` hợp lệ, `TenantId` rỗng (edge case — service không tự chặn giá trị rỗng,
  vì mỗi bên tiêu thụ token đều PHẢI tự kiểm tra claim này trước khi tin nó, theo data-model.md), và
  không tìm thấy user.
- `Config.cs` khai báo hai client: `ecommerce-web-spa` (Authorization Code + PKCE — client production
  duy nhất, research.md Decision 9) và `integration-test-ropc` (Resource Owner Password, có secret,
  đánh dấu rõ "không dùng cho bất kỳ client thật nào" — chỉ tồn tại để test/chạy thử tự động hoá login
  mà không cần giao diện đăng nhập tương tác, xem mục 6).

### 2.2. Gateway — cutover có toggle (`services/gateway/src/Gateway.Api/Identity/`)

`ToggleGatedAuthenticationExtensions.AddToggleGatedIdentity()` đăng ký một `AddPolicyScheme` chọn
giữa hai scheme **mỗi request** (không phải một lần lúc khởi động):

```csharp
policySchemeOptions.ForwardDefaultSelector = context =>
{
    var toggles = context.RequestServices.GetRequiredService<IOptionsMonitor<FeatureToggleOptions>>();
    return toggles.CurrentValue.IdentityServerAuthCutover
        ? JwtBearerDefaults.AuthenticationScheme
        : StubIdentityAuthenticationHandler.SchemeName;
};
```

Đây chính là thay đổi duy nhất mà `research.md` Decision 2 dự đoán trước khi triển khai: thay một
dòng đăng ký scheme, không đổi gì khác downstream — `TenantHeaderPropagationMiddleware`/
`SubjectHeaderPropagationMiddleware` đọc đúng hai claim `tenant_id`/`sub` (ánh xạ ClaimTypes mặc định
của ASP.NET Core, research.md Decision 3) từ cả token thật lẫn stub, nên không cần sửa một dòng nào.

### 2.3. Xác thực độc lập ở BFF + 4 domain service (`shared/Identity`)

Thư viện chia sẻ mới `AddIdentityValidation()`/`UseIdentityValidation()` (research.md Decision 4),
gọi giống hệt nhau ở `Bff.Api`, `Parties.Api`, `Products.Api`, `Baskets.Api`, `Orders.Api`:

- `AddJwtBearer` với `Authority`/`Audience` đọc từ config section `Identity` (xem mục 5 — đây chính
  là section bị thiếu ở 5/6 service trước khi vá).
- `AddAuthorization(o => o.FallbackPolicy = RequireAuthenticatedUser())` — deny-by-default tường
  minh (research.md Decision 6, constitution Principle VI): mọi endpoint yêu cầu token Valid trừ khi
  đánh dấu `[AllowAnonymous]` tường minh, hiện chỉ áp dụng cho `/health/live` và `/health/ready`.
- `ClearUnauthorizedResponseEvents` cấu hình `OnAuthenticationFailed`/`OnChallenge` để trả JSON
  `{"error": "token_expired"|"unauthorized", "message": "..."}` thay vì 401 rỗng mặc định của
  framework — phân biệt rõ token hết hạn khỏi các lỗi xác thực khác (spec FR-006, US3).

Gateway không gọi trực tiếp `AddIdentityValidation()` (vì cần cơ chế 3-scheme ở mục 2.2), nhưng tái
sử dụng đúng `ClearUnauthorizedResponseEvents` và `AuthenticationFallbackPolicy` từ cùng thư viện —
nên hành vi 401 rõ ràng nhất quán ở mọi nơi, kể cả gateway.

### 2.4. Hợp đồng claim token

Chi tiết đầy đủ:
[`contracts/identity-token-claims-contract.md`](../../specs/014-identity-server-auth/contracts/identity-token-claims-contract.md)
và
[`contracts/service-authentication-contract.md`](../../specs/014-identity-server-auth/contracts/service-authentication-contract.md).
Tóm tắt: JWT RS256, mang `sub`, `tenant_id` (claim tuỳ biến — cùng tên `StubIdentityAuthenticationHandler.TenantClaimType`
cũ, đi qua nguyên vẹn), `iss`, `aud`, `exp`. Đây là hợp đồng nội bộ giữa các service trong cùng một
hệ thống triển khai, không phải hợp đồng công khai theo nghĩa versioning của Principle II.

## 3. Trạng thái ba giá trị của Token

Tại thời điểm bất kỳ service nào xác thực một token, nó luôn ở đúng một trong ba trạng thái
(data-model.md):

| Trạng thái | Điều kiện | Kết quả |
|---|---|---|
| **Valid** | Chữ ký khớp JWKS đã cache, `iss`/`aud` khớp cấu hình, `exp` chưa qua | Xử lý với `sub`/`tenant_id` đã xác minh |
| **Expired** | Mọi điều kiện khác đúng nhưng `exp` đã qua | `401` kèm `{"error":"token_expired"}` — không phải thất bại chung chung |
| **Invalid** | Chữ ký sai, `iss`/`aud` không khớp, thiếu/malformed | `401`, xử lý y hệt request không có token — không có danh tính mặc định |

Không có trạng thái thứ tư "hợp lệ nhưng thiếu `tenant_id`" được coi là một tenant nào đó (spec
FR-010): claim `tenant_id` thiếu hoặc không phân tích được → tương đương trạng thái `Unresolved` của
`TenantContext` đã có từ 003 — `shared/Tenancy` xử lý y hệt trường hợp header `X-Tenant-Id` vắng mặt,
không đổi.

**Lưu ý vận hành về `ClockSkew`**: `JwtBearerHandler` mặc định dung sai 5 phút quanh `exp` (hành vi
mặc định của thư viện, không bị tắt trong tính năng này). Một token hết hạn 8 giây trước vẫn được
chấp nhận — đây là lý do bộ test tự động (`JwtBearerAuthenticationTests.CreateToken(expired: true)`)
dùng `exp = now - 5 phút`, không phải "vừa hết hạn", và cũng là điều đã quan sát thật khi chạy T047.

## 4. Toggle & rollback không cần redeploy — đã kiểm chứng bằng thực nghiệm

**AMENDED so với ADR-0008/research.md Decision 7 gốc**: toggle KHÔNG dùng Unleash thật. ADR-0008 đã
*chọn* Unleash ở cấp kiến trúc, nhưng cả 3 Action Item của ADR đó (deploy self-hosted Unleash, tích
hợp SDK, CI check hạn dùng toggle) chưa được triển khai ở bất kỳ đâu trong nền tảng — xây toàn bộ hạ
tầng Unleash chỉ để phục vụ một toggle của riêng tính năng này là vượt phạm vi hợp lý. Toggle đọc từ
`FeatureToggles:IdentityServerAuthCutover` qua `IOptionsMonitor`, đánh giá lại mỗi request (mục 2.2)
— nên hot-reload một ConfigMap/file cấu hình vẫn có tác dụng ngay, không cần restart pod. Việc thay
nguồn đọc bằng `IFeatureManager` do Unleash cấp sau này chỉ là một thay đổi một dòng tại điểm đọc.

**Điểm quan trọng đã thay đổi kể từ khi US2 (mục 2.3) triển khai**: toggle này giờ chỉ kiểm soát lớp
xác thực **của riêng gateway**, không còn khôi phục hành vi "không cần token nào, ở bất kỳ đâu" của
Phase 1 nữa — vì BFF (và mọi domain service) tự xác thực độc lập, bất kể gateway nói gì. Đây là hệ
quả đúng thiết kế của Principle VI, không phải lỗi, và đã được sửa lại trong
[`quickstart.md`](../../specs/014-identity-server-auth/quickstart.md) Scenario 7 sau khi lượt chạy
T047 phát hiện tài liệu gốc mô tả sai hành vi này.

**Bằng chứng thực nghiệm** (T047 results, Scenario 7): sửa trực tiếp
`FeatureToggles:IdentityServerAuthCutover` từ `true` sang `false` ngay trong file cấu hình đang chạy
bên trong container gateway (`docker exec`, không restart), rồi dừng tạm `bff-api` và gửi lại request
không token qua gateway — nhận `502 Bad Gateway` (không phải `401`). Điều này chứng minh chính gateway
đã ngừng tự đòi token và chỉ chuyển tiếp; khi bật lại `bff-api`, cùng request nhận `401` từ tầng BFF
(xác thực độc lập của nó), không phải từ gateway.

## 5. Ba lỗ hổng cấu hình thật đã phát hiện và vá khi chạy quickstart T047

Không mục nào dưới đây bị `dotnet test` bắt được — mọi bộ test tự động của tính năng này dùng
`IntegrationTestSupport.TestJwtBearer` (một cấu hình test riêng, bỏ qua hoàn toàn bước fetch
JWKS/discovery thật qua `Identity:Authority`), nên một `Authority` bị thiếu/sai hoàn toàn vô hình với
`dotnet test`. Chỉ có lượt chạy T047 chống lại một `Identity.Api` thật mới lộ ra được:

1. **5/6 service (`bff`/`products`/`baskets`/`orders`/`parties`) hoàn toàn không có cấu hình
   `Identity:Authority` ở bất kỳ đâu** (không `appsettings.json`, không `appsettings.Development.json`)
   — nghĩa là `JwtBearerOptions.Authority` sẽ là `null` trong bất kỳ môi trường triển khai thật nào,
   khiến các service này không thể xác thực được BẤT KỲ token thật nào, kể cả token hợp lệ 100%. Chỉ
   `gateway` có cấu hình này từ trước. **Đã vá**: thêm section `Identity` (`Authority` + `Audience`
   cho `appsettings.json`, chỉ `Authority` cho `appsettings.Development.json`) vào cả 5 service, theo
   đúng khuôn mẫu đã có của gateway.
2. **`docker-compose.yml`** (file "deployed shape in miniature" — lệnh `./scripts/up.ps1`) **chưa
   từng được thêm `identity-db`/`identity-migrate`/`identity-api`.** T010/T011 (Phase 1, Setup) chỉ
   thêm service `identity` vào `docker-compose.local.yml` và `docker-compose.deps.yml`. **Đã vá**:
   thêm cả ba entry theo đúng khuôn mẫu SQL-Server-dùng-chung đã có ở file đó, cộng
   `depends_on: identity-api` ở gateway/BFF/4 domain service.
3. **`docker-compose.local.yml`** thiếu override `Identity__Authority` bằng hostname nội bộ Docker
   (`http://identity-api:8080`) cho cả 6 service — cùng loại lỗi mà chính comment đầu file này đã
   cảnh báo trước ("Miss one and the service starts, looks healthy, and fails every call that goes
   through it"). **Đã vá**, cộng `depends_on: identity-api: condition: service_healthy` ở cả 6
   service.

Sau các bản vá trên: `dotnet build Ecommerce.slnx` — 0 lỗi; `tests/CrossServiceIsolation.Tests`,
`tests/StructureConventionTests`, `tests/ContainerConventionTests` — tất cả pass lại.

## 6. Giới hạn phạm vi đã biết

Màn hình đăng nhập tương tác (Authorization Code + PKCE, luồng redirect trình duyệt thật) **chưa
được xây** — `Config.cs` ghi rõ trong doc-comment: client `ecommerce-web-spa` đã đăng ký sẵn, nhưng
"nó không thể hoàn tất một luồng đăng nhập trình duyệt thật đầu-cuối hôm nay" vì Duende's Razor Pages
quickstart UI (research.md Decision 9) là một việc riêng, đã được đánh dấu để làm sau, ngoài phạm vi
Jira SCRUM-23 (spec.md Assumptions: "việc đăng ký và quản lý tài khoản người dùng... nằm ngoài phạm
vi của tính năng này").

Để việc phát hành/kiểm tra token vẫn kiểm thử được đầu-cuối mà không cần UI đó, `Config.cs` đăng ký
thêm client `integration-test-ropc` (Resource Owner Password, có secret, đánh dấu rõ không dùng cho
production) — dùng bởi cả `Identity.Api.IntegrationTests.LoginIssuesTokenTests` và lượt chạy thủ công
T047 (mục "Trạng thái xác minh"). Đây không phải một lỗ hổng bảo mật bị bỏ sót — là một quyết định
phạm vi tường minh, ghi nhận sẵn trong code lẫn `research.md`.

## 7. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/014-identity-server-component.drawio`](../diagrams/014-identity-server-component.drawio)
- Sơ đồ trình tự (đăng nhập → token → request qua từng lớp xác thực, gồm nhánh token hết hạn/giả
  mạo): [`docs/diagrams/014-identity-server-sequence.drawio`](../diagrams/014-identity-server-sequence.drawio)

## 8. Trạng thái xác minh đầy đủ theo từng project test

| Project | Kết quả |
|---|---|
| `Identity.Api.UnitTests` (mới) | 5/5 pass (`TenantClaimsProfileServiceTests`) |
| `Identity.Api.IntegrationTests` (mới, Testcontainers SQL Server thật) | 2/2 pass (`LoginIssuesTokenTests`) |
| `Gateway.Api.IntegrationTests` | 31/31 pass |
| `Bff.Api.IntegrationTests` | 46/46 pass |
| `Products.Api.IntegrationTests` | 16/16 pass |
| `Baskets.Api.IntegrationTests` | 24/24 pass |
| `Orders.Api.IntegrationTests` | 22/22 pass |
| `Parties.Api.IntegrationTests` | 12/12 pass |
| `tests/CrossServiceIsolation.Tests` | 17/17 pass (gồm `AuthenticatedByDefaultScannerTests` — mới, xác nhận cả 6 service gọi đúng một lần `AddIdentityValidation()`/tương đương) |
| quickstart.md Scenario 1-7, chạy thật trên `docker-compose.local.yml` | 7/7 PASS — xem T047 results trong `tasks.md` |

Hai lớp phòng vệ (`Gateway.Api.IntegrationTests.JwtBearerAuthenticationTests` và
`Products.Api.IntegrationTests.IndependentTokenValidationTests`) từng bị 401 sai do một lỗ hổng
test-harness (thiếu `.UseTestJwtBearer()` ở helper dùng chung của bộ test gateway/BFF) — không liên
quan tới ba lỗ hổng cấu hình ở mục 5, đã vá riêng trước khi chạy T047.
