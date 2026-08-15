extern alias BasketsApi;

using System.Net;
using System.Net.Http.Json;
using BasketsApi::Baskets.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// US1: the BFF serves basket data so the SPA never addresses the baskets service directly
/// (spec FR-002). Asserted against a real Baskets.Api reading a real database
/// (research.md Decision 5).
/// </summary>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class BasketsRouteTests(DownstreamServicesFixture fixture)
{
    [Fact]
    public async Task GetBasket_ReturnsShapedBasketFromTheBasketsService()
    {
        var basket = new Basket { Id = Guid.NewGuid(), CustomerId = Guid.NewGuid() };

        await using var baskets = await CreateBasketsServiceAsync("bff-baskets", basket);
        await using var bff = BffTestHost.CreateBff("BasketsApi", baskets);
        var client = bff.CreateClient();

        var response = await client.GetAsync($"/bff/baskets/{basket.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.NotNull(actual);
        Assert.Equal(basket.Id, actual.Id);
        Assert.Equal(basket.CustomerId, actual.CustomerId);
    }

    /// <summary>
    /// A downstream 404 must surface as a 404, not as a 502 or an empty 200. "No such basket" is
    /// the downstream answering correctly, not a downstream failure, and US3's error handling must
    /// not swallow the distinction.
    /// </summary>
    [Fact]
    public async Task GetBasket_ReturnsNotFound_WhenTheBasketsServiceHasNoSuchBasket()
    {
        await using var baskets = await CreateBasketsServiceAsync("bff-baskets-missing");
        await using var bff = BffTestHost.CreateBff("BasketsApi", baskets);
        var client = bff.CreateClient();

        var response = await client.GetAsync($"/bff/baskets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<BasketsApi::Program>>
        CreateBasketsServiceAsync(string database, params Basket[] baskets) =>
        BffTestHost.CreateDownstreamAsync<BasketsApi::Program, BasketsDbContext>(
            "BasketsDb",
            fixture.ConnectionStringFor(database),
            async dbContext =>
            {
                dbContext.Baskets.RemoveRange(dbContext.Baskets);
                dbContext.Baskets.AddRange(baskets);
                await dbContext.SaveChangesAsync();
            });

    private sealed record BasketResponse(Guid Id, Guid CustomerId);
}
