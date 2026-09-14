# Bước 002: Thay đổi nghiệp vụ so với bước 001

## Phạm vi

Bước 002 đặt Gateway và BFF trước bốn service của bước 001. Gateway dùng YARP để định tuyến; BFF dùng Minimal API để gọi downstream và định hình response. Bước này chưa thêm nghiệp vụ mua hàng; nó tạo đường đi cho client đến catalog, basket, order và party.

## 1. Gateway chuyển request đến BFF

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 002: Gateway không sở hữu database và không đọc database của domain service.
builder.Services.AddReverseProxy()
    .LoadFromConfig(
        builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();
app.UseServiceDefaults();

// 002: mọi route nghiệp vụ đi qua reverse proxy.
app.MapReverseProxy();
app.Run();
```

[services/gateway/src/Gateway.Api/appsettings.json](../../services/gateway/src/Gateway.Api/appsettings.json)

```json
{
  "ReverseProxy": {
    "Routes": {
      "bff-route": {
        "ClusterId": "bff-cluster",
        "Match": { "Path": "{**catch-all}" }
      }
    },
    "Clusters": {
      "bff-cluster": {
        "Destinations": {
          "bff": { "Address": "http://localhost:5301" }
        }
      }
    }
  }
}
```

Gateway chỉ biết BFF. Client không cần biết địa chỉ nội bộ của Products, Baskets, Orders hoặc Parties.

## 2. BFF tạo route phía client

[services/bff/src/Bff.Api/Program.cs](../../services/bff/src/Bff.Api/Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

// 002: đăng ký client gọi các domain service.
builder.Services.AddDownstreamClients(builder.Configuration);

var app = builder.Build();
app.UseServiceDefaults();

// 002: BFF map các capability client-facing.
app.MapProductsEndpoints();
app.MapBasketsEndpoints();
app.MapOrdersEndpoints();
app.MapPartiesEndpoints();
app.Run();
```

BFF là lớp tổng hợp và định hình dữ liệu. Nó không sở hữu database nghiệp vụ và không tự thực hiện rule của Products, Baskets hoặc Orders.

## 3. BFF gọi Products service qua HttpClient

[services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs](../../services/bff/src/Bff.Api/DownstreamClients/ProductsApiClient.cs)

```csharp
public sealed class ProductsApiClient(HttpClient httpClient)
{
    public const string ServiceName = "ProductsApi";

    // 002: BFF gọi API downstream, không truy cập ProductsDbContext.
    public Task<IReadOnlyList<ProductResource>> GetProductsAsync(
        CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            var products = await httpClient.GetFromJsonAsync<
                IReadOnlyList<ProductResource>>(
                    "/products",
                    cancellationToken);

            return products ?? [];
        });
}

public sealed record ProductResource(
    Guid Id,
    string Name,
    decimal Price);
```

[services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs](../../services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs)

```csharp
public static WebApplication MapProductsEndpoints(
    this WebApplication app)
{
    app.MapGet("/bff/products", async (
        ProductsApiClient products,
        CancellationToken cancellationToken) =>
    {
        // 002: route gọi downstream rồi đổi shape cho client.
        var resources = await products.GetProductsAsync(
            cancellationToken);

        return Results.Ok(resources.Select(product =>
            new ProductSummary(
                product.Id,
                product.Name,
                product.Price)));
    });

    return app;
}
```

Các route Baskets, Orders và Parties áp dụng cùng mô hình: BFF gọi typed client, nhận response downstream và trả shape client-facing.

## 4. Lỗi downstream được chuyển thành lỗi API có cấu trúc

[services/bff/src/Bff.Api/ErrorHandling/DownstreamExceptionHandler.cs](../../services/bff/src/Bff.Api/ErrorHandling/DownstreamExceptionHandler.cs)

```csharp
public async ValueTask<bool> TryHandleAsync(
    HttpContext httpContext,
    Exception exception,
    CancellationToken cancellationToken)
{
    // 002: lỗi gọi service không bị trả nguyên exception ra client.
    var problem = new ProblemDetails
    {
        Status = StatusCodes.Status502BadGateway,
        Title = "Downstream service unavailable",
        Detail = exception.Message,
    };

    httpContext.Response.StatusCode = problem.Status.Value;
    await httpContext.Response.WriteAsJsonAsync(
        problem,
        cancellationToken);

    return true;
}
```

## 5. Luồng mới sau bước 002

```text
Client
  -> Gateway (YARP)
  -> BFF (aggregation/shaping)
  -> Products / Baskets / Orders / Parties
```

**Kết luận:** 001 tạo service độc lập; 002 tạo lớp Gateway và BFF để client truy cập các service qua một điểm vào thống nhất mà không lộ topology nội bộ.

## 6. Shared project được sử dụng ở bước 002

Trong các commit của bước 002, shared project dùng chung vẫn là `ServiceDefaults`. Bước 002 chưa tạo shared project mới. Gateway và BFF cùng dùng ServiceDefaults; các domain service kế thừa cách dùng đã có từ 001.

[services/gateway/src/Gateway.Api/Gateway.Api.csproj](../../services/gateway/src/Gateway.Api/Gateway.Api.csproj)

```xml
<!-- 002: Gateway dùng ServiceDefaults cho hạ tầng dùng chung. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[services/bff/src/Bff.Api/Bff.Api.csproj](../../services/bff/src/Bff.Api/Bff.Api.csproj)

```xml
<!-- 002: BFF dùng cùng ServiceDefaults với các domain service. -->
<ProjectReference Include="..\..\..\..\shared\ServiceDefaults\ServiceDefaults.csproj" />
```

[services/gateway/src/Gateway.Api/Program.cs](../../services/gateway/src/Gateway.Api/Program.cs)

```csharp
// 002: Gateway dùng wiring observability/correlation dùng chung.
builder.AddServiceDefaults();
app.UseServiceDefaults();
```

[services/bff/src/Bff.Api/Program.cs](../../services/bff/src/Bff.Api/Program.cs)

```csharp
// 002: BFF dùng cùng shared wiring trước khi map các route aggregation.
builder.AddServiceDefaults();
app.UseServiceDefaults();
```

[shared/ServiceDefaults/ServiceDefaultsExtensions.cs](../../shared/ServiceDefaults/ServiceDefaultsExtensions.cs)

```csharp
// 002: shared project đăng ký tracing, metrics, logging và correlation middleware.
public static TBuilder AddServiceDefaults<TBuilder>(
  this TBuilder builder)
  where TBuilder : IHostApplicationBuilder
{
  builder.Services.AddOpenTelemetry();
  return builder;
}

public static WebApplication UseServiceDefaults(
  this WebApplication app)
{
  app.UseMiddleware<CorrelationIdMiddleware>();
  return app;
}
```

Vai trò của shared project trong bước 002 là giữ Gateway, BFF và domain services có cùng nền tảng observability/correlation. Logic định tuyến vẫn nằm ở Gateway; logic gọi downstream vẫn nằm ở BFF.
