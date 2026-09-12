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
/// Bất biến 1 và 2 của contracts/outbox-guarantees-contract.md (spec FR-001/FR-002, US1): ghi đơn
/// hàng và ghi outbox xảy ra trong cùng một giao dịch — cả hai cùng tồn tại khi thành công, cả hai
/// cùng không tồn tại khi thất bại.
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedOutboxAtomicityTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>Bất biến 1: transaction commit → order + outbox record cùng tồn tại.</summary>
    [Fact]
    public async Task PlaceOrder_WritesTheOrder_AndTheOutboxRecord_InTheSameTransaction()
    {
        await using var factory = await CreateFactoryAsync("orders-outbox-atomicity");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(order);

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var storedOrder = await dbContext.Orders.AsNoTracking()
            .SingleOrDefaultAsync(o => o.Id == order!.Id);
        Assert.NotNull(storedOrder);

        var outboxRows = await dbContext.Set<OutboxMessage>().AsNoTracking().ToListAsync();
        Assert.Contains(outboxRows, row => row.Body.Contains(order!.Id.ToString()));
    }

    /// <summary>
    /// Bất biến 2: nếu giao dịch tạo đơn bị rollback, không có bản ghi outbox nào được tạo cho yêu
    /// cầu đó — thao tác trực tiếp trên <see cref="OrdersDbContext"/>/<see cref="IPublishEndpoint"/>
    /// (đúng cặp phụ thuộc mà <c>OrderEndpoints.cs</c> dùng), buộc một vi phạm khoá chính CSDL thật
    /// sau khi <c>Publish</c> đã được gọi, để chứng minh outbox write đi theo transaction, không phải
    /// một side effect độc lập.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_WhenTheTransactionRollsBack_WritesNeitherTheOrderNorTheOutboxRecord()
    {
        await using var factory = await CreateFactoryAsync("orders-outbox-rollback");

        // MassTransit registers a scoped bus-context provider that implements only
        // IAsyncDisposable — resolving IPublishEndpoint from this scope means it must be disposed
        // asynchronously, not via a synchronous `using`.
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        var survivingOrder = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 12.50m)], DateTime.UtcNow, TenantId);
        dbContext.Orders.Add(survivingOrder);
        await publishEndpoint.Publish(SamplePlacedEvent(survivingOrder));
        await dbContext.SaveChangesAsync();

        var outboxCountAfterFirstOrder = await dbContext.Set<OutboxMessage>().CountAsync();

        // Otherwise EF's own change tracker rejects the doomed order below client-side (identity
        // map conflict) before the request ever reaches SQL Server — this test needs a real
        // server-side constraint violation, not a local tracking error.
        dbContext.ChangeTracker.Clear();

        // A second order forced to collide on the primary key of the first — SaveChangesAsync must
        // fail with a real SqlException surfaced as DbUpdateException, rolling back everything
        // staged in the same call, including the outbox row the Publish() below stages.
        var doomedOrder = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 12.50m)], DateTime.UtcNow, TenantId);
        doomedOrder.Id = survivingOrder.Id;
        dbContext.Orders.Add(doomedOrder);
        await publishEndpoint.Publish(SamplePlacedEvent(doomedOrder));

        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.Equal(1, await verifyDbContext.Orders.CountAsync(o => o.Id == survivingOrder.Id));
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
