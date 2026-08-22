using System.Net;
using Bff.Api.DownstreamClients;
using PactNet.Matchers;

namespace Bff.Api.ContractTests;

/// <summary>
/// What the BFF relies on from the products service, stated as a Pact document
/// (<c>pacts/bff-products.json</c>) for the products service's own build to verify
/// (011-consumer-contract-tests FR-001, FR-002).
/// </summary>
/// <remarks>
/// The expectations are driven through <see cref="ProductsApiClient"/> itself rather than through a
/// hand-written request. That is the point of a consumer-driven pact: what gets recorded is what
/// the real client sends and what it can actually deserialise, so a field the BFF never reads
/// cannot leak into the contract and pin the producer down for no reason (FR-007).
/// </remarks>
public class ProductsConsumerPactTests
{
    private const string Provider = "products";

    [Fact]
    public async Task GetProducts_DependsOnIdNameAndPrice()
    {
        var pact = BffPact.For(Provider);

        pact
            .UponReceiving("a request for the catalog")
                .Given("the catalog contains at least one product")
                .WithRequest(HttpMethod.Get, "/products")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                // MinType(_, 1) rather than a fixed array: the BFF reads however many products
                // there are, so pinning the count would make an unrelated catalog change a
                // contract break.
                .WithJsonBody(Match.MinType(
                    new
                    {
                        id = Match.Type("9f8d6b1e-0001-4000-8000-000000000001"),
                        name = Match.Type("Field Notes Notebook"),
                        price = Match.Number(12.50m),
                    },
                    1));

        await pact.VerifyAsync(async context =>
        {
            using var httpClient = BffPact.CreateRelayingClient(context.MockServerUri);
            var client = new ProductsApiClient(httpClient);

            var products = await client.GetProductsAsync(CancellationToken.None);

            // Asserting on the deserialised resource, not on raw JSON: it is what proves the shape
            // recorded above is one ProductResource can actually be built from.
            var product = Assert.Single(products);
            Assert.NotEqual(Guid.Empty, product.Id);
            Assert.False(string.IsNullOrWhiteSpace(product.Name));
            Assert.Equal(12.50m, product.Price);
        });
    }
}
