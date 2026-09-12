using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Orders.Api.IntegrationTests.Support;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// Bất biến 3 của contracts/outbox-guarantees-contract.md (spec FR-003/FR-004, US2): nếu tiến trình
/// sập ngay sau khi transaction tạo đơn commit nhưng trước khi outbox delivery service kịp gửi,
/// khởi động lại vẫn tự động phát sự kiện đó ra ngoài — kịch bản 2-host của research.md Quyết định 4.
/// </summary>
[Collection(OutboxVerificationCollectionDefinition.Name)]
public class OrderPlacedOutboxCrashRecoveryTests(OutboxTestFixture fixture)
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task OutboxMessage_CommittedButUnsentWhenTheProcessStops_StillGetsPublished_AfterRestart()
    {
        await using var verificationHost = await VerificationConsumerHost.StartAsync(
            new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(fixture.SqlConnectionString)
            {
                InitialCatalog = "orders-outbox-verification-crash-recovery",
            }.ConnectionString,
            fixture.RabbitMqConnectionString);

        var deliveredCountBeforeCrash = OrderPlacedVerificationConsumer.ProcessedCounts.Count;

        var database = "orders-outbox-crash-recovery";

        // Host A ("before the crash"): its outbox delivery service is configured to poll every 2
        // minutes — far longer than this test needs to place the order and dispose the host — so
        // the commit below is guaranteed to land with its OutboxMessage still unsent when Host A is
        // torn down, deliberately, right after.
        Guid orderId;
        await using (var hostA = await CreateFactoryAsync(database, queryDelaySeconds: 120))
        {
            var client = CreateClient(hostA);

            var response = await client.PostAsJsonAsync("/orders", new
            {
                items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
            });

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
            Assert.NotNull(order);
            orderId = order!.Id;

            // Confirms the premise directly, the same way OrderPlacedOutboxAtomicityTests does: the
            // outbox row exists, committed, in the database Host A leaves behind.
            using var verifyScope = hostA.Services.CreateScope();
            var dbContext = verifyScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var outboxCount = await dbContext.Set<MassTransit.EntityFrameworkCoreIntegration.OutboxMessage>()
                .CountAsync(row => row.Body.Contains(orderId.ToString()));
            Assert.Equal(1, outboxCount);
        } // await using disposes Host A here — the simulated crash.

        // Host B ("restart"): a brand-new host, same database, short QueryDelay — standing in for
        // the service coming back up after the process that crashed above.
        await using var hostB = await CreateFactoryAsync(database, queryDelaySeconds: 1);

        // Touching .Services starts Host B (and its outbox delivery service) for the first time.
        using (hostB.Services.CreateScope())
        {
        }

        var delivered = await WaitUntilAsync(
            () => OrderPlacedVerificationConsumer.ProcessedCounts.Count > deliveredCountBeforeCrash,
            TimeSpan.FromSeconds(30));

        Assert.True(
            delivered,
            "Host B never delivered the OutboxMessage Host A committed but did not send before it stopped.");
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250));
        }

        return condition();
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database, int queryDelaySeconds)
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
                    ["Outbox:QueryDelaySeconds"] = queryDelaySeconds.ToString(System.Globalization.CultureInfo.InvariantCulture),
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
