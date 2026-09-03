using Bff.Api.DownstreamClients;
using Identity;

namespace Bff.Api.Features.Parties;

/// <summary>
/// The client-facing party (customer) capability, whole: route mapping and response shape live
/// together in this one folder (spec SC-004, constitution's vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/bff-openapi.yaml</c> —
/// <c>/bff/parties/{partyId}</c>.
/// </summary>
/// <remarks>Spec FR-005: aggregation and shaping only — no domain rules.</remarks>
public static class PartiesEndpoints
{
    public static WebApplication MapPartiesEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/bff");

        group.MapGet("/parties/{partyId:guid}", async (
            Guid partyId,
            PartiesApiClient parties,
            CancellationToken cancellationToken) =>
        {
            var party = await parties.GetPartyAsync(partyId, cancellationToken);

            return party is null
                ? Results.NotFound()
                : Results.Ok(ToResponse(party));
        })
            .WithName("getParty")
            // See ProductsEndpoints for why the failure responses are declared explicitly: the
            // generated document is the contract the frontend's client is built from.
            .Produces<PartyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway)
            .ProducesProblem(StatusCodes.Status504GatewayTimeout)
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    /// <summary>
    /// Names each forwarded field explicitly rather than passing the downstream resource through.
    /// Party is the type most likely to gain personal data, and an explicit mapping means a new
    /// field reaches the client only when someone decides it should.
    /// </summary>
    internal static PartyResponse ToResponse(PartyResource party) =>
        new(party.Id, party.DisplayName);

    /// <summary>A party as the client sees it.</summary>
    public sealed record PartyResponse(Guid Id, string DisplayName);
}
