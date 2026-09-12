# Kiến trúc: Phân quyền từ chối theo mặc định trên mọi endpoint/handler

*Đối tượng đọc: kỹ sư phần mềm / software architect gia nhập dự án, cần hiểu hệ thống hoạt động ra
sao để bảo trì hoặc mở rộng.*

**Nguồn gốc**: Jira [SCRUM-24](https://nmhieuit.atlassian.net/browse/SCRUM-24) ("[SECURE-3] Deny-by-default
authorization on every endpoint/handler", roadmap.md Phase 3), đặc tả tại
[`specs/015-deny-by-default-authz/`](../../specs/015-deny-by-default-authz/), xây trên nền xác thực
độc lập đã dựng ở [014-identity-server-auth](../../specs/014-identity-server-auth/) (giữ nguyên không
đổi — tính năng này đóng nốt nửa "phân quyền" của constitution Principle VI mà 014 chủ đích để ngỏ).
Bảy quyết định kiến trúc chi tiết: [`research.md`](../../specs/015-deny-by-default-authz/research.md).

**Trạng thái xác minh**: toàn bộ 38/38 task trong `tasks.md` đã hoàn thành. Ngoài bộ test tự động,
tính năng còn được xác nhận bằng một thử nghiệm chủ động (scanner thật sự chặn được vi phạm — xem
[technical-debt.md](technical-debt.md)).

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
                                     │ trực tiếp — lý do ở mục 2.2)
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
  Principle X).
- **`AuthenticationFallbackPolicy.cs`** (nâng cấp): `Build()` giờ thêm
  `.AddRequirements(new RequireApiScopeRequirement())` bên cạnh `RequireAuthenticatedUser()` đã có từ
  014 — `FallbackPolicy` deliberately ít nhất nghiêm ngặt bằng policy `ApiScope` mà mọi route khai báo
  tường minh, để một endpoint quên khai báo không bao giờ được bảo vệ *kém hơn* một endpoint có khai
  báo.
- **`ClearForbiddenResponseEvents.cs`**: `IAuthorizationMiddlewareResultHandler` tuỳ biến, bọc
  `AuthorizationMiddlewareResultHandler` mặc định — khi `PolicyAuthorizationResult.Forbidden`, ghi thân
  JSON `{"error":"forbidden_scope","message":"..."}` thay vì 403 rỗng mặc định của framework. Đối
  xứng với `ClearUnauthorizedResponseEvents` (401, 014).

### 2.2. Gateway (`services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs`)

Không gọi được `AddIdentityValidation()` trực tiếp — 014 đã cho gateway một đăng ký 3-scheme riêng
không tương thích với việc gọi thẳng helper dùng chung, nên `ToggleGatedAuthenticationExtensions` lặp
lại đúng nội dung của nó (toggle options, handler, result handler, named policy `ApiScope`). Gateway
không có route nghiệp vụ nào để khai báo `.RequireAuthorization(...)` riêng lẻ — toàn bộ lưu lượng
ngoài health probe đi qua đúng một `MapReverseProxy()` catch-all — nên chính sách của nó là
`FallbackPolicy` (đã nâng cấp) áp dụng đồng nhất, không phải khai báo per-route.

### 2.3. Khai báo tường minh trên từng route (BFF + 4 domain service)

Mọi route trong 9 file `*Endpoints.cs` (4 domain service + 5 file `Features/*/Endpoints.cs` của BFF)
chain thêm `.RequireAuthorization(AuthorizationPolicies.ApiScope)`; hai health probe mỗi service giữ
nguyên `.AllowAnonymous()` đã có từ 014. `service-manifest.yaml` của cả 5 service được cập nhật thêm
trường `authorization:` song song `authentication:` — cho mục đích tài liệu hoá, không phải cơ chế
thực thi (cơ chế thực thi là scanner ở mục 2.4).

### 2.4. `AuthorizationPolicyDeclaredScanner` (`tests/CrossServiceIsolation.Tests`)

Mirror `AuthenticatedByDefaultScanner` (014): đọc mã nguồn tĩnh, không khởi động service nào.
`ScanEndpoints()` trích từng call site `Map(Get|Post|Put|Delete|Patch)(...)`, xác định điểm đóng ngoặc
khớp, rồi quét chuỗi fluent-chain phía sau tới dấu `;` đầu tiên ở paren-depth 0 để tìm
`.RequireAuthorization(`/`.AllowAnonymous()`. `ScanConsumers()` quét toàn `services/**/*.cs` tìm
`IConsumer<T>` thiếu doc-comment "Trusted source:" — pass rỗng hôm nay vì không có handler nào tồn
tại. `gateway`/`identity` cố ý không nằm trong `AuthorizingServices` — xem
[technical-debt.md](technical-debt.md).

## 3. Bảng quyết định — khi nào 401, 403, hay xử lý bình thường

| Tình huống | Trạng thái toggle `AuthorizationRequireApiScope` | Kết quả |
|---|---|---|
| Không có token / token giả mạo / hết hạn | Bất kỳ | `401` (không đổi từ 014) |
| Token hợp lệ, đã xác thực, **thiếu** claim `scope=ecommerce-api` | **Tắt** | Cho qua — giữ đúng hành vi trước tính năng 015 |
| Token hợp lệ, đã xác thực, **thiếu** claim `scope=ecommerce-api` | **Bật** | `403` kèm `{"error":"forbidden_scope","message":"..."}` |
| Token hợp lệ, đã xác thực, **có** claim `scope=ecommerce-api` | Bất kỳ | Xử lý bình thường |
| Route đã đánh dấu `AllowAnonymous()` (health probe) | Bất kỳ | Cho qua, không kiểm tra phân quyền |

## 4. Toggle & rollback không cần redeploy

Mặc định `false` ở `appsettings.json` (an toàn — trạng thái rollback), `true` ở
`appsettings.Development.json` (để chạy được `quickstart.md` cục bộ). Bối cảnh ADR-0008/Unleash: xem
[technical-debt.md](technical-debt.md).

**Điểm quan trọng khác với toggle của 014**: khai báo tường minh `.RequireAuthorization(ApiScope)` tại
từng route **không phụ thuộc** trạng thái toggle — nó luôn tồn tại trong mã nguồn, luôn được scanner
kiểm chứng. Toggle chỉ chi phối **nội dung** mà `RequireApiScopeAuthorizationHandler` đánh giá bên
trong policy đó. Nói cách khác: gạt toggle về tắt không làm "biến mất" quyết định phân quyền của một
route — nó chỉ làm quyết định đó tạm thời nới lỏng về đúng mức "chỉ cần đã xác thực" như trước 015.

## 5. Sơ đồ

- Sơ đồ thành phần: [`docs/diagrams/015-deny-by-default-authz-component.drawio`](../diagrams/015-deny-by-default-authz-component.drawio)
- Sơ đồ luồng nghiệp vụ (phi kỹ thuật): [`docs/diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio`](../diagrams/015-deny-by-default-authz-flow-nghiep-vu.drawio)
- Sơ đồ trình tự kỹ thuật: [`docs/diagrams/015-deny-by-default-authz-sequence.drawio`](../diagrams/015-deny-by-default-authz-sequence.drawio)

Sanity check thật trên scanner, bảng test đầy đủ theo project (gồm phân tích các dòng fail do môi
trường), và giới hạn phạm vi đã biết: xem [technical-debt.md](technical-debt.md).
