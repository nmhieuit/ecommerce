# Bước 004: Thay đổi nghiệp vụ so với bước 003

## Phạm vi

Bước 004 bổ sung nghiệp vụ mua hàng thật trên nền tenant context của bước 003: subject của shopper, catalog seed, basket line item, tổng basket, tạo order và checkout. Frontend và kiểm thử được bỏ qua.

Các commit backend chính của bước 004:

- `f3f13ac`: caller context và subject propagation.
- `08c2c49`: seed catalog.
- `1bc77a6`: basket line item và tổng basket.
- `c99783c`: tạo order và checkout.

## 1. Bổ sung subject bên cạnh tenant

[shared/Tenancy/CallerContext.cs](../../shared/Tenancy/CallerContext.cs)

```csharp
public sealed class CallerContext
{
    // 004: xác định shopper đang thực hiện request.
    public string? SubjectId { get; set; }

    // 004: basket/order không được dùng nếu chưa xác định shopper.
    public string RequireSubjectId() =>
        string.IsNullOrWhiteSpace(SubjectId)
            ? throw new MissingCallerContextException()
            : SubjectId;
}
```

[shared/Tenancy/CallerContextMiddleware.cs](../../shared/Tenancy/CallerContextMiddleware.cs)

```csharp
if (context.Request.Headers.TryGetValue(
        HeaderName,
        out var header) &&
    !string.IsNullOrWhiteSpace(header))
{
    // 004: đọc subject header do Gateway tạo.
    callerContext.SubjectId = header.ToString();
    await _next(context);
    return;
}

// 004: không tạo subject mặc định.
await _next(context);
```

[services/gateway/src/Gateway.Api/Identity/SubjectHeaderPropagationMiddleware.cs](../../services/gateway/src/Gateway.Api/Identity/SubjectHeaderPropagationMiddleware.cs)

```csharp
var subjectId = context.User.FindFirst(
    ClaimTypes.NameIdentifier)?.Value;

if (string.IsNullOrWhiteSpace(subjectId))
{
    // 004: xóa subject do client tự gửi.
    context.Request.Headers.Remove(HeaderName);
    await _next(context);
    return;
}

// 004: Gateway là nơi duy nhất stamp subject.
context.Request.Headers[HeaderName] = subjectId;
await _next(context);
```

[services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs](../../services/bff/src/Bff.Api/DownstreamClients/TenantPropagationHandler.cs)

```csharp
// 003: handler đã relay tenant.
// 004: relay thêm subject qua BFF -> domain service.
Relay(
    request,
    CallerContextMiddleware.HeaderName,
    requestServices?
        .GetService<CallerContext>()?
        .SubjectId);
```

## 2. Products có catalog seed

[services/products/src/Products.Api/Data/CatalogSeed.cs](../../services/products/src/Products.Api/Data/CatalogSeed.cs)

```csharp
public static IReadOnlyList<Product> Products { get; } =
[
    // 004: dữ liệu cố định để catalog có thể dùng ngay.
    new Product
    {
        Id = new Guid("9f8d6b1e-0001-4000-8000-000000000001"),
        Name = "Field Notes Notebook",
        Price = 12.50m,
    },
    new Product
    {
        Id = new Guid("9f8d6b1e-0001-4000-8000-000000000002"),
        Name = "Ceramic Pour-Over Set",
        Price = 48.00m,
    },
    new Product
    {
        Id = new Guid("9f8d6b1e-0001-4000-8000-000000000003"),
        Name = "Linen Apron",
        Price = 34.25m,
    },
];
```

[services/products/src/Products.Api/Data/ProductsDbContext.cs](../../services/products/src/Products.Api/Data/ProductsDbContext.cs)

```csharp
// 004: catalog seed được đưa vào EF migration.
product.HasData(CatalogSeed.Products);
```

[services/products/src/Products.Api/Migrations/20260816085315_SeedCatalog.cs](../../services/products/src/Products.Api/Migrations/20260816085315_SeedCatalog.cs)

```csharp
// 004: Up insert các sản phẩm cố định vào Products.
migrationBuilder.InsertData(
    table: "Products",
    columns: new[] { "Id", "Name", "Price" },
    values: new object[,]
    {
        { notebookId, "Field Notes Notebook", 12.50m },
        { pourOverId, "Ceramic Pour-Over Set", 48.00m },
        { apronId, "Linen Apron", 34.25m }
    });
```

## 3. Baskets có line item và tổng tiền

[services/baskets/src/Baskets.Api/Data/BasketLineItem.cs](../../services/baskets/src/Baskets.Api/Data/BasketLineItem.cs)

```csharp
public class BasketLineItem
{
    // 004: chỉ lưu ID sản phẩm, không truy cập Products database.
    public Guid ProductId { get; set; }

    public int Quantity { get; set; }

    // 004: giá được capture khi thêm sản phẩm.
    public decimal UnitPrice { get; set; }

    // 004: thành tiền của dòng.
    public decimal LineTotal => Quantity * UnitPrice;
}
```

