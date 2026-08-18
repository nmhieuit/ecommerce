using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Gateway.Api.IntegrationTests;

/// <summary>
/// The storefront calls the gateway from its own origin, so the browser will not send a request at
/// all unless the gateway admits that origin.
/// </summary>
/// <remarks>
/// This suite exists because the gap it covers was shipped and then found by the end-to-end
/// walkthrough (004-minimal-shopping-spa T065). Nothing before it could have caught the problem:
/// the component tests mock fetch, which has no notion of origins, and every manual check used
/// curl, which does not enforce CORS. The only honest way to keep it fixed is an assertion that
/// speaks in preflights.
/// </remarks>
public class StorefrontCorsTests
{
    private const string StorefrontOrigin = "http://localhost:5173";

    /// <summary>
    /// The preflight the browser sends before any credentialed cross-origin request. It must be
    /// answered by the gateway itself rather than forwarded to the BFF.
    /// </summary>
    [Fact]
    public async Task APreflightFromTheStorefront_IsAllowed()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", StorefrontOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.Equal(
            StorefrontOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// The client sends credentials, so the response must say so explicitly — and the CORS
    /// specification forbids answering a credentialed request with a wildcard origin, which is why
    /// the allowed origins are configured rather than set to <c>*</c>.
    /// </summary>
    [Fact]
    public async Task APreflightFromTheStorefront_AllowsCredentials()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/basket");
        request.Headers.Add("Origin", StorefrontOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");

        var response = await client.SendAsync(request);

        Assert.Equal(
            "true",
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Credentials")));
        Assert.NotEqual("*", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// An origin nobody configured gets no allow header, so the browser blocks the response. The
    /// policy is an allow-list, not an open door.
    /// </summary>
    [Fact]
    public async Task ARequestFromAnUnknownOrigin_IsNotAdmitted()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", "http://evil.example");
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// The allowed origins come from configuration. A deployment that serves the storefront from
    /// the gateway's own origin needs none, and one that does not must name its origin — neither
    /// case should require a rebuild.
    /// </summary>
    [Fact]
    public async Task TheAllowedOrigins_ComeFromConfiguration()
    {
        const string ConfiguredOrigin = "https://storefront.example";

        await using var gateway = CreateGateway(ConfiguredOrigin);
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", ConfiguredOrigin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.Equal(
            ConfiguredOrigin,
            Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// A same-origin request carries no <c>Origin</c> header and must be unaffected by any of this.
    /// </summary>
    [Fact]
    public async Task ARequestWithNoOrigin_IsUntouched()
    {
        await using var gateway = CreateGateway();
        var client = gateway.CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    /// <summary>
    /// The storefront is served two ways, and both are real: a Vite dev server on 5173 while
    /// developing, and a container on 4173 in the one-command stack
    /// (005-one-command-local-run research.md Decision 7). The allow-list has to admit both, so the
    /// policy must handle more than a single origin.
    /// </summary>
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4173")]
    public async Task EachConfiguredOrigin_IsAdmitted(string origin)
    {
        await using var gateway = CreateGateway("http://localhost:5173", "http://localhost:4173");
        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    /// <summary>
    /// The shipped Development configuration, not an in-memory substitute.
    /// </summary>
    /// <remarks>
    /// Every other test here supplies its own origins, which proves the policy works but says
    /// nothing about whether the repository is actually configured for the storefronts it ships.
    /// That gap is how 004 shipped a gateway with no CORS at all — the tests were right and the
    /// configuration was missing. This reads the real file.
    /// </remarks>
    [Theory]
    [InlineData("http://localhost:5173")]
    [InlineData("http://localhost:4173")]
    public async Task TheDevelopmentConfiguration_Admits_BothStorefrontOrigins(string origin)
    {
        await using var gateway = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

        var client = gateway.CreateClient();

        using var request = new HttpRequestMessage(HttpMethod.Options, "/bff/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", "GET");

        var response = await client.SendAsync(request);

        Assert.True(
            response.Headers.Contains("Access-Control-Allow-Origin"),
            $"'{origin}' is not in the gateway's configured allow-list, so a storefront served there "
            + "would be blocked by the browser before any request left it.");
    }

    private static WebApplicationFactory<Program> CreateGateway(params string[] origins)
    {
        var configured = origins.Length == 0 ? [StorefrontOrigin] : origins;

        return new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                configured
                    .Select((origin, index) =>
                        new KeyValuePair<string, string?>($"Cors:AllowedOrigins:{index}", origin))
                    .ToDictionary())));
    }
}
