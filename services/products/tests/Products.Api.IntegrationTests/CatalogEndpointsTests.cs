using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Products.Api.Data;

namespace Products.Api.IntegrationTests;

/// <summary>
/// The catalog read surface the BFF's product-listing route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml, spec FR-004).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider —
/// these assertions only mean something if the rows made a round trip through a real database.
/// </summary>
public class CatalogEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task GetProducts_ReturnsEveryProduct_WithIdNameAndPrice()
    {
        var seeded = new[]
        {
            new Product { Id = Guid.NewGuid(), Name = "Ceramic mug", Price = 12.50m },
            new Product { Id = Guid.NewGuid(), Name = "Cafetiere", Price = 34.99m },
        };

        await using var factory = await CreateFactoryWithCatalogAsync(seeded);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<ProductResponse[]>();
        Assert.NotNull(products);
        Assert.Equal(2, products.Length);

        // Asserted field by field rather than by count alone: the BFF shapes ProductSummary
        // directly from these three fields, so a silently dropped or renamed one must fail here
        // rather than surfacing later as an empty column in the SPA.
        foreach (var expected in seeded)
        {
            var actual = Assert.Single(products, product => product.Id == expected.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Price, actual.Price);
        }
    }

    /// <summary>
    /// An empty catalog is a legitimate state, not an error — the contract specifies an empty
    /// array. A 404 here would force the BFF (and then the SPA) to treat "no products yet" as a
    /// failure case.
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsEmptyArray_WhenCatalogIsEmpty()
    {
        await using var factory = await CreateFactoryWithCatalogAsync([]);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty((await response.Content.ReadFromJsonAsync<ProductResponse[]>())!);
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
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = sqlServer.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ProductsDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Products.RemoveRange(dbContext.Products);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    private sealed record ProductResponse(Guid Id, string Name, decimal Price);
}
