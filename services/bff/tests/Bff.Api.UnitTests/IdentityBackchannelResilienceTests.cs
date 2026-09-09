using Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Bff.Api.UnitTests;

/// <summary>
/// 020-timeouts-retry-circuit-breaker spec FR-001 / research.md Decision 4: the JwtBearer
/// backchannel (OIDC discovery + JWKS fetch against the identity server) is an outbound call like
/// any other, and must declare an explicit resilience policy instead of relying on the framework's
/// implicit default <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
/// <remarks>
/// <para>
/// <c>AddIdentityValidation</c> (<c>shared/Identity/IdentityValidationExtensions.cs</c>) is the one
/// registration every non-gateway service in the platform shares (parties, products, orders,
/// baskets, and the BFF here) — fixing it once here covers all five (research.md Decision 4). The
/// gateway's own scheme is covered separately by
/// <c>Gateway.Api.UnitTests/IdentityBackchannelResilienceTests.cs</c>, since it cannot call this
/// helper directly (see <c>ToggleGatedAuthenticationExtensions</c>'s remarks).
/// </para>
/// <para>
/// Asserted at the <c>IHttpClientFactory</c> registration level rather than by inspecting
/// <c>JwtBearerOptions.Backchannel</c> directly: that property is non-null even before this feature
/// (the framework's own default is a plain <c>HttpClient</c> assigned at options-construction time),
/// so a null-check cannot tell "explicit resilience pipeline" apart from "framework default".
/// Whether <c>AddHttpClient("IdentityBackchannel").AddStandardResilienceHandler(...)</c> actually ran
/// shows up as extra <see cref="HttpClientFactoryOptions.HttpMessageHandlerBuilderActions"/> registered
/// for that name — a name nobody configured has none.
/// </para>
/// </remarks>
public class IdentityBackchannelResilienceTests
{
    private const string BackchannelClientName = "IdentityBackchannel";

    [Fact]
    public void AddIdentityValidation_RegistersAResiliencePipelineForTheBackchannelClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIdentityValidation(BuildConfiguration());

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
