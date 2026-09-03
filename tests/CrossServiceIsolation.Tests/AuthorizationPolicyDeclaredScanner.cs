using System.Text.RegularExpressions;

namespace CrossServiceIsolation.Tests;

/// <summary>
/// One mapped HTTP route, and whether it declares an authorization decision (015-deny-by-default-authz,
/// spec FR-001; research.md Decision 2/3).
/// </summary>
public sealed record EndpointAuthorizationFinding(
    string Service,
    string EndpointsFile,
    string RouteCallSite,
    bool DeclaresAuthorizationDecision);

/// <summary>
/// What a scan looked at, not only what it objected to — the same guard
/// <see cref="AuthenticatedByDefaultScanner"/> and its siblings carry.
/// </summary>
public sealed record EndpointAuthorizationScanResult(
    IReadOnlyList<string> ScannedFiles,
    IReadOnlyList<EndpointAuthorizationFinding> Findings);

/// <summary>One <c>IConsumer&lt;T&gt;</c> implementation, and whether it declares a trusted source.</summary>
public sealed record ConsumerTrustFinding(string File, string TypeDeclaration, bool DeclaresTrustedSource);

public sealed record ConsumerTrustScanResult(
    IReadOnlyList<string> ScannedFiles,
    IReadOnlyList<ConsumerTrustFinding> Findings);

/// <summary>
/// The repeatable check behind spec FR-001/FR-002/FR-004/FR-008 (US1/US2): every mapped HTTP route
/// declares an authorization decision explicitly, and every message handler declares a trusted
/// source explicitly — asserted structurally here, mirroring how
/// <see cref="AuthenticatedByDefaultScanner"/> already does the same for authentication wiring
/// (014-identity-server-auth).
/// </summary>
/// <remarks>
/// <para>
/// Reads committed <c>*Endpoints.cs</c> source rather than starting each service and sending real
/// requests — the live behaviour (a token missing the required scope is actually rejected 403) is
/// what each service's own <c>AuthorizationPolicyTests.cs</c> proves instead; this scan proves the
/// declaration exists everywhere it must, which a live HTTP test run against a handful of sampled
/// routes cannot (research.md Decision 3).
/// </para>
/// <para>
/// <c>gateway</c> is deliberately excluded from <see cref="AuthorizingServices"/>: its only
/// non-health route is a single catch-all <c>MapReverseProxy()</c>, which carries no per-route
/// granularity to declare anything on — its authorization decision is the (toggle-gated)
/// <c>FallbackPolicy</c> applied uniformly, already covered by
/// <see cref="AuthenticatedByDefaultScanner"/> (plan.md — scope note). <c>identity</c> is excluded
/// for the same reason 014 excludes it from authentication scanning: it issues tokens, it serves no
/// business endpoints of its own to protect.
/// </para>
/// </remarks>
public static class AuthorizationPolicyDeclaredScanner
{
    private const string ServicesDirectoryName = "services";
    private const string RepositoryRootMarker = "Ecommerce.slnx";
    private const string EndpointsFileSuffix = "Endpoints.cs";
    private const string HealthCheckFileName = "HealthCheckEndpoints.cs";

    private const string RequireAuthorizationCall = ".RequireAuthorization(";
    private const string AllowAnonymousCall = ".AllowAnonymous()";

    /// <summary>Doc-comment marker a trusted <c>IConsumer&lt;T&gt;</c> must carry (contracts/message-handler-authorization-contract.md).</summary>
    private const string TrustedSourceMarker = "Trusted source:";

    private static readonly Regex MappedRoutePattern =
        new(@"\.Map(Get|Post|Put|Delete|Patch)\(", RegexOptions.Compiled);

    private static readonly Regex ConsumerPattern =
        new(@"class\s+(\w+)[^{]*:\s*[^{]*\bIConsumer<", RegexOptions.Compiled);

    /// <summary>
    /// The services whose HTTP routes must each declare an authorization decision — every service
    /// with per-route granularity, per the class remarks.
    /// </summary>
    public static readonly string[] AuthorizingServices = ["baskets", "bff", "orders", "parties", "products"];

