namespace CrossServiceIsolation.Tests;

/// <summary>
/// spec FR-011, SC-005: every service that accepts external traffic registers independent token
/// validation exactly once, and only its two health probes are exempt. Constitution Principle VI
/// makes deny-by-default a hard requirement, so it is asserted structurally here rather than left to
/// per-service discipline — the same reasoning <see cref="TenantGatedConnectionTests"/> already
/// applies to tenant gating.
/// </summary>
public class AuthenticatedByDefaultScannerTests
{
    /// <summary>
    /// Kiểm tra: quét mã nguồn thật — mỗi service trong danh sách `AuthenticatingServices` đăng ký
    /// `AddIdentityValidation()` (hoặc `AddToggleGatedIdentity()` của gateway) ĐÚNG 1 lần.
    /// Lý do: FR-011 — 0 lần nghĩa là service âm thầm chấp nhận request chưa xác thực (đúng lỗ hổng
    /// spec này lấp); ≥ 2 lần nghĩa là đăng ký trùng/xung đột.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — FR-011.
    /// </summary>
    [Fact]
    public void EveryAuthenticatingService_RegistersIdentityValidation_ExactlyOnce()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        // Assert.All(tập hợp, hành động): chạy hành động cho từng phần tử, đỏ nếu bất kỳ phần tử
        // nào không đạt.
        Assert.All(AuthenticatedByDefaultScanner.AuthenticatingServices, service =>
        {
            // Assert.Single(tập hợp, điều kiện): xanh khi có đúng 1 phần tử thoả điều kiện, đỏ khi 0
            // hoặc nhiều hơn — mỗi service phải có đúng 1 kết quả quét.
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — số chỗ đăng ký phải
            // đúng bằng 1.
            Assert.Equal(1, finding.RegistrationCallSiteCount);
        });
    }

    /// <summary>
    /// Kiểm tra: mỗi service đánh dấu ĐÚNG 2 endpoint (`/health/live`, `/health/ready`) là
    /// `[AllowAnonymous]` — không hơn, không kém.
    /// Lý do: research.md Decision 6 — 2 health probe là ngoại lệ tường minh DUY NHẤT; quên đánh dấu
    /// thì Kubernetes không thăm dò được (probe bị 401), đánh dấu thừa 1 endpoint khác thì lỗ hổng
    /// deny-by-default xuất hiện ở đúng chỗ không ai ngờ.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — research.md Decision 6.
    /// </summary>
    [Fact]
    public void EveryAuthenticatingService_MarksExactlyItsTwoHealthProbes_AllowAnonymous()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        Assert.All(AuthenticatedByDefaultScanner.AuthenticatingServices, service =>
        {
            // Assert.Single(tập hợp, điều kiện): xanh khi có đúng 1 phần tử thoả điều kiện.
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            // Assert.NotNull(giá trị): xanh khi khác null — scanner phải tìm thấy file health-check.
            Assert.NotNull(finding.HealthCheckFile);
            // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — đúng 2 endpoint được
            // đánh dấu AllowAnonymous.
            Assert.Equal(2, finding.AllowAnonymousCallSiteCount);
        });
    }

    /// <summary>
    /// Kiểm tra: lượt quét ở 2 test trên thật sự đã xét đủ mọi service trong danh sách, không quét
    /// nhầm chỗ hoặc bỏ sót.
    /// Lý do: scanner trỏ nhầm thư mục hoặc không khớp được file nào sẽ không có gì để phản đối —
    /// trông y hệt 1 repo tuân thủ hoàn toàn, dù thực ra chưa quét được gì.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — FR-011.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryService()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        // Assert.All + Assert.Contains(phần tử, tập hợp): với mỗi service kỳ vọng, phải tìm thấy nó
        // trong danh sách service đã quét; đỏ nếu thiếu 1 service nào đó.
        Assert.All(
            AuthenticatedByDefaultScanner.AuthenticatingServices,
            service => Assert.Contains(result.ScannedServices, scanned => scanned == service));
        // Assert.True(điều kiện, thông báo): xanh khi điều kiện đúng, thông báo hiện khi đỏ. Điều
        // kiện: số kết quả quét được ít nhất bằng số service kỳ vọng.
        Assert.True(
            result.Findings.Count >= AuthenticatedByDefaultScanner.AuthenticatingServices.Length,
            $"Expected at least {AuthenticatedByDefaultScanner.AuthenticatingServices.Length} findings, "
            + $"found {result.Findings.Count}.");
    }
}
