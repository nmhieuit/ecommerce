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
    /// A caller-supplied ID must be reused rather than replaced, or a client correlating its own
    /// logs with ours loses the thread at the edge.
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
    /// research.md Decision 2: a value a client controls must never reach a structured log
    /// unfiltered — <c>\r\n</c> inside it could forge a second, fake log line.
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

    /// <summary>research.md Decision 2: an unbounded client-supplied value could bloat every log line it touches indefinitely.</summary>
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
