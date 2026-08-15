using Bff.Api.DownstreamClients;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Bff.Api.UnitTests;

/// <summary>
/// data-model.md, Downstream Service Client validation rule: "A downstream client with no
/// configured BaseUrl is a startup configuration error, not a runtime null-reference — fail fast
/// rather than fail per-request."
/// </summary>
/// <remarks>
/// The distinction matters operationally. A misconfigured deployment that fails at startup never
/// takes traffic; one that fails per request looks healthy, passes its readiness probe, and serves
/// errors to real users until someone reads the logs.
/// </remarks>
public class DownstreamServiceClientOptionsTests
{
    [Fact]
    public void Validation_Fails_WhenBaseUrlIsMissing()
    {
        var options = BuildOptions(new Dictionary<string, string?>());

        var exception = Assert.Throws<OptionsValidationException>(() => _ = options.Value);

        // Names the offending configuration key, so the failure tells an operator what to fix
        // rather than only that something is wrong.
        Assert.Contains("Services:ProductsApi:BaseUrl", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validation_Fails_WhenBaseUrlIsNotAnAbsoluteUri()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["Services:ProductsApi:BaseUrl"] = "/products",
        });

        Assert.Throws<OptionsValidationException>(() => _ = options.Value);
    }

    [Fact]
    public void Validation_Succeeds_WhenBaseUrlIsAnAbsoluteUri()
    {
        var options = BuildOptions(new Dictionary<string, string?>
        {
            ["Services:ProductsApi:BaseUrl"] = "http://products-api:8080",
        });

        Assert.Equal(new Uri("http://products-api:8080"), options.Value.BaseUrl);
        Assert.Equal("ProductsApi", options.Value.ServiceName);
    }

    private static NamedOptions BuildOptions(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();

        var services = new ServiceCollection();
        services.AddDownstreamServiceOptions("ProductsApi", configuration);

        return new NamedOptions(services.BuildServiceProvider(), "ProductsApi");
    }

    /// <summary>
    /// Adapts the named options the BFF registers to the plain <see cref="IOptions{T}"/> shape
    /// these tests assert against, so a validation failure surfaces on first access.
    /// </summary>
    private sealed class NamedOptions(IServiceProvider services, string name)
        : IOptions<DownstreamServiceClientOptions>
    {
        public DownstreamServiceClientOptions Value =>
            services.GetRequiredService<IOptionsMonitor<DownstreamServiceClientOptions>>().Get(name);
    }
}
