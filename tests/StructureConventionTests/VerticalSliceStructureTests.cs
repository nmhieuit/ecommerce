namespace StructureConventionTests;

/// <summary>
/// Spec SC-004: a developer can find all the code for one capability in one place. Constitution
/// Principle I makes vertical-slice organisation the platform default, so this is the check that
/// keeps the default from quietly eroding one <c>Services/</c> folder at a time.
/// </summary>
public class VerticalSliceStructureTests
{
    /// <summary>
    /// Every service under <c>services/</c>, in the ordinal order the scanner returns them.
    /// Vertical-slice organisation applies to the edge services too: <c>bff</c> and <c>gateway</c>
    /// own no data, but they still organise what they do have by capability rather than by
    /// technical role, and nothing about them earns an exemption from SC-004.
    /// </summary>
    private static readonly string[] ExpectedServices =
        ["baskets", "bff", "gateway", "identity", "orders", "parties", "products"];

    /// <summary>
    /// Kiểm tra: không project API của service nào có thư mục "lớp kỹ thuật" (`Controllers/`,
    /// `Services/`, `Repositories/`...) ở cấp cao nhất.
    /// Lý do: đây chính là bài kiểm chứng cho SC-004 — tìm toàn bộ code của 1 capability ở đúng 1
    /// chỗ (vertical slice, mặc định của Constitution Principle I), không phải lục qua nhiều thư
    /// mục theo lớp kỹ thuật; nó chặn việc cấu trúc này bị bào mòn dần từng thư mục `Services/`.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
    /// </summary>
    [Fact]
    public void NoService_HasATopLevelTechnicalLayerFolder()
    {
        var result = VerticalSliceStructureScanner.Scan(
            VerticalSliceStructureScanner.LocateServicesDirectory());

        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có. Danh sách vi phạm phải
        // có 0 phần tử. ĐẠT khi không service nào có thư mục lớp kỹ thuật ở cấp cao nhất. ĐỎ khi
        // có: thông báo nêu service và thư mục vi phạm, vd. "parties / Services".
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: scanner thực sự quét đúng cả 7 service (đúng danh sách) và tìm đủ project API
    /// tương ứng cho từng service.
    /// Lý do phải test: bảo vệ assertion ở test phía trên khỏi "pass vì lý do sai" — 1 scanner trỏ
    /// sai thư mục, hoặc không khớp được project nào sau khi đổi cấu trúc thư mục, vẫn báo "0 vi
    /// phạm" và không thể phân biệt được với 1 repo thực sự tổ chức tốt.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
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
    /// Kiểm tra: mỗi service trong 7 service kỳ vọng có ÍT NHẤT 1 thư mục capability dưới
    /// `Features/`.
    /// Lý do: "không có thư mục lớp kỹ thuật" mới chỉ là nửa SC-004 — 1 service không có
    /// `Features/` nào cũng không hề tổ chức theo capability nhưng vẫn "vượt qua" nếu chỉ kiểm tra
    /// những gì KHÔNG được tồn tại.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
    /// </summary>
    [Fact]
    public void EveryService_OrganisesAtLeastOneCapabilityUnderFeatures()
    {
        var result = VerticalSliceStructureScanner.Scan(
            VerticalSliceStructureScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt.
        Assert.All(
            ExpectedServices,
            service => Assert.Contains(
                result.CapabilityFolders,
                folder => folder.Contains(service, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Kiểm tra: với 1 cây thư mục services/ giả, khi project API có 1 thư mục cấp cao nhất trùng
    /// tên lớp kỹ thuật bị cấm (Controllers, Services, Repositories — kể cả viết thường), scanner
    /// phải phát hiện đúng 1 vi phạm, nêu đúng service và đúng tên thư mục vi phạm.
    /// Lý do phải test: đối chứng cho việc scanner thực sự phát hiện được điều cấm — thiếu test
    /// này, 1 scanner luôn trả về "0 vi phạm" (không kiểm tra gì) vẫn làm SC-004 trông như đã đạt
    /// mãi mãi.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
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

    /// <summary>
    /// Kiểm tra: thư mục capability (Features/HealthCheck) cùng các thư mục không thuộc danh sách
    /// cấm (Data, Properties) đặt ở cấp cao nhất không bị báo vi phạm.
    /// Lý do phải test: đảm bảo scanner không quá tay/dương tính giả — chỉ chặn đúng tên thư mục
    /// lớp kỹ thuật bị cấm, không chặn nhầm cấu trúc hợp lệ khác.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
    /// </summary>
    [Fact]
    public void Scan_AllowsCapabilityFoldersAndNonLayerFolders()
    {
        using var fixture = new ServicesDirectoryFixture();
        fixture.WriteService("parties", "Parties", "Features/HealthCheck", "Data", "Properties");

        var result = VerticalSliceStructureScanner.Scan(fixture.ServicesDirectory);

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: 1 thư mục tên kỹ thuật (Services) nằm LỒNG BÊN TRONG 1 capability
    /// (Features/HealthCheck/Services) không bị coi là vi phạm.
    /// Lý do phải test: SC-004 cấm tổ chức code THEO lớp kỹ thuật ở cấp toàn service, chứ không cấm
    /// 1 capability tự tổ chức nội bộ theo cách nó cần — code trong trường hợp này vẫn nằm chung 1
    /// chỗ với tính năng nó phục vụ, đúng tinh thần SC-004.
    /// Task nguồn: spec 001 (dựng khung 4 dịch vụ) — T043, US3.
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
