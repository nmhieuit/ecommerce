namespace QueryCoverageTests;

/// <summary>
/// One collection endpoint in the platform, and the file plus the markers that together prove it
/// paginates by default and enforces a server-side page-size cap (023-audit-n1-unbounded-pagination
/// data-model.md, "List Endpoint Inventory Entry"; contracts/query-coverage-inventory-contract.md).
/// </summary>
/// <param name="Name">How the endpoint is referred to in the spec/plan and in the inventory contract.</param>
/// <param name="SourceFile">Repository-relative path of the source file that declares the endpoint.</param>
/// <param name="RequiredMarkers">
/// Every string that MUST appear in <see cref="SourceFile"/> for this endpoint to count as covered.
/// A marker missing from the file is exactly as much a violation as the file itself being missing.
/// </param>
public sealed record ExpectedListEndpoint(
    string Name,
    string SourceFile,
    IReadOnlyList<string> RequiredMarkers);

/// <summary>
/// One relational-data query call site with N+1/over-fetch risk, and the file plus the marker that
/// together prove it uses a bounded lookup instead of fetching an unbounded related set
/// (data-model.md "Bounded Query Site Inventory Entry").
/// </summary>
/// <param name="Name">How the call site is referred to in the spec/plan and in the inventory contract.</param>
/// <param name="SourceFile">Repository-relative path of the source file that declares the call site.</param>
/// <param name="RequiredMarkers">Every string that MUST appear in <see cref="SourceFile"/> for this call site to count as bounded.</param>
public sealed record ExpectedBoundedQuerySite(
    string Name,
    string SourceFile,
    IReadOnlyList<string> RequiredMarkers);

/// <summary>An endpoint or call site missing one of its required markers (or the file itself).</summary>
public sealed record CoverageViolation(string SiteName, string SourceFile, string Reason);

/// <summary>
/// What a scan looked at, not only what it objected to. A scan pointed at the wrong directory finds
/// no sites and reports no violations, which is indistinguishable from full coverage — so the count
/// of what was examined is part of the result rather than an afterthought.
/// </summary>
public sealed record CoverageScanResult(
    IReadOnlyList<string> ScannedSiteNames,
    IReadOnlyList<CoverageViolation> Violations);

/// <summary>
/// The repeatable check behind spec FR-001/FR-002/FR-004/FR-005: every collection endpoint paginates
/// and enforces a server-side cap, every relational-data query call site with over-fetch risk uses a
/// bounded lookup, and a newly added endpoint or call site cannot be forgotten silently.
/// </summary>
/// <remarks>
/// <para>
/// The expected sets are literals here rather than something discovered from the filesystem, and
/// that is the entire point — see <c>tests/ResilienceCoverageTests/ResilienceCoverageScanner.cs</c>
/// for the same reasoning, which this scanner mirrors. A scanner that grepped the solution for
/// <c>MapGet(</c> returning an array would report full coverage the moment a genuinely-bounded
/// endpoint were flagged as needing pagination it does not need, or would miss a new unbounded
/// endpoint entirely if it used an unfamiliar shape. Adding an endpoint or call site means editing
/// these lists, which is a reviewable decision in the same pull request rather than a silent
/// consequence.
/// </para>
/// <para>
/// Filesystem-only, like <c>tests/ContractCoverageTests</c> and
/// <c>tests/ResilienceCoverageTests</c>: it judges what is present in text, so it deliberately
/// cannot compile against the projects it is judging.
/// </para>
/// </remarks>
public static class QueryCoverageScanner
{
    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// Every collection endpoint known to exist in the system, matching
    /// specs/023-audit-n1-unbounded-pagination/contracts/query-coverage-inventory-contract.md.
    /// Populated incrementally as each user story closes the gap its row represents (tasks.md T007,
    /// T008, T019) — a row appears here in the same commit that makes it pass, per the Test-First
    /// discipline that keeps this list red before green rather than backfilled after the fact.
    /// `orders` and `parties` have no row: neither service has a collection endpoint at all today
    /// (research.md Decision 1, #3/#4) — a row pointing at a file that does not exist would always
    /// be a violation, which is not the point of this list.
    /// </summary>
    public static IReadOnlyList<ExpectedListEndpoint> ExpectedListEndpoints { get; } =
    [
        new ExpectedListEndpoint(
            "products-listing",
            SourceFile: "services/products/src/Products.Api/Features/Catalog/CatalogEndpoints.cs",
            // DefaultPageSize: bounded-by-default (US1, tasks.md T007). MaxPageSize: server-side cap
            // for an explicit caller-supplied value (US3, tasks.md T019).
            RequiredMarkers: ["DefaultPageSize", "MaxPageSize"]),
        new ExpectedListEndpoint(
            "bff-products-listing",
            SourceFile: "services/bff/src/Bff.Api/Features/Products/ProductsEndpoints.cs",
            // Proof that page/pageSize are accepted from the caller and forwarded downstream rather
            // than the BFF silently re-introducing an unbounded fetch (US1, tasks.md T008).
            RequiredMarkers: ["page", "pageSize"]),
    ];

