extern alias ProductsApi;

using Microsoft.Extensions.DependencyInjection;
using ProductsApi::Products.Api.Data;
using Tenancy;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Spec US1 Acceptance Scenario 1, the BFF → domain-service hop. YARP forwards headers to the BFF
/// for free; a typed <see cref="HttpClient"/> does not, so without an outbound handler the chain
/// would break exactly here (research.md Decision 4).
/// </summary>
/// <remarks>
/// The assertion is made on the outbound request itself, by a recording handler sitting inside the
/// client's pipeline, because that is the thing under test — whether the downstream service then
/// likes what it received is that service's own suite's business (US2).
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class TenantPropagationTests(DownstreamServicesFixture fixture)
{
    private const string ResolvedTenant = "contoso";

    [Fact]
    public async Task TheBffsOutboundCall_CarriesTheTenantTheBffReceived()
    {
        await using var products = await CreateEmptyProductsServiceAsync("bff-tenant-propagation");

        var recorder = new OutboundTenantRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);

        await client.SendAsync(request);

        Assert.Equal(ResolvedTenant, Assert.Single(recorder.Observed));
    }

    /// <summary>
    /// contracts/tenant-id-header.md: the BFF "relays, does not resolve". If its own context is
    /// Unresolved — the gateway was bypassed — it must send no header rather than inventing one,
    /// so the failure propagates downstream instead of being masked by a default.
    /// </summary>
    [Fact]
    public async Task TheBffsOutboundCall_CarriesNoTenant_WhenTheBffItselfHasNone()
    {
        await using var products = await CreateEmptyProductsServiceAsync("bff-tenant-propagation-unresolved");

        var recorder = new OutboundTenantRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient();

        // No X-Tenant-Id on the way in: nothing resolved this request's tenant.
        await client.GetAsync("/bff/products");

        Assert.Null(Assert.Single(recorder.Observed));
    }

    private Task<Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program>>
        CreateEmptyProductsServiceAsync(string database) =>
        BffTestHost.CreateDownstreamAsync<ProductsApi::Program, ProductsDbContext>(
            "ProductsDb",
            fixture.ConnectionStringFor(database),
            async dbContext =>
            {
                dbContext.Products.RemoveRange(dbContext.Products);
                await dbContext.SaveChangesAsync();
            });

    /// <summary>
    /// Appends the recorder to the products client's handler pipeline. Registered after the BFF's
    /// own handlers, which puts it innermost — so it observes the request as it finally leaves,
    /// tenant header and all, rather than before the propagation handler has run.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateRecordingBff(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program> products,
        OutboundTenantRecorder recorder) =>
        BffTestHost.CreateBff("ProductsApi", products).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient("ProductsApi")
                    .AddHttpMessageHandler(() => new RecordingHandler(recorder))));

    private sealed class OutboundTenantRecorder
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

    private sealed class RecordingHandler(OutboundTenantRecorder recorder) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            recorder.Record(
                request.Headers.TryGetValues(TenantContextMiddleware.HeaderName, out var values)
                    ? values.Single()
                    : null);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
