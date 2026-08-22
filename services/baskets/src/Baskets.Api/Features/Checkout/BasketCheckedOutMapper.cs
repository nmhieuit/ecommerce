using Baskets.Api.Data;
using EventContracts;

namespace Baskets.Api.Features.Checkout;

/// <summary>
/// Builds the <see cref="BasketCheckedOutV1"/> payload this service will publish when a basket is
/// checked out (<c>shared/EventContracts/schemas/BasketCheckedOut.v1.schema.json</c>).
/// </summary>
/// <remarks>
/// <para>
/// Nothing calls this yet, and that is deliberate. Checkout is synchronous BFF orchestration today
/// (ADR-0011); the outbox and the publisher that will send this are SCRUM-31's work. What exists
/// here is the payload construction alone, so that work starts against a contract the orders
/// service has already verified rather than defining one after the fact
/// (011-consumer-contract-tests research.md Decision 3).
/// </para>
/// <para>
/// A pure function over a <see cref="Basket"/> rather than a method on it: the identifiers below
/// belong to the message, not to the basket, and pushing them onto the entity would put
/// transport concerns inside the domain model. Being pure is also what lets
/// <c>BasketCheckedOutProviderPactTests</c> verify the real payload without a broker.
/// </para>
/// </remarks>
public static class BasketCheckedOutMapper
{
    /// <param name="basket">The basket as it stood when it was checked out, before it was cleared.</param>
    /// <param name="tenantId">
    /// The tenant the basket belongs to, from the resolved request context. Required on the
    /// contract even though no basket response surfaces it: a consumer reacting asynchronously has
    /// no request to resolve it from, and an event nobody can attribute is one nobody can act on
    /// (constitution Principle V).
    /// </param>
    /// <param name="correlationId">The correlation id threaded through the checkout request.</param>
    /// <param name="eventId">
    /// This message instance's identifier, so a consumer can discard a redelivery rather than order
    /// twice. Passed in rather than generated here, because a retried publish of the <em>same</em>
    /// checkout has to carry the same id, and only the caller knows whether that is what it is.
    /// </param>
    /// <param name="occurredAtUtc">When the checkout happened. Passed in for the same reason.</param>
    public static BasketCheckedOutV1 ToEvent(
        Basket basket,
        string tenantId,
        string correlationId,
        Guid eventId,
        DateTime occurredAtUtc)
    {
        ArgumentNullException.ThrowIfNull(basket);
        ArgumentException.ThrowIfNullOrWhiteSpace(tenantId);
        ArgumentException.ThrowIfNullOrWhiteSpace(correlationId);

        return new BasketCheckedOutV1(
            eventId,
            occurredAtUtc,
            basket.Id,
            basket.CustomerRef,
            tenantId,
            correlationId,
            [.. basket.LineItems
                .OrderBy(line => line.ProductId)
                .Select(line => new BasketLineItemV1(
                    line.ProductId,
                    line.Quantity,
                    line.UnitPrice,
                    // Carried, never recomputed downstream: money arithmetic stays in the service
                    // that owns the basket (004 plan.md, post-design re-check).
                    line.LineTotal))],
            basket.Total);
    }
}
