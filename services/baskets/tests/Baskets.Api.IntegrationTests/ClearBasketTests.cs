using System.Net;
using System.Net.Http.Json;
using Baskets.Api.Data;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Baskets.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-010 and contracts/downstream-openapi.yaml: checkout empties the
/// basket, leaving the basket itself so the shopper's basket identity is stable across purchases.
/// </summary>
public class ClearBasketTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    /// <summary>
    /// Kiểm tra: xoá giỏ làm mất mọi dòng nhưng giữ lại bản ghi giỏ.
    /// Lý do: FR-010: thanh toán xong giỏ phải rỗng; giữ lại chính giỏ để định danh giỏ của người
    /// mua ổn định qua các lần mua.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T051, US3 (FR-010).
    /// </summary>
    [Fact]
    public async Task Clear_RemovesEveryLine_ButKeepsTheBasket()
    {
        await using var factory = await CreateFactoryAsync("basket-clear");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 2, unitPrice = 12.50m });

        var before = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        var response = await client.PostAsync("/baskets/current/clear", content: null);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 204 (xoá xong,
        // không có body);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var after = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Đỏ khi giỏ còn dòng
        // hàng.
        Assert.Empty(after!.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng tổng tiền 0 (hậu
        // tố m = kiểu decimal); đỏ khi còn số dư.
        Assert.Equal(0m, after.Total);

        // The same basket, now empty — not a new one. A fresh identifier each checkout would mean
        // the basket row is being deleted and recreated, which is not what FR-010 asks for.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Id giỏ trước và sau phải
        // bằng nhau: giỏ được làm rỗng chứ không bị tạo giỏ mới. Đỏ khi id đổi.
        Assert.Equal(before!.Id, after.Id);
    }

    /// <summary>
    /// Kiểm tra: xoá giỏ đang rỗng trả 409 Conflict.
    /// Lý do: FR-008/FR-016: phải báo rõ thay vì lặng lẽ thành công, để checkout giỏ đã rỗng không
    /// thể đi tiếp để tạo đơn thứ hai.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T051, US3 (FR-008, FR-016).
    /// </summary>
    [Fact]
    public async Task Clear_ReturnsConflict_WhenTheBasketIsAlreadyEmpty()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-empty");
        var client = CreateClient(factory);

        var response = await client.PostAsync("/baskets/current/clear", content: null);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Xoá giỏ đã rỗng bị coi
        // là xung đột, không phải thành công.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: xoá giỏ lần thứ hai liên tiếp cũng trả 409.
    /// Lý do: đây là thứ khiến 1 lần checkout lặp thất bại to tiếng thay vì lặng lẽ xoá rỗng rồi
    /// chạy tiếp — chốt chặn thứ hai của FR-016.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T051, US3 (FR-016, SC-008).
    /// </summary>
    [Fact]
    public async Task Clear_ReturnsConflict_OnASecondClear()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-twice");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 1, unitPrice = 12.50m });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Lần đầu phải là 204.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await client.PostAsync("/baskets/current/clear", content: null)).StatusCode);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Lần 2 phải là 409; đỏ
        // khi lần 2 vẫn 204 (không phát hiện thao tác lặp, dẫn tới tạo 2 đơn khi checkout bấm đúp).
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await client.PostAsync("/baskets/current/clear", content: null)).StatusCode);
    }

    /// <summary>
    /// Kiểm tra: sau khi xoá, người mua thêm hàng lại vào đúng giỏ cũ được.
    /// Lý do: thanh toán kết thúc 1 lần mua, không kết thúc quan hệ giữa người mua và giỏ của họ.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T051, US3 (FR-010).
    /// </summary>
    [Fact]
    public async Task Clear_LeavesTheBasketUsable_ForTheNextPurchase()
    {
        await using var factory = await CreateFactoryAsync("basket-clear-reuse");
        var client = CreateClient(factory);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 1, unitPrice = 12.50m });
        await client.PostAsync("/baskets/current/clear", content: null);

        await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 3, unitPrice = 12.50m });

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Single: giỏ có đúng 1
        // dòng; Equal: số lượng là 3 (không cộng dồn với lần mua trước).
        Assert.Equal(3, Assert.Single(basket!.Items).Quantity);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. 3 × 12,50 = 37,50; đỏ
        // khi tổng sai.
        Assert.Equal(37.50m, basket.Total);
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
                    ["ConnectionStrings:BasketsDb"] = connectionString,
                }));
            host.UseTestJwtBearer();
        });

        using var scope = factory.Services.CreateScope();
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = TenantId;
        await scope.ServiceProvider.GetRequiredService<BasketsDbContext>().Database.MigrateAsync();

        return factory;
    }

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, Shopper);

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(
        Guid ProductId,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
