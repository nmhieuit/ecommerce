namespace CrossServiceIsolation.Tests;

/// <summary>
/// spec FR-001/FR-004 (US1/US2): every mapped HTTP route across the services with per-route
/// granularity declares an authorization decision explicitly — constitution Principle VI makes
/// deny-by-default a hard requirement, so it is asserted structurally here rather than left to
/// per-PR discipline, the same reasoning <see cref="AuthenticatedByDefaultScannerTests"/> already
/// applies to authentication wiring.
/// </summary>
public class AuthorizationPolicyDeclaredScannerTests
{
    /// <summary>
    /// spec Test Scenario 1 (US2): grep all controllers/handlers for missing authorization-equivalent
    /// declarations — expect zero. A route missing both <c>.RequireAuthorization(...)</c> and
    /// <c>.AllowAnonymous()</c> is exactly the gap this feature closes.
    /// </summary>
    [Fact]
    public void EveryMappedRoute_DeclaresAnAuthorizationDecision()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanEndpoints(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        Assert.All(result.Findings, finding => Assert.True(
            finding.DeclaresAuthorizationDecision,
            $"{finding.Service}: {finding.RouteCallSite} in {finding.EndpointsFile} declares neither "
            + "RequireAuthorization(...) nor AllowAnonymous()."));
    }

    /// <summary>
    /// Guards the assertion above against passing for the wrong reason — a scan that resolved the
    /// wrong directory, or matched no files after a layout change, reports nothing to object to and
    /// is indistinguishable from a compliant repository (mirrors
    /// <c>AuthenticatedByDefaultScannerTests.Scan_ActuallyExaminesEveryService</c>).
    /// </summary>
    [Fact]
    public void ScanEndpoints_ActuallyExaminesEveryAuthorizingService()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanEndpoints(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        Assert.All(
            AuthorizationPolicyDeclaredScanner.AuthorizingServices,
            service => Assert.Contains(result.ScannedFiles, file => file.Contains(
                $"{Path.DirectorySeparatorChar}{service}{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)));

        Assert.True(result.Findings.Count > 0, "Expected at least one mapped route across every service.");
    }

    /// <summary>
    /// research.md Decision 4: no <c>IConsumer&lt;T&gt;</c> exists in the repository today — this
    /// passes vacuously, and is the guard that starts failing the moment the first message handler is
    /// added without a trust declaration (contracts/message-handler-authorization-contract.md).
    /// </summary>
    [Fact]
    public void EveryMessageConsumer_DeclaresATrustedSource()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanConsumers(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        Assert.All(result.Findings, finding => Assert.True(
            finding.DeclaresTrustedSource,
            $"{finding.TypeDeclaration} in {finding.File} does not declare a trusted source "
            + "(contracts/message-handler-authorization-contract.md)."));
    }

    /// <summary>
    /// Guards the consumer scan itself against silently examining nothing — it must actually walk
    /// every file under every service, even though it finds zero <c>IConsumer&lt;T&gt;</c> today.
    /// </summary>
    [Fact]
    public void ScanConsumers_ActuallyExaminesEveryService()
    {
        var result = AuthorizationPolicyDeclaredScanner.ScanConsumers(AuthorizationPolicyDeclaredScanner.LocateServicesDirectory());

        Assert.All(
            AuthorizationPolicyDeclaredScanner.AuthorizingServices,
            service => Assert.Contains(result.ScannedFiles, file => file.Contains(
                $"{Path.DirectorySeparatorChar}{service}{Path.DirectorySeparatorChar}",
                StringComparison.Ordinal)));

        Assert.Empty(result.Findings);
    }
}
