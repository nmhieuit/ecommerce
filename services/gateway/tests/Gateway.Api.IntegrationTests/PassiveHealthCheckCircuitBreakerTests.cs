using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// 020-timeouts-retry-circuit-breaker User Story 2 (spec FR-002/FR-003): when the BFF repeatedly
/// fails, the gateway's passive health check must open — subsequent requests must fail fast instead
/// of each one attempting (and waiting out) a real connection.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="DownstreamUnavailableTests"/> (which only proves a single failure is bounded),
/// this proves the circuit-breaker transition itself: YARP's <c>TransportFailureRateHealthPolicy</c>
/// marks the <c>bff</c> destination unhealthy once its failure rate crosses the configured limit
/// within the minimum request count, and a cluster with no healthy destination answers with 503
/// (<c>HealthCheckConstants.AvailableDestinations.HealthyAndUnknown</c>) instead of attempting a
/// connection at all — a different, and distinctly faster, failure than the 502 an ordinary
/// unreachable-destination attempt produces.
/// </para>
/// <para>
/// <c>MinimalTotalCountThreshold</c> is overridden down from YARP's default of 10 so the test does
/// not need to send ten requests to observe the transition; production keeps the framework default
/// (research.md Decision 3 — nothing in <c>appsettings.json</c> or <c>Program.cs</c> sets it).
/// </para>
/// </remarks>
public class PassiveHealthCheckCircuitBreakerTests
{
    [Fact]
    public async Task AfterRepeatedFailures_TheCircuitOpens_AndSubsequentRequestsFailFast_WithoutAttemptingAConnection()
    {
        await using var gateway = CreateGatewayWithUnreachableBffAndAggressivePassiveHealthCheck();
        var client = gateway.CreateClient();

        // Below MinimalTotalCountThreshold (2): the destination is still "Unknown", so YARP still
        // attempts a real connection and gets the ordinary unreachable-destination failure.
        var firstAttempt = await client.GetAsync("/bff/products");
        Assert.Equal(HttpStatusCode.BadGateway, firstAttempt.StatusCode);

        var secondAttempt = await client.GetAsync("/bff/products");
        Assert.Equal(HttpStatusCode.BadGateway, secondAttempt.StatusCode);

        // The failure rate policy has now seen 2 proxied requests, both transport failures — 100%,
        // over the configured 50% limit. The destination should be marked unhealthy and excluded.
        var afterThreshold = await client.GetAsync("/bff/products");

        Assert.Equal(
            HttpStatusCode.ServiceUnavailable,
            afterThreshold.StatusCode);
    }

    /// <summary>
    /// Overrides the destination address (unreachable, like <see cref="DownstreamUnavailableTests"/>)
    /// plus the failure-rate policy's minimum sample count, leaving the real route table, cluster
    /// metadata (<c>TransportFailureRateHealthPolicy.RateLimit</c>), and passive health check
    /// registration exactly as production configures them.
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
