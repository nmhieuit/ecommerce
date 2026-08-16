using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Tenancy;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// Builds the two-host arrangement every BFF route test needs: a real downstream service running
/// in-process against a real database, and a BFF whose typed client for that service is pointed at
/// it (research.md Decision 5).
/// </summary>
/// <remarks>
/// The BFF's typed clients issue ordinary outbound HTTP. Pointing one at an in-process
/// <see cref="WebApplicationFactory{T}"/> means replacing its primary handler with that host's
/// <c>Server.CreateHandler()</c>, which is why <see cref="CreateBff{TClient}"/> exists rather than
/// tests simply setting a base URL. The base URL is still configured — the request needs an
/// absolute URI — but the handler, not the address, decides where it actually lands.
/// </remarks>
public static class BffTestHost
{
    /// <summary>
    /// A placeholder authority. The test handler routes the call regardless of host, but a typed
    /// client with no BaseAddress cannot build an absolute request URI.
    /// </summary>
    private const string UnusedDownstreamBaseUrl = "http://downstream.test";

    /// <summary>
    /// The tenant downstream services are seeded as. Route tests reach these rows through the BFF,
    /// which relays whatever tenant its own request carried — so a test that wants to read what it
    /// seeded sends this same value inbound.
    /// </summary>
    public const string SeedTenantId = "contoso";

    /// <summary>
    /// Starts a downstream service in-process against <paramref name="connectionString"/>, applies
    /// its migrations, and hands the seeded context to <paramref name="seedAsync"/>.
    /// </summary>
    /// <typeparam name="TEntryPoint">The downstream service's <c>Program</c> (extern-aliased).</typeparam>
    /// <typeparam name="TDbContext">That service's own <c>DbContext</c>.</typeparam>
    public static async Task<WebApplicationFactory<TEntryPoint>> CreateDownstreamAsync<TEntryPoint, TDbContext>(
        string connectionStringKey,
        string connectionString,
        Func<TDbContext, Task> seedAsync)
        where TEntryPoint : class
        where TDbContext : DbContext
    {
        var factory = new WebApplicationFactory<TEntryPoint>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"ConnectionStrings:{connectionStringKey}"] = connectionString,
                })));

        using var scope = factory.Services.CreateScope();

        // No HTTP request runs for this scope, so the downstream service's TenantContextMiddleware
        // never populates it — seeding must prime the tenant itself or that service's gated
        // AddDbContext registration throws before any connection is built (research.md Decision 7).
        scope.ServiceProvider.GetRequiredService<TenantContext>().TenantId = SeedTenantId;

        var dbContext = scope.ServiceProvider.GetRequiredService<TDbContext>();
        await dbContext.Database.MigrateAsync();
        await seedAsync(dbContext);

        return factory;
    }

    /// <summary>
    /// Starts the BFF with <typeparamref name="TClient"/>'s typed <c>HttpClient</c> routed into
    /// <paramref name="downstream"/>'s in-process test server.
    /// </summary>
    public static WebApplicationFactory<Program> CreateBff<TClient>(
        string serviceConfigurationName,
        WebApplicationFactory<TClient> downstream)
        where TClient : class =>
        CreateBff(serviceConfigurationName, UnusedDownstreamBaseUrl, downstream.Server.CreateHandler);

    /// <summary>
    /// A client whose requests carry the tenant the gateway would have resolved, matching what
    /// <see cref="CreateDownstreamAsync{TEntryPoint, TDbContext}"/> seeded.
    /// </summary>
    /// <remarks>
    /// Route tests need this because the BFF relays rather than resolves: an inbound request with
    /// no tenant produces an outbound call with no tenant, and the downstream service's gate then
    /// refuses it. That is the correct end-to-end behaviour (US2) — it just means a test asserting
    /// on proxied data has to arrive as some tenant, exactly as a real caller does.
    /// </remarks>
    public static HttpClient CreateTenantClient(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(TenantContextMiddleware.HeaderName, SeedTenantId);

        return client;
    }

    /// <summary>
    /// Starts the BFF with a downstream that is configured but has nothing listening — the
    /// "service is down" case (US3). No handler is replaced: the real socket connect fails, which
    /// is what production failure actually looks like.
    /// </summary>
    public static WebApplicationFactory<Program> CreateBffWithUnreachableService(
        string serviceConfigurationName,
        string unreachableBaseUrl) =>
        CreateBff(serviceConfigurationName, unreachableBaseUrl, handlerFactory: null);

    /// <summary>
    /// Starts the BFF with a downstream whose transport fails immediately — a refused connection or
    /// an unresolvable host, without depending on the machine actually producing one.
    /// </summary>
    /// <remarks>
    /// Whether a *real* unreachable address fails fast is an environment behaviour, and it decides
    /// which status this maps to: a transport error under the attempt timeout is unavailability
    /// (502), one that exceeds it is a timeout (504). Measured here, a refused connection on Windows
    /// loopback took ~2 s and DNS failure ~13 ms — but under Docker contention the DNS path was
    /// observed exceeding the 1 s attempt timeout too, flipping 502 to 504 and failing the suite
    /// intermittently. Injecting the failure removes the machine from the equation, so the mapping
    /// under test is asserted rather than the host's socket timing. The real-transport path is still
    /// exercised by <c>ADownstreamFailure_IsBoundedAndStructured_AgainstARealUnreachableHost</c>
    /// and by quickstart Scenario 4.
    /// </remarks>
    public static WebApplicationFactory<Program> CreateBffWithFailingTransport(
        string serviceConfigurationName) =>
        CreateBff(serviceConfigurationName, UnusedDownstreamBaseUrl, () => new FailingTransportHandler());

    /// <summary>
    /// Starts the BFF with a downstream that accepts the request and then never answers — the
    /// "responds slowly rather than being fully down" case the spec's Edge Cases call out, which
    /// must produce a timeout rather than an unbounded wait.
    /// </summary>
    public static WebApplicationFactory<Program> CreateBffWithUnresponsiveService(
        string serviceConfigurationName) =>
        CreateBff(serviceConfigurationName, UnusedDownstreamBaseUrl, () => new NeverRespondingHandler());

    private static WebApplicationFactory<Program> CreateBff(
        string serviceConfigurationName,
        string baseUrl,
        Func<HttpMessageHandler>? handlerFactory)
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"Services:{serviceConfigurationName}:BaseUrl"] = baseUrl,
                }));

            if (handlerFactory is not null)
            {
                builder.ConfigureServices(services =>
                    services.AddHttpClient(serviceConfigurationName)
                        .ConfigurePrimaryHttpMessageHandler(handlerFactory));
            }
        });
    }

    /// <summary>
    /// Fails the transport immediately, exactly as an unresolvable host or refused connection does,
    /// but in no measurable time — so the failure cannot be reclassified as a timeout on a loaded
    /// machine.
    /// </summary>
    private sealed class FailingTransportHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("No such host is known.");
    }

    /// <summary>
    /// Accepts the request and waits until cancelled, so the resilience pipeline's timeout is what
    /// ends the call rather than the downstream answering.
    /// </summary>
    private sealed class NeverRespondingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            throw new InvalidOperationException("Unreachable: the delay above only ends by cancellation.");
        }
    }
}
