using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Identity;

/// <summary>
/// Satisfies <see cref="RequireApiScopeRequirement"/> — the toggle-gated half of the
/// <c>ApiScope</c> policy (015-deny-by-default-authz, research.md Decision 1/5; constitution
/// Principle X). Registered once as a singleton (<see cref="IdentityValidationExtensions.AddIdentityValidation"/>),
/// evaluated on every request the requirement is attached to.
/// </summary>
/// <remarks>
/// Reads <see cref="AuthorizationToggleOptions"/> through <see cref="IOptionsMonitor{TOptions}"/> at
/// every evaluation, never cached at construction — the mechanism that makes "flip a config value,
/// no redeploy" (constitution Principle X) actually take effect without a pod restart, mirroring how
/// the gateway's <c>ToggleGatedAuthenticationExtensions</c> reads <c>FeatureToggleOptions</c> from
/// 014.
/// </remarks>
public sealed class RequireApiScopeAuthorizationHandler(IOptionsMonitor<AuthorizationToggleOptions> toggles)
    : AuthorizationHandler<RequireApiScopeRequirement>
{
    private const string ScopeClaimType = "scope";

    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, RequireApiScopeRequirement requirement)
    {
        // Off is the safe rollback state (research.md Decision 5): the requirement behaves exactly
        // like it did not exist, which is today's shipped behaviour (authenticated is enough).
        if (!toggles.CurrentValue.AuthorizationRequireApiScope)
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        // A token's scopes can arrive as one space-delimited "scope" claim or as several individual
        // claims of that type, depending on how claims mapping is configured — checked both ways
        // rather than assuming one shape.
        var hasRequiredScope = context.User.Claims
            .Where(claim => claim.Type == ScopeClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(AuthorizationPolicies.RequiredApiScopeValue, StringComparer.Ordinal);

        if (hasRequiredScope)
        {
            context.Succeed(requirement);
        }

        // Not calling Fail(): a Forbid must only ever come from this requirement genuinely not being
        // met, never race a still-pending handler for some other requirement on the same policy into
        // failing early.
        return Task.CompletedTask;
    }
}