[services/baskets/src/Baskets.Api/Data/Basket.cs](../../services/baskets/src/Baskets.Api/Data/Basket.cs)

```csharp
// 004: subject dùng để tìm basket của shopper.
public string CustomerRef { get; set; } = string.Empty;

// 004: basket chứa nhiều dòng.
public ICollection<BasketLineItem> LineItems { get; } = [];

// 004: tổng được tính từ các dòng, không lưu riêng.
public decimal Total =>
    LineItems.Sum(line => line.LineTotal);
```

[services/baskets/src/Baskets.Api/Data/Basket.cs](../../services/baskets/src/Baskets.Api/Data/Basket.cs)

```csharp
public void AddItem(
    Guid productId,
    int quantity,
    decimal unitPrice)
{
    // 004: rule nghiệp vụ quantity và price.
    ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
    ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

    var existing = LineItems.SingleOrDefault(
        line => line.ProductId == productId);

    if (existing is not null)
    {
        // 004: cùng sản phẩm được gộp quantity.
        existing.Quantity += quantity;
        return;
    }

    LineItems.Add(new BasketLineItem
    {
        BasketId = Id,
        ProductId = productId,
        Quantity = quantity,
        UnitPrice = unitPrice,
    });
}
```

[services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs](../../services/baskets/src/Baskets.Api/Data/BasketsDbContext.cs)

```csharp
// 004: một shopper chỉ có một basket.
basket.HasIndex(entity => entity.CustomerRef)
      .IsUnique();

// 004: không map giá trị tính toán thành cột.
basket.Ignore(entity => entity.Total);
line.Ignore(entity => entity.LineTotal);

// 004: một sản phẩm chỉ chiếm một dòng trong basket.
line.HasIndex(entity => new
{
    entity.BasketId,
    entity.ProductId
}).IsUnique();
```

## 4. Baskets API dùng basket hiện tại của subject

[services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs](../../services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs)

```csharp
app.MapGet("/baskets/current", async (...) =>
{
    // 004: resolve basket từ subject, không nhận customer ID từ client.
    var basket = await FindOrCreateCurrentAsync(...);
    return Results.Ok(ToResponse(basket));
});
```

[services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs](../../services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs)

```csharp
app.MapPost("/baskets/current/items", async (...) =>
{
    if (request.Quantity < 1)
        return Results.BadRequest(...);

    // 004: Baskets nhận giá đã được BFF resolve từ Products.
    var basket = await FindOrCreateCurrentAsync(...);
    basket.AddItem(
        request.ProductId,
        request.Quantity,
        request.UnitPrice);

    await dbContext.SaveChangesAsync(cancellationToken);
    return Results.Ok(ToResponse(basket));
});
```

[services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs](../../services/baskets/src/Baskets.Api/Features/Baskets/BasketEndpoints.cs)

```csharp
private static async Task<Basket>
    FindOrCreateCurrentAsync(...)
{
    // 004: không có subject thì không được chạm basket.
    var customerRef = caller.RequireSubjectId();

    var basket = await dbContext.Baskets
        .Include(entity => entity.LineItems)
        .SingleOrDefaultAsync(
            entity => entity.CustomerRef == customerRef,
            cancellationToken);

    if (basket is not null)
        return basket;

    // 004: shopper mới được tạo basket rỗng.
    basket = Basket.ForCustomer(customerRef);
    dbContext.Baskets.Add(basket);
    await dbContext.SaveChangesAsync(cancellationToken);
    return basket;
}
```

## 5. BFF resolve giá, không nhận giá từ client

[services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs](../../services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs)

```csharp
// 004: client chỉ được gửi product và quantity.
public sealed record AddBasketItemRequest(
    Guid ProductId,
    int Quantity);
```

[services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs](../../services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs)

```csharp
// 004: BFF tìm giá trong Products service.
var catalog = await products.GetProductsByIdsAsync(
    [request.ProductId],
    cancellationToken);

var product = catalog.SingleOrDefault(
    entry => entry.Id == request.ProductId);

if (product is null)
    return Results.NotFound();

// 004: gửi giá catalog sang Baskets.
var basket = await baskets.AddItemAsync(
    new AddBasketItemCommand(
        product.Id,
        request.Quantity,
        product.Price),
    cancellationToken);
```

[services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs](../../services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs)

```csharp
// 004: BFF chỉ shape và pass-through total từ Baskets.
return new BasketResponse(
    basket.Id,
    basket.CustomerRef,
    items,
    basket.Total);
```

## 6. Orders tính tổng và tạo order

[services/orders/src/Orders.Api/Data/Order.cs](../../services/orders/src/Orders.Api/Data/Order.cs)

```csharp
public static Order PlaceFrom(
    IReadOnlyCollection<OrderLine> lines,
    DateTime placedAtUtc)
{
    // 004: không tạo order rỗng.
    if (lines.Count == 0)
        throw new ArgumentException(...);

    foreach (var line in lines)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(
            line.Quantity, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(
            line.UnitPrice);
    }

    return new Order
    {
        Id = Guid.NewGuid(),
        PlacedAtUtc = placedAtUtc,

        // 004: tổng do Orders service tự tính.
        Total = lines.Sum(
            line => line.Quantity * line.UnitPrice),
    };
}
```

