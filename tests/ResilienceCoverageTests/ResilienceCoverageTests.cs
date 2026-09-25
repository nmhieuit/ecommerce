namespace ResilienceCoverageTests;

/// <summary>
/// Spec 020 FR-007: điểm gọi ra ngoài nào đã khai báo chính sách resilience tường minh có thể trả lời
/// được bằng cách đọc các file khai báo nó, và mất phủ sóng ở 1 điểm sẽ bị bắt ngay thay vì để lâu
/// mới nhận ra (spec SC-001).
/// </summary>
/// <remarks>
/// Mô phỏng theo <c>tests/ContractCoverageTests</c>: 1 bộ test quy ước đọc repository chứ không biên
/// dịch cùng nó. Nhờ vậy nó fail với thông báo "điểm gọi này thiếu marker X" thay vì 1 lỗi biên dịch
/// nêu tên 1 kiểu không liên quan.
/// </remarks>
public class ResilienceCoverageTests
{
    /// <summary>
    /// Kiểm tra: quét danh mục `ExpectedCallSites` hiện có trong repo thật — không có vi phạm nào
    /// (mọi file đều chứa đủ marker bắt buộc).
    /// Lý do: FR-007/SC-001 — bằng chứng lặp lại được, chạy được trong CI, rằng các điểm gọi ĐÃ LIỆT
    /// KÊ đều còn đủ timeout/retry/circuit breaker.
    /// Lưu ý: chỉ bắt được mất marker ở các điểm gọi ĐÃ có trong danh sách viết tay — không tự phát hiện
    /// điểm gọi mới thêm sau này (xem QA_Debt mục 020: `Orders.Api → RabbitMQ` của spec 024).
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-007, SC-001.
    /// </summary>
    [Fact]
    public void Scan_ReportsNoViolations_ForCurrentInventory()
    {
        var result = ResilienceCoverageScanner.Scan(ResilienceCoverageScanner.LocateRepositoryRoot());

        // Assert.Empty(tập hợp): xanh khi danh sách vi phạm rỗng, đỏ khi có ít nhất 1 vi phạm.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: lượt quét thật sự đã đi qua ĐỦ mọi điểm gọi trong danh sách `ExpectedCallSites`.
    /// Lý do: bảo vệ khẳng định ở test trên khỏi "xanh vì lý do sai" — 1 danh sách điểm gọi rỗng (hoặc bị
    /// cắt bớt nhầm) báo 0 vi phạm và không phân biệt được với phủ sóng thật.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-007, tự bảo vệ cơ chế rà soát.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryExpectedCallSite()
    {
        var result = ResilienceCoverageScanner.Scan(ResilienceCoverageScanner.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — số điểm gọi đã quét phải
        // đúng bằng số điểm gọi kỳ vọng.
        Assert.Equal(ResilienceCoverageScanner.ExpectedCallSites.Count, result.ScannedCallSites.Count);
    }

    /// <summary>
    /// Kiểm tra: khi file cấu hình của 1 điểm gọi KHÔNG tồn tại, scanner báo đúng 1 vi phạm nêu tên
    /// điểm gọi đó.
    /// Lý do: bảo vệ khỏi 1 bộ kiểm tra không thể phát hiện gì — nếu không có test này, 1 cài đặt
    /// luôn trả 0 vi phạm sẽ thoả FR-007 mãi mãi.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-007, tự bảo vệ cơ chế rà soát.
    /// </summary>
    [Fact]
    public void Scan_DetectsViolation_WhenConfigurationFileIsMissing()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "does-not-exist",
            Caller: "bff",
            Callee: "nowhere",
            ConfigurationFile: "does/not/exist.cs",
            RequiredMarkers: ["AddStandardResilienceHandler"]);

        var result = ResilienceCoverageScanner.Scan(fixture.Root, [callSite]);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc nhiều
        // hơn — đúng 1 vi phạm cho file thiếu.
        var violation = Assert.Single(result.Violations);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau — vi phạm phải nêu đúng tên điểm gọi.
        Assert.Equal("does-not-exist", violation.CallSite);
    }

    /// <summary>
    /// Kiểm tra: file cấu hình có mặt nhưng chỉ chứa 1 trong 3 marker bắt buộc thì scanner báo đúng 2
    /// vi phạm (mỗi marker thiếu là 1 vi phạm riêng), cùng thuộc điểm gọi đó.
    /// Lý do: bảo vệ khỏi 1 bộ kiểm tra không thể phát hiện gì — cài đặt luôn trả 0 vi phạm sẽ thoả
    /// FR-007 mãi mãi; và mỗi marker thiếu phải hiện thành vi phạm riêng, không gộp thành 1 lỗi chung
    /// chung chung.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-007, tự bảo vệ cơ chế rà soát.
    /// </summary>
    [Fact]
    public void Scan_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "gateway-cluster",
            Caller: "gateway",
            Callee: "bff",
            ConfigurationFile: "config.json",
            RequiredMarkers: ["ActivityTimeout", "HealthCheck", "Passive"]);

        // Có mặt, nhưng chỉ có 1 trong 3 marker bắt buộc — 2 marker thiếu phải mỗi cái hiện thành 1
        // vi phạm riêng, không gộp thành 1 lỗi chung chung.
        fixture.Write(callSite.ConfigurationFile, "{ \"ActivityTimeout\": \"00:00:10\" }");

        var result = ResilienceCoverageScanner.Scan(fixture.Root, [callSite]);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 2 vi phạm.
        Assert.Equal(2, result.Violations.Count);
        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử nào
        // không đạt — mọi vi phạm đều phải thuộc điểm gọi "gateway-cluster".
        Assert.All(result.Violations, violation => Assert.Equal("gateway-cluster", violation.CallSite));
    }

    /// <summary>
    /// Kiểm tra: file có đủ mọi marker bắt buộc thì scanner báo 0 vi phạm.
    /// Lý do phải test: nhánh "xanh thật" — đối xứng với 2 test phát hiện vi phạm ở trên, đảm bảo
    /// scanner không báo vi phạm nhầm cho 1 điểm gọi cấu hình đúng.
    /// Task nguồn: spec 020 (timeout/retry/circuit breaker) — FR-007.
    /// </summary>
    [Fact]
    public void Scan_ReportsNoViolations_WhenFileHasEveryRequiredMarker()
    {
        using var fixture = new RepositoryFixture();

        var callSite = new OutboundCallSite(
            "bff-downstream",
            Caller: "bff",
            Callee: "products",
            ConfigurationFile: "DownstreamClientRegistrationExtensions.cs",
            RequiredMarkers: ["AddStandardResilienceHandler"]);

        fixture.Write(callSite.ConfigurationFile, "services.AddStandardResilienceHandler(...)");

        // Assert.Empty(tập hợp): xanh khi danh sách vi phạm rỗng, đỏ khi có vi phạm.
        Assert.Empty(ResilienceCoverageScanner.Scan(fixture.Root, [callSite]).Violations);
    }

    /// <summary>
    /// 1 repository root tạm thời, để có thể trỏ scanner vào 1 cây thư mục cố ý thiếu sót mà không
    /// làm ảnh hưởng cây thật.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), $"resilience-coverage-scan-{Guid.NewGuid():N}");

        public RepositoryFixture() => Directory.CreateDirectory(Root);

        public void Write(string relativePath, string content)
        {
            var absolute = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, content);
        }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
