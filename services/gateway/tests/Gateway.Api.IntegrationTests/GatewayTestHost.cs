extern alias BffApi;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Yarp.ReverseProxy.Forwarder;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// Builds a gateway whose YARP proxy forwards into a real BFF running in-process.
/// </summary>
/// <remarks>
/// YARP forwards over a real socket to its configured destination address. To reach an in-process
/// host instead, the thing to replace is its <see cref="IForwarderHttpClientFactory"/> — the
/// gateway's route table, cluster selection, header handling, and timeout all still run exactly as
/// configured; only the transport underneath is swapped. That is the same substitution the BFF's
/// own tests make with <c>ConfigurePrimaryHttpMessageHandler</c>, one layer down.
/// <para>
/// Deliberately *not* done by pointing the cluster at a real Kestrel port: binding ports makes the
/// suite flaky under parallel CI runs, and the socket is the one part of forwarding YARP is not
/// responsible for getting right.
/// </para>
/// </remarks>
public static class GatewayTestHost
{
    /// <summary>
    /// Starts the real BFF in-process, to act as the gateway's forwarding destination.
    /// </summary>
    /// <remarks>
    /// Pinned to the Development environment because the BFF publishes its OpenAPI document only
    /// there (T013). That document is the one 200-returning response only the BFF can produce
    /// without any domain service running, which makes it the cleanest proof that a request
    /// actually arrived at the BFF rather than being answered by the gateway.
    /// </remarks>
    public static WebApplicationFactory<BffApi::Program> CreateBff() =>
        new WebApplicationFactory<BffApi::Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment(Environments.Development));

    /// <summary>
    /// Starts the gateway with its configured routes, forwarding into <paramref name="bff"/>.
    /// </summary>
    public static WebApplicationFactory<Program> CreateGateway(WebApplicationFactory<BffApi::Program> bff) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IForwarderHttpClientFactory>();
                services.AddSingleton<IForwarderHttpClientFactory>(
                    new InProcessForwarderHttpClientFactory(bff.Server.CreateHandler()));
            }));

    /// <summary>
    /// Hands YARP an invoker bound to the in-process BFF's test server rather than a socket.
    /// </summary>
    private sealed class InProcessForwarderHttpClientFactory(HttpMessageHandler handler)
        : IForwarderHttpClientFactory
    {
        public HttpMessageInvoker CreateClient(ForwarderHttpClientContext context) =>
            // disposeHandler: false — the handler belongs to the BFF factory, which outlives the
            // invoker and disposes it itself.
            new(handler, disposeHandler: false);
    }
}
