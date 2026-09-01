using System.Security.Claims;
using Duende.IdentityServer.Extensions;
using Duende.IdentityServer.Models;
using Duende.IdentityServer.Services;
using Identity.Api.Data;
using Microsoft.AspNetCore.Identity;

namespace Identity.Api.HostedIdentity;

/// <summary>
/// Issues the <c>tenant_id</c> claim on every token (data-model.md — Token; contracts/
/// identity-token-claims-contract.md). Named to match the claim type
/// <c>Gateway.Api.Identity.StubIdentityAuthenticationHandler.TenantClaimType</c> already used —
/// deliberately a literal here, not a project reference to the gateway, since the claim name is a
/// wire contract between services, not shared code (research.md Decision 3).
/// </summary>
public sealed class TenantClaimsProfileService(UserManager<ApplicationUser> userManager) : IProfileService
{
    /// <summary>The claim carrying the tenant identifier — must equal the gateway's <c>TenantClaimType</c>.</summary>
    public const string TenantClaimType = "tenant_id";

    public async Task GetProfileDataAsync(ProfileDataRequestContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = await userManager.GetUserAsync(context.Subject);
        if (user is null)
        {
            // No user behind this subject (deleted after the token's session started, or a subject
            // this profile service was never meant to resolve) — issue nothing rather than guess.
            context.IssuedClaims = [];
            return;
        }

        context.IssuedClaims.Add(new Claim(TenantClaimType, user.TenantId));
    }

    public async Task IsActiveAsync(IsActiveContext context, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var user = await userManager.GetUserAsync(context.Subject);
        context.IsActive = user is not null;
    }
}
