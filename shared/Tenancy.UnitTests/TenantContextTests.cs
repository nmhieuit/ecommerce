namespace Tenancy.UnitTests;

/// <summary>
/// Spec FR-004/FR-005 and Test Scenario 2: a tenant is either resolved or it is an error to
/// proceed. data-model.md gives <c>TenantContext</c> exactly two states — Resolved and Unresolved —
/// and this suite is what keeps a third "resolved to a default" state from quietly appearing.
/// </summary>
public class TenantContextTests
{
    [Fact]
    public void RequireTenantId_ReturnsTheResolvedTenant_WhenOneHasBeenSet()
    {
        var context = new TenantContext { TenantId = "acme" };

        Assert.Equal("acme", context.RequireTenantId());
    }

    [Fact]
    public void TenantContext_IsUnresolved_BeforeAnythingSetsIt()
    {
        var context = new TenantContext();

        Assert.Null(context.TenantId);
    }

    [Fact]
    public void RequireTenantId_Throws_WhenNoTenantHasBeenResolved()
    {
        var context = new TenantContext();

        Assert.Throws<MissingTenantContextException>(() => context.RequireTenantId());
    }

    /// <summary>
    /// data-model.md is explicit that there is no "empty but present" state: a blank tenant
    /// identifier is Unresolved, not a tenant whose name happens to be blank. Without this, an
    /// empty <c>X-Tenant-Id</c> header would sail past the guard and reach persistence.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RequireTenantId_Throws_WhenTheResolvedTenantIsBlank(string blank)
    {
        var context = new TenantContext { TenantId = blank };

        Assert.Throws<MissingTenantContextException>(() => context.RequireTenantId());
    }
}
