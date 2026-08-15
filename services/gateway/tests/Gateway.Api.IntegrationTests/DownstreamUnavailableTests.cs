using System.Diagnostics;
using System.Net;
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

    [Fact]
    public async Task ARequest_ReturnsAClearError_WhenTheBffIsUnreachable()
    {
        await using var gateway = CreateGatewayWithUnreachableBff();
        var client = gateway.CreateClient();

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
    /// A BFF outage must not take the gateway's own health reporting with it. If it did, every
    /// gateway pod would be restarted for a fault in a different service.
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
    /// The error must not disclose where the gateway tried to forward to (FR-007's "leaking
    /// internal routing details", applied to the failure path).
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
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    // Syntactically valid, deliberately unroutable — nothing answers on port 1.
                    ["ReverseProxy:Clusters:bff-cluster:Destinations:bff:Address"] = "http://127.0.0.1:1",
                })));
}
