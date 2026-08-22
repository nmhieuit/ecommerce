using System.Text.Json.Serialization;

namespace EventContracts;

/// <summary>
/// Version 1 of the <c>OrderPlaced</c> integration event: an order has been placed.
/// </summary>
/// <remarks>
/// <para>
/// The authoritative contract is <c>schemas/OrderPlaced.v1.schema.json</c>, which this record
/// mirrors field for field. The schema is the artifact a reviewer approves; this record is the
/// code-level shape a publisher constructs. <c>SchemaValidationTests</c> proves the two agree by
/// serializing an instance and validating it against the schema.
/// </para>
/// <para>
/// Every field is required. A breaking change — a new required field, a removed field, a changed
/// type or meaning — ships as <c>OrderPlacedV2</c> plus <c>OrderPlaced.v2.schema.json</c>; neither
/// this type nor the v1 schema is edited in place (see <c>README.md</c>).
/// </para>
/// <para>
/// The JSON property names are pinned with <see cref="JsonPropertyNameAttribute"/> rather than left
/// to a serializer naming policy, so the wire format matches the schema regardless of how the
/// publisher happens to configure <c>System.Text.Json</c>.
/// </para>
/// </remarks>
/// <param name="EventId">
/// Required. Unique identifier of this event instance, distinct from <paramref name="OrderId"/>,
/// so a consumer can deduplicate a redelivered message.
/// </param>
/// <param name="OccurredAtUtc">
/// Required. When the order was placed, in UTC. Source: <c>Order.PlacedAtUtc</c>.
/// </param>
/// <param name="OrderId">Required. Source: <c>Order.Id</c>.</param>
/// <param name="TenantId">
/// Required. The tenant the order belongs to. Required at the contract level even though today's
/// <c>OrderResponse.TenantId</c> is nullable in the read model.
/// </param>
/// <param name="CorrelationId">
/// Required. The correlation id generated at the edge and threaded through the request that
/// created the order.
/// </param>
/// <param name="Total">
/// Required. Source: <c>Order.Total</c> — computed by Orders, never by a caller.
/// </param>
/// <param name="Lines">Required. At least one line; an order cannot exist with zero lines.</param>
public sealed record OrderPlacedV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("total")] decimal Total,
    [property: JsonPropertyName("lines")] IReadOnlyList<OrderLineV1> Lines);

/// <summary>
/// A single line of an <see cref="OrderPlacedV1"/>.
/// </summary>
/// <remarks>
/// Not independently versioned: it changes only when <see cref="OrderPlacedV1"/> does, which is why
/// it carries the same <c>V1</c> suffix and no schema file of its own (it lives under
/// <c>$defs</c> in <c>OrderPlaced.v1.schema.json</c>).
/// </remarks>
/// <param name="ProductId">Required. The ordered product.</param>
/// <param name="Quantity">Required. At least 1.</param>
/// <param name="UnitPrice">Required. Non-negative.</param>
public sealed record OrderLineV1(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice);
