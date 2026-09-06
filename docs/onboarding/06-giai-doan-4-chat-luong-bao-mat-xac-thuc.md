# 06 — Giai đoạn 4: Chất lượng, bảo mật & xác thực thật

> Đọc [01](01-tong-quan-kien-truc.md) và [03](03-giai-doan-1-nen-tang-dich-vu-va-routing.md) trước — giai đoạn này thay thế "danh tính giả lập (stub)" của Giai đoạn 1 bằng 1 máy chủ định danh **thật**, và thêm 1 lớp phân quyền hoàn toàn mới lên trên lớp xác thực đó. Toàn bộ cơ chế `X-Tenant-Id`/`X-Subject-Id`/`TenantContext`/`CallerContext` đã xây ở Giai đoạn 1-2 **không đổi 1 dòng nào** — chỉ nguồn phát sinh ra claim thay đổi.
>
> **Specs thuộc giai đoạn này:** `specs/013-sonarqube-merge-blocker`, `specs/014-identity-server-auth`, `specs/015-deny-by-default-authz`.
> **Kỹ thuật trọng tâm:** cổng chất lượng CI/SonarQube chặn merge, máy chủ định danh thật (Duende IdentityServer) thay stub, và phân quyền deny-by-default trên mọi endpoint.

## Thay đổi trong shared/

### `shared/Identity` ra đời — xác thực độc lập cho mọi service (spec 014)

Commit: `2a421b8 feat(identity): scaffold Identity service and shared Identity library`, `4940818 feat: Implement independent token validation for services`

