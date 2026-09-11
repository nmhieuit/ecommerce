using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Products.Api.Data;
using Tenancy;

namespace Products.Api.IntegrationTests;

/// <summary>
/// specs/023-audit-n1-unbounded-pagination spec FR-001/FR-004, User Story 1 and User Story 3: every
/// call to <c>GET /products</c> returns a bounded page, whether the caller supplied no paging
/// parameters at all (defaults to a bounded page) or supplied an excessively large one (server-side
/// cap wins over the caller's value).
/// </summary>
/// <remarks>
/// Constitution Principle III: real SQL Server via Testcontainers — a seed of 500 rows only proves
/// the endpoint is bounded if the 500 rows genuinely exist in a real database, not an in-memory
/// stand-in that could paginate an already-small collection by coincidence.
/// </remarks>
public class ProductListingPaginationTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string SeedTenantId = "contoso";

    /// <summary>
    /// Jira SCRUM-33 Test Scenario 1, spec User Story 1 Acceptance Scenario 1 / SC-001: seeding 500
    /// products and calling the listing endpoint without a page parameter must still return a
    /// bounded page, not all 500.
    /// </summary>
    [Fact]
    public async Task ListProducts_WithoutQueryParameters_ReturnsOnlyDefaultPageSize_NotTheWholeCatalog()
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(500, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    /// <summary>
    /// Jira SCRUM-33 Test Scenario 3, spec User Story 3 Acceptance Scenario 1 / SC-003: a caller
    /// requesting an absurdly large page size must still be capped server-side, not honoured as-is.
    /// </summary>
    [Fact]
    public async Task ListProducts_WithExcessivePageSize_IsCappedAtMaxPageSize()
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products?pageSize=1000000");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(500, page.TotalCount);
    }

    /// <summary>
    /// An invalid page size (zero or negative) must not slip past the cap or fail the request — it
    /// falls back to the same default a caller who supplied nothing at all would get.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ListProducts_WithZeroOrNegativePageSize_FallsBackToDefault(int requestedPageSize)
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/products?pageSize={requestedPageSize}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(20, page.PageSize);
    }

    /// <summary>
    /// research.md Decision 4: an explicit <c>ids</c> filter is bounded by the caller-supplied id
    /// set itself, not by page/pageSize — this is what lets the BFF resolve exactly the products a
    /// basket needs in one call (spec User Story 2 / <c>ProductLookupBatchingTests</c> at the BFF).
    /// </summary>
    [Fact]
    public async Task ListProducts_WithIdsFilter_ReturnsExactlyTheMatchingProducts_IgnoringPageSize()
    {
        var seeded = GenerateProducts(10).ToArray();
        await using var factory = await CreateFactoryWithCatalogAsync(seeded);
        var client = CreateTenantClient(factory);

        var wanted = new[] { seeded[2].Id, seeded[5].Id };

        var response = await client.GetAsync($"/products?ids={wanted[0]},{wanted[1]}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.All(page.Items, item => Assert.Contains(item.Id, wanted));
    }

    private static IEnumerable<Product> GenerateProducts(int count) =>
        Enumerable.Range(1, count).Select(i => new Product
        {
            Id = Guid.NewGuid(),
            Name = $"Seeded product {i:D4}",
            Price = 9.99m,
        });

    /// <summary>
    /// Applies migrations and replaces the catalog with exactly <paramref name="products"/>, so
    /// each test states the whole database state it depends on instead of inheriting whatever a
    /// previously-run test left in the shared container. Mirrors <c>CatalogEndpointsTests</c>.
    /// </summary>
    private async Task<WebApplicationFactory<Program>> CreateFactoryWithCatalogAsync(
        IEnumerable<Product> products)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = sqlServer.ConnectionString,
                }));
            builder.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Products.RemoveRange(dbContext.Products);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record ProductSummary(Guid Id, string Name, decimal Price);

    private sealed record PagedProductsResponse(
        IReadOnlyList<ProductSummary> Items, int Page, int PageSize, int TotalCount);
}
