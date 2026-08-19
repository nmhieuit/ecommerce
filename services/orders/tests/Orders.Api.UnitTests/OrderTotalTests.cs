using Orders.Api.Data;

namespace Orders.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa research.md Decision 8: "the orders service computes the total from the
/// lines it is sent" — never accepts one. That is what keeps every monetary computation inside a
/// domain service and the aggregation layer above free of arithmetic.
/// </summary>
public class OrderTotalTests
{
    /// <summary>
    /// Incidental to every assertion in this class - the tenant is required to build an order
    /// (006 FR-005) but none of these tests are about it. <see cref="OrderTenantTests"/> is.
    /// </summary>
    private const string Tenant = "contoso";

    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    [Fact]
    public void PlaceFrom_MultipliesQuantityByUnitPrice()
    {
        var order = Order.PlaceFrom([new OrderLine(Notebook, 2, 12.50m)], placedAtUtc: Now, tenantId: Tenant);

        Assert.Equal(25.00m, order.Total);
    }

    [Fact]
    public void PlaceFrom_SumsEveryLine()
    {
        var order = Order.PlaceFrom(
            [new OrderLine(Notebook, 2, 12.50m), new OrderLine(Apron, 1, 34.25m)],
            placedAtUtc: Now,
            tenantId: Tenant);

        // The figure quickstart.md Scenario 5 quotes: two notebooks and an apron.
        Assert.Equal(59.25m, order.Total);
    }

    [Fact]
    public void PlaceFrom_RecordsWhenTheOrderWasPlaced_AndGivesItAnIdentifier()
    {
        var placedAt = new DateTime(2026, 8, 16, 9, 30, 0, DateTimeKind.Utc);

        var order = Order.PlaceFrom([new OrderLine(Notebook, 1, 12.50m)], placedAt, Tenant);

        Assert.Equal(placedAt, order.PlacedAtUtc);
        Assert.NotEqual(Guid.Empty, order.Id);
    }

    /// <summary>
    /// An order with no lines would be an order for nothing, at a total of zero. The empty-basket
    /// rule (spec FR-008) is enforced at three layers, and this is the innermost of them.
    /// </summary>
    [Fact]
    public void PlaceFrom_Rejects_AnEmptyLineSet()
    {
        Assert.Throws<ArgumentException>(() => Order.PlaceFrom([], Now, Tenant));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void PlaceFrom_Rejects_ALineWithANonPositiveQuantity(int quantity)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, quantity, 12.50m)], Now, Tenant));
    }

    [Fact]
    public void PlaceFrom_Rejects_ALineWithANegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => Order.PlaceFrom([new OrderLine(Notebook, 1, -0.01m)], Now, Tenant));
    }

    /// <summary>
    /// Decimal arithmetic, not floating point — the same sum in doubles produces
    /// 0.30000000000000004, and an order total a cent out is a defect a shopper will notice on
    /// their confirmation.
    /// </summary>
    [Fact]
    public void PlaceFrom_IsExact_ForAmountsThatFloatingPointWouldRound()
    {
        var order = Order.PlaceFrom(
            [new OrderLine(Notebook, 1, 0.10m), new OrderLine(Apron, 1, 0.20m)],
            Now,
            Tenant);

        Assert.Equal(0.30m, order.Total);
    }

    private static DateTime Now => new(2026, 8, 16, 12, 0, 0, DateTimeKind.Utc);
}
