using Bff.Api.DownstreamClients;
using Bff.Api.Features.Baskets;
using Bff.Api.Features.Orders;
using Bff.Api.Features.Parties;
using Bff.Api.Features.Products;

namespace Bff.Api.UnitTests;

/// <summary>
/// The response-shaping functions, exercised without HTTP.
/// </summary>
/// <remarks>
/// Shaping is the only logic the BFF owns — everything else is proxying (spec FR-005) — so it is
/// the one thing worth asserting directly rather than only through an integration test. These also
/// pin the property-by-property mapping: an integration test comparing whole objects would pass if
/// two same-typed fields were transposed, and <c>Order</c> carries exactly such a pair once
/// <c>Total</c> and a future amount field coexist.
/// </remarks>
public class ResponseMappingTests
{
    [Fact]
    public void ProductSummary_CarriesEveryFieldFromTheDownstreamProduct()
    {
        var product = new ProductResource(Guid.NewGuid(), "Ceramic mug", 12.50m);

        var summary = ProductsEndpoints.ToSummary(product);

        Assert.Equal(product.Id, summary.Id);
        Assert.Equal(product.Name, summary.Name);
        Assert.Equal(product.Price, summary.Price);
    }

    /// <summary>
    /// 004: the basket line the shopper sees is the downstream line plus a product name joined in
    /// from the catalog. Every other field must survive that join untouched — and the line total
    /// in particular is passed through, never recomputed, because money arithmetic belongs to the
    /// baskets service (004 plan.md, post-design re-check).
    /// </summary>
    [Fact]
    public void BasketItem_JoinsTheProductName_AndPassesEveryOtherFieldThrough()
    {
        var productId = Guid.Parse("9f8d6b1e-0001-4000-8000-000000000001");
        var line = new BasketLineItemResource(productId, Quantity: 2, UnitPrice: 12.50m, LineTotal: 25.00m);

        var item = BasketsEndpoints.ToItem(line, new Dictionary<Guid, string>
        {
            [productId] = "Field Notes Notebook",
        });

        Assert.Equal(productId, item.ProductId);
        Assert.Equal("Field Notes Notebook", item.Name);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(12.50m, item.UnitPrice);
        Assert.Equal(25.00m, item.LineTotal);
    }

    /// <summary>
    /// A line whose product has since left the catalog keeps its place rather than vanishing: the
    /// shopper chose it and is being charged for it, so dropping it would misrepresent the total
    /// they are about to pay.
    /// </summary>
    [Fact]
    public void BasketItem_SurvivesAProductMissingFromTheCatalog()
    {
        var line = new BasketLineItemResource(Guid.NewGuid(), Quantity: 1, UnitPrice: 9.99m, LineTotal: 9.99m);

        var item = BasketsEndpoints.ToItem(line, new Dictionary<Guid, string>());

        Assert.Equal(9.99m, item.LineTotal);
        Assert.False(string.IsNullOrWhiteSpace(item.Name));
    }

    [Fact]
    public void OrderResponse_CarriesEveryFieldFromTheDownstreamOrder()
    {
        var order = new OrderResource(
            Guid.NewGuid(),
            new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc),
            47.49m);

        var response = OrdersEndpoints.ToResponse(order);

        Assert.Equal(order.Id, response.Id);
        Assert.Equal(order.PlacedAtUtc, response.PlacedAtUtc);
        Assert.Equal(order.Total, response.Total);
    }

    /// <summary>
    /// The instant must survive shaping as an instant. Dropping <see cref="DateTimeKind.Utc"/> here
    /// would leave the SPA rendering an order time in the wrong zone with nothing to indicate it.
    /// </summary>
    [Fact]
    public void OrderResponse_PreservesTheUtcKindOfThePlacedTimestamp()
    {
        var placedAtUtc = new DateTime(2026, 8, 15, 9, 30, 0, DateTimeKind.Utc);

        var response = OrdersEndpoints.ToResponse(new OrderResource(Guid.NewGuid(), placedAtUtc, 1m));

        Assert.Equal(DateTimeKind.Utc, response.PlacedAtUtc.Kind);
    }

    [Fact]
    public void PartyResponse_CarriesEveryFieldFromTheDownstreamParty()
    {
        var party = new PartyResource(Guid.NewGuid(), "Ada Lovelace");

        var response = PartiesEndpoints.ToResponse(party);

        Assert.Equal(party.Id, response.Id);
        Assert.Equal(party.DisplayName, response.DisplayName);
    }

    /// <summary>
    /// Money must not be rounded on the way through. A shaping step that narrowed
    /// <see cref="decimal"/> to <see cref="double"/> would pass every equality check above on
    /// round numbers and quietly corrupt prices like this one.
    /// </summary>
    [Theory]
    [InlineData("0.01")]
    [InlineData("12.50")]
    [InlineData("999999999999.99")]
    public void ProductSummary_PreservesPricePrecisionExactly(string price)
    {
        var exact = decimal.Parse(price, System.Globalization.CultureInfo.InvariantCulture);

        var summary = ProductsEndpoints.ToSummary(new ProductResource(Guid.NewGuid(), "Anything", exact));

        Assert.Equal(exact, summary.Price);
        // Trailing zeros are part of a decimal's representation; a round trip through double or
        // float would not preserve them.
        Assert.Equal(price, summary.Price.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
