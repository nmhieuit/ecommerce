namespace Baskets.Api.Data;

/// <summary>
/// A shopping basket owned by this service.
/// </summary>
/// <remarks>
/// Deliberately minimal — the read surface the BFF's basket route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml — <c>Basket</c>), not this
/// service's first domain story: no line items, no totals, no reservation rules.
/// <para>
/// <see cref="CustomerId"/> is an identifier only. This service holds no party record and performs
/// no lookup against the parties service — cross-service data access happens through a published
/// API or event, never a join (constitution Principle I).
/// </para>
/// </remarks>
public class Basket
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }
}
