namespace ContractCoverageTests;

/// <summary>
/// One boundary of the thin slice, and the two files that together prove it is covered
/// (011-consumer-contract-tests data-model.md, "Boundary").
/// </summary>
/// <param name="Name">How the boundary is referred to in the spec and in <c>pacts/README.md</c>.</param>
/// <param name="Consumer">The service that states the expectation.</param>
/// <param name="Producer">The service whose own build has to satisfy it.</param>
/// <param name="PactFile">Repository-relative path of the committed Pact document.</param>
/// <param name="VerificationTestFile">
/// Repository-relative path of the provider-side test that verifies it. A pact file with no test
/// reading it proves nothing: it would sit in the directory looking like coverage while no build
/// ever checked it.
/// </param>
public sealed record Boundary(
    string Name,
    string Consumer,
    string Producer,
    string PactFile,
    string VerificationTestFile);

/// <summary>A boundary missing one of the two files that would make it covered.</summary>
public sealed record CoverageViolation(string Boundary, string MissingPath, string Reason);

/// <summary>
/// What a scan looked at, not only what it objected to. A scan pointed at the wrong directory finds
/// no boundaries and reports no violations, which is indistinguishable from full coverage — so the
/// count of what was examined is part of the result rather than an afterthought.
/// </summary>
public sealed record CoverageScanResult(
    IReadOnlyList<Boundary> ScannedBoundaries,
    IReadOnlyList<CoverageViolation> Violations);

/// <summary>
/// The repeatable check behind spec FR-008 and FR-009: every boundary in the thin slice has a
/// committed consumer expectation and a provider-side test that verifies it.
/// </summary>
/// <remarks>
/// <para>
/// The expected set is a literal here rather than something discovered from the filesystem, and
/// that is the entire point. A scan that derived the boundaries from whatever pact files happen to
/// exist would report full coverage the moment one was deleted. Adding a boundary means editing
/// this list, which is a reviewable decision rather than a silent consequence.
/// </para>
/// <para>
/// Filesystem-only, like <c>tests/StructureConventionTests</c>: it judges what is present, so it
/// deliberately cannot compile against the projects it is judging.
/// </para>
/// </remarks>
public static class ContractCoverageScanner
{
    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// The four boundaries of the thin slice, matching the table in
    /// <c>specs/011-consumer-contract-tests/data-model.md</c> and in <c>pacts/README.md</c>.
    /// </summary>
    public static IReadOnlyList<Boundary> ExpectedBoundaries { get; } =
    [
        new Boundary(
            "BFF-products",
            Consumer: "bff",
            Producer: "products",
            PactFile: "pacts/bff-products.json",
            VerificationTestFile:
                "services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs"),
        new Boundary(
            "BFF-baskets",
            Consumer: "bff",
            Producer: "baskets",
            PactFile: "pacts/bff-baskets.json",
            VerificationTestFile:
                "services/baskets/tests/Baskets.Api.ContractTests/BasketsProviderPactTests.cs"),
        new Boundary(
            "BFF-orders",
            Consumer: "bff",
            Producer: "orders",
            PactFile: "pacts/bff-orders.json",
            VerificationTestFile:
                "services/orders/tests/Orders.Api.ContractTests/OrdersProviderPactTests.cs"),
        new Boundary(
            "BasketCheckedOut",
            Consumer: "orders",
            Producer: "baskets",
            PactFile: "pacts/orders-basketcheckedout.json",
            VerificationTestFile:
                "services/baskets/tests/Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs"),
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

    /// <summary>
    /// Reports every expected boundary under <paramref name="repositoryRoot"/> that is missing its
    /// pact document or its provider-side verification test.
    /// </summary>
    public static CoverageScanResult Scan(string repositoryRoot) =>
        Scan(repositoryRoot, ExpectedBoundaries);

    /// <summary>
    /// The same check against a caller-supplied boundary set, so the scanner's own tests can prove
    /// it detects a gap without disturbing the repository.
    /// </summary>
    public static CoverageScanResult Scan(string repositoryRoot, IReadOnlyList<Boundary> expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(expected);

        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"No repository root at '{repositoryRoot}'.");
        }

        var violations = new List<CoverageViolation>();

        foreach (var boundary in expected)
        {
            Check(
                boundary,
                boundary.PactFile,
                $"has no committed consumer expectation — '{boundary.Consumer}' must write "
                + $"'{boundary.PactFile}' before '{boundary.Producer}' can verify anything");

            Check(
                boundary,
                boundary.VerificationTestFile,
                $"has a pact but no provider-side test reading it — '{boundary.Producer}' would "
                + "build green while the expectation went unchecked");
        }

        return new CoverageScanResult(expected, violations);

        void Check(Boundary boundary, string relativePath, string reason)
        {
            var absolute = Path.Combine(
                repositoryRoot,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absolute))
            {
                violations.Add(new CoverageViolation(boundary.Name, relativePath, reason));
            }
        }
    }
}
