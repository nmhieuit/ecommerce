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
/// The basket read surface the BFF's basket route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class BasketEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// The tenant these tests seed and read as. Any non-blank value works — this suite is about the
    /// basket surface, not about which tenant resolved; that is
    /// <see cref="TenantEnforcementTests"/>' subject.
    /// </summary>
    private const string SeedTenantId = "contoso";

    [Fact]
    public async Task GetBasket_ReturnsTheBasket_WhenItExists()
    {
        var basket = Basket.ForCustomer("read-by-id-shopper");
        basket.AddItem(new Guid("9f8d6b1e-0001-4000-8000-000000000001"), quantity: 2, unitPrice: 12.50m);

        await using var factory = await CreateFactoryWithBasketsAsync([basket]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/baskets/{basket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.NotNull(actual);
        Assert.Equal(basket.Id, actual.Id);
        Assert.Equal(basket.CustomerRef, actual.CustomerRef);

        // 004 widened this shape: the read-by-id route now carries line items and a total like
        // every other basket response (004 contracts/downstream-openapi.yaml).
        var line = Assert.Single(actual.Items);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, actual.Total);
    }

    /// <summary>
    /// An unknown id must be a clean 404, not an empty 200 — the BFF distinguishes "no such
    /// basket" from "a basket with no contents", and cannot if both look identical here.
    /// </summary>
    [Fact]
    public async Task GetBasket_ReturnsNotFound_WhenNoBasketHasThatId()
    {
        await using var factory = await CreateFactoryWithBasketsAsync([]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/baskets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryWithBasketsAsync(
        IReadOnlyCollection<Basket> baskets)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BasketsDb"] = sqlServer.ConnectionString,
                }));
            builder.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so TenantContextMiddleware never populates it —
        // seeding must prime the tenant itself or the gated registration throws (research.md
        // Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<BasketsDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Baskets.RemoveRange(dbContext.Baskets);
        dbContext.Baskets.AddRange(baskets);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    /// <summary>
    /// A client whose requests carry the tenant the gateway would have resolved. Without it every
    /// request here is Unresolved and never reaches persistence at all.
    /// </summary>
    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
