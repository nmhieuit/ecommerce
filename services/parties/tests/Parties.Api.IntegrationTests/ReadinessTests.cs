using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Parties.Api.IntegrationTests;

/// <summary>
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory
/// provider or a hand-rolled fake. Proves /health/ready reflects actual database
/// connectivity (spec FR-003), not just process liveness.
/// </summary>
public class ReadinessTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// Syntactically valid, deliberately unroutable — there is nothing at this address to answer.
    /// </summary>
    private const string UnreachableConnectionString =
        "Server=127.0.0.1,1;Database=unreachable;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=True";

    [Fact]
    public async Task HealthReady_ReturnsOk_WhenDatabaseReachable()
    {
        await using var factory = CreateFactory(sqlServer.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable()
    {
        // Proves readiness fails closed rather than silently reporting healthy (spec Edge Cases).
        await using var factory = CreateFactory(UnreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    /// <summary>
    /// Spec US2 acceptance scenario 2: with its own database unreachable, the service must not
    /// quietly fall back to another service's data store or a shared default. The fixture's
    /// container stands in for another service's database — genuinely reachable, and offered to
    /// the service under every other service's connection-string key. Readiness must still fail.
    /// </summary>
    [Fact]
    public async Task HealthReady_DoesNotFallBackToAnotherServicesDatabase_WhenOwnDatabaseUnreachable()
    {
        await using var factory = CreateFactory(
            UnreachableConnectionString,
            reachableForeignConnectionString: sqlServer.ConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        // Named explicitly so the assertion cannot be satisfied by some unrelated failure.
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("self-database", body);
        Assert.Contains("Unhealthy", body);
    }

    private static WebApplicationFactory<Program> CreateFactory(
        string connectionString,
        string? reachableForeignConnectionString = null)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                var settings = new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PartiesDb"] = connectionString,
                };

                if (reachableForeignConnectionString is not null)
                {
                    settings["ConnectionStrings:ProductsDb"] = reachableForeignConnectionString;
                    settings["ConnectionStrings:BasketsDb"] = reachableForeignConnectionString;
                    settings["ConnectionStrings:OrdersDb"] = reachableForeignConnectionString;
                }

                config.AddInMemoryCollection(settings);
            });
        });
    }
}
