using System.Net.Http.Json;
using System.Text.Json;
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
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

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
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);

        var response = await client.SendAsync(request);

        Assert.Equal(
            supplied,
            Assert.Single(response.Headers.GetValues(CorrelationIdMiddleware.HeaderName)));

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(supplied, problem.GetProperty("correlationId").GetString());
    }
}
