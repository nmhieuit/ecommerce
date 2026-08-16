using System.Net;
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

    private static WebApplicationFactory<Program> CreateGateway(string origin = StorefrontOrigin) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Cors:AllowedOrigins:0"] = origin,
                })));
}
