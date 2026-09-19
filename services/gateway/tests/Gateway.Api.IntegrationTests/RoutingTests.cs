using System.Net;
using System.Net.Http.Json;
using IntegrationTestSupport;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// US2 Acceptance Scenario 1 / spec FR-001: a request arriving at the gateway reaches the correct
/// downstream destination without the caller having specified anything about internal topology.
/// </summary>
/// <remarks>
/// Every request below is issued against the gateway's own client and names only a path. No test
/// here knows the BFF's address, port, or existence — which is the property US2 exists to
/// establish, so it holds by construction rather than by assertion.
/// </remarks>
public class RoutingTests
{
    /// <summary>
    /// Kiểm tra: gọi `/openapi/v1.json` qua gateway trả về `200` và đúng là tài liệu OpenAPI của BFF
    /// (có path `/bff/products`).
    /// Lý do phải test: đây là bằng chứng mạnh nhất cho việc request thật sự "tới nơi" — tài liệu này
    /// chỉ BFF sinh ra được, gateway không tự tạo ra nó, và test không cần bật service nghiệp vụ nào
    /// cả để chạy.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T049, US2.
    /// </summary>
    [Fact]
    public async Task ARequestToTheGateway_ReachesAResponseOnlyTheBffCanProduce()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<OpenApiDocument>();
        Assert.NotNull(document);
        Assert.Contains("/bff/products", document.Paths.Keys);
    }

    /// <summary>
    /// Kiểm tra: gọi `/bff/products` (route thật, không phải tài liệu) qua gateway KHÔNG trả về
    /// `404`.
    /// Lý do phải test: route hướng người dùng cũng phải được chuyển tiếp, không chỉ tài liệu OpenAPI
    /// ở test trên. Products service không chạy trong test này nên downstream call của BFF sẽ lỗi —
    /// nhưng việc chạm được tới handler của BFF (dù nó lỗi) mới là điều US2 khẳng định; nếu gateway
    /// chưa từng chuyển tiếp, kết quả sẽ là `404`.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T049, US2.
    /// </summary>
    [Fact]
    public async Task AClientFacingRoute_IsForwardedToTheBffsHandler()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/bff/products");

        Assert.NotEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: `/health/live` và `/health/ready` gọi qua gateway trả về `200` — tức route catch-all
    /// không "nuốt" mất 2 health probe của chính gateway.
    /// Lý do phải test: Kubernetes gọi thẳng 2 probe này vào chính gateway. Nếu chúng bị route
    /// catch-all chuyển tiếp xuống BFF, gateway sẽ báo cáo sức khoẻ của BFF như thể là sức khoẻ của
    /// chính nó — 1 lần BFF gặp sự cố sẽ khiến Kubernetes restart toàn bộ pod gateway. ASP.NET Core
    /// vốn ưu tiên route cụ thể hơn catch-all nên hiện tại việc này tự đúng, nhưng không có test này
    /// thì 1 thay đổi route-table sau này có thể âm thầm phá vỡ nó (ghi trong "Phase 5 implementation
    /// notes" của tasks.md, không có mã task T riêng).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — US2 (không có mã T riêng, xem Phase 5 notes).
    /// </summary>
    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task TheGatewaysOwnHealthProbes_AreServedLocally_NotForwarded(string probe)
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        var response = await client.GetAsync(probe);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private sealed record OpenApiDocument(Dictionary<string, object> Paths);
}
