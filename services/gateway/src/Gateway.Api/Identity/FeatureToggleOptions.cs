namespace Gateway.Api.Identity;

/// <summary>
/// Toggle configuration for the StubIdentity → JwtBearer cutover (research.md Decision 7;
/// constitution Principle X). Bound from the "FeatureToggles" configuration section — a
/// ConfigMap-backed value in Kubernetes, not a code change, so flipping it does not require a
/// redeploy.
/// </summary>
/// <remarks>
/// This is a deliberately minimal stand-in for the real toggle service: ADR-0008 picked Unleash,
/// but no service in this platform has wired the Unleash server or SDK yet (its own Action Items —
/// deploy self-hosted Unleash, integrate the .NET/React SDKs — are still unchecked). Building that
/// platform-wide infrastructure is out of this feature's scope; a follow-up task is tracked
/// separately. <see cref="ToggleGatedAuthenticationExtensions"/> reads this value through
/// <see cref="Microsoft.Extensions.Options.IOptionsMonitor{TOptions}"/> so that swapping the source
/// for a real Unleash-backed <c>IFeatureManager</c> later is a one-line change at the read site,
/// not a redesign of the toggle-gating mechanism itself.
/// </remarks>
public sealed class FeatureToggleOptions
{
    public const string ConfigSectionName = "FeatureToggles";

    /// <summary>
    /// When <see langword="true"/>, the gateway authenticates with <c>AddJwtBearer</c> against the
    /// real identity server. When <see langword="false"/> (the default — safe rollback state),
    /// it authenticates with <see cref="StubIdentityAuthenticationHandler"/> as before.
    /// </summary>
    public bool IdentityServerAuthCutover { get; set; }
}
