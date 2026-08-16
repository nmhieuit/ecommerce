extern alias BasketsApi;
extern alias ProductsApi;

using System.Net;
using System.Net.Http.Json;
using BasketsApi::Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductsApi::Products.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-003, FR-021 and contracts/bff-openapi.yaml: the client-facing
/// basket surface. The shopper names a product and a quantity; the BFF resolves the price from the
/// products service and writes to the baskets service.
/// </summary>
/// <remarks>
/// The price resolution is the reason this suite spans two downstreams. It is also the security
/// property worth pinning: a client-supplied price is a client-supplied discount, so the route must
/// ignore anything the caller says about money (004 research.md Decision 7).
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class BasketFlowTests(DownstreamServicesFixture fixture)
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private const decimal NotebookPrice = 12.50m;

    [Fact]
    public async Task GetBasket_ReturnsAnEmptyBasket_ForAShopperWhoHasAddedNothing()
    {
        await using var products = await CreateProductsAsync("bff-basket-empty");
        await using var baskets = await CreateBasketsAsync("bff-basket-empty");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).GetAsync("/bff/basket");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.NotNull(basket);
        Assert.Empty(basket.Items);
        Assert.Equal(0m, basket.Total);
    }

    /// <summary>
    /// Spec FR-004: the basket view shows each item's name as well as its quantity and price. The
    /// baskets service stores a product identifier and nothing else, so the name can only come from
    /// the BFF aggregating the catalog in — which is precisely its job (spec 002 FR-003).
    /// </summary>
    [Fact]
    public async Task AddItem_ReturnsTheBasket_WithTheProductsNameAndResolvedPrice()
    {
        await using var products = await CreateProductsAsync("bff-basket-add");
        await using var baskets = await CreateBasketsAsync("bff-basket-add");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        var response = await client.PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity = 1 });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        var line = Assert.Single(basket!.Items);

        Assert.Equal(Notebook, line.ProductId);
        Assert.Equal("Field Notes Notebook", line.Name);
        Assert.Equal(NotebookPrice, line.UnitPrice);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(NotebookPrice, line.LineTotal);
        Assert.Equal(NotebookPrice, basket.Total);
    }

    /// <summary>
    /// The security property: whatever the caller says a product costs is discarded. Without this,
    /// the storefront's own client could set its own prices.
    /// </summary>
    [Fact]
    public async Task AddItem_IgnoresAPriceSuppliedByTheClient()
    {
        await using var products = await CreateProductsAsync("bff-basket-price-injection");
        await using var baskets = await CreateBasketsAsync("bff-basket-price-injection");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        var response = await client.PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity = 1, unitPrice = 0.01m });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.Equal(NotebookPrice, Assert.Single(basket!.Items).UnitPrice);
    }

    [Fact]
    public async Task AddItem_MergesIntoTheExistingLine_WhenTheSameProductIsAddedAgain()
    {
        await using var products = await CreateProductsAsync("bff-basket-merge");
        await using var baskets = await CreateBasketsAsync("bff-basket-merge");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        await client.PostAsJsonAsync("/bff/basket/items", new { productId = Notebook, quantity = 1 });
        await client.PostAsJsonAsync("/bff/basket/items", new { productId = Notebook, quantity = 1 });

        var basket = await client.GetFromJsonAsync<BasketResponse>("/bff/basket");

        Assert.Equal(2, Assert.Single(basket!.Items).Quantity);
        Assert.Equal(25.00m, basket.Total);
    }

    /// <summary>
    /// A product that is not in the catalog has no price to resolve, so there is nothing to add.
    /// A 404 rather than a 502: the downstream answered correctly, the request was simply wrong.
    /// </summary>
    [Fact]
    public async Task AddItem_ReturnsNotFound_WhenNoSuchProductExists()
    {
        await using var products = await CreateProductsAsync("bff-basket-unknown-product");
        await using var baskets = await CreateBasketsAsync("bff-basket-unknown-product");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Guid.NewGuid(), quantity = 1 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        await using var products = await CreateProductsAsync($"bff-basket-bad-qty-{Math.Abs(quantity)}");
        await using var baskets = await CreateBasketsAsync($"bff-basket-bad-qty-{Math.Abs(quantity)}");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<WebApplicationFactory<ProductsApi::Program>> CreateProductsAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor(database),
            // The migration seeds the catalog, so nothing more is needed — that is FR-018 working.
            _ => Task.CompletedTask);

    private Task<WebApplicationFactory<BasketsApi::Program>> CreateBasketsAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<BasketsApi::Program, BasketsDbContext>(
            "BasketsDb",
            fixture.ConnectionStringFor($"{database}-baskets"),
            _ => Task.CompletedTask);

    private static WebApplicationFactory<Program> CreateBff(
        WebApplicationFactory<ProductsApi::Program> products,
        WebApplicationFactory<BasketsApi::Program> baskets) =>
        BffTestHost.CreateBff(new Dictionary<string, Func<HttpMessageHandler>>
        {
            ["ProductsApi"] = products.Server.CreateHandler,
            ["BasketsApi"] = baskets.Server.CreateHandler,
        });

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketItemResponse> Items,
        decimal Total);

    private sealed record BasketItemResponse(
        Guid ProductId,
        string Name,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
