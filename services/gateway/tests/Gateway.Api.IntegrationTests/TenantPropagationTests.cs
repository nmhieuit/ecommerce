extern alias BffApi;

using Gateway.Api.Identity;
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
    [Fact]
    public async Task ARequestThroughTheGateway_CarriesTheResolvedTenantToTheBff()
    {
        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        await client.GetAsync("/bff/products");

        var observed = Assert.Single(recorder.Observed);
        Assert.Equal(ResolvedTenantOf(gateway), observed);
    }

    /// <summary>
    /// contracts/tenant-id-header.md: the gateway "always overwrites any inbound value ... a
    /// client-supplied <c>X-Tenant-Id</c> is never trusted". A caller who could name their own
    /// tenant would have defeated the isolation boundary before any service saw the request.
    /// </summary>
    [Fact]
    public async Task ACallerSuppliedTenant_IsOverwritten_NeverTrusted()
    {
        const string CallerDeclaredTenant = "some-other-tenant";

        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(TenantHeaderPropagationMiddleware.HeaderName, CallerDeclaredTenant);

        await client.SendAsync(request);

        var observed = Assert.Single(recorder.Observed);
        Assert.NotEqual(CallerDeclaredTenant, observed);
        Assert.Equal(ResolvedTenantOf(gateway), observed);
    }

    /// <summary>
    /// Every request, not only the ones that route somewhere useful — an unmatched path still
    /// passes through the gateway's identity pipeline.
    /// </summary>
    [Theory]
    [InlineData("/bff/products")]
    [InlineData("/bff/baskets/8a1f6f6e-0000-4000-8000-000000000001")]
    public async Task EveryForwardedRequest_CarriesATenant(string route)
    {
        var recorder = new TenantHeaderRecorder();
        await using var bff = CreateRecordingBff(recorder);
        await using var gateway = GatewayTestHost.CreateGateway(bff);
        var client = gateway.CreateClient();

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
