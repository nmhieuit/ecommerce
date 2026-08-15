using System.Globalization;
using System.Text.Json;

namespace Gateway.Api.UnitTests;

/// <summary>
/// data-model.md, Route Mapping: the gateway's forwarding timeout "must be ≥ the BFF's own
/// downstream timeout budget so the gateway doesn't cut a request off that the BFF was still
/// legitimately waiting on."
/// </summary>
/// <remarks>
/// This invariant spans two services, so nothing about either one alone can enforce it. Left as a
/// note, it would hold only until somebody tuned one of the two numbers — and the symptom would be
/// subtle: the gateway returning its own error while the BFF was still working, producing a
/// confusing 504 that no BFF log would explain.
/// </remarks>
public class ForwardingTimeoutBudgetTests
{
    /// <summary>
    /// The BFF's total per-downstream budget, from
    /// <c>services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs</c>
    /// (<c>TotalRequestTimeout</c>). Mirrored rather than referenced so this test project does not
    /// take a dependency on another service's assembly; that file carries a comment pointing back.
    /// </summary>
    private static readonly TimeSpan BffTotalRequestTimeout = TimeSpan.FromSeconds(3);

    private const string RepositoryRootMarker = "Ecommerce.slnx";

    [Fact]
    public void TheGatewaysForwardingTimeout_IsAtLeastTheBffsTotalDownstreamBudget()
    {
        var activityTimeout = ReadActivityTimeout();

        Assert.True(
            activityTimeout >= BffTotalRequestTimeout,
            $"The gateway forwards with a {activityTimeout.TotalSeconds:F0}s timeout but the BFF may "
            + $"legitimately spend {BffTotalRequestTimeout.TotalSeconds:F0}s on one downstream call. "
            + "Raise ActivityTimeout in the gateway's appsettings.json, or lower the BFF's budget.");
    }

    /// <summary>
    /// Constitution Principle VIII: no unbounded wait may exist. An absent or infinite
    /// <c>ActivityTimeout</c> would satisfy the comparison above vacuously.
    /// </summary>
    [Fact]
    public void TheGatewaysForwardingTimeout_IsBounded()
    {
        var activityTimeout = ReadActivityTimeout();

        Assert.NotEqual(Timeout.InfiniteTimeSpan, activityTimeout);
        Assert.InRange(activityTimeout, BffTotalRequestTimeout, TimeSpan.FromMinutes(1));
    }

    private static TimeSpan ReadActivityTimeout()
    {
        var settingsPath = Path.Combine(
            LocateRepositoryRoot(),
            "services", "gateway", "src", "Gateway.Api", "appsettings.json");

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));

        var raw = document.RootElement
            .GetProperty("ReverseProxy")
            .GetProperty("Clusters")
            .GetProperty("bff-cluster")
            .GetProperty("HttpRequest")
            .GetProperty("ActivityTimeout")
            .GetString();

        Assert.False(string.IsNullOrWhiteSpace(raw), "bff-cluster declares no ActivityTimeout.");

        return TimeSpan.Parse(raw, CultureInfo.InvariantCulture);
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
