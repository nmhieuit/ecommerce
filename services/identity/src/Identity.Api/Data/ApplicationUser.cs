using Microsoft.AspNetCore.Identity;

namespace Identity.Api.Data;

/// <summary>
/// The credential/identity half of a platform user (data-model.md — Identity User). Deliberately
/// separate from the <c>parties</c> service's business/CRM records — the two are linked only by
/// <see cref="IdentityUser.Id"/> (the <c>sub</c> claim), never by a shared table or database
/// (research.md Decision 8; constitution Principle I).
/// </summary>
public sealed class ApplicationUser : IdentityUser
{
    /// <summary>
    /// The single tenant this account belongs to (spec Assumptions: a user account is scoped to one
    /// tenant). <see cref="HostedIdentity.TenantClaimsProfileService"/> is the only reader — this is
    /// the source of the token's <c>tenant_id</c> claim (data-model.md — Token).
    /// </summary>
    public required string TenantId { get; set; }
}
