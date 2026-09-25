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
/// Spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) FR-001/FR-004, User Story 1 và 3: mọi lời
/// gọi <c>GET /products</c> đều trả về 1 trang có giới hạn — dù caller không truyền tham số phân trang
/// nào (mặc định 1 trang có giới hạn) hay truyền 1 giá trị quá lớn (trần phía server thắng giá trị
/// caller).
/// </summary>
/// <remarks>
/// Hiến chương Principle III: SQL Server thật qua Testcontainers — gieo 500 dòng chỉ chứng minh
/// endpoint có giới hạn nếu 500 dòng đó thật sự tồn tại trong 1 database thật, không phải 1 giả lập
/// in-memory có thể "vô tình" phân trang 1 tập vốn đã nhỏ.
/// </remarks>
public class ProductListingPaginationTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string SeedTenantId = "contoso";

    /// <summary>
    /// Kiểm tra: gieo 500 sản phẩm, gọi `GET /products` KHÔNG truyền tham số phân trang → trả đúng 20
    /// mục (mặc định), `TotalCount = 500`, `Page = 1`, `PageSize = 20` — không phải cả 500.
    /// Lý do: Jira SCRUM-33 Test Scenario 1, US1 Acceptance Scenario 1 / SC-001 — "quên truyền tham số
    /// thì lấy hết" là đúng lỗ hổng tính năng này đóng lại.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001, US1, SC-001.
    /// </summary>
    [Fact]
    public async Task ListProducts_WithoutQueryParameters_ReturnsOnlyDefaultPageSize_NotTheWholeCatalog()
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi body không đọc được thành envelope.
        Assert.NotNull(page);
        // Đúng 20 mục (mặc định) dù có 500 bản ghi; nhiều hơn nghĩa là trần mặc định đã mất.
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(500, page.TotalCount);
        Assert.Equal(1, page.Page);
        Assert.Equal(20, page.PageSize);
    }

    /// <summary>
    /// Kiểm tra: gọi `GET /products?pageSize=1000000` → trả đúng 100 mục (`MaxPageSize`), `PageSize =
    /// 100`, `TotalCount = 500`.
    /// Lý do: Jira SCRUM-33 Test Scenario 3, US3 Acceptance Scenario 1 / SC-003 — trần phải do server
    /// ép (FR-004), không được thực thi nguyên giá trị caller đòi.
    /// Lưu ý: chỉ phủ tham số `pageSize` hợp lệ về kiểu; KHÔNG phủ `page`/`pageSize` không phải số
    /// (BFF trả `500`) hay `page` cực lớn (tràn số nguyên → `OFFSET` âm) và cũng KHÔNG phủ nhánh `ids`
    /// (không bị trần) — xem QA_Debt mục 023.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-004, US3, SC-003.
    /// </summary>
    [Fact]
    public async Task ListProducts_WithExcessivePageSize_IsCappedAtMaxPageSize()
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products?pageSize=1000000");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null.
        Assert.NotNull(page);
        // Đúng 100 (trần), không phải 500 hay 1000000; đỏ khi trần không được ép.
        Assert.Equal(100, page.Items.Count);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(500, page.TotalCount);
    }

    /// <summary>
    /// Kiểm tra: `pageSize = 0` hoặc `-5` (`[Theory]` chạy 2 lần) không vượt trần và không làm request
    /// thất bại — quay về đúng mặc định (20 mục) như caller không truyền gì.
    /// Lý do: 1 kích thước trang không hợp lệ (0/âm) không được lách qua trần hay gây lỗi.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-004, US3-KB3.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task ListProducts_WithZeroOrNegativePageSize_FallsBackToDefault(int requestedPageSize)
    {
        await using var factory = await CreateFactoryWithCatalogAsync(GenerateProducts(500));
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/products?pageSize={requestedPageSize}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null.
        Assert.NotNull(page);
        Assert.Equal(20, page.Items.Count);
        Assert.Equal(20, page.PageSize);
    }

    /// <summary>
    /// Kiểm tra: `GET /products?ids=a,b` trả đúng 2 sản phẩm được yêu cầu (`TotalCount = 2`, `Page = 1`),
    /// bỏ qua `page`/`pageSize`.
    /// Lý do: research.md Decision 4 — bộ lọc `ids` tường minh bị chặn bởi chính tập id caller truyền,
    /// không phải bởi page/pageSize; nhờ đó BFF lấy đúng các sản phẩm 1 giỏ cần chỉ với 1 lời gọi (US2 /
    /// `ProductLookupBatchingTests` ở BFF).
    /// Lưu ý: nghĩa là nhánh `ids` KHÔNG chịu trần `MaxPageSize` (đo thật: 200 id → trả 200 mục, bị giới
    /// hạn chỉ bởi độ dài URL) — xem QA_Debt mục 023.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — US2, research.md Decision 4.
    /// </summary>
    [Fact]
    public async Task ListProducts_WithIdsFilter_ReturnsExactlyTheMatchingProducts_IgnoringPageSize()
    {
        var seeded = GenerateProducts(10).ToArray();
        await using var factory = await CreateFactoryWithCatalogAsync(seeded);
        var client = CreateTenantClient(factory);

        var wanted = new[] { seeded[2].Id, seeded[5].Id };

        var response = await client.GetAsync($"/products?ids={wanted[0]},{wanted[1]}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null.
        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(1, page.Page);
        // Assert.All(tập hợp, hành động): đỏ nếu bất kỳ mục trả về không thuộc tập id đã yêu cầu.
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
