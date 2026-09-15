extern alias BffApi;

using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Mvc.Testing;
using ServiceDefaults;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// Constitution Principle VII: "A correlation ID MUST be generated at the edge and propagated
/// across every synchronous call." research.md Decision 7 makes that concrete for this feature —
/// the gateway and BFF forward <c>X-Correlation-Id</c> end to end.
/// </summary>
/// <remarks>
/// The gateway is the edge here, so it is where an ID gets generated when a caller supplies none.
/// If that generated ID reaches only the gateway's own response and not the forwarded request, the
/// BFF mints a second one — and the ID the caller is handed then identifies nothing in the BFF's
/// logs, which is precisely when a caller needs it (US3's error responses quote it).
/// </remarks>
public class CorrelationIdPropagationTests
{
    /// <summary>
    /// Kiểm tra: khi caller không gửi kèm `X-Correlation-Id`, ID gateway tự sinh và trả về trong
    /// header response phải khớp CHÍNH XÁC với `correlationId` mà BFF ghi trong body lỗi.
    /// Lý do phải test: đây là regression test cho 1 bug thật đã tìm thấy — `CorrelationIdMiddleware`
    /// từng chỉ ghi ID vào header RESPONSE, không ghi vào header REQUEST khi forward tiếp, nên YARP
    /// không mang ID đó sang BFF được — BFF phải tự sinh 1 ID khác, và ID caller cầm trên tay không
    /// khớp gì với log thật của BFF (vi phạm Constitution Principle VII). Lỗi đã sửa bằng 1 dòng ở
    /// `shared/ServiceDefaults/CorrelationIdMiddleware.cs`.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — phát hiện & sửa ngoài task chính thức, xem
    /// "Phase 6 implementation notes" trong specs/002-gateway-bff-routing/tasks.md, US3.
    /// </summary>
    [Fact]
    public async Task AGeneratedCorrelationId_ReachesTheBff_AndMatchesWhatTheCallerIsGiven()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = CreateGatewayWithTestJwtBearer(bff);
        var client = gateway.CreateClient().UseTestBearerToken();

        // No inbound header: the gateway must generate one and forward it. The route fails because
        // no products service is running, which is convenient — the BFF's ProblemDetails is what
        // reports the correlation ID the BFF actually saw.
        var response = await client.GetAsync("/bff/products");

        var callerFacingId = Assert.Single(
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        var idTheBffSaw = problem.GetProperty("correlationId").GetString();

        Assert.Equal(callerFacingId, idTheBffSaw);
    }

    /// <summary>
    /// Kiểm tra: khi caller tự gửi kèm `X-Correlation-Id`, giá trị đó phải được giữ nguyên xuyên suốt
    /// — cả trong header response lẫn `correlationId` của body lỗi phía BFF.
    /// Lý do phải test: 1 ID do caller cung cấp phải được TÁI SỬ DỤNG, không được thay bằng ID khác —
    /// nếu không, 1 client đang cố đối chiếu log của chính họ với log hệ thống sẽ mất dấu vết ngay từ
    /// điểm vào.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — phát hiện & sửa ngoài task chính thức, xem
    /// "Phase 6 implementation notes" trong specs/002-gateway-bff-routing/tasks.md, US3.
    /// </summary>
    [Fact]
    public async Task ACallerSuppliedCorrelationId_IsPreservedEndToEnd()
    {
        const string supplied = "caller-supplied-correlation-id";

        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = CreateGatewayWithTestJwtBearer(bff);
        var client = gateway.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

        var response = await client.SendAsync(request);

        Assert.Equal(
            supplied,
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(supplied, problem.GetProperty("correlationId").GetString());
    }

    /// <summary>
    /// Kiểm tra: nếu ID caller gửi lên (qua đường lách kiểm tra header thông thường) chứa ký tự điều
    /// khiển `\r`/`\n`, gateway phải thay nó bằng 1 ID tự sinh khác — không giữ nguyên ký tự đó.
    /// Lý do phải test: research.md Decision 2 — 1 giá trị do client tuỳ ý kiểm soát không bao giờ
    /// được lọt vào structured log mà không lọc, vì `\r\n` bên trong có thể giả mạo thêm 1 dòng log
    /// khác (log injection).
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — phát hiện & sửa ngoài task chính thức, xem
    /// "Phase 6 implementation notes" trong specs/002-gateway-bff-routing/tasks.md, US3.
    /// </summary>
    [Fact]
    public async Task ACorrelationIdContainingControlCharacters_IsReplacedWithAGeneratedOne()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = CreateGatewayWithTestJwtBearer(bff);
        var client = gateway.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        // HttpRequestMessage rejects a literal CR/LF in a header value outright, so the attack this
        // guards against is smuggled via UTF-8 bytes on the wire rather than System.Net's own header
        // API — TryAddWithoutValidation is what lets a malicious/misbehaving client actually send it.
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, "bad\r\nvalue");

        var response = await client.SendAsync(request);

        var callerFacingId = Assert.Single(
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.DoesNotContain('\r', callerFacingId);
        Assert.DoesNotContain('\n', callerFacingId);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(callerFacingId, problem.GetProperty("correlationId").GetString());
    }

    /// <summary>
    /// Kiểm tra: nếu ID caller gửi lên dài hơn 128 ký tự, gateway phải thay bằng 1 ID tự sinh khác
    /// (độ dài ≤ 128).
    /// Lý do phải test: research.md Decision 2 — 1 giá trị client tự đặt, không giới hạn độ dài, có
    /// thể làm phình to vô hạn mọi dòng log mà nó xuất hiện.
    /// Task nguồn: spec 002 (định tuyến gateway-BFF) — phát hiện & sửa ngoài task chính thức, xem
    /// "Phase 6 implementation notes" trong specs/002-gateway-bff-routing/tasks.md, US3.
    /// </summary>
    [Fact]
    public async Task ACorrelationIdLongerThan128Characters_IsReplacedWithAGeneratedOne()
    {
        var tooLong = new string('a', 129);

        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = CreateGatewayWithTestJwtBearer(bff);
        var client = gateway.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, tooLong);

        var response = await client.SendAsync(request);

        var callerFacingId = Assert.Single(
            response.Headers.GetValues(CorrelationIdMiddleware.HeaderName));
        Assert.NotEqual(tooLong, callerFacingId);
        Assert.True(callerFacingId.Length <= 128);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(callerFacingId, problem.GetProperty("correlationId").GetString());
    }

    /// <summary>
    /// Every test in this class sends a request the gateway itself must authenticate (Development's
    /// default <c>FeatureToggles:IdentityServerAuthCutover</c> is <see langword="true"/> —
    /// <c>appsettings.Development.json</c> — so the gateway's own <c>JwtBearer</c> scheme runs, not
    /// just the BFF's). <see cref="GatewayTestHost.CreateGateway"/> alone only wires the in-process
    /// forwarder to the BFF; without this, the gateway would attempt a real OIDC discovery/JWKS
    /// fetch against its configured (non-running, in tests) <c>Authority</c> and reject every
    /// request as unauthenticated before it ever reached the correlation ID logic under test —
    /// mirroring the bypass <c>JwtBearerAuthenticationTests.CreateGatewayWithTestJwtBearer</c>
    /// already uses for the same reason.
    /// </summary>
    private static WebApplicationFactory<Program> CreateGatewayWithTestJwtBearer(
        WebApplicationFactory<BffApi::Program> bff) =>
        GatewayTestHost.CreateGateway(bff).WithWebHostBuilder(builder => builder.UseTestJwtBearer());
}
