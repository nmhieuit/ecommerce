using System.Security.Claims;
using System.Text.Encodings.Web;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Gateway.Api.UnitTests;

/// <summary>
/// The stub identity on its own, away from the full pipeline the gateway's integration tests drive
/// through it. research.md Decision 1 rests on this being a real authentication scheme rather than a
/// header-stamping shortcut, so the claim it issues is worth asserting directly: everything below
/// the gateway reads that claim and nothing else.
/// </summary>
public class StubIdentityAuthenticationHandlerTests
{
    private const string ConfiguredTenant = "contoso";
    private const string ConfiguredSubject = "phase1-stub-user";

    /// <summary>
    /// Kiểm tra: `AuthenticateAsync` thành công (`Succeeded`, không có `Failure`) với 1 request bất kỳ.
    /// Lý do phải test: danh tính giả lập Phase 1 luôn thành công — chưa có credential nào để kiểm tra
    /// (spec Assumptions). Nếu nó từ chối được thì mọi request qua gateway sẽ mất tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_Succeeds_ForAnyRequest()
    {
        var result = await AuthenticateAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.Failure);
    }

    /// <summary>
    /// Kiểm tra: principal được cấp có claim `tenant_id` bằng đúng tenant đã cấu hình ("contoso").
    /// Lý do phải test: toàn bộ chặng phía sau gateway chỉ đọc đúng claim này và không gì khác
    /// (FR-001, FR-007) — đây là điểm neo của nguồn phân giải tenant, thứ mà JWT thật sẽ thay thế sau.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_IssuesTheConfiguredTenantClaim()
    {
        var result = await AuthenticateAsync();

        var tenant = result.Principal?.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType);
        Assert.NotNull(tenant);
        Assert.Equal(ConfiguredTenant, tenant.Value);
    }

    /// <summary>
    /// Kiểm tra: principal có thêm claim subject (`NameIdentifier`) bằng đúng subject đã cấu hình.
    /// Lý do phải test: data-model.md (Stub Identity) — principal giả lập mang cả subject lẫn tenant để
    /// có hình dạng giống principal thật sẽ thay thế nó ở Phase 3, thay vì chỉ mang mỗi tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_IssuesTheConfiguredSubjectClaim()
    {
        var result = await AuthenticateAsync();

        Assert.Equal(ConfiguredSubject, result.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value);
    }

    /// <summary>
    /// Kiểm tra: đổi path hoặc gửi kèm header `Authorization: Bearer not-a-real-token` không làm đổi
    /// kết quả — vẫn thành công với đúng tenant đã cấu hình.
    /// Lý do phải test: Phase 1 không có credential để trình (spec Assumptions) nên không yếu tố nào của
    /// request được phép ảnh hưởng tới câu trả lời — kể cả việc client cố tác động qua header.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/anything-at-all")]
    public async Task AuthenticateAsync_IgnoresTheRequest_HavingNoCredentialsToRead(string path)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Path = path;
        httpContext.Request.Headers.Authorization = "Bearer not-a-real-token";

        var result = await AuthenticateAsync(httpContext: httpContext);

        Assert.True(result.Succeeded);
        Assert.Equal(
            ConfiguredTenant,
            result.Principal?.FindFirst(StubIdentityAuthenticationHandler.TenantClaimType)?.Value);
    }

    /// <summary>
    /// Kiểm tra: khi tenant cấu hình là chuỗi rỗng hoặc khoảng trắng, xác thực THẤT BẠI (không có
    /// principal).
    /// Lý do phải test: gateway chưa cấu hình thì không được xác thực bất kỳ ai. 1 principal không có
    /// tenant chỉ là 1 request Unresolved đội lốt — mọi chặng phía sau vẫn coi nó là Unresolved — nên
    /// thất bại ngay tại đây giữ cho nguyên tắc "phân giải 1 lần ở biên" đúng thật chứ không chỉ đúng
    /// trên danh nghĩa.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task AuthenticateAsync_Fails_WhenNoTenantIsConfigured(string unconfigured)
    {
        var result = await AuthenticateAsync(tenantId: unconfigured);

        Assert.False(result.Succeeded);
        Assert.Null(result.Principal);
    }

    /// <summary>
    /// Kiểm tra: identity được cấp có `AuthenticationType` bằng đúng tên scheme của stub.
    /// Lý do phải test: research.md Decision 1 dựa trên việc stub là 1 authentication scheme thật (dù
    /// giả), không phải lối tắt tự stamp header — tên scheme là dấu vết để xác nhận điều đó và để phân
    /// biệt nó với JwtBearer khi toggle cutover được bật.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T038, US1.
    /// </summary>
    [Fact]
    public async Task AuthenticateAsync_IssuesAnIdentityNamingTheStubScheme()
    {
        var result = await AuthenticateAsync();

        Assert.Equal(
            StubIdentityAuthenticationHandler.SchemeName,
            result.Principal?.Identity?.AuthenticationType);
    }

    private static async Task<AuthenticateResult> AuthenticateAsync(
        string tenantId = ConfiguredTenant,
        HttpContext? httpContext = null)
    {
        var options = new StubIdentityAuthenticationSchemeOptions
        {
            TenantId = tenantId,
            SubjectId = ConfiguredSubject,
        };

        var handler = new StubIdentityAuthenticationHandler(
            new StaticOptionsMonitor(options),
            NullLoggerFactory.Instance,
            UrlEncoder.Default);

        await handler.InitializeAsync(
            new AuthenticationScheme(
                StubIdentityAuthenticationHandler.SchemeName,
                displayName: null,
                handlerType: typeof(StubIdentityAuthenticationHandler)),
            httpContext ?? new DefaultHttpContext());

        return await handler.AuthenticateAsync();
    }

    /// <summary>
    /// The handler resolves its options through <see cref="IOptionsMonitor{TOptions}"/>; nothing
    /// here reloads them, so a fixed instance is the whole contract.
    /// </summary>
    private sealed class StaticOptionsMonitor(StubIdentityAuthenticationSchemeOptions options)
        : IOptionsMonitor<StubIdentityAuthenticationSchemeOptions>
    {
        public StubIdentityAuthenticationSchemeOptions CurrentValue => options;

        public StubIdentityAuthenticationSchemeOptions Get(string? name) => options;

        public IDisposable? OnChange(Action<StubIdentityAuthenticationSchemeOptions, string?> listener) => null;
    }
}
