using System.Net;
using System.Net.Http.Json;

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
    /// The strongest available proof of arrival: the BFF's generated OpenAPI document, which the
    /// gateway cannot produce and which needs no domain service running to return 200.
    /// </summary>
    [Fact]
    public async Task ARequestToTheGateway_ReachesAResponseOnlyTheBffCanProduce()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var document = await response.Content.ReadFromJsonAsync<OpenApiDocument>();
        Assert.NotNull(document);
        Assert.Contains("/bff/products", document.Paths.Keys);
    }

    /// <summary>
    /// A client-facing route forwards too, not only the contract document. The products service is
    /// not running here, so the BFF's own downstream call fails — but reaching a BFF handler at all
    /// is what US2 claims. A 404 would mean the gateway never forwarded.
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
    /// The catch-all route must not swallow the gateway's own health probes. Kubernetes calls
    /// these on the gateway itself; if they were forwarded, the gateway would report the BFF's
    /// health as its own and a BFF outage would restart every gateway pod.
    /// </summary>
    /// <remarks>
    /// ASP.NET Core routing prefers the more specific <c>/health/live</c> over
    /// <c>{**catch-all}</c>, so this passes today — the test exists so that a later change to the
    /// route table cannot quietly break it.
    /// </remarks>
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
