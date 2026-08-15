using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace Gateway.Api.UnitTests;

/// <summary>
/// The gateway's entire routing surface is declarative configuration (research.md Decision 2), so
/// its correctness is a property of <c>appsettings.json</c> rather than of any code path a test
/// could otherwise exercise.
/// </summary>
/// <remarks>
/// Loaded through YARP's own <c>LoadFromConfig</c> binding rather than read as raw JSON. A test
/// that only asserted the presence of JSON keys would pass on config YARP rejects — a misspelled
/// <c>ClusterId</c>, a malformed match — which is precisely the failure worth catching before it
/// reaches a running gateway.
/// </remarks>
public class RouteConfigurationTests
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    private const string ExpectedRouteId = "bff-route";
    private const string ExpectedClusterId = "bff-cluster";

    /// <summary>
    /// research.md Decision 1: "The gateway's YARP configuration defines exactly one destination
    /// cluster — the BFF. It does not define clusters for products/baskets/orders."
    /// </summary>
    private static readonly string[] DomainServices = ["products", "baskets", "orders", "parties"];

    [Fact]
    public void TheConfiguration_DefinesExactlyOneRoute_ToTheBffCluster()
    {
        var config = LoadProxyConfig();

        var route = Assert.Single(config.Routes);
        Assert.Equal(ExpectedRouteId, route.RouteId);
        Assert.Equal(ExpectedClusterId, route.ClusterId);
    }

    /// <summary>
    /// The catch-all is load-bearing, not incidental: a gateway that enumerated the BFF's paths
    /// would need editing every time the BFF gained one, reintroducing the topology coupling
    /// spec FR-001 exists to remove.
    /// </summary>
    [Fact]
    public void TheRoute_MatchesEveryPath()
    {
        var config = LoadProxyConfig();

        var route = Assert.Single(config.Routes);
        Assert.Equal("{**catch-all}", route.Match.Path);
    }

    [Fact]
    public void TheConfiguration_DefinesExactlyOneCluster_WithOneDestination()
    {
        var config = LoadProxyConfig();

        var cluster = Assert.Single(config.Clusters);
        Assert.Equal(ExpectedClusterId, cluster.ClusterId);

        var destination = Assert.Single(cluster.Destinations!);
        Assert.False(string.IsNullOrWhiteSpace(destination.Value.Address));
    }

    /// <summary>
    /// The gateway must never be given a route to a domain service directly — that would let a
    /// caller reach one without passing through the BFF, which is the whole arrangement the
    /// constitution's edge chain fixes (load balancer → gateway → BFF).
    /// </summary>
    [Fact]
    public void TheConfiguration_NamesNoDomainServiceAsADestination()
    {
        var config = LoadProxyConfig();

        var addresses = config.Clusters
            .SelectMany(cluster => cluster.Destinations?.Values ?? [])
            .Select(destination => destination.Address)
            .ToArray();

        Assert.All(addresses, address => Assert.All(
            DomainServices,
            service => Assert.DoesNotContain(service, address, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Every route must resolve to a cluster that exists. data-model.md's validation rule: "a route
    /// with no matching cluster is a configuration error and MUST fail startup, not route silently
    /// to nothing."
    /// </summary>
    [Fact]
    public void EveryRoute_ResolvesToADefinedCluster()
    {
        var config = LoadProxyConfig();
        var clusterIds = config.Clusters.Select(cluster => cluster.ClusterId).ToHashSet(StringComparer.Ordinal);

        Assert.All(config.Routes, route =>
        {
            // A route with no ClusterId routes nowhere, which the rule below could not otherwise
            // distinguish from a route pointing at a cluster that happens to exist.
            Assert.NotNull(route.ClusterId);
            Assert.Contains(route.ClusterId, clusterIds);
        });
    }

    /// <summary>
    /// Binds the committed <c>appsettings.json</c> exactly as <c>Program.cs</c> does.
    /// </summary>
    private static IProxyConfig LoadProxyConfig()
    {
        var settingsPath = Path.Combine(
            LocateRepositoryRoot(),
            "services", "gateway", "src", "Gateway.Api", "appsettings.json");

        var configuration = new ConfigurationBuilder()
            .AddJsonFile(settingsPath, optional: false)
            .Build();

        var services = new ServiceCollection();
        // YARP's configuration provider resolves an ILogger; a bare ServiceCollection has none,
        // unlike the host Program.cs builds.
        services.AddLogging();
        services.AddReverseProxy().LoadFromConfig(configuration.GetSection("ReverseProxy"));

        return services.BuildServiceProvider()
            .GetRequiredService<IProxyConfigProvider>()
            .GetConfig();
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
