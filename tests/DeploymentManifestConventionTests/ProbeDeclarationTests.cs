namespace DeploymentManifestConventionTests;

/// <summary>
/// Xác nhận đúng các bất biến ở
/// specs/019-liveness-readiness-probes/contracts/probe-manifest-shape.md đối với manifest Deployment
/// đã render của MỌI service. Viết trước và kỳ vọng ĐỎ lúc đầu (`deployment.yaml.j2` chưa có khối
/// probe nào) — hiến chương Principle III, Test-First.
/// </summary>
public class ProbeDeclarationTests
{
    private static readonly string RepoRoot = ProbeTemplateRenderer.LocateRepositoryRoot();
    private static readonly string[] AllServices =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    /// <summary>
    /// Kiểm tra: mọi service (cả 7) đều khai báo đủ cả `livenessProbe` lẫn `readinessProbe` trong
    /// manifest Deployment đã render.
    /// Lý do: FR-001/FR-002 — thiếu 1 trong 2 nghĩa là Kubernetes không có cách nào biết service đó
    /// còn sống hay đã sẵn sàng nhận traffic.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-001/FR-002, bất biến #1.
    /// </summary>
    [Theory]
    [MemberData(nameof(Services))]
    public void AllServices_DeclareBothProbes(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi null — probe phải tồn tại.
        Assert.NotNull(container.LivenessProbe);
        Assert.NotNull(container.ReadinessProbe);
    }

    /// <summary>
    /// Kiểm tra: cả 2 probe trỏ đúng path (`/health/live`, `/health/ready`) và đúng cổng container
    /// đã khai báo — không trỏ nhầm sang path/cổng khác.
    /// Lý do: FR-008 — việc thêm khai báo probe không được đổi hành vi/hợp đồng endpoint sức khoẻ đã
    /// có sẵn, chỉ nối dây (wiring) vào manifest; bất biến #2/#3.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-008.
    /// </summary>
    [Theory]
    [MemberData(nameof(Services))]
    public void Probes_UseCorrectHttpPathAndPort(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);
        var containerPort = Assert.Single(container.Ports).ContainerPort;

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("/health/live", container.LivenessProbe?.HttpGet?.Path);
        Assert.Equal("/health/ready", container.ReadinessProbe?.HttpGet?.Path);
        Assert.Equal(containerPort, container.LivenessProbe?.HttpGet?.Port);
        Assert.Equal(containerPort, container.ReadinessProbe?.HttpGet?.Port);
    }

    /// <summary>
    /// Kiểm tra: `livenessProbe` không bao giờ trỏ vào path phản ánh sức khoẻ dependency ngoài
    /// (`/health/ready`) — kể cả ở 5 service sở hữu database riêng.
    /// Lý do: FR-003, bất biến #4 — nếu liveness lỡ trỏ nhầm vào readiness, 1 sự cố tạm thời của
    /// database sẽ khiến Kubernetes khởi động lại 1 tiến trình vốn dĩ vẫn khoẻ mạnh, thay vì chỉ
    /// tạm loại nó khỏi traffic.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-003.
    /// </summary>
    [Theory]
    [MemberData(nameof(Services))]
    public void LivenessProbe_NeverPointsAtTheDependencyCheckingReadinessPath(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi 2 path trùng nhau.
        Assert.NotEqual(container.ReadinessProbe?.HttpGet?.Path, container.LivenessProbe?.HttpGet?.Path);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal("/health/live", container.LivenessProbe?.HttpGet?.Path);
    }

    /// <summary>
    /// Kiểm tra: cả 2 probe đều có `failureThreshold` và `periodSeconds` dương (> 0).
    /// Lý do: FR-005/FR-006, bất biến #6 — 1 probe với ngưỡng lỗi hoặc chu kỳ không hợp lệ (0/âm)
    /// không thể chặn traffic hay kích hoạt restart được, coi như vô hiệu dù có khai báo trên giấy.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-005/FR-006.
    /// </summary>
    [Theory]
    [MemberData(nameof(Services))]
    public void BothProbes_HavePositiveFailureThresholdAndPeriod(string serviceName)
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);
        var container = SingleContainer(manifests, serviceName);

        // Assert.True(điều kiện): xanh khi điều kiện đúng (> 0), đỏ khi sai.
        Assert.True(container.ReadinessProbe?.FailureThreshold > 0);
        Assert.True(container.ReadinessProbe?.PeriodSeconds > 0);
        Assert.True(container.LivenessProbe?.FailureThreshold > 0);
        Assert.True(container.LivenessProbe?.PeriodSeconds > 0);
    }

    /// <summary>
    /// Kiểm tra: ngưỡng `failureThreshold` của readiness ở nhóm có database (`orders`) phải LỚN HƠN
    /// hẳn nhóm không trạng thái (`gateway`).
    /// Lý do: FR-004/FR-007 — service sở hữu database cần dung sai dài hơn để chờ SQL Server phục
    /// hồi sau khởi động lại (tái dùng đúng số liệu đã kiểm chứng qua vận hành từ healthcheck
    /// `docker-compose.yml`, research.md Decision 2), không phải 1 con số chung cho mọi service.
    /// Task nguồn: spec 019 (liveness/readiness probe cho mọi service) — FR-004/FR-007.
    /// </summary>
    [Fact]
    public void ReadinessDefaults_DifferByDependencyGroup()
    {
        var manifests = ManifestFixture.RenderAll(RepoRoot);

        var dbBackedThreshold = SingleContainer(manifests, "orders").ReadinessProbe!.FailureThreshold;
        var statelessThreshold = SingleContainer(manifests, "gateway").ReadinessProbe!.FailureThreshold;

        // Assert.True(điều kiện, thông báo): xanh khi ngưỡng nhóm có database lớn hơn nhóm không
        // trạng thái; đỏ (kèm 2 con số thật) khi ngược lại hoặc bằng nhau.
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
