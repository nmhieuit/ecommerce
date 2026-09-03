using Identity;
using Microsoft.EntityFrameworkCore;
using Parties.Api.Data;

namespace Parties.Api.Features.Parties;

/// <summary>
/// The party read capability, whole: route mapping and response shape live together in this one
/// folder rather than being split across technical-layer folders (spec SC-004, constitution's
/// vertical-slice default).
/// Contract: <c>specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml</c>.
/// </summary>
public static class PartyEndpoints
{
    public static WebApplication MapPartyEndpoints(this WebApplication app)
    {
        app.MapGet("/parties/{partyId:guid}", async (
            Guid partyId,
            PartiesDbContext dbContext,
            CancellationToken cancellationToken) =>
        {
            var party = await dbContext.Parties
                .AsNoTracking()
                .Where(entity => entity.Id == partyId)
                .Select(entity => new PartyResponse(entity.Id, entity.DisplayName))
                .SingleOrDefaultAsync(cancellationToken);

            return party is null ? Results.NotFound() : Results.Ok(party);
        })
            .RequireAuthorization(AuthorizationPolicies.ApiScope);

        return app;
    }

    /// <summary>
    /// The wire shape, kept separate from the <see cref="Party"/> entity — especially important
    /// here, since Party is the type most likely to gain personal data later and must not leak new
    /// fields to callers merely by having them added to the stored model.
    /// </summary>
    public sealed record PartyResponse(Guid Id, string DisplayName);
}
