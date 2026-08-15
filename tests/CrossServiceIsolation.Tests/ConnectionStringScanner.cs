using System.Data.Common;
using System.Text.Json;

namespace CrossServiceIsolation.Tests;

/// <summary>
/// One configured connection string that reaches, names, or claims a database belonging to a
/// service other than the one whose configuration declares it (spec FR-004, FR-005).
/// </summary>
public sealed record ConnectionStringViolation(
    string OwningService,
    string ConfigurationFile,
    string ConnectionStringKey,
    string ForeignService,
    string Reason);

/// <summary>
/// What a scan looked at, not just what it objected to. The counts exist so a scan that silently
/// examined nothing — wrong root directory, renamed folder, changed file layout — fails loudly
/// instead of reporting a clean bill of health it never earned.
/// </summary>
public sealed record ScanResult(
    IReadOnlyList<string> ScannedServices,
    IReadOnlyList<string> ScannedConfigurationFiles,
    int ScannedConnectionStringCount,
    IReadOnlyList<ConnectionStringViolation> Violations);

/// <summary>
/// The repeatable check behind spec SC-003: reads every service's committed configuration and
/// reports any connection string that names another service's database.
/// </summary>
/// <remarks>
/// Deliberately structural. It reads files rather than starting services, so it cannot be
/// satisfied by a service that merely happens not to use a connection it was handed, and it
/// discovers services from the directory layout rather than a hardcoded list — logistics and
/// invoices are covered the day their folders appear, with no change here.
/// </remarks>
public static class ConnectionStringScanner
{
    private const string ServicesDirectoryName = "services";

    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>Connection-string keys that name a host, in the spellings SQL Server accepts.</summary>
    private static readonly string[] ServerKeys = ["Server", "Data Source", "Address", "Addr", "Network Address"];

    /// <summary>Connection-string keys that name a database, in the spellings SQL Server accepts.</summary>
    private static readonly string[] DatabaseKeys = ["Database", "Initial Catalog"];

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
    /// Scans every service under <paramref name="servicesDirectory"/> and reports the connection
    /// strings that cross a service boundary.
    /// </summary>
    public static ScanResult Scan(string servicesDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(servicesDirectory);

        if (!Directory.Exists(servicesDirectory))
        {
            throw new DirectoryNotFoundException($"No services directory at '{servicesDirectory}'.");
        }

        var services = Directory.GetDirectories(servicesDirectory)
            .Select(directory => Path.GetFileName(directory))
            .Order(StringComparer.Ordinal)
            .ToArray();

        var scannedFiles = new List<string>();
        var violations = new List<ConnectionStringViolation>();
        var connectionStringCount = 0;

        foreach (var service in services)
        {
            // Every appsettings file, not just appsettings.json: a boundary leak in a
            // per-environment override is exactly as real as one in the base file.
            var configurationFiles = Directory.GetFiles(
                Path.Combine(servicesDirectory, service),
                "appsettings*.json",
                SearchOption.AllDirectories)
                .Where(file => !IsBuildOutput(file))
                .Order(StringComparer.Ordinal);

            foreach (var file in configurationFiles)
            {
                scannedFiles.Add(file);

                foreach (var (key, value) in ReadConnectionStrings(file))
                {
                    connectionStringCount++;
                    var foreignServices = services.Where(other =>
                        !other.Equals(service, StringComparison.OrdinalIgnoreCase));

                    foreach (var other in foreignServices)
                    {
                        var reason = DescribeViolation(key, value, other);
                        if (reason is not null)
                        {
                            violations.Add(new ConnectionStringViolation(service, file, key, other, reason));
                        }
                    }
                }
            }
        }

        return new ScanResult(services, scannedFiles, connectionStringCount, violations);
    }

    /// <summary>
    /// Returns why this connection string breaches <paramref name="foreignService"/>'s boundary,
    /// or <see langword="null"/> if it does not.
    /// </summary>
    private static string? DescribeViolation(string key, string value, string foreignService)
    {
        // A service declaring another service's key is a leak even if the value is later
        // overridden — the credential is present in this service's configuration surface.
        if (key.StartsWith(foreignService, StringComparison.OrdinalIgnoreCase))
        {
            return $"declares connection-string key '{key}', which belongs to the {foreignService} service";
        }

        var parts = ParseConnectionString(value);

        var database = FirstValue(parts, DatabaseKeys);
        if (MatchesService(database, foreignService))
        {
            return $"names database '{database}', which belongs to the {foreignService} service";
        }

        var server = FirstValue(parts, ServerKeys);
        if (MatchesService(StripPort(server), foreignService))
        {
            return $"points at host '{server}', which belongs to the {foreignService} service";
        }

        return null;
    }

    /// <summary>
    /// Matches a service's own database or host naming — bare (<c>orders</c>) or container-suffixed
    /// (<c>orders-db</c>). Compared whole rather than by substring, so <c>parties</c> is never
    /// mistaken for a hit inside an unrelated longer name.
    /// </summary>
    private static bool MatchesService(string? candidate, string service) =>
        !string.IsNullOrWhiteSpace(candidate)
        && (candidate.Equals(service, StringComparison.OrdinalIgnoreCase)
            || candidate.Equals($"{service}-db", StringComparison.OrdinalIgnoreCase));

    /// <summary>Drops the <c>,1433</c> port suffix SQL Server connection strings allow on a host.</summary>
    private static string? StripPort(string? server) =>
        server?.Split(',', StringSplitOptions.TrimEntries)[0];

    private static string? FirstValue(DbConnectionStringBuilder parts, string[] keys)
    {
        foreach (var key in keys)
        {
            if (parts.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text))
            {
                return text.Trim();
            }
        }

        return null;
    }

    private static DbConnectionStringBuilder ParseConnectionString(string value)
    {
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = value;
        }
        catch (ArgumentException)
        {
            // Unparseable configuration is a separate problem; for boundary purposes an
            // unreadable string simply yields no host and no database to object to.
        }

        return builder;
    }

    private static IEnumerable<KeyValuePair<string, string>> ReadConnectionStrings(string file)
    {
        using var document = JsonDocument.Parse(
            File.ReadAllText(file),
            new JsonDocumentOptions { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true });

        if (!document.RootElement.TryGetProperty("ConnectionStrings", out var section)
            || section.ValueKind != JsonValueKind.Object)
        {
            yield break;
        }

        foreach (var property in section.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String)
            {
                yield return new KeyValuePair<string, string>(property.Name, property.Value.GetString() ?? string.Empty);
            }
        }
    }

    private static bool IsBuildOutput(string file) =>
        file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal);
}
