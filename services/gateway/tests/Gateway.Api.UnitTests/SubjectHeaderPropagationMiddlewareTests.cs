using System.Security.Claims;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gateway.Api.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa contracts/subject-id-header.md: the gateway is the only component that
/// may say who the caller is. This suite pins all three halves of that — it stamps the resolved
/// subject, it overwrites anything the caller sent, and it removes the header entirely when nothing
/// resolved rather than letting a caller-supplied value through.
/// </summary>
/// <remarks>
/// A unit test rather than an integration one, unlike the tenant's equivalent coverage, because the
/// third case is unreachable end to end: Phase 1's stub authentication handler always succeeds, so
/// no request through a running gateway can arrive with an unresolved principal. That is exactly
/// the case worth pinning before Phase 3 replaces the stub with a real issuer that can fail.
/// </remarks>
public class SubjectHeaderPropagationMiddlewareTests
{
    private const string ResolvedSubject = "phase1-stub-user";

    [Fact]
    public async Task InvokeAsync_StampsTheSubjectHeader_FromTheAuthenticatedPrincipal()
    {
        var httpContext = CreateContextFor(ResolvedSubject);

        await CreateMiddleware().InvokeAsync(httpContext);

        Assert.Equal(
            ResolvedSubject,
            httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName].ToString());
    }

    /// <summary>
    /// A caller who could name their own subject would be reading and checking out somebody else's
    /// basket. The inbound value is overwritten, never merged and never trusted.
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OverwritesACallerSuppliedSubject_NeverTrustsIt()
    {
        const string CallerDeclaredSubject = "somebody-else";

        var httpContext = CreateContextFor(ResolvedSubject);
        httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName] = CallerDeclaredSubject;

        await CreateMiddleware().InvokeAsync(httpContext);

        var observed = httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName].ToString();
        Assert.NotEqual(CallerDeclaredSubject, observed);
        Assert.Equal(ResolvedSubject, observed);
    }

    /// <summary>
    /// Removing rather than leaving the inbound value alone is the point: passing a caller's own
    /// header through unchecked is precisely the smuggling route the overwrite above closes.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvokeAsync_RemovesTheHeader_WhenNoSubjectIsResolved(string? resolvedSubject)
    {
        var httpContext = CreateContextFor(resolvedSubject);
        httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName] = "somebody-else";

        await CreateMiddleware().InvokeAsync(httpContext);

        Assert.False(
            httpContext.Request.Headers.ContainsKey(SubjectHeaderPropagationMiddleware.HeaderName));
    }

    [Theory]
    [InlineData(ResolvedSubject)]
    [InlineData(null)]
    public async Task InvokeAsync_AlwaysCallsTheRestOfThePipeline(string? resolvedSubject)
    {
        var called = false;
        var middleware = new SubjectHeaderPropagationMiddleware(
            _ =>
            {
                called = true;
                return Task.CompletedTask;
            },
            NullLogger<SubjectHeaderPropagationMiddleware>.Instance);

        await middleware.InvokeAsync(CreateContextFor(resolvedSubject));

        Assert.True(called);
    }

    private static SubjectHeaderPropagationMiddleware CreateMiddleware() =>
        new(_ => Task.CompletedTask, NullLogger<SubjectHeaderPropagationMiddleware>.Instance);

    /// <summary>
    /// Builds a context whose principal carries the given subject claim, or no claim at all when
    /// <paramref name="subjectId"/> is <see langword="null"/> — the shape
    /// <c>StubIdentityAuthenticationHandler</c> produces.
    /// </summary>
    private static DefaultHttpContext CreateContextFor(string? subjectId)
    {
        var claims = subjectId is null
            ? Array.Empty<Claim>()
            : [new Claim(ClaimTypes.NameIdentifier, subjectId)];

        return new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestScheme")),
        };
    }
}
