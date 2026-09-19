using System.Diagnostics;
using System.Net;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// US3 from the gateway's side: when the BFF itself is unreachable, a caller gets a clear error
/// rather than a hang (spec FR-006).
/// </summary>
/// <remarks>
/// The BFF is not started here at all. The gateway's configured cluster address points at a port
/// with nothing listening, so YARP's own forwarding failure path runs — the same path a real BFF
/// outage would take.
/// </remarks>
public class DownstreamUnavailableTests
{
    /// <summary>SC-003's bound, applied to the gateway as well as the BFF.</summary>
    private static readonly TimeSpan ClearErrorBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Kiểm tra: khi BFF hoàn toàn không tới được (cluster trỏ vào cổng không ai lắng nghe), request
    /// qua gateway trả về `502 Bad Gateway` trong dưới 5 giây, không treo.
    /// Lý do phải test: FR-006 áp dụng ở phía gateway — chuỗi lỗi US3 không chỉ xảy ra khi 1 service
    /// nghiệp vụ chết, mà cả khi chính BFF (tầng ngay sau gateway) chết. YARP tự báo `502` khi đích
    /// không tới được; điều cần khẳng định là đây là lỗi rõ ràng có giới hạn thời gian, không phải
    /// hang hay lộ exception thô ra caller.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T055, US3.
    /// </summary>
    [Fact]
    public async Task ARequest_ReturnsAClearError_WhenTheBffIsUnreachable()
    {
        await using var gateway = CreateGatewayWithUnreachableBff();
        var client = gateway.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        // YARP reports an unreachable destination as 502. What matters for FR-006 is that it is a
        // definite, server-side error rather than a hang or a socket exception reaching the caller.
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; FR-006 requires a bounded error, not a hang.");
    }

    /// <summary>
    /// Kiểm tra: khi BFF không tới được, `/health/live` VÀ `/health/ready` của chính gateway vẫn trả
    /// `200`.
    /// Lý do phải test: BFF gặp sự cố không được kéo theo sức khoẻ tự báo cáo của gateway. Nếu
    /// readiness của gateway phụ thuộc vào BFF, 1 lần BFF sập sẽ khiến Kubernetes coi toàn bộ pod
    /// gateway là không sẵn sàng và restart hàng loạt — vì 1 lỗi ở tầng khác.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T055, US3.
    /// </summary>
    [Fact]
    public async Task TheGatewaysOwnHealth_StaysHealthy_WhenTheBffIsUnreachable()
    {
        await using var gateway = CreateGatewayWithUnreachableBff();
        var client = gateway.CreateClient();

        var live = await client.GetAsync("/health/live");
        var ready = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.OK, ready.StatusCode);
    }

    /// <summary>
    /// Kiểm tra: body lỗi khi BFF không tới được không chứa địa chỉ nội bộ (`127.0.0.1`), tên cluster
    /// (`bff-cluster`), tên route (`bff-route`), hay tên loại exception (`SocketException`).
    /// Lý do phải test: áp dụng đúng yêu cầu "không lộ chi tiết định tuyến nội bộ" của FR-007 sang cả
    /// đường xử lý lỗi — không chỉ path không khớp route mới phải giấu topology, lúc downstream chết
    /// cũng vậy.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — T055, US3.
    /// </summary>
    [Fact]
    public async Task TheError_LeaksNoInternalRoutingDetail()
    {
        await using var gateway = CreateGatewayWithUnreachableBff();
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/bff/products");
        var body = await response.Content.ReadAsStringAsync();

        foreach (var leak in new[] { "127.0.0.1", "bff-cluster", "bff-route", "SocketException" })
        {
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Overrides only the destination address, leaving the real route table, cluster, and timeout
    /// in place — the failure under test is "the destination is gone", not "the gateway is
    /// misconfigured".
    /// </summary>
    private static WebApplicationFactory<Program> CreateGatewayWithUnreachableBff() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Syntactically valid, deliberately unroutable — nothing answers on port 1.
                    ["ReverseProxy:Clusters:bff-cluster:Destinations:bff:Address"] = "http://127.0.0.1:1",
                }));

            builder.UseTestJwtBearer();
        });
}
