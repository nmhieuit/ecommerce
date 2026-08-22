using System.Net;
using Bff.Api.DownstreamClients;
using PactNet.Matchers;

namespace Bff.Api.ContractTests;

/// <summary>
/// What the BFF relies on from the baskets service, stated as a Pact document
/// (<c>pacts/bff-baskets.json</c>) for the baskets service's own build to verify
/// (011-consumer-contract-tests FR-001, FR-002).
/// </summary>
/// <remarks>
/// <para>
/// All four interactions are recorded in one test against one builder. The pact writer merges into
/// an existing file rather than replacing it, so splitting a boundary across test methods would let
/// an interaction that no longer exists survive in the committed document — precisely the stale
/// expectation this feature is meant to make impossible.
/// </para>
/// <para>
/// The two clear-basket interactions are told apart by their caller, not by their provider state:
/// a Pact mock server matches on the request alone, and both clears are the same method and path.
/// Using a second subject is not a trick to get around that — the baskets service really does
/// resolve which basket to clear from <c>X-Subject-Id</c>, so the caller is part of the request.
/// </para>
/// </remarks>
public class BasketsConsumerPactTests
{
    private const string Provider = "baskets";

    /// <summary>A caller the baskets service has a basket with contents for.</summary>
    private const string ShopperWithItems = BffPact.SubjectId;

    /// <summary>A caller whose basket is empty — the state behind the 409 the BFF depends on.</summary>
    private const string ShopperWithEmptyBasket = "pact-shopper-empty";

    private static readonly Guid ProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    [Fact]
    public async Task BasketInteractions_DependOnIdCustomerRefItemsAndTotal()
    {
        var pact = BffPact.For(Provider);

        pact
            .UponReceiving("a request for the caller's current basket")
                .Given("a basket holding one item exists for the caller", CallerState(ShopperWithItems))
                .WithRequest(HttpMethod.Get, "/baskets/current")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", ShopperWithItems)
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(BasketBody());

        pact
            .UponReceiving("a request to add an item to the caller's current basket")
                .Given("an empty basket exists for the caller", CallerState(ShopperWithEmptyBasket))
                .WithRequest(HttpMethod.Post, "/baskets/current/items")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", ShopperWithEmptyBasket)
                .WithJsonBody(new
                {
                    productId = ProductId,
                    quantity = 2,
                    unitPrice = 12.50m,
                })
            .WillRespond()
                .WithStatus(HttpStatusCode.OK)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(BasketBody());

        pact
            .UponReceiving("a request to clear the caller's current basket")
                .Given("a basket holding one item exists for the caller", CallerState(ShopperWithItems))
                .WithRequest(HttpMethod.Post, "/baskets/current/clear")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", ShopperWithItems)
            .WillRespond()
                // No body is expected: the BFF reads the status alone, and asking for one here
                // would pin the producer to something no consumer looks at (FR-007).
                .WithStatus(HttpStatusCode.NoContent);

        pact
            .UponReceiving("a request to clear a basket that is already empty")
                .Given("an empty basket exists for the caller", CallerState(ShopperWithEmptyBasket))
                .WithRequest(HttpMethod.Post, "/baskets/current/clear")
                .WithHeader("X-Tenant-Id", BffPact.TenantId)
                .WithHeader("X-Subject-Id", ShopperWithEmptyBasket)
            .WillRespond()
                // 409, not a 4xx of any kind: the BFF turns this exact status into "there was
                // nothing to check out" rather than a failure, so the status itself is the contract.
                .WithStatus(HttpStatusCode.Conflict)
                .WithHeader("Content-Type", "application/json; charset=utf-8")
                .WithJsonBody(new { error = Match.Type("The basket is already empty.") });

        await pact.VerifyAsync(async context =>
        {
            using var withItems = BffPact.CreateRelayingClient(context.MockServerUri, ShopperWithItems);
            using var withEmpty = BffPact.CreateRelayingClient(context.MockServerUri, ShopperWithEmptyBasket);

            var readClient = new BasketsApiClient(withItems);
            var emptyBasketClient = new BasketsApiClient(withEmpty);

            var current = await readClient.GetCurrentBasketAsync(CancellationToken.None);
            AssertReadable(current);

            var afterAdd = await emptyBasketClient.AddItemAsync(
                new AddBasketItemCommand(ProductId, Quantity: 2, UnitPrice: 12.50m),
                CancellationToken.None);
            AssertReadable(afterAdd);

            Assert.True(await readClient.ClearCurrentBasketAsync(CancellationToken.None));

            // False rather than an exception is the whole reason the 409 is in this pact.
            Assert.False(await emptyBasketClient.ClearCurrentBasketAsync(CancellationToken.None));
        });
    }

    private static Dictionary<string, string> CallerState(string customerRef) =>
        new Dictionary<string, string> { ["customerRef"] = customerRef };

    /// <summary>
    /// The basket shape both basket-returning routes share, expressed once. Matchers throughout:
    /// what the BFF depends on is that these fields are present and are numbers or strings, not
    /// that any particular basket is in the producer's database.
    /// </summary>
    private static object BasketBody() => new
    {
        id = Match.Regex("2b7c1f5a-0002-4000-8000-000000000001", PactRegex.Uuid),
        customerRef = Match.Type(ShopperWithItems),
        items = Match.MinType(
            new
            {
                productId = Match.Regex(ProductId.ToString(), PactRegex.Uuid),
                quantity = Match.Integer(2),
                unitPrice = Match.Number(12.50m),
                // Carried, never recomputed: the BFF passes money through, so a producer that
                // stopped sending it would silently make the storefront do arithmetic it must not
                // (004 plan.md).
                lineTotal = Match.Number(25.00m),
            },
            1),
        total = Match.Number(25.00m),
    };

    private static void AssertReadable(BasketResource basket)
    {
        Assert.NotEqual(Guid.Empty, basket.Id);
        Assert.False(string.IsNullOrWhiteSpace(basket.CustomerRef));

        var line = Assert.Single(basket.Items);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, line.LineTotal);
        Assert.Equal(25.00m, basket.Total);
    }
}
