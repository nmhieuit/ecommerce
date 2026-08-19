namespace Orders.Api.Data;

/// <summary>
/// An order owned by this service.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately minimal: an identifier, when it was placed, and what it came to. Line items are
/// <em>not</em> persisted — no acceptance criterion needs them (the confirmation shows a reference
/// and a total, spec FR-009), and the basket is what carries per-product detail. Orders gain line
/// items with the story that owns them (004 spec.md, Assumptions).
/// </para>
/// <para>
/// The outbox table (constitution Principle IV) is likewise absent; it lands alongside the first
/// event this platform publishes (SCRUM-18).
/// </para>
/// </remarks>
public class Order
{
    public Guid Id { get; set; }

    /// <summary>When the order was placed, in UTC. The platform stores instants, never local time.</summary>
    public DateTime PlacedAtUtc { get; set; }

    public decimal Total { get; set; }

    /// <summary>
    /// The tenant this order belongs to, as resolved for the request that placed it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Set from <c>TenantContext.RequireTenantId()</c> at the point of creation, never from the
    /// request body (006 research.md Decision 2). A tenant the caller could name is a tenant the
    /// caller could choose, which is the case constitution Principle V exists to close.
    /// </para>
    /// <para>
    /// Nullable in the schema and never null in practice: the column was added as the expand half of
    /// expand/contract (Principle X), so the previous version of this service keeps inserting rows
    /// against the new schema. What guarantees the value is <see cref="PlaceFrom"/>, which refuses
    /// to build an order without one. Tightening the column to NOT NULL is the contract half, and a
    /// later story's work.
    /// </para>
    /// <para>
    /// This is evidence of attribution, not the mechanism of isolation. What actually stops a
    /// request reaching another tenant's data is the gate in <c>Program.cs</c>, which refuses to
    /// construct a <see cref="OrdersDbContext"/> at all without a resolved tenant.
    /// </para>
    /// </remarks>
    public string? TenantId { get; set; }

    /// <summary>
    /// Creates an order from the lines it is being placed for, computing the total here rather than
    /// accepting one.
    /// </summary>
    /// <remarks>
    /// The caller (the BFF) supplies what was bought; what it comes to is this service's answer.
    /// That is the whole reason the total is not a parameter — money arithmetic belongs in the
    /// domain service that owns the record, not in the aggregation layer above it
    /// (004 research.md Decision 8).
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// There are no lines to order, or no tenant was given to attribute the order to.
    /// </exception>
    public static Order PlaceFrom(
        IReadOnlyCollection<OrderLine> lines,
        DateTime placedAtUtc,
        string tenantId)
    {
        ArgumentNullException.ThrowIfNull(lines);

        // Blank counts as absent, exactly as TenantContext.RequireTenantId treats it. An order
        // attributed to an empty string is a row nobody can account for, and it would satisfy every
        // other check here.
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new ArgumentException(
                "An order needs the tenant it was placed for - an order belonging to nobody is not an order this service can hold.",
                nameof(tenantId));
        }

        if (lines.Count == 0)
        {
            throw new ArgumentException(
                "An order needs at least one line — an empty basket has nothing to order.",
                nameof(lines));
        }

        foreach (var line in lines)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(line.Quantity, 1, nameof(lines));
            ArgumentOutOfRangeException.ThrowIfNegative(line.UnitPrice, nameof(lines));
        }

        return new Order
        {
            Id = Guid.NewGuid(),
            PlacedAtUtc = placedAtUtc,
            Total = lines.Sum(line => line.Quantity * line.UnitPrice),
            TenantId = tenantId,
        };
    }
}

/// <summary>
/// One line being ordered. Used to compute the total and then discarded — see <see cref="Order"/>
/// on why line items are not persisted in Phase 1.
/// </summary>
public sealed record OrderLine(Guid ProductId, int Quantity, decimal UnitPrice);
