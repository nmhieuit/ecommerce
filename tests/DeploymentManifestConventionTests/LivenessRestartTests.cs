namespace DeploymentManifestConventionTests;

/// <summary>
/// FR-006, FR-007 / User Story 3: 1 tiến trình bị treo cuối cùng phải bị khởi động lại, còn 1 tiến
/// trình khoẻ mạnh đang khởi động bình thường thì KHÔNG — cả 2 đều phụ thuộc vào việc các tham số
/// thời gian của liveness probe hợp lý, không chỉ đơn thuần "có tồn tại". Viết trước; chỉ kỳ vọng
/// đỏ nếu sau này ai đó sửa `defaults/main.yml` thành giá trị không hợp lệ (0/âm) cho 1 trong 2
/// trường.
/// </summary>
public class LivenessRestartTests
{
    private static readonly string RepoRoot = ProbeTemplateRenderer.LocateRepositoryRoot();

    /// <summary>
    /// Kiểm tra: `livenessProbe` của cả 7 service có `failureThreshold` và `periodSeconds` dương.
    /// Lý do: 1 trong 2 giá trị ≤ 0 khiến pod hoặc không bao giờ được coi là "đủ lỗi để restart",
    /// hoặc probe không bao giờ chạy lại lần nữa — cả 2 đều phá vỡ US3 (tự phục hồi khi treo).
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-006/FR-007, US3.
    /// </summary>
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

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null.
        Assert.NotNull(liveness);
        // Assert.True(điều kiện, thông báo): xanh khi > 0; đỏ kèm lý do cụ thể khi không dương.
        Assert.True(liveness!.FailureThreshold > 0, "failureThreshold must be positive or the pod never restarts.");
        Assert.True(liveness.PeriodSeconds > 0, "periodSeconds must be positive or the probe never runs again.");
    }

    /// <summary>
    /// Kiểm tra: `initialDelaySeconds` của liveness nằm trong khoảng 1-15 giây cho cả 7 service.
    /// Lý do: research.md Decision 2 — liveness không bao giờ chờ 1 dependency ngoài (FR-003), nên
    /// chỉ cần đủ thời gian cho tiến trình khởi động bình thường (vài giây), không cần khoảng ~10
    /// giây mà readiness có database phải dung thứ. Test này chặn 1 lần sửa tương lai vô tình dùng
    /// lại cửa sổ rộng rãi hơn nhiều của readiness cho liveness — điều sẽ che giấu 1 tiến trình
    /// thật sự bị treo lâu hơn mức cần thiết trước khi Kubernetes phát hiện ra.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-003, research.md
    /// Decision 2.
    /// </summary>
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

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null.
        Assert.NotNull(liveness);
        // Assert.InRange(giá trị, thấp, cao): xanh khi giá trị nằm trong [1, 15], đỏ khi ngoài
        // khoảng — quá thấp thì probe chạy trước khi tiến trình kịp sẵn sàng, quá cao thì che giấu
        // 1 tiến trình treo thật lâu hơn cần thiết.
        Assert.InRange(liveness!.InitialDelaySeconds, 1, 15);
    }
}
