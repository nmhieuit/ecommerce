namespace Baskets.Api.Data;

/// <summary>
/// One product within a basket, together with how many of it the shopper wants
/// (004-minimal-shopping-spa data-model.md — BasketLineItem).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ProductId"/> is an identifier only. This service holds no product record and performs
/// no lookup against the products service — cross-service data access happens through a published
/// API or event, never a join (constitution Principle I).
/// </para>
/// <para>
/// <see cref="UnitPrice"/> is the price at the moment the product was added, supplied by the caller
/// and never recomputed. Pricing a basket from the live catalog would silently reprice what the
/// shopper already chose (004 research.md Decision 7).
/// </para>
/// </remarks>
public class BasketLineItem
{
    public Guid Id { get; set; }

    public Guid BasketId { get; set; }

    public Guid ProductId { get; set; }

    /// <summary>How many of the product. Never below 1 — see <see cref="Basket.AddItem"/>.</summary>
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>What this line contributes to the basket total.</summary>
    public decimal LineTotal => Quantity * UnitPrice;
}
