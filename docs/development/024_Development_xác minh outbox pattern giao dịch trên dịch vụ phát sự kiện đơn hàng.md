# Bước 024: Thay đổi nghiệp vụ so với bước 023

## Phạm vi

Tài liệu này mô tả phần code được tạo bởi bước 024 so với trạng thái code sau bước 023.

Ranh giới commit được xác định từ lịch sử Git:

- Mốc hoàn tất bước 023 (merge vào `master`): commit `b70a08e`.
- Triển khai chính (đăng ký MassTransit, outbox/inbox trên `OrdersDbContext`, publish `OrderPlacedV1`):
  commit `0351698`.
- Hoàn thiện outbox cho `OrderPlaced` (xác nhận lại `0351698` là ancestor trực tiếp của commit này —
  đây là mốc hoàn tất bước 024): commit `3c290bf`.

Chỉ sửa `services/orders/`, `shared/EventContracts` (tham chiếu, không sửa schema), và
`shared/ServiceDefaults` (thêm `AddSource("MassTransit")`) — không đụng `services/baskets`, BFF, hay
gateway (research.md Quyết định 1: phạm vi cố ý hẹp, chỉ đóng outbox của `OrderPlaced`).

## 1. `POST /orders`: ghi đơn hàng và ghi outbox trong cùng 1 transaction

[services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs](../../services/orders/src/Orders.Api/Features/Orders/OrderEndpoints.cs)

```csharp
app.MapPost("/orders", async (
    PlaceOrderRequest request, OrdersDbContext dbContext, CallerContext caller,
    TenantContext tenant, IPublishEndpoint publishEndpoint,   // 024: tham số mới
    HttpContext httpContext, CancellationToken cancellationToken) =>
{
    // ... Order.PlaceFrom(...) như trước, không đổi ...

    dbContext.Orders.Add(order);

    // 024: Publish() stage bản ghi outbox qua Bus Outbox (Program.cs's UseBusOutbox()) — chưa gửi gì
    // cả ở dòng này. Việc gửi thật chỉ xảy ra SAU KHI SaveChangesAsync bên dưới commit, trong CÙNG
    // transaction, và chỉ qua outbox delivery service sau đó (không bao giờ inline request này) —
    // đây chính là điều giữ atomicity (contracts/outbox-guarantees-contract.md Bất biến 1/2) và giữ
    // response của endpoint này không phụ thuộc độ trễ broker (spec FR-007).
    await publishEndpoint.Publish(new OrderPlacedV1(
        Guid.NewGuid(), order.PlacedAtUtc, order.Id, order.TenantId ?? string.Empty,
        httpContext.Items[CorrelationIdMiddleware.HeaderName] as string ?? string.Empty,
        order.Total,
        [.. request.Items.Select(item => new OrderLineV1(item.ProductId, item.Quantity, item.UnitPrice))]),
        cancellationToken);

    await dbContext.SaveChangesAsync(cancellationToken);   // 024: outbox record commit CÙNG lúc với Order

    // ... response như trước, không đổi ...
})
```

TRƯỚC bước 024, sau `SaveChangesAsync()` không có gì được publish — `OrderPlacedV1` (từ spec 008) chỉ
là hợp đồng, chưa từng có ai gọi tới nó trong `services/`.

## 2. Đăng ký MassTransit + outbox/inbox (`Program.cs`, `OrdersDbContext.cs`)

```csharp
// 024: OrdersDbContext.OnModelCreating
modelBuilder.AddTransactionalOutboxEntities();   // đăng ký bảng OutboxMessage/OutboxState/InboxState
                                                   // do MassTransit.EntityFrameworkCore định nghĩa sẵn

// 024: Program.cs
builder.Services.AddMassTransit(x =>
{
    x.AddEntityFrameworkOutbox<OrdersDbContext>(o => { /* QueryDelay cấu hình được — US2 */ });
    x.UsingRabbitMq((context, cfg) => cfg.Host(/* đọc từ configuration */));
});
// UseBusOutbox() cho Bus Outbox phía publisher (research.md Quyết định 2)
```

Migration EF Core mới `AddTransactionalOutbox` — additive, không sửa bảng `Orders` hiện có
(constitution Principle X).

## 3. Consumer xác minh idempotency — chỉ tồn tại trong test, không phải nghiệp vụ thật

`OrderPlacedVerificationConsumer` (`services/orders/tests/Orders.Api.IntegrationTests/Support/`) dùng
đúng cơ chế Consumer Outbox/Inbox thật của MassTransit (`UseEntityFrameworkOutbox<OrdersDbContext>()`,
khoá theo `MessageId` qua `InboxState`) — không phải logic dedupe tự viết. Đăng ký qua
`WebApplicationFactory.WithWebHostBuilder` trong bộ test, không tồn tại ở bất kỳ service production
nào (research.md Quyết định 3 — chưa có consumer nghiệp vụ thật nào tiêu thụ `OrderPlacedV1`).

## Tóm tắt 023 → 024

| Khu vực | Bước 023 | Bước 024 |
|---|---|---|
| `POST /orders` | Chỉ `SaveChangesAsync()`, không publish gì | + `IPublishEndpoint.Publish(OrderPlacedV1)`, cùng transaction qua Bus Outbox |
| `OrdersDbContext` | Chỉ bảng `Orders` | + `OutboxMessage`/`OutboxState`/`InboxState` (MassTransit.EntityFrameworkCore) |
| RabbitMQ | Container có sẵn, không service nào kết nối | `orders` kết nối thật — publisher đầu tiên của nền tảng |
| Consumer `OrderPlacedV1` | Không tồn tại | Consumer xác minh (test-only), khoá trùng qua `InboxState` |
| `BasketCheckedOut`/saga checkout | Không đổi | Không đổi — cố ý ngoài phạm vi (ADR-0011 vẫn để ngỏ) |

**Kết luận:** bước 024 không thêm nghiệp vụ mua hàng mới cho người dùng cuối — response của
`POST /orders` không đổi hình dạng. Nó đóng đúng 1 khoảng trống outbox cho `OrderPlaced`, cố ý không
mở rộng sang `BasketCheckedOut`/saga checkout đầy đủ.

## Shared project trong bước 024

`shared/ServiceDefaults` (001) mở rộng 2 dòng (`AddSource("MassTransit")`/`AddMeter("MassTransit")`,
cùng khuôn mẫu `"Polly"` của 020). Không có shared project production mới nào được tạo — `orders` là
service DUY NHẤT thêm `PackageReference` cho `MassTransit`/`MassTransit.RabbitMQ`/
`MassTransit.EntityFrameworkCore` (`Directory.Packages.props`), không service nào khác cần đổi cách
tham chiếu shared project.
