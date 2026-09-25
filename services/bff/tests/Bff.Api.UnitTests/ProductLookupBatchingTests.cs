using System.Net;
using System.Net.Http.Json;
using Bff.Api.DownstreamClients;
using Bff.Api.Features.Baskets;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// Spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) FR-002/FR-003, User Story 2; Jira SCRUM-33
/// Test Scenario 2 ("tải 1 giỏ nhiều món — xác nhận 1 truy vấn, không phải 1 truy vấn cho mỗi món").
/// Trước tính năng này, <see cref="BasketsEndpoints.ToResponseAsync"/> gọi
/// <c>ProductsApiClient.GetProductsAsync()</c> — TOÀN BỘ catalog — mỗi lần render, bất kể giỏ lớn
/// hay nhỏ. Bộ test này khoá bản sửa: đúng 1 lời gọi có giới hạn cho mọi cỡ giỏ, và không lời gọi nào
/// cho giỏ rỗng.
/// </summary>
/// <remarks>
/// Gọi trực tiếp <see cref="BasketsEndpoints.ToResponseAsync"/> (không host HTTP, không baskets
/// service) với 1 <see cref="ProductsApiClient"/> thật có primary handler là 1 handler giả biết đếm —
/// cùng kỹ thuật với <c>RetryMethodPolicyTests</c> và <c>ProductsEndpointPaginationTests</c>.
/// Lưu ý về phạm vi: test chỉ ĐẾM số lần gọi, không kiểm tra request có mang `ids=` hay không — nên
/// 1 hồi quy về "1 lời gọi lấy 1 trang catalog" (đo thật: đổi thành `GetProductsAsync(1, 100, …)`) vẫn
/// qua toàn bộ 22 test BFF + 8 test scanner; xem QA_Debt mục 023.
/// </remarks>
public class ProductLookupBatchingTests
{
    /// <summary>
    /// Kiểm tra: render 1 giỏ có 5 dòng thuộc 5 sản phẩm khác nhau → `ProductsApiClient` bị gọi ĐÚNG 1
    /// lần.
    /// Lý do: US2/Jira Test Scenario 2 — số lời gọi tới Products không được tăng theo số dòng (không
    /// N+1 xuyên service).
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002/FR-003, US2.
    /// </summary>
    [Fact]
    public async Task RenderingBasket_WithMultipleDistinctProducts_CallsProductsClientExactlyOnce()
    {
        var handler = new CountingHandler(new PagedProductsPayload(
            [
                new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m),
                new ProductResource(Guid.NewGuid(), "Cafetiere", 34.99m),
                new ProductResource(Guid.NewGuid(), "Field Notes Notebook", 4.50m),
            ],
            Page: 1,
            PageSize: 3,
            TotalCount: 3));
        var productsClient = BuildProductsClient(handler);
        var basket = BasketWithDistinctLines(count: 5);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 1 lần gọi; nhiều hơn
        // nghĩa là quay lại kiểu 1 lời gọi/dòng.
        Assert.Equal(1, handler.InvocationCount);
    }

    /// <summary>
    /// Kiểm tra: 2 dòng giỏ cùng trỏ tới 1 sản phẩm → vẫn chỉ 1 lời gọi (tra theo tập id RIÊNG BIỆT).
    /// Lý do: 2 dòng có thể cùng 1 sản phẩm (vd tăng số lượng bằng 1 lệnh add-item riêng trước khi
    /// baskets service gộp) — tra cứu vẫn phải theo tập id khác nhau, không phải 1 lời gọi cho mỗi dòng.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002, US2.
    /// </summary>
    [Fact]
    public async Task RenderingBasket_WithDuplicateProductAcrossLines_StillCallsProductsClientExactlyOnce()
    {
        var sharedProductId = Guid.NewGuid();
        var handler = new CountingHandler(new PagedProductsPayload(
            [new ProductResource(sharedProductId, "Ceramic mug", 12.50m)],
            Page: 1,
            PageSize: 1,
            TotalCount: 1));
        var productsClient = BuildProductsClient(handler);

        var basket = new BasketResource(
            Guid.NewGuid(),
            "shopper-1",
            [
                new BasketLineItemResource(sharedProductId, Quantity: 1, UnitPrice: 12.50m, LineTotal: 12.50m),
                new BasketLineItemResource(sharedProductId, Quantity: 2, UnitPrice: 12.50m, LineTotal: 25.00m),
            ],
            Total: 37.50m);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 1 lần gọi.
        Assert.Equal(1, handler.InvocationCount);
    }

    /// <summary>
    /// Kiểm tra: render giỏ RỖNG → không gọi `ProductsApiClient` lần nào.
    /// Lý do phải test: hành vi đã đúng từ trước tính năng này (return sớm khi giỏ rỗng) — giữ làm
    /// regression guard cạnh 2 bản sửa ở trên, không phải 1 bản sửa mới.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002, Edge Case (giỏ rỗng).
    /// </summary>
    [Fact]
    public async Task RenderingEmptyBasket_DoesNotCallProductsClient()
    {
        var handler = new CountingHandler(new PagedProductsPayload([], 1, 0, 0));
        var productsClient = BuildProductsClient(handler);
        var basket = new BasketResource(Guid.NewGuid(), "shopper-1", [], Total: 0m);

        await BasketsEndpoints.ToResponseAsync(basket, productsClient, CancellationToken.None);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — 0 lần gọi.
        Assert.Equal(0, handler.InvocationCount);
    }

    private static BasketResource BasketWithDistinctLines(int count) =>
        new(
            Guid.NewGuid(),
            "shopper-1",
            [.. Enumerable.Range(1, count).Select(_ => new BasketLineItemResource(
                Guid.NewGuid(),
                Quantity: 1,
                UnitPrice: 9.99m,
                LineTotal: 9.99m))],
            Total: count * 9.99m);

    private static ProductsApiClient BuildProductsClient(HttpMessageHandler primaryHandler)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Services:ProductsApi:BaseUrl"] = "http://products.test",
                ["Services:BasketsApi:BaseUrl"] = "http://baskets.test",
                ["Services:OrdersApi:BaseUrl"] = "http://orders.test",
                ["Services:PartiesApi:BaseUrl"] = "http://parties.test",
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDownstreamClients(configuration);

        services.AddHttpClient(ProductsApiClient.ServiceName)
            .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);

        return services.BuildServiceProvider().GetRequiredService<ProductsApiClient>();
    }

    private sealed record PagedProductsPayload(
        IReadOnlyList<ProductResource> Items, int Page, int PageSize, int TotalCount);

    /// <summary>Counts invocations and replies with a fixed, valid payload — no assertions on the request itself; <c>ProductsEndpointPaginationTests</c> already covers the request shape.</summary>
    private sealed class CountingHandler(PagedProductsPayload payload) : HttpMessageHandler
    {
        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            };

            return Task.FromResult(response);
        }
    }
}
