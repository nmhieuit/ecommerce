using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Identity;

/// <summary>
/// Makes an authorization-policy rejection distinguishable from a generic empty-body 403
/// (015-deny-by-default-authz, research.md Decision 6) — the 403 counterpart to
/// <see cref="ClearUnauthorizedResponseEvents"/>'s 401 handling from 014.
/// </summary>
/// <remarks>
/// A <see cref="PolicyAuthorizationResult.Forbidden"/> result only ever means "authenticated
/// successfully, but a requirement — here, <see cref="RequireApiScopeRequirement"/> — was not met":
/// the framework's own <c>PolicyEvaluator</c> returns Challenge instead whenever authentication
/// itself failed or was absent, so this handler never needs to re-check
/// <c>context.User.Identity.IsAuthenticated</c> itself.
/// </remarks>
public sealed class ClearForbiddenResponseEvents : Microsoft.AspNetCore.Authorization.IAuthorizationMiddlewareResultHandler
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Forbidden)
        {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json; charset=utf-8";

        await context.Response.WriteAsync(JsonSerializer.Serialize(
            new
            {
                error = "forbidden_scope",
                message = "Authentication succeeded, but the token does not carry the required scope.",
            },
            ResponseJsonOptions));
    }
}
