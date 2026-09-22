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

    /// <summary>
    /// Kiểm tra: mỗi service sở hữu database (baskets, orders, parties, products) có đúng 1 call
    /// site `AddDbContext`.
    /// Lý do: cổng tenant chỉ có ý nghĩa nếu chỉ có 1 điểm duy nhất tạo kết nối — nếu có call site
    /// thứ 2, có thể tồn tại 1 đường tới database không đi qua cổng (research.md Decision 6).
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
    [Fact]
    public void EveryDatabaseOwningService_HasExactlyOneDbContextRegistration()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Chạy kiểm tra cho từng service sở hữu DB (baskets, orders, parties,
        // products);
        Assert.All(DatabaseOwningServices, service =>
        {
            // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
            // nhiều hơn. Có đúng 1 kết quả quét cho service đó rồi trả ra.
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Kỳ vọng đúng 1 chỗ
            // đăng ký DbContext; đỏ khi 0 hoặc nhiều hơn (2 chỗ đăng ký làm cổng tenant khó bảo
            // đảm).
            Assert.Equal(1, finding.CallSiteCount);
        });
    }

    /// <summary>
    /// Kiểm tra: với mỗi service sở hữu database, số call site `AddDbContext` được gate bởi
    /// `RequireTenantId()` bằng đúng số call site.
    /// Lý do: đây là assertion trực tiếp của SC-003 — quét toàn bộ mã nguồn, không có điểm tạo kết
    /// nối lưu trữ nào thiếu cổng tenant. LƯU Ý: test này hiện ĐỎ ở `orders` (kỳ vọng 1 call site
    /// gated, thực tế 0) vì spec 024 đã dời cổng tenant của Orders xuống `OrderEndpoints`; xem
    /// docs/QA/QA_Debt.md, mục 003.
    /// Lưu ý: test này đang ĐỎ ở service `orders` (thực tế 0 chỗ có cổng) vì spec 024 chuyển cổng
    /// xuống `OrderEndpoints.cs`. Xem QA_Debt mục 003.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
    [Fact]
    public void EveryDbContextRegistration_IsGatedOnAResolvedTenant()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt.
        Assert.All(DatabaseOwningServices, service =>
        {
            // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
            // nhiều hơn. Đúng 1 kết quả cho service.
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Số chỗ đăng ký phải
            // bằng số chỗ có cổng RequireTenantId(); đỏ khi có chỗ đăng ký không qua cổng.
            Assert.Equal(finding.CallSiteCount, finding.GatedCallSiteCount);
        });
    }

    /// <summary>
    /// Kiểm tra: các service không sở hữu dữ liệu (bff, gateway) không đăng ký DbContext nào (trừ
    /// `identity` được miễn có chủ đích).
    /// Lý do: nửa còn lại của ranh giới — service không sở hữu dữ liệu thì không được mở kết nối
    /// nào cả, dù có gate hay không (Principle I).
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
    [Fact]
    public void NoStatelessService_RegistersADbContext()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());
        var exempt = DatabaseOwningServices.Concat(TenantAgnosticDatabaseOwningServices);

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt. Với mọi service KHÔNG thuộc nhóm được phép (gateway, bff): số chỗ đăng ký
        // DbContext phải bằng 0 (Assert.Equal(0, thực tế)). Đỏ khi gateway/BFF (không trạng thái)
        // lỡ đăng ký DbContext.
        Assert.All(
            result.Findings.Where(finding => !exempt.Contains(finding.Service, StringComparer.Ordinal)),
            finding => Assert.Equal(0, finding.CallSiteCount));
    }

    /// <summary>
    /// Kiểm tra: lượt quét thật sự duyệt đủ 7 service kỳ vọng (baskets, bff, gateway, identity,
    /// orders, parties, products).
    /// Lý do: chống các assertion phía trên "pass vì sai lý do" — 1 lượt quét trỏ nhầm thư mục,
    /// hoặc không khớp file nào sau khi đổi cấu trúc, sẽ không có gì để phản đối và trông y hệt 1
    /// repository tuân thủ.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryServicesRegistration()
    {
        var result = TenantGatedConnectionScanner.Scan(TenantGatedConnectionScanner.LocateServicesDirectory());

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. So sánh 2 danh sách theo
        // thứ tự (Equal cho tập hợp so từng phần tử); đạt khi scanner đã quét đủ 7 service. Đỏ khi
        // thiếu/dư service (scanner "xanh" vì không quét gì).
        Assert.Equal(ExpectedServices, result.ScannedServices);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Số kết quả bằng số
        // service.
        Assert.Equal(ExpectedServices.Length, result.Findings.Count);
    }

    /// <summary>
    /// Kiểm tra: scanner báo đúng 1 call site và 0 call site được gate khi quét 1 `Program.cs` đăng
    /// ký `AddDbContext` mà không có `RequireTenantId()`.
    /// Lý do: chứng minh scanner thật sự bắt được đăng ký thiếu cổng (kiểm tra chính công cụ kiểm
    /// tra) — dùng cây thư mục tạm, không đụng repository thật.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
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

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var finding = Assert.Single(result.Findings);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Nhận ra 1 chỗ đăng ký.
        Assert.Equal(1, finding.CallSiteCount);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Không chỗ nào được tính
        // là có cổng; đỏ khi scanner bỏ sót lỗi.
        Assert.Equal(0, finding.GatedCallSiteCount);
    }

    /// <summary>
    /// Kiểm tra: scanner báo 1 call site và 1 call site được gate khi `AddDbContext` gọi
    /// `RequireTenantId()` bên trong factory.
    /// Lý do: đối chứng cho test phía trên — scanner không được báo nhầm 1 đăng ký đúng chuẩn là vi
    /// phạm, nếu không SC-003 sẽ luôn đỏ vì lý do không có thật.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
    /// </summary>
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

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var finding = Assert.Single(result.Findings);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(1, finding.CallSiteCount);
        Assert.Equal(1, finding.GatedCallSiteCount);
    }

    /// <summary>
    /// Kiểm tra: đoạn `RequireTenantId()` chỉ xuất hiện trong comment (không nằm trong code) không
    /// được tính là đã gate.
    /// Lý do: thiếu test này, xoá cổng nhưng để lại comment mô tả nó vẫn khiến bộ test xanh —
    /// scanner phải loại comment trước khi đếm.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T028, US2 (SC-003).
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

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Single: 1 kết quả;
        // Equal: số chỗ có cổng phải bằng 0. Đỏ khi scanner bị lừa bởi chú thích.
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
