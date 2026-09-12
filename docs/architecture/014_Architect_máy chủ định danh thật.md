# Kiến trúc: Máy chủ định danh thay thế xác thực giả lập

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira SCRUM-23 ("[SECURE-3] Stand up identity server, replace stubbed auth"), đặc tả
tại [`specs/014-identity-server-auth/`](../../specs/014-identity-server-auth/), xây trên nền lan
truyền tenant/subject đã dựng ở
[`specs/003-stub-identity-tenant-context`](../../specs/003-stub-identity-tenant-context/) (giữ
nguyên không đổi — chỉ nguồn xác định tenant thay đổi). Quyết định kiến trúc gốc:
[ADR-0001](../adr/0001-identity-provider.md) (chọn Duende IdentityServer) và
[ADR-0008](../adr/0008-feature-toggle-system.md) (hệ thống feature toggle — xem
[technical-debt.md](technical-debt.md) về 1 điểm AMENDED so với ADR gốc).

**Trạng thái xác minh**: toàn bộ 47/47 task trong `tasks.md` đã hoàn thành. Tính năng này đã được chạy
**thật** trên một môi trường đầy đủ (service `identity` + 4 domain service + BFF + gateway, mỗi
service một SQL Server container riêng, qua `docker-compose.local.yml`), thực hiện đăng nhập thật,
lấy token thật, và gửi request thật qua toàn bộ đường đi — lượt chạy đó phát hiện và vá ba lỗ hổng
cấu hình thật mà không bộ test tự động nào bắt được, xem [technical-debt.md](technical-debt.md).

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
  cả trường hợp `TenantId` hợp lệ, `TenantId` rỗng, và không tìm thấy user.
- `Config.cs` khai báo hai client: `ecommerce-web-spa` (Authorization Code + PKCE — client production
  duy nhất, research.md Decision 9) và `integration-test-ropc` (Resource Owner Password, có secret,
  chỉ dùng cho test — xem [technical-debt.md](technical-debt.md)).

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

- `AddJwtBearer` với `Authority`/`Audience` đọc từ config section `Identity`.
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
`TenantContext` đã có từ 003 — `shared/Tenancy` xử lý y hệt trường hợp header `X-Tenant-Id` vắng mặt.

**Lưu ý vận hành về `ClockSkew`**: `JwtBearerHandler` mặc định dung sai 5 phút quanh `exp` (hành vi
mặc định của thư viện, không bị tắt trong tính năng này). Một token hết hạn 8 giây trước vẫn được
chấp nhận — đây là lý do bộ test tự động dùng `exp = now - 5 phút`, không phải "vừa hết hạn".

## 4. Toggle không cần redeploy

Toggle đọc từ `FeatureToggles:IdentityServerAuthCutover` qua `IOptionsMonitor`, đánh giá lại mỗi
request (mục 2.2) — nên hot-reload một ConfigMap/file cấu hình vẫn có tác dụng ngay, không cần restart
pod. Kể từ khi US2 (mục 2.3) triển khai, toggle này chỉ kiểm soát lớp xác thực **của riêng gateway** —
BFF và mọi domain service vẫn luôn tự xác thực độc lập, bất kể gateway nói gì (đúng thiết kế Principle
VI). Bối cảnh ADR-0008/Unleash và bằng chứng thực nghiệm đo trực tiếp trên container đang chạy: xem
[technical-debt.md](technical-debt.md).

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/014-identity-server-component.drawio`](../diagrams/014-identity-server-component.drawio)
- Sơ đồ trình tự (đăng nhập → token → request qua từng lớp xác thực, gồm nhánh token hết hạn/giả
  mạo): [`docs/diagrams/014-identity-server-sequence.drawio`](../diagrams/014-identity-server-sequence.drawio)

Ba lỗ hổng cấu hình thật đã phát hiện/vá, bằng chứng thực nghiệm toggle, bảng test đầy đủ theo từng
project, và giới hạn phạm vi (chưa có màn hình đăng nhập tương tác): xem
[technical-debt.md](technical-debt.md).
