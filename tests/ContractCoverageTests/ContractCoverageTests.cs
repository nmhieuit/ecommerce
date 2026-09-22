namespace ContractCoverageTests;

/// <summary>
/// Spec FR-008 and FR-009: which boundaries have contract-test coverage is answerable by listing
/// files, and losing one is caught rather than noticed later (spec SC-001, SC-003, SC-004).
/// </summary>
/// <remarks>
/// Modelled on <c>tests/StructureConventionTests</c>: a convention suite that reads the repository
/// rather than compiling against it. That is what lets it fail with "this boundary has no
/// verification test" instead of a compiler error naming a missing type.
/// </remarks>
public class ContractCoverageTests
{
    /// <summary>
    /// Kiểm tra: quét repo thật (`ContractCoverageScanner`) — cả 4 boundary trong "lát cắt mỏng" đều
    /// phải có đủ file pact VÀ file test verify.
    /// Lý do: đây là cổng coverage tự động mà FR-009/SC-004 đòi hỏi — gỡ 1 file bắt buộc (pact hoặc
    /// test) ở bất kỳ boundary nào phải làm test này đỏ, nêu đích danh boundary và đường dẫn thiếu.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T024/T025, US3 (FR-008, FR-009).
    /// </summary>
    [Fact]
    public void AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest()
    {
        var result = ContractCoverageScanner.Scan(ContractCoverageScanner.LocateRepositoryRoot());

        // Assert.Empty(danh sách): xanh khi rỗng, đỏ khi có ≥ 1 vi phạm. Danh sách ở đây là các
        // boundary thiếu pact hoặc thiếu test verify; đỏ thì xUnit in ra tên boundary + đường dẫn
        // thiếu (kiểu CoverageViolation) chứ không chỉ "test fail" chung chung.
        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason. A scan that resolved the
    /// wrong root, or an expected-boundary list someone trimmed to make a failure go away, reports
    /// zero violations and is indistinguishable from genuine coverage.
    /// </summary>
    /// <summary>
    /// Kiểm tra: lượt quét ở test trên thật sự đã xét đủ 4 boundary kỳ vọng, không quét nhầm chỗ.
    /// Lý do: scanner trỏ nhầm thư mục gốc sẽ tìm thấy 0 boundary và báo 0 vi phạm — trông y hệt
    /// coverage đầy đủ; test này bảo đảm test trên đã thật sự xét đúng thứ nó tuyên bố xét.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T024, US3 (FR-008, FR-009).
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesAllFourExpectedBoundaries()
    {
        var result = ContractCoverageScanner.Scan(ContractCoverageScanner.LocateRepositoryRoot());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng 4 (đúng số
        // boundary của lát cắt mỏng); đỏ khi scanner tìm ra khác 4 (trỏ sai thư mục, hoặc danh sách
        // kỳ vọng trong scanner bị ai đó xén bớt để né 1 lỗi).
        Assert.Equal(4, result.ScannedBoundaries.Count);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-009 forever.
    /// </summary>
    /// <summary>
    /// Kiểm tra: với 1 thư mục giả (không phải repo thật), scanner phải báo vi phạm khi thiếu pact,
    /// khi thiếu test, hoặc khi thiếu cả hai (`[Theory]` chạy 3 lần, mỗi lần 1 tổ hợp thiếu/đủ từ
    /// `[InlineData]`; tổ hợp "có đủ cả 2" không nằm trong bảng này — xem test PASS bên dưới).
    /// Lý do: nếu scanner luôn báo 0 vi phạm (kể cả khi rõ ràng thiếu file) thì FR-009 chỉ tồn tại
    /// trên giấy — test này buộc scanner phải thật sự phát hiện được ít nhất 1 kiểu thiếu sót.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T023, US3 (FR-009).
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Scan_FlagsABoundaryMissingEitherFile(bool writePact, bool writeTest)
    {
        using var fixture = new RepositoryFixture();

        var boundary = new Boundary(
            "BFF-products",
            Consumer: "bff",
            Producer: "products",
            PactFile: "pacts/bff-products.json",
            VerificationTestFile:
                "services/products/tests/Products.Api.ContractTests/ProductsProviderPactTests.cs");

        if (writePact)
        {
            fixture.Write(boundary.PactFile);
        }

        if (writeTest)
        {
            fixture.Write(boundary.VerificationTestFile);
        }

        var result = ContractCoverageScanner.Scan(fixture.Root, [boundary]);

        // Assert.NotEmpty(danh sách): xanh khi có ≥ 1 phần tử, đỏ khi rỗng — thiếu file phải sinh ra
        // ít nhất 1 vi phạm.
        Assert.NotEmpty(result.Violations);
        // Assert.All(danh sách, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Mọi vi phạm sinh ra phải gắn đúng tên boundary "BFF-products" — không báo
        // nhầm sang boundary khác.
        Assert.All(result.Violations, violation => Assert.Equal("BFF-products", violation.Boundary));
    }

    /// <summary>
    /// Kiểm tra: với 1 boundary có đủ cả pact lẫn test verify trong thư mục giả, scanner không báo
    /// vi phạm nào.
    /// Lý do: đối chứng cho test trên — nếu thiếu test này, 1 scanner luôn báo vi phạm (kể cả khi đủ
    /// file) cũng "qua" được test trên mà không ai phát hiện.
    /// Task nguồn: spec 011 (kiểm thử hợp đồng tiêu dùng) — T023, US3 (FR-008, FR-009).
    /// </summary>
    [Fact]
    public void Scan_ReportsNoViolations_WhenBothFilesArePresent()
    {
        using var fixture = new RepositoryFixture();

        var boundary = new Boundary(
            "BasketCheckedOut",
            Consumer: "orders",
            Producer: "baskets",
            PactFile: "pacts/orders-basketcheckedout.json",
            VerificationTestFile:
                "services/baskets/tests/Baskets.Api.ContractTests/BasketCheckedOutProviderPactTests.cs");

        fixture.Write(boundary.PactFile);
        fixture.Write(boundary.VerificationTestFile);

        // Assert.Empty(danh sách): xanh khi rỗng, đỏ khi có ≥ 1 vi phạm.
        Assert.Empty(ContractCoverageScanner.Scan(fixture.Root, [boundary]).Violations);
    }

    /// <summary>
    /// A throwaway repository root, so the scanner can be pointed at a deliberately incomplete tree
    /// without disturbing the real one.
    /// </summary>
    private sealed class RepositoryFixture : IDisposable
    {
        public string Root { get; } =
            Path.Combine(Path.GetTempPath(), $"contract-coverage-scan-{Guid.NewGuid():N}");

        public RepositoryFixture() => Directory.CreateDirectory(Root);

        public void Write(string relativePath)
        {
            var absolute = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            File.WriteAllText(absolute, string.Empty);
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
