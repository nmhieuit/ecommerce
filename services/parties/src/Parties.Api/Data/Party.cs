namespace Parties.Api.Data;

/// <summary>
/// A party (customer) owned by this service.
/// </summary>
/// <remarks>
/// Deliberately minimal — the read surface the BFF's party route proxies
/// (specs/002-gateway-bff-routing/contracts/downstream-openapi.yaml — <c>Party</c>), not this
/// service's first domain story: no contact details, no addresses, no identity linkage. Party is
/// the type most likely to carry personal data later, which is another reason to add only what
/// this feature's routing actually needs and let the owning story introduce the rest deliberately.
/// </remarks>
public class Party
{
    public Guid Id { get; set; }

    public required string DisplayName { get; set; }
}
