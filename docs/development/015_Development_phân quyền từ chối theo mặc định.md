# Bước 015: Thay đổi nghiệp vụ so với bước 014

## Phạm vi

Bước 015 đóng nốt nửa "phân quyền" của constitution Principle VI mà 014 chủ đích để ngỏ (014 chỉ làm
nửa "xác thực"). Cơ chế xác thực độc lập, database `identity`, và lan truyền tenant/subject của 014
giữ nguyên không đổi.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 014: commit `b3fd511`.
- Đặc tả bước 015: commit `37ba26c`.
- `shared/Identity` có thêm chính sách `ApiScope` toggle-gated (`AuthorizationPolicies`,
  `AuthorizationToggleOptions`, `RequireApiScopeRequirement`, `RequireApiScopeAuthorizationHandler`,
  `ClearForbiddenResponseEvents`), Gateway lặp lại cùng nội dung: commit `c3f68a8`.
- Khai báo `.RequireAuthorization(ApiScope)` tường minh trên từng route của BFF + 4 domain service,
  thêm scanner tĩnh xác nhận không route nào thiếu khai báo — mốc hoàn tất bước 015: commit `be79cbf`.

`AuthorizationPolicyDeclaredScanner` (`tests/CrossServiceIsolation.Tests`, cơ chế thực thi FR-001)
là nội dung kiểm thử, bị loại theo đúng phạm vi đã áp dụng — chỉ được nhắc tên, không trích code.

## 1. `shared/Identity` — chính sách `ApiScope` toggle-gated

[shared/Identity/AuthorizationPolicies.cs](../../shared/Identity/AuthorizationPolicies.cs)

```csharp
// 015: tên chính sách mọi route gọi qua .RequireAuthorization(...), và giá trị scope literal —
// khớp Identity.Api.Config.ApiScopeName theo hợp đồng, không tham chiếu project (như tenant_id
// của 014).
public static class AuthorizationPolicies
{
    public const string ApiScope = "ApiScope";
    public const string RequiredApiScopeValue = "ecommerce-api";
}
```

[shared/Identity/AuthorizationToggleOptions.cs](../../shared/Identity/AuthorizationToggleOptions.cs)

```csharp
// 015: cùng section "FeatureToggles" mà IdentityServerAuthCutover (014) đã dùng.
public sealed class AuthorizationToggleOptions
{
    public const string ConfigSectionName = "FeatureToggles";

    // 015: false là mặc định an toàn để rollback — policy lùi về đúng hành vi trước 015
    // (đã xác thực là đủ, không kiểm tra scope).
    public bool AuthorizationRequireApiScope { get; set; }
}
```

[shared/Identity/RequireApiScopeRequirement.cs](../../shared/Identity/RequireApiScopeRequirement.cs)

```csharp
// 015: marker requirement thuần, không mang dữ liệu — toggle và claim check đều nằm trong handler.
public sealed class RequireApiScopeRequirement : IAuthorizationRequirement;
```

[shared/Identity/RequireApiScopeAuthorizationHandler.cs](../../shared/Identity/RequireApiScopeAuthorizationHandler.cs)

```csharp
public sealed class RequireApiScopeAuthorizationHandler(IOptionsMonitor<AuthorizationToggleOptions> toggles)
    : AuthorizationHandler<RequireApiScopeRequirement>
{
    private const string ScopeClaimType = "scope";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequireApiScopeRequirement requirement)
    {
        // 015: toggle tắt = giữ đúng hành vi trước 015 (đã xác thực là đủ).
        if (!toggles.CurrentValue.AuthorizationRequireApiScope)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // 015: scope có thể đến như một claim "scope" gộp khoảng trắng hoặc nhiều claim rời —
        // kiểm tra cả hai dạng, không giả định một hình dạng cố định.
        var hasRequiredScope = context.User.Claims
            .Where(claim => claim.Type == ScopeClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(AuthorizationPolicies.RequiredApiScopeValue, StringComparer.Ordinal);

        if (hasRequiredScope)
        {
            context.Succeed(requirement);
        }

        // 015: không gọi Fail() — một Forbid chỉ nên đến từ chính requirement này thật sự không đạt,
        // không "đua" với handler khác đang chờ xử lý requirement khác trên cùng policy.
        return Task.CompletedTask;
    }
}
```

