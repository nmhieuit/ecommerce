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
    /// spec FR-011: every service in <see cref="AuthenticatedByDefaultScanner.AuthenticatingServices"/>
    /// registers <c>AddIdentityValidation()</c> (or the gateway's <c>AddToggleGatedIdentity()</c>)
    /// exactly once. Two or more would mean a duplicate/conflicting registration; zero would mean the
    /// service silently accepts unauthenticated requests — the exact gap this feature closes.
    /// </summary>
    [Fact]
    public void EveryAuthenticatingService_RegistersIdentityValidation_ExactlyOnce()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        Assert.All(AuthenticatedByDefaultScanner.AuthenticatingServices, service =>
        {
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            Assert.Equal(1, finding.RegistrationCallSiteCount);
        });
    }

    /// <summary>
    /// research.md Decision 6: exactly the two health probes are the explicit exception — this fails
    /// if a service forgets to mark them (probes start failing with 401 in production) just as much
    /// as if some other endpoint were marked anonymous by mistake.
    /// </summary>
    [Fact]
    public void EveryAuthenticatingService_MarksExactlyItsTwoHealthProbes_AllowAnonymous()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        Assert.All(AuthenticatedByDefaultScanner.AuthenticatingServices, service =>
        {
            var finding = Assert.Single(result.Findings, item => item.Service == service);
            Assert.NotNull(finding.HealthCheckFile);
            Assert.Equal(2, finding.AllowAnonymousCallSiteCount);
        });
    }

    /// <summary>
    /// Guards the assertions above against passing for the wrong reason — a scan that resolved the
    /// wrong directory, or matched no files after a layout change, reports nothing to object to and
    /// is indistinguishable from a compliant repository.
    /// </summary>
    [Fact]
    public void Scan_ActuallyExaminesEveryService()
    {
        var result = AuthenticatedByDefaultScanner.Scan(AuthenticatedByDefaultScanner.LocateServicesDirectory());

        Assert.All(
            AuthenticatedByDefaultScanner.AuthenticatingServices,
            service => Assert.Contains(result.ScannedServices, scanned => scanned == service));
        Assert.True(
            result.Findings.Count >= AuthenticatedByDefaultScanner.AuthenticatingServices.Length,
            $"Expected at least {AuthenticatedByDefaultScanner.AuthenticatingServices.Length} findings, "
            + $"found {result.Findings.Count}.");
    }
}
