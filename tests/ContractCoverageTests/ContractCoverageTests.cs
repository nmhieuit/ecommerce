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
    [Fact]
    public void AllThinSliceBoundaries_HaveAPactFileAndAVerificationTest()
    {
        var result = ContractCoverageScanner.Scan(ContractCoverageScanner.LocateRepositoryRoot());

        Assert.Empty(result.Violations);
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason. A scan that resolved the
    /// wrong root, or an expected-boundary list someone trimmed to make a failure go away, reports
    /// zero violations and is indistinguishable from genuine coverage.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesAllFourExpectedBoundaries()
    {
        var result = ContractCoverageScanner.Scan(ContractCoverageScanner.LocateRepositoryRoot());

        Assert.Equal(4, result.ScannedBoundaries.Count);
    }

    /// <summary>
    /// Guards against a check that cannot detect anything. Without this, an implementation that
    /// always returned zero violations would satisfy FR-009 forever.
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

        Assert.NotEmpty(result.Violations);
        Assert.All(result.Violations, violation => Assert.Equal("BFF-products", violation.Boundary));
    }

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
