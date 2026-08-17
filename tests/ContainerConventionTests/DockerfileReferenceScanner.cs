using System.Text.RegularExpressions;

namespace ContainerConventionTests;

/// <summary>
/// A shared project a service compiles against but its image never receives.
/// </summary>
public sealed record MissingCopyViolation(
    string Service,
    string ProjectFile,
    string DockerfilePath,
    string SharedProject,
    string Reason);

/// <summary>
/// What a scan looked at, not only what it objected to.
/// </summary>
/// <remarks>
/// The same guard <c>ConnectionStringScanner</c> and <c>VerticalSliceStructureScanner</c> apply, and
/// for the same reason: a scan that resolved the wrong directory finds no violations and looks
/// exactly like a compliant repository. The counts are part of the result so a test can assert the
/// scan actually saw the services it was meant to judge.
/// </remarks>
public sealed record DockerfileScanResult(
    IReadOnlyList<string> ScannedServices,
    IReadOnlyList<string> ScannedDockerfiles,
    IReadOnlyList<string> ObservedSharedReferences,
    IReadOnlyList<MissingCopyViolation> Violations);

/// <summary>
/// The repeatable check behind 005-one-command-local-run FR-014: every service image is given every
/// shared project the service compiles against.
/// </summary>
/// <remarks>
/// <para>
/// This exists because five of the six service images could not build for the whole of two
/// features. Each Dockerfile copies <c>shared/ServiceDefaults</c>, and every service except the
/// gateway has referenced <c>shared/Tenancy</c> since 003-stub-identity-tenant-context — but no
/// image copied it, and nothing noticed, because no image had been built since. A restore inside
/// the build stage fails on the missing reference.
/// </para>
/// <para>
/// The check reads two files and compares them: the project references a <c>.csproj</c> declares
/// under <c>shared/</c>, and the paths the Dockerfile copies. It deliberately does not build
/// anything — a build is slow, needs a daemon, and would only tell you that <em>something</em> is
/// wrong. This names the missing project.
/// </para>
/// </remarks>
public static class DockerfileReferenceScanner
{
    private const string ServicesDirectoryName = "services";
    private const string SourceDirectoryName = "src";
    private const string SharedDirectoryName = "shared";
    private const string ApiProjectSuffix = ".Api";
    private const string DockerfileName = "Dockerfile";

    /// <summary>The file that marks the repository root.</summary>
    private const string RepositoryRootMarker = "Ecommerce.slnx";

    /// <summary>
    /// Matches a project reference pointing anywhere under <c>shared/</c>, in either slash style,
    /// capturing the shared project's folder name.
    /// </summary>
    private static readonly Regex SharedProjectReference = new(
        @"ProjectReference\s+Include\s*=\s*""[^""]*[\\/]shared[\\/](?<name>[^\\/""]+)[\\/][^""]+\.csproj""",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Walks up from the test assembly to the repository root, so the scan behaves the same from
    /// the IDE, the CLI, and CI regardless of working directory.
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
    /// Scans every <c>services/*/src/*.Api/</c> project and the Dockerfile beside it.
    /// </summary>
    public static DockerfileScanResult Scan(string repositoryRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repositoryRoot);

        var servicesDirectory = Path.Combine(repositoryRoot, ServicesDirectoryName);

        if (!Directory.Exists(servicesDirectory))
        {
            throw new DirectoryNotFoundException($"No services directory at '{servicesDirectory}'.");
        }

        var services = Directory.GetDirectories(servicesDirectory)
            .Select(Path.GetFileName)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        var scannedDockerfiles = new List<string>();
        var observedReferences = new List<string>();
        var violations = new List<MissingCopyViolation>();

        foreach (var service in services)
        {
            foreach (var projectFile in FindApiProjectFiles(servicesDirectory, service))
            {
                var dockerfile = Path.Combine(Path.GetDirectoryName(projectFile)!, DockerfileName);

                if (!File.Exists(dockerfile))
                {
                    // A service with no image is a separate concern from one with a wrong image.
                    // Reporting it here would conflate "cannot be containerised" with "is
                    // containerised incorrectly"; the test asserts coverage separately.
                    continue;
                }

                scannedDockerfiles.Add(dockerfile);

                var dockerfileText = File.ReadAllText(dockerfile);

                foreach (var sharedProject in ReadSharedReferences(projectFile))
                {
                    observedReferences.Add($"{service} -> {sharedProject}");

                    if (!CopiesSharedProject(dockerfileText, sharedProject))
                    {
                        violations.Add(new MissingCopyViolation(
                            service,
                            projectFile,
                            dockerfile,
                            sharedProject,
                            $"'{service}' compiles against 'shared/{sharedProject}', but its Dockerfile "
                            + $"never copies it — 'dotnet restore' inside the build stage fails on the "
                            + $"missing reference"));
                    }
                }
            }
        }

        return new DockerfileScanResult(services, scannedDockerfiles, observedReferences, violations);
    }

    /// <summary>The shared project folder names a project file references, deduplicated.</summary>
    public static IReadOnlyList<string> ReadSharedReferences(string projectFile) =>
        [.. SharedProjectReference
            .Matches(File.ReadAllText(projectFile))
            .Select(match => match.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// Whether a Dockerfile copies the named shared project's sources.
    /// </summary>
    /// <remarks>
    /// Looks for the directory copy (<c>COPY shared/Tenancy/ shared/Tenancy/</c>) rather than the
    /// project-file copy that precedes it for restore-layer caching. Copying only the
    /// <c>.csproj</c> restores but does not compile, so the directory line is the one that decides
    /// whether the image can actually build.
    /// </remarks>
    private static bool CopiesSharedProject(string dockerfileText, string sharedProject)
    {
        var pattern = new Regex(
            @$"^\s*COPY\s+{SharedDirectoryName}[\\/]{Regex.Escape(sharedProject)}[\\/]\s",
            RegexOptions.IgnoreCase | RegexOptions.Multiline);

        return pattern.IsMatch(dockerfileText);
    }

    private static IEnumerable<string> FindApiProjectFiles(string servicesDirectory, string service)
    {
        var sourceDirectory = Path.Combine(servicesDirectory, service, SourceDirectoryName);

        if (!Directory.Exists(sourceDirectory))
        {
            return [];
        }

        return Directory.GetDirectories(sourceDirectory, $"*{ApiProjectSuffix}")
            .SelectMany(project => Directory.GetFiles(project, "*.csproj"))
            .Order(StringComparer.Ordinal);
    }
}
