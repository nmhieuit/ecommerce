using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Products.Api.Data;
using Tenancy;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Spec US2 Acceptance Scenario 1 and Test Scenario 2: reaching this service without going through
/// the gateway means no tenant was ever resolved, and that must fail loudly rather than quietly
/// serving whatever lives in a default schema.
/// </summary>
/// <remarks>
/// Run against the real SQL Server the rest of the suite uses, deliberately: pointing these at an
/// unreachable database would make them pass whether or not the gate exists, since the request
/// would fail either way. With a working database behind it, an ungated service would answer
/// <c>200 OK</c> — so this failing is evidence of the gate and nothing else.
/// </remarks>
public class TenantEnforcementTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// The gate lives at the single <c>AddDbContext</c> call site (research.md Decision 6), so a
    /// context cannot even be constructed without a tenant — there is no window in which one exists
    /// but has not yet been checked.
    /// </summary>
    [Fact]
    public async Task ResolvingTheDbContext_Throws_WhenNoTenantHasBeenResolved()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        Assert.Throws<MissingTenantContextException>(
            () => scope.ServiceProvider.GetRequiredService<ProductsDbContext>());
    }

    /// <summary>
    /// quickstart.md Scenario 3: a 500-class response is the accepted Phase 1 outcome — the point
    /// under test is that it fails loudly rather than answering <c>200 OK</c> with some default
    /// tenant's catalog.
    /// </summary>
    [Fact]
    public async Task ARequestWithoutATenant_Fails_RatherThanServingDefaultSchemaData()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/products");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = sqlServer.ConnectionString,
                })));
}
