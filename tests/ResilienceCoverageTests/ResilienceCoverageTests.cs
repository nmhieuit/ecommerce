namespace ResilienceCoverageTests;

/// <summary>
/// Spec FR-007: which outbound call sites declare an explicit resilience policy is answerable by
/// reading the files that declare it, and losing coverage for one is caught rather than noticed
/// later (spec SC-001).
/// </summary>
/// <remarks>
/// Modelled on <c>tests/ContractCoverageTests</c>: a convention suite that reads the repository
/// rather than compiling against it. That is what lets it fail with "this call site is missing
/// marker X" instead of a compiler error naming an unrelated type.
/// </remarks>
public class ResilienceCoverageTests
{
    [Fact]
    public void Scan_ReportsNoViolations_ForCurrentInventory()
    {
        var result = ResilienceCoverageScanner.Scan(ResilienceCoverageScanner.LocateRepositoryRoot());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason once the inventory is
    /// populated by later user stories. An empty (or wrongly trimmed) expected-call-site list
    /// reports zero violations and is indistinguishable from genuine coverage.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryExpectedCallSite()
    {
        var result = ResilienceCoverageScanner.Scan(ResilienceCoverageScanner.LocateRepositoryRoot());

        Assert.Equal(ResilienceCoverageScanner.ExpectedCallSites.Count, result.ScannedCallSites.Count);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-007 forever.
    /// </summary>
    [Fact]
    public void Scan_DetectsViolation_WhenConfigurationFileIsMissing()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "does-not-exist",
            Caller: "bff",
            Callee: "nowhere",
            ConfigurationFile: "does/not/exist.cs",
            RequiredMarkers: ["AddStandardResilienceHandler"]);

        var result = ResilienceCoverageScanner.Scan(fixture.Root, [callSite]);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("does-not-exist", violation.CallSite);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-007 forever.
    /// </summary>
    [Fact]
    public void Scan_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "gateway-cluster",
            Caller: "gateway",
            Callee: "bff",
            ConfigurationFile: "config.json",
            RequiredMarkers: ["ActivityTimeout", "HealthCheck", "Passive"]);

        // Present, but only one of the three required markers — the missing two must each surface
        // as their own violation, not collapse into a single generic failure.
        fixture.Write(callSite.ConfigurationFile, "{ \"ActivityTimeout\": \"00:00:10\" }");

        var result = ResilienceCoverageScanner.Scan(fixture.Root, [callSite]);

        Assert.Equal(2, result.Violations.Count);
        Assert.All(result.Violations, violation => Assert.Equal("gateway-cluster", violation.CallSite));
    }

    [Fact]
    public void Scan_ReportsNoViolations_WhenFileHasEveryRequiredMarker()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "bff-downstream",
            Caller: "bff",
            Callee: "products",
            ConfigurationFile: "DownstreamClientRegistrationExtensions.cs",
            RequiredMarkers: ["AddStandardResilienceHandler"]);

        fixture.Write(callSite.ConfigurationFile, "services.AddStandardResilienceHandler(...)");

        Assert.Empty(ResilienceCoverageScanner.Scan(fixture.Root, [callSite]).Violations);
    }

    /// <summary>
    /// A throwaway repository root, so the scanner can be pointed at a deliberately incomplete tree
    /// without disturbing the real one.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), $"resilience-coverage-scan-{Guid.NewGuid():N}");

        public RepositoryFixture() => Directory.CreateDirectory(Root);

        public void Write(string relativePath, string content)
        {
            var absolute = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
