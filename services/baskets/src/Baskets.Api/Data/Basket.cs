namespace Baskets.Api.Data;

/// <summary>
/// A shopping basket owned by this service.
/// </summary>
/// <remarks>
/// <para>
/// A shopper has at most one open basket, found from their identity rather than from anything the
/// browser holds (004-minimal-shopping-spa spec FR-006). That is why <see cref="CustomerRef"/> is
/// unique and why there is no "create a basket" call — the basket is a consequence of being a
/// shopper, not something the client asks for.
/// </para>
/// <para>
/// The merge rule and the total live here rather than in the endpoint. They are domain rules, so
/// they hold before anything is persisted, they are unit-testable without a database, and the
/// aggregation layer above never has to do arithmetic of its own (004 plan.md, post-design
/// re-check).
/// </para>
/// </remarks>
public class Basket
{
    public Guid Id { get; set; }

    /// <summary>
    /// The caller's subject identifier, as resolved at the gateway and carried on
    /// <c>X-Subject-Id</c> (004 contracts/subject-id-header.md). Replaces the earlier
    /// <c>CustomerId</c> GUID: the Phase 1 stub subject is <c>phase1-stub-user</c>, and inventing a
    /// GUID mapping for it would add a translation layer whose only purpose was preserving a field
    /// type nothing depended on.
    /// </summary>
    public string CustomerRef { get; set; } = string.Empty;

    public ICollection<BasketLineItem> LineItems { get; } = [];

    /// <summary>
    /// The sum of every line. Computed, never stored — a stored total is a second source of truth
    /// that can disagree with the lines it came from (data-model.md).
    /// </summary>
    public decimal Total => LineItems.Sum(line => line.LineTotal);

    /// <summary>Starts an empty basket for a shopper.</summary>
    public static Basket ForCustomer(string customerRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerRef);

        return new Basket { Id = Guid.NewGuid(), CustomerRef = customerRef };
    }

    /// <summary>
    /// Adds a product, or increases the quantity of the line it already occupies (spec FR-005,
    /// FR-021).
    /// </summary>
    /// <param name="unitPrice">
    /// The catalog price at the moment of adding. Ignored when the product is already present: the
    /// price the shopper first saw is the one that stands.
    /// </param>
    public void AddItem(Guid productId, int quantity, decimal unitPrice)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantity, 1);
        ArgumentOutOfRangeException.ThrowIfNegative(unitPrice);

        var existing = LineItems.SingleOrDefault(line => line.ProductId == productId);

        if (existing is not null)
        {
            existing.Quantity += quantity;
            return;
        }

        // Id is deliberately left unset. A new line is discovered through this navigation rather
        // than added to the context directly, and the change tracker decides "new row" versus
        // "existing row" by whether the key is already populated — assigning one here makes it
        // issue an UPDATE against a row that does not exist yet. The identifier is generated on
        // insert instead.
        LineItems.Add(new BasketLineItem
        {
            BasketId = Id,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
        });
    }

    /// <summary>
    /// Removes every line, leaving the basket itself. Called after checkout (spec FR-010); the row
    /// survives so the shopper's basket identity is stable across purchases.
    /// </summary>
    public void Clear() => LineItems.Clear();
}
