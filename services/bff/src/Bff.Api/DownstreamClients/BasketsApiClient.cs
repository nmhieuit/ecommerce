using System.Net;
using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the baskets service's read surface
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// </summary>
public sealed class BasketsApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "BasketsApi";

    /// <summary>
    /// Returns the basket, or <see langword="null"/> if the service reports it does not exist.
    /// </summary>
    /// <remarks>
    /// A 404 is the downstream answering correctly, not failing, so it is mapped to a null result
    /// rather than left to <c>EnsureSuccessStatusCode</c>. Letting it throw would send it down the
    /// same path as a genuine outage and turn "no such basket" into a 502 (US3, FR-006).
    /// </remarks>
    public async Task<BasketResource?> GetBasketAsync(Guid basketId, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync($"/baskets/{basketId}", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<BasketResource>(cancellationToken);
    }
}

/// <summary>A basket exactly as the baskets service returns it.</summary>
public sealed record BasketResource(Guid Id, Guid CustomerId);