    /// <summary>
    /// Every relational-data query call site with N+1/over-fetch risk known to exist in the system.
    /// Both rows point at the same file — the two places <c>BasketsEndpoints</c> used to fetch the
    /// entire product catalog just to resolve a handful of ids (research.md Decision 1, #6/#7).
    /// </summary>
    public static IReadOnlyList<ExpectedBoundedQuerySite> ExpectedBoundedQuerySites { get; } =
    [
        new ExpectedBoundedQuerySite(
            "bff-basket-render",
            SourceFile: "services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs",
            RequiredMarkers: ["GetProductsByIdsAsync"]),
        new ExpectedBoundedQuerySite(
            "bff-basket-add-item",
            SourceFile: "services/bff/src/Bff.Api/Features/Baskets/BasketsEndpoints.cs",
            RequiredMarkers: ["GetProductsByIdsAsync"]),
    ];

    /// <summary>
    /// Walks up from the test assembly to the repository root, so the scan works the same from the
    /// IDE, the CLI, and CI regardless of working directory.
    /// </summary>
    public static string LocateRepositoryRoot()
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

    /// <summary>Reports every expected list endpoint under <paramref name="repositoryRoot"/> missing a required marker (or its file).</summary>
    public static CoverageScanResult ScanListEndpoints(string repositoryRoot) =>
        ScanListEndpoints(repositoryRoot, ExpectedListEndpoints);

    /// <summary>
    /// The same check against a caller-supplied endpoint set, so the scanner's own tests can prove
    /// it detects a gap without disturbing the repository.
    /// </summary>
    public static CoverageScanResult ScanListEndpoints(
        string repositoryRoot, IReadOnlyList<ExpectedListEndpoint> expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(expected);

        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"No repository root at '{repositoryRoot}'.");
        }

        var violations = new List<CoverageViolation>();

        foreach (var endpoint in expected)
        {
            var absolute = Path.Combine(
                repositoryRoot, endpoint.SourceFile.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absolute))
            {
                violations.Add(new CoverageViolation(
                    endpoint.Name,
                    endpoint.SourceFile,
                    $"has no source file at '{endpoint.SourceFile}' — the endpoint cannot be "
                    + "pagination-covered if the file that would declare it does not exist"));
                continue;
            }

            var content = File.ReadAllText(absolute);

            foreach (var marker in endpoint.RequiredMarkers)
            {
                if (!content.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add(new CoverageViolation(
                        endpoint.Name,
                        endpoint.SourceFile,
                        $"is missing required marker '{marker}' — its pagination/cap is either "
                        + "absent or was not declared the way this inventory expects"));
                }
            }
        }

        return new CoverageScanResult([.. expected.Select(e => e.Name)], violations);
    }

    /// <summary>Reports every expected bounded query site under <paramref name="repositoryRoot"/> missing a required marker (or its file).</summary>
    public static CoverageScanResult ScanBoundedQuerySites(string repositoryRoot) =>
        ScanBoundedQuerySites(repositoryRoot, ExpectedBoundedQuerySites);

    /// <summary>
    /// The same check against a caller-supplied call-site set, so the scanner's own tests can prove
    /// it detects a gap without disturbing the repository.
    /// </summary>
    public static CoverageScanResult ScanBoundedQuerySites(
        string repositoryRoot, IReadOnlyList<ExpectedBoundedQuerySite> expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(expected);

        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"No repository root at '{repositoryRoot}'.");
        }

        var violations = new List<CoverageViolation>();

        foreach (var site in expected)
        {
            var absolute = Path.Combine(
                repositoryRoot, site.SourceFile.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absolute))
            {
                violations.Add(new CoverageViolation(
                    site.Name,
                    site.SourceFile,
                    $"has no source file at '{site.SourceFile}' — the call site cannot be "
                    + "bounded-query-covered if the file that would declare it does not exist"));
                continue;
            }

            var content = File.ReadAllText(absolute);

            foreach (var marker in site.RequiredMarkers)
            {
                if (!content.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add(new CoverageViolation(
                        site.Name,
                        site.SourceFile,
                        $"is missing required marker '{marker}' — it may still be fetching an "
                        + "unbounded related set instead of a bounded lookup"));
                }
            }
        }

        return new CoverageScanResult([.. expected.Select(e => e.Name)], violations);
    }
}
