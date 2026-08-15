using System.Net;
using System.Net.Http.Json;
using Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// The basket read surface the BFF's basket route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class BasketEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task GetBasket_ReturnsTheBasket_WhenItExists()
    {
        var basket = new Basket { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() };

        await using var factory = await CreateFactoryWithBasketsAsync([basket]);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/baskets/{basket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.NotNull(actual);
        Assert.Equal(basket.Id, actual.Id);
        Assert.Equal(basket.CustomerId, actual.CustomerId);
    }

    /// <summary>
    /// An unknown id must be a clean 404, not an empty 200 — the BFF distinguishes "no such
    /// basket" from "a basket with no contents", and cannot if both look identical here.
    /// </summary>
    [Fact]
    public async Task GetBasket_ReturnsNotFound_WhenNoBasketHasThatId()
    {
        await using var factory = await CreateFactoryWithBasketsAsync([]);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/baskets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryWithBasketsAsync(
        IReadOnlyCollection<Basket> baskets)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BasketsDb"] = sqlServer.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<BasketsDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Baskets.RemoveRange(dbContext.Baskets);
        dbContext.Baskets.AddRange(baskets);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    private sealed record BasketResponse(Guid Id, Guid CustomerId);
}
