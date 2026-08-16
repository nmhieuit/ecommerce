extern alias BasketsApi;
extern alias OrdersApi;
extern alias ProductsApi;

using System.Net;
using System.Net.Http.Json;
using BasketsApi::Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using OrdersApi::Orders.Api.Data;
using ProductsApi::Products.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec US3: the shopper turns their basket into an order and is shown a
/// confirmation naming it. The basket is emptied; an empty basket cannot be checked out.
/// </summary>
/// <remarks>
/// Three real services behind one BFF, each on its own database — the only arrangement that can
/// actually exercise research.md Decision 9's two-step, because the interesting behaviour is the
/// ordering between them.
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class CheckoutTests(DownstreamServicesFixture fixture)
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    [Fact]
    public async Task Checkout_CreatesAnOrder_ForWhatIsInTheBasket()
    {
        await using var services = await StartServicesAsync("checkout-happy");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 2);
        await AddAsync(client, Apron, quantity: 1);

        var response = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var confirmation = await response.Content.ReadFromJsonAsync<OrderConfirmationResponse>();
        Assert.NotNull(confirmation);
        Assert.NotEqual(Guid.Empty, confirmation.Id);

        // quickstart.md Scenario 5's figure, arrived at through three services.
        Assert.Equal(59.25m, confirmation.Total);
    }

    /// <summary>
    /// Spec SC-005: "the order reference shown on the confirmation screen matches the order actually
    /// created in the backend" — read back through the BFF's own order route, exactly as the
    /// quickstart walkthrough does with curl.
    /// </summary>
    [Fact]
    public async Task Checkout_ReturnsAReference_ThatReadsBackAsTheSameOrder()
    {
        await using var services = await StartServicesAsync("checkout-readback");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);

        var confirmation = await (await client.PostAsync("/bff/checkout", content: null))
            .Content.ReadFromJsonAsync<OrderConfirmationResponse>();

        var readBack = await client.GetFromJsonAsync<OrderConfirmationResponse>(
            $"/bff/orders/{confirmation!.Id}");

        Assert.Equal(confirmation.Id, readBack!.Id);
        Assert.Equal(confirmation.Total, readBack.Total);
    }

    /// <summary>Spec FR-010: after a successful checkout, the shopper's basket is empty.</summary>
    [Fact]
    public async Task Checkout_EmptiesTheBasket()
    {
        await using var services = await StartServicesAsync("checkout-empties");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);
        await client.PostAsync("/bff/checkout", content: null);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/bff/basket");

        Assert.Empty(basket!.Items);
        Assert.Equal(0m, basket.Total);
    }

    /// <summary>
    /// Spec FR-008: an empty basket has nothing to order. The storefront blocks this before it is
    /// sent, and this is the server refusing it anyway.
    /// </summary>
    [Fact]
    public async Task Checkout_ReturnsConflict_WhenTheBasketIsEmpty()
    {
        await using var services = await StartServicesAsync("checkout-empty-basket");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        var response = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Spec FR-016 and SC-008: checking out twice must not produce two orders. The second attempt
    /// finds an emptied basket and is refused — which is why FR-010's emptying is load-bearing and
    /// not merely tidy.
    /// </summary>
    [Fact]
    public async Task Checkout_CreatesExactlyOneOrder_WhenAttemptedTwice()
    {
        await using var services = await StartServicesAsync("checkout-twice");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);

        var first = await client.PostAsync("/bff/checkout", content: null);
        var second = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Two shoppers, two baskets, two orders — neither checking out the other's items. With one
    /// stub identity in Phase 1 this is what stops the caller-scoping quietly regressing.
    /// </summary>
    [Fact]
    public async Task Checkout_OrdersOnlyTheCallersOwnBasket()
    {
        await using var services = await StartServicesAsync("checkout-per-shopper");

        var mine = BffTestHost.CreateShopperClient(services.Bff);
        var theirs = BffTestHost.CreateShopperClient(services.Bff, "someone-else");

        await AddAsync(mine, Notebook, quantity: 1);
        await AddAsync(theirs, Apron, quantity: 1);

        var myOrder = await (await mine.PostAsync("/bff/checkout", content: null))
            .Content.ReadFromJsonAsync<OrderConfirmationResponse>();

        Assert.Equal(12.50m, myOrder!.Total);

        // Their basket is untouched by my checkout.
        var theirBasket = await theirs.GetFromJsonAsync<BasketResponse>("/bff/basket");
        Assert.Single(theirBasket!.Items);
    }

    private static async Task AddAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/bff/basket/items", new { productId, quantity });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Adding to the basket failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
    }

    private async Task<CheckoutServices> StartServicesAsync(string prefix)
    {
        var products = await BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor($"{prefix}-products"),
            _ => Task.CompletedTask);

        var baskets = await BffTestHost.CreateDownstreamAsync<BasketsApi::Program, BasketsDbContext>(
            "BasketsDb",
            fixture.ConnectionStringFor($"{prefix}-baskets"),
            _ => Task.CompletedTask);

        var orders = await BffTestHost.CreateDownstreamAsync<OrdersApi::Program, OrdersDbContext>(
            "OrdersDb",
            fixture.ConnectionStringFor($"{prefix}-orders"),
            _ => Task.CompletedTask);

        var bff = BffTestHost.CreateBff(new Dictionary<string, Func<HttpMessageHandler>>
        {
            ["ProductsApi"] = products.Server.CreateHandler,
            ["BasketsApi"] = baskets.Server.CreateHandler,
            ["OrdersApi"] = orders.Server.CreateHandler,
        });

        return new CheckoutServices(products, baskets, orders, bff);
    }

    private sealed record CheckoutServices(
        WebApplicationFactory<ProductsApi::Program> Products,
        WebApplicationFactory<BasketsApi::Program> Baskets,
        WebApplicationFactory<OrdersApi::Program> Orders,
        WebApplicationFactory<Program> Bff) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Bff.DisposeAsync();
            await Orders.DisposeAsync();
            await Baskets.DisposeAsync();
            await Products.DisposeAsync();
        }
    }

    private sealed record OrderConfirmationResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);

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
