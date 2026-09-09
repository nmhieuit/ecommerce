using System.Diagnostics;
using System.Globalization;
using System.Text.Json;

namespace Gateway.Api.UnitTests;

/// <summary>
/// 020-timeouts-retry-circuit-breaker spec FR-002/FR-003 (research.md Decision 3): the gateway's
/// forward to the BFF has a timeout (<see cref="ForwardingTimeoutBudgetTests"/>) but, before this
/// feature, no circuit breaker — a BFF outage meant every request kept attempting a real connection
/// and waiting out the full <c>ActivityTimeout</c> instead of failing fast. YARP's passive health
/// check is the mechanism that closes that gap without adding a Polly pipeline to the reverse proxy
/// itself (research.md Decision 3's alternatives).
/// </summary>
public class PassiveHealthCheckConfigurationTests
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    [Fact]
    public void BffCluster_HasPassiveHealthCheckEnabled()
    {
        var passive = ReadPassiveHealthCheck();

        Assert.True(
            passive.RootElement.TryGetProperty("Enabled", out var enabled) && enabled.GetBoolean(),
            "bff-cluster declares no enabled HealthCheck:Passive policy — a repeatedly failing BFF "
            + "has no circuit breaker and every request keeps attempting a real connection.");
    }

    [Fact]
    public void BffCluster_DeclaresAPassiveHealthCheckPolicy()
    {
        var passive = ReadPassiveHealthCheck();

        var policy = passive.RootElement.TryGetProperty("Policy", out var value) ? value.GetString() : null;

        Assert.False(
            string.IsNullOrWhiteSpace(policy),
            "bff-cluster's HealthCheck:Passive declares no Policy — YARP cannot evaluate failures "
            + "against a threshold without one.");
    }

    /// <summary>
    /// Constitution Principle VIII: no unbounded wait may exist. A destination marked unhealthy that
    /// never gets a chance to recover would turn a transient BFF blip into a permanent outage.
    /// </summary>
    [Fact]
    public void BffCluster_ReactivatesAfterABoundedPeriod()
    {
        var passive = ReadPassiveHealthCheck();

        var raw = passive.RootElement.TryGetProperty("ReactivationPeriod", out var value)
            ? value.GetString()
            : null;

        Assert.False(
            string.IsNullOrWhiteSpace(raw),
            "bff-cluster's HealthCheck:Passive declares no ReactivationPeriod — an unhealthy "
            + "destination would never be retried again.");

        var reactivationPeriod = TimeSpan.Parse(raw!, CultureInfo.InvariantCulture);

        Assert.True(reactivationPeriod > TimeSpan.Zero, "ReactivationPeriod must be a positive, bounded duration.");
    }

    /// <summary>
    /// YARP's own default <c>AvailableDestinationsPolicy</c> ("HealthyOrPanic") falls back to
    /// treating every destination as available whenever none are healthy — sensible for a
    /// multi-destination cluster, but it means a single-destination cluster like <c>bff-cluster</c>
    /// keeps attempting a connection it already knows will fail, never actually failing fast.
    /// Verified live: without this override, repeated requests kept returning 502 in ~2-4s each
    /// even after the destination was logged as 'Unhealthy'; with "HealthyAndUnknown" explicitly
    /// set, the same requests return 503 in single-digit milliseconds once the circuit opens.
    /// </summary>
    [Fact]
    public void BffCluster_UsesAvailableDestinationsPolicyThatActuallyExcludesUnhealthyDestinations()
    {
        var healthCheck = ReadHealthCheck();

        var policy = healthCheck.RootElement.TryGetProperty("AvailableDestinationsPolicy", out var value)
            ? value.GetString()
            : null;

        Assert.Equal("HealthyAndUnknown", policy);
    }

    private static JsonDocument ReadPassiveHealthCheck()
    {
        var healthCheck = ReadHealthCheck();

        if (!healthCheck.RootElement.TryGetProperty("Passive", out var passive))
        {
            Assert.Fail("bff-cluster declares no HealthCheck:Passive section at all.");
            throw new UnreachableException(); // Assert.Fail always throws; satisfies definite assignment.
        }

        // Cloned so it survives the `using` document above being disposed.
        return JsonDocument.Parse(passive.GetRawText());
    }

    private static JsonDocument ReadHealthCheck()
    {
        var settingsPath = Path.Combine(
            LocateRepositoryRoot(),
            "services", "gateway", "src", "Gateway.Api", "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

        var clusterElement = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Clusters")
            .GetProperty("bff-cluster");

        if (!clusterElement.TryGetProperty("HealthCheck", out var healthCheck))
        {
            Assert.Fail("bff-cluster declares no HealthCheck section at all.");
            throw new UnreachableException(); // Assert.Fail always throws; satisfies definite assignment.
        }

        // Cloned so it survives the `using` document above being disposed.
        return JsonDocument.Parse(healthCheck.GetRawText());
    }

    private static string LocateRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{RepositoryRootMarker}' walking up from '{AppContext.BaseDirectory}'.");
    }
}
