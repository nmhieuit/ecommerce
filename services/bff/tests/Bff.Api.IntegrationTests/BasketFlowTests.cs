extern alias BasketsApi;
extern alias ProductsApi;

using System.Net;
using System.Net.Http.Json;
using BasketsApi::Baskets.Api.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using ProductsApi::Products.Api.Data;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-003, FR-021 and contracts/bff-openapi.yaml: the client-facing
/// basket surface. The shopper names a product and a quantity; the BFF resolves the price from the
/// products service and writes to the baskets service.
/// </summary>
/// <remarks>
/// The price resolution is the reason this suite spans two downstreams. It is also the security
/// property worth pinning: a client-supplied price is a client-supplied discount, so the route must
/// ignore anything the caller says about money (004 research.md Decision 7).
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class BasketFlowTests(DownstreamServicesFixture fixture)
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private const decimal NotebookPrice = 12.50m;

    /// <summary>
    /// Kiểm tra: `GET /bff/basket` của người mua chưa thêm gì trả về giỏ rỗng.
    /// Lý do: lần đầu vào cửa hàng không phải lỗi; storefront cần 1 giỏ rỗng hợp lệ để hiện trạng
    /// thái trống (FR-004, FR-020).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (FR-004, FR-020).
    /// </summary>
    [Fact]
    public async Task GetBasket_ReturnsAnEmptyBasket_ForAShopperWhoHasAddedNothing()
    {
        await using var products = await CreateProductsAsync("bff-basket-empty");
        await using var baskets = await CreateBasketsAsync("bff-basket-empty");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).GetAsync("/bff/basket");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi khác (ví dụ
        // 401/404/500).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null. Body đọc được.
        Assert.NotNull(basket);
        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Không có dòng hàng.
        Assert.Empty(basket.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Tổng bằng 0.
        Assert.Equal(0m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: `POST /bff/basket/items` trả về giỏ có tên sản phẩm và đơn giá do BFF tra từ
    /// catalog.
    /// Lý do: FR-004: giỏ hiển thị tên chứ không chỉ số lượng và giá. Baskets chỉ lưu mã sản phẩm,
    /// nên tên chỉ có thể do BFF ghép từ catalog vào — đúng nhiệm vụ tổng hợp của BFF (spec 002
    /// FR-003).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (FR-004, FR-021).
    /// </summary>
    [Fact]
    public async Task AddItem_ReturnsTheBasket_WithTheProductsNameAndResolvedPrice()
    {
        await using var products = await CreateProductsAsync("bff-basket-add");
        await using var baskets = await CreateBasketsAsync("bff-basket-add");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        var response = await client.PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity = 1 });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var line = Assert.Single(basket!.Items);

        Assert.Equal(Notebook, line.ProductId);
        Assert.Equal("Field Notes Notebook", line.Name);
        Assert.Equal(NotebookPrice, line.UnitPrice);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(NotebookPrice, line.LineTotal);
        Assert.Equal(NotebookPrice, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: client gửi kèm 1 đơn giá tự khai thì BFF bỏ qua, giá ghi nhận là giá catalog.
    /// Lý do: đặc tính bảo mật cốt lõi: giá do client khai chính là giảm giá do client tự đặt.
    /// Thiếu test này thì chính client của storefront có thể tự đặt giá (research.md Decision 7).
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (research.md Decision 7).
    /// </summary>
    [Fact]
    public async Task AddItem_IgnoresAPriceSuppliedByTheClient()
    {
        await using var products = await CreateProductsAsync("bff-basket-price-injection");
        await using var baskets = await CreateBasketsAsync("bff-basket-price-injection");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        var response = await client.PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity = 1, unitPrice = 0.01m });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi khác (ví dụ
        // 401/404/500).
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var basket = await response.Content.ReadFromJsonAsync<BasketResponse>();
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Giá trong giỏ vẫn là giá
        // catalog 12,50, không phải giá client gửi; đỏ khi tin giá từ client (gian lận giá).
        Assert.Equal(NotebookPrice, Assert.Single(basket!.Items).UnitPrice);
    }

    /// <summary>
    /// Kiểm tra: thêm lại cùng 1 sản phẩm qua BFF thì giỏ có 1 dòng với số lượng cộng dồn.
    /// Lý do: quy tắc gộp dòng (FR-005) phải giữ nguyên qua cả chặng BFF, không chỉ ở service
    /// Baskets.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (FR-005, FR-021).
    /// </summary>
    [Fact]
    public async Task AddItem_MergesIntoTheExistingLine_WhenTheSameProductIsAddedAgain()
    {
        await using var products = await CreateProductsAsync("bff-basket-merge");
        await using var baskets = await CreateBasketsAsync("bff-basket-merge");
        await using var bff = CreateBff(products, baskets);
        var client = BffTestHost.CreateShopperClient(bff);

        await client.PostAsJsonAsync("/bff/basket/items", new { productId = Notebook, quantity = 1 });
        await client.PostAsJsonAsync("/bff/basket/items", new { productId = Notebook, quantity = 1 });

        var basket = await client.GetFromJsonAsync<BasketResponse>("/bff/basket");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Single: 1 dòng; Equal:
        // số lượng 2.
        Assert.Equal(2, Assert.Single(basket!.Items).Quantity);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. 2 × 12,50.
        Assert.Equal(25.00m, basket.Total);
    }

    /// <summary>
    /// Kiểm tra: thêm 1 sản phẩm không có trong catalog trả 404.
    /// Lý do: sản phẩm không tồn tại thì không có giá để tra nên không có gì để thêm. Trả 404 chứ
    /// không phải 502: downstream trả lời đúng, chỉ là request sai.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (FR-021).
    /// </summary>
    [Fact]
    public async Task AddItem_ReturnsNotFound_WhenNoSuchProductExists()
    {
        await using var products = await CreateProductsAsync("bff-basket-unknown-product");
        await using var baskets = await CreateBasketsAsync("bff-basket-unknown-product");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Guid.NewGuid(), quantity = 1 });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Đỏ khi 200 (thêm hàng
        // ma) hoặc 500.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: thêm với số lượng nhỏ hơn 1 (0, -2) bị từ chối.
    /// Lý do: validate hình dạng request ngay ở BFF (FR-005 của spec 002 cho phép); tránh gọi
    /// downstream với dữ liệu chắc chắn sai.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T035, US2 (FR-021).
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        await using var products = await CreateProductsAsync($"bff-basket-bad-qty-{Math.Abs(quantity)}");
        await using var baskets = await CreateBasketsAsync($"bff-basket-bad-qty-{Math.Abs(quantity)}");
        await using var bff = CreateBff(products, baskets);

        var response = await BffTestHost.CreateShopperClient(bff).PostAsJsonAsync(
            "/bff/basket/items",
            new { productId = Notebook, quantity });

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 400 (yêu cầu sai
        // bị từ chối); đỏ khi service chấp nhận (200/201) hoặc trả mã khác.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private Task<WebApplicationFactory<ProductsApi::Program>> CreateProductsAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor(database),
            // The migration seeds the catalog, so nothing more is needed — that is FR-018 working.
            _ => Task.CompletedTask);

    private Task<WebApplicationFactory<BasketsApi::Program>> CreateBasketsAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<BasketsApi::Program, BasketsDbContext>(
            "BasketsDb",
            fixture.ConnectionStringFor($"{database}-baskets"),
            _ => Task.CompletedTask);

    private static WebApplicationFactory<Program> CreateBff(
        WebApplicationFactory<ProductsApi::Program> products,
        WebApplicationFactory<BasketsApi::Program> baskets) =>
        BffTestHost.CreateBff(new Dictionary<string, Func<HttpMessageHandler>>
        {
            ["ProductsApi"] = products.Server.CreateHandler,
            ["BasketsApi"] = baskets.Server.CreateHandler,
        });

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
