extern alias ProductsApi;

using System.Net;
using System.Net.Http.Json;
using ProductsApi::Products.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Spec Test Scenario 1 / FR-004 / SC-002: the BFF's product-listing route proxies the real
/// products service and returns shaped data. This is the proxy path the whole feature is built to
/// prove, so it is asserted against a real Products.Api reading a real database
/// (research.md Decision 5) rather than a stand-in.
/// </summary>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class ProductsRouteTests(DownstreamServicesFixture fixture)
{
    /// <summary>
    /// Kiểm tra: `GET /bff/products` (qua BFF, chạm 1 Products.Api thật với database thật) trả về
    /// `200` với danh sách sản phẩm đã định hình đúng từng trường (`id`/`name`/`price`) khớp
    /// `contracts/bff-openapi.yaml`, so khớp từng sản phẩm chứ không chỉ đếm số lượng.
    /// Lý do: đây chính là đường proxy cốt lõi mà cả spec 002 tồn tại để chứng minh, nên được kiểm
    /// chứng bằng service + database thật, không phải bằng stand-in — so khớp từng trường vì chỉ
    /// đếm `Items.Length` vẫn có thể pass dù BFF trả về 2 object rỗng.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T033, US1.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsShapedListingFromTheProductsService()
    {
        var mug = new Product { Id = Guid.NewGuid(), Name = "Ceramic mug", Price = 12.50m };
        var cafetiere = new Product { Id = Guid.NewGuid(), Name = "Cafetiere", Price = 34.99m };

        await using var products = await BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor("bff-products"),
            async dbContext =>
            {
                dbContext.Products.RemoveRange(dbContext.Products);
                dbContext.Products.AddRange(mug, cafetiere);
                await dbContext.SaveChangesAsync();
            });

        await using var bff = BffTestHost.CreateBff("ProductsApi", products);
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync("/bff/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listing = await response.Content.ReadFromJsonAsync<ProductListResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null.
        Assert.NotNull(listing);
        Assert.Equal(2, listing.Items.Length);

        // Field by field against contracts/bff-openapi.yaml's ProductSummary. Asserting the count
        // alone would pass even if the BFF returned two empty objects.
        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var actualMug = Assert.Single(listing.Items, item => item.Id == mug.Id);
        Assert.Equal(mug.Name, actualMug.Name);
        Assert.Equal(mug.Price, actualMug.Price);

        var actualCafetiere = Assert.Single(listing.Items, item => item.Id == cafetiere.Id);
        Assert.Equal(cafetiere.Name, actualCafetiere.Name);
        Assert.Equal(cafetiere.Price, actualCafetiere.Price);
    }

    /// <summary>
    /// Kiểm tra: khi catalog rỗng, `GET /bff/products` vẫn trả `200` với envelope `{"items": []}`,
    /// không phải mảng trần rỗng hay lỗi.
    /// Lý do: hợp đồng API bọc danh sách trong object `items`, không trả mảng trần. Catalog rỗng
    /// vẫn phải giữ đúng shape đó, để SPA luôn đọc `items` một cách vô điều kiện thay vì phải rẽ
    /// nhánh theo hình dạng response.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T033, US1.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsEmptyItemsEnvelope_WhenTheCatalogIsEmpty()
    {
        await using var products = await BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor("bff-products-empty"),
            async dbContext =>
            {
                dbContext.Products.RemoveRange(dbContext.Products);
                await dbContext.SaveChangesAsync();
            });

        await using var bff = BffTestHost.CreateBff("ProductsApi", products);
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync("/bff/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 200 (không phải
        // 404).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listing = await response.Content.ReadFromJsonAsync<ProductListResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Vẫn phải có "phong bì" items.
        Assert.NotNull(listing);
        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có.
        Assert.Empty(listing.Items);
    }

    private sealed record ProductListResponse(ProductSummary[] Items);

    private sealed record ProductSummary(Guid Id, string Name, decimal Price);
}
