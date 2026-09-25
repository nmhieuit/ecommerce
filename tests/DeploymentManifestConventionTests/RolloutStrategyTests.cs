namespace DeploymentManifestConventionTests;

/// <summary>
/// FR-009 / User Story 2: trong lúc rolling update, pod cũ phải tiếp tục phục vụ cho tới khi pod
/// mới pass readiness. Bảo đảm đó cần <c>maxUnavailable: 0</c> tường minh — mặc định của Kubernetes
/// (25%) sẽ cho phép 1 pod biến mất TRƯỚC KHI pod thay thế sẵn sàng, đúng khoảng trống US2 sinh ra
/// để đóng lại. Viết trước và kỳ vọng đỏ lúc đầu (`deployment.yaml.j2` chưa khai `strategy` nào).
/// </summary>
public class RolloutStrategyTests
{
    private static readonly string RepoRoot = ProbeTemplateRenderer.LocateRepositoryRoot();

    /// <summary>
    /// Kiểm tra: cả 7 service đều khai `strategy.type: RollingUpdate` và
    /// `strategy.rollingUpdate.maxUnavailable: 0`.
    /// Lý do: FR-009 — nếu thiếu khai báo này, Kubernetes dùng mặc định 25%, cho phép rút 1 pod cũ
    /// khỏi traffic trước khi pod mới sẵn sàng — đúng lỗ hổng gây gián đoạn dịch vụ mà US2 phải
    /// đóng lại.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-009, US2.
    /// </summary>
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

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null — phải có khối `strategy`.
        Assert.NotNull(strategy);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("RollingUpdate", strategy!.Type);
        Assert.NotNull(strategy.RollingUpdate);
        Assert.Equal(0, strategy.RollingUpdate!.MaxUnavailable);
    }
}
