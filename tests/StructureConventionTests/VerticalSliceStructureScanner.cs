namespace StructureConventionTests;

/// <summary>
/// A generic technical-layer folder found where a capability folder belongs (spec FR-006, SC-004).
/// </summary>
public sealed record StructureViolation(
    string Service,
    string ProjectDirectory,
    string Folder,
    string Reason);

/// <summary>
/// What a scan looked at, not only what it objected to. A scan that resolved the wrong directory
/// finds no technical-layer folders and looks exactly like a well-organised codebase, so the
/// counts are part of the result rather than an afterthought.
/// </summary>
public sealed record StructureScanResult(
    IReadOnlyList<string> ScannedServices,
    IReadOnlyList<string> ScannedProjects,
    IReadOnlyList<string> CapabilityFolders,
    IReadOnlyList<StructureViolation> Violations);

/// <summary>
/// The repeatable check behind spec SC-004: every service's code is organised by capability, not
/// by technical role.
/// </summary>
/// <remarks>
/// <para>
/// Only the direct children of each <c>*.Api</c> project are judged. A <c>Services/</c> folder
/// sitting beside <c>Features/</c> splits one capability across the tree, which is the thing
/// SC-004 forbids; the same name nested inside a single capability keeps that code next to the
/// feature it serves and is nobody else's business.
/// </para>
/// <para>
/// There is deliberately no per-service opt-out switch. FR-006 allows a documented exception and
/// the constitution permits escalation to layered architecture for a service that owns genuine
/// business invariants — but that escalation is supposed to be argued in the feature's
/// <c>plan.md</c> and reviewed, not toggled. Making the exception require an edit here keeps it a
/// conscious, visible decision instead of a configuration flag someone flips in passing.
/// </para>
/// </remarks>
public static class VerticalSliceStructureScanner
{
    /// <summary>
    /// Folder names that organise code by technical role rather than by capability.
    /// </summary>
    private static readonly string[] TechnicalLayerFolders = ["Controllers", "Services", "Repositories"];

    private const string ServicesDirectoryName = "services";
    private const string SourceDirectoryName = "src";
    private const string FeaturesDirectoryName = "Features";
    private const string ApiProjectSuffix = ".Api";

    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

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
    /// Scans every <c>services/*/src/*.Api/</c> project under <paramref name="servicesDirectory"/>.
    /// </summary>
    public static StructureScanResult Scan(string servicesDirectory)
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

        var scannedProjects = new List<string>();
        var capabilityFolders = new List<string>();
        var violations = new List<StructureViolation>();

        foreach (var service in services)
        {
            foreach (var project in FindApiProjects(servicesDirectory, service))
            {
                scannedProjects.Add(project);
                capabilityFolders.AddRange(FindCapabilityFolders(project));

                foreach (var folder in Directory.GetDirectories(project))
                {
                    var name = Path.GetFileName(folder);
                    if (TechnicalLayerFolders.Contains(name, StringComparer.OrdinalIgnoreCase))
                    {
                        violations.Add(new StructureViolation(
                            service,
                            project,
                            name,
                            $"'{name}/' organises code by technical role; a capability's handler and "
                            + $"registration belong together under '{FeaturesDirectoryName}/<Capability>/'"));
                    }
                }
            }
        }

        return new StructureScanResult(services, scannedProjects, capabilityFolders, violations);
    }

    private static IEnumerable<string> FindApiProjects(string servicesDirectory, string service)
    {
        var sourceDirectory = Path.Combine(servicesDirectory, service, SourceDirectoryName);

        return Directory.Exists(sourceDirectory)
            ? Directory.GetDirectories(sourceDirectory, $"*{ApiProjectSuffix}").Order(StringComparer.Ordinal)
            : [];
    }

    private static IEnumerable<string> FindCapabilityFolders(string project)
    {
        var featuresDirectory = Path.Combine(project, FeaturesDirectoryName);

        return Directory.Exists(featuresDirectory)
            ? Directory.GetDirectories(featuresDirectory).Order(StringComparer.Ordinal)
            : [];
    }
}
