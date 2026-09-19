extern alias BffApi;

using Gateway.Api.Identity;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// Spec US1 Acceptance Scenario 1 and Test Scenario 1: the gateway resolves a tenant for every
/// request and that same tenant reaches the BFF. Constitution Principle V makes the gateway the
/// sole resolution point, so this suite also pins the other half — a caller cannot declare its own
/// tenant.
/// </summary>
/// <remarks>
/// What the BFF actually received is recorded by a filter injected into the BFF's own pipeline,
/// rather than inferred from the response: the route deliberately fails (no products service is
/// running behind the BFF here), and a failure response says nothing about which headers arrived.
/// </remarks>
public class TenantPropagationTests
{
    /// <summary>
    /// Kiểm tra: request đi qua gateway tới `/bff/products` mang tới BFF đúng giá trị tenant mà gateway
    /// được cấu hình để phân giải (`StubIdentity:TenantId`).
    /// Lý do phải test: đây là nhánh happy-case của US1 kịch bản 1 — tenant được xác định 1 lần ở
    /// gateway rồi lan truyền xuống chặng kế tiếp. Giá trị mong đợi được đọc từ host đang chạy (không
    /// hard-code) để test kiểm tra việc lan truyền chứ không phải khẳng định lại 1 hằng số.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T016, US1.
    /// </summary>
    [Fact]
    public async Task ARequestThroughTheGateway_CarriesTheResolvedTenantToTheBff()
    {
        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient().UseTestBearerToken(tenantId: ResolvedTenantOf(gateway));

        await client.GetAsync("/bff/products");

        var observed = Assert.Single(recorder.Observed);
        Assert.Equal(ResolvedTenantOf(gateway), observed);
    }

    /// <summary>
    /// Kiểm tra: client tự gửi kèm `X-Tenant-Id: some-other-tenant` thì BFF vẫn nhận tenant do gateway
    /// phân giải, không phải giá trị client gửi.
    /// Lý do phải test: theo contracts/tenant-id-header.md, gateway "luôn ghi đè mọi giá trị đến ...
    /// tenant do client khai không bao giờ được tin". Nếu client tự chọn được tenant của mình thì ranh
    /// giới cách ly đã bị phá ngay trước khi bất kỳ service nào nhìn thấy request (FR-002).
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T016, US1.
    /// </summary>
    [Fact]
    public async Task ACallerSuppliedTenant_IsOverwritten_NeverTrusted()
    {
        const string CallerDeclaredTenant = "some-other-tenant";

        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient().UseTestBearerToken(tenantId: ResolvedTenantOf(gateway));

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(TenantHeaderPropagationMiddleware.HeaderName, CallerDeclaredTenant);

        await client.SendAsync(request);

        var observed = Assert.Single(recorder.Observed);
        Assert.NotEqual(CallerDeclaredTenant, observed);
        Assert.Equal(ResolvedTenantOf(gateway), observed);
    }

    /// <summary>
    /// Kiểm tra: với nhiều route khác nhau (`/bff/products`, `/bff/baskets/{id}`), request được chuyển
    /// tiếp tới BFF luôn mang 1 tenant khác rỗng.
    /// Lý do phải test: FR-001 yêu cầu phân giải tenant cho MỌI request, không chỉ vài route mẫu — mọi
    /// đường đi qua pipeline danh tính của gateway đều phải được stamp tenant.
    /// Task nguồn: spec 003 (danh tính giả lập và tenant) — T016, US1.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/bff/baskets/8a1f6f6e-0000-4000-8000-000000000001")]
    public async Task EveryForwardedRequest_CarriesATenant(string route)
    {
        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient().UseTestBearerToken(tenantId: ResolvedTenantOf(gateway));

        await client.GetAsync(route);

        Assert.False(string.IsNullOrWhiteSpace(Assert.Single(recorder.Observed)));
    }

    /// <summary>
    /// The tenant the gateway is configured to resolve. Read from the running host rather than
    /// duplicated as a literal here, so this suite tests propagation rather than re-asserting a
    /// constant — with a non-blank guard, since an unset value would otherwise make every
    /// assertion above pass against nothing.
    /// </summary>
    private static string ResolvedTenantOf(WebApplicationFactory<Program> gateway)
    {
        var configured = gateway.Services.GetRequiredService<IConfiguration>()["StubIdentity:TenantId"];
        Assert.False(string.IsNullOrWhiteSpace(configured), "The gateway must be configured with a Phase 1 tenant id.");

        return configured;
    }

    private static WebApplicationFactory<BffApi::Program> CreateRecordingBff(TenantHeaderRecorder recorder) =>
        GatewayTestHost.CreateBff().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddSingleton<IStartupFilter>(new TenantHeaderRecordingStartupFilter(recorder))));

    private sealed class TenantHeaderRecorder
    {
        private readonly List<string?> _observed = [];

        public IReadOnlyList<string?> Observed
        {
            get
            {
                lock (_observed)
                {
                    return _observed.ToArray();
                }
            }
        }

        public void Record(string? tenantId)
        {
            lock (_observed)
            {
                _observed.Add(tenantId);
            }
        }
    }

    /// <summary>
    /// Records the tenant header as the BFF received it, at the very front of the BFF's pipeline —
    /// before anything in the BFF could have set one itself.
    /// </summary>
    private sealed class TenantHeaderRecordingStartupFilter(TenantHeaderRecorder recorder) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            app =>
            {
                app.Use(async (context, nextMiddleware) =>
                {
                    recorder.Record(
                        context.Request.Headers.TryGetValue(TenantHeaderPropagationMiddleware.HeaderName, out var value)
                            ? value.ToString()
                            : null);

                    await nextMiddleware();
                });

                next(app);
            };
    }
}
