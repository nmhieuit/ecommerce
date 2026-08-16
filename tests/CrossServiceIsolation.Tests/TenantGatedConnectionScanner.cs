using System.Text.RegularExpressions;

namespace CrossServiceIsolation.Tests;

/// <summary>
/// One service's <c>AddDbContext</c> call sites, and whether each is gated on a resolved tenant
/// (spec Test Scenario 3 / SC-003; research.md Decision 6).
/// </summary>
public sealed record TenantGateFinding(
    string Service,
    string ConfigurationFile,
    int CallSiteCount,
    int GatedCallSiteCount);

/// <summary>
/// What a scan looked at, not only what it objected to — the same guard the sibling
/// <see cref="ConnectionStringScanner"/> carries, for the same reason: a scan that examined nothing
/// reports no problems and looks exactly like a compliant codebase.
/// </summary>
public sealed record TenantGateScanResult(
    IReadOnlyList<string> ScannedServices,
    IReadOnlyList<TenantGateFinding> Findings);

/// <summary>
/// The repeatable check behind spec Test Scenario 3: each domain service constructs its database
/// connection in exactly one place, and that place refuses to proceed without a resolved tenant.
/// </summary>
/// <remarks>
/// <para>
/// The ticket's literal wording asks for "any repository/DB call that does not require a tenant
/// parameter". This scans the single choke point instead, which research.md Decision 6 argues is
/// the stronger reading: a threaded-through parameter still compiles when someone passes the wrong
/// value, whereas a query cannot obtain a working <c>DbContext</c> at all unless a tenant was
/// already resolved. There is nothing to bypass, so there is nothing to grep for.
/// </para>
/// <para>
/// Structural on purpose — it reads committed source rather than starting a service, so it cannot
/// be satisfied by a service that merely happens not to exercise an ungated path today.
/// </para>
/// </remarks>
public static class TenantGatedConnectionScanner
{
    private const string ServicesDirectoryName = "services";
    private const string RepositoryRootMarker = "Ecommerce.slnx";
    private const string RegistrationFileName = "Program.cs";

    /// <summary>The guard that makes a registration tenant-gated.</summary>
    private const string TenantGuard = "RequireTenantId()";

    private const string RegistrationCall = "AddDbContext";

    /// <summary>
    /// Walks up from the test assembly to the repository root's <c>services/</c> directory, so the
    /// scan works the same from the IDE, the CLI, and CI regardless of working directory.
    /// </summary>
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
    /// Scans every <c>services/*/src/*.Api/Program.cs</c> under <paramref name="servicesDirectory"/>.
    /// </summary>
    public static TenantGateScanResult Scan(string servicesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servicesDirectory);

        if (!Directory.Exists(servicesDirectory))
        {
            throw new DirectoryNotFoundException($"No services directory at '{servicesDirectory}'.");
        }

        var services = Directory.GetDirectories(servicesDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        var findings = new List<TenantGateFinding>();

        foreach (var service in services)
        {
            foreach (var file in FindRegistrationFiles(servicesDirectory, service))
            {
                var source = StripComments(File.ReadAllText(file));
                var callSites = ExtractCallSites(source).ToArray();

                findings.Add(new TenantGateFinding(
                    service,
                    file,
                    callSites.Length,
                    callSites.Count(callSite => callSite.Contains(TenantGuard, StringComparison.Ordinal))));
            }
        }

        return new TenantGateScanResult(services, findings);
    }

    private static IEnumerable<string> FindRegistrationFiles(string servicesDirectory, string service)
    {
        var sourceDirectory = Path.Combine(servicesDirectory, service, "src");

        return Directory.Exists(sourceDirectory)
            ? Directory.GetFiles(sourceDirectory, RegistrationFileName, SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Order(StringComparer.Ordinal)
            : [];
    }

    /// <summary>
    /// Returns the source text of each <c>AddDbContext(...)</c> call, from the call itself to its
    /// matching close parenthesis — so a guard counts only when it sits inside the registration it
    /// is supposed to gate, not merely somewhere in the same file.
    /// </summary>
    private static IEnumerable<string> ExtractCallSites(string source)
    {
        var index = 0;

        while ((index = source.IndexOf(RegistrationCall, index, StringComparison.Ordinal)) >= 0)
        {
            var open = source.IndexOf('(', index);
            if (open < 0)
            {
                yield break;
            }

            var depth = 0;
            var end = open;

            for (; end < source.Length; end++)
            {
                if (source[end] == '(')
                {
                    depth++;
                }
                else if (source[end] == ')' && --depth == 0)
                {
                    break;
                }
            }

            yield return source[index..Math.Min(end + 1, source.Length)];
            index = end;
        }
    }

    /// <summary>
    /// Drops comments before scanning, so a call site cannot be counted — or excused — by prose
    /// that merely mentions <c>AddDbContext</c> or the guard.
    /// </summary>
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
