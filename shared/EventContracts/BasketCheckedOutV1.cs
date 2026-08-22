using System.Text.Json.Serialization;

namespace EventContracts;

/// <summary>
/// Version 1 of the <c>BasketCheckedOut</c> integration event: a basket has been checked out and
/// cleared as part of a successful order placement.
/// </summary>
/// <remarks>
/// <para>
/// The authoritative contract is <c>schemas/BasketCheckedOut.v1.schema.json</c>, which this record
/// mirrors field for field; see <see cref="OrderPlacedV1"/> for the full rationale on why both
/// artifacts exist and how a version is superseded.
/// </para>
/// </remarks>
/// <param name="EventId">
/// Required. Unique identifier of this event instance, so a consumer can deduplicate a redelivered
/// message.
/// </param>
/// <param name="OccurredAtUtc">
/// Required. When the basket was cleared as part of checkout, in UTC.
/// </param>
/// <param name="BasketId">Required. Source: <c>Basket.Id</c>.</param>
/// <param name="CustomerRef">
/// Required. Source: <c>Basket.CustomerRef</c> — the caller's resolved subject id.
/// </param>
/// <param name="TenantId">
/// Required. The tenant the basket belongs to. Required at the contract level even though
/// <c>BasketResponse</c> does not surface it today.
/// </param>
/// <param name="CorrelationId">
/// Required. The correlation id generated at the edge and threaded through the checkout request.
/// </param>
/// <param name="Items">
/// Required. The lines that were checked out. Checkout of an already-empty basket is rejected
/// upstream with a 409, so in practice there is always at least one.
/// </param>
/// <param name="Total">Required. Source: <c>Basket.Total</c>.</param>
public sealed record BasketCheckedOutV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("occurredAtUtc")] DateTime OccurredAtUtc,
    [property: JsonPropertyName("basketId")] Guid BasketId,
    [property: JsonPropertyName("customerRef")] string CustomerRef,
    [property: JsonPropertyName("tenantId")] string TenantId,
    [property: JsonPropertyName("correlationId")] string CorrelationId,
    [property: JsonPropertyName("items")] IReadOnlyList<BasketLineItemV1> Items,
    [property: JsonPropertyName("total")] decimal Total);

/// <summary>
/// A single checked-out line of a <see cref="BasketCheckedOutV1"/>.
/// </summary>
/// <remarks>
/// Not independently versioned: it changes only when <see cref="BasketCheckedOutV1"/> does, and
/// lives under <c>$defs</c> in <c>BasketCheckedOut.v1.schema.json</c> rather than in a schema file
/// of its own.
/// </remarks>
/// <param name="ProductId">Required. The product in the basket.</param>
/// <param name="Quantity">Required. At least 1.</param>
/// <param name="UnitPrice">Required. Non-negative.</param>
/// <param name="LineTotal">
/// Required. Source: <c>BasketLineItemResponse.LineTotal</c> — money arithmetic stays in the owning
/// service and is carried on the event, never recomputed by a consumer.
/// </param>
public sealed record BasketLineItemV1(
    [property: JsonPropertyName("productId")] Guid ProductId,
    [property: JsonPropertyName("quantity")] int Quantity,
    [property: JsonPropertyName("unitPrice")] decimal UnitPrice,
    [property: JsonPropertyName("lineTotal")] decimal LineTotal);
