namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 2 (spec.md): SLO values match the platform default for the service's classification,
/// unless slos.justification documents why — contracts/service-manifest-slo-shape.md bất biến 4–5.
/// </summary>
public class SloDefaultComplianceTests
{
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());
        var manifest = discovered[serviceDirectoryName].Document;
        var slos = manifest.Slos!;
        var classification = manifest.Service!.Classification!;
        var defaultProfile = PlatformSloDefaults.ByClassification[classification];

        var matchesDefault =
            slos.Availability == defaultProfile.Availability
            && slos.ErrorRate?.MaxFiveXxRatio == defaultProfile.MaxFiveXxRatio
            && slos.Latency?.P95 == defaultProfile.LatencyP95
            && slos.Latency?.P99 == defaultProfile.LatencyP99;

        if (matchesDefault)
        {
            // Bất biến 4: khớp mặc định thì justification không bắt buộc — không assert gì thêm.
            return;
        }

        // Bất biến 5: lệch mặc định thì PHẢI có lý do, không rỗng, không placeholder.
        Assert.False(
            string.IsNullOrWhiteSpace(slos.Justification),
            $"'{serviceDirectoryName}' declares SLO values that differ from the '{classification}' platform " +
            $"default (availability={defaultProfile.Availability}, error-rate={defaultProfile.MaxFiveXxRatio}, " +
            $"p95={defaultProfile.LatencyP95}, p99={defaultProfile.LatencyP99}) but has no 'slos.justification'.");
    }
}
