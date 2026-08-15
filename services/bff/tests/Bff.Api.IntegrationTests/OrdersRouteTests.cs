extern alias OrdersApi;

using System.Net;
using System.Net.Http.Json;
using OrdersApi::Orders.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// US1: the BFF serves order data so the SPA never addresses the orders service directly
/// (spec FR-002). Asserted against a real Orders.Api reading a real database
/// (research.md Decision 5).
/// </summary>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class OrdersRouteTests(DownstreamServicesFixture fixture)
{
    [Fact]
    public async Task GetOrder_ReturnsShapedOrderFromTheOrdersService()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            PlacedAtUtc = new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            Total = 47.49m,
        };

        await using var orders = await CreateOrdersServiceAsync("bff-orders", order);
        await using var bff = BffTestHost.CreateBff("OrdersApi", orders);
        var client = bff.CreateClient();

        var response = await client.GetAsync($"/bff/orders/{order.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<OrderResponse>();
        Assert.NotNull(actual);
        Assert.Equal(order.Id, actual.Id);
        Assert.Equal(order.PlacedAtUtc, actual.PlacedAtUtc);
        Assert.Equal(order.Total, actual.Total);
    }

    [Fact]
    public async Task GetOrder_ReturnsNotFound_WhenTheOrdersServiceHasNoSuchOrder()
    {
        await using var orders = await CreateOrdersServiceAsync("bff-orders-missing");
        await using var bff = BffTestHost.CreateBff("OrdersApi", orders);
        var client = bff.CreateClient();

        var response = await client.GetAsync($"/bff/orders/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<OrdersApi::Program>>
        CreateOrdersServiceAsync(string database, params Order[] orders) =>
        BffTestHost.CreateDownstreamAsync<OrdersApi::Program, OrdersDbContext>(
            "OrdersDb",
            fixture.ConnectionStringFor(database),
            async dbContext =>
            {
                dbContext.Orders.RemoveRange(dbContext.Orders);
                dbContext.Orders.AddRange(orders);
                await dbContext.SaveChangesAsync();
            });

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);
}
