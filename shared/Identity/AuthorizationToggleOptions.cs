namespace Identity;

/// <summary>
/// Toggle configuration for the <c>ApiScope</c> authorization policy's strict requirement
/// (015-deny-by-default-authz, research.md Decision 5; constitution Principle X). Bound from the
/// same "FeatureToggles" configuration section 014-identity-server-auth's
/// <c>Gateway.Api.Identity.FeatureToggleOptions</c> already introduced — a ConfigMap-backed value,
/// not a code change, so flipping it does not require a redeploy.
/// </summary>
/// <remarks>
/// Same deliberately minimal stand-in for the real toggle service that 014 already documented: no
/// service in this platform has wired the Unleash server/SDK yet (ADR-0008's Action Items are still
/// unchecked). <see cref="RequireApiScopeAuthorizationHandler"/> reads this value through
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> at every evaluation (not
/// cached at construction), so swapping the source for a real Unleash-backed feature flag later is a
/// one-line change at the read site, not a redesign of the toggle-gating mechanism itself.
/// </remarks>
public sealed class AuthorizationToggleOptions
{
    public const string ConfigSectionName = "FeatureToggles";

    /// <summary>
    /// When <see langword="true"/>, the <c>ApiScope</c> policy requires the authenticated principal
    /// to carry <see cref="AuthorizationPolicies.RequiredApiScopeValue"/> in its <c>scope</c> claim.
    /// When <see langword="false"/> (the default — safe rollback state, mirrors 014's
    /// <c>IdentityServerAuthCutover</c> convention), the policy falls back to exactly today's
    /// behaviour: authenticated is enough, scope is not checked.
    /// </summary>
    public bool AuthorizationRequireApiScope { get; set; }
}
