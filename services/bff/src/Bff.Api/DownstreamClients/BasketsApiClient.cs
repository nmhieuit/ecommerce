using System.Net;
using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the baskets service
/// (specs/004-minimal-shopping-spa/contracts/downstream-openapi.yaml).
/// </summary>
/// <remarks>
/// "Current" routes carry no identifier: the baskets service resolves the caller's basket from the
/// <c>X-Subject-Id</c> the BFF relays, so there is nothing for this client to pass and nothing a
/// caller could substitute (spec FR-006).
/// </remarks>
public sealed class BasketsApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "BasketsApi";

    /// <summary>The calling shopper's basket, created empty by the service on first read.</summary>
    public Task<BasketResource> GetCurrentBasketAsync(CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            var basket = await httpClient.GetFromJsonAsync<BasketResource>(
                "/baskets/current",
                cancellationToken);

            // The service always answers with a basket for a resolved caller, so a null body is a
            // contract violation rather than an empty basket — surfacing it as such beats handing
            // the route a null to trip over later.
            return basket ?? throw new InvalidOperationException(
                "The baskets service returned no basket for a resolved caller.");
        });

    /// <summary>
    /// Adds a product to the calling shopper's basket at the price the caller resolved, and returns
    /// the basket as it then stands.
    /// </summary>
    public Task<BasketResource> AddItemAsync(
        AddBasketItemCommand command,
        CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            using var response = await httpClient.PostAsJsonAsync(
                "/baskets/current/items",
                command,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            var basket = await response.Content.ReadFromJsonAsync<BasketResource>(cancellationToken);

            return basket ?? throw new InvalidOperationException(
                "The baskets service returned no basket after adding an item.");
        });

    /// <summary>
    /// Returns the basket, or <see langword="null"/> if the service reports it does not exist.
    /// </summary>
    /// <remarks>
    /// A 404 is the downstream answering correctly, not failing, so it is mapped to a null result
    /// rather than left to <c>EnsureSuccessStatusCode</c>. Letting it throw would send it down the
    /// same path as a genuine outage and turn "no such basket" into a 502 (spec 002 US3, FR-006).
    /// </remarks>
    public Task<BasketResource?> GetBasketAsync(Guid basketId, CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            using var response = await httpClient.GetAsync($"/baskets/{basketId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<BasketResource>(cancellationToken);
        });
}

/// <summary>A basket exactly as the baskets service returns it.</summary>
public sealed record BasketResource(
    Guid Id,
    string CustomerRef,
    IReadOnlyList<BasketLineItemResource> Items,
    decimal Total);

/// <summary>
/// One line of a basket, as the baskets service returns it — including its own line total, so the
/// BFF passes money through rather than computing it.
/// </summary>
public sealed record BasketLineItemResource(
    Guid ProductId,
    int Quantity,
    decimal UnitPrice,
    decimal LineTotal);

/// <summary>
/// What the BFF sends to add an item. The unit price is included because the BFF resolved it from
/// the catalog — the baskets service captures what it is given rather than calling products itself
/// (004 research.md Decision 7).
/// </summary>
public sealed record AddBasketItemCommand(Guid ProductId, int Quantity, decimal UnitPrice);
