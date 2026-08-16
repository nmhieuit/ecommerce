using Baskets.Api.Data;

namespace Baskets.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa spec FR-005 and FR-021: "adding a product already present in the basket
/// MUST increase that product's quantity rather than create a second entry for the same product."
/// </summary>
/// <remarks>
/// Unit tests rather than integration ones because this is a domain rule, not a persistence
/// behaviour — the merge has to hold before anything is saved, and asserting it here means a
/// regression fails in milliseconds instead of behind a container start.
/// </remarks>
public class BasketLineMergeTests
{
    private static readonly Guid Notebook = new("9f8d6b1e-0001-4000-8000-000000000001");
    private static readonly Guid Apron = new("9f8d6b1e-0001-4000-8000-000000000003");

    [Fact]
    public void AddItem_CreatesALine_WhenTheProductIsNotInTheBasketYet()
    {
        var basket = NewBasket();

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        var line = Assert.Single(basket.LineItems);
        Assert.Equal(Notebook, line.ProductId);
        Assert.Equal(1, line.Quantity);
        Assert.Equal(12.50m, line.UnitPrice);
    }

    [Fact]
    public void AddItem_IncrementsTheExistingLine_WhenTheProductIsAlreadyInTheBasket()
    {
        var basket = NewBasket();
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        var line = Assert.Single(basket.LineItems);
        Assert.Equal(2, line.Quantity);
    }

    [Fact]
    public void AddItem_KeepsProductsApart_WhenDifferentProductsAreAdded()
    {
        var basket = NewBasket();

        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        basket.AddItem(Apron, quantity: 2, unitPrice: 34.25m);

        Assert.Equal(2, basket.LineItems.Count);
        Assert.Equal(1, Assert.Single(basket.LineItems, line => line.ProductId == Notebook).Quantity);
        Assert.Equal(2, Assert.Single(basket.LineItems, line => line.ProductId == Apron).Quantity);
    }

    [Fact]
    public void AddItem_AccumulatesQuantities_AcrossManyAdditions()
    {
        var basket = NewBasket();

        for (var i = 0; i < 5; i++)
        {
            basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);
        }

        // Spec SC-003: the quantity equals the number of times the product was added, in 100% of
        // trials. One line, five of it.
        Assert.Equal(5, Assert.Single(basket.LineItems).Quantity);
    }

    /// <summary>
    /// The price captured on the line is the price when the product was first added, not the most
    /// recent one offered. Re-pricing a basket the shopper already assembled is exactly what
    /// capturing a unit price is meant to prevent (004 research.md Decision 7).
    /// </summary>
    [Fact]
    public void AddItem_KeepsTheOriginallyCapturedPrice_WhenTheCatalogPriceHasChanged()
    {
        var basket = NewBasket();
        basket.AddItem(Notebook, quantity: 1, unitPrice: 12.50m);

        basket.AddItem(Notebook, quantity: 1, unitPrice: 99.99m);

        var line = Assert.Single(basket.LineItems);
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
    }

    /// <summary>
    /// data-model.md: a line's quantity is at least 1, and "a line with quantity 0 must not exist".
    /// Rejecting it here means the rule holds regardless of which caller forgot to validate.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AddItem_Rejects_AQuantityBelowOne(int quantity)
    {
        var basket = NewBasket();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => basket.AddItem(Notebook, quantity, unitPrice: 12.50m));
    }

    [Fact]
    public void AddItem_Rejects_ANegativeUnitPrice()
    {
        var basket = NewBasket();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => basket.AddItem(Notebook, quantity: 1, unitPrice: -0.01m));
    }

    private static Basket NewBasket() => Basket.ForCustomer("phase1-stub-user");
}
