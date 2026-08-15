namespace StructureConventionTests;

/// <summary>
/// Spec SC-004: a developer can find all the code for one capability in one place. Constitution
/// Principle I makes vertical-slice organisation the platform default, so this is the check that
/// keeps the default from quietly eroding one <c>Services/</c> folder at a time.
/// </summary>
public class VerticalSliceStructureTests
{
    private static readonly string[] ExpectedServices = ["baskets", "orders", "parties", "products"];

    [Fact]
    public void NoService_HasATopLevelTechnicalLayerFolder()
    {
        var result = VerticalSliceStructureScanner.Scan(
            VerticalSliceStructureScanner.LocateServicesDirectory());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason. A scan that resolved the
    /// wrong directory, or matched no projects after a layout change, reports zero violations and
    /// is indistinguishable from a genuinely well-organised repository.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryServicesApiProject()
    {
        var result = VerticalSliceStructureScanner.Scan(
            VerticalSliceStructureScanner.LocateServicesDirectory());

        Assert.Equal(ExpectedServices, result.ScannedServices);
        Assert.Equal(ExpectedServices.Length, result.ScannedProjects.Count);
    }

    /// <summary>
    /// Absence of technical-layer folders is only half of SC-004. A service with no
    /// <c>Features/</c> folder at all has nothing organised by capability either, and would sail
    /// through a check that only looked for what must not exist.
    /// </summary>
    [Fact]
    public void EveryService_OrganisesAtLeastOneCapabilityUnderFeatures()
    {
        var result = VerticalSliceStructureScanner.Scan(
            VerticalSliceStructureScanner.LocateServicesDirectory());

        Assert.All(
            ExpectedServices,
            service => Assert.Contains(
                result.CapabilityFolders,
                folder => folder.Contains(service, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy SC-004 forever.
    /// </summary>
    [Theory]
    [InlineData("Controllers")]
    [InlineData("Services")]
    [InlineData("Repositories")]
    [InlineData("repositories")]
    public void Scan_FlagsATopLevelTechnicalLayerFolder(string bannedFolder)
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", "Features/HealthCheck", bannedFolder);

        var result = VerticalSliceStructureScanner.Scan(fixture.ServicesDirectory);

        var violation = Assert.Single(result.Violations);
        Assert.Equal("parties", violation.Service);
        Assert.Equal(bannedFolder, violation.Folder);
    }

    [Fact]
    public void Scan_AllowsCapabilityFoldersAndNonLayerFolders()
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", "Features/HealthCheck", "Data", "Properties");

        var result = VerticalSliceStructureScanner.Scan(fixture.ServicesDirectory);

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// A technical-layer name nested inside one capability is that capability's business, not a
    /// platform-wide layering violation — the code still lives with the feature it serves.
    /// </summary>
    [Fact]
    public void Scan_AllowsATechnicalNameNestedInsideACapability()
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", "Features/HealthCheck/Services");

        var result = VerticalSliceStructureScanner.Scan(fixture.ServicesDirectory);

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// A throwaway <c>services/</c> tree shaped exactly like the real one, so the scanner can be
    /// pointed at a deliberately badly-organised service without disturbing the repository.
    /// </summary>
    private sealed class ServicesDirectoryFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"structure-scan-{Guid.NewGuid():N}");

        public string ServicesDirectory => Path.Combine(_root, "services");

        public void WriteService(string serviceName, string projectPrefix, params string[] folders)
        {
            var projectDirectory = Path.Combine(ServicesDirectory, serviceName, "src", $"{projectPrefix}.Api");
            foreach (var folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(projectDirectory, folder.Replace('/', Path.DirectorySeparatorChar)));
            }
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}
