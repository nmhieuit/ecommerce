# Bước 003: Thay đổi nghiệp vụ so với bước 002

## Phạm vi

Bước 003 thêm tenant context và danh tính giả lập vào luồng Gateway → BFF → domain services. Tenant được resolve ở Gateway, truyền bằng `X-Tenant-Id`, rồi được dùng để ngăn request chưa có tenant chạm vào persistence.

## 1. Tenant context dùng theo request

[shared/Tenancy/TenantContext.cs](../../shared/Tenancy/TenantContext.cs)

```csharp
public sealed class TenantContext
{
    // 003: tenant được resolve cho request hiện tại.
    public string? TenantId { get; set; }

    // 003: không có tenant mặc định.
    public string RequireTenantId() =>
        string.IsNullOrWhiteSpace(TenantId)
            ? throw new MissingTenantContextException()
            : TenantId;
}
```

[shared/Tenancy/TenantContextMiddleware.cs](../../shared/Tenancy/TenantContextMiddleware.cs)

```csharp
public async Task InvokeAsync(
    HttpContext context,
    TenantContext tenantContext)
{
    if (context.Request.Headers.TryGetValue(
            HeaderName,
            out var header) &&
        !string.IsNullOrWhiteSpace(header))
    {
        // 003: service đọc tenant header do Gateway truyền xuống.
        tenantContext.TenantId = header.ToString();
        await _next(context);
        return;
    }

    // 003: giữ trạng thái unresolved để persistence gate xử lý.
    await _next(context);
}
```

[shared/Tenancy/TenancyExtensions.cs](../../shared/Tenancy/TenancyExtensions.cs)

```csharp
public static IServiceCollection AddTenancy(
    this IServiceCollection services)
{
    // 003: mỗi request có một TenantContext riêng.
    services.AddScoped<TenantContext>();
    return services;
}

public static WebApplication UseTenancy(
    this WebApplication app)
{
    // 003: đưa tenant middleware vào pipeline service.
    app.UseMiddleware<TenantContextMiddleware>();
    return app;
}
```

## 2. Gateway dùng identity giả lập để tạo tenant header

[services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs](../../services/gateway/src/Gateway.Api/Identity/StubIdentityAuthenticationHandler.cs)

```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // 003: tạo một principal giả lập cho giai đoạn chưa có identity server thật.
    var claims = new[]
    {
        new Claim(
            "tenant_id",
            Options.TenantId)
    };

    var identity = new ClaimsIdentity(
        claims,
        Scheme.Name);
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(
        principal,
        Scheme.Name);

    return Task.FromResult(
        AuthenticateResult.Success(ticket));
}
```

[services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs](../../services/gateway/src/Gateway.Api/Identity/TenantHeaderPropagationMiddleware.cs)

```csharp
public async Task InvokeAsync(HttpContext context)
{
    // 003: Gateway là nơi resolve tenant duy nhất.
    var tenantId = context.User.FindFirst(
        "tenant_id")?.Value;

    if (string.IsNullOrWhiteSpace(tenantId))
    {
        // 003: không giữ lại tenant client tự gửi.
        context.Request.Headers.Remove(HeaderName);
        await _next(context);
        return;
    }

    // 003: ghi tenant lên request để YARP forward xuống BFF.
    context.Request.Headers[HeaderName] = tenantId;
    await _next(context);
}
```

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
// 003: bật authentication giả lập trước khi stamp tenant header.
builder.Services
    .AddAuthentication(StubIdentityAuthenticationHandler.SchemeName)
    .AddScheme<
        StubIdentityAuthenticationSchemeOptions,
        StubIdentityAuthenticationHandler>(
            StubIdentityAuthenticationHandler.SchemeName,
            options => builder.Configuration
                .GetSection("StubIdentity")
                .Bind(options));

