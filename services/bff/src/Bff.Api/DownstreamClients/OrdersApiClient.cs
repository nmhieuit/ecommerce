using System.Net;
using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the orders service's read surface
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// </summary>
public sealed class OrdersApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "OrdersApi";

    /// <summary>
    /// Returns the order, or <see langword="null"/> if the service reports it does not exist.
    /// A 404 is a correct answer, not a downstream failure — see <see cref="BasketsApiClient"/>.
    /// </summary>
    public Task<OrderResource?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            using var response = await httpClient.GetAsync($"/orders/{orderId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<OrderResource>(cancellationToken);
        });
}

/// <summary>An order exactly as the orders service returns it.</summary>
public sealed record OrderResource(Guid Id, DateTime PlacedAtUtc, decimal Total);
