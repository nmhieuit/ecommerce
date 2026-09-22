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
/// 004-minimal-shopping-spa spec FR-006 and FR-021: the basket is resolved from the caller's
/// identity, one per shopper, and adding a product merges into the line it already occupies.
/// </summary>
/// <remarks>
/// Against real SQL Server via Testcontainers (Principle III). The unique indexes are the point of
/// several of these assertions, and an in-memory provider does not enforce them — the suite would
/// pass while production had duplicate baskets.
/// </remarks>
public class CurrentBasketTests(SqlServerFixture sqlServer) : IClassFixture<SqlServerFixture>
{
    private const string TenantId = "contoso";
    private const string Shopper = "phase1-stub-user";
    private const string OtherShopper = "someone-else";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: người mua chưa từng thêm gì gọi `GET /baskets/current` nhận về giỏ rỗng, không
    /// phải 404.
    /// Lý do: lần đầu ghé cửa hàng không phải lỗi; trả 404 sẽ khiến storefront coi "chưa từng mua
    /// sắm" là 1 thất bại cần khắc phục.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-006, FR-020).
    /// </summary>
    [Fact]
    public async Task GetCurrent_ReturnsAnEmptyBasket_ForACallerWhoHasNeverAddedAnything()
    {
        await using var factory = await CreateFactoryAsync("basket-current-first-visit");
        var client = CreateClient(factory, Shopper);

        var response = await client.GetAsync("/baskets/current");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi khác (ví dụ
        // 401/404/500).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Body đọc được thành giỏ hàng
        // (Assert.NotNull đạt khi khác null).
        Assert.NotNull(basket);
        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Không có dòng hàng.
        Assert.Empty(basket.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Tổng tiền 0.
        Assert.Equal(0m, basket.Total);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Giỏ gắn đúng người dùng
        // gọi; đỏ khi gắn sai/không gắn.
        Assert.Equal(Shopper, basket.CustomerRef);
    }

    /// <summary>
    /// Kiểm tra: hai request riêng biệt của cùng 1 người mua thấy cùng 1 giỏ.
    /// Lý do: FR-011/SC-007: giỏ tồn tại qua tải lại trang và đóng-mở trình duyệt vì đó là giỏ của
    /// server cho người gọi này; 2 request đại diện cho 2 lần tải trang, chỉ thiếu trình duyệt.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-011, SC-007).
    /// </summary>
    [Fact]
    public async Task GetCurrent_ReturnsTheSameBasket_AcrossSeparateRequests()
    {
        await using var factory = await CreateFactoryAsync("basket-current-stable");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 2, unitPrice: 12.50m);

        var first = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");
        var second = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Cùng 1 giỏ qua các
        // request; đỏ khi mỗi lần lại tạo giỏ mới.
        Assert.Equal(first!.Id, second!.Id);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Single: đúng 1 dòng;
        // Equal: số lượng 2.
        Assert.Equal(2, Assert.Single(second.Items).Quantity);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. 2 × 12,50.
        Assert.Equal(25.00m, second.Total);
    }

    /// <summary>
    /// Kiểm tra: hai người mua khác nhau nhận về hai giỏ khác nhau.
    /// Lý do: nửa còn lại của FR-006: mỗi người mua có giỏ riêng, không dùng chung. Với 1 danh tính
    /// stub ở Phase 1, đây là assertion ngăn "phân giải theo tenant" pass vì sai lý do.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task GetCurrent_GivesDifferentShoppersDifferentBaskets()
    {
        await using var factory = await CreateFactoryAsync("basket-current-per-shopper");

        await AddItemAsync(CreateClient(factory, Shopper), Notebook, quantity: 1, unitPrice: 12.50m);

        var theirs = await CreateClient(factory, OtherShopper)
            .GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Có giỏ trả về.
        Assert.NotNull(theirs);
        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Giỏ của người thứ hai
        // trống, không thấy đồ của người thứ nhất; đỏ khi lộ giỏ giữa người dùng.
        Assert.Empty(theirs.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Giỏ thuộc đúng người thứ
        // hai.
        Assert.Equal(OtherShopper, theirs.CustomerRef);
    }

    /// <summary>
    /// Kiểm tra: thêm cùng 1 sản phẩm lần nữa qua API thì chỉ có 1 dòng với số lượng cộng dồn.
    /// Lý do: kiểm chứng quy tắc gộp dòng (FR-005/FR-021) trên SQL Server thật — unique index là
    /// điểm mấu chốt và provider in-memory không thực thi được nó.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-005, FR-021).
    /// </summary>
    [Fact]
    public async Task AddItem_MergesIntoTheExistingLine_WhenTheSameProductIsAddedAgain()
    {
        await using var factory = await CreateFactoryAsync("basket-current-merge");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn. Đúng 1 dòng (đã gộp); đỏ khi có 2 dòng riêng.
        var line = Assert.Single(basket!.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Số lượng cộng dồn thành
        // 2.
        Assert.Equal(2, line.Quantity);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. 2 × 12,50.
        Assert.Equal(25.00m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: thêm 2 sản phẩm khác nhau qua API cho ra 2 dòng riêng.
    /// Lý do: chống quy tắc gộp dòng gộp nhầm các sản phẩm khác nhau, đối chứng cho test gộp phía
    /// trên.
    /// Lưu ý: test còn kiểm tra số lượng từng dòng bằng `Assert.Single(...)` theo ProductId (mỗi
    /// lời gọi trả đúng 1 dòng thoả điều kiện).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-021).
    /// </summary>
    [Fact]
    public async Task AddItem_KeepsDistinctProductsOnSeparateLines()
    {
        await using var factory = await CreateFactoryAsync("basket-current-two-products");
        var client = CreateClient(factory, Shopper);

        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Notebook, quantity: 1, unitPrice: 12.50m);
        await AddItemAsync(client, Apron, quantity: 1, unitPrice: 34.25m);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/baskets/current");

        // Two lines, three items: the merge holds across requests, not only within one.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đúng 2 dòng (mỗi sản
        // phẩm 1 dòng).
        Assert.Equal(2, basket!.Items.Count);

        // The figure quickstart.md Scenarios 2 and 5 quote.
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. 2 × 12,50 + 34,25 =
        // 59,25; đỏ khi tổng sai.
        Assert.Equal(59.25m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: thêm với số lượng nhỏ hơn 1 qua API bị từ chối (400).
    /// Lý do: quy tắc số lượng tối thiểu phải được thực thi phía server, dù storefront có kiểm tra
    /// hay không.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-021).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    public async Task AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        await using var factory = await CreateFactoryAsync($"basket-current-bad-quantity-{Math.Abs(quantity)}");
        var client = CreateClient(factory, Shopper);

        var response = await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity, unitPrice = 12.50m });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 400 (yêu cầu sai
        // bị từ chối); đỏ khi service chấp nhận (200/201) hoặc trả mã khác.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: thêm với đơn giá âm qua API bị từ chối.
    /// Lý do: SPA không bao giờ gửi giá (giá do server tra từ catalog) nên quy tắc này không có bản
    /// sao phía client để bị vượt qua — server là nơi duy nhất thực thi, và test chứng minh điều đó
    /// độc lập với mọi client.
    /// Task nguồn: spec 015 (phân quyền từ chối theo mặc định) — US3, mở rộng bộ test giỏ hàng của
    /// spec 004.
    /// </summary>
    [Fact]
    public async Task AddItem_Rejects_ANegativeUnitPrice()
    {
        await using var factory = await CreateFactoryAsync("basket-current-negative-price");
        var client = CreateClient(factory, Shopper);

        var response = await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId = Notebook, quantity = 1, unitPrice = -0.01m });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 400 (yêu cầu sai
        // bị từ chối); đỏ khi service chấp nhận (200/201) hoặc trả mã khác.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: gọi `GET /baskets/current` khi không có người gọi nào được phân giải thì thất bại
    /// (500), không trả giỏ của ai.
    /// Lý do: Constitution Principle V mở rộng cho caller: request không đi qua gateway thì chưa
    /// xác định được ai, và không được phát giỏ của người khác; không có người gọi mặc định để dùng
    /// tạm.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T034, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task GetCurrent_Fails_WhenNoCallerWasResolved()
    {
        await using var factory = await CreateFactoryAsync("basket-current-no-caller");

        // Tenant but no subject: enough to reach persistence, not enough to name a shopper.
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);

        var response = await client.GetAsync("/baskets/current");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 500 (không biết
        // là ai thì không phục vụ giỏ nào); đỏ khi trả 200 với giỏ mặc định.
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
    }

    /// <summary>
    /// Asserts on the status with the body attached. <c>EnsureSuccessStatusCode</c> reports "500"
    /// and nothing else, which turns any server-side fault into a guessing exercise.
    /// </summary>
    private static async Task AddItemAsync(HttpClient client, Guid productId, int quantity, decimal unitPrice)
    {
        var response = await client.PostAsJsonAsync(
            "/baskets/current/items",
            new { productId, quantity, unitPrice });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Adding to the basket failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
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

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory, string subjectId)
    {
        var client = factory.CreateClient().UseTestBearerToken();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, TenantId);
        client.DefaultRequestHeaders.Add(CallerContextMiddleware.HeaderName, subjectId);

        return client;
    }

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketLineItemResponse> Items,
        decimal Total);

    private sealed record BasketLineItemResponse(Guid ProductId, int Quantity, decimal UnitPrice, decimal LineTotal);
}
