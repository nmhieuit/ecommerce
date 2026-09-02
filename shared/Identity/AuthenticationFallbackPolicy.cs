using Microsoft.AspNetCore.Authorization;

namespace Identity;

/// <summary>
/// The deny-by-default authorization policy <see cref="IdentityValidationExtensions.AddIdentityValidation"/>
/// installs as every service's <c>FallbackPolicy</c> (research.md Decision 6; constitution
/// Principle VI: "an endpoint without an authorization decision MUST fail the build or the
/// review"). An endpoint added later with no explicit policy falls back to this one automatically —
/// it never silently becomes anonymous.
/// </summary>
public static class AuthenticationFallbackPolicy
{
    /// <summary>
    /// Requires an authenticated user (014) AND — once <see cref="RequireApiScopeAuthorizationHandler"/>'s
    /// toggle is on — the <c>ApiScope</c> policy's scope requirement (015-deny-by-default-authz,
    /// research.md Decision 1). The fallback is deliberately at least as strict as the named
    /// <c>AuthorizationPolicies.ApiScope</c> policy every route declares explicitly
    /// (<see cref="IdentityValidationExtensions.AddIdentityValidation"/>), so an endpoint that
    /// forgets to declare it is never left less protected than one that does.
    /// </summary>
    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .AddRequirements(new RequireApiScopeRequirement())
            .Build();
}
