using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Tenancy;

namespace Orders.Api.IntegrationTests;

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
    private const string CountingTenantId = "contoso";

    private static readonly Guid AnyOrderId = new("8a1f6f6e-0000-4000-8000-000000000002");
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task ResolvingTheDbContext_Throws_WhenNoTenantHasBeenResolved()
    {
        await using var factory = CreateFactory();
        using var scope = factory.Services.CreateScope();

        Assert.Throws<MissingTenantContextException>(
            () => scope.ServiceProvider.GetRequiredService<OrdersDbContext>());
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

        var response = await client.GetAsync($"/orders/{AnyOrderId}");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// 006-e2e-order-demo FR-006 and User Story 2 scenario 2: a write that reaches this service with
    /// no tenant resolved creates nothing.
    /// </summary>
    /// <remarks>
    /// The row count is what is asserted, not the status code. A failing response proves the caller
    /// was told no; it does not prove nothing was written, and those are different claims. This is
    /// the assertion the demo's "WITHOUT A TENANT" step stands on.
    /// </remarks>
    [Fact]
    public async Task AWriteWithoutATenant_CreatesNoOrder()
    {
        await using var factory = CreateFactory();

        var before = await CountOrdersAsync(factory);

        var response = await factory.CreateClient().PostAsJsonAsync("/orders", new
        {
            items = new[]
            {
                new { productId = Notebook, quantity = 1, unitPrice = 12.50m },
            },
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(before, await CountOrdersAsync(factory));
    }

    /// <summary>
    /// Counted through a scope that primes a tenant of its own, because counting is itself a read
    /// and this service refuses reads without one. That is the behaviour under test in the sibling
    /// cases here, so the counter has to opt in explicitly rather than inherit it.
    /// </summary>
    private static async Task<int> CountOrdersAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = CountingTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        return await dbContext.Orders.CountAsync();
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = sqlServer.ConnectionString,
                })));
}
