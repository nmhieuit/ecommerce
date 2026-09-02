namespace CrossServiceIsolation.Tests;

/// <summary>
/// Spec Test Scenario 3 / SC-003, tenant edition: no code path reaches persistence without a
/// resolved tenant. Constitution Principle V makes that a security boundary, so it is asserted
/// structurally here rather than left to per-service discipline.
/// </summary>
public class TenantGatedConnectionTests
{
    /// <summary>Every service under <c>services/</c>, in the ordinal order the scanner returns them.</summary>
    private static readonly string[] ExpectedServices =
        ["baskets", "bff", "gateway", "identity", "orders", "parties", "products"];

    /// <summary>
    /// The services that own a tenant-partitioned database, gated the way <c>Scan_AcceptsAGatedRegistration</c>
    /// demonstrates. <c>bff</c> and <c>gateway</c> own no database at all (Principle I), so requiring
    /// a tenant-gated registration of them would assert something untrue.
    /// </summary>
    private static readonly string[] DatabaseOwningServices = ["baskets", "orders", "parties", "products"];

    /// <summary>
    /// <c>identity</c> owns a database (014-identity-server-auth's User Story 1, tasks.md T020-T021 —
    /// its configuration/operational store and user credential store) but does not belong in
    /// <see cref="DatabaseOwningServices"/>: it is the <em>source</em> of the tenant claim every other
    /// service's persistence gate requires, not a consumer of one, so its <c>AddDbContext</c>
    /// registrations are correctly ungated (research.md Decision 8). It is excluded from
    /// <see cref="NoStatelessService_RegistersADbContext"/>'s zero-registration requirement below for
    /// the same reason, rather than added to <see cref="DatabaseOwningServices"/>, which would
    /// wrongly assert it has exactly one registration and that it is tenant-gated.
    /// </summary>
    private static readonly string[] TenantAgnosticDatabaseOwningServices = ["identity"];

    [Fact]
    public void EveryDatabaseOwningService_HasExactlyOneDbContextRegistration()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        Assert.All(DatabaseOwningServices, service =>
        {
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            Assert.Equal(1, finding.CallSiteCount);
        });
    }

    [Fact]
    public void EveryDbContextRegistration_IsGatedOnAResolvedTenant()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        Assert.All(DatabaseOwningServices, service =>
        {
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            Assert.Equal(finding.CallSiteCount, finding.GatedCallSiteCount);
        });
    }

    /// <summary>
    /// The other half of the boundary: a service that owns no data must not open a connection at
    /// all, gated or otherwise.
    /// </summary>
    [Fact]
    public void NoStatelessService_RegistersADbContext()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());
        var exempt = DatabaseOwningServices.Concat(TenantAgnosticDatabaseOwningServices);

        Assert.All(
            result.Findings.Where(finding => !exempt.Contains(finding.Service, StringComparer.Ordinal)),
            finding => Assert.Equal(0, finding.CallSiteCount));
    }

    /// <summary>
    /// Guards the assertions above against passing for the wrong reason — a scan that resolved the
    /// wrong directory, or matched no files after a layout change, reports nothing to object to and
    /// is indistinguishable from a compliant repository.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryServicesRegistration()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        Assert.Equal(ExpectedServices, result.ScannedServices);
        Assert.Equal(ExpectedServices.Length, result.Findings.Count);
    }

    [Fact]
    public void Scan_FlagsAnUngatedRegistration()
    {
        using var fixture = new RegistrationFixture();
        fixture.WriteProgram(
            "products",
            "Products",
            """
            builder.Services.AddDbContext<ProductsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("ProductsDb")));
            """);

        var result = TenantGatedConnectionScanner.Scan(fixture.ServicesDirectory);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(1, finding.CallSiteCount);
        Assert.Equal(0, finding.GatedCallSiteCount);
    }

    [Fact]
    public void Scan_AcceptsAGatedRegistration()
    {
        using var fixture = new RegistrationFixture();
        fixture.WriteProgram(
            "products",
            "Products",
            """
            builder.Services.AddDbContext<ProductsDbContext>((serviceProvider, options) =>
            {
                serviceProvider.GetRequiredService<TenantContext>().RequireTenantId();
                options.UseSqlServer(builder.Configuration.GetConnectionString("ProductsDb"));
            });
            """);

        var result = TenantGatedConnectionScanner.Scan(fixture.ServicesDirectory);

        var finding = Assert.Single(result.Findings);
        Assert.Equal(1, finding.CallSiteCount);
        Assert.Equal(1, finding.GatedCallSiteCount);
    }

    /// <summary>
    /// A guard mentioned only in prose must not count. Without this, deleting the gate and leaving
    /// the comment that describes it behind would keep the suite green.
    /// </summary>
    [Fact]
    public void Scan_DoesNotAcceptAGuardThatOnlyAppearsInAComment()
    {
        using var fixture = new RegistrationFixture();
        fixture.WriteProgram(
            "products",
            "Products",
            """
            // Gated by TenantContext.RequireTenantId() before the connection is built.
            builder.Services.AddDbContext<ProductsDbContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("ProductsDb")));
            """);

        var result = TenantGatedConnectionScanner.Scan(fixture.ServicesDirectory);

        Assert.Equal(0, Assert.Single(result.Findings).GatedCallSiteCount);
    }

    /// <summary>
    /// A throwaway <c>services/</c> tree shaped like the real one, so the scanner can be pointed at
    /// a deliberately ungated registration without disturbing the repository.
    /// </summary>
    private sealed class RegistrationFixture : IDisposable
    {
        private readonly string _root = Path.Combine(Path.GetTempPath(), $"tenant-gate-scan-{Guid.NewGuid():N}");

        public string ServicesDirectory => Path.Combine(_root, "services");

        public void WriteProgram(string serviceName, string projectPrefix, string body)
        {
            var projectDirectory = Path.Combine(ServicesDirectory, serviceName, "src", $"{projectPrefix}.Api");
            Directory.CreateDirectory(projectDirectory);
            File.WriteAllText(Path.Combine(projectDirectory, "Program.cs"), body);
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
