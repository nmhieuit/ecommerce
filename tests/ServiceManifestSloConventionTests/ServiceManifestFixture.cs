using YamlDotNet.Serialization;

namespace ServiceManifestSloConventionTests;

/// <summary>One discovered <c>service-manifest.yaml</c>, parsed and tied back to its owning directory.</summary>
/// <param name="ServiceDirectoryName">
/// The folder name directly under <c>services/</c> (e.g. <c>orders</c>) — the source of truth SC-001
/// compares <see cref="ServiceManifestDocument.Service"/>'s <c>name</c> against.
/// </param>
public sealed record DiscoveredServiceManifest(
    string ServiceDirectoryName,
    string FilePath,
    ServiceManifestDocument Document);

/// <summary>
/// Finds and parses every <c>services/*/src/*/service-manifest.yaml</c> — the sole data source for
/// this whole suite (021-declare-service-slos research.md Quyết định 1). No service needs to build
/// or run; this only ever reads files already on disk.
/// </summary>
public static class ServiceManifestFixture
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    private static readonly IDeserializer YamlDeserializer = new DeserializerBuilder()
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>
    /// Repository root, found by walking up from this assembly's location to the marker file.
    /// Deliberately not a ".git" directory check: a git worktree's ".git" is a file, not a
    /// directory, and this suite must resolve correctly whether it runs from the main checkout or a
    /// worktree (same approach as DeploymentManifestConventionTests.ProbeTemplateRenderer).
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
    /// Discovers and parses every <c>service-manifest.yaml</c> under <c>services/&lt;name&gt;/src/*/</c>,
    /// keyed by the service directory name. Throws if a discovered file fails to parse — a broken
    /// manifest is a defect this suite must surface, not silently skip.
    /// </summary>
    public static IReadOnlyDictionary<string, DiscoveredServiceManifest> DiscoverAll(string repositoryRoot)
    {
        var servicesDir = Path.Combine(repositoryRoot, "services");
        var result = new Dictionary<string, DiscoveredServiceManifest>();

        foreach (var serviceDir in Directory.EnumerateDirectories(servicesDir))
        {
            var serviceDirectoryName = Path.GetFileName(serviceDir);
            var manifestPaths = Directory.EnumerateFiles(
                Path.Combine(serviceDir, "src"), "service-manifest.yaml", SearchOption.AllDirectories);

            foreach (var manifestPath in manifestPaths)
            {
                var yaml = File.ReadAllText(manifestPath);
                var document = YamlDeserializer.Deserialize<ServiceManifestDocument>(yaml)
                    ?? throw new InvalidDataException($"'{manifestPath}' did not deserialize to a document.");

                result[serviceDirectoryName] = new DiscoveredServiceManifest(serviceDirectoryName, manifestPath, document);
            }
        }

        return result;
    }
}
