using Baskets.Api.Data;

namespace Baskets.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa data-model.md: the basket total is the sum of quantity × captured unit
/// price, "computed by the baskets service from its own rows and never stored".
/// </summary>
/// <remarks>
/// This is also what keeps the BFF free of arithmetic (004 plan.md, post-design re-check): the
/// total exists here so that no aggregation layer has to add anything up.
/// </remarks>
public class BasketTotalTests
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid PourOver = new("9f8d6b1e-0001-4000-8000-000000000002");

    [Fact]
    public void Total_IsZero_ForAnEmptyBasket()
    {
        Assert.Equal(0m, Basket.ForCustomer("phase1-stub-user").Total);
    }

    [Fact]
    public void Total_MultipliesQuantityByUnitPrice()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 2, unitPrice: 12.50m);

        Assert.Equal(25.00m, basket.Total);
    }

    [Fact]
    public void Total_SumsEveryLine()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 2, unitPrice: 12.50m);
        basket.AddItem(PourOver, quantity: 1, unitPrice: 48.00m);

        Assert.Equal(73.00m, basket.Total);
    }

    /// <summary>
    /// quickstart.md Scenarios 2 and 5 quote this figure: two notebooks at $12.50 plus one apron at
    /// $34.25 is $59.25. Pinned here so a change in how the total is reached fails in a unit test
    /// rather than halfway through a manual walkthrough.
    /// </summary>
    [Fact]
    public void Total_MatchesTheWalkthroughFigure()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(new Guid("9f8d6b1e-0001-4000-8000-000000000003"), quantity: 1, unitPrice: 34.25m);

        Assert.Equal(59.25m, basket.Total);
    }

    /// <summary>
    /// Decimal arithmetic, not floating point. The same sum in doubles produces 0.30000000000000004,
    /// and a basket total that is a cent out is a bug a shopper will notice.
    /// </summary>
    [Fact]
    public void Total_IsExact_ForAmountsThatFloatingPointWouldRound()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");

        basket.AddItem(Notebook, quantity: 1, unitPrice: 0.10m);
        basket.AddItem(PourOver, quantity: 1, unitPrice: 0.20m);

        Assert.Equal(0.30m, basket.Total);
    }

    [Fact]
    public void Clear_EmptiesTheBasket_AndZeroesTheTotal()
    {
        var basket = Basket.ForCustomer("phase1-stub-user");
        basket.AddItem(Notebook, quantity: 3, unitPrice: 12.50m);

        basket.Clear();

        Assert.Empty(basket.LineItems);
        Assert.Equal(0m, basket.Total);
    }
}
