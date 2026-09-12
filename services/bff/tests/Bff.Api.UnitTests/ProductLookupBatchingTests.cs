using System.Net;
using System.Net.Http.Json;
using Bff.Api.DownstreamClients;
using Bff.Api.Features.Baskets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// specs/023-audit-n1-unbounded-pagination spec FR-002/FR-003, User Story 2; Jira SCRUM-33 Test
/// Scenario 2 ("load a basket with multiple items — confirm one query, not one query per item").
/// Before this feature, <see cref="BasketsEndpoints.ToResponseAsync"/> called
/// <c>ProductsApiClient.GetProductsAsync()</c> — the entire catalog — on every render, regardless of
/// basket size. This suite locks in the fix: exactly one bounded call, for any basket size, and none
/// at all for an empty basket.
/// </summary>
/// <remarks>
/// Exercises <see cref="BasketsEndpoints.ToResponseAsync"/> directly (no HTTP host, no baskets
/// service) with a real <see cref="ProductsApiClient"/> whose primary handler is a counting fake —
/// same technique as <c>RetryMethodPolicyTests</c> and <c>ProductsEndpointPaginationTests</c>.
/// </remarks>
public class ProductLookupBatchingTests
{
    [Fact]
    public async Task RenderingBasket_WithMultipleDistinctProducts_CallsProductsClientExactlyOnce()
    {
        var handler = new CountingHandler(new PagedProductsPayload(
            [
                new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m),
                new ProductResource(Guid.NewGuid(), "Cafetiere", 34.99m),
                new ProductResource(Guid.NewGuid(), "Field Notes Notebook", 4.50m),
            ],
            Page: 1,
            PageSize: 3,
            TotalCount: 3));
        var productsClient = BuildProductsClient(handler);
        var basket = BasketWithDistinctLines(count: 5);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        Assert.Equal(1, handler.InvocationCount);
    }

    /// <summary>
    /// Two basket lines can name the same product (e.g. quantity bumped in a separate add-item
    /// call before the baskets service merges them) — the lookup must still resolve to the
    /// <em>distinct</em> id set, not one call per line.
    /// </summary>
    [Fact]
    public async Task RenderingBasket_WithDuplicateProductAcrossLines_StillCallsProductsClientExactlyOnce()
    {
        var sharedProductId = Guid.NewGuid();
        var handler = new CountingHandler(new PagedProductsPayload(
            [new ProductResource(sharedProductId, "Ceramic mug", 12.50m)],
            Page: 1,
            PageSize: 1,
            TotalCount: 1));
        var productsClient = BuildProductsClient(handler);

        var basket = new BasketResource(
            Guid.NewGuid(),
            "shopper-1",
            [
                new BasketLineItemResource(sharedProductId, Quantity: 1, UnitPrice: 12.50m, LineTotal: 12.50m),
                new BasketLineItemResource(sharedProductId, Quantity: 2, UnitPrice: 12.50m, LineTotal: 25.00m),
            ],
            Total: 37.50m);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        Assert.Equal(1, handler.InvocationCount);
    }

    /// <summary>
    /// Already-correct behaviour before this feature (the empty-basket early return) — kept as a
    /// regression guard alongside the two fixes above, not a new fix itself.
    /// </summary>
    [Fact]
    public async Task RenderingEmptyBasket_DoesNotCallProductsClient()
    {
        var handler = new CountingHandler(new PagedProductsPayload([], 1, 0, 0));
        var productsClient = BuildProductsClient(handler);
        var basket = new BasketResource(Guid.NewGuid(), "shopper-1", [], Total: 0m);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        Assert.Equal(0, handler.InvocationCount);
    }

    private static BasketResource BasketWithDistinctLines(int count) =>
        new(
            Guid.NewGuid(),
            "shopper-1",
            [.. Enumerable.Range(1, count).Select(_ => new BasketLineItemResource(
                Guid.NewGuid(),
                Quantity: 1,
                UnitPrice: 9.99m,
                LineTotal: 9.99m))],
            Total: count * 9.99m);

    private static ProductsApiClient BuildProductsClient(HttpMessageHandler primaryHandler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:ProductsApi:BaseUrl"] = "http://products.test",
                ["Services:BasketsApi:BaseUrl"] = "http://baskets.test",
                ["Services:OrdersApi:BaseUrl"] = "http://orders.test",
                ["Services:PartiesApi:BaseUrl"] = "http://parties.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDownstreamClients(configuration);

        services.AddHttpClient(ProductsApiClient.ServiceName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services.BuildServiceProvider().GetRequiredService<ProductsApiClient>();
    }

    private sealed record PagedProductsPayload(
        IReadOnlyList<ProductResource> Items, int Page, int PageSize, int TotalCount);

    /// <summary>Counts invocations and replies with a fixed, valid payload — no assertions on the request itself; <c>ProductsEndpointPaginationTests</c> already covers the request shape.</summary>
    private sealed class CountingHandler(PagedProductsPayload payload) : HttpMessageHandler
    {
        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            };

            return Task.FromResult(response);
        }
    }
}
