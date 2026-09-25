using System.Net;
using System.Net.Http.Json;
using EventContracts;
using IntegrationTestSupport;
using MassTransit;
using MassTransit.EntityFrameworkCoreIntegration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Spec 024 (xác minh outbox pattern giao dịch) — Bất biến 1 và 2 của
/// `contracts/outbox-guarantees-contract.md` (FR-001/FR-002, User Story 1): việc ghi đơn hàng và việc
/// ghi outbox xảy ra trong cùng một giao dịch — cả hai cùng tồn tại khi thành công, cả hai cùng không
/// tồn tại khi thất bại. Dùng SQL Server + RabbitMQ thật qua Testcontainers (`OutboxTestFixture`).
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedOutboxAtomicityTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: `POST /orders` hợp lệ qua HTTP thật trả `201`; đọc thẳng CSDL (không qua API) thấy
    /// đúng hàng `Order` vừa tạo và có ít nhất 1 hàng `OutboxMessage` có body chứa id đơn hàng đó.
    /// Lý do (phải test): FR-001/US1-KB1/SC-001 — nếu đơn hàng và outbox không cùng được ghi thì sự
    /// kiện `OrderPlaced` có thể mất mà không ai biết; đây là tiền đề của cả cơ chế outbox.
    /// Lưu ý: chỉ kiểm tra "cả hai cùng tồn tại sau khi thành công", KHÔNG kiểm tra được việc hai lần
    /// ghi thuộc cùng một transaction (xem QA_Debt mục 024); cũng không kiểm tra "đúng 1" bản ghi
    /// outbox (US1-KB3).
    /// Task nguồn: spec 024 (xác minh outbox pattern giao dịch) — T012, Bất biến 1.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WritesTheOrder_AndTheOutboxRecord_InTheSameTransaction()
    {
        await using var factory = await CreateFactoryAsync("orders-outbox-atomicity");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        // Assert.Equal(mong đợi, thực tế): xanh khi endpoint trả `201 Created`; đỏ khi mã khác (ví dụ
        // 500 do publish outbox hỏng, 401 hoặc 400) — khi đó các kiểm tra CSDL bên dưới không còn ý nghĩa.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        // Assert.NotNull: xanh khi body phản hồi đọc ra được đơn hàng; đỏ khi body rỗng/sai hình dạng
        // (không có id để tra CSDL).
        Assert.NotNull(order);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var storedOrder = await dbContext.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == order!.Id);
        // Assert.NotNull: xanh khi hàng `Order` thật sự nằm trong CSDL; đỏ khi API trả 201 nhưng không
        // có hàng nào được lưu (đơn "ma").
        Assert.NotNull(storedOrder);

        var outboxRows = await dbContext.Set<OutboxMessage>().AsNoTracking().ToListAsync();
        // Assert.Contains(tập, điều kiện): xanh khi có ít nhất 1 hàng outbox mà body chứa id đơn hàng
        // vừa tạo; đỏ khi bảng `OutboxMessage` rỗng hoặc không hàng nào nhắc tới đơn này (sự kiện
        // `OrderPlaced` không được xếp hàng để gửi — ví dụ gỡ `UseBusOutbox()` khỏi Program.cs làm
        // test đỏ với "Collection: []").
        Assert.Contains(outboxRows, row => row.Body.Contains(order!.Id.ToString()));
    }

    /// <summary>
    /// Kiểm tra: lưu thành công 1 đơn hợp lệ (kèm `Publish`); sau đó cố lưu đơn thứ hai trùng khoá
    /// chính với đơn đầu (kèm 1 lần `Publish` nữa) → `SaveChangesAsync` ném `DbUpdateException` thật
    /// từ SQL Server; rồi số hàng `Order` của đơn đầu vẫn là 1 và số hàng `OutboxMessage` KHÔNG tăng
    /// so với sau đơn đầu. Thao tác trực tiếp trên `OrdersDbContext` + `IPublishEndpoint` (cùng cặp
    /// phụ thuộc `OrderEndpoints.cs` dùng), không đi qua endpoint HTTP.
    /// Lý do (phải test): FR-002/US1-KB2/SC-004 — giao dịch bị rollback thì không được có sự kiện
    /// `OrderPlaced` "ma" cho đơn hàng không tồn tại.
    /// Lưu ý: nếu `UseBusOutbox()` bị gỡ thì bảng `OutboxMessage` luôn rỗng (0 == 0) và test này VẪN
    /// xanh — nó chỉ chứng minh rollback khi bus outbox đã bật, và vì không đi qua endpoint nên không
    /// bảo vệ logic ghi của `POST /orders` (xem QA_Debt mục 024).
    /// Task nguồn: spec 024 (xác minh outbox pattern giao dịch) — T013, Bất biến 2.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenTheTransactionRollsBack_WritesNeitherTheOrderNorTheOutboxRecord()
    {
        await using var factory = await CreateFactoryAsync("orders-outbox-rollback");

        // MassTransit đăng ký 1 bus-context provider theo scope chỉ hiện thực IAsyncDisposable — nên
        // scope chứa IPublishEndpoint này phải được giải phóng bất đồng bộ (`await using`), không dùng
        // `using` đồng bộ.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var survivingOrder = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 12.50m)], DateTime.UtcNow, TenantId);
        dbContext.Orders.Add(survivingOrder);
        await publishEndpoint.Publish(SamplePlacedEvent(survivingOrder));
        await dbContext.SaveChangesAsync();

        var outboxCountAfterFirstOrder = await dbContext.Set<OutboxMessage>().CountAsync();

        // Nếu không xoá change tracker, EF sẽ tự từ chối đơn "định mệnh" bên dưới ngay phía client
        // (xung đột identity map) trước khi truy vấn tới SQL Server — test này cần một vi phạm ràng
        // buộc thật từ phía server, không phải lỗi theo dõi cục bộ.
        dbContext.ChangeTracker.Clear();

        // Đơn thứ hai bị ép trùng khoá chính với đơn đầu — `SaveChangesAsync` phải thất bại với
        // SqlException thật (hiện ra dưới dạng DbUpdateException), rollback mọi thứ được xếp trong
        // cùng lệnh gọi, gồm cả hàng outbox mà `Publish()` bên dưới xếp vào.
        var doomedOrder = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 12.50m)], DateTime.UtcNow, TenantId);
        doomedOrder.Id = survivingOrder.Id;
        dbContext.Orders.Add(doomedOrder);
        await publishEndpoint.Publish(SamplePlacedEvent(doomedOrder));

        // Assert.ThrowsAsync<T>: xanh khi lưu đơn trùng khoá chính ném đúng DbUpdateException; đỏ khi
        // không ném gì (đơn trùng lọt vào CSDL — tiền đề của test không còn) hoặc ném loại khác.
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        // Assert.Equal #1: xanh khi vẫn đúng 1 hàng `Order` với id đó (đơn đầu còn nguyên, đơn trùng
        // không được lưu); đỏ khi số hàng khác 1 (mất đơn đầu hoặc đơn trùng lọt vào).
        Assert.Equal(1, await verifyDbContext.Orders.CountAsync(o => o.Id == survivingOrder.Id));
        // Assert.Equal #2: xanh khi số hàng `OutboxMessage` bằng đúng số sau đơn đầu (lần `Publish` của
        // giao dịch bị rollback không để lại hàng nào); đỏ khi tăng thêm — tức có sự kiện "ma" cho đơn
        // không tồn tại (dấu hiệu outbox không đi cùng transaction).
        Assert.Equal(
            outboxCountAfterFirstOrder,
            await verifyDbContext.Set<OutboxMessage>().CountAsync());
    }

    private static OrderPlacedV1 SamplePlacedEvent(Order order) => new(
        Guid.NewGuid(),
        order.PlacedAtUtc,
        order.Id,
        order.TenantId ?? TenantId,
        Guid.NewGuid().ToString("n"),
        order.Total,
        [new OrderLineV1(Notebook, 1, 12.50m)]);

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            fixture.SqlConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = connectionString,
                    ["ConnectionStrings:RabbitMq"] = fixture.RabbitMqConnectionString,
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total, string TenantId);
}
