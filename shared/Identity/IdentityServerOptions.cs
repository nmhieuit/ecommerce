namespace Identity;

/// <summary>
/// Binds the "Identity" configuration section every service reads to configure JwtBearer against
/// the identity server (data-model.md — cấu hình xác thực; contracts/identity-token-claims-contract.md).
/// </summary>
public sealed class IdentityServerOptions
{
    /// <summary>The configuration section name every service's appsettings.json binds this from.</summary>
    public const string ConfigSectionName = "Identity";

    /// <summary>
    /// The identity server's base URL. Used both as <c>JwtBearerOptions.Authority</c> (from which
    /// the OIDC discovery document and JWKS are fetched and cached — research.md Decision 5) and as
    /// the expected <c>iss</c> claim.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>The expected <c>aud</c> claim on every token this service accepts.</summary>
    public string? Audience { get; set; }
}