[services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
app.MapPost("/orders", async (...) =>
{
    // 004: order phải thuộc một shopper.
    caller.RequireSubjectId();

    if (request.Items is null || request.Items.Count == 0)
        return Results.BadRequest(...);

    var order = Order.PlaceFrom(
        [.. request.Items.Select(item =>
            new OrderLine(
                item.ProductId,
                item.Quantity,
                item.UnitPrice))],
        DateTime.UtcNow);

    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created(
        $"/orders/{order.Id}",
        new OrderResponse(
            order.Id,
            order.PlacedAtUtc,
            order.Total));
});
```

## 7. BFF điều phối checkout hai bước

[services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs](../../services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs)

```csharp
group.MapPost("/checkout", async (...) =>
{
    // 004: đọc basket hiện tại.
    var basket = await baskets.GetCurrentBasketAsync(...);

    // 004: không checkout basket rỗng.
    if (basket.Items.Count == 0)
        return Results.Conflict(...);

    // 004: tạo order từ các dòng basket.
    var order = await orders.PlaceOrderAsync(
        new PlaceOrderCommand(
            [.. basket.Items.Select(line =>
                new PlaceOrderLine(
                    line.ProductId,
                    line.Quantity,
                    line.UnitPrice))]),
        cancellationToken);

    // 004: tạo order trước rồi clear basket.
    await baskets.ClearCurrentBasketAsync(
        cancellationToken);

    return Results.Created(
        $"/bff/orders/{order.Id}",
        new OrderConfirmationResponse(
            order.Id,
            order.PlacedAtUtc,
            order.Total));
});
```

Checkout là workflow đồng bộ do BFF điều phối. BFF không tự nhân hoặc cộng tiền; Baskets và Orders là nơi sở hữu phép tính tương ứng.

## Tóm tắt 003 → 004

```text
003:
Gateway resolve tenant và truyền X-Tenant-Id.

004:
Gateway truyền thêm X-Subject-Id.
Products có catalog seed.
Baskets có line item, quantity và giá đã capture.
Orders tạo order và tính tổng.
BFF điều phối checkout.
```

**Kết luận:** bước 004 là bước đầu tiên biến các service shell thành luồng mua hàng có dữ liệu thật, trong khi vẫn giữ tenant boundary và nguyên tắc BFF không chứa phép tính nghiệp vụ.

## 8. Shared project tăng trưởng ở bước 004

Bước 004 không tạo shared project mới. Nó mở rộng shared project `Tenancy` của bước 003 bằng `CallerContext`, để tenant và shopper được xử lý song song.

[shared/Tenancy/CallerContext.cs](../../shared/Tenancy/CallerContext.cs)

```csharp
// 004: shared Tenancy có thêm subject bên cạnh TenantId.
public sealed class CallerContext
{
    public string? SubjectId { get; set; }

    public string RequireSubjectId() =>
        string.IsNullOrWhiteSpace(SubjectId)
            ? throw new MissingCallerContextException()
            : SubjectId;
}
```

[shared/Tenancy/CallerContextMiddleware.cs](../../shared/Tenancy/CallerContextMiddleware.cs)

```csharp
// 004: mọi service dùng chung middleware để đọc X-Subject-Id.
if (context.Request.Headers.TryGetValue(
        HeaderName,
        out var header) &&
    !string.IsNullOrWhiteSpace(header))
{
    callerContext.SubjectId = header.ToString();
    await _next(context);
    return;
}

await _next(context);
```

[shared/Tenancy/TenancyExtensions.cs](../../shared/Tenancy/TenancyExtensions.cs)

```csharp
// 004: AddTenancy đăng ký cả tenant context và caller context.
services.AddScoped<TenantContext>();
services.AddScoped<CallerContext>();

// 004: UseTenancy đọc cả hai loại context trong cùng pipeline.
app.UseMiddleware<TenantContextMiddleware>();
app.UseMiddleware<CallerContextMiddleware>();
```

[services/baskets/src/Baskets.Api/Baskets.Api.csproj](../../services/baskets/src/Baskets.Api/Baskets.Api.csproj)

```xml
<!-- 004: Baskets dùng shared Tenancy cho cả tenant và subject. -->
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<!-- 004: Orders dùng CallerContext để yêu cầu shopper khi tạo order. -->
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/bff/src/Bff.Api/Bff.Api.csproj](../../services/bff/src/Bff.Api/Bff.Api.csproj)

```xml
<!-- 004: BFF đọc context từ shared Tenancy rồi relay xuống domain service. -->
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

Tăng trưởng của shared layer trong bước 004 là mở rộng phạm vi từ “request thuộc tenant nào” thành “request thuộc tenant nào và do shopper nào thực hiện”. `ServiceDefaults` vẫn được các service sử dụng như ở các bước trước; không có shared project mới trong commit backend 004.
