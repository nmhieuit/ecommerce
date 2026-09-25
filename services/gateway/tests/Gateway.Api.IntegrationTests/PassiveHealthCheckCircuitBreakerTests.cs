using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// Spec 020 (timeout/retry/circuit breaker) User Story 2 (FR-002/FR-003): khi BFF liên tục lỗi,
/// passive health check của gateway phải MỞ mạch — các request sau phải fail fast thay vì mỗi request
/// đều thử (và chờ hết) 1 kết nối thật.
/// </summary>
/// <remarks>
/// <para>
/// Khác <see cref="DownstreamUnavailableTests"/> (chỉ chứng minh 1 lần lỗi có giới hạn thời gian), test
/// này chứng minh chính bước chuyển của circuit breaker: <c>TransportFailureRateHealthPolicy</c> của
/// YARP đánh dấu destination <c>bff</c> là unhealthy khi tỷ lệ lỗi vượt ngưỡng cấu hình trong số
/// request tối thiểu, và 1 cluster không còn destination khoẻ trả `503`
/// (<c>HealthCheckConstants.AvailableDestinations.HealthyAndUnknown</c>) thay vì cố kết nối — 1 kiểu
/// lỗi khác, và nhanh hơn hẳn, so với `502` của 1 lần thử destination không tới được thông thường.
/// </para>
/// <para>
/// <c>MinimalTotalCountThreshold</c> được ghi đè hạ xuống từ mặc định 10 của YARP để test không phải
/// gửi 10 request mới thấy được bước chuyển; production giữ mặc định của framework (research.md
/// Decision 3 — không có gì trong <c>appsettings.json</c> hay <c>Program.cs</c> đặt giá trị này).
/// </para>
/// </remarks>
public class PassiveHealthCheckCircuitBreakerTests
{
    /// <summary>
    /// Kiểm tra: BFF không tới được — 2 request đầu (dưới ngưỡng tối thiểu) trả `502 BadGateway`, sang
    /// request thứ 3 (sau khi tỷ lệ lỗi 100% vượt ngưỡng 50%) mạch mở và trả `503 ServiceUnavailable`
    /// mà không thử kết nối thật.
    /// Lý do: US2 Acceptance Scenario 2 — chứng minh động (không chỉ đọc cấu hình) rằng circuit breaker
    /// gateway→BFF thật sự chuyển từ "thử kết nối rồi fail chậm" sang "fail nhanh không thử kết nối".
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-002/FR-003, US2 Acceptance Scenario 2.
    /// </summary>
    [Fact]
    public async Task AfterRepeatedFailures_TheCircuitOpens_AndSubsequentRequestsFailFast_WithoutAttemptingAConnection()
    {
        await using var gateway = CreateGatewayWithUnreachableBffAndAggressivePassiveHealthCheck();
        var client = gateway.CreateClient();

        // Dưới MinimalTotalCountThreshold (2): destination vẫn ở trạng thái "Unknown", nên YARP vẫn thử
        // kết nối thật và nhận lỗi destination-không-tới-được thông thường.
        var firstAttempt = await client.GetAsync("/bff/products");
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — 502 nghĩa là YARP đã thử
        // kết nối thật và thất bại.
        Assert.Equal(HttpStatusCode.BadGateway, firstAttempt.StatusCode);

        var secondAttempt = await client.GetAsync("/bff/products");
        Assert.Equal(HttpStatusCode.BadGateway, secondAttempt.StatusCode);

        // Policy tỷ lệ lỗi đã thấy 2 request được proxy, cả 2 đều lỗi transport — 100%, vượt ngưỡng
        // cấu hình 50%. Destination phải bị đánh dấu unhealthy và bị loại khỏi danh sách khả dụng.
        var afterThreshold = await client.GetAsync("/bff/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — 503 nghĩa là mạch đã mở và
        // YARP fail fast; nếu vẫn 502 thì circuit breaker chưa hoạt động.
        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            afterThreshold.StatusCode);
    }

    /// <summary>
    /// Ghi đè địa chỉ destination (không tới được, giống <see cref="DownstreamUnavailableTests"/>) cùng
    /// số mẫu tối thiểu của policy tỷ lệ lỗi, giữ nguyên bảng route thật, metadata cluster
    /// (<c>TransportFailureRateHealthPolicy.RateLimit</c>) và đăng ký passive health check đúng như
    /// production cấu hình.
    /// </summary>
    private static WebApplicationFactory<Program> CreateGatewayWithUnreachableBffAndAggressivePassiveHealthCheck() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Syntactically valid, deliberately unroutable — nothing answers on port 1.
                    ["ReverseProxy:Clusters:bff-cluster:Destinations:bff:Address"] = "http://127.0.0.1:1",
                    // Production default is 10 (Yarp.ReverseProxy.Health.TransportFailureRateHealthPolicyOptions);
                    // lowered here only so the test does not need ten requests to observe the transition.
                    ["ReverseProxy:TransportFailureRateHealthPolicy:MinimalTotalCountThreshold"] = "2",
                    // appsettings.Development.json defaults this to true for local hand-testing
                    // against a real identity server (quickstart.md Scenarios 1-6); this test is
                    // about the passive health check, not authentication, so it forces the Phase 1
                    // stub back on regardless of which environment WebApplicationFactory runs under.
                    ["FeatureToggles:IdentityServerAuthCutover"] = "false",
                    ["FeatureToggles:AuthorizationRequireApiScope"] = "false",
                })));
}