## 2. `AuthenticationFallbackPolicy` nâng cấp — lưới an toàn không được yếu hơn route có khai báo

[shared/Identity/AuthenticationFallbackPolicy.cs](../../shared/Identity/AuthenticationFallbackPolicy.cs)

```csharp
public static class AuthenticationFallbackPolicy
{
    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()

            // 015: thêm so với 014 — fallback giờ nghiêm ngặt ít nhất bằng policy ApiScope mà
            // mọi route khai báo tường minh, để một endpoint quên khai báo không bao giờ được
            // bảo vệ KÉM HƠN một endpoint có khai báo.
            .AddRequirements(new RequireApiScopeRequirement())
            .Build();
}
```

## 3. `ClearForbiddenResponseEvents` — 403 có thân JSON, đối xứng với 401 của 014

[shared/Identity/ClearForbiddenResponseEvents.cs](../../shared/Identity/ClearForbiddenResponseEvents.cs)

```csharp
// 015: một PolicyAuthorizationResult.Forbidden chỉ có nghĩa "đã xác thực thành công, nhưng một
// requirement (ở đây là RequireApiScopeRequirement) không đạt" — PolicyEvaluator của framework
// tự trả Challenge (401) khi bản thân việc xác thực thất bại, nên handler này không cần tự kiểm
// tra IsAuthenticated.
public sealed class ClearForbiddenResponseEvents : IAuthorizationMiddlewareResultHandler
{
    public async Task HandleAsync(
        RequestDelegate next, HttpContext context, AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (!authorizeResult.Forbidden)
        {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(new
        {
            error = "forbidden_scope",
            message = "Authentication succeeded, but the token does not carry the required scope.",
        }, ResponseJsonOptions));
    }
}
```

## 4. Khai báo tường minh trên từng route

[services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs](../../services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs)

```csharp
app.MapGet("/baskets/current", async (...) =>
{
    var basket = await FindOrCreateCurrentAsync(dbContext, caller, cancellationToken);
    return Results.Ok(ToResponse(basket));
})
    // 015: mọi route nghiệp vụ tự khai báo tường minh — không dựa vào FallbackPolicy ngầm định.
    .RequireAuthorization(AuthorizationPolicies.ApiScope);

app.MapPost("/baskets/current/items", async (...) => { /* ... */ })
    .RequireAuthorization(AuthorizationPolicies.ApiScope);

app.MapPost("/baskets/current/clear", async (...) => { /* ... */ })
    .RequireAuthorization(AuthorizationPolicies.ApiScope);
```

Cùng một dòng `.RequireAuthorization(AuthorizationPolicies.ApiScope)` được thêm vào mọi route trong 9
file `*Endpoints.cs` (4 domain service + 5 file `Features/*/Endpoints.cs` của BFF). Hai health probe
mỗi service giữ nguyên `.AllowAnonymous()` đã có từ 014 — không đổi.

## 5. Gateway lặp lại cùng nội dung, không có route riêng để khai báo

[services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs](../../services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs)

```csharp
// 015: gateway không gọi được AddIdentityValidation() (lý do từ 014 — cần đăng ký 3-scheme
// riêng), nên lặp lại đúng nội dung ApiScope tại đây.
services.Configure<AuthorizationToggleOptions>(
    configuration.GetSection(AuthorizationToggleOptions.ConfigSectionName));
services.AddSingleton<IAuthorizationHandler, RequireApiScopeAuthorizationHandler>();
services.AddSingleton<IAuthorizationMiddlewareResultHandler, ClearForbiddenResponseEvents>();

services.AddAuthorization(authorizationOptions =>
{
    authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build();
    authorizationOptions.AddPolicy(AuthorizationPolicies.ApiScope, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new RequireApiScopeRequirement()));
});
```

