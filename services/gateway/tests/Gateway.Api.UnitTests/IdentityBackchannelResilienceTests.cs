using Gateway.Api.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Gateway.Api.UnitTests;

/// <summary>
/// 020-timeouts-retry-circuit-breaker spec FR-001 / research.md Decision 4: the JwtBearer
/// backchannel (OIDC discovery + JWKS fetch against the identity server) is an outbound call like
/// any other, and must declare an explicit resilience policy instead of relying on the framework's
/// implicit default <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
/// <remarks>
/// The gateway registers its JwtBearer scheme through <c>AddToggleGatedIdentity</c> rather than the
/// shared <c>AddIdentityValidation</c> every other service uses (see that class's remarks), so it
/// needs its own copy of this fix and its own copy of this test — mirrors
/// <c>Bff.Api.UnitTests/IdentityBackchannelResilienceTests.cs</c>, including why the assertion is
/// made at the <c>IHttpClientFactory</c> registration level rather than by inspecting
/// <c>JwtBearerOptions.Backchannel</c> directly (that property is non-null by framework default).
/// </remarks>
public class IdentityBackchannelResilienceTests
{
    private const string BackchannelClientName = "IdentityBackchannel";

    [Fact]
    public void AddToggleGatedIdentity_RegistersAResiliencePipelineForTheBackchannelClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddToggleGatedIdentity(BuildConfiguration());

        var factoryOptions = services.BuildServiceProvider()
            .GetRequiredService<IOptionsMonitor<HttpClientFactoryOptions>>();

        var configured = factoryOptions.Get(BackchannelClientName);
        var neverRegistered = factoryOptions.Get("a-name-nobody-configured");

        Assert.True(
            configured.HttpMessageHandlerBuilderActions.Count > neverRegistered.HttpMessageHandlerBuilderActions.Count,
            $"Expected '{BackchannelClientName}' to have a resilience handler pipeline attached via "
            + "AddStandardResilienceHandler(), but it has no more handler-builder actions than a "
            + "client name nobody registered.");
    }

    private static IConfiguration BuildConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Identity:Authority"] = "http://identity-api:8080",
                ["Identity:Audience"] = "ecommerce-api",
            })
            .Build();
}
