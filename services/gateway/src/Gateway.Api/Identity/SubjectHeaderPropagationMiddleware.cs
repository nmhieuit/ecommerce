using System.Security.Claims;

namespace Gateway.Api.Identity;

/// <summary>
/// Turns the authenticated identity's subject claim into the <c>X-Subject-Id</c> request header
/// that every hop below the gateway reads
/// (004-minimal-shopping-spa contracts/subject-id-header.md).
/// </summary>
/// <remarks>
/// <para>
/// A deliberate mirror of <see cref="TenantHeaderPropagationMiddleware"/>. The two carry different
/// facts — which data store a request may reach, and whose rows inside it — but the mechanism is
/// identical, so keeping them identical means a reader who understands one understands both, and
/// Phase 3's swap to a real issuer changes neither.
/// </para>
/// <para>
/// Written onto the <em>request</em>, so YARP's default forwarding carries it. Any inbound value is
/// always overwritten, never merged or trusted: a caller who could name their own subject would be
/// reading and checking out another shopper's basket.
/// </para>
/// </remarks>
public sealed class SubjectHeaderPropagationMiddleware
{
    public const string HeaderName = "X-Subject-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<SubjectHeaderPropagationMiddleware> _logger;

    public SubjectHeaderPropagationMiddleware(
        RequestDelegate next,
        ILogger<SubjectHeaderPropagationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var subjectId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrWhiteSpace(subjectId))
        {
            // Nothing resolved a caller for this request, so nothing may claim one on its behalf.
            context.Request.Headers.Remove(HeaderName);

            await _next(context);
            return;
        }

        context.Request.Headers[HeaderName] = subjectId;

        // The same logging scope Tenancy's CallerContextMiddleware opens at every hop below. The
        // gateway does not use that middleware — it produces the header rather than consuming one —
        // but the hop that resolved the caller is the last one that should be missing from the
        // trail (constitution Principle VII).
        using (_logger.BeginScope(new Dictionary<string, object> { ["SubjectId"] = subjectId }))
        {
            await _next(context);
        }
    }
}
