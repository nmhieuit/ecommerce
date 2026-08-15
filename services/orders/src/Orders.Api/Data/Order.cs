namespace Orders.Api.Data;

/// <summary>
/// An order owned by this service.
/// </summary>
/// <remarks>
/// Deliberately minimal — the read surface the BFF's order route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml — <c>Order</c>), not this
/// service's first domain story: no line items, no status transitions, no payment or fulfilment.
/// The outbox table (constitution Principle IV) is likewise absent — it lands alongside the first
/// event this platform publishes.
/// </remarks>
public class Order
{
    public Guid Id { get; set; }

    /// <summary>When the order was placed, in UTC. The platform stores instants, never local time.</summary>
    public DateTime PlacedAtUtc { get; set; }

    public decimal Total { get; set; }
}
