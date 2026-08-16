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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listing = await response.Content.ReadFromJsonAsync<ProductListResponse>();
        Assert.NotNull(listing);
        Assert.Equal(2, listing.Items.Length);

        // Field by field against contracts/bff-openapi.yaml's ProductSummary. Asserting the count
        // alone would pass even if the BFF returned two empty objects.
        var actualMug = Assert.Single(listing.Items, item => item.Id == mug.Id);
        Assert.Equal(mug.Name, actualMug.Name);
        Assert.Equal(mug.Price, actualMug.Price);

        var actualCafetiere = Assert.Single(listing.Items, item => item.Id == cafetiere.Id);
        Assert.Equal(cafetiere.Name, actualCafetiere.Name);
        Assert.Equal(cafetiere.Price, actualCafetiere.Price);
    }

    /// <summary>
    /// The contract wraps the listing in an <c>items</c> object rather than returning a bare
    /// array. An empty catalog must still produce that envelope, so the SPA can read
    /// <c>items</c> unconditionally instead of branching on the response's very shape.
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

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var listing = await response.Content.ReadFromJsonAsync<ProductListResponse>();
        Assert.NotNull(listing);
        Assert.Empty(listing.Items);
    }

    private sealed record ProductListResponse(ProductSummary[] Items);

    private sealed record ProductSummary(Guid Id, string Name, decimal Price);
}
