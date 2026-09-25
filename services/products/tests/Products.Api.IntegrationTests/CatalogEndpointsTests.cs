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
/// Bề mặt đọc catalog mà route liệt kê sản phẩm của BFF proxy tới
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml, spec 002 FR-004). Spec 023 đã cập
/// nhật 2 test ở đây từ mảng trần sang envelope phân trang `PagedProductsResponse`.
/// Hiến chương Principle III: SQL Server thật qua Testcontainers, không bao giờ provider in-memory —
/// các khẳng định này chỉ có nghĩa nếu dữ liệu đã đi 1 vòng qua database thật.
/// </summary>
public class CatalogEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Tenant mà các test này gieo và đọc dữ liệu. Giá trị không trắng nào cũng được — bộ test này nói
    /// về bề mặt catalog, không phải về việc tenant nào được resolve; đó là chủ đề của
    /// <see cref="TenantEnforcementTests"/>.
    /// </summary>
    private const string SeedTenantId = "contoso";

    /// <summary>
    /// Kiểm tra: gieo 2 sản phẩm, `GET /products` trả đúng 2 mục (`TotalCount = 2`), mỗi mục có đủ và
    /// đúng `Id`, `Name`, `Price`.
    /// Lý do: BFF định hình `ProductSummary` trực tiếp từ đúng 3 trường này — 1 trường bị rơi hoặc đổi
    /// tên phải fail ở đây, không phải lộ ra sau dưới dạng 1 cột trống trong SPA (kiểm từng trường, không
    /// chỉ đếm).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — FR-004 (bề mặt đọc catalog); shape envelope cập
    /// nhật bởi spec 023 (research.md Decision 5).
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsEveryProduct_WithIdNameAndPrice()
    {
        var seeded = new[]
        {
            new Product { Id = Guid.NewGuid(), Name = "Ceramic mug", Price = 12.50m },
            new Product { Id = Guid.NewGuid(), Name = "Cafetiere", Price = 34.99m },
        };

        await using var factory = await CreateFactoryWithCatalogAsync(seeded);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null.
        Assert.NotNull(page);
        Assert.Equal(2, page.Items.Count);
        Assert.Equal(2, page.TotalCount);

        // Kiểm từng trường thay vì chỉ đếm: BFF định hình ProductSummary trực tiếp từ đúng 3 trường
        // này, nên 1 trường bị rơi hoặc đổi tên âm thầm phải fail ở đây.
        foreach (var expected in seeded)
        {
            // Assert.Single(tập hợp, điều kiện): xanh khi đúng 1 phần tử khớp, đỏ khi 0 hoặc nhiều hơn.
            var actual = Assert.Single(page.Items, product => product.Id == expected.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Price, actual.Price);
        }
    }

    /// <summary>
    /// Kiểm tra: catalog rỗng → `GET /products` trả `200` với `Items` rỗng và `TotalCount = 0` (không
    /// phải `404`).
    /// Lý do: catalog rỗng là 1 trạng thái hợp lệ, không phải lỗi — hợp đồng quy định danh sách rỗng;
    /// `404` ở đây sẽ buộc BFF (rồi SPA) coi "chưa có sản phẩm" là 1 ca thất bại.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — FR-004; shape envelope cập nhật bởi spec 023.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsEmptyArray_WhenCatalogIsEmpty()
    {
        await using var factory = await CreateFactoryWithCatalogAsync([]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — `200`, không phải `404`.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<PagedProductsResponse>();
        Assert.NotNull(page);
        // Assert.Empty(tập hợp): xanh khi rỗng, đỏ khi có phần tử.
        Assert.Empty(page.Items);
        Assert.Equal(0, page.TotalCount);
    }

    /// <summary>
    /// Applies migrations and replaces the catalog with exactly <paramref name="products"/>, so
    /// each test states the whole database state it depends on instead of inheriting whatever a
    /// previously-run test left in the shared container.
    /// </summary>
    private async Task<WebApplicationFactory<Program>> CreateFactoryWithCatalogAsync(
        IReadOnlyCollection<Product> products)
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

        // No HTTP request runs for this scope, so TenantContextMiddleware never populates it —
        // seeding must prime the tenant itself or the gated registration throws (research.md
        // Decision 7). Nothing about the guarantee under test changes: a real request still has
        // this set only by the middleware.
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Products.RemoveRange(dbContext.Products);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    /// <summary>
    /// A client whose requests carry the tenant the gateway would have resolved. Without it every
    /// request here is Unresolved and never reaches the catalog at all — which is correct behaviour
    /// (<see cref="TenantEnforcementTests"/>) but not what this suite is asserting.
    /// </summary>
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
