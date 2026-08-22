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

        return client;
    }
}
