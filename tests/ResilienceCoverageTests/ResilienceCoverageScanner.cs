namespace ResilienceCoverageTests;

/// <summary>
/// One outbound call site in the platform, and the file plus the markers that together prove it
/// declares an explicit resilience policy (020-timeouts-retry-circuit-breaker data-model.md,
/// "Outbound Call Inventory Entry"; contracts/outbound-call-inventory-contract.md).
/// </summary>
/// <param name="Name">How the call site is referred to in the spec/plan and in the inventory contract.</param>
/// <param name="Caller">The service that initiates the call.</param>
/// <param name="Callee">The dependency the call reaches.</param>
/// <param name="ConfigurationFile">
/// Repository-relative path of the source/configuration file where the resilience policy is
/// declared.
/// </param>
/// <param name="RequiredMarkers">
/// Every string that MUST appear in <see cref="ConfigurationFile"/> for this call site to count as
/// covered. A marker missing from the file is exactly as much a violation as the file itself being
/// missing.
/// </param>
public sealed record OutboundCallSite(
    string Name,
    string Caller,
    string Callee,
    string ConfigurationFile,
    IReadOnlyList<string> RequiredMarkers);

/// <summary>A call site missing one of its required markers (or the file itself).</summary>
public sealed record CoverageViolation(string CallSite, string ConfigurationFile, string Reason);

/// <summary>
/// What a scan looked at, not only what it objected to. A scan pointed at the wrong directory finds
/// no call sites and reports no violations, which is indistinguishable from full coverage — so the
/// count of what was examined is part of the result rather than an afterthought.
/// </summary>
public sealed record CoverageScanResult(
    IReadOnlyList<OutboundCallSite> ScannedCallSites,
    IReadOnlyList<CoverageViolation> Violations);

/// <summary>
/// The repeatable check behind spec FR-007: every outbound call site in the system declares an
/// explicit timeout, retry, and circuit-breaker policy, and a newly added call site cannot be
/// forgotten silently.
/// </summary>
/// <remarks>
/// <para>
/// The expected set is a literal here rather than something discovered from the filesystem, and
/// that is the entire point — see <c>tests/ContractCoverageTests/ContractCoverageScanner.cs</c> for
/// the same reasoning. A scanner that grepped the solution for <c>AddHttpClient(</c> would report
/// full coverage the moment a call site were routed through a shared helper it did not recognise, or
/// would miss one entirely if a call site were deleted without anyone noticing. Adding a call site
/// means editing this list, which is a reviewable decision in the same pull request rather than a
/// silent consequence.
/// </para>
/// <para>
/// Filesystem-only, like <c>tests/StructureConventionTests</c> and
/// <c>tests/ContractCoverageTests</c>: it judges what is present in text, so it deliberately cannot
/// compile against the projects it is judging.
/// </para>
/// </remarks>
public static class ResilienceCoverageScanner
{
    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// Every outbound call site known to exist in the system, matching
    /// specs/020-timeouts-retry-circuit-breaker/contracts/outbound-call-inventory-contract.md.
    /// Populated incrementally as each user story closes the gap its row represents (tasks.md T007,
    /// T015, T020) — a row appears here in the same commit that makes it pass, per the Test-First
    /// discipline that keeps this list red before green rather than backfilled after the fact.
    /// </summary>
    public static IReadOnlyList<OutboundCallSite> ExpectedCallSites { get; } =
    [
        // US1 (tasks.md T007) — timeout tường minh. bff-cluster's ActivityTimeout already exists
        // from 002-gateway-bff-routing; the two IdentityBackchannel rows are the actual gap US1
        // closes (research.md Decision 1 #1 and #3).
        new OutboundCallSite(
            "bff-cluster",
            Caller: "gateway",
            Callee: "bff",
            ConfigurationFile: "services/gateway/src/Gateway.Api/appsettings.json",
            // ActivityTimeout: timeout (US1, already present from 002-gateway-bff-routing).
            // HealthCheck + Passive + AvailableDestinationsPolicy: circuit breaker (US2, tasks.md
            // T016) — the last one is what actually makes an unhealthy destination fail fast
            // instead of YARP's default "HealthyOrPanic" fallback still attempting it.
            RequiredMarkers: ["ActivityTimeout", "HealthCheck", "Passive", "AvailableDestinationsPolicy"]),
        new OutboundCallSite(
            "IdentityBackchannel (gateway)",
            Caller: "gateway",
            Callee: "identity-server",
            ConfigurationFile: "services/gateway/src/Gateway.Api/Identity/ToggleGatedAuthenticationExtensions.cs",
            RequiredMarkers: ["AddIdentityBackchannelResilience"]),
        new OutboundCallSite(
            "IdentityBackchannel (shared)",
            Caller: "bff/parties/products/orders/baskets",
            Callee: "identity-server",
            ConfigurationFile: "shared/Identity/IdentityValidationExtensions.cs",
            RequiredMarkers: ["AddIdentityBackchannelResilience"]),

        // US3 (tasks.md T020) — retry an toàn theo method. Chỉ BasketsApi và OrdersApi có route ghi
        // dữ liệu (POST /basket/items, POST /checkout); ProductsApi và PartiesApi chỉ có route đọc
        // nên không cần dòng riêng ở đây (retry mặc định của chúng đã an toàn, không có gì để phá).
        new OutboundCallSite(
            "BasketsApi",
            Caller: "bff",
            Callee: "baskets",
            ConfigurationFile: "services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs",
            RequiredMarkers: ["SafeToRetryMethods"]),
        new OutboundCallSite(
            "OrdersApi",
            Caller: "bff",
            Callee: "orders",
            ConfigurationFile: "services/bff/src/Bff.Api/DownstreamClients/DownstreamClientRegistrationExtensions.cs",
            RequiredMarkers: ["SafeToRetryMethods"]),
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
    /// Reports every expected call site under <paramref name="repositoryRoot"/> whose configuration
    /// file is missing, or present but missing one of its required markers.
    /// </summary>
    public static CoverageScanResult Scan(string repositoryRoot) =>
        Scan(repositoryRoot, ExpectedCallSites);

    /// <summary>
    /// The same check against a caller-supplied call-site set, so the scanner's own tests can prove
    /// it detects a gap without disturbing the repository.
    /// </summary>
    public static CoverageScanResult Scan(string repositoryRoot, IReadOnlyList<OutboundCallSite> expected)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);
        ArgumentNullException.ThrowIfNull(expected);

        if (!Directory.Exists(repositoryRoot))
        {
            throw new DirectoryNotFoundException($"No repository root at '{repositoryRoot}'.");
        }

        var violations = new List<CoverageViolation>();

        foreach (var callSite in expected)
        {
            var absolute = Path.Combine(
                repositoryRoot,
                callSite.ConfigurationFile.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(absolute))
            {
                violations.Add(new CoverageViolation(
                    callSite.Name,
                    callSite.ConfigurationFile,
                    $"has no configuration file at '{callSite.ConfigurationFile}' — the call site "
                    + "cannot be resilience-covered if the file that would declare its policy does "
                    + "not exist"));
                continue;
            }

            var content = File.ReadAllText(absolute);

            foreach (var marker in callSite.RequiredMarkers)
            {
                if (!content.Contains(marker, StringComparison.Ordinal))
                {
                    violations.Add(new CoverageViolation(
                        callSite.Name,
                        callSite.ConfigurationFile,
                        $"is missing required marker '{marker}' — its resilience policy is either "
                        + "absent or was not declared the way this inventory expects"));
                }
            }
        }

        return new CoverageScanResult(expected, violations);
    }
}
