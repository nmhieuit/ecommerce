using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the products service's catalog read surface
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// </summary>
/// <remarks>
/// The client speaks the downstream's shape, not the client-facing one. Translating
/// <see cref="ProductResource"/> into the SPA-facing summary is the route group's job, which keeps
/// a downstream field rename from silently reaching the SPA.
/// </remarks>
public sealed class ProductsApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "ProductsApi";

    /// <summary>
    /// Lists the catalog. An empty catalog is a legitimate answer, so this returns an empty list
    /// rather than treating "no products" as a failure.
    /// </summary>
    public Task<IReadOnlyList<ProductResource>> GetProductsAsync(CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            var products = await httpClient.GetFromJsonAsync<IReadOnlyList<ProductResource>>(
                "/products",
                cancellationToken);

            return products ?? (IReadOnlyList<ProductResource>)[];
        });
}

/// <summary>A product exactly as the products service returns it.</summary>
public sealed record ProductResource(Guid Id, string Name, decimal Price);
