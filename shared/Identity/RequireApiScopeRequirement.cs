using Microsoft.AspNetCore.Authorization;

namespace Identity;

/// <summary>
/// Marker requirement satisfied by <see cref="RequireApiScopeAuthorizationHandler"/> — carries no
/// data of its own; the toggle and claim check both live in the handler (015-deny-by-default-authz,
/// research.md Decision 1).
/// </summary>
public sealed class RequireApiScopeRequirement : IAuthorizationRequirement;
