# Bước 014: Thay đổi nghiệp vụ so với bước 013

## Phạm vi

Bước 013 không đổi service/shared code (chỉ CI, xem file 013). Baseline nghiệp vụ thực tế của bước
014 vẫn là trạng thái sau bước 011 (Baskets tham chiếu `EventContracts`) cộng bước 006 (tenant
propagation). Bước 014 thay thế cơ chế xác thực giả lập (003) bằng một identity server thật, **không
đổi** cơ chế lan truyền tenant/subject đã có — chỉ đổi nguồn xác định tenant.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 013: commit `6847b79`.
- Đặc tả bước 014: commit `c002df3`.
- Scaffold service `identity` + shared library `shared/Identity`: commit `2a421b8`.
- Migration `PersistedGrantDb`, `Config.cs`, `TenantClaimsProfileService`, gateway toggle scaffold:
  commit `db86efa`.
- Independent token validation ở BFF + 4 domain service, `ClearUnauthorizedResponseEvents`: commit
  `4940818`.
- Identity configuration cho từng service, toggle wiring cuối cùng — mốc hoàn tất bước 014: commit
  `b3fd511`.

Code trích dẫn dưới đây được lấy đúng tại trạng thái commit `b3fd511` (không phải HEAD hiện tại) —
`shared/Identity/IdentityValidationExtensions.cs` và các file liên quan đã được bước 015 và 020 sửa
thêm (deny-by-default theo API scope, resilience cho JWKS fetch); phần đó ngoài phạm vi file này.
Nội dung kiểm thử (`*.IntegrationTests`, `*.UnitTests`, `AuthenticatedByDefaultScanner` trong
`shared/IntegrationTestSupport`) được bỏ qua theo đúng phạm vi đã áp dụng.

## 1. Service `identity` mới — database tách biệt hoàn toàn khỏi `parties`

[services/identity/src/Identity.Api/Data/ApplicationUser.cs](../../services/identity/src/Identity.Api/Data/ApplicationUser.cs)

```csharp
// 014: nửa "credential/identity" của user — tách biệt hoàn toàn khỏi record CRM của parties.
// Hai bên chỉ liên kết qua IdentityUser.Id (claim sub), không chia sẻ bảng/schema.
public sealed class ApplicationUser : IdentityUser
{
    // 014: một account chỉ thuộc một tenant — TenantClaimsProfileService là nơi đọc duy nhất,
    // là nguồn phát hành claim tenant_id của token.
    public required string TenantId { get; set; }
}
```

## 2. `TenantClaimsProfileService` — nguồn phát hành `tenant_id` duy nhất

[services/identity/src/Identity.Api/HostedIdentity/TenantClaimsProfileService.cs](../../services/identity/src/Identity.Api/HostedIdentity/TenantClaimsProfileService.cs)

```csharp
// 014: tên claim là literal "tenant_id", khớp với TenantClaimType phía Gateway (StubIdentity cũ)
// một cách chủ đích — đây là hợp đồng giữa các service qua wire, không phải code dùng chung.
public sealed class TenantClaimsProfileService(UserManager<ApplicationUser> userManager) : IProfileService
{
    public const string TenantClaimType = "tenant_id";

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct = default)
    {
        var user = await userManager.GetUserAsync(context.Subject);
        if (user is null)
        {
            // 014: không đoán — subject không tìm thấy user thì không phát hành claim nào.
            context.IssuedClaims = [];
            return;
        }

        context.IssuedClaims.Add(new Claim(TenantClaimType, user.TenantId));
    }

    public async Task IsActiveAsync(IsActiveContext context, CancellationToken ct = default)
    {
        var user = await userManager.GetUserAsync(context.Subject);
        context.IsActive = user is not null;
    }
}
```

## 3. `Config.cs` — chỉ một client production

[services/identity/src/Identity.Api/Config.cs](../../services/identity/src/Identity.Api/Config.cs)

```csharp
// 014: client production duy nhất — Authorization Code + PKCE, không secret (public client).
public static IEnumerable<Client> Clients =>
    [
        new Client
        {
            ClientId = "ecommerce-web-spa",
            ClientName = "Ecommerce Web Storefront",
            AllowedGrantTypes = GrantTypes.Code,
            RequirePkce = true,
            RequireClientSecret = false,
            RedirectUris = { "http://localhost:5173/callback", "http://localhost:4173/callback" },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                ApiScopeName,
            },
        },
        IntegrationTestClient, // 014: Resource Owner Password — chỉ dùng cho test, không deploy production
    ];
```

Chưa có màn hình đăng nhập tương tác nào được nối vào client `ecommerce-web-spa` ở bước 014 — client
được đăng ký trước để phần UI sau này là bổ sung, không phải redesign.

## 4. `shared/Identity` — cách một service tự xác thực độc lập

