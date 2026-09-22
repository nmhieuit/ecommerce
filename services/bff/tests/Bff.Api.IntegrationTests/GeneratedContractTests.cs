using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Constitution Principle II and ADR-0004: the generated OpenAPI document is the authoritative
/// contract, and the frontend's API client is generated from it rather than hand-written.
/// </summary>
/// <remarks>
/// That makes an incomplete document a real defect, not a documentation nit. Minimal APIs infer
/// only the success response, so without explicit metadata the document would advertise 200 as the
/// sole outcome of every route — and SCRUM-14's generated client would have no typed notion of
/// "basket not found" or "products service unavailable", despite the BFF returning both.
/// These assertions pin the document against the hand-authored reference in
/// <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c>.
/// </remarks>
public class GeneratedContractTests
{
    /// <summary>
    /// Kiểm tra: tài liệu `/openapi/v1.json` (chỉ công bố ở Development) có đủ các path
    /// `/bff/products`, `/bff/baskets/{basketId}`, `/bff/orders/{orderId}`,
    /// `/bff/parties/{partyId}`.
    /// Lý do: Constitution Principle II + ADR-0004: tài liệu OpenAPI sinh tự động là hợp đồng có
    /// thẩm quyền và client SPA được sinh từ nó; thiếu 1 route trong tài liệu nghĩa là client không
    /// có hàm gọi route đó (FR-001/SC-001 của spec 007). Lưu ý test chỉ ghim 4 route này, chưa ghim
    /// `/bff/basket`, `/bff/basket/items`, `/bff/checkout` thêm ở spec 004.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T062 (tìm ra khoảng hở tài liệu OpenAPI chỉ
    /// khai `200`), US3; là bằng chứng cho spec 007 SC-001.
    /// </summary>
    [Fact]
    public async Task TheDocument_DescribesEveryClientFacingRoute()
    {
        var paths = (await GetDocumentAsync()).GetProperty("paths");

        foreach (var route in new[]
                 { "/bff/products", "/bff/baskets/{basketId}", "/bff/orders/{orderId}", "/bff/parties/{partyId}" })
        {
            // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đỏ (kèm tên route bị
            // thiếu) khi tài liệu không mô tả route.
            Assert.True(paths.TryGetProperty(route, out _), $"The document omits '{route}'.");
        }
    }

    /// <summary>
    /// Kiểm tra: mỗi route GET khai đủ `200`, `502` và `504` trong tài liệu.
    /// Lý do: Minimal API chỉ tự suy ra phản hồi thành công nên nếu không khai tường minh thì tài
    /// liệu chỉ có `200` và client sinh ra không có kiểu cho "downstream không sẵn sàng", dù BFF
    /// thật sự trả `502`/`504` (US3 của spec 002).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T062 (tìm ra khoảng hở tài liệu OpenAPI chỉ
    /// khai `200`), US3; là bằng chứng cho spec 007 SC-001.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/bff/baskets/{basketId}")]
    [InlineData("/bff/orders/{orderId}")]
    [InlineData("/bff/parties/{partyId}")]
    public async Task EveryRoute_DeclaresItsDownstreamFailureResponses(string route)
    {
        var responses = await GetResponseCodesAsync(route);

        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không.
        Assert.Contains("200", responses);
        Assert.Contains("502", responses);
        Assert.Contains("504", responses);
    }

    /// <summary>
    /// Kiểm tra: các route theo id (`baskets`, `orders`, `parties`) khai `404` trong tài liệu.
    /// Lý do: `404` cho id không tồn tại là 1 kết quả bình thường, riêng biệt mà client phải rẽ
    /// nhánh được chứ không coi là thất bại chung.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T062 (tìm ra khoảng hở tài liệu OpenAPI chỉ
    /// khai `200`), US3; là bằng chứng cho spec 007 SC-001.
    /// </summary>
    [Theory]
    [InlineData("/bff/baskets/{basketId}")]
    [InlineData("/bff/orders/{orderId}")]
    [InlineData("/bff/parties/{partyId}")]
    public async Task TheByIdRoutes_DeclareNotFound(string route)
    {
        // Assert.Contains(phần tử, tập hợp): xanh khi tập hợp có chứa phần tử, đỏ khi không. Đạt
        // khi có khai báo 404;
        Assert.Contains("404", await GetResponseCodesAsync(route));
    }

    /// <summary>
    /// Kiểm tra: schema `ProductListResponse` có `items` và `ProductSummary` có đủ `id`, `name`,
    /// `price`, khớp hợp đồng viết tay `specs/002-gateway-bff-routing/contracts/bff-openapi.yaml`.
    /// Lý do: danh sách sản phẩm là hình dạng duy nhất mà hợp đồng đặc tả đầy đủ (FR-004 của spec
    /// 002) nên được kiểm từng trường; đây là điểm neo để client sinh ra không lệch khỏi hợp đồng.
    /// Lưu ý: test chỉ kiểm tên trường, không kiểm phân trang (`page`/`pageSize`/`totalCount`) mà
    /// spec 023 thêm sau — xem QA_Debt mục 007.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T062 (tìm ra khoảng hở tài liệu OpenAPI chỉ
    /// khai `200`), US3; là bằng chứng cho spec 007 SC-001.
    /// </summary>
    [Fact]
    public async Task TheProductListingSchema_MatchesTheHandAuthoredContract()
    {
        var schemas = (await GetDocumentAsync()).GetProperty("components").GetProperty("schemas");

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Có schema danh sách.
        Assert.True(schemas.TryGetProperty("ProductListResponse", out var listing));
        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Danh sách có thuộc tính
        // items.
        Assert.True(listing.GetProperty("properties").TryGetProperty("items", out _));

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Có schema sản phẩm.
        Assert.True(schemas.TryGetProperty("ProductSummary", out var summary));
        var properties = summary.GetProperty("properties");
        foreach (var field in new[] { "id", "name", "price" })
        {
            // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Schema sản phẩm phải có
            // trường đó; đỏ kèm tên trường thiếu.
            Assert.True(properties.TryGetProperty(field, out _), $"ProductSummary omits '{field}'.");
        }
    }

    private static async Task<IReadOnlyList<string>> GetResponseCodesAsync(string route)
    {
        var operation = (await GetDocumentAsync())
            .GetProperty("paths")
            .GetProperty(route)
            .GetProperty("get");

        return [.. operation.GetProperty("responses").EnumerateObject().Select(response => response.Name)];
    }

    /// <summary>
    /// The document is published in Development only (T013), which is also where the codegen
    /// pipeline reads it from.
    /// </summary>
    private static async Task<JsonElement> GetDocumentAsync()
    {
        await using var bff = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));

        return await bff.CreateClient().GetFromJsonAsync<JsonElement>("/openapi/v1.json");
    }
}
