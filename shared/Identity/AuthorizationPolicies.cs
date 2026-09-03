namespace Identity;

/// <summary>
/// Names and values the <c>ApiScope</c> authorization policy is built from (015-deny-by-default-authz,
/// research.md Decision 1/2) — the one policy every business route across every service declares
/// explicitly via <c>.RequireAuthorization(AuthorizationPolicies.ApiScope)</c>, and the same
/// requirement <see cref="AuthenticationFallbackPolicy"/> now also carries as a safety net.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>The name every route registers via <c>.RequireAuthorization(...)</c>.</summary>
    public const string ApiScope = "ApiScope";

    /// <summary>
    /// The OAuth2 <c>scope</c> claim value a token must carry once the toggle
    /// (<see cref="AuthorizationToggleOptions.AuthorizationRequireApiScope"/>) is on. A literal, not
    /// a project reference to <c>Identity.Api.Config.ApiScopeName</c> — a wire contract between
    /// services, not shared code (same reasoning as
    /// <c>Identity.Api.HostedIdentity.TenantClaimsProfileService.TenantClaimType</c>, 014).
    /// </summary>
    public const string RequiredApiScopeValue = "ecommerce-api";
}
