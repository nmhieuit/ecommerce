using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// The order read surface the BFF's order route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class OrderEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    [Fact]
    public async Task GetOrder_ReturnsTheOrder_WhenItExists()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            // Whole seconds and explicitly UTC: SQL Server's datetime2 and the JSON round trip
            // both preserve this exactly, so a failure means a real mapping fault rather than
            // sub-millisecond precision loss.
            PlacedAtUtc = new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            Total = 47.49m,
        };

        await using var factory = await CreateFactoryWithOrdersAsync([order]);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(actual);
        Assert.Equal(order.Id, actual.Id);
        Assert.Equal(order.PlacedAtUtc, actual.PlacedAtUtc);
        Assert.Equal(order.Total, actual.Total);
    }

    [Fact]
    public async Task GetOrder_ReturnsNotFound_WhenNoOrderHasThatId()
    {
        await using var factory = await CreateFactoryWithOrdersAsync([]);
        var client = factory.CreateClient();

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryWithOrdersAsync(
        IReadOnlyCollection<Order> orders)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = sqlServer.ConnectionString,
                })));

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Orders.RemoveRange(dbContext.Orders);
        dbContext.Orders.AddRange(orders);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);
}
