using System.Net;
using IntegrationTestSupport;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Baskets.Api.ContractTests;

/// <summary>
/// This service, hosted in-process on a real port, for Pact's verifier to replay the BFF's
/// recorded requests against (011-consumer-contract-tests research.md Decision 5).
/// </summary>
/// <remarks>
/// <para>
/// A plain <see cref="WebApplicationFactory{TEntryPoint}"/> serves over an in-memory transport with
/// no socket behind it. The verifier is a native library that speaks HTTP over the network, so it
/// needs a listener it can actually connect to — hence Kestrel on port 0 alongside the test server
/// the factory's own internals still expect.
/// </para>
/// <para>
/// A real SQL Server backs it, from the same Testcontainers fixture the integration suite uses.
/// Research Decision 5 rules out standing the whole service up in a container; it does not rule out
/// the database, and there is no way to leave it out — this service's <c>DbContext</c> is
/// registered against SQL Server and gated on a resolved tenant, so an in-memory substitute would
/// be verifying something other than the real code path (constitution Principle III).
/// </para>
/// </remarks>
internal sealed class PactProviderHost(
    string connectionString,
    string tenantId,
    Func<ProviderState, IServiceProvider, Task> applyStateAsync) : WebApplicationFactory<Program>
{
    private IHost? _kestrelHost;

    /// <summary>Where the verifier should send the replayed requests.</summary>
    public Uri BaseUri { get; private set; } = new("http://localhost");

    /// <summary>Where the verifier should announce each interaction's provider state.</summary>
    public Uri ProviderStateUri => new(BaseUri, ProviderStateStartupFilter.Path);

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:BasketsDb"] = connectionString,
            }));

        // 015-deny-by-default-authz gated every route on AuthorizationPolicies.ApiScope
        // (RequireAuthenticatedUser + the scope requirement), attached directly to each endpoint —
        // not through AuthorizationOptions.FallbackPolicy, which this host used to null out instead
        // (011-consumer-contract-tests QA_Debt entry, resolved). An endpoint-level policy ignores
        // FallbackPolicy, so nulling it stopped being enough the moment 015 shipped; the verifier's
        // replayed requests need a token this host actually accepts, which is what this configures —
        // the same symmetric-key bypass every *.Api.IntegrationTests project already uses instead of
        // a real identity server. *ProviderPactTests attaches the token itself via
        // PactVerifierSource.WithCustomHeader.
        builder.UseTestJwtBearer();

        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter>(new ProviderStateStartupFilter(tenantId, applyStateAsync)));
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Built before the switch to Kestrel below, because the factory's internals still require
        // the returned host to be the TestServer-backed one.
        var testHost = builder.Build();

        builder.ConfigureWebHost(webHost => webHost.UseKestrel(options => options.Listen(IPAddress.Loopback, port: 0)));

        _kestrelHost = builder.Build();
        _kestrelHost.Start();

        var addresses = _kestrelHost.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()
            ?? throw new InvalidOperationException("Kestrel started without reporting an address to verify against.");

        BaseUri = new Uri(addresses.Addresses.Last());

        testHost.Start();

        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _kestrelHost?.Dispose();
            _kestrelHost = null;
        }

        base.Dispose(disposing);
    }
}
