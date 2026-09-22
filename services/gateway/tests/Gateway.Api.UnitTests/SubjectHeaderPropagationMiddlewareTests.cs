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

    /// <summary>
    /// Kiểm tra: principal đã xác thực có claim `NameIdentifier` thì gateway ghi header
    /// `X-Subject-Id` bằng đúng giá trị đó.
    /// Lý do: gateway là thành phần duy nhất được nói ai là người gọi
    /// (contracts/subject-id-header.md); đây là nhánh happy-case của cơ chế lan truyền subject.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T016, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_StampsTheSubjectHeader_FromTheAuthenticatedPrincipal()
    {
        var httpContext = CreateContextFor(ResolvedSubject);

        await CreateMiddleware().InvokeAsync(httpContext);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Header danh tính trên
        // request đi tiếp phải bằng subject của người dùng;
        Assert.Equal(
            ResolvedSubject,
            httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName].ToString());
    }

    /// <summary>
    /// Kiểm tra: client tự gửi `X-Subject-Id` khác thì gateway vẫn ghi đè bằng subject đã phân
    /// giải.
    /// Lý do: người gọi tự đặt được subject của mình là đọc và thanh toán được giỏ của người khác;
    /// giá trị đến bị ghi đè, không bao giờ trộn hay tin.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T016, US2 (FR-006).
    /// </summary>
    [Fact]
    public async Task InvokeAsync_OverwritesACallerSuppliedSubject_NeverTrustsIt()
    {
        const string CallerDeclaredSubject = "somebody-else";

        var httpContext = CreateContextFor(ResolvedSubject);
        httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName] = CallerDeclaredSubject;

        await CreateMiddleware().InvokeAsync(httpContext);

        var observed = httpContext.Request.Headers[SubjectHeaderPropagationMiddleware.HeaderName].ToString();
        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau. Đỏ khi giá
        // trị giả của client được giữ.
        Assert.NotEqual(CallerDeclaredSubject, observed);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác. Header phải là subject
        // thật của principal.
        Assert.Equal(ResolvedSubject, observed);
    }

    /// <summary>
    /// Kiểm tra: khi principal không có subject (null, rỗng, khoảng trắng) thì header
    /// `X-Subject-Id` bị XOÁ.
    /// Lý do: để nguyên giá trị client gửi là đúng cửa ngách mà việc ghi đè phía trên đã đóng. Đây
    /// là test đơn vị vì nhánh này không thể tới được qua gateway thật khi stub luôn thành công.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T016, US2 (FR-006).
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

        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng. Đỏ khi header giả vẫn lọt
        // xuống.
        Assert.False(
            httpContext.Request.Headers.ContainsKey(SubjectHeaderPropagationMiddleware.HeaderName));
    }

    /// <summary>
    /// Kiểm tra: dù có subject hay không, middleware luôn gọi tiếp pipeline.
    /// Lý do: middleware chỉ stamp/xoá header, không tự chặn request; việc từ chối do các chặng
    /// phía sau đảm nhiệm.
    /// Task nguồn: spec 004 (SPA mua sắm tối thiểu) — T016, US2 (FR-006).
    /// </summary>
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

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai. Đạt khi middleware kế tiếp
        // đã được gọi; đỏ khi middleware này tự chặn request.
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