[shared/Identity/IdentityValidationExtensions.cs](../../shared/Identity/IdentityValidationExtensions.cs)

```csharp
// 014: cách duy nhất một service opt-in independent token validation — gọi trước Build()
// và UseIdentityValidation() sau khi build, giống hệt mô hình TenancyExtensions.
public static class IdentityValidationExtensions
{
    public static IServiceCollection AddIdentityValidation(this IServiceCollection services, IConfiguration configuration)
    {
        var identityOptions = configuration.GetSection(IdentityServerOptions.ConfigSectionName)
            .Get<IdentityServerOptions>() ?? new IdentityServerOptions();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = identityOptions.Authority;
                jwtOptions.Audience = identityOptions.Audience;

                // 014: https Authority giữ RequireHttpsMetadata=true (mặc định an toàn);
                // http Authority (local/compose, in-cluster, TLS terminate ở edge) thì không.
                jwtOptions.RequireHttpsMetadata =
                    identityOptions.Authority?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ?? true;

                ClearUnauthorizedResponseEvents.Configure(jwtOptions);
            });

        // 014: deny-by-default — mọi endpoint yêu cầu authenticated user trừ khi tự đánh dấu
        // [AllowAnonymous] tường minh.
        services.AddAuthorization(authorizationOptions =>
            authorizationOptions.FallbackPolicy = AuthenticationFallbackPolicy.Build());

        return services;
    }

    public static WebApplication UseIdentityValidation(this WebApplication app)
    {
        app.UseAuthentication();
        app.UseAuthorization();
        return app;
    }
}
```

[shared/Identity/AuthenticationFallbackPolicy.cs](../../shared/Identity/AuthenticationFallbackPolicy.cs)

```csharp
// 014: chỉ yêu cầu authenticated user, không role/scope — RBAC/scope chi tiết là việc
// của bước sau (015), ngoài phạm vi tính năng xác thực này.
public static class AuthenticationFallbackPolicy
{
    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
}
```

[shared/Identity/ClearUnauthorizedResponseEvents.cs](../../shared/Identity/ClearUnauthorizedResponseEvents.cs)

```csharp
// 014: phân biệt token hết hạn với các lỗi xác thực khác — framework mặc định trả 401 rỗng,
// không đủ để client biết nên làm gì tiếp theo (refresh token hay đăng nhập lại).
options.Events.OnChallenge = context =>
{
    context.HandleResponse();

    var expired = context.HttpContext.Items.TryGetValue(TokenExpiredItemKey, out var value) && value is true;

    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
    context.Response.ContentType = "application/json; charset=utf-8";

    return context.Response.WriteAsync(JsonSerializer.Serialize(new
    {
        error = expired ? "token_expired" : "unauthorized",
        message = expired
            ? "The bearer token has expired."
            : "Authentication is required, or the supplied token is invalid.",
    }, ResponseJsonOptions));
};
```

## 5. Gateway — toggle chọn scheme mỗi request, không phải lúc khởi động

[services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs](../../services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs)

```csharp
// 014: gateway là ngoại lệ duy nhất không gọi AddIdentityValidation() thẳng — nó vẫn phải giữ
// được StubIdentity của Phase 1 để rollback không cần redeploy.
.AddPolicyScheme(SchemeName, "Toggle-gated identity", policySchemeOptions =>
    policySchemeOptions.ForwardDefaultSelector = context =>
    {
        // 014: đánh giá lại MỖI request qua IOptionsMonitor, không phải một lần lúc startup.
        var toggles = context.RequestServices.GetRequiredService<IOptionsMonitor<FeatureToggleOptions>>();
        return toggles.CurrentValue.IdentityServerAuthCutover
            ? JwtBearerDefaults.AuthenticationScheme
            : StubIdentityAuthenticationHandler.SchemeName;
    })
```

[services/gateway/src/Gateway.Api/Identity/FeatureToggleOptions.cs](../../services/gateway/src/Gateway.Api/Identity/FeatureToggleOptions.cs)

```csharp
// 014: đọc từ section "FeatureToggles" (ConfigMap trong Kubernetes) — đổi giá trị không cần
// redeploy. Đây là stand-in tối giản cho Unleash thật (ADR-0008 đã chọn nhưng chưa wiring);
// đổi sang IFeatureManager thật sau này chỉ là đổi 1 dòng đọc giá trị, không redesign cơ chế toggle.
public sealed class FeatureToggleOptions
{
    public const string ConfigSectionName = "FeatureToggles";

    // 014: false là trạng thái mặc định, an toàn để rollback — dùng StubIdentityAuthenticationHandler.
    public bool IdentityServerAuthCutover { get; set; }
}
```

