using System.Net;
using System.Net.Http.Json;
using Baskets.Api.Data;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// specs/023-audit-n1-unbounded-pagination spec FR-002/SC-002, User Story 2: the number of SQL
/// statements issued to load a basket and its line items must not grow with the number of lines.
/// </summary>
/// <remarks>
/// <para>
/// This locks in behaviour that was already correct before this feature —
/// <c>BasketEndpoints</c>'s <c>.Include(b =&gt; b.LineItems)</c> eager-loads in a single query — as
/// an automated regression test. Spec User Story 2's Independent Test calls for "profiling it with
/// EF Core logging", not just reading the code, so this replaces that manual step with
/// <see cref="QueryCountInterceptor"/> attached to a real SQL Server via Testcontainers
/// (constitution Principle III).
/// </para>
/// <para>
/// Two baskets, not one basket asserted against a hard-coded number: comparing a 1-line basket
/// against a 5-line basket is what actually proves the count is independent of N, rather than
/// merely small for one particular N.
/// </para>
/// </remarks>
public class BasketQueryCountTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";

    [Fact]
    public async Task GetBasket_IssuesTheSameNumberOfStatements_RegardlessOfLineItemCount()
    {
        var interceptor = new QueryCountInterceptor();
        await using var factory = await CreateFactoryAsync("basket-query-count", interceptor);

        var oneLineBasketId = await SeedBasketAsync(factory, "shopper-one-line", lineItemCount: 1);
        var fiveLineBasketId = await SeedBasketAsync(factory, "shopper-five-lines", lineItemCount: 5);

        var client = CreateClient(factory);

        interceptor.Reset();
        var oneLineResponse = await client.GetAsync($"/baskets/{oneLineBasketId}");
        var statementsForOneLine = interceptor.ExecutedCommandCount;

        interceptor.Reset();
        var fiveLineResponse = await client.GetAsync($"/baskets/{fiveLineBasketId}");
        var statementsForFiveLines = interceptor.ExecutedCommandCount;

        Assert.Equal(HttpStatusCode.OK, oneLineResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, fiveLineResponse.StatusCode);

        var oneLineBasket = await oneLineResponse.Content.ReadFromJsonAsync<BasketResponse>();
        var fiveLineBasket = await fiveLineResponse.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.Single(oneLineBasket!.Items);
        Assert.Equal(5, fiveLineBasket!.Items.Count);

        // The actual assertion: statement count does not track line-item count. Equality (not just
        // "bounded by some constant") is the sharpest statement `.Include()`'s single-query eager
        // load can make here — 5 items are one row-per-item in the SAME result set, not 5 round
        // trips.
        Assert.Equal(statementsForOneLine, statementsForFiveLines);
    }

    private static async Task<Guid> SeedBasketAsync(
        WebApplicationFactory<Program> factory, string customerRef, int lineItemCount)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<BasketsDbContext>();
        var basket = Basket.ForCustomer(customerRef);

        for (var i = 0; i < lineItemCount; i++)
        {
            basket.AddItem(Guid.NewGuid(), quantity: 1, unitPrice: 9.99m);
        }

        dbContext.Baskets.Add(basket);
        await dbContext.SaveChangesAsync();

        return basket.Id;
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(
        string database, QueryCountInterceptor interceptor)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BasketsDb"] = connectionString,
                }));
            host.UseTestJwtBearer();

            // Re-registers BasketsDbContext with the same options the app's own Program.cs uses,
            // plus the counting interceptor — the last AddDbContext registration wins, so this
            // replaces rather than duplicates the production registration (no other behaviour
            // changes: same tenant gate, same connection string).
            host.ConfigureServices(services =>
            {
                services.AddDbContext<BasketsDbContext>((serviceProvider, options) =>
                {
                    serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
                    options.UseSqlServer(connectionString);
                    options.AddInterceptors(interceptor);
                });
            });
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<BasketsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, "query-count-caller");

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(
        Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
