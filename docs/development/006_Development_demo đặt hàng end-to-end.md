# Bước 006: Thay đổi nghiệp vụ so với bước 005

## Phạm vi

Tài liệu này mô tả phần code nghiệp vụ được tạo hoặc thay đổi bởi bước 006 so với bước 005. Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 005: commit `5578fff`.
- Đặc tả bước 006: commit `5a193b6`.
- Cấu hình demo: commit `564313a`.
- Triển khai demo end-to-end: commit `75f8718`.
- Bổ sung tenant attribution cho Order: commit `b3873b5`.
- Hoàn thiện kiểm tra trạng thái Order trong demo: commit `4024234`.

Theo diff code, phần nghiệp vụ backend do 006 tạo ra là gắn tenant vào Order. Bước 006 không thay đổi cách tính tổng, số lượng dòng hoặc workflow checkout đã có ở bước 004. Các phần demo runner, frontend, ảnh, Docker Compose demo và kiểm thử được bỏ qua.

## 1. Order bắt đầu lưu tenant sở hữu

Trước 006, `Order` có ID, thời điểm tạo và tổng tiền. Bước 006 thêm `TenantId` để biết order thuộc tenant nào.

[Order.cs](../../services/orders/src/Orders.Api/Data/Order.cs)

```csharp
public class Order
{
    public Guid Id { get; set; }

    public DateTime PlacedAtUtc { get; set; }

    public decimal Total { get; set; }

    // 006: lưu tenant đã được resolve cho request tạo order.
    // Giá trị này không đến từ request body.
    public string? TenantId { get; set; }
}
```

`TenantId` là thông tin attribution, tức thông tin trả lời câu hỏi “order này thuộc tenant nào?”. Theo code, cơ chế ngăn request truy cập sai tenant vẫn nằm ở tenant context và pipeline của service; `TenantId` là dữ liệu được lưu trên bản ghi order.

## 2. `Order.PlaceFrom` bắt buộc tenant

Bước 004 đã có `Order.PlaceFrom` để kiểm tra dòng hàng và tính tổng. Bước 006 mở rộng hàm này để nhận tenant và lưu tenant vào order.

[Order.cs](../../services/orders/src/Orders.Api/Data/Order.cs)

```csharp
public static Order PlaceFrom(
    IReadOnlyCollection<OrderLine> lines,
    DateTime placedAtUtc,
    string tenantId)
{
    ArgumentNullException.ThrowIfNull(lines);

    // 006: tenant rỗng được xem như không có tenant.
    if (string.IsNullOrWhiteSpace(tenantId))
    {
        throw new ArgumentException(
            "An order needs the tenant it was placed for - an order belonging to nobody is not an order this service can hold.",
            nameof(tenantId));
    }

    // Logic 004 vẫn được giữ nguyên: order không được rỗng.
    if (lines.Count == 0)
    {
        throw new ArgumentException(
            "An order needs at least one line - an empty basket has nothing to order.",
            nameof(lines));
    }

    foreach (var line in lines)
    {
        // Logic 004: quantity phải từ 1 và giá không âm.
        ArgumentOutOfRangeException.ThrowIfLessThan(
            line.Quantity,
            1,
            nameof(lines));

        ArgumentOutOfRangeException.ThrowIfNegative(
            line.UnitPrice,
            nameof(lines));
    }

    return new Order
    {
        Id = Guid.NewGuid(),
        PlacedAtUtc = placedAtUtc,

        // Logic 004: Orders service vẫn tự tính tổng.
        Total = lines.Sum(
            line => line.Quantity * line.UnitPrice),

        // 006: lưu tenant đã được xác định cho order.
        TenantId = tenantId,
    };
}
```

Có ba thay đổi nghiệp vụ cần chú ý:

1. Hàm nhận thêm `tenantId`.
2. Tenant rỗng làm việc tạo order thất bại.
3. Tenant hợp lệ được lưu trực tiếp vào Order.

## 3. API lấy tenant từ context, không nhận tenant từ client

Bước 006 giữ request tạo order không có trường tenant. Endpoint lấy tenant từ `TenantContext`, tức context đã được resolve từ request pipeline.

[OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
app.MapPost("/orders", async (
    PlaceOrderRequest request,
    OrdersDbContext dbContext,
    CallerContext caller,
    TenantContext tenant,
    CancellationToken cancellationToken) =>
{
    // 004: order vẫn phải có shopper.
    caller.RequireSubjectId();

    if (request.Items is null || request.Items.Count == 0)
    {
        return Results.BadRequest(
            new { error = "An order needs at least one line." });
    }

    Order order;

    try
    {
        order = Order.PlaceFrom(
            [.. request.Items.Select(item =>
                new OrderLine(
                    item.ProductId,
                    item.Quantity,
                    item.UnitPrice))],
            DateTime.UtcNow,

            // 006: lấy tenant từ context đã resolve,
            // không lấy từ request body.
            tenant.RequireTenantId());
    }
    catch (ArgumentException exception)
    {
        return Results.BadRequest(
            new { error = exception.Message });
    }

    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync(cancellationToken);

    return Results.Created(
        $"/orders/{order.Id}",
        new OrderResponse(
            order.Id,
            order.PlacedAtUtc,
            order.Total,
            order.TenantId));
});
```

Request vẫn chỉ chứa các dòng hàng:

[OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
// 006: không thêm TenantId vào request.
// Client không được tự chọn tenant của order.
public sealed record PlaceOrderRequest(
    IReadOnlyList<PlaceOrderLine> Items);
```

Điểm khác biệt so với bước 005 là tenant không chỉ được dùng để kiểm soát request; nó còn được ghi vào bản ghi Order tại thời điểm tạo.

## 4. API đọc Order trả thêm tenant

Bước 006 mở rộng response nội bộ của Orders service để trả `TenantId`.

[OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
app.MapGet("/orders/{orderId:guid}", async (
    Guid orderId,
    OrdersDbContext dbContext,
    CancellationToken cancellationToken) =>
{
    var order = await dbContext.Orders
        .AsNoTracking()
        .Where(entity => entity.Id == orderId)
        .Select(entity => new OrderResponse(
            entity.Id,
            entity.PlacedAtUtc,
            entity.Total,

            // 006: response cho biết order thuộc tenant nào.
            entity.TenantId))
        .SingleOrDefaultAsync(cancellationToken);

    return order is null
        ? Results.NotFound()
        : Results.Ok(order);
});
```

[OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
// 006: response của Orders service có thêm TenantId.
public sealed record OrderResponse(
    Guid Id,
    DateTime PlacedAtUtc,
    decimal Total,
    string? TenantId);
```

`TenantId` được thêm vào response của Orders service để có thể xác minh attribution của order. Theo phạm vi 006, shape client-facing của BFF không được mở rộng chỉ vì trường này.

## 5. Database thêm cột `TenantId`

Bước 006 thêm cột `TenantId` vào bảng `Orders`.

[OrdersDbContext.cs](../../services/orders/src/Orders.Api/Data/OrdersDbContext.cs)

```csharp
modelBuilder.Entity<Order>(order =>
{
    order.HasKey(entity => entity.Id);

    // Logic đã có từ trước: tổng tiền dùng độ chính xác tiền tệ rõ ràng.
    order.Property(entity => entity.Total)
         .HasPrecision(18, 2);

    // 006: TenantId giới hạn tối đa 128 ký tự.
    order.Property(entity => entity.TenantId)
         .HasMaxLength(128);
});
```

Cột được đặt độ dài tối đa 128 ký tự. Trong bước 006, cột database là nullable trong giai đoạn chuyển tiếp.

[20260819140237_AddOrderTenantId.cs](../../services/orders/src/Orders.Api/Migrations/20260819140237_AddOrderTenantId.cs)

```csharp
protected override void Up(MigrationBuilder migrationBuilder)
{
    // 006: thêm TenantId vào bảng Orders.
    // Nullable để schema mới vẫn tương thích với version trước trong giai đoạn chuyển tiếp.
    migrationBuilder.AddColumn<string>(
        name: "TenantId",
        table: "Orders",
        type: "nvarchar(128)",
        maxLength: 128,
        nullable: true);
}

protected override void Down(MigrationBuilder migrationBuilder)
{
    // 006: rollback sẽ xóa cột TenantId.
    migrationBuilder.DropColumn(
        name: "TenantId",
        table: "Orders");
}
```

Ý nghĩa của trạng thái nullable:

- Schema mới có thể tồn tại cùng version cũ trong quá trình chuyển tiếp.
- Code 006 vẫn không tạo order thiếu tenant vì `PlaceFrom` từ chối tenant rỗng.
- Cột chưa được siết thành `NOT NULL` trong migration này.

## 6. Hợp đồng Orders được mở rộng

Bước 006 thêm `tenantId` vào schema `Order` của Orders service. Request `PlaceOrderRequest` không thêm tenant.

[orders-openapi.yaml](../../specs/006-e2e-order-demo/contracts/orders-openapi.yaml)

```yaml
components:
  schemas:
    Order:
      type: object
      required: [id, placedAtUtc, total, tenantId]
      properties:
        id:
          type: string
          format: uuid
        placedAtUtc:
          type: string
          format: date-time
        total:
          type: number
          format: decimal

        # 006: tenant của order được resolve tại server.
        tenantId:
          type: string
          maxLength: 128
```

[orders-openapi.yaml](../../specs/006-e2e-order-demo/contracts/orders-openapi.yaml)

```yaml
PlaceOrderRequest:
  type: object
  required: [items]
  description: Request không chứa tenant; service lấy tenant từ context.
  properties:
    items:
      type: array
      minItems: 1
      items:
        $ref: "#/components/schemas/PlaceOrderLine"
```

Bước 006 cũng cập nhật các downstream contract liên quan để schema Order có thể mô tả trường mới.

[downstream-openapi.yaml](../../specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml)

```yaml
# 006: schema Order của downstream service được mở rộng bằng tenantId.
# Đây là trường bổ sung trong response, không phải trường client gửi khi tạo order.
```

[downstream-openapi.yaml](../../specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml)

```yaml
# 006: contract downstream của bước 004 được đồng bộ với Order response mới.
# BFF-facing contract không được mở rộng chỉ để hiển thị TenantId cho storefront.
```

## 7. BFF không đổi nghiệp vụ checkout

Theo diff của 006, BFF vẫn gửi các dòng hàng sang Orders service và không gửi tenant trong request. Tenant được truyền qua request context/header đã có từ các bước trước.

[OrdersApiClient.cs](../../services/bff/src/Bff.Api/DownstreamClients/OrdersApiClient.cs)

```csharp
// 006: command của BFF vẫn không có TenantId.
// Tenant được resolve ở server, không do BFF hoặc client tự chọn.
public sealed record PlaceOrderCommand(
    IReadOnlyList<PlaceOrderLine> Items);
```

[CheckoutEndpoints.cs](../../services/bff/src/Bff.Api/Features/Checkout/CheckoutEndpoints.cs)

```csharp
// 006: checkout vẫn giữ workflow của 004.
// BFF gửi line và nhận tổng từ Orders service; không tự tính tiền.
var order = await orders.PlaceOrderAsync(
    new PlaceOrderCommand(
        [.. basket.Items.Select(line =>
            new PlaceOrderLine(
                line.ProductId,
                line.Quantity,
                line.UnitPrice))]),
    cancellationToken);
```

Từ góc nhìn nghiệp vụ, 006 không thay đổi:

- Cách tính tổng order.
- Cách gộp dòng basket.
- Cách xóa basket sau checkout.
- Dữ liệu mà client gửi để tạo order.

Thay đổi là order sau khi tạo có thêm thông tin tenant được server gắn vào.

## Tóm tắt 005 → 006

| Khu vực | Bước 005 | Bước 006 |
|---|---|---|
| Order model | ID, thời điểm, tổng tiền | Thêm `TenantId` |
| Tạo Order | Tính tổng từ các dòng | Ngoài tính tổng, bắt buộc tenant |
| Request tạo Order | Chỉ có các dòng | Vẫn chỉ có các dòng, không thêm tenant |
| Response Orders | Không có tenant | Có `TenantId` |
| Database | Chưa có cột tenant trên Orders | Thêm `nvarchar(128)` nullable |
| BFF checkout | Gửi các dòng và nhận tổng | Giữ nguyên nghiệp vụ, không tự gửi tenant |
| Mục tiêu | Local stack chạy được | Có thể xác minh order thuộc tenant nào |

**Kết luận:** bước 006 bổ sung attribution cho Order. Tenant được lấy từ context của request, được lưu vào Order, được trả trong response của Orders service và được thêm vào schema database. Các nghiệp vụ mua hàng đã có ở bước 004 không bị thay đổi.

## 8. Shared project trong bước 006

Theo các commit từ `5a193b6` đến `4024234`, bước 006 không tạo shared project mới và không mở rộng code của `ServiceDefaults` hoặc `Tenancy`. Bước 006 tái sử dụng `TenantContext` đã có từ bước 003 để gắn tenant vào Order.

[shared/Tenancy/TenantContext.cs](../../shared/Tenancy/TenantContext.cs)

```csharp
// 006: Orders lấy tenant từ shared context đã được pipeline resolve.
public string RequireTenantId() =>
    string.IsNullOrWhiteSpace(TenantId)
        ? throw new MissingTenantContextException()
        : TenantId;
```

[services/orders/src/Orders.Api/Orders.Api.csproj](../../services/orders/src/Orders.Api/Orders.Api.csproj)

```xml
<!-- 006: Orders tiếp tục tham chiếu shared Tenancy để dùng TenantContext. -->
<ProjectReference Include="..\..\..\..\shared\Tenancy\Tenancy.csproj" />
```

[services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
// 006: endpoint lấy tenant từ shared context,
// không nhận tenant do client gửi.
order = Order.PlaceFrom(
    lines,
    DateTime.UtcNow,
    tenant.RequireTenantId());
```

[shared/ServiceDefaults/ServiceDefaults.csproj](../../shared/ServiceDefaults/ServiceDefaults.csproj)

```xml
<!-- 006: ServiceDefaults vẫn được dùng như shared hạ tầng của Orders và các service. -->
<Project Sdk="Microsoft.NET.Sdk" />
```

Tăng trưởng của shared layer trong bước 006 là mức sử dụng: Orders dùng lại `TenantContext` để bảo đảm attribution. Không có commit 006 nào tạo shared project mới theo phạm vi lịch sử đã đối chiếu.
