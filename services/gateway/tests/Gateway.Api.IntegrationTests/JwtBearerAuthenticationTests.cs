extern alias BffApi;

using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Gateway.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// spec US1 Acceptance Scenario 2 (a valid token is treated as a genuine, resolved identity — not
/// the Phase 1 fake user) and constitution Principle X (toggle rollback without redeploy). Tests the
/// gateway's *consumption* of a token — tasks.md T017, against a real running <c>Identity.Api</c>,
/// covers *issuance*; this suite proves the gateway side independently by supplying its own
/// symmetric-key-signed token and bypassing the real OIDC discovery/JWKS fetch (research.md
/// Decision 5) that a real <c>Authority</c> would otherwise require over the network.
/// </summary>
public class JwtBearerAuthenticationTests
{
    private const string TestSigningKey = "integration-test-jwt-signing-key-at-least-32-bytes!!";
    private const string TestTenantId = "contoso-jwt-test";
    private const string TestSubjectId = "jwt-bearer-test-user";

    /// <summary>
    /// spec US1 Acceptance Scenario 2, FR-008: a valid token is authenticated, and
    /// <see cref="TenantHeaderPropagationMiddleware"/>/<see cref="SubjectHeaderPropagationMiddleware"/>
    /// produce the same headers from its claims as they always have from
    /// <c>StubIdentityAuthenticationHandler</c>'s — proving research.md Decision 3's claim that
    /// nothing downstream of the authentication scheme needed to change.
    /// </summary>
    /// <summary>
    /// Kiểm tra: với toggle BẬT (dùng `JwtBearer` thật), 1 token hợp lệ được gateway xác thực, và
    /// đúng `tenant_id`/`sub` trong token đó lan truyền xuống BFF qua header.
    /// Lý do: chứng minh research.md Decision 3 — không có gì ở tầng downstream (middleware lan
    /// truyền) cần sửa, vì token thật phát đúng loại claim mà stub cũ đã phát.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US1 (FR-008).
    /// </summary>
    [Fact]
    public async Task ARequestWithAValidToken_PropagatesTenantAndSubject_WhenToggleIsOn()
    {
        var recorder = new HeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = CreateGatewayWithTestJwtBearer(bff, toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken());

        var response = await client.SendAsync(request);

        // Assert.NotEqual(giá trị cấm, thực tế): xanh khi khác nhau, đỏ khi bằng nhau — mã không
        // được là 401.
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn — BFF phải nhận đúng 1 request.
        var observed = Assert.Single(recorder.Observed);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — header BFF thấy phải
        // đúng claim đã ký trong token.
        Assert.Equal(TestTenantId, observed.TenantId);
        Assert.Equal(TestSubjectId, observed.SubjectId);
    }

