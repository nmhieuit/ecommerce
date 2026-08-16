using Microsoft.AspNetCore.Authentication;

namespace Gateway.Api.Identity;

/// <summary>
/// The fixed identity Phase 1 resolves for every request, in place of a real identity server
/// (data-model.md — Stub Identity). Bound from the <c>StubIdentity</c> configuration section.
/// </summary>
public sealed class StubIdentityAuthenticationSchemeOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// The one tenant Phase 1 can resolve (spec FR-008). Deliberately has no default: a hardcoded
    /// fallback here would be a second, invisible resolution source, and an unconfigured gateway
    /// should fail loudly rather than quietly serve some built-in tenant.
    /// </summary>
    public string TenantId { get; set; } = string.Empty;

    /// <summary>
    /// The fake user's identifier. Nothing in Phase 1 acts on it — it exists so that the principal
    /// this scheme issues is shaped like the real one Phase 3 will replace it with, rather than
    /// carrying a tenant and nothing else.
    /// </summary>
    public string SubjectId { get; set; } = string.Empty;
}
