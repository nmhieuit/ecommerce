using Orders.Api.Data;

namespace Orders.Api.UnitTests;

/// <summary>
/// 006-e2e-order-demo FR-005: an order records the tenant it was placed for.
/// </summary>
/// <remarks>
/// <para>
/// The tenant is a constructor-level requirement rather than a property something sets afterwards,
/// for the same reason the total is computed rather than accepted: an order that exists without one
/// is a row nobody can attribute, and the window in which it could exist is the window in which it
/// could be saved.
/// </para>
/// <para>
/// Blank counts as absent here, exactly as it does in <c>TenantContext.RequireTenantId</c>. A tenant
/// whose identifier is an empty string is not a tenant, and accepting one would satisfy the schema
/// while defeating the point (constitution Principle V).
/// </para>
/// </remarks>
public class OrderTenantTests
{
    private const string Tenant = "contoso";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public void PlaceFrom_RecordsTheTenantItWasPlacedFor()
    {
        var order = Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], Now, Tenant);

        Assert.Equal(Tenant, order.TenantId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void PlaceFrom_Rejects_AnAbsentOrBlankTenant(string? tenantId)
    {
        Assert.Throws<ArgumentException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], Now, tenantId!));
    }

    /// <summary>
    /// The tenant is not validated at the expense of the rules that were already there: a request
    /// that is wrong in two ways is still rejected, whichever check runs first.
    /// </summary>
    [Fact]
    public void PlaceFrom_StillRejects_AnEmptyLineSet_EvenWithATenant()
    {
        Assert.Throws<ArgumentException>(() => Order.PlaceFrom([], Now, Tenant));
    }

    private static DateTime Now => new(2026, 8, 19, 12, 0, 0, DateTimeKind.Utc);
}
