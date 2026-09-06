extern alias ProductsApi;

using IntegrationTestSupport;
using Microsoft.Extensions.DependencyInjection;
using ProductsApi::Products.Api.Data;
using ServiceDefaults;
using Tenancy;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 016-correlation-id-propagation spec US1/US3, research.md Decision 1/7: the BFF → domain-service
/// hop is the one place a typed <see cref="HttpClient"/> forwards nothing by itself — unlike the
/// gateway → BFF hop, which YARP relays for free (<c>Gateway.Api.IntegrationTests.CorrelationIdPropagationTests</c>).
/// Without an outbound handler carrying it, this hop is where a correlation ID generated at the edge
/// would silently stop, and every domain service reachable only through the BFF would mint its own
/// instead.
/// </summary>
/// <remarks>
/// Mirrors <see cref="TenantPropagationTests"/> exactly: the assertion is made on the outbound
/// request itself, via a recording handler inside the client's pipeline, because that is the thing
/// under test — whether the downstream service then likes what it received is that service's own
/// suite's business.
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class CorrelationPropagationTests(DownstreamServicesFixture fixture)
{
    /// <summary>
    /// An unresolved tenant would make Products throw <c>MissingTenantContextException</c>
    /// (contracts/tenant-id-header.md) before ever answering — turning this test's request into one
    /// the resilience pipeline treats as failed and retries, which would multiply
    /// <see cref="OutboundCorrelationIdRecorder.Observed"/> by the retry count instead of leaving it
    /// at one call per logical request. A resolved tenant keeps this suite about correlation ID
    /// propagation only, not an incidental proof of the resilience pipeline's retry budget.
    /// </summary>
    private const string ResolvedTenant = "contoso";

    [Fact]
    public async Task TheBffsOutboundCall_CarriesTheCorrelationIdTheBffReceived()
    {
        const string supplied = "bff-correlation-propagation-test";

        await using var products = await CreateEmptyProductsServiceAsync("bff-correlation-propagation");

        var recorder = new OutboundCorrelationIdRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, supplied);
        request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);

        await client.SendAsync(request);

        Assert.Equal(supplied, Assert.Single(recorder.Observed));
    }

    /// <summary>
    /// research.md Decision 7: the real risk this hop introduces is a pooled <see cref="HttpClient"/>
    /// handler capturing one request's state and replaying it for another — the same failure class
    /// <see cref="TenantPropagationHandler"/>'s own remarks warn about for the tenant. Reading
    /// <c>IHttpContextAccessor.HttpContext</c> fresh on every <c>SendAsync</c> call (rather than
    /// capturing it at construction) is what should prevent that; this proves it under real
    /// concurrent load rather than by argument alone.
    /// </summary>
    [Fact]
    public async Task TheBffsOutboundCalls_DoNotCrossContaminateCorrelationIds_UnderConcurrentRequests()
    {
        const int concurrentRequests = 10;

        await using var products = await CreateEmptyProductsServiceAsync("bff-correlation-concurrency");

        var recorder = new OutboundCorrelationIdRecorder();
        await using var bff = CreateRecordingBff(products, recorder);

        var sent = Enumerable.Range(0, concurrentRequests)
            .Select(i => $"concurrent-correlation-{i}")
            .ToArray();

        await Task.WhenAll(sent.Select(async correlationId =>
        {
            var client = bff.CreateClient().UseTestBearerToken();
            using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
            request.Headers.Add(CorrelationIdMiddleware.HeaderName, correlationId);
            request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);
            await client.SendAsync(request);
        }));

        var observed = recorder.Observed;
        Assert.Equal(sent.Length, observed.Count);
        // Every sent id is observed exactly once — no id missing, no id duplicated onto a different
        // request's outbound call (which is what cross-contamination would look like).
        Assert.Equal(sent.OrderBy(id => id), observed.OrderBy(id => id));
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
    /// correlation ID header and all, rather than before the propagation handler has run.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateRecordingBff(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program> products,
        OutboundCorrelationIdRecorder recorder) =>
        BffTestHost.CreateBff("ProductsApi", products).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient("ProductsApi")
                    .AddHttpMessageHandler(() => new RecordingHandler(recorder))));

    private sealed class OutboundCorrelationIdRecorder
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

        public void Record(string? correlationId)
        {
            lock (_observed)
            {
                _observed.Add(correlationId);
            }
        }
    }

    private sealed class RecordingHandler(OutboundCorrelationIdRecorder recorder) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            recorder.Record(
                request.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values)
                    ? values.Single()
                    : null);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
