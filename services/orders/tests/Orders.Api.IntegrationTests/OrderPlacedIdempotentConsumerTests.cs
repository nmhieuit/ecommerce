using EventContracts;
using IntegrationTestSupport;
using MassTransit;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Orders.Api.IntegrationTests.Support;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Bất biến 4 của contracts/outbox-guarantees-contract.md (spec FR-005, US3): một consumer nhận
/// cùng một <see cref="OrderPlacedV1"/> (cùng message id) hai lần chỉ thực sự xử lý đúng 1 lần, dùng
/// <c>InboxState</c> thật của MassTransit (research.md Quyết định 3) — không phải dedupe tự viết.
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedIdempotentConsumerTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task Consumer_ProcessesTheSameRedeliveredMessage_ExactlyOnce()
    {
        var eventId = Guid.NewGuid();
        var messageId = Guid.NewGuid();

        await using var verificationHost = await VerificationConsumerHost.StartAsync(
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.SqlConnectionString)
            {
                InitialCatalog = "orders-outbox-verification-idempotency",
            }.ConnectionString,
            fixture.RabbitMqConnectionString);

        await using var factory = await CreateFactoryAsync("orders-idempotency");

        var placedEvent = new OrderPlacedV1(
            eventId,
            DateTime.UtcNow,
            Guid.NewGuid(),
            TenantId,
            Guid.NewGuid().ToString("n"),
            12.50m,
            [new OrderLineV1(Notebook, 1, 12.50m)]);

        // Two separate publishes carrying the SAME MessageId — what InboxState actually dedupes on
        // (a real broker redelivery/retry preserves the message id; EventId is a domain-level field
        // inside the payload, not the transport envelope id InboxState keys by).
        await PublishAsync(factory, placedEvent, messageId);
        await PublishAsync(factory, placedEvent, messageId);

        var observed = await WaitForAtLeastOneDeliveryAsync(eventId, TimeSpan.FromSeconds(30));
        Assert.True(observed, "The verification consumer never observed the published event.");

        // A generous settle window: if dedup were broken, the second delivery would land within
        // this window too (same broker, same process, no network partition to wait out).
        await Task.Delay(TimeSpan.FromSeconds(3));

        Assert.Equal(1, OrderPlacedVerificationConsumer.ProcessedCounts[eventId]);
    }

    private static async Task PublishAsync(WebApplicationFactory<Program> factory, OrderPlacedV1 evt, Guid messageId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();

        await publishEndpoint.Publish(evt, context => context.MessageId = messageId);
        await dbContext.SaveChangesAsync();
    }

    private static async Task<bool> WaitForAtLeastOneDeliveryAsync(Guid eventId, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (OrderPlacedVerificationConsumer.ProcessedCounts.ContainsKey(eventId))
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return false;
    }

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
}
