namespace ContainerConventionTests;

/// <summary>
/// 005-one-command-local-run FR-014: every service image MUST build from a clean checkout.
/// </summary>
/// <remarks>
/// <para>
/// This suite exists because that was false for two whole features and nothing said so. Every
/// service except the gateway has referenced <c>shared/Tenancy</c> since
/// 003-stub-identity-tenant-context; no Dockerfile copied it; five of six images could not build.
/// It went unnoticed because no image had been built since the reference appeared — the failure
/// only surfaces when someone runs <c>docker build</c>, and until this feature nobody had to.
/// </para>
/// <para>
/// A test that reads the two files and compares them fails in milliseconds on the day the
/// reference is added, which is the difference between a typo and a two-feature-old defect.
/// </para>
/// </remarks>
public class DockerfileSharedProjectTests
{
    /// <summary>Every service that has an image, and therefore must have a correct one.</summary>
    private static readonly string[] ExpectedServices =
        ["baskets", "bff", "gateway", "identity", "orders", "parties", "products"];

    [Fact]
    public void EveryServiceImage_ReceivesEverySharedProject_ItCompilesAgainst()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// The scan must have judged what it claims to judge. A scanner pointed at the wrong directory
    /// reports zero violations and is indistinguishable from a healthy repository — the same trap
    /// <c>ConnectionStringIsolationTests</c> guards against.
    /// </summary>
    [Fact]
    public void TheScan_Examined_EveryService()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        Assert.Equal(ExpectedServices, result.ScannedServices);
        Assert.Equal(ExpectedServices.Length, result.ScannedDockerfiles.Count);
    }

    /// <summary>
    /// And it must have found references to compare. A scanner whose regex stopped matching would
    /// observe nothing, object to nothing, and pass — reporting compliance because it had gone
    /// blind.
    /// </summary>
    [Fact]
    public void TheScan_Observed_SharedProjectReferences()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        Assert.NotEmpty(result.ObservedSharedReferences);

        // Every service uses the shared telemetry wiring; that is what makes it shared
        // (constitution Principle VII).
        Assert.All(
            ExpectedServices,
            service => Assert.Contains(
                result.ObservedSharedReferences,
                reference => reference.StartsWith($"{service} -> ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The specific regression that motivated this suite, named so a failure says what broke rather
    /// than only that something did.
    /// </summary>
    [Theory]
    [InlineData("baskets")]
    [InlineData("bff")]
    [InlineData("orders")]
    [InlineData("parties")]
    [InlineData("products")]
    public void ServicesThatUseTheTenancyLibrary_CopyIt(string service)
    {
        var root = DockerfileReferenceScanner.LocateRepositoryRoot();
        var result = DockerfileReferenceScanner.Scan(root);

        Assert.DoesNotContain(
            result.Violations,
            violation => violation.Service == service && violation.SharedProject == "Tenancy");
    }

    /// <summary>
    /// The gateway is the exception, and stating it keeps the theory above honest: it produces the
    /// tenant and subject headers from claims rather than reading them, so it references only the
    /// telemetry library and its image was never broken.
    /// </summary>
    [Fact]
    public void TheGateway_DoesNotReferenceTheTenancyLibrary()
    {
        var root = DockerfileReferenceScanner.LocateRepositoryRoot();
        var projectFile = Path.Combine(root, "services", "gateway", "src", "Gateway.Api", "Gateway.Api.csproj");

        Assert.DoesNotContain("Tenancy", DockerfileReferenceScanner.ReadSharedReferences(projectFile));
    }
}
