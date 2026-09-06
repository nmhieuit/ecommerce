namespace DeploymentManifestConventionTests;

/// <summary>
/// FR-009 / User Story 2: during a rolling update, the old pod keeps serving until the new pod
/// passes readiness. That guarantee needs <c>maxUnavailable: 0</c> — the Kubernetes default (25%)
/// would let a pod go away before its replacement is ready, exactly the gap US2 exists to close.
/// Written first and expected to fail (deployment.yaml.j2 declares no <c>strategy</c> yet).
/// </summary>
public class RolloutStrategyTests
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
    public void RollingUpdateStrategy_KeepsOldPodsServingUntilNewPodReady(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        Assert.True(manifests.TryGetValue(serviceName, out var manifest), $"No rendered manifest for '{serviceName}'.");

        var strategy = manifest!.Spec?.Strategy;

        Assert.NotNull(strategy);
        Assert.Equal("RollingUpdate", strategy!.Type);
        Assert.NotNull(strategy.RollingUpdate);
        Assert.Equal(0, strategy.RollingUpdate!.MaxUnavailable);
    }
}
