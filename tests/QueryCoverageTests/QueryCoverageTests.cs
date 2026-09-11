namespace QueryCoverageTests;

/// <summary>
/// Spec FR-001/FR-002/FR-004/FR-005: which collection endpoints paginate with an enforced cap, and
/// which relational-data query call sites use a bounded lookup, is answerable by reading the files
/// that declare them, and losing coverage for one is caught rather than noticed later (spec SC-001,
/// SC-002, SC-003).
/// </summary>
/// <remarks>
/// Modelled on <c>tests/ResilienceCoverageTests</c>: a convention suite that reads the repository
/// rather than compiling against it. That is what lets it fail with "this endpoint is missing marker
/// X" instead of a compiler error naming an unrelated type.
/// </remarks>
public class QueryCoverageTests
{
    [Fact]
    public void ScanListEndpoints_ReportsNoViolations_ForCurrentInventory()
    {
        var result = QueryCoverageScanner.ScanListEndpoints(QueryCoverageScanner.LocateRepositoryRoot());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason once the inventory is
    /// populated by later user stories. An empty (or wrongly trimmed) expected-endpoint list reports
    /// zero violations and is indistinguishable from genuine coverage.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_ActuallyExaminesEveryExpectedEndpoint()
    {
        var result = QueryCoverageScanner.ScanListEndpoints(QueryCoverageScanner.LocateRepositoryRoot());

        Assert.Equal(
            QueryCoverageScanner.ExpectedListEndpoints.Count, result.ScannedSiteNames.Count);
    }

    [Fact]
    public void ScanBoundedQuerySites_ReportsNoViolations_ForCurrentInventory()
    {
        var result = QueryCoverageScanner.ScanBoundedQuerySites(QueryCoverageScanner.LocateRepositoryRoot());

        Assert.Empty(result.Violations);
    }

    /// <summary>Same guard as <see cref="ScanListEndpoints_ActuallyExaminesEveryExpectedEndpoint"/>, for the bounded-query-site list.</summary>
    [Fact]
    public void ScanBoundedQuerySites_ActuallyExaminesEveryExpectedSite()
    {
        var result = QueryCoverageScanner.ScanBoundedQuerySites(QueryCoverageScanner.LocateRepositoryRoot());

        Assert.Equal(
            QueryCoverageScanner.ExpectedBoundedQuerySites.Count, result.ScannedSiteNames.Count);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-001/FR-004 forever.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_DetectsViolation_WhenSourceFileIsMissing()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "does-not-exist",
            SourceFile: "does/not/exist.cs",
            RequiredMarkers: ["DefaultPageSize"]);

        var result = QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("does-not-exist", violation.SiteName);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-001/FR-004 forever.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "products-listing",
            SourceFile: "CatalogEndpoints.cs",
            RequiredMarkers: ["DefaultPageSize", "MaxPageSize"]);

        // Present, but only one of the two required markers — the missing one must surface as its
        // own violation, not collapse into a silent pass.
        fixture.Write(endpoint.SourceFile, "const int DefaultPageSize = 20;");

        var result = QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]);

        var violation = Assert.Single(result.Violations);
        Assert.Contains("MaxPageSize", violation.Reason);
    }

    [Fact]
    public void ScanListEndpoints_ReportsNoViolations_WhenFileHasEveryRequiredMarker()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "products-listing",
            SourceFile: "CatalogEndpoints.cs",
            RequiredMarkers: ["DefaultPageSize", "MaxPageSize"]);

        fixture.Write(endpoint.SourceFile, "const int DefaultPageSize = 20; const int MaxPageSize = 100;");

        Assert.Empty(QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]).Violations);
    }

    /// <summary>Same guard as the list-endpoint tests above, for the bounded-query-site scan.</summary>
    [Fact]
    public void ScanBoundedQuerySites_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var site = new ExpectedBoundedQuerySite(
            "bff-basket-render",
            SourceFile: "BasketsEndpoints.cs",
            RequiredMarkers: ["GetProductsByIdsAsync"]);

        fixture.Write(site.SourceFile, "await products.GetProductsAsync(cancellationToken);");

        var result = QueryCoverageScanner.ScanBoundedQuerySites(fixture.Root, [site]);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("bff-basket-render", violation.SiteName);
    }

    /// <summary>
    /// A throwaway repository root, so the scanner can be pointed at a deliberately incomplete tree
    /// without disturbing the real one.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), $"query-coverage-scan-{Guid.NewGuid():N}");

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
