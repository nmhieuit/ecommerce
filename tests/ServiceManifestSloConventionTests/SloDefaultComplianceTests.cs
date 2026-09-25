namespace ServiceManifestSloConventionTests;

/// <summary>
/// User Story 2 (spec 021): giá trị SLO khớp hồ sơ mặc định của nền tảng theo classification của
/// service, trừ khi `slos.justification` ghi rõ lý do — contracts/service-manifest-slo-shape.md
/// bất biến 4–5.
/// </summary>
public class SloDefaultComplianceTests
{
    /// <summary>
    /// Kiểm tra: 4 giá trị SLO của mỗi service khớp hồ sơ mặc định theo classification
    /// (`client-facing-bff`: p95 300ms/p99 800ms; `internal-service-api`: p95 150ms/p99 500ms; cùng
    /// availability 99.9% và max-5xx 0.1%); nếu lệch thì `slos.justification` phải có, không rỗng.
    /// Lý do: FR-002/FR-003, US2 — không được tồn tại "tiêu chuẩn ngầm": 1 service có ngân sách khác
    /// chuẩn chung mà không ai biết vì sao. Đã kiểm chứng sống (mutate→đỏ→revert→xanh): đổi p95 của
    /// `orders` từ 150ms sang 50ms (không justification) làm test đỏ đúng cho `orders`.
    /// Lưu ý: chỉ so sánh 4 giá trị cấp service (`slos:`), KHÔNG kiểm tra các giá trị `latency`
    /// theo từng endpoint trong `endpoints:` của manifest.
    /// Task nguồn: spec 021 (khai báo SLO theo service) — FR-002/FR-003, US2 (bất biến 4–5).
    /// </summary>
    [Theory]
    [InlineData("parties")]
    [InlineData("products")]
    [InlineData("baskets")]
    [InlineData("orders")]
    [InlineData("identity")]
    [InlineData("gateway")]
    [InlineData("bff")]
    public void EveryService_MatchesPlatformDefault_OrDocumentsAJustifiedAlternative(string serviceDirectoryName)
    {
        var discovered = ServiceManifestFixture.DiscoverAll(ServiceManifestFixture.LocateRepositoryRoot());
        var manifest = discovered[serviceDirectoryName].Document;
        var slos = manifest.Slos!;
        var classification = manifest.Service!.Classification!;
        var defaultProfile = PlatformSloDefaults.ByClassification[classification];

        var matchesDefault =
            slos.Availability == defaultProfile.Availability
            && slos.ErrorRate?.MaxFiveXxRatio == defaultProfile.MaxFiveXxRatio
            && slos.Latency?.P95 == defaultProfile.LatencyP95
            && slos.Latency?.P99 == defaultProfile.LatencyP99;

        if (matchesDefault)
        {
            // Bất biến 4: khớp mặc định thì justification không bắt buộc — không assert gì thêm.
            return;
        }

        // Bất biến 5: lệch mặc định thì PHẢI có lý do, không rỗng, không placeholder.
        // Assert.False(điều kiện, thông báo): xanh khi `slos.justification` có nội dung (điều kiện
        // "rỗng/trắng" là false); đỏ kèm tên service, classification và hồ sơ mặc định bị lệch.
        Assert.False(
            string.IsNullOrWhiteSpace(slos.Justification),
            $"'{serviceDirectoryName}' declares SLO values that differ from the '{classification}' platform " +
            $"default (availability={defaultProfile.Availability}, error-rate={defaultProfile.MaxFiveXxRatio}, " +
            $"p95={defaultProfile.LatencyP95}, p99={defaultProfile.LatencyP99}) but has no 'slos.justification'.");
    }
}
