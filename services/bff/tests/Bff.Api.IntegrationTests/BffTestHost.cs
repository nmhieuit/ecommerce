using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        where TClient : class
    {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    [$"Services:{serviceConfigurationName}:BaseUrl"] = UnusedDownstreamBaseUrl,
                }));

            builder.ConfigureServices(services =>
                services.AddHttpClient(serviceConfigurationName)
                    .ConfigurePrimaryHttpMessageHandler(downstream.Server.CreateHandler));
        });
    }
}
