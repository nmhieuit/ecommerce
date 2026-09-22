using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Orders.Api.Data;
using Tenancy;

namespace Orders.Api.IntegrationTests;

/// <summary>
/// The order read surface the BFF's order route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// Constitution Principle III: real SQL Server via Testcontainers, never an in-memory provider.
/// </summary>
public class OrderEndpointsTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    /// <summary>
    /// The tenant these tests seed and read as. Any non-blank value works — this suite is about the
    /// order surface, not about which tenant resolved; that is
    /// <see cref="TenantEnforcementTests"/>' subject.
    /// </summary>
    private const string SeedTenantId = "contoso";

    /// <summary>
    /// Kiểm tra: `GET /orders/{id}` của 1 đơn đã có trả `200` với đúng `id`, thời điểm đặt và tổng
    /// tiền.
    /// Lý do: nhánh happy-case của việc đọc lại đơn qua service Orders; dùng SQL Server thật, giây
    /// nguyên và UTC để 1 lỗi chỉ có thể là lỗi ánh xạ thật chứ không phải mất độ chính xác thời
    /// gian.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — bề mặt đọc đơn hàng của Orders (FR-022), bộ
    /// test này được spec 006 T023 mở rộng.
    /// </summary>
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
            TenantId = SeedTenantId,
        };

        await using var factory = await CreateFactoryWithOrdersAsync([order]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/orders/{order.Id}");

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
    /// Kiểm tra: đọc lại đơn thì response có `tenantId` không rỗng và bằng đúng tenant đã lưu
    /// ("contoso").
    /// Lý do: FR-005a: quy thuộc tenant phải nhìn thấy được ngay từ kết quả đọc lại — bước kiểm
    /// chứng của demo phát biểu được điều đó mà không cần soi database hay đọc mã nguồn; assert cả
    /// "không rỗng" chứ không chỉ "có lưu".
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — T023, US2 (FR-005a).
    /// </summary>
    [Fact]
    public async Task GetOrder_ReturnsTheTenantTheOrderBelongsTo()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            PlacedAtUtc = new DateTime(2026, 8, 19, 10, 0, 0, DateTimeKind.Utc),
            Total = 59.25m,
            TenantId = SeedTenantId,
        };

        await using var factory = await CreateFactoryWithOrdersAsync([order]);
        var client = CreateTenantClient(factory);

        var actual = await client.GetFromJsonAsync<OrderResponse>($"/orders/{order.Id}");

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Có đơn trả về.
        Assert.NotNull(actual);
        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng. Đỏ khi đọc lại đơn mà thiếu
        // tenant.
        Assert.False(
            string.IsNullOrWhiteSpace(actual.TenantId),
            "an order read back must name its tenant, not merely have one stored");
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Tenant trả về phải đúng
        // tenant đã nạp.
        Assert.Equal(SeedTenantId, actual.TenantId);
    }

    /// <summary>
    /// Kiểm tra: đọc đơn không tồn tại trả `404`.
    /// Lý do: người mua/người chạy demo tra 1 mã sai phải nhận câu trả lời rõ ràng, không phải lỗi
    /// 500 hay đơn của tenant khác.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — bề mặt đọc đơn hàng của Orders; bộ test này
    /// được spec 006 T023 mở rộng.
    /// </summary>
    [Fact]
    public async Task GetOrder_ReturnsNotFound_WhenNoOrderHasThatId()
    {
        await using var factory = await CreateFactoryWithOrdersAsync([]);
        var client = CreateTenantClient(factory);

        var response = await client.GetAsync($"/orders/{Guid.NewGuid()}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi 200 hoặc 500.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryWithOrdersAsync(
        IReadOnlyCollection<Order> orders)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = sqlServer.ConnectionString,
                }));
            builder.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so TenantContextMiddleware never populates it —
        // seeding must prime the tenant itself or the gated registration throws (research.md
        // Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        await dbContext.Database.MigrateAsync();

        dbContext.Orders.RemoveRange(dbContext.Orders);
        dbContext.Orders.AddRange(orders);
        await dbContext.SaveChangesAsync();

        return factory;
    }

    /// <summary>
    /// A client whose requests carry the tenant the gateway would have resolved. Without it every
    /// request here is Unresolved and never reaches persistence at all.
    /// </summary>
    private static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total, string TenantId);
}