Trước spec 014, thư viện `shared/Identity` **chưa tồn tại** — mỗi service chỉ có `AddTenancy()`/`AddServiceDefaults()` (Giai đoạn 1-2), và chỉ gateway biết cách "xác thực" (qua `StubIdentityAuthenticationHandler` giả lập). Spec 014 tạo ra `shared/Identity`, cho phép **mọi service tự xác thực token của chính nó**, không cần tin tưởng gateway đã làm việc đó (lý do kiến trúc đã giải thích ở [01-tong-quan-kien-truc.md § 6](01-tong-quan-kien-truc.md#6-vì-sao-mỗi-service-tự-xác-thực-không-tin-gateway)).

Phiên bản ĐẦU TIÊN của `AddIdentityValidation()` (trước khi 015 thêm phân quyền) — chỉ 3 việc:
```csharp
public static IServiceCollection AddIdentityValidation(this IServiceCollection services, IConfiguration configuration)
{
    var identityOptions = configuration.GetSection(IdentityServerOptions.ConfigSectionName).Get<IdentityServerOptions>()
        ?? new IdentityServerOptions();

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(jwtOptions =>
        {
            jwtOptions.Authority = identityOptions.Authority;   // ai phát hành token mà service này tin
            jwtOptions.Audience = identityOptions.Audience;     // token này phải "dành cho ai"
            jwtOptions.RequireHttpsMetadata = identityOptions.Authority?.StartsWith("https://") ?? true;
            ClearUnauthorizedResponseEvents.Configure(jwtOptions);
        });

    services.AddAuthorization(authorizationOptions =>
        authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build());  // lúc này chỉ = RequireAuthenticatedUser()

    return services;
}
```
[`shared/Identity/IdentityServerOptions.cs`](../../shared/Identity/IdentityServerOptions.cs) — chỉ 2 field, đọc từ mục `"Identity"` trong `appsettings.json` mọi service:
```csharp
public sealed class IdentityServerOptions
{
    public string? Authority { get; set; }  // vừa dùng để tải metadata OIDC, vừa là giá trị `iss` (issuer) mong đợi trong token
    public string? Audience { get; set; }   // giá trị `aud` mong đợi trong token
}
```
[`shared/Identity/ClearUnauthorizedResponseEvents.cs`](../../shared/Identity/ClearUnauthorizedResponseEvents.cs) — chi tiết đáng chú ý: framework mặc định trả `401` **rỗng** khi token sai/hết hạn, không phân biệt được lý do. File này bắt lại sự kiện `OnAuthenticationFailed`/`OnChallenge` của JwtBearer để trả JSON rõ ràng:
```csharp
options.Events.OnAuthenticationFailed = context =>
{
    context.HttpContext.Items[TokenExpiredItemKey] = context.Exception is SecurityTokenExpiredException;
    return Task.CompletedTask;
};
options.Events.OnChallenge = context =>
{
    context.HandleResponse();   // chặn 401 rỗng mặc định của framework
    var expired = ...;
    return context.Response.WriteAsync(JsonSerializer.Serialize(new {
        error = expired ? "token_expired" : "unauthorized",
        message = expired ? "The bearer token has expired." : "Authentication is required, or the supplied token is invalid.",
    }));
};
```
Nhờ vậy, 1 client gọi API với token hết hạn nhận được `{"error":"token_expired",...}` thay vì 1 `401` không lời giải thích — dễ debug hơn nhiều cho cả người phát triển frontend lẫn người vận hành.

### `shared/Identity` được MỞ RỘNG thêm phân quyền (spec 015)

Commit: `c3f68a8 feat: Implement toggle-gated API scope authorization`, `be79cbf Implement authorization policy tests and enforce authorization requirements across services`

014 chỉ trả lời "bạn là ai" (xác thực). 015 thêm câu hỏi thứ hai: "bạn được phép làm gì" (phân quyền) — 1 token hợp lệ bất kỳ trước đó gọi được MỌI route, kể cả khi chỉ xin quyền tối thiểu. 4 file mới, dùng cùng nhau:

[`shared/Identity/AuthorizationPolicies.cs`](../../shared/Identity/AuthorizationPolicies.cs) — "từ vựng chung":
```csharp
public static class AuthorizationPolicies
{
    public const string ApiScope = "ApiScope";                    // tên policy mọi route sẽ khai báo
    public const string RequiredApiScopeValue = "ecommerce-api";  // giá trị scope OAuth2 bắt buộc
}
```
[`shared/Identity/AuthorizationToggleOptions.cs`](../../shared/Identity/AuthorizationToggleOptions.cs) — 1 công tắc bật/tắt, đọc từ `appsettings.json` mục `FeatureToggles`, **mặc định `false`** (an toàn khi rollback — merge code này vào production không tự đổi hành vi gì cho tới khi ai đó chủ động bật).

[`shared/Identity/RequireApiScopeAuthorizationHandler.cs`](../../shared/Identity/RequireApiScopeAuthorizationHandler.cs) — logic thật, đọc công tắc ở **mỗi request** (không cache, nên sửa `appsettings.json` có hiệu lực ngay, không cần khởi động lại service):
```csharp
protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, RequireApiScopeRequirement requirement)
{
    if (!toggles.CurrentValue.AuthorizationRequireApiScope)
    {
        context.Succeed(requirement);   // công tắc TẮT → luôn cho qua, giữ nguyên hành vi của 014
        return Task.CompletedTask;
    }

    var hasRequiredScope = context.User.Claims
        .Where(claim => claim.Type == "scope")
        .SelectMany(claim => claim.Value.Split(' '))
        .Contains(AuthorizationPolicies.RequiredApiScopeValue);

    if (hasRequiredScope) context.Succeed(requirement);
    // Không gọi Fail() — để không "thắng" 1 handler khác đang chờ xử lý requirement khác trên cùng policy
    return Task.CompletedTask;
}
```

[`shared/Identity/AuthenticationFallbackPolicy.cs`](../../shared/Identity/AuthenticationFallbackPolicy.cs) — chính sách áp dụng cho MỌI route không khai báo gì cả, được nâng cấp:
```diff
  public static AuthorizationPolicy Build() =>
      new AuthorizationPolicyBuilder()
          .RequireAuthenticatedUser()
+         .AddRequirements(new RequireApiScopeRequirement())
          .Build();
```
Đây là cơ chế "fail closed" mấu chốt: 1 route tương lai lỡ quên gắn `.RequireAuthorization(...)` vẫn tự động rơi vào policy này — **không bao giờ** rơi tự do thành công khai. `IdentityValidationExtensions.AddIdentityValidation()` (đã xem ở mục 014) được sửa để đăng ký thêm named policy `"ApiScope"` mọi route dùng, và [`ClearForbiddenResponseEvents.cs`](../../shared/Identity/ClearForbiddenResponseEvents.cs) — bản sao của `ClearUnauthorizedResponseEvents` nhưng cho `403` thay vì `401`:
```csharp
context.Response.StatusCode = StatusCodes.Status403Forbidden;
await context.Response.WriteAsync(JsonSerializer.Serialize(new {
    error = "forbidden_scope",
    message = "Authentication succeeded, but the token does not carry the required scope.",
}));
```
Phân biệt quan trọng: **401** = không xác thực được (token thiếu/sai/hết hạn); **403** = xác thực **thành công** nhưng thiếu quyền. Framework tự tách 2 trường hợp trước khi gọi tới handler tương ứng — không có chỗ nào trong 015 phải tự kiểm tra lại "người này đã đăng nhập chưa".

## Thay đổi trong service nghiệp vụ

### `services/identity` — 1 service hoàn toàn mới, phát hành token thật (spec 014)

Commit: `c002df3 feat(identity-server-auth): implement identity server to replace stub authentication`

Đây là service thứ 7 của platform, dùng thư viện **Duende IdentityServer** (chuẩn OAuth2/OIDC), có database riêng như mọi service khác (`identity-db`). Câu hỏi quan trọng nhất cho 1 hệ thống multi-tenant: **claim `tenant_id` trong token lấy từ đâu?** Trả lời bởi [`services/identity/src/Identity.Api/HostedIdentity/TenantClaimsProfileService.cs`](../../services/identity/src/Identity.Api/HostedIdentity/TenantClaimsProfileService.cs):
```csharp
public sealed class TenantClaimsProfileService(UserManager<ApplicationUser> userManager) : IProfileService
{
    public const string TenantClaimType = "tenant_id";

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct = default)
    {
        var user = await userManager.GetUserAsync(context.Subject);
        if (user is null) { context.IssuedClaims = []; return; }   // không đoán — không có user thì không cấp claim nào

        context.IssuedClaims.Add(new Claim(TenantClaimType, user.TenantId));  // đọc thẳng từ record user thật trong DB
    }
}
```
Đây chính là mảnh ghép cuối cùng của câu chuyện multi-tenant xuyên suốt 4 giai đoạn: Giai đoạn 1 xây middleware **đọc** claim tenant từ token; Giai đoạn 4 này mới thật sự có 1 nơi **cấp phát** claim đó dựa trên dữ liệu user thật (`ApplicationUser.TenantId`, cột trong `identity-db`), thay vì giá trị gateway tự "diễn" như ở stub. `TenantClaimType = "tenant_id"` là 1 **literal**, không phải reference sang code của gateway — comment giải thích: tên claim là 1 "hợp đồng dây" (wire contract) giữa 2 service độc lập, không phải code dùng chung, nên cố ý lặp lại giá trị chuỗi thay vì ép 2 service cùng phụ thuộc 1 hằng số.

### Gateway chuyển đổi mượt từ stub sang thật — không cần "big bang" (spec 014)

[`services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs`](../../services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs) đăng ký **cả 2** cơ chế xác thực cùng lúc, và chọn cái nào dùng **theo từng request** bằng 1 công tắc:
```csharp
.AddPolicyScheme(SchemeName, "Toggle-gated identity", policySchemeOptions =>
    policySchemeOptions.ForwardDefaultSelector = context =>
    {
        var toggles = context.RequestServices.GetRequiredService<IOptionsMonitor<FeatureToggleOptions>>();
        return toggles.CurrentValue.IdentityServerAuthCutover
            ? JwtBearerDefaults.AuthenticationScheme      // BẬT → tin token thật từ services/identity
            : StubIdentityAuthenticationHandler.SchemeName; // TẮT → vẫn dùng stub như Giai đoạn 1
    })
```
Đọc `toggles.CurrentValue` ở mỗi request (không cache lúc khởi động) nghĩa là đổi giá trị `IdentityServerAuthCutover` trong `appsettings.json` chuyển đổi TOÀN BỘ gateway từ stub sang thật **ngay lập tức, không cần build lại hay khởi động lại container** — kỹ thuật cực kỳ hữu ích khi rollout 1 thay đổi rủi ro cao ra production: bật thử, quan sát, tắt lại ngay nếu có vấn đề, không cần deploy lại.

### Mọi route nghiệp vụ tự khai báo phân quyền (spec 015)

Commit: `be79cbf Implement authorization policy tests and enforce authorization requirements across services`

Đây chính là dòng `.RequireAuthorization(AuthorizationPolicies.ApiScope)` bạn đã thấy lặp lại ở MỌI route trong [02-orders-service-va-cac-api-endpoint.md](02-orders-service-va-cac-api-endpoint.md) — commit này là nơi nó được gắn vào `BasketEndpoints.cs`, `OrderEndpoints.cs`, `PartyEndpoints.cs`, `CatalogEndpoints.cs` và 5 file endpoint của BFF **lần đầu tiên**. Trước commit này, các route chỉ được bảo vệ **ngầm** qua `FallbackPolicy` (mục shared/ ở trên) — sau commit này, mỗi route tự khai báo tường minh, không còn dựa hoàn toàn vào cơ chế ngầm.

Đi kèm là 1 "scanner" mới — [`tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScanner.cs`](../../tests/CrossServiceIsolation.Tests/AuthorizationPolicyDeclaredScanner.cs) — không khởi động service nào, chỉ **đọc file `.cs` đã commit như văn bản** (regex), quét mọi `app.Map(Get|Post|Put|Delete|Patch)(...)` trong mọi `*Endpoints.cs` và **fail cả CI build** nếu tìm thấy 1 route thiếu `.RequireAuthorization(...)` lẫn `.AllowAnonymous()`. Nói cách khác: từ commit này, quên khai báo phân quyền cho 1 route mới không còn là lỗi runtime chờ bị phát hiện — nó là lỗi build/PR ngay lập tức, không merge được. Đội ngũ đã tự kiểm chứng scanner này thật sự "bắt lỗi" (không chỉ chạy cho có) bằng cách tạm xoá `.RequireAuthorization(...)` khỏi `CatalogEndpoints.cs`, xác nhận test đỏ, rồi khôi phục lại — kỹ thuật đáng áp dụng khi bạn review 1 PR thêm 1 scanner/rule kiểm tra mới trong tương lai: nếu chưa ai từng thấy nó đỏ, không có gì đảm bảo nó thực sự kiểm tra được điều nó tuyên bố.

## Hạ tầng CI — cổng chất lượng chặn merge (spec 013, trước đó là 012)

Commit thực thi chính: `e334c24 feat: Implement SonarQube quality gate enforcement in CI pipeline` (lúc đó spec còn mang số cũ `012-sonarqube-quality-gate`, sau đổi thành `013-sonarqube-merge-blocker` — thấy qua `docs/adr/0012-ci-quality-gate-enforcement.md` và việc `specs/013-...` sau này chỉ còn chứa tài liệu, không có commit code mới).

Đây không phải code trong `shared/` hay 1 service cụ thể — mà là **pipeline CI** áp dụng cho toàn repo, định nghĩa trong `Jenkinsfile` (5 bước tuần tự: build → unit test → integration test → contract test → phân tích SonarQube). Sau bước phân tích, **Quality Gate** của SonarQube phải xanh thì PR mới merge được — đây là ý nghĩa "merge blocker" trong tên spec.

Chi tiết dễ gây nhầm lẫn nhất: [`scripts/ci/sonar-begin.sh`](../../scripts/ci/sonar-begin.sh) — SonarScanner cho .NET nhận cấu hình qua tham số dòng lệnh (`/d:key=value`), **không tự đọc** file `.properties` như bản CLI thường. Script này đọc [`sonar-scanner.properties`](../../sonar-scanner.properties) rồi tự dịch từng dòng thành tham số `/d:`:
```sh
case "$key" in
    sonar.projectKey)     set -- "$@" "/k:${value}" ;;
    sonar.projectName)    set -- "$@" "/n:${value}" ;;
    sonar.sources|sonar.tests) ;;   # bỏ qua — scanner tự suy ra từ project MSBuild, truyền vào sẽ bị từ chối
    *) set -- "$@" "/d:${key}=${value}" ;;
esac
```
File cấu hình này **cố ý không đặt tên** `sonar-project.properties` (đây chính là lý do 2 commit fix `483b273 fix(ci): rename sonar-project.properties to avoid a scanner hard-fail` và `a534daa fix(ci): actually add sonar-scanner.properties` tồn tại ngay sau `e334c24`) — SonarScanner for .NET bản 11.x tự dừng cứng (hard-fail) nếu thấy bất kỳ file nào mang đúng cái tên đó trong cây thư mục được quét, dù bản thân nó không đọc file đó. Đây là 1 ví dụ thực tế về việc: 1 lựa chọn tên file tưởng như tuỳ ý lại có thể là né 1 hành vi khó lường của công cụ bên thứ 3 — nếu gặp lỗi tương tự, đọc kỹ comment đầu file cấu hình trước khi đổi tên lại.

`sonar.sources=services,frontend` — 1 project Sonar duy nhất cho cả backend .NET lẫn frontend TypeScript, để 1 PR nhận **1** kết quả Quality Gate, không phải 1 gate riêng mỗi service.

## Đi đâu tiếp theo

Đây là tài liệu cuối trong loạt "đi theo lịch sử specs". Quay lại [01-tong-quan-kien-truc.md](01-tong-quan-kien-truc.md) để ôn lại bức tranh tổng thể, hoặc đọc trực tiếp `specs/0NN-*/research.md` của spec nào bạn cần đào sâu thêm — mỗi quyết định nhắc tới trong 4 tài liệu này đều có 1 "Decision N" tương ứng ghi đầy đủ lý do và phương án đã cân nhắc.
