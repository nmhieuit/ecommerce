using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Gateway.Api.Identity;

/// <summary>
/// Authenticates every request as one fixed fake user carrying one fixed tenant claim — the Phase 1
/// stand-in for the identity server ADR-0001 picked and SCRUM-23 will stand up.
/// </summary>
/// <remarks>
/// <para>
/// This is a real <see cref="AuthenticationHandler{TOptions}"/> rather than a middleware that
/// stamps a header, and that is the whole point (research.md Decision 1). Spec FR-007 requires
/// Phase 3 to change only the <em>resolution source</em>: swapping this
/// <c>AddScheme&lt;...&gt;</c> registration for <c>AddJwtBearer(...)</c> leaves the claim, the
/// propagation middleware, and every consumer downstream untouched. A hand-rolled shortcut would
/// have made Phase 3 introduce the authentication pipeline for the first time instead.
/// </para>
/// <para>
/// It never fails: Phase 1 has no login flow and no credentials to reject (spec Assumptions), so
/// there is no "authentication failed" path to model. That is a documented Principle VI deviation
/// (plan.md Complexity Tracking), time-bound to SCRUM-23 — not an oversight.
/// </para>
/// </remarks>
public sealed class StubIdentityAuthenticationHandler(
    IOptionsMonitor<StubIdentityAuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<StubIdentityAuthenticationSchemeOptions>(options, logger, encoder)
{
    /// <summary>The scheme name this handler is registered under (data-model.md — Stub Identity).</summary>
    public const string SchemeName = "StubIdentity";

    /// <summary>
    /// The claim carrying the tenant. Named for the OIDC-style claim a real token would carry, so
    /// Phase 3's issuer can populate the same claim type and nothing downstream notices the swap.
    /// </summary>
    public const string TenantClaimType = "tenant_id";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (string.IsNullOrWhiteSpace(Options.TenantId))
        {
            // No tenant configured is a misconfigured gateway, not an anonymous request. Failing
            // here keeps the "resolved once, at the edge" guarantee honest: the alternative is a
            // successfully authenticated principal with no tenant, which every hop below would
            // then have to treat as unresolved anyway.
            return Task.FromResult(AuthenticateResult.Fail(
                $"No tenant is configured for the '{SchemeName}' scheme (StubIdentity:TenantId)."));
        }

        var identity = new ClaimsIdentity(
            [
                new Claim(TenantClaimType, Options.TenantId),
                new Claim(ClaimTypes.NameIdentifier, Options.SubjectId),
            ],
            SchemeName);

        var ticket = new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
