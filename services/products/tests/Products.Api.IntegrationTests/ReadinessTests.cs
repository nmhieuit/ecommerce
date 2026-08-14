using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.MsSql;

namespace Products.Api.IntegrationTests;

/// <summary>
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory
/// provider or a hand-rolled fake. Proves /health/ready reflects actual database
/// connectivity (spec FR-003), not just process liveness.
/// </summary>
public class ReadinessTests
{
    [Fact]
    public async Task HealthReady_ReturnsOk_WhenDatabaseReachable()
    {
        var sqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest").Build();
        await sqlContainer.StartAsync();
        try
        {
            await using var factory = CreateFactory(sqlContainer.GetConnectionString());
            var client = factory.CreateClient();

            var response = await client.GetAsync("/health/ready");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await sqlContainer.DisposeAsync();
        }
    }

    [Fact]
    public async Task HealthReady_ReturnsServiceUnavailable_WhenDatabaseUnreachable()
    {
        const string unreachableConnectionString =
            "Server=127.0.0.1,1;Database=unreachable;User Id=sa;Password=wrong;Connect Timeout=1;TrustServerCertificate=True";

        await using var factory = CreateFactory(unreachableConnectionString);
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:ProductsDb"] = connectionString,
                });
            });
        });
    }
}
