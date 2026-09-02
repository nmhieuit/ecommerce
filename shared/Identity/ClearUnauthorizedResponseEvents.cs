using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.IdentityModel.Tokens;

namespace Identity;

/// <summary>
/// Makes an expired token's rejection distinguishable from any other validation failure
/// (data-model.md — Token, trạng thái Expired; spec FR-006, US3) — the framework's default
/// <c>401</c> carries no body and no way to tell "expired" from "tampered" or "malformed" apart.
/// </summary>
/// <remarks>
/// Applied to both the shared <c>AddIdentityValidation()</c> (BFF + 4 domain services) and the
/// gateway's own toggle-gated registration (<c>ToggleGatedAuthenticationExtensions</c>), which
/// cannot call <c>AddIdentityValidation()</c> directly (it needs the toggle's three-scheme
/// registration instead) — this is the one piece both share, so the response shape stays identical
/// everywhere a token is rejected.
/// </remarks>
public static class ClearUnauthorizedResponseEvents
{
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    public static void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.Events ??= new JwtBearerEvents();

        options.Events.OnAuthenticationFailed = context =>
        {
            // Stashed on HttpContext.Items, not a field: a fresh AuthenticationFailedContext/
            // ForbiddenContext pair is created per request by the framework, but OnChallenge below
            // needs to know what OnAuthenticationFailed observed for *this* request.
            context.HttpContext.Items[TokenExpiredItemKey] = context.Exception is SecurityTokenExpiredException;
            return Task.CompletedTask;
        };

        options.Events.OnChallenge = context =>
        {
            // Suppress the default WWW-Authenticate-only, empty-body 401 so the explicit body below
            // is what a caller actually sees — the whole point of this type.
            context.HandleResponse();

            var expired = context.HttpContext.Items.TryGetValue(TokenExpiredItemKey, out var value) && value is true;

            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json; charset=utf-8";

            return context.Response.WriteAsync(JsonSerializer.Serialize(
                new
                {
                    error = expired ? "token_expired" : "unauthorized",
                    message = expired
                        ? "The bearer token has expired."
                        : "Authentication is required, or the supplied token is invalid.",
                },
                ResponseJsonOptions));
        };
    }

    private const string TokenExpiredItemKey = "Identity.ClearUnauthorizedResponseEvents.TokenExpired";
}
