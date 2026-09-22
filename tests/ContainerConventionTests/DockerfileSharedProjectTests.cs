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

    /// <summary>
    /// Kiểm tra: mọi project `shared/*` mà `.csproj` của một service tham chiếu đều được
    /// `Dockerfile` của service đó `COPY` vào image.
    /// Lý do: FR-014: image phải build được từ bản checkout sạch; so 2 file cho kết quả trong
    /// mili-giây thay vì chờ `docker build` hỏng (lỗi từng tồn tại 2 feature mà không ai biết).
    /// Task nguồn: spec 005 (chạy local một lệnh) — T002, US1 (FR-014).
    /// </summary>
    [Fact]
    public void EveryServiceImage_ReceivesEverySharedProject_ItCompilesAgainst()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        // Assert.Empty(danh sách): xanh khi danh sách rỗng. Ở đây là danh sách vi phạm (Dockerfile
        // thiếu COPY); đỏ khi có ≥ 1 vi phạm, xUnit in ra service và project bị thiếu.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: lượt quét ở test trên thật sự duyệt đủ 7 service và 7 Dockerfile.
    /// Lý do: scanner trỏ nhầm thư mục sẽ báo 0 vi phạm, trông y hệt repo lành mạnh.
    /// Task nguồn: spec 005 — T002/T003, US1 (FR-014).
    /// </summary>
    [Fact]
    public void TheScan_Examined_EveryService()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau (danh sách thì so từng phần tử theo
        // thứ tự). Đỏ khi thêm/mất/đổi tên service so với 7 service kỳ vọng.
        Assert.Equal(ExpectedServices, result.ScannedServices);
        // So 2 số: 7 kỳ vọng với số Dockerfile scanner đọc được; đỏ khi có service thiếu
        // Dockerfile.
        Assert.Equal(ExpectedServices.Length, result.ScannedDockerfiles.Count);
    }

    /// <summary>
    /// Kiểm tra: scanner đọc được tham chiếu `shared/*` từ `.csproj`, và mỗi service đều có ít nhất
    /// 1 tham chiếu.
    /// Lý do: regex ngừng khớp thì scanner "mù": không thấy gì, không phản đối gì, vẫn pass.
    /// Task nguồn: spec 005 — T002/T003, US1 (FR-014).
    /// </summary>
    [Fact]
    public void TheScan_Observed_SharedProjectReferences()
    {
        var result = DockerfileReferenceScanner.Scan(DockerfileReferenceScanner.LocateRepositoryRoot());

        // Assert.NotEmpty(danh sách): xanh khi có ≥ 1 phần tử. Đỏ khi rỗng, nghĩa là scanner không
        // đọc được tham chiếu nào.
        Assert.NotEmpty(result.ObservedSharedReferences);

        // Assert.All(danh sách, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Assert.Contains(danh sách, điều kiện): xanh khi có ≥ 1 phần tử thoả điều
        // kiện. Gộp lại: mỗi service phải có ít nhất 1 tham chiếu "{service} -> ..." (mọi service
        // đều dùng chung wiring telemetry, Principle VII); đỏ và nêu tên service nào thiếu.
        Assert.All(
            ExpectedServices,
            service => Assert.Contains(
                result.ObservedSharedReferences,
                reference => reference.StartsWith($"{service} -> ", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Kiểm tra: với từng service trong baskets, bff, orders, parties, products (mỗi `[InlineData]`
    /// một lần chạy), không có vi phạm nào báo thiếu `COPY shared/Tenancy/`.
    /// Lý do: đây là regression đã sinh ra bộ test: từ spec 003 các service tham chiếu Tenancy
    /// nhưng không Dockerfile nào copy, 5/6 image không build được.
    /// Task nguồn: spec 005 (chạy local một lệnh) — T002, US1 (FR-014).
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

        // Assert.DoesNotContain(danh sách, điều kiện): xanh khi KHÔNG phần tử nào thoả điều kiện
        // (ngược Assert.Contains). Điều kiện: vi phạm của đúng service này về project "Tenancy"; đỏ
        // khi có, tức lỗi lịch sử của spec 003 tái diễn.
        Assert.DoesNotContain(
            result.Violations,
            violation => violation.Service == service && violation.SharedProject == "Tenancy");
    }

    /// <summary>
    /// Kiểm tra: `Gateway.Api.csproj` không tham chiếu `shared/Tenancy`.
    /// Lý do: gateway là ngoại lệ có chủ đích (chỉ sinh header từ claim, không đọc); ghi rõ để test
    /// `[Theory]` phía trên trung thực (5 service, không có gateway).
    /// Task nguồn: spec 005 (chạy local một lệnh) — T002, US1 (FR-014).
    /// </summary>
    [Fact]
    public void TheGateway_DoesNotReferenceTheTenancyLibrary()
    {
        var root = DockerfileReferenceScanner.LocateRepositoryRoot();
        var projectFile = Path.Combine(root, "services", "gateway", "src", "Gateway.Api", "Gateway.Api.csproj");

        // Assert.DoesNotContain(giá trị, tập hợp): xanh khi tập hợp không chứa giá trị. Đỏ khi
        // gateway bắt đầu tham chiếu Tenancy; lúc đó phải thêm InlineData("gateway") vào test
        // Theory phía trên.
        Assert.DoesNotContain("Tenancy", DockerfileReferenceScanner.ReadSharedReferences(projectFile));
    }
}
