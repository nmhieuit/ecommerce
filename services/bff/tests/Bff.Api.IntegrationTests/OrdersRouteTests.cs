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
    /// <summary>
    /// Kiểm tra: `GET /bff/orders/{id}` đọc 1 đơn từ service Orders thật (SQL Server thật) và trả
    /// `200` với đúng `id`, thời điểm đặt và tổng tiền.
    /// Lý do: spec 002 FR-002/US1: SPA đọc đơn qua BFF chứ không gọi thẳng Orders. Sau khi spec 006
    /// thêm `tenantId` vào response của Orders, chính test này (không đổi) là bằng chứng BFF vẫn
    /// đọc được response mới và giữ hình dạng client (research.md Decision 4 của 006); lưu ý test
    /// chỉ deserialize 3 trường, không assert vắng `tenantId`.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — US1; được spec 006 T029 dùng làm bằng chứng
    /// BFF không cần đổi.
    /// </summary>
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
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync($"/bff/orders/{order.Id}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var actual = await response.Content.ReadFromJsonAsync<OrderResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null.
        Assert.NotNull(actual);
        Assert.Equal(order.Id, actual.Id);
        Assert.Equal(order.PlacedAtUtc, actual.PlacedAtUtc);
        Assert.Equal(order.Total, actual.Total);
    }

    /// <summary>
    /// Kiểm tra: Orders không có đơn đó thì BFF trả `404`.
    /// Lý do: lỗi "không tìm thấy" của downstream phải được chuyển thành `404` rõ ràng cho client,
    /// không biến thành `502` hay `200` rỗng.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — US1; được spec 006 T029 dùng làm bằng chứng
    /// BFF không cần đổi.
    /// </summary>
    [Fact]
    public async Task GetOrder_ReturnsNotFound_WhenTheOrdersServiceHasNoSuchOrder()
    {
        await using var orders = await CreateOrdersServiceAsync("bff-orders-missing");
        await using var bff = BffTestHost.CreateBff("OrdersApi", orders);
        var client = BffTestHost.CreateTenantClient(bff);

        var response = await client.GetAsync($"/bff/orders/{Guid.NewGuid()}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi 200 hoặc 500. BFF
        // phải chuyển 404 của Orders thành 404 cho client.
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
