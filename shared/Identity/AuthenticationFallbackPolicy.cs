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
    /// Requires an authenticated user and nothing more — no role, no scope. Fine-grained RBAC/scope
    /// policies are a separate concern layered on top of this default (contracts/service-authentication-contract.md
    /// — Stability), not part of this feature's scope (spec.md — SCRUM-23 is authentication, not
    /// authorization roles).
    /// </summary>
    public static AuthorizationPolicy Build() =>
        new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
}
