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
/// 004-minimal-shopping-spa spec FR-018: "the catalog MUST contain at least one purchasable product
/// in every environment where this flow is demonstrated, so that the walkthrough is reachable
/// without manual data setup." The seed is part of the migration history, so applying migrations is
/// the whole setup — that is what these tests assert.
/// </summary>
/// <remarks>
/// Unlike <see cref="CatalogEndpointsTests"/>, this suite deliberately does not clear the catalog
/// before reading it. Clearing it would remove the very rows under test; here the migrated state
/// <em>is</em> the fixture.
/// </remarks>
public class CatalogSeedTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string SeedTenantId = "contoso";

    /// <summary>
    /// Kiểm tra: sau khi áp dụng migration, bảng Products có đúng 3 sản phẩm đã biết với mã, tên,
    /// giá cố định (Notebook $12.50, Pour-Over $48.00, Apron $34.25).
    /// Lý do phải test: từng sản phẩm được nêu tên riêng chứ không chỉ đếm — quickstart.md trích
    /// các giá này và Playwright (T065) chọn theo các tên này, nên đổi âm thầm sẽ làm hỏng 1 kiểm
    /// tra ở rất xa đây.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T023, US1 (FR-018).
    /// </summary>
    [Fact]
    public async Task ApplyingMigrations_SeedsTheCatalog_WithTheThreeKnownProducts()
    {
        await using var factory = await CreateMigratedFactoryAsync("products-seed");
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        var products = page.Items;

        foreach (var expected in CatalogSeed.Products)
        {
            var actual = Assert.Single(products, product => product.Id == expected.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Price, actual.Price);
        }
    }

    /// <summary>
    /// Kiểm tra: chỉ áp dụng migration là catalog đã có ít nhất 1 sản phẩm mua được, không cần
    /// setup dữ liệu thủ công.
    /// Lý do phải test: lời hứa thật của FR-018 là "ít nhất một" — reviewer kiểm được điều này mà
    /// không cần quan tâm 3 sản phẩm cụ thể là gì.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T023, US1 (FR-018).
    /// </summary>
    [Fact]
    public async Task ApplyingMigrations_LeavesAPurchasableProduct_WithoutAnyManualSetup()
    {
        await using var factory = await CreateMigratedFactoryAsync("products-seed-minimum");
        var client = CreateTenantClient(factory);

        var page = await client.GetFromJsonAsync<PagedProductsResponse>("/products");

        Assert.NotNull(page);
        Assert.NotEmpty(page.Items);
        Assert.All(page.Items, product =>
        {
            Assert.False(string.IsNullOrWhiteSpace(product.Name));
            Assert.True(product.Price > 0m, "A seeded product must have a real price to be purchasable.");
        });
    }

    /// <summary>
    /// Kiểm tra: mã sản phẩm seed giống hệt nhau trên các database mới.
    /// Lý do phải test: mã là hằng cố định, không sinh ngẫu nhiên: test, quickstart và e2e đều gọi
    /// tên sản phẩm cụ thể; mã đổi theo môi trường sẽ làm mọi tham chiếu đó vô dụng (research.md
    /// Decision 10).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T023, US1 (FR-018, research.md Decision 10).
    /// </summary>
    [Fact]
    public async Task TheSeededIdentifiers_AreStableAcrossFreshDatabases()
    {
        await using var first = await CreateMigratedFactoryAsync("products-seed-stable-one");
        await using var second = await CreateMigratedFactoryAsync("products-seed-stable-two");

        var fromFirst = await CreateTenantClient(first).GetFromJsonAsync<PagedProductsResponse>("/products");
        var fromSecond = await CreateTenantClient(second).GetFromJsonAsync<PagedProductsResponse>("/products");

        Assert.Equal(
            fromFirst!.Items.Select(product => product.Id).OrderBy(id => id),
            fromSecond!.Items.Select(product => product.Id).OrderBy(id => id));
    }

    /// <summary>
    /// Applies migrations against a database of its own and returns the host, without touching the
    /// rows the migration inserted.
    /// </summary>
    private async Task<WebApplicationFactory<Program>> CreateMigratedFactoryAsync(string database)
    {
        var builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        };

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = builder.ConnectionString,
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so the tenant must be primed by hand or the gated
        // registration throws (003 research.md Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        await scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record ProductResponse(Guid Id, string Name, decimal Price);

    private sealed record PagedProductsResponse(
        IReadOnlyList<ProductResponse> Items, int Page, int PageSize, int TotalCount);
}
