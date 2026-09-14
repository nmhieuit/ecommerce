# Bước 001: Dựng khung bốn dịch vụ

## Phạm vi

Bước 001 tạo nền tảng backend ban đầu cho bốn service độc lập: Parties, Products, Baskets và Orders. Theo đặc tả của bước này, chưa có entity nghiệp vụ thật; phần được tạo là ranh giới service, DbContext riêng và các endpoint health.

## 1. Bốn service độc lập

[services/products/src/Products.Api/Products.Api.csproj](../../services/products/src/Products.Api/Products.Api.csproj)

```xml
<!-- 001: Products là một project API riêng. -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <!-- Project chỉ sở hữu phần code và database của Products. -->
</Project>
```

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 001: Baskets có project API và DbContext riêng. -->
<Project Sdk="Microsoft.NET.Sdk.Web">
</Project>
```

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<!-- 001: Orders là bounded context riêng, không dùng DbContext của service khác. -->
<Project Sdk="Microsoft.NET.Sdk.Web">
</Project>
```

[services/parties/src/Parties.Api/Parties.Api.csproj](../../services/parties/src/Parties.Api/Parties.Api.csproj)

```xml
<!-- 001: Parties là service thứ tư trong bộ khung ban đầu. -->
<Project Sdk="Microsoft.NET.Sdk.Web">
</Project>
```

Mỗi service có đường dẫn `src/{Service}.Api`, database context riêng và cấu hình connection string riêng. Bước 001 chưa cho service này truy cập database của service khác.

## 2. DbContext thuộc sở hữu từng service

[services/products/src/Products.Api/Data/ProductsDbContext.cs](../../services/products/src/Products.Api/Data/ProductsDbContext.cs)

```csharp
public class ProductsDbContext(
    DbContextOptions<ProductsDbContext> options) : DbContext(options)
{
    // 001: Products chỉ khai báo DbSet của chính nó.
    public DbSet<Product> Products => Set<Product>();
}
```

[services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs](../../services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs)

```csharp
public class BasketsDbContext(
    DbContextOptions<BasketsDbContext> options) : DbContext(options)
{
    // 001: Baskets có context riêng, không dùng ProductsDbContext.
    public DbSet<Basket> Baskets => Set<Basket>();
}
```

[services/orders/src/Orders.Api/Data/OrdersDbContext.cs](../../services/orders/src/Orders.Api/Data/OrdersDbContext.cs)

```csharp
public class OrdersDbContext(
    DbContextOptions<OrdersDbContext> options) : DbContext(options)
{
    // 001: Orders chỉ sở hữu dữ liệu Orders của mình.
    public DbSet<Order> Orders => Set<Order>();
}
```

Bước 001 thiết lập ranh giới sở hữu dữ liệu. Các entity nghiệp vụ đầy đủ được để cho các feature sau bổ sung.

## 3. Health endpoint phân biệt tiến trình và database

[services/products/src/Products.Api/Features/HealthCheck/HealthCheckEndpoints.cs](../../services/products/src/Products.Api/Features/HealthCheck/HealthCheckEndpoints.cs)

```csharp
public static WebApplication MapHealthCheckEndpoints(
    this WebApplication app)
{
    // 001: liveness chỉ trả lời tiến trình còn sống.
    app.MapHealthChecks("/health/live");

    // 001: readiness kiểm tra khả năng phục vụ, gồm database check.
    app.MapHealthChecks("/health/ready");

    return app;
}
```

[services/products/src/Products.Api/Program.cs](../../services/products/src/Products.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);

// 001: cấu hình mặc định dùng chung cho service.
builder.AddServiceDefaults();

// 001: đăng ký DbContext của riêng Products.
builder.Services.AddDbContext<ProductsDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("ProductsDb")));

var app = builder.Build();
app.UseServiceDefaults();
app.MapHealthCheckEndpoints();
app.Run();
```

Các service khác áp dụng cùng mô hình: mỗi service tự khởi động và readiness phản ánh khả năng kết nối database của chính service đó.

## 4. Ý nghĩa nghiệp vụ của bước 001

Bước 001 chưa triển khai browse, basket hoặc order. Nó chỉ tạo nơi để các nghiệp vụ đó được thêm vào mà không phá ranh giới service:

```text
Parties  -> Parties database
Products -> Products database
Baskets  -> Baskets database
Orders   -> Orders database
```

**Kết luận:** bước 001 là nền tảng ownership và health của bốn bounded context; chưa có luồng mua hàng hay entity nghiệp vụ hoàn chỉnh.

## 5. Shared project được dùng ở bước 001

Commit `c8f6627` tạo project `ServiceDefaults`; commit `693c4e3` bổ sung telemetry và correlation ID vào project này. Đây là shared project duy nhất thuộc phạm vi bước 001.

[shared/ServiceDefaults/ServiceDefaults.csproj](../../shared/ServiceDefaults/ServiceDefaults.csproj)

```xml
<!-- 001: project dùng chung cho các concern hạ tầng giống nhau giữa các service. -->
<Project Sdk="Microsoft.NET.Sdk">
    <PropertyGroup>
        <TargetFramework>net10.0</TargetFramework>
    </PropertyGroup>
</Project>
```

[services/products/src/Products.Api/Products.Api.csproj](../../services/products/src/Products.Api/Products.Api.csproj)

```xml
<!-- 001: Products tham chiếu ServiceDefaults thay vì tự cấu hình concern hạ tầng. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 001: Baskets dùng cùng shared project. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<!-- 001: Orders dùng cùng ServiceDefaults. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[services/parties/src/Parties.Api/Parties.Api.csproj](../../services/parties/src/Parties.Api/Parties.Api.csproj)

```xml
<!-- 001: Parties dùng cùng ServiceDefaults. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[shared/ServiceDefaults/ServiceDefaultsExtensions.cs](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs)

```csharp
// 001: mỗi service gọi cùng một wiring cho telemetry và concern hạ tầng chung.
builder.AddServiceDefaults();

// 001: middleware dùng chung được đưa vào pipeline của service.
app.UseServiceDefaults();
```

`ServiceDefaults` không chứa rule Products, Baskets, Orders hoặc Parties. Nó cung cấp phần dùng chung mà tất cả service cần, còn mỗi service vẫn sở hữu code và database riêng.
