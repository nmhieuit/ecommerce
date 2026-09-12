using System.Net;
using System.Net.Http.Json;
using Bff.Api.DownstreamClients;
using Bff.Api.Features.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// specs/023-audit-n1-unbounded-pagination spec FR-001, User Story 1: <c>GET /bff/products</c> must
/// forward the caller's <c>page</c>/<c>pageSize</c> to the products service rather than silently
/// re-introducing an unbounded fetch at the BFF layer, and the downstream page metadata must survive
/// into <see cref="ProductsEndpoints.ProductListResponse"/> untouched (spec FR-007 — additive only).
/// </summary>
/// <remarks>
/// Two halves, mirroring <c>RetryMethodPolicyTests</c> (client-forwarding, through the real DI
/// registration with a captured request) and <c>ResponseMappingTests</c> (pure shaping function, no
/// HTTP at all) — each half is independently the simplest test that can fail for its own reason.
/// </remarks>
public class ProductsEndpointPaginationTests
{
    [Fact]
    public async Task GetProductsAsync_ForwardsPageAndPageSize_AsQueryParameters()
    {
        var handler = new CapturingHandler(new PagedProductsPayload([], 3, 50, 0));
        var productsClient = BuildProductsClient(handler);

        await productsClient.GetProductsAsync(page: 3, pageSize: 50, CancellationToken.None);

        Assert.NotNull(handler.LastRequestUri);
        var query = System.Web.HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
        Assert.Equal("3", query["page"]);
        Assert.Equal("50", query["pageSize"]);
    }

    [Fact]
    public async Task GetProductsByIdsAsync_SendsIdsAsCommaSeparatedQueryParameter_NotFullCatalogFetch()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var handler = new CapturingHandler(new PagedProductsPayload([], 1, 2, 2));
        var productsClient = BuildProductsClient(handler);

        await productsClient.GetProductsByIdsAsync([id1, id2], CancellationToken.None);

        Assert.NotNull(handler.LastRequestUri);
        var query = System.Web.HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
        Assert.Equal($"{id1},{id2}", query["ids"]);
        // No page/pageSize at all — an ids lookup is bounded by the id set itself
        // (research.md Decision 4), not by a page.
        Assert.Null(query["page"]);
    }

    [Fact]
    public async Task GetProductsByIdsAsync_WithNoIds_DoesNotCallDownstreamAtAll()
    {
        var handler = new CapturingHandler(new PagedProductsPayload([], 1, 0, 0));
        var productsClient = BuildProductsClient(handler);

        var result = await productsClient.GetProductsByIdsAsync([], CancellationToken.None);

        Assert.Empty(result);
        Assert.Equal(0, handler.InvocationCount);
    }

    /// <summary>Pure shaping, no HTTP — mirrors <c>ResponseMappingTests.ProductSummary_CarriesEveryFieldFromTheDownstreamProduct</c>.</summary>
    [Fact]
    public void ToListResponse_MapsDownstreamPageMetadata_AlongsideShapedItems()
    {
        var page = new ProductPageResource(
            [new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m)],
            Page: 2,
            PageSize: 20,
            TotalCount: 45);

        var response = ProductsEndpoints.ToListResponse(page);

        Assert.Single(response.Items);
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(45, response.TotalCount);
    }

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

    /// <summary>Records the request it was asked to send and replies with a fixed, valid payload.</summary>
    private sealed class CapturingHandler(PagedProductsPayload payload) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            };

            return Task.FromResult(response);
        }
    }
}