`TenantHeaderPropagationMiddleware`/`SubjectHeaderPropagationMiddleware` (từ bước 003/004) không bị
sửa dòng nào — cả token thật lẫn stub đều phát hành đúng claim `tenant_id`/`sub` theo ClaimTypes mặc
định của ASP.NET Core, nên middleware đọc downstream vẫn hoạt động y hệt trước.

## 6. Mỗi service tự xác thực độc lập — không tin gateway

[services/baskets/src/Baskets.Api/Program.cs](../../services/baskets/src/Baskets.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 014: service này không tin gateway đã xác thực request — tự validate token của chính nó.
// FallbackPolicy deny-by-default nên mọi endpoint cần token trừ khi [AllowAnonymous]
// (health probe).
builder.Services.AddIdentityValidation(builder.Configuration);

// 003: đăng ký sau AddIdentityValidation, trước DbContext bị gate theo tenant.
builder.Services.AddTenancy();

builder.Services.AddDbContext<BasketsDbContext>((serviceProvider, options) =>
{
    serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
    options.UseSqlServer(builder.Configuration.GetConnectionString("BasketsDb"));
});

var app = builder.Build();
app.UseServiceDefaults();

// 014: xác thực/xác quyền TRƯỚC khi resolve tenant — request chưa xác thực bị từ chối
// trước khi tốn công resolve tenant hay chạm persistence.
app.UseIdentityValidation();

app.UseTenancy();
app.MapHealthCheckEndpoints();
app.MapBasketEndpoints();
app.Run();
```

Cùng khuôn mẫu `AddIdentityValidation()`/`UseIdentityValidation()` được lặp lại y hệt ở `Bff.Api`,
`Parties.Api`, `Products.Api`, `Orders.Api` — không service nào coi service phía trước (gateway hay
BFF) là ranh giới tin cậy trung gian (constitution Principle VI).

## Tóm tắt 011 → 014

| Khu vực | Bước 011 | Bước 014 |
|---|---|---|
| Xác thực | `StubIdentityAuthenticationHandler` giả lập (003) | Service `identity` thật (Duende IdentityServer), toggle chuyển đổi không redeploy |
| Nguồn `tenant_id` | Claim giả lập từ Gateway stub | `TenantClaimsProfileService` đọc từ `ApplicationUser.TenantId` |
| Xác thực ở service | Không có | Mỗi service (BFF + 4 domain service) tự `AddIdentityValidation()`, deny-by-default |
| Phản hồi 401 | Không có | JSON phân biệt `token_expired` khỏi `unauthorized` |
| Database identity | Không có | DB riêng của `identity`, tách biệt hoàn toàn khỏi `parties` |
| Tenant/subject propagation (003/004) | Không đổi | Không đổi — middleware đọc đúng claim từ cả token thật lẫn stub |

**Kết luận:** bước 014 thay hẳn nguồn xác thực từ giả lập sang identity server thật, với nguyên tắc
xuyên suốt là gateway không phải ranh giới tin cậy — mọi service phía sau tự validate lại token của
chính nó. Cơ chế lan truyền tenant/subject (003/004) và nghiệp vụ mua hàng (004/006) không đổi một
dòng nào; chỉ nguồn phát hành `tenant_id` đổi từ Gateway stub sang identity server thật.

## 7. Shared project trong bước 014

Bước 014 tạo shared project mới `shared/Identity`, bên cạnh `ServiceDefaults` (001), `Tenancy` (003)
và `EventContracts` (008). `IdentityValidationExtensions`/`AuthenticationFallbackPolicy`/
`ClearUnauthorizedResponseEvents` được dùng thống nhất ở BFF và cả 4 domain service; Gateway dùng
riêng `ClearUnauthorizedResponseEvents`/`AuthenticationFallbackPolicy` (không gọi được
`AddIdentityValidation()` trực tiếp vì cần cơ chế 3-scheme của chính nó).

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 014: Baskets giờ tham chiếu 4 shared project: hạ tầng chung, tenant boundary,
     hợp đồng event, và xác thực độc lập. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
<ProjectReference Include="..\..\..\..\shared\EventContracts\EventContracts.csproj" />
<ProjectReference Include="..\..\..\..\shared\Identity\Identity.csproj" />
```

Cùng reference `shared/Identity` được thêm vào `Bff.Api`, `Parties.Api`, `Products.Api`,
`Orders.Api`. Gateway tham chiếu `shared/Identity` để dùng `ClearUnauthorizedResponseEvents`/
`AuthenticationFallbackPolicy` nhưng tự viết phần đăng ký scheme (`ToggleGatedAuthenticationExtensions`)
thay vì gọi `AddIdentityValidation()`. Đây là shared project thứ tư của nền tảng, và là project đầu
tiên được TẤT CẢ sáu service (Gateway, BFF, 4 domain service) cùng dùng.
