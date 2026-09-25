namespace QueryCoverageTests;

/// <summary>
/// Spec 023 FR-001/FR-002/FR-004/FR-005: endpoint danh sách nào phân trang có ép trần, và điểm gọi
/// truy vấn dữ liệu quan hệ nào dùng tra cứu có giới hạn, trả lời được bằng cách đọc các file khai báo
/// chúng, và mất phủ sóng ở 1 chỗ sẽ bị bắt ngay thay vì để lâu mới nhận ra (spec SC-001, SC-002,
/// SC-003).
/// </summary>
/// <remarks>
/// Mô phỏng theo <c>tests/ResilienceCoverageTests</c>: 1 bộ test quy ước đọc repository chứ không biên
/// dịch cùng nó. Nhờ vậy nó fail với thông báo "endpoint này thiếu marker X" thay vì 1 lỗi biên dịch
/// nêu tên 1 kiểu không liên quan. Giới hạn cố hữu: chỉ kiểm tra sự HIỆN DIỆN của marker (chuỗi) trong
/// file, không kiểm tra marker đó được dùng đúng chỗ — vd `GetProductsByIdsAsync` còn xuất hiện ở route
/// add-item nên vẫn "đạt" dù đường render giỏ đã bị đổi (xem QA_Debt mục 023).
/// </remarks>
public class QueryCoverageTests
{
    /// <summary>
    /// Kiểm tra: quét danh mục `ExpectedListEndpoints` trong repo thật — mọi file khai báo có đủ marker
    /// bắt buộc (`DefaultPageSize`, `MaxPageSize`...).
    /// Lý do: FR-001/FR-004/SC-001/SC-003 — bằng chứng lặp lại được, chạy được trong CI, rằng endpoint
    /// danh sách ĐÃ LIỆT KÊ còn phân trang có ép trần.
    /// Lưu ý: chỉ bắt mất marker ở endpoint ĐÃ có trong danh sách viết tay — endpoint danh sách mới
    /// thêm sau này không tự bị phát hiện.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001/FR-004, SC-001/SC-003.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_ReportsNoViolations_ForCurrentInventory()
    {
        var result = QueryCoverageScanner.ScanListEndpoints(QueryCoverageScanner.LocateRepositoryRoot());

        // Assert.Empty(tập hợp): xanh khi danh sách vi phạm rỗng, đỏ khi có vi phạm.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: lượt quét endpoint danh sách thật sự đã đi qua ĐỦ mọi endpoint trong danh sách kỳ vọng.
    /// Lý do: bảo vệ khẳng định ở test trên khỏi "xanh vì lý do sai" — 1 danh sách rỗng (hoặc bị cắt nhầm)
    /// báo 0 vi phạm và không phân biệt được với phủ sóng thật.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-005, tự bảo vệ cơ chế
    /// rà soát.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_ActuallyExaminesEveryExpectedEndpoint()
    {
        var result = QueryCoverageScanner.ScanListEndpoints(QueryCoverageScanner.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — số endpoint đã quét phải bằng
        // số endpoint kỳ vọng.
        Assert.Equal(
            QueryCoverageScanner.ExpectedListEndpoints.Count, result.ScannedSiteNames.Count);
    }

    /// <summary>
    /// Kiểm tra: quét danh mục `ExpectedBoundedQuerySites` trong repo thật — mọi file có đủ marker (vd
    /// `GetProductsByIdsAsync` trong `BasketsEndpoints.cs`).
    /// Lý do: FR-002/FR-003/SC-002 — điểm gọi truy vấn dữ liệu quan hệ ĐÃ LIỆT KÊ còn dùng tra cứu có
    /// giới hạn.
    /// Lưu ý: kiểm tra chuỗi marker có mặt trong file, KHÔNG kiểm tra nó nằm đúng đường render giỏ — đo
    /// thật: sửa đường render thành `GetProductsAsync(1, 100, …)` mà marker vẫn còn ở route add-item nên
    /// test vẫn xanh.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002/FR-003, SC-002.
    /// </summary>
    [Fact]
    public void ScanBoundedQuerySites_ReportsNoViolations_ForCurrentInventory()
    {
        var result = QueryCoverageScanner.ScanBoundedQuerySites(QueryCoverageScanner.LocateRepositoryRoot());

        // Assert.Empty(tập hợp): xanh khi danh sách vi phạm rỗng, đỏ khi có vi phạm.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Kiểm tra: lượt quét điểm-gọi-có-giới-hạn thật sự đã đi qua ĐỦ mọi điểm trong danh sách kỳ vọng —
    /// cùng cơ chế tự bảo vệ như <see cref="ScanListEndpoints_ActuallyExaminesEveryExpectedEndpoint"/>.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-005, tự bảo vệ cơ chế
    /// rà soát.
    /// </summary>
    [Fact]
    public void ScanBoundedQuerySites_ActuallyExaminesEveryExpectedSite()
    {
        var result = QueryCoverageScanner.ScanBoundedQuerySites(QueryCoverageScanner.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(
            QueryCoverageScanner.ExpectedBoundedQuerySites.Count, result.ScannedSiteNames.Count);
    }

    /// <summary>
    /// Kiểm tra: khi file nguồn của 1 endpoint KHÔNG tồn tại, scanner báo đúng 1 vi phạm nêu tên
    /// endpoint đó.
    /// Lý do: bảo vệ khỏi 1 bộ kiểm tra không thể phát hiện gì — nếu không có test này, 1 cài đặt luôn
    /// trả 0 vi phạm sẽ thoả FR-001/FR-004 mãi mãi.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001/FR-004, tự bảo vệ
    /// cơ chế rà soát.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_DetectsViolation_WhenSourceFileIsMissing()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "does-not-exist",
            SourceFile: "does/not/exist.cs",
            RequiredMarkers: ["DefaultPageSize"]);

        var result = QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc nhiều hơn.
        var violation = Assert.Single(result.Violations);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau — vi phạm phải nêu đúng tên endpoint.
        Assert.Equal("does-not-exist", violation.SiteName);
    }

    /// <summary>
    /// Kiểm tra: file có mặt nhưng chỉ chứa 1 trong 2 marker bắt buộc → scanner báo đúng 1 vi phạm, và
    /// lý do nêu tên marker còn thiếu (`MaxPageSize`).
    /// Lý do: bảo vệ khỏi 1 bộ kiểm tra không thể phát hiện gì; marker thiếu phải hiện thành 1 vi phạm
    /// riêng, không được gộp thành 1 lượt "đạt" im lặng.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001/FR-004, tự bảo vệ
    /// cơ chế rà soát.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "products-listing",
            SourceFile: "CatalogEndpoints.cs",
            RequiredMarkers: ["DefaultPageSize", "MaxPageSize"]);

        // Present, but only one of the two required markers — the missing one must surface as its
        // own violation, not collapse into a silent pass.
        fixture.Write(endpoint.SourceFile, "const int DefaultPageSize = 20;");

        var result = QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]);

        // Assert.Single(tập hợp): xanh khi đúng 1 vi phạm, đỏ khi 0 hoặc nhiều hơn.
        var violation = Assert.Single(result.Violations);
        // Assert.Contains(chuỗi con, chuỗi): xanh khi lý do có nêu `MaxPageSize`, đỏ khi không.
        Assert.Contains("MaxPageSize", violation.Reason);
    }

    /// <summary>
    /// Kiểm tra: file có đủ mọi marker bắt buộc → scanner báo 0 vi phạm.
    /// Lý do phải test: nhánh "xanh thật" — đối xứng với 2 test phát hiện vi phạm, đảm bảo scanner không
    /// báo nhầm cho endpoint cấu hình đúng.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-001/FR-004.
    /// </summary>
    [Fact]
    public void ScanListEndpoints_ReportsNoViolations_WhenFileHasEveryRequiredMarker()
    {
        using var fixture = new RepositoryFixture();

        var endpoint = new ExpectedListEndpoint(
            "products-listing",
            SourceFile: "CatalogEndpoints.cs",
            RequiredMarkers: ["DefaultPageSize", "MaxPageSize"]);

        fixture.Write(endpoint.SourceFile, "const int DefaultPageSize = 20; const int MaxPageSize = 100;");

        // Assert.Empty(tập hợp): xanh khi không có vi phạm.
        Assert.Empty(QueryCoverageScanner.ScanListEndpoints(fixture.Root, [endpoint]).Violations);
    }

    /// <summary>
    /// Kiểm tra: khi `BasketsEndpoints.cs` chỉ còn `GetProductsAsync` (không còn `GetProductsByIdsAsync`),
    /// scanner điểm-gọi-có-giới-hạn báo đúng 1 vi phạm nêu tên `bff-basket-render`. Cùng cơ chế tự bảo
    /// vệ như các test phát hiện vi phạm ở endpoint danh sách.
    /// Task nguồn: spec 023 (rà soát N+1/không giới hạn/thiếu phân trang) — FR-002/FR-003, tự bảo vệ
    /// cơ chế rà soát.
    /// </summary>
    [Fact]
    public void ScanBoundedQuerySites_DetectsViolation_WhenMarkerMissing()
    {
        using var fixture = new RepositoryFixture();

        var site = new ExpectedBoundedQuerySite(
            "bff-basket-render",
            SourceFile: "BasketsEndpoints.cs",
            RequiredMarkers: ["GetProductsByIdsAsync"]);

        fixture.Write(site.SourceFile, "await products.GetProductsAsync(cancellationToken);");

        var result = QueryCoverageScanner.ScanBoundedQuerySites(fixture.Root, [site]);

        // Assert.Single(tập hợp): xanh khi đúng 1 vi phạm, đỏ khi 0 hoặc nhiều hơn.
        var violation = Assert.Single(result.Violations);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau — vi phạm phải nêu đúng tên điểm gọi.
        Assert.Equal("bff-basket-render", violation.SiteName);
    }

    /// <summary>
    /// A throwaway repository root, so the scanner can be pointed at a deliberately incomplete tree
    /// without disturbing the real one.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), $"query-coverage-scan-{Guid.NewGuid():N}");

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
