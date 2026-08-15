using System.Net;
using System.Net.Http.Json;

namespace Bff.Api.DownstreamClients;

/// <summary>
/// Calls the parties service's read surface
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml).
/// </summary>
public sealed class PartiesApiClient(HttpClient httpClient)
{
    /// <summary>The logical service name, matching this client's configuration section.</summary>
    public const string ServiceName = "PartiesApi";

    /// <summary>
    /// Returns the party, or <see langword="null"/> if the service reports it does not exist.
    /// A 404 is a correct answer, not a downstream failure — see <see cref="BasketsApiClient"/>.
    /// </summary>
    public Task<PartyResource?> GetPartyAsync(Guid partyId, CancellationToken cancellationToken) =>
        DownstreamCall.ExecuteAsync(ServiceName, async () =>
        {
            using var response = await httpClient.GetAsync($"/parties/{partyId}", cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<PartyResource>(cancellationToken);
        });
}

/// <summary>A party exactly as the parties service returns it.</summary>
public sealed record PartyResource(Guid Id, string DisplayName);
