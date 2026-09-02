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
/// 004-minimal-shopping-spa spec FR-006 and FR-021: the basket is resolved from the caller's
/// identity, one per shopper, and adding a product merges into the line it already occupies.
/// </summary>
/// <remarks>
/// Against real SQL Server via Testcontainers (Principle III). The unique indexes are the point of
/// several of these assertions, and an in-memory provider does not enforce them — the suite would
/// pass while production had duplicate baskets.
/// </remarks>
public class CurrentBasketTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";
    private const string OtherShopper = "someone-else";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// A first visit is not an error. Returning 404 here would make the storefront treat "you have
    /// never shopped before" as a failure to recover from.
    /// </summary>
    [Fact]
    public async Task GetCurrent_ReturnsAnEmptyBasket_ForACallerWhoHasNeverAddedAnything()
    {
        await using var factory = await CreateFactoryAsync("basket-current-first-visit");
        var client = CreateClient(factory, Shopper);

        var response = await client.GetAsync("/baskets/current");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        Assert.NotNull(basket);
        Assert.Empty(basket.Items);
        Assert.Equal(0m, basket.Total);
        Assert.Equal(Shopper, basket.CustomerRef);
    }

    /// <summary>
    /// Spec FR-011 and SC-007: the basket survives a refresh and a browser restart because it is
    /// the server's basket for this caller. Two separate requests standing in for two separate page
    /// loads is exactly that guarantee, minus the browser.
    /// </summary>
    [Fact]
    public async Task GetCurrent_ReturnsTheSameBasket_AcrossSeparateRequests()
    {
        await using var factory = await CreateFactoryAsync("basket-current-stable");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 2, unitPrice: 12.50m);

        var first = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");
        var second = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        Assert.Equal(first!.Id, second!.Id);
        Assert.Equal(2, Assert.Single(second.Items).Quantity);
        Assert.Equal(25.00m, second.Total);
    }

    /// <summary>
    /// The other half of FR-006: one basket per shopper means a different shopper gets a different
    /// basket, not a shared one. With one stub identity in Phase 1 this is the assertion that stops
    /// "resolve by tenant" quietly passing for the wrong reason.
    /// </summary>
    [Fact]
    public async Task GetCurrent_GivesDifferentShoppersDifferentBaskets()
    {
        await using var factory = await CreateFactoryAsync("basket-current-per-shopper");

        await AddItemAsync(CreateClient(factory, Shopper), Notebook, quantity: 1, unitPrice: 12.50m);

        var theirs = await CreateClient(factory, OtherShopper)
            .GetFromJsonAsync<BasketResponse>("/baskets/current");

        Assert.NotNull(theirs);
        Assert.Empty(theirs.Items);
        Assert.Equal(OtherShopper, theirs.CustomerRef);
    }

    [Fact]
    public async Task AddItem_MergesIntoTheExistingLine_WhenTheSameProductIsAddedAgain()
    {
        await using var factory = await CreateFactoryAsync("basket-current-merge");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        var line = Assert.Single(basket!.Items);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, basket.Total);
    }

    [Fact]
    public async Task AddItem_KeepsDistinctProductsOnSeparateLines()
    {
        await using var factory = await CreateFactoryAsync("basket-current-two-products");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Apron, quantity: 1, unitPrice: 34.25m);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Two lines, three items: the merge holds across requests, not only within one.
        Assert.Equal(2, basket!.Items.Count);

        // The figure quickstart.md Scenarios 2 and 5 quote.
        Assert.Equal(59.25m, basket.Total);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        await using var factory = await CreateFactoryAsync($"basket-current-bad-quantity-{Math.Abs(quantity)}");
        var client = CreateClient(factory, Shopper);

        var response = await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity, unitPrice = 12.50m });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Constitution Principle V, extended to the caller: a request that did not come through the
    /// gateway resolved nobody, and must not be handed somebody's basket. There is no default
    /// caller to fall back to.
    /// </summary>
    [Fact]
    public async Task GetCurrent_Fails_WhenNoCallerWasResolved()
    {
        await using var factory = await CreateFactoryAsync("basket-current-no-caller");

        // Tenant but no subject: enough to reach persistence, not enough to name a shopper.
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);

        var response = await client.GetAsync("/baskets/current");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Asserts on the status with the body attached. <c>EnsureSuccessStatusCode</c> reports "500"
    /// and nothing else, which turns any server-side fault into a guessing exercise.
    /// </summary>
    private static async Task AddItemAsync(HttpClient client, Guid productId, int quantity, decimal unitPrice)
    {
        var response = await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId, quantity, unitPrice });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Adding to the basket failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database)
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
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<BasketsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string subjectId)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, subjectId);

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
