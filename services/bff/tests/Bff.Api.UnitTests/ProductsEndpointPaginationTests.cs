using System.Net;
using System.Net.Http.Json;
using Bff.Api.DownstreamClients;
using Bff.Api.Features.Products;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bff.Api.UnitTests;

/// <summary>
/// Spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) FR-001, User Story 1: <c>GET /bff/products</c>
/// phải chuyển tiếp <c>page</c>/<c>pageSize</c> của caller sang products service thay vì âm thầm đưa
/// việc lấy không giới hạn quay lại ở tầng BFF, và metadata trang từ downstream phải sống sót nguyên
/// vẹn vào <see cref="ProductsEndpoints.ProductListResponse"/> (FR-007 — chỉ thêm, không đổi hợp đồng).
/// </summary>
/// <remarks>
/// Hai nửa, mô phỏng <c>RetryMethodPolicyTests</c> (chuyển tiếp qua client, đăng ký DI thật, bắt lấy
/// request) và <c>ResponseMappingTests</c> (hàm định hình thuần, không HTTP) — mỗi nửa độc lập là
/// test đơn giản nhất có thể fail vì đúng lý do của nó.
/// </remarks>
public class ProductsEndpointPaginationTests
{
    /// <summary>
    /// Kiểm tra: `GetProductsAsync(page: 3, pageSize: 50)` gửi đúng `page=3&pageSize=50` trong query
    /// string tới Products.
    /// Lý do: FR-001 — BFF chỉ chuyển tiếp những gì caller đòi; Products (không phải BFF) là nguồn sự
    /// thật duy nhất của kích thước mặc định và trần.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001, US1.
    /// </summary>
    [Fact]
    public async Task GetProductsAsync_ForwardsPageAndPageSize_AsQueryParameters()
    {
        var handler = new CapturingHandler(new PagedProductsPayload([], 3, 50, 0));
        var productsClient = BuildProductsClient(handler);

        await productsClient.GetProductsAsync(page: 3, pageSize: 50, CancellationToken.None);

        // Assert.NotNull(giá trị): xanh khi handler đã bắt được 1 request, đỏ khi không có request nào.
        Assert.NotNull(handler.LastRequestUri);
        var query = System.Web.HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("3", query["page"]);
        Assert.Equal("50", query["pageSize"]);
    }

    /// <summary>
    /// Kiểm tra: `GetProductsByIdsAsync([id1, id2])` gửi `ids=id1,id2` (phân tách dấu phẩy) và KHÔNG
    /// kèm `page` — không phải lời gọi lấy cả catalog.
    /// Lý do: research.md Decision 4 — tra cứu theo `ids` bị chặn bởi chính tập id, không phải bởi 1
    /// trang.
    /// Lưu ý: test này chứng minh hình dạng request CỦA CLIENT — không chứng minh `BasketsEndpoints`
    /// thật sự dùng phương thức này khi render giỏ (xem QA_Debt mục 023).
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002, US2.
    /// </summary>
    [Fact]
    public async Task GetProductsByIdsAsync_SendsIdsAsCommaSeparatedQueryParameter_NotFullCatalogFetch()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var handler = new CapturingHandler(new PagedProductsPayload([], 1, 2, 2));
        var productsClient = BuildProductsClient(handler);

        await productsClient.GetProductsByIdsAsync([id1, id2], CancellationToken.None);

        // Assert.NotNull(giá trị): xanh khi đã bắt được 1 request.
        Assert.NotNull(handler.LastRequestUri);
        var query = System.Web.HttpUtility.ParseQueryString(handler.LastRequestUri!.Query);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal($"{id1},{id2}", query["ids"]);
        // Không có page/pageSize nào — tra cứu `ids` bị chặn bởi chính tập id (research.md Decision 4),
        // không phải bởi 1 trang.
        // Assert.Null(giá trị): xanh khi null, đỏ khi có `page`.
        Assert.Null(query["page"]);
    }

    /// <summary>
    /// Kiểm tra: `GetProductsByIdsAsync([])` (danh sách id rỗng) trả tập rỗng và KHÔNG gọi downstream
    /// lần nào.
    /// Lý do: 1 giỏ không có dòng nào không được chạm tới products service (Edge Case giỏ rỗng).
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002, Edge Case.
    /// </summary>
    [Fact]
    public async Task GetProductsByIdsAsync_WithNoIds_DoesNotCallDownstreamAtAll()
    {
        var handler = new CapturingHandler(new PagedProductsPayload([], 1, 0, 0));
        var productsClient = BuildProductsClient(handler);

        var result = await productsClient.GetProductsByIdsAsync([], CancellationToken.None);

        // Assert.Empty(tập hợp): xanh khi rỗng, đỏ khi có phần tử.
        Assert.Empty(result);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau — 0 lần gọi downstream.
        Assert.Equal(0, handler.InvocationCount);
    }

    /// <summary>
    /// Kiểm tra: hàm định hình thuần `ToListResponse` giữ nguyên `Page`/`PageSize`/`TotalCount` từ
    /// downstream cạnh các mục đã định hình. Không có HTTP — mô phỏng
    /// <c>ResponseMappingTests.ProductSummary_CarriesEveryFieldFromTheDownstreamProduct</c>.
    /// Lý do: FR-007 — thay đổi hợp đồng chỉ được thêm phần phân trang, và phần thêm đó không được rơi
    /// rụng giữa Products và SPA.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-007, US1.
    /// </summary>
    [Fact]
    public void ToListResponse_MapsDownstreamPageMetadata_AlongsideShapedItems()
    {
        var page = new ProductPageResource(
            [new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m)],
            Page: 2,
            PageSize: 20,
            TotalCount: 45);

        var response = ProductsEndpoints.ToListResponse(page);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử, đỏ khi 0 hoặc nhiều hơn.
        Assert.Single(response.Items);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(2, response.Page);
        Assert.Equal(20, response.PageSize);
        Assert.Equal(45, response.TotalCount);
    }

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

    /// <summary>Records the request it was asked to send and replies with a fixed, valid payload.</summary>
    private sealed class CapturingHandler(PagedProductsPayload payload) : HttpMessageHandler
    {
        public Uri? LastRequestUri { get; private set; }

        public int InvocationCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            LastRequestUri = request.RequestUri;

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(payload),
            };

            return Task.FromResult(response);
        }
    }
}
