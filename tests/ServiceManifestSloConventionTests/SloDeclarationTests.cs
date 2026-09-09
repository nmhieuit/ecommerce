namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 1 (spec.md): every service-manifest.yaml declares all 4 SLO values, non-empty and not
/// a placeholder — contracts/service-manifest-slo-shape.md bất biến 1–3, 6.
/// </summary>
public class SloDeclarationTests
{
    /// <summary>The 7 services expected to exist — same list DeploymentManifestConventionTests uses.</summary>
    private static readonly string[] ExpectedServiceDirectories =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    private static readonly string[] PlaceholderTokens =
        ["TODO", "TBD", "XXX", "PLACEHOLDER", "N/A", "CHANGEME"];

    /// <summary>
    /// Spec SC-001: a scan that silently examines fewer than 7 services would report "all pass"
    /// while missing one entirely — the same trap ServiceInventoryTests guards against for the
    /// deployment inventory.
    /// </summary>
    [Fact]
    public void Discovery_FindsExactlyTheSevenExpectedServices()
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        Assert.Equal(ExpectedServiceDirectories.OrderBy(s => s), discovered.Keys.OrderBy(s => s));
    }

    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresAllFourSloValues_NonEmptyAndNotPlaceholder(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        Assert.True(discovered.ContainsKey(serviceDirectoryName), $"'{serviceDirectoryName}' has no service-manifest.yaml.");
        var slos = discovered[serviceDirectoryName].Document.Slos;

        Assert.NotNull(slos);
        AssertPresentAndNotPlaceholder(slos!.Availability, serviceDirectoryName, "slos.availability");
        AssertPresentAndNotPlaceholder(slos.ErrorRate?.MaxFiveXxRatio, serviceDirectoryName, "slos.error-rate.max-5xx-ratio");
        AssertPresentAndNotPlaceholder(slos.Latency?.P95, serviceDirectoryName, "slos.latency.p95");
        AssertPresentAndNotPlaceholder(slos.Latency?.P99, serviceDirectoryName, "slos.latency.p99");
    }

    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_HasAKnownClassification(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());
        var classification = discovered[serviceDirectoryName].Document.Service?.Classification;

        Assert.True(
            classification is not null && PlatformSloDefaults.ByClassification.ContainsKey(classification),
            $"'{serviceDirectoryName}' has service.classification='{classification}', which is not one of the known " +
            $"platform profiles ({string.Join(", ", PlatformSloDefaults.ByClassification.Keys)}).");
    }

    /// <summary>Contracts bất biến 6, SC-001: no "orphan" manifest whose declared name disagrees with its folder.</summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaredNameMatchesItsDirectory(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        Assert.Equal(serviceDirectoryName, discovered[serviceDirectoryName].Document.Service?.Name);
    }

    private static void AssertPresentAndNotPlaceholder(string? value, string serviceDirectoryName, string fieldPath)
    {
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            $"'{serviceDirectoryName}' is missing a value for '{fieldPath}'.");
        Assert.False(
            PlaceholderTokens.Any(token => string.Equals(value, token, StringComparison.OrdinalIgnoreCase)),
            $"'{serviceDirectoryName}' has a placeholder value '{value}' for '{fieldPath}'.");
    }
}