    public static string LocateServicesDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return Path.Combine(directory.FullName, ServicesDirectoryName);
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{RepositoryRootMarker}' walking up from '{AppContext.BaseDirectory}'.");
    }

    /// <summary>
    /// Scans every <c>*Endpoints.cs</c> (excluding <c>HealthCheckEndpoints.cs</c>, already covered by
    /// <see cref="AuthenticatedByDefaultScanner"/>'s <c>[AllowAnonymous]</c> count) under each of
    /// <see cref="AuthorizingServices"/>.
    /// </summary>
    public static EndpointAuthorizationScanResult ScanEndpoints(string servicesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servicesDirectory);

        if (!Directory.Exists(servicesDirectory))
        {
            throw new DirectoryNotFoundException($"No services directory at '{servicesDirectory}'.");
        }

        var scannedFiles = new List<string>();
        var findings = new List<EndpointAuthorizationFinding>();

        foreach (var service in AuthorizingServices)
        {
            foreach (var file in FindEndpointsFiles(servicesDirectory, service))
            {
                scannedFiles.Add(file);

                var source = StripComments(File.ReadAllText(file));

                foreach (var (routeCallSite, hasDeclaration) in ExtractMappedRoutes(source))
                {
                    findings.Add(new EndpointAuthorizationFinding(service, file, routeCallSite, hasDeclaration));
                }
            }
        }

        return new EndpointAuthorizationScanResult(scannedFiles, findings);
    }

    /// <summary>
    /// Scans every <c>.cs</c> file under <paramref name="servicesDirectory"/> for
    /// <c>IConsumer&lt;T&gt;</c> implementations (research.md Decision 4). Returns an empty
    /// <see cref="ConsumerTrustScanResult.Findings"/> today — no handler exists yet — but is exercised
    /// against every file in every service, so the first handler added without a trust declaration is
    /// caught immediately.
    /// </summary>
    public static ConsumerTrustScanResult ScanConsumers(string servicesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servicesDirectory);

        if (!Directory.Exists(servicesDirectory))
        {
            throw new DirectoryNotFoundException($"No services directory at '{servicesDirectory}'.");
        }

        var scannedFiles = Directory.GetFiles(servicesDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var findings = new List<ConsumerTrustFinding>();

        foreach (var file in scannedFiles)
        {
            var rawSource = File.ReadAllText(file);

            foreach (Match match in ConsumerPattern.Matches(StripComments(rawSource)))
            {
                // Looked up in the RAW source (comments included) — the trust declaration is a doc
                // comment, which StripComments would otherwise erase before it could be found.
                var precedingText = rawSource[..Math.Min(match.Index, rawSource.Length)];
                var declaresTrust = precedingText.Contains(TrustedSourceMarker, StringComparison.Ordinal);

                findings.Add(new ConsumerTrustFinding(file, match.Value, declaresTrust));
            }
        }

        return new ConsumerTrustScanResult(scannedFiles, findings);
    }

    private static IEnumerable<string> FindEndpointsFiles(string servicesDirectory, string service)
    {
        var sourceDirectory = Path.Combine(servicesDirectory, service, "src");

        return Directory.Exists(sourceDirectory)
            ? Directory.GetFiles(sourceDirectory, $"*{EndpointsFileSuffix}", SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file) && !file.EndsWith(HealthCheckFileName, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
            : [];
    }

    /// <summary>
    /// For each <c>Map(Get|Post|Put|Delete|Patch)(...)</c> call, captures the call site itself and
    /// whether an authorization decision (<c>.RequireAuthorization(</c> or <c>.AllowAnonymous()</c>)
    /// is chained onto it before the statement's closing <c>;</c> — so a declaration counts only when
    /// it protects the route it follows, not merely somewhere later in the file.
    /// </summary>
    private static IEnumerable<(string RouteCallSite, bool HasDeclaration)> ExtractMappedRoutes(string source)
    {
        var searchIndex = 0;

        while (true)
        {
            var match = MappedRoutePattern.Match(source, searchIndex);
            if (!match.Success)
            {
                yield break;
            }

            var open = source.IndexOf('(', match.Index);
            var mapCallEnd = FindMatchingClose(source, open);
            var statementEnd = FindStatementEnd(source, mapCallEnd + 1);

            var chain = source[(mapCallEnd + 1)..Math.Min(statementEnd + 1, source.Length)];
            var hasDeclaration =
                chain.Contains(RequireAuthorizationCall, StringComparison.Ordinal)
                || chain.Contains(AllowAnonymousCall, StringComparison.Ordinal);

            yield return (source[match.Index..(mapCallEnd + 1)], hasDeclaration);

            searchIndex = statementEnd + 1;
        }
    }

    /// <summary>Returns the index of the <c>)</c> matching the <c>(</c> at <paramref name="open"/>.</summary>
    private static int FindMatchingClose(string source, int open)
    {
        var depth = 0;

        for (var i = open; i < source.Length; i++)
        {
            if (source[i] == '(')
            {
                depth++;
            }
            else if (source[i] == ')' && --depth == 0)
            {
                return i;
            }
        }

        return source.Length - 1;
    }

    /// <summary>
    /// Returns the index of the first top-level (paren-depth-zero) <c>;</c> at or after
    /// <paramref name="from"/> — the end of the fluent chain a mapped route's builder may carry.
    /// </summary>
    private static int FindStatementEnd(string source, int from)
    {
        var depth = 0;

        for (var i = from; i < source.Length; i++)
        {
            switch (source[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ';' when depth <= 0:
                    return i;
            }
        }

        return source.Length - 1;
    }

    private static string StripComments(string source) =>
        Regex.Replace(
            source,
            @"//.*?$|/\*.*?\*/",
            string.Empty,
            RegexOptions.Multiline | RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
