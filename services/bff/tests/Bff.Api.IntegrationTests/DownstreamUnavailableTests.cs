using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IntegrationTestSupport;

namespace Bff.Api.IntegrationTests;

/// <summary>
/// US3 / spec Test Scenario 3 / FR-006 / SC-003: when a downstream service is unavailable, the
/// caller gets a clear, well-formed error within a bounded time instead of hanging.
/// </summary>
/// <remarks>
/// No downstream host is started here. Every failure below travels through the real resilience
/// pipeline and the real exception handler; only the transport underneath is substituted.
/// <para>
/// The tests that assert a *specific* status inject the transport failure, because 502 versus 504
/// turns on whether the failure occurs inside the 1 s attempt timeout — and how fast a real machine
/// reports an unreachable host is that machine's business, not ours. That was not a hypothetical:
/// asserting against a genuinely unresolvable host passed in isolation and failed intermittently
/// when the suite ran alongside other Testcontainers suites, where DNS resolution slowed past the
/// attempt timeout and a 502 became a 504. The last test keeps a real unreachable address, and
/// asserts only what is true regardless of the host's speed.
/// </para>
/// </remarks>
public class DownstreamUnavailableTests
{
    /// <summary>
    /// SC-003: "callers receive a clear error response in under 5 seconds, in 100% of observed
    /// cases". Asserted as a hard bound, not a comment — an unbounded wait is the exact failure
    /// this story exists to prevent.
    /// </summary>
    private static readonly TimeSpan ClearErrorBudget = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A host that cannot resolve. <c>.invalid</c> is reserved by RFC 2606 precisely so it can
    /// never exist.
    /// </summary>
    private const string UnreachableAddress = "http://products-service.invalid";

    [Fact]
    public async Task GetProducts_ReturnsBadGateway_WhenTheProductsServiceIsUnreachable()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");
    }

    /// <summary>
    /// The spec's Edge Case: "a downstream service responds slowly rather than being fully down".
    /// A timeout is a distinct failure from an unreachable host and gets its own status, so an
    /// operator reading a dashboard can tell "the service is gone" from "the service is struggling".
    /// </summary>
    [Fact]
    public async Task GetProducts_ReturnsGatewayTimeout_WhenTheProductsServiceNeverAnswers()
    {
        await using var bff = BffTestHost.CreateBffWithUnresponsiveService("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");
    }

    /// <summary>
    /// data-model.md, Error response: RFC 7807 ProblemDetails carrying type, title, status, and a
    /// correlationId so the failure is traceable in the shared observability stack (Principle VII).
    /// </summary>
    [Fact]
    public async Task ADownstreamFailure_ReturnsProblemDetailsCarryingTheCorrelationId()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync("/bff/products");

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("type").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("title").GetString()));
        Assert.Equal((int)HttpStatusCode.BadGateway, problem.GetProperty("status").GetInt32());

        var correlationId = problem.GetProperty("correlationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(correlationId));

        // The same value the response header carries, or it is useless for tracing the request.
        Assert.Equal(
            response.Headers.GetValues("X-Correlation-Id").Single(),
            correlationId);
    }

    /// <summary>
    /// T054 — data-model.md's validation rule: an error "MUST NOT include the downstream service's
    /// internal URL/address — only its logical name — so the error is diagnosable without leaking
    /// topology to the client" (consistent with FR-001).
    /// </summary>
    [Fact]
    public async Task ADownstreamFailure_NamesTheLogicalServiceOnly_NeverItsAddress()
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport("ProductsApi");
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync("/bff/products");
        var body = await response.Content.ReadAsStringAsync();

        // Diagnosable: the caller can tell which dependency failed.
        Assert.Contains("ProductsApi", body, StringComparison.Ordinal);

        // But nothing about where it lives — no host, no scheme, and no stack trace.
        foreach (var leak in new[]
                 { "downstream.test", "http://", "Polly", "System.Net.Http", "at Bff.Api", "No such host" })
        {
            Assert.DoesNotContain(leak, body, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// Each route must fail as clearly as the product-listing route. A route whose failure path was
    /// never wired would return a bare 500 here.
    /// </summary>
    [Theory]
    [InlineData("BasketsApi", "/bff/baskets/8a1f6f6e-0000-4000-8000-000000000001")]
    [InlineData("OrdersApi", "/bff/orders/8a1f6f6e-0000-4000-8000-000000000002")]
    [InlineData("PartiesApi", "/bff/parties/8a1f6f6e-0000-4000-8000-000000000003")]
    public async Task EveryRoute_FailsAsAProblemDetails_WhenItsDownstreamIsUnreachable(
        string serviceConfigurationName,
        string route)
    {
        await using var bff = BffTestHost.CreateBffWithFailingTransport(serviceConfigurationName);
        var client = bff.CreateClient().UseTestBearerToken();

        var response = await client.GetAsync(route);

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// The real transport, against a genuinely unreachable host — no substituted handler anywhere.
    /// </summary>
    /// <remarks>
    /// Asserts only what holds however slowly the machine reports the failure: it is bounded, it is
    /// a structured problem document, and it names the dependency. Which of 502 or 504 it lands on
    /// depends on whether resolution beats the attempt timeout, so pinning that here is what made
    /// this suite flaky under load; both are correct answers to "the dependency did not answer".
    /// </remarks>
    [Fact]
    public async Task ADownstreamFailure_IsBoundedAndStructured_AgainstARealUnreachableHost()
    {
        await using var bff = BffTestHost.CreateBffWithUnreachableService("ProductsApi", UnreachableAddress);
        var client = bff.CreateClient().UseTestBearerToken();

        var stopwatch = Stopwatch.StartNew();
        var response = await client.GetAsync("/bff/products");
        stopwatch.Stop();

        Assert.Contains(
            response.StatusCode,
            new[] { HttpStatusCode.BadGateway, HttpStatusCode.GatewayTimeout });
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.True(
            stopwatch.Elapsed < ClearErrorBudget,
            $"Took {stopwatch.Elapsed.TotalSeconds:F1}s; SC-003 requires a clear error in under 5s.");

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("ProductsApi", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("correlationId").GetString()));
    }
}
