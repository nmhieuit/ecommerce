using System.Diagnostics;
using System.Net;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// Spec FR-007 and the Edge Case it comes from: "What happens when the gateway receives a request
/// path that doesn't match any known route? The gateway MUST return a clear not-found response
/// rather than hanging or leaking internal routing details."
/// </summary>
/// <remarks>
/// <para>
/// Worth being precise about where the 404 originates. The gateway's route table is a single
/// catch-all (data-model.md, Route Mapping: <c>{**catch-all}</c> — "all API traffic forwards to
/// the BFF"), so from the gateway's point of view <em>no</em> path is unmatched. An unknown path
/// is forwarded, and the BFF — which has a finite route table — answers 404. The requirement is
/// about what the caller observes, and the caller observes a prompt, clear 404 either way.
/// </para>
/// <para>
/// The alternative design, scoping the gateway route to <c>/bff/{**catch-all}</c> so YARP itself
/// 404s anything outside that prefix, would satisfy FR-007 one layer earlier. It was not chosen
/// because data-model.md specifies the catch-all, and because a gateway that only forwards
/// <c>/bff/*</c> would have to be edited every time the BFF gains a new top-level path — exactly
/// the topology coupling US2 exists to remove.
/// </para>
/// </remarks>
public class UnmatchedRouteTests
{
    /// <summary>
    /// Generous enough not to be flaky on a cold test host, tight enough that a genuine hang — the
    /// failure mode FR-007 names — cannot pass.
    /// </summary>
    private static readonly TimeSpan ClearlyNotAHang = TimeSpan.FromSeconds(15);

    [Theory]
    [InlineData("/no-such-path")]
    [InlineData("/bff/no-such-resource")]
    [InlineData("/bff/products/extra/segments")]
    public async Task AnUnknownPath_ReturnsAClearNotFound_RatherThanHanging(string path)
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync(path);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.True(
            stopwatch.Elapsed < ClearlyNotAHang,
            $"'{path}' took {stopwatch.Elapsed.TotalSeconds:F1}s; FR-007 requires a clear error, not a hang.");
    }

    /// <summary>
    /// "…rather than leaking internal routing details" (FR-007). A 404 body must not name the
    /// cluster, destination, or address the gateway would have forwarded to.
    /// </summary>
    [Fact]
    public async Task AnUnknownPathsResponse_LeaksNoInternalRoutingDetail()
    {
        await using var bff = GatewayTestHost.CreateBff();
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/no-such-path");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        foreach (var internalDetail in new[] { "bff-cluster", "bff-route", "products-api", "8080" })
        {
            Assert.DoesNotContain(internalDetail, body, StringComparison.OrdinalIgnoreCase);
        }
    }
}
