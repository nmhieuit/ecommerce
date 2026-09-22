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
/// 004-minimal-shopping-spa spec FR-022 and contracts/downstream-openapi.yaml: an order is created
/// from the lines it is sent, with the total computed here.
/// </summary>
public class PlaceOrderTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: `POST /orders` với các dòng hợp lệ tạo đơn và trả về tổng do Orders tự tính.
    /// Lý do: nhánh happy-case của FR-022: đơn được tạo từ các dòng được gửi tới, tổng tính ở đây
    /// chứ không nhận từ caller.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-022).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_CreatesTheOrder_AndComputesItsTotal()
    {
        await using var factory = await CreateFactoryAsync("orders-place");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[]
            {
                new { productId = Notebook, quantity = 2, unitPrice = 12.50m },
                new { productId = Apron, quantity = 1, unitPrice = 34.25m },
            },
        });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Body là đơn hợp lệ.
        Assert.NotNull(order);
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau. Có id thật.
        Assert.NotEqual(Guid.Empty, order.Id);

        // quickstart.md Scenario 5's figure.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Server tự tính tổng
        // đúng.
        Assert.Equal(59.25m, order.Total);
    }

    /// <summary>
    /// Kiểm tra: mã định danh trả về khi đặt đơn đọc lại được đúng đơn đó.
    /// Lý do: SC-005: mã tham chiếu trên màn hình xác nhận phải khớp đơn thật trong backend — chỉ
    /// đúng nếu đơn tra được bằng chính mã đã đưa cho caller.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-022, SC-005).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_ReturnsAnIdentifier_ThatReadsBackAsTheSameOrder()
    {
        await using var factory = await CreateFactoryAsync("orders-readback");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        var readBack = await client.GetFromJsonAsync<OrderResponse>($"/orders/{created!.Id}");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(created.Id, readBack!.Id);
        Assert.Equal(created.Total, readBack.Total);
        Assert.Equal(created.PlacedAtUtc, readBack.PlacedAtUtc);
    }

    /// <summary>
    /// Kiểm tra: response tạo đơn kèm header `Location` trỏ tới đơn vừa tạo.
    /// Lý do: hợp đồng downstream (contracts/downstream-openapi.yaml) cho biết nơi tra cứu tài
    /// nguyên mới tạo.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-022).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_ReturnsALocationHeader_ForTheCreatedOrder()
    {
        await using var factory = await CreateFactoryAsync("orders-location");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        var order = await response.Content.ReadFromJsonAsync<OrderResponse>();

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Header Location phải trỏ
        // tới /orders/{id}; đỏ khi thiếu (null) hoặc sai đường dẫn.
        Assert.Equal($"/orders/{order!.Id}", response.Headers.Location?.ToString());
    }

    /// <summary>
    /// Kiểm tra: đặt đơn không có dòng nào bị từ chối.
    /// Lý do: nửa phía server của FR-008: storefront chặn giỏ rỗng trước khi gửi nhưng validate
    /// phía client chỉ là UX (Principle VI) — đơn cho không có gì phải bất khả thi ngay cả khi gọi
    /// thẳng API.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-008).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Rejects_ARequestWithNoLines()
    {
        await using var factory = await CreateFactoryAsync("orders-empty");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new { items = Array.Empty<object>() });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 400 (yêu cầu sai
        // bị từ chối); đỏ khi service chấp nhận (200/201) hoặc trả mã khác.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: đặt đơn có dòng số lượng không dương bị từ chối.
    /// Lý do: chặn dòng vô nghĩa hoặc dòng làm giảm tổng đơn ở lớp API, không chỉ ở domain.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-022).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Rejects_ALineWithANonPositiveQuantity()
    {
        await using var factory = await CreateFactoryAsync("orders-bad-quantity");
        var client = CreateClient(factory);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 0, unitPrice = 12.50m } },
        });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 400 (yêu cầu sai
        // bị từ chối); đỏ khi service chấp nhận (200/201) hoặc trả mã khác.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: đặt đơn khi không có người gọi nào được phân giải thì thất bại.
    /// Lý do: đơn hàng thuộc về 1 ai đó; request không đi qua gateway không xác định được ai và
    /// không được để lại đơn nào.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T050, US3 (FR-006, FR-022).
    /// </summary>
    [Fact]
    public async Task PlaceOrder_Fails_WhenNoCallerWasResolved()
    {
        await using var factory = await CreateFactoryAsync("orders-no-caller");

        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);

        var response = await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi tạo đơn không
        // biết chủ.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: đơn được lưu kèm đúng tenant đã phân giải của request đặt đơn.
    /// Lý do: assert trên dòng đã lưu chứ không phải response, vì response có thể lặp lại 1 giá trị
    /// mà bản ghi chưa bao giờ giữ.
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — FR-005, mở rộng bộ test đặt đơn của spec
    /// 004.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_PersistsTheResolvedTenant_OnTheOrderRow()
    {
        await using var factory = await CreateFactoryAsync("orders-tenant-persisted");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var stored = await dbContext.Orders.AsNoTracking().SingleAsync(order => order.Id == created!.Id);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Cột tenant của dòng lưu
        // trong DB phải là tenant đã xác định;
        Assert.Equal(TenantId, stored.TenantId);
    }

    /// <summary>
    /// Kiểm tra: body có khai 1 tenant khác thì không đổi gì — tenant lưu vẫn là tenant gateway đã
    /// phân giải.
    /// Lý do: trường tenant không thuộc hợp đồng request; thêm nó vào body chính là đường lén mà
    /// Constitution Principle V tồn tại để đóng.
    /// Task nguồn: spec 006 (demo đặt hàng end-to-end) — FR-005, mở rộng bộ test đặt đơn của spec
    /// 004.
    /// </summary>
    [Fact]
    public async Task PlaceOrder_IgnoresATenantNamedInTheRequestBody()
    {
        await using var factory = await CreateFactoryAsync("orders-tenant-smuggle");
        var client = CreateClient(factory);

        var created = await (await client.PostAsJsonAsync("/orders", new
        {
            tenantId = "someone-elses-tenant",
            items = new[] { new { productId = Notebook, quantity = 1, unitPrice = 12.50m } },
        })).Content.ReadFromJsonAsync<OrderResponse>();

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        var dbContext = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        var stored = await dbContext.Orders.AsNoTracking().SingleAsync(order => order.Id == created!.Id);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Tenant lưu vẫn là tenant
        // từ header đã xác thực, không phải giá trị trong body; đỏ khi tin body (ghi đơn vào tenant
        // khác).
        Assert.Equal(TenantId, stored.TenantId);
    }

    private async Task<WebApplicationFactory<Program>> CreateFactoryAsync(string database)
    {
        var connectionString = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(
            sqlServer.ConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(host =>
        {
            host.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:OrdersDb"] = connectionString,
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<OrdersDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record OrderResponse(Guid Id, DateTime PlacedAtUtc, decimal Total, string TenantId);
}
