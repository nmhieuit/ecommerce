namespace DeploymentManifestConventionTests;

/// <summary>
/// FR-006, FR-007 / User Story 3: a hung process must eventually be restarted, and a healthy
/// process starting up normally must not be — both depend on the liveness probe's timing fields
/// being sane, not just present. Written first; expected to fail only if a future edit to
/// defaults/main.yml sets an invalid (zero/negative) value for either field.
/// </summary>
public class LivenessRestartTests
{
    private static readonly string RepoRoot = ProbeTemplateRenderer.LocateRepositoryRoot();

    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void LivenessProbe_HasPositiveFailureThresholdAndPeriod(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        Assert.True(manifests.TryGetValue(serviceName, out var manifest), $"No rendered manifest for '{serviceName}'.");
        var liveness = Assert.Single(manifest!.Spec!.Template!.Spec!.Containers).LivenessProbe;

        Assert.NotNull(liveness);
        Assert.True(liveness!.FailureThreshold > 0, "failureThreshold must be positive or the pod never restarts.");
        Assert.True(liveness.PeriodSeconds > 0, "periodSeconds must be positive or the probe never runs again.");
    }

    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void LivenessProbe_InitialDelayTeleratesNormalStartup(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        Assert.True(manifests.TryGetValue(serviceName, out var manifest), $"No rendered manifest for '{serviceName}'.");
        var liveness = Assert.Single(manifest!.Spec!.Template!.Spec!.Containers).LivenessProbe;

        // research.md Decision 2: liveness never waits on a dependency (FR-003), so it needs only
        // enough delay to cover normal process boot — a few seconds, not the ~10s a database-backed
        // readiness check tolerates. Guards against a future edit silently reusing readiness's much
        // more generous window for liveness, which would mask a genuinely hung process for longer
        // than necessary.
        Assert.NotNull(liveness);
        Assert.InRange(liveness!.InitialDelaySeconds, 1, 15);
    }
}
