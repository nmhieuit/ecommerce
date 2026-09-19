extern alias BasketsApi;
extern alias OrdersApi;
extern alias ProductsApi;

using System.Net;
using System.Net.Http.Json;
using BasketsApi::Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using OrdersApi::Orders.Api.Data;
using ProductsApi::Products.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec US3: the shopper turns their basket into an order and is shown a
/// confirmation naming it. The basket is emptied; an empty basket cannot be checked out.
/// </summary>
/// <remarks>
/// Three real services behind one BFF, each on its own database — the only arrangement that can
/// actually exercise research.md Decision 9's two-step, because the interesting behaviour is the
/// ordering between them.
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class CheckoutTests(DownstreamServicesFixture fixture)
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    /// <summary>
    /// Kiểm tra: checkout tạo 1 đơn có các dòng và tổng khớp với nội dung giỏ.
    /// Lý do phải test: nhánh happy-case của US3 (FR-007, FR-022): giỏ được chuyển thành đơn thật.
    /// Dùng 3 service thật sau 1 BFF, mỗi service 1 database, vì hành vi đáng kiểm là thứ tự giữa
    /// các bước (research.md Decision 9).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-007, FR-022).
    /// </summary>
    [Fact]
    public async Task Checkout_CreatesAnOrder_ForWhatIsInTheBasket()
    {
        await using var services = await StartServicesAsync("checkout-happy");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 2);
        await AddAsync(client, Apron, quantity: 1);

        var response = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var confirmation = await response.Content.ReadFromJsonAsync<OrderConfirmationResponse>();
        Assert.NotNull(confirmation);
        Assert.NotEqual(Guid.Empty, confirmation.Id);

        // quickstart.md Scenario 5's figure, arrived at through three services.
        Assert.Equal(59.25m, confirmation.Total);
    }

    /// <summary>
    /// Kiểm tra: mã tham chiếu do checkout trả về đọc lại qua route đơn hàng của BFF ra đúng đơn
    /// đó.
    /// Lý do phải test: SC-005: mã trên màn hình xác nhận phải khớp đơn thật trong backend — đọc
    /// lại qua chính route của BFF, giống cách quickstart.md làm bằng curl.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-009, SC-005).
    /// </summary>
    [Fact]
    public async Task Checkout_ReturnsAReference_ThatReadsBackAsTheSameOrder()
    {
        await using var services = await StartServicesAsync("checkout-readback");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);

        var confirmation = await (await client.PostAsync("/bff/checkout", content: null))
            .Content.ReadFromJsonAsync<OrderConfirmationResponse>();

        var readBack = await client.GetFromJsonAsync<OrderConfirmationResponse>(
            $"/bff/orders/{confirmation!.Id}");

        Assert.Equal(confirmation.Id, readBack!.Id);
        Assert.Equal(confirmation.Total, readBack.Total);
    }

    /// <summary>
    /// Kiểm tra: sau khi checkout thành công giỏ của người mua rỗng.
    /// Lý do phải test: FR-010: giỏ đã thanh toán không được còn hàng; đồng thời là chốt chặn để
    /// lần checkout lặp thất bại (FR-016).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-010).
    /// </summary>
    [Fact]
    public async Task Checkout_EmptiesTheBasket()
    {
        await using var services = await StartServicesAsync("checkout-empties");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);
        await client.PostAsync("/bff/checkout", content: null);

        var basket = await client.GetFromJsonAsync<BasketResponse>("/bff/basket");

        Assert.Empty(basket!.Items);
        Assert.Equal(0m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: checkout khi giỏ rỗng trả 409 Conflict và không tạo đơn.
    /// Lý do phải test: FR-008: giỏ rỗng không có gì để đặt; storefront đã chặn trước khi gửi, còn
    /// đây là server từ chối thêm lần nữa (validate phía client chỉ là UX).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-008).
    /// </summary>
    [Fact]
    public async Task Checkout_ReturnsConflict_WhenTheBasketIsEmpty()
    {
        await using var services = await StartServicesAsync("checkout-empty-basket");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        var response = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: checkout 2 lần liên tiếp chỉ tạo đúng 1 đơn; lần thứ hai bị từ chối.
    /// Lý do phải test: FR-016/SC-008: lần 2 thấy giỏ đã rỗng và bị chặn — nên việc làm rỗng giỏ
    /// của FR-010 là chốt chặn thực sự chứ không chỉ để gọn gàng.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-016, SC-008).
    /// </summary>
    [Fact]
    public async Task Checkout_CreatesExactlyOneOrder_WhenAttemptedTwice()
    {
        await using var services = await StartServicesAsync("checkout-twice");
        var client = BffTestHost.CreateShopperClient(services.Bff);

        await AddAsync(client, Notebook, quantity: 1);

        var first = await client.PostAsync("/bff/checkout", content: null);
        var second = await client.PostAsync("/bff/checkout", content: null);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: hai người mua, hai giỏ, hai đơn — không ai đặt nhầm hàng của người kia.
    /// Lý do phải test: với 1 danh tính stub ở Phase 1, đây là assertion ngăn việc gắn giỏ theo
    /// người gọi âm thầm bị hồi quy.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T052, US3 (FR-006).
    /// </summary>
    [Fact]
    public async Task Checkout_OrdersOnlyTheCallersOwnBasket()
    {
        await using var services = await StartServicesAsync("checkout-per-shopper");

        var mine = BffTestHost.CreateShopperClient(services.Bff);
        var theirs = BffTestHost.CreateShopperClient(services.Bff, "someone-else");

        await AddAsync(mine, Notebook, quantity: 1);
        await AddAsync(theirs, Apron, quantity: 1);

        var myOrder = await (await mine.PostAsync("/bff/checkout", content: null))
            .Content.ReadFromJsonAsync<OrderConfirmationResponse>();

        Assert.Equal(12.50m, myOrder!.Total);

        // Their basket is untouched by my checkout.
        var theirBasket = await theirs.GetFromJsonAsync<BasketResponse>("/bff/basket");
        Assert.Single(theirBasket!.Items);
    }

    private static async Task AddAsync(HttpClient client, Guid productId, int quantity)
    {
        var response = await client.PostAsJsonAsync("/bff/basket/items", new { productId, quantity });

        Assert.True(
            response.IsSuccessStatusCode,
            $"Adding to the basket failed with {(int)response.StatusCode}: "
            + await response.Content.ReadAsStringAsync());
    }

    private async Task<CheckoutServices> StartServicesAsync(string prefix)
    {
        var products = await BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor($"{prefix}-products"),
            _ => Task.CompletedTask);

        var baskets = await BffTestHost.CreateDownstreamAsync<BasketsApi::Program, BasketsDbContext>(
            "BasketsDb",
            fixture.ConnectionStringFor($"{prefix}-baskets"),
            _ => Task.CompletedTask);

        var orders = await BffTestHost.CreateDownstreamAsync<OrdersApi::Program, OrdersDbContext>(
            "OrdersDb",
            fixture.ConnectionStringFor($"{prefix}-orders"),
            _ => Task.CompletedTask);

        var bff = BffTestHost.CreateBff(new Dictionary<string, Func<HttpMessageHandler>>
        {
            ["ProductsApi"] = products.Server.CreateHandler,
            ["BasketsApi"] = baskets.Server.CreateHandler,
            ["OrdersApi"] = orders.Server.CreateHandler,
        });

        return new CheckoutServices(products, baskets, orders, bff);
    }

    private sealed record CheckoutServices(
        WebApplicationFactory<ProductsApi::Program> Products,
        WebApplicationFactory<BasketsApi::Program> Baskets,
        WebApplicationFactory<OrdersApi::Program> Orders,
        WebApplicationFactory<Program> Bff) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Bff.DisposeAsync();
            await Orders.DisposeAsync();
            await Baskets.DisposeAsync();
            await Products.DisposeAsync();
        }
    }

    private sealed record OrderConfirmationResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);

    private sealed record BasketResponse(
        Guid Id,
        string CustomerRef,
        IReadOnlyList<BasketItemResponse> Items,
        decimal Total);

    private sealed record BasketItemResponse(
        Guid ProductId,
        string Name,
        int Quantity,
        decimal UnitPrice,
        decimal LineTotal);
}
