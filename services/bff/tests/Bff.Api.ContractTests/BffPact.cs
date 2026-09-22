using Bff.Api.DownstreamClients;
using IntegrationTestSupport;
using PactNet;

namespace Bff.Api.ContractTests;

/// <summary>
/// The one place a consumer pact for the BFF is configured, so every boundary this project
/// documents lands in the same directory under the same participant name.
/// </summary>
/// <remarks>
/// V3 rather than V4: V3 is what the message-pact half of this feature's event pilot needs
/// (011-consumer-contract-tests research.md Decision 1), and running both halves at one
/// specification version keeps the committed documents readable side by side.
/// </remarks>
internal static class BffPact
{
    /// <summary>The consumer participant name, shared by all three HTTP boundaries.</summary>
    public const string Consumer = "bff";

    /// <summary>
    /// The tenant every interaction here is stated for. Any non-blank value would do — what the
    /// pact records is that the BFF relays a tenant at all, which is what a downstream service's
    /// gate refuses without (<c>TenantPropagationHandler</c>).
    /// </summary>
    public const string TenantId = "contoso";

    /// <summary>
    /// The caller every "current"-scoped interaction is stated for. The baskets service resolves
    /// the basket from this header rather than from a path value, so it is part of the request
    /// shape, not incidental transport detail.
    /// </summary>
    public const string SubjectId = "pact-shopper";

    /// <summary>
    /// The bearer token every interaction here is recorded with. 015-deny-by-default-authz gated
    /// every downstream boundary on <c>AuthorizationPolicies.ApiScope</c> (an authenticated caller
    /// carrying the <c>ecommerce-api</c> scope), and <see cref="TenantPropagationHandler"/> has
    /// relayed the caller's real <c>Authorization</c> header to every downstream call since
    /// 014-identity-server-auth — so a pact recorded without one no longer describes what the BFF
    /// actually sends. A fixed, very-long-lived token (not <see cref="TestJwtBearer.CreateToken"/>'s
    /// 5-minute default) so the literal value committed into <c>pacts/bff-*.json</c> stays valid
    /// between regenerations; the provider side never reads this value anyway (it supplies its own,
    /// see <c>PactProviderHost</c>/<c>*ProviderPactTests</c> — <see cref="AuthorizationHeader"/>'s
    /// regex matcher is what the pact actually asserts).
    /// </summary>
    private static readonly string BearerToken =
        "Bearer " + TestJwtBearer.CreateToken(subject: "pact-shopper", expires: DateTime.UtcNow.AddYears(10));

    /// <summary>
    /// What the pact records for the <c>Authorization</c> header: a regex ("looks like a bearer
    /// token"), not a fixed value — the provider side mints its own fresh token at verification
    /// time (<c>PactVerifierSource.WithCustomHeader</c>), so pinning the literal here would make
    /// every provider build depend on the exact string this consumer test last produced.
    /// </summary>
    public static PactNet.Matchers.IMatcher AuthorizationHeader =>
        PactNet.Matchers.Match.Regex(BearerToken, "^Bearer .+$");

    public static IPactBuilderV3 For(string provider) =>
        Pact.V3(Consumer, provider, new PactConfig { PactDir = PactPaths.Directory })
            .WithHttpInteractions();

    /// <summary>
    /// A client carrying the headers the BFF's <c>TenantPropagationHandler</c> stamps on every
    /// outbound call. Built by hand here because the handler needs an inbound
    /// <c>HttpContext</c> to relay from, and there is no request in flight in a consumer pact test.
    /// </summary>
    public static HttpClient CreateRelayingClient(Uri mockServerUri, string subjectId = SubjectId)
    {
        var client = new HttpClient { BaseAddress = mockServerUri };
        client.DefaultRequestHeaders.Add("X-Tenant-Id", TenantId);
        client.DefaultRequestHeaders.Add("X-Subject-Id", subjectId);
        // Mirrors TenantPropagationHandler relaying the caller's real Authorization header
        // (015-deny-by-default-authz) — see BearerToken's remarks for why the literal value here
        // does not need to match what the provider side sends.
        client.DefaultRequestHeaders.Add("Authorization", BearerToken);

        return client;
    }
}