app.UseAuthentication();
app.UseMiddleware<TenantHeaderPropagationMiddleware>();
app.MapReverseProxy();
```

## 3. BFF relay tenant xuống domain service

[services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs](../../services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs)

```csharp
protected override Task<HttpResponseMessage> SendAsync(
    HttpRequestMessage request,
    CancellationToken cancellationToken)
{
    // 003: BFF không resolve lại tenant; chỉ relay context đã nhận.
    var tenantId = _httpContextAccessor.HttpContext?
        .RequestServices
        .GetService<TenantContext>()?
        .TenantId;

    request.Headers.Remove(
        TenantContextMiddleware.HeaderName);

    if (!string.IsNullOrWhiteSpace(tenantId))
    {
        request.Headers.Add(
            TenantContextMiddleware.HeaderName,
            tenantId);
    }

    return base.SendAsync(request, cancellationToken);
}
```

## 4. Persistence bị chặn nếu tenant chưa được resolve

[services/products/src/Products.Api/Program.cs](../../services/products/src/Products.Api/Program.cs)

```csharp
// 003: đăng ký context tenant trước DbContext.
builder.Services.AddTenancy();

builder.Services.AddDbContext<ProductsDbContext>(
    (serviceProvider, options) =>
    {
        // 003: gate xảy ra trước khi tạo SQL connection.
        var tenant = serviceProvider
            .GetRequiredService<TenantContext>();
        tenant.RequireTenantId();

        options.UseSqlServer(
            builder.Configuration
                .GetConnectionString("ProductsDb"));
    });
```

Baskets, Orders, Parties và BFF áp dụng cùng cơ chế `AddTenancy()`/`UseTenancy()`. Health endpoint có thể chạy khi chưa có tenant; endpoint chạm persistence thì không có tenant mặc định để dùng.

## 5. Luồng mới sau bước 003

```text
Client
  -> Gateway xác thực identity giả lập
  -> Gateway resolve tenant và stamp X-Tenant-Id
  -> BFF relay X-Tenant-Id
  -> domain service đọc TenantContext
  -> AddDbContext yêu cầu tenant trước khi mở persistence
```

**Kết luận:** 002 tạo routing và aggregation; 003 thêm tenant boundary: tenant được xác định ở edge, truyền qua các hop và trở thành điều kiện bắt buộc trước khi service truy cập dữ liệu.

## 6. Shared project tăng trưởng ở bước 003

Bước 003 tạo shared project mới `Tenancy` bên cạnh `ServiceDefaults`. `ServiceDefaults` tiếp tục được dùng như nền tảng chung; `Tenancy` chứa context, middleware và extension dùng cho các service cần tenant.

[shared/Tenancy/Tenancy.csproj](../../shared/Tenancy/Tenancy.csproj)

```xml
<!-- 003: shared project mới chứa tenant context và middleware. -->
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

[services/products/src/Products.Api/Products.Api.csproj](../../services/products/src/Products.Api/Products.Api.csproj)

```xml
<!-- 003: Products tham chiếu cả hạ tầng chung và tenant boundary. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 003: Baskets dùng Tenancy để gate persistence theo tenant. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<!-- 003: Orders dùng cùng tenant context. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/parties/src/Parties.Api/Parties.Api.csproj](../../services/parties/src/Parties.Api/Parties.Api.csproj)

```xml
<!-- 003: Parties cũng tham gia cùng tenant boundary. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/bff/src/Bff.Api/Bff.Api.csproj](../../services/bff/src/Bff.Api/Bff.Api.csproj)

```xml
<!-- 003: BFF giữ tenant context để relay tenant qua typed HttpClient. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[shared/Tenancy/TenancyExtensions.cs](../../shared/Tenancy/TenancyExtensions.cs)

```csharp
// 003: mọi service opt-in bằng cùng một extension, không tự copy middleware.
services.AddScoped<TenantContext>();

app.UseMiddleware<TenantContextMiddleware>();
```

`ServiceDefaults` cung cấp concern hạ tầng chung; `Tenancy` bổ sung concern tenant dùng chung. Gateway là nơi tạo header, còn các service và BFF dùng shared `Tenancy` để đọc hoặc relay context.
