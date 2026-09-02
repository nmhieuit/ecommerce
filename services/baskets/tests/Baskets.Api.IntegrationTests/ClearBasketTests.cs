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
/// 004-minimal-shopping-spa spec FR-010 and contracts/downstream-openapi.yaml: checkout empties the
/// basket, leaving the basket itself so the shopper's basket identity is stable across purchases.
/// </summary>
public class ClearBasketTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task Clear_RemovesEveryLine_ButKeepsTheBasket()
    {
        await using var factory = await CreateFactoryAsync("basket-clear");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 2, unitPrice = 12.50m });

        var before = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        var response = await client.PostAsync("/baskets/current/clear", content: null);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        Assert.Empty(after!.Items);
        Assert.Equal(0m, after.Total);

        // The same basket, now empty — not a new one. A fresh identifier each checkout would mean
        // the basket row is being deleted and recreated, which is not what FR-010 asks for.
        Assert.Equal(before!.Id, after.Id);
    }

    /// <summary>
    /// Spec FR-008 and FR-016: reported rather than silently succeeding, so a checkout of an
    /// already-empty basket cannot proceed to create a second order.
    /// </summary>
    [Fact]
    public async Task Clear_ReturnsConflict_WhenTheBasketIsAlreadyEmpty()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-empty");
        var client = CreateClient(factory);

        var response = await client.PostAsync("/baskets/current/clear", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// The second clear of a checked-out basket is the same conflict. This is what makes a repeated
    /// checkout attempt fail loudly instead of quietly emptying nothing and carrying on.
    /// </summary>
    [Fact]
    public async Task Clear_ReturnsConflict_OnASecondClear()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-twice");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 1, unitPrice = 12.50m });

        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync("/baskets/current/clear", content: null)).StatusCode);

        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync("/baskets/current/clear", content: null)).StatusCode);
    }

    /// <summary>
    /// After clearing, the shopper can shop again into the same basket. Checkout ends a purchase,
    /// not the shopper's relationship with their basket.
    /// </summary>
    [Fact]
    public async Task Clear_LeavesTheBasketUsable_ForTheNextPurchase()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-reuse");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 1, unitPrice = 12.50m });
        await client.PostAsync("/baskets/current/clear", content: null);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 3, unitPrice = 12.50m });

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        Assert.Equal(3, Assert.Single(basket!.Items).Quantity);
        Assert.Equal(37.50m, basket.Total);
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