    /// <summary>
    /// spec US2 Acceptance Scenario 1 (the gateway validates before forwarding) and spec FR-011: no
    /// token is exactly as unauthenticated as an invalid one.
    /// </summary>
    /// <summary>
    /// Kiểm tra: với toggle BẬT, request không kèm token nào bị gateway từ chối `401`.
    /// Lý do: FR-011 — không token thì không có danh tính mặc định nào cả, phải bị từ chối y hệt
    /// token không hợp lệ.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US2 (FR-011).
    /// </summary>
    [Fact]
    public async Task ARequestWithNoToken_IsRejected_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/bff/products");

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// spec US2 Test Scenario 2 / Acceptance Scenario 3: a tampered token is rejected — the gateway
    /// does not trust a signature it cannot verify.
    /// </summary>
    /// <summary>
    /// Kiểm tra: token hợp lệ bị nối thêm chuỗi rác vào cuối (làm hỏng chữ ký) bị gateway từ chối
    /// `401`.
    /// Lý do: gateway không được tin bất kỳ chữ ký nào nó không tự xác minh được khớp khoá.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US2, Test Scenario 2 (FR-005, SC-002).
    /// </summary>
    [Fact]
    public async Task ARequestWithATamperedToken_IsRejected_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken() + "tampered");

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// spec US3 Acceptance Scenario 1/2, Test Scenario 3: an expired token is rejected with a clear,
    /// distinguishable response — not the framework's default empty-body 401, and not conflated with
    /// a tampered or malformed token (data-model.md — Token, trạng thái Expired).
    /// </summary>
    /// <summary>
    /// Kiểm tra: token đã hết hạn (5 phút trước) bị từ chối `401`, và body nêu rõ `token_expired`
    /// — không phải 401 rỗng chung chung.
    /// Lý do: US3/FR-006 — người dùng cần biết chính xác lý do bị từ chối là "hết hạn, đăng nhập
    /// lại" chứ không phải lỗi mơ hồ khiến họ không biết làm gì tiếp.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — US3, Test Scenario 3 (FR-006, SC-003).
    /// </summary>
    [Fact]
    public async Task ARequestWithAnExpiredToken_IsRejected_WithAClearExpiredMessage_WhenToggleIsOn()
    {
        await using var gateway = CreateGatewayWithTestJwtBearer(GatewayTestHost.CreateBff(), toggleOn: true);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", CreateToken(expired: true));

        var response = await client.SendAsync(request);

        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        // Assert.Contains(chuỗi con, chuỗi): xanh khi chuỗi có chứa chuỗi con, đỏ khi không — body
        // phải nêu đích danh "token_expired", không phải thông báo chung chung.
        Assert.Contains("token_expired", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Kiểm tra: với toggle TẮT (dùng `StubIdentity`), request không kèm token vẫn phải chạm được
    /// tới pipeline của BFF — tức gateway không tự đòi token khi ở chế độ rollback.
    /// Lý do: đây là kịch bản rollback khẩn cấp của Principle X — gạt toggle về false phải phục hồi
    /// hành vi Phase 1 của CHÍNH gateway ngay lập tức, không cần redeploy; BFF vẫn tự xác thực độc
    /// lập theo Principle VI nên không phụ thuộc toggle này.
    /// Lưu ý: hiện ĐANG ĐỎ — `StubIdentityAuthenticationHandler` chưa phát hành claim `scope`, còn
    /// `FallbackPolicy` dùng chung (sửa bởi spec 015) nay đòi cả claim đó khi
    /// `AuthorizationRequireApiScope=true` (mặc định trong `appsettings.Development.json`, đúng môi
    /// trường `docker-compose.local.yml` dùng) — gateway tự trả `403 forbidden_scope`, request
    /// không tới được BFF. Xem QA_Debt mục 014.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — Principle X, research.md Decision 7.
    /// </summary>
    [Fact]
    public async Task ARequestWithNoToken_StillReachesTheBff_WhenToggleIsOff()
    {
        var recorder = new HeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = CreateGatewayWithTestJwtBearer(bff, toggleOn: false);
        var client = gateway.CreateClient();

        await client.GetAsync("/bff/products");

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử, đỏ khi 0 hoặc nhiều hơn — BFF phải
        // nhận đúng 1 request. Đỏ hiện tại vì tập hợp rỗng (xem "Lưu ý" ở trên).
        Assert.Single(recorder.Observed);
    }

    private static string CreateToken(bool expired = false)
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            claims:
            [
                new Claim(JwtRegisteredClaimNames.Sub, TestSubjectId),
                new Claim(TenantClaimType, TestTenantId),
                // 015-deny-by-default-authz: appsettings.Development.json defaults
                // AuthorizationRequireApiScope to true, so RequireApiScopeAuthorizationHandler
                // requires this claim on the FallbackPolicy the gateway forwards through — without
                // it the token is authenticated but still rejected (403) before ever reaching the
                // BFF. Mirrors TestJwtBearer.CreateToken's default (IntegrationTestSupport).
                new Claim("scope", "ecommerce-api"),
            ],
            expires: expired ? DateTime.UtcNow.AddMinutes(-5) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    /// <summary>Must equal <c>Identity.Api.HostedIdentity.TenantClaimsProfileService.TenantClaimType</c> — a wire contract, not shared code (research.md Decision 3).</summary>
    private const string TenantClaimType = "tenant_id";

    private static WebApplicationFactory<Program> CreateGatewayWithTestJwtBearer(
        WebApplicationFactory<BffApi::Program> bff, bool toggleOn) =>
        GatewayTestHost.CreateGateway(bff).WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["FeatureToggles:IdentityServerAuthCutover"] = toggleOn ? "true" : "false",
                }));

            builder.ConfigureServices(services =>
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    // A non-null Configuration alone is not enough: AddToggleGatedIdentity() already
                    // set a real Authority, so the framework's own PostConfigureOptions<JwtBearerOptions>
                    // (registered inside AddJwtBearer(), run before this one) already built a real,
                    // network-fetching ConfigurationManager from it before this PostConfigure runs.
                    // JwtBearerHandler checks ConfigurationManager, not Configuration, so without the
                    // line below it still calls out to the real (absent in tests) Authority on every
                    // request that carries a token (research.md Decision 5 — this suite tests the
                    // gateway's consumption of a token, not the identity server's issuance, that's
                    // tasks.md T017, against a real running Identity.Api).
                    options.Configuration = new OpenIdConnectConfiguration();
                    options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(options.Configuration);
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = false,
                        ValidateAudience = false,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSigningKey)),
                    };
                }));
        });

    private static WebApplicationFactory<BffApi::Program> CreateRecordingBff(HeaderRecorder recorder) =>
        GatewayTestHost.CreateBff().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new HeaderRecordingStartupFilter(recorder))));

    private sealed record ObservedHeaders(string? TenantId, string? SubjectId);

    private sealed class HeaderRecorder
    {
        private readonly List<ObservedHeaders> _observed = [];

        public IReadOnlyList<ObservedHeaders> Observed
        {
            get
            {
                lock (_observed)
                {
                    return _observed.ToArray();
                }
            }
        }

        public void Record(ObservedHeaders headers)
        {
            lock (_observed)
            {
                _observed.Add(headers);
            }
        }
    }

    private sealed class HeaderRecordingStartupFilter(HeaderRecorder recorder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    recorder.Record(new ObservedHeaders(
                        context.Request.Headers.TryGetValue(TenantHeaderPropagationMiddleware.HeaderName, out var tenant)
                            ? tenant.ToString()
                            : null,
                        context.Request.Headers.TryGetValue(SubjectHeaderPropagationMiddleware.HeaderName, out var subject)
                            ? subject.ToString()
                            : null));

                    await nextMiddleware();
                });

                next(app);
            };
    }
}