Gateway không có route nghiệp vụ nào để tự khai báo `.RequireAuthorization(...)` — toàn bộ lưu lượng
ngoài health probe đi qua đúng một `MapReverseProxy()` catch-all, nên chính sách bảo vệ nó là
`FallbackPolicy` (đã nâng cấp), áp dụng đồng nhất thay vì khai báo per-route.

## Tóm tắt 014 → 015

| Khu vực | Bước 014 | Bước 015 |
|---|---|---|
| Xác thực | Mỗi service tự validate token (JwtBearer) | Không đổi |
| Phân quyền per-route | Không có khai báo tường minh | Mọi route `.RequireAuthorization(AuthorizationPolicies.ApiScope)` |
| `FallbackPolicy` | Chỉ `RequireAuthenticatedUser()` | Thêm `RequireApiScopeRequirement` — nghiêm ngặt ≥ policy per-route |
| Toggle | `IdentityServerAuthCutover` (xác thực) | Thêm `AuthorizationRequireApiScope` (phân quyền), cùng section config |
| 403 | Không có xử lý riêng | JSON `{"error":"forbidden_scope",...}` thay vì 403 rỗng |
| Thực thi "không route nào quên khai báo" | Không có | Scanner tĩnh (test) chặn PR nếu thiếu |

**Kết luận:** bước 015 không thêm nghiệp vụ mua hàng mới. Nó đóng nốt nguyên tắc "mọi endpoint phải
khai báo tường minh một quyết định phân quyền" (Principle VI) bằng một policy `ApiScope` toggle-gated
dùng chung, khai báo tường minh trên từng route, và một `FallbackPolicy` được nâng cấp để không bao
giờ bảo vệ yếu hơn route có khai báo. Toggle tắt (mặc định production) giữ nguyên hành vi trước 015;
toggle bật mới thực sự đòi hỏi claim `scope=ecommerce-api`.

## 6. Shared project trong bước 015

Bước 015 không tạo shared project mới. Nó mở rộng `shared/Identity` (014) với 5 file mới
(`AuthorizationPolicies`, `AuthorizationToggleOptions`, `RequireApiScopeRequirement`,
`RequireApiScopeAuthorizationHandler`, `ClearForbiddenResponseEvents`) và sửa 1 file đã có
(`AuthenticationFallbackPolicy`). Không service nào cần thêm `ProjectReference` mới — cả 6 service đã
tham chiếu `shared/Identity` từ bước 014.

[shared/Identity/IdentityValidationExtensions.cs](../../shared/Identity/IdentityValidationExtensions.cs)

```csharp
// 015: AddIdentityValidation() giờ đăng ký thêm RequireApiScopeAuthorizationHandler,
// ClearForbiddenResponseEvents, và policy có tên "ApiScope" — mọi service gọi hàm này
// (BFF + 4 domain service) tự động có cả phần phân quyền mới, không cần tự sửa Program.cs.
services.Configure<AuthorizationToggleOptions>(
    configuration.GetSection(AuthorizationToggleOptions.ConfigSectionName));
services.AddSingleton<IAuthorizationHandler, RequireApiScopeAuthorizationHandler>();
services.AddSingleton<IAuthorizationMiddlewareResultHandler, ClearForbiddenResponseEvents>();

services.AddAuthorization(authorizationOptions =>
{
    authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build();
    authorizationOptions.AddPolicy(AuthorizationPolicies.ApiScope, policy => policy
        .RequireAuthenticatedUser()
        .AddRequirements(new RequireApiScopeRequirement()));
});
```

Đây là lần đầu một bước mở rộng `shared/Identity` mà không service nào cần đổi `.csproj` hay
`Dockerfile` — toàn bộ tác dụng đến qua đúng một hàm dùng chung đã tồn tại từ 014.
