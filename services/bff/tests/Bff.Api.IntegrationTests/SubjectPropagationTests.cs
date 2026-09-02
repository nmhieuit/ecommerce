extern alias ProductsApi;

using IntegrationTestSupport;
using Microsoft.Extensions.DependencyInjection;
using ProductsApi::Products.Api.Data;
using Tenancy;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// 004-minimal-shopping-spa contracts/subject-id-header.md, the BFF → domain-service hop. The same
/// gap the tenant had exists for the caller: YARP forwards headers into the BFF for free, but a
/// typed <see cref="HttpClient"/> forwards nothing, so without the outbound handler the subject
/// would silently stop at the BFF and every basket lookup below would be caller-less.
/// </summary>
/// <remarks>
/// A deliberate mirror of <see cref="TenantPropagationTests"/> — same recorder shape, same
/// reasoning — because the two headers must behave identically and a divergence should read as a
/// diff between these two files.
/// </remarks>
[Collection(DownstreamServicesCollectionDefinition.Name)]
public class SubjectPropagationTests(DownstreamServicesFixture fixture)
{
    private const string ResolvedTenant = "contoso";
    private const string ResolvedSubject = "phase1-stub-user";

    [Fact]
    public async Task TheBffsOutboundCall_CarriesTheSubjectTheBffReceived()
    {
        await using var products = await CreateEmptyProductsServiceAsync("bff-subject-propagation");

        var recorder = new OutboundSubjectRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient().UseTestBearerToken();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);
        request.Headers.Add(CallerContextMiddleware.HeaderName, ResolvedSubject);

        await client.SendAsync(request);

        Assert.Equal(ResolvedSubject, Assert.Single(recorder.Observed));
    }

    /// <summary>
    /// The BFF relays, it never resolves. With no inbound subject — the gateway was bypassed — it
    /// must send no header rather than inventing one, so the failure propagates downstream instead
    /// of being masked by a default caller whose basket everyone would share.
    /// </summary>
    [Fact]
    public async Task TheBffsOutboundCall_CarriesNoSubject_WhenTheBffItselfHasNone()
    {
        await using var products = await CreateEmptyProductsServiceAsync("bff-subject-propagation-unresolved");

        var recorder = new OutboundSubjectRecorder();
        await using var bff = CreateRecordingBff(products, recorder);
        var client = bff.CreateClient().UseTestBearerToken();

        // A tenant but no subject: enough to reach persistence, not enough to name a caller. This
        // is the combination that would quietly pick a default if one existed.
        using var request = new HttpRequestMessage(HttpMethod.Get, "/bff/products");
        request.Headers.Add(TenantContextMiddleware.HeaderName, ResolvedTenant);

        await client.SendAsync(request);

        Assert.NotEmpty(recorder.Observed);
        Assert.All(recorder.Observed, Assert.Null);
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
    /// Innermost in the products client's pipeline, so it observes the request as it finally
    /// leaves — after the propagation handler has run, not before.
    /// </summary>
    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> CreateRecordingBff(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<ProductsApi::Program> products,
        OutboundSubjectRecorder recorder) =>
        BffTestHost.CreateBff("ProductsApi", products).WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
                services.AddHttpClient("ProductsApi")
                    .AddHttpMessageHandler(() => new RecordingHandler(recorder))));

    private sealed class OutboundSubjectRecorder
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

        public void Record(string? subjectId)
        {
            lock (_observed)
            {
                _observed.Add(subjectId);
            }
        }
    }

    private sealed class RecordingHandler(OutboundSubjectRecorder recorder) : DelegatingHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            recorder.Record(
                request.Headers.TryGetValues(CallerContextMiddleware.HeaderName, out var values)
                    ? values.Single()
                    : null);

            return base.SendAsync(request, cancellationToken);
        }
    }
}
