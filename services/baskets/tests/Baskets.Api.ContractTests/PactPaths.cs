namespace Baskets.Api.ContractTests;

/// <summary>
/// Where the committed Pact documents live (<c>pacts/</c> at the repository root).
/// </summary>
/// <remarks>
/// Resolved by walking up from the test assembly to the file that marks the repository root, the
/// same way <c>tests/StructureConventionTests</c>' scanner locates <c>services/</c>. A relative
/// path would work from the CLI and break in the IDE, and the whole point of committing these
/// documents is that both write to the one directory a reviewer reads.
/// </remarks>
internal static class PactPaths
{
    private const string RepositoryRootMarker = "Ecommerce.slnx";
    private const string PactDirectoryName = "pacts";

    /// <summary>The absolute path of the repository-root <c>pacts/</c> directory.</summary>
    public static string Directory { get; } = Locate();

    private static string Locate()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, RepositoryRootMarker)))
            {
                return Path.Combine(directory.FullName, PactDirectoryName);
            }
        }

        throw new InvalidOperationException(
            $"Could not locate '{RepositoryRootMarker}' walking up from '{AppContext.BaseDirectory}'.");
    }
}
