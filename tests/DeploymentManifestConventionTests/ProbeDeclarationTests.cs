namespace DeploymentManifestConventionTests;

/// <summary>
/// Verifies the invariants in specs/019-liveness-readiness-probes/contracts/probe-manifest-shape.md
/// against every service's rendered Deployment manifest. Written first and expected to fail
/// (deployment.yaml.j2 has no probe block yet) — Constitution Principle III, Test-First.
/// </summary>
public class ProbeDeclarationTests
{
    private static readonly string RepoRoot = ProbeTemplateRenderer.LocateRepositoryRoot();
    private static readonly string[] AllServices =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    [Theory]
    [MemberData(nameof(Services))]
    public void AllServices_DeclareBothProbes(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // FR-001, FR-002, contract invariant #1.
        Assert.NotNull(container.LivenessProbe);
        Assert.NotNull(container.ReadinessProbe);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void Probes_UseCorrectHttpPathAndPort(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);
        var containerPort = Assert.Single(container.Ports).ContainerPort;

        // FR-008 (do not change the existing endpoint contract), invariant #2 and #3.
        Assert.Equal("/health/live", container.LivenessProbe?.HttpGet?.Path);
        Assert.Equal("/health/ready", container.ReadinessProbe?.HttpGet?.Path);
        Assert.Equal(containerPort, container.LivenessProbe?.HttpGet?.Port);
        Assert.Equal(containerPort, container.ReadinessProbe?.HttpGet?.Port);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void LivenessProbe_NeverPointsAtTheDependencyCheckingReadinessPath(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // FR-003, invariant #4: liveness must never be wired to the path that reflects external
        // dependency health, for any service — including the 5 that own a database.
        Assert.NotEqual(container.ReadinessProbe?.HttpGet?.Path, container.LivenessProbe?.HttpGet?.Path);
        Assert.Equal("/health/live", container.LivenessProbe?.HttpGet?.Path);
    }

    [Theory]
    [MemberData(nameof(Services))]
    public void BothProbes_HavePositiveFailureThresholdAndPeriod(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // FR-005, FR-006, invariant #6 — a probe with no valid failure threshold cannot gate
        // traffic or trigger a restart.
        Assert.True(container.ReadinessProbe?.FailureThreshold > 0);
        Assert.True(container.ReadinessProbe?.PeriodSeconds > 0);
        Assert.True(container.LivenessProbe?.FailureThreshold > 0);
        Assert.True(container.LivenessProbe?.PeriodSeconds > 0);
    }

    [Fact]
    public void ReadinessDefaults_DifferByDependencyGroup()
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);

        // FR-004, FR-007: a service that owns a database gets a materially longer readiness
        // failure threshold than a stateless one — reusing docker-compose.yml's SQL Server
        // recovery-time tolerance (research.md Decision 2), not a one-size-fits-all number.
        var dbBackedThreshold = SingleContainer(manifests, "orders").ReadinessProbe!.FailureThreshold;
        var statelessThreshold = SingleContainer(manifests, "gateway").ReadinessProbe!.FailureThreshold;

        Assert.True(
            dbBackedThreshold > statelessThreshold,
            $"Expected the DB-backed group's readiness failureThreshold ({dbBackedThreshold}) to exceed " +
            $"the stateless group's ({statelessThreshold}) — orders waits on SQL Server, gateway does not.");
    }

    public static TheoryData<string> Services() => [.. AllServices];

    private static ContainerSpec SingleContainer(
        IReadOnlyDictionary<string, DeploymentManifest> manifests,
        string serviceName)
    {
        Assert.True(manifests.TryGetValue(serviceName, out var manifest), $"No rendered manifest for '{serviceName}'.");
        return Assert.Single(manifest!.Spec!.Template!.Spec!.Containers);
    }
}
