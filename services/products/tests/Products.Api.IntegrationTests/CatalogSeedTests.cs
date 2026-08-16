using System.Net;
using System.Net.Http.Json;
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
    /// Each product is named individually rather than only counted: the walkthrough in
    /// quickstart.md quotes these prices, and the Playwright spec (T065) selects by these names, so
    /// a silent change to either would break a check somewhere far away from here.
    /// </summary>
    [Fact]
    public async Task ApplyingMigrations_SeedsTheCatalog_WithTheThreeKnownProducts()
    {
        await using var factory = await CreateMigratedFactoryAsync("products-seed");
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var products = await response.Content.ReadFromJsonAsync<ProductResponse[]>();
        Assert.NotNull(products);

        foreach (var expected in CatalogSeed.Products)
        {
            var actual = Assert.Single(products, product => product.Id == expected.Id);
            Assert.Equal(expected.Name, actual.Name);
            Assert.Equal(expected.Price, actual.Price);
        }
    }

    /// <summary>
    /// FR-018's actual promise is "at least one", and this is what a reviewer can check without
    /// caring which three products were chosen.
    /// </summary>
    [Fact]
    public async Task ApplyingMigrations_LeavesAPurchasableProduct_WithoutAnyManualSetup()
    {
        await using var factory = await CreateMigratedFactoryAsync("products-seed-minimum");
        var client = CreateTenantClient(factory);

        var products = await client.GetFromJsonAsync<ProductResponse[]>("/products");

        Assert.NotNull(products);
        Assert.NotEmpty(products);
        Assert.All(products, product =>
        {
            Assert.False(string.IsNullOrWhiteSpace(product.Name));
            Assert.True(product.Price > 0m, "A seeded product must have a real price to be purchasable.");
        });
    }

    /// <summary>
    /// The identifiers are fixed, not generated. Tests, the quickstart walkthrough, and the
    /// end-to-end spec all name specific products; a fresh identifier per environment would make
    /// every one of those references unusable.
    /// </summary>
    [Fact]
    public async Task TheSeededIdentifiers_AreStableAcrossFreshDatabases()
    {
        await using var first = await CreateMigratedFactoryAsync("products-seed-stable-one");
        await using var second = await CreateMigratedFactoryAsync("products-seed-stable-two");

        var fromFirst = await CreateTenantClient(first).GetFromJsonAsync<ProductResponse[]>("/products");
        var fromSecond = await CreateTenantClient(second).GetFromJsonAsync<ProductResponse[]>("/products");

        Assert.Equal(
            fromFirst!.Select(product => product.Id).OrderBy(id => id),
            fromSecond!.Select(product => product.Id).OrderBy(id => id));
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
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = builder.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so the tenant must be primed by hand or the gated
        // registration throws (003 research.md Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        await scope.ServiceProvider.GetRequiredService<ProductsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record ProductResponse(Guid Id, string Name, decimal Price);
}
