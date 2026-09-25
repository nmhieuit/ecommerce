namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 1 (spec 021): mọi service-manifest.yaml khai báo đủ 4 giá trị SLO, không rỗng và không
/// phải giá trị giữ chỗ — contracts/service-manifest-slo-shape.md bất biến 1–3, 6.
/// </summary>
public class SloDeclarationTests
{
    /// <summary>7 service kỳ vọng tồn tại — cùng danh sách mà DeploymentManifestConventionTests dùng.</summary>
    private static readonly string[] ExpectedServiceDirectories =
        ["parties", "products", "baskets", "orders", "identity", "gateway", "bff"];

    private static readonly string[] PlaceholderTokens =
        ["TODO", "TBD", "XXX", "PLACEHOLDER", "N/A", "CHANGEME"];

    /// <summary>
    /// Kiểm tra: lượt quét tìm thấy ĐÚNG 7 file `service-manifest.yaml` của 7 service kỳ vọng — không
    /// thiếu, không thừa.
    /// Lý do: SC-001 — 1 lượt quét âm thầm chỉ xét dưới 7 service sẽ báo "tất cả đạt" trong khi bỏ sót
    /// hẳn 1 service — cùng cái bẫy mà ServiceInventoryTests (spec 019) đã phòng cho inventory triển khai.
    /// Task nguồn: spec 021 (khai báo SLO theo service) — SC-001, US1.
    /// </summary>
    [Fact]
    public void Discovery_FindsExactlyTheSevenExpectedServices()
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi 2 tập hợp (đã sắp xếp) giống hệt nhau, đỏ khi khác.
        Assert.Equal(ExpectedServiceDirectories.OrderBy(s => s), discovered.Keys.OrderBy(s => s));
    }

    /// <summary>
    /// Kiểm tra: mỗi service khai báo đủ cả 4 giá trị SLO (`availability`, `error-rate.max-5xx-ratio`,
    /// `latency.p95`, `latency.p99`), mỗi giá trị không rỗng và không phải token giữ chỗ (`TODO`,
    /// `TBD`, `XXX`, `PLACEHOLDER`, `N/A`, `CHANGEME`).
    /// Lý do: FR-001/US1-KB1 — khai báo thiếu hoặc giữ chỗ không thể đối chiếu với số đo thật, coi như
    /// chưa có cam kết nào.
    /// Task nguồn: spec 021 (khai báo SLO theo service) — FR-001, US1 (bất biến 1–3).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaresAllFourSloValues_NonEmptyAndNotPlaceholder(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        // Assert.True(điều kiện, thông báo): xanh khi service có file manifest; đỏ kèm tên service.
        Assert.True(discovered.ContainsKey(serviceDirectoryName), $"'{serviceDirectoryName}' has no service-manifest.yaml.");
        var slos = discovered[serviceDirectoryName].Document.Slos;

        // Assert.NotNull(giá trị): xanh khi khác null, đỏ khi manifest không có khối `slos:` nào.
        Assert.NotNull(slos);
        AssertPresentAndNotPlaceholder(slos!.Availability, serviceDirectoryName, "slos.availability");
        AssertPresentAndNotPlaceholder(slos.ErrorRate?.MaxFiveXxRatio, serviceDirectoryName, "slos.error-rate.max-5xx-ratio");
        AssertPresentAndNotPlaceholder(slos.Latency?.P95, serviceDirectoryName, "slos.latency.p95");
        AssertPresentAndNotPlaceholder(slos.Latency?.P99, serviceDirectoryName, "slos.latency.p99");
    }

    /// <summary>
    /// Kiểm tra: `service.classification` của mỗi service là 1 trong 2 hồ sơ nền tảng đã biết
    /// (`client-facing-bff`, `internal-service-api`).
    /// Lý do: không có classification hợp lệ thì không có hồ sơ mặc định để đối chiếu ở
    /// SloDefaultComplianceTests. Test này chính là test bắt được lỗi thật lúc viết spec 021:
    /// `identity` từng thiếu hẳn dòng `classification` (test đỏ 1 lần, đã vá).
    /// Task nguồn: spec 021 (khai báo SLO theo service) — FR-002, US1/US2.
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_HasAKnownClassification(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());
        var classification = discovered[serviceDirectoryName].Document.Service?.Classification;

        // Assert.True(điều kiện, thông báo): xanh khi classification khác null và nằm trong danh sách
        // hồ sơ nền tảng; đỏ kèm classification thật và danh sách hợp lệ.
        Assert.True(
            classification is not null && PlatformSloDefaults.ByClassification.ContainsKey(classification),
            $"'{serviceDirectoryName}' has service.classification='{classification}', which is not one of the known " +
            $"platform profiles ({string.Join(", ", PlatformSloDefaults.ByClassification.Keys)}).");
    }

    /// <summary>
    /// Kiểm tra: tên khai báo `service.name` trong manifest khớp đúng tên thư mục của service.
    /// Lý do: contracts bất biến 6, SC-001 — không được có manifest "mồ côi" tự nhận tên khác thư mục
    /// chứa nó (dễ khiến số đo/khai báo bị gán nhầm service).
    /// Task nguồn: spec 021 (khai báo SLO theo service) — SC-001, bất biến 6.
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_DeclaredNameMatchesItsDirectory(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(serviceDirectoryName, discovered[serviceDirectoryName].Document.Service?.Name);
    }

    private static void AssertPresentAndNotPlaceholder(string? value, string serviceDirectoryName, string fieldPath)
    {
        // Assert.False(điều kiện, thông báo): xanh khi giá trị KHÔNG rỗng/trắng; đỏ kèm tên service và
        // đường dẫn trường bị thiếu.
        Assert.False(
            string.IsNullOrWhiteSpace(value),
            $"'{serviceDirectoryName}' is missing a value for '{fieldPath}'.");
        // Assert.False(điều kiện, thông báo): xanh khi giá trị KHÔNG trùng token giữ chỗ nào (không
        // phân biệt hoa thường); đỏ kèm giá trị giữ chỗ tìm thấy.
        Assert.False(
            PlaceholderTokens.Any(token => string.Equals(value, token, StringComparison.OrdinalIgnoreCase)),
            $"'{serviceDirectoryName}' has a placeholder value '{value}' for '{fieldPath}'.");
    }
}
