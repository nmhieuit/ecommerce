using System.Net;
using Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// Spec US2 Acceptance Scenario 1 and Test Scenario 2: reaching this service without going through
/// the gateway means no tenant was ever resolved, and that must fail loudly rather than quietly
/// serving whatever lives in a default schema.
/// </summary>
/// <remarks>
/// Run against the real SQL Server the rest of the suite uses, deliberately: pointing these at an
/// unreachable database would make them pass whether or not the gate exists, since the request
/// would fail either way.
/// </remarks>
public class TenantEnforcementTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private static readonly Guid AnyBasketId = new("8a1f6f6e-0000-4000-8000-000000000001");

    [Fact]
    public async Task ResolvingTheDbContext_Throws_WhenNoTenantHasBeenResolved()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        Assert.Throws<MissingTenantContextException>(
            () => scope.ServiceProvider.GetRequiredService<BasketsDbContext>());
    }

    /// <summary>
    /// quickstart.md Scenario 3: a 500-class response is the accepted Phase 1 outcome — the point
    /// under test is that it fails loudly rather than answering from some default tenant's data.
    /// </summary>
    [Fact]
    public async Task ARequestWithoutATenant_Fails_RatherThanServingDefaultSchemaData()
    {
        await using var factory = CreateFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/baskets/{AnyBasketId}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:BasketsDb"] = sqlServer.ConnectionString,
                })));
}
