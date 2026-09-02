using System.Text.RegularExpressions;

namespace CrossServiceIsolation.Tests;

/// <summary>
/// One service's authentication registration and health-probe exemption, as committed source shows
/// it (spec FR-011; research.md Decision 6; SC-005).
/// </summary>
public sealed record AuthenticationFinding(
    string Service,
    string ProgramFile,
    int RegistrationCallSiteCount,
    string? HealthCheckFile,
    int AllowAnonymousCallSiteCount);

/// <summary>
/// What a scan looked at, not only what it objected to — the same guard
/// <see cref="TenantGatedConnectionScanner"/> and <see cref="ConnectionStringScanner"/> carry.
/// </summary>
public sealed record AuthenticationScanResult(
    IReadOnlyList<string> ScannedServices,
    IReadOnlyList<AuthenticationFinding> Findings);

/// <summary>
/// The repeatable check behind spec FR-011/SC-005: every service that accepts external traffic
/// registers independent token validation exactly once, and only its two health probes opt out.
/// </summary>
/// <remarks>
/// <para>
/// Structural, like its siblings — it reads committed <c>Program.cs</c>/<c>HealthCheckEndpoints.cs</c>
/// source rather than starting each service and sending real requests. The live behaviour (a request
/// with no/tampered/expired token is actually rejected) is what each service's own
/// <c>IndependentTokenValidationTests.cs</c> proves instead; this scan proves the wiring exists
/// everywhere it must, which a live HTTP test run against only a sample of services cannot.
/// </para>
/// <para>
/// The gateway registers via its own <c>AddToggleGatedIdentity(...)</c> (research.md Decision 7),
/// not the shared <c>AddIdentityValidation(...)</c> every other service calls directly — both count
/// as "registered" here. <c>identity</c> is deliberately excluded from
/// <see cref="AuthenticatingServices"/>: it issues tokens, it does not validate its own (data-model.md
/// — this service has no tenant/caller context of its own to protect).
/// </para>
/// </remarks>
public static class AuthenticatedByDefaultScanner
{
    private const string ServicesDirectoryName = "services";
    private const string RepositoryRootMarker = "Ecommerce.slnx";
    private const string ProgramFileName = "Program.cs";
    private const string HealthCheckFileName = "HealthCheckEndpoints.cs";

    private static readonly string[] RegistrationCalls = ["AddIdentityValidation(", "AddToggleGatedIdentity("];

    /// <summary>The services this guarantee applies to — every service reachable from outside the cluster.</summary>
    public static readonly string[] AuthenticatingServices =
        ["baskets", "bff", "gateway", "orders", "parties", "products"];

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

    public static AuthenticationScanResult Scan(string servicesDirectory)
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

        var findings = new List<AuthenticationFinding>();

        foreach (var service in services)
        {
            var programFile = FindFile(servicesDirectory, service, ProgramFileName);
            if (programFile is null)
            {
                continue;
            }

            var programSource = StripComments(File.ReadAllText(programFile));
            var registrationCount = RegistrationCalls.Sum(call => CountOccurrences(programSource, call));

            var healthCheckFile = FindFile(servicesDirectory, service, HealthCheckFileName);
            var allowAnonymousCount = healthCheckFile is null
                ? 0
                : CountOccurrences(StripComments(File.ReadAllText(healthCheckFile)), "AllowAnonymous()");

            findings.Add(new AuthenticationFinding(
                service, programFile, registrationCount, healthCheckFile, allowAnonymousCount));
        }

        return new AuthenticationScanResult(services, findings);
    }

    private static string? FindFile(string servicesDirectory, string service, string fileName)
    {
        var sourceDirectory = Path.Combine(servicesDirectory, service, "src");

        return Directory.Exists(sourceDirectory)
            ? Directory.GetFiles(sourceDirectory, fileName, SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Order(StringComparer.Ordinal)
                .FirstOrDefault()
            : null;
    }

    private static int CountOccurrences(string source, string needle)
    {
        var count = 0;
        var index = 0;
        while ((index = source.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
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
