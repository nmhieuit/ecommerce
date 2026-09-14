using System.Globalization;
using System.Net.Http.Json;
using NBomber.Contracts;
using NBomber.CSharp;

namespace CriticalPathLoadTests;

/// <summary>
/// The browse→basket→checkout→order critical path (spec FR-001), as one NBomber scenario with 4
/// named steps — in the same order and through the same gateway surface as a real shopper
/// (<c>frontend/apps/web/e2e/walkthrough.spec.ts</c>).
/// </summary>
/// <remarks>
/// The demo/local stack's gateway authenticates every caller as the SAME fixed stub subject
/// (<see cref="GatewayClient"/> remarks) — every simulated virtual user shares one basket. Concurrent
/// virtual users can therefore race on that one basket (one clears it via checkout while another is
/// mid-add), which is a real limitation of this environment's current stub-identity phase, not a
/// defect in this scenario (spec.md Edge Cases: "Tải giả lập... không phản ánh đúng tỷ lệ traffic
/// thật"). <see cref="CriticalPathLoadTest"/> keeps the injected load modest for exactly this reason.
/// </remarks>
public static class CriticalPathScenario
{
    /// <summary>"Field Notes Notebook" — a stable, literal seed id (Products.Api CatalogSeed.cs), the
    /// same one the Playwright walkthrough adds to the basket.</summary>
    private static readonly Guid SeedProductId = new("9f8d6b1e-0001-4000-8000-000000000001");

    public static ScenarioProps Create(HttpClient httpClient)
    {
        return Scenario.Create("critical_path", async context =>
            {
                // Not a tracked step: clears whatever the previous iteration (or another virtual
                // user) left in the shared basket, so every iteration starts from a known state
                // without manual cleanup between runs (FR-006, US3) — same technique as
                // walkthrough.spec.ts's resetBasket().
                await httpClient.PostAsync("/bff/checkout", content: null);

                var products = await Step.Run(CriticalPathStepBudgets.StepNames[0], context, async () =>
                {
                    var response = await httpClient.GetAsync("/bff/products");
                    return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail(statusCode: StatusCodeString(response));
                });

                var addItem = await Step.Run(CriticalPathStepBudgets.StepNames[1], context, async () =>
                {
                    var response = await httpClient.PostAsJsonAsync(
                        "/bff/basket/items",
                        new AddBasketItemRequest(SeedProductId, Quantity: 1));

                    return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail(statusCode: StatusCodeString(response));
                });

                Guid? orderId = null;

                var checkout = await Step.Run(CriticalPathStepBudgets.StepNames[2], context, async () =>
                {
                    var response = await httpClient.PostAsync("/bff/checkout", content: null);

                    if (!response.IsSuccessStatusCode)
                    {
                        return Response.Fail(statusCode: StatusCodeString(response));
                    }

                    var confirmation = await response.Content.ReadFromJsonAsync<OrderConfirmationResponse>();
                    orderId = confirmation?.Id;

                    return orderId is null ? Response.Fail(statusCode: "no-order-id") : Response.Ok();
                });

                var order = await Step.Run(CriticalPathStepBudgets.StepNames[3], context, async () =>
                {
                    if (orderId is null)
                    {
                        // Checkout failed above; there is nothing to look up. Failing this step too
                        // keeps its own stats honest (an unmeasured step would understate the
                        // failure) rather than skipping it silently.
                        return Response.Fail(statusCode: "no-order-id");
                    }

                    var response = await httpClient.GetAsync($"/bff/orders/{orderId}");
                    return response.IsSuccessStatusCode ? Response.Ok() : Response.Fail(statusCode: StatusCodeString(response));
                });

                return products.IsError || addItem.IsError || checkout.IsError || order.IsError
                    ? Response.Fail()
                    : Response.Ok();
            })
            .WithoutWarmUp();
    }

    private static string StatusCodeString(HttpResponseMessage response) =>
        ((int)response.StatusCode).ToString(CultureInfo.InvariantCulture);

    /// <summary>Mirrors <c>Bff.Api.Features.Checkout.CheckoutEndpoints.OrderConfirmationResponse</c>.</summary>
    private sealed record OrderConfirmationResponse(Guid Id, DateTime PlacedAtUtc, decimal Total);

    /// <summary>Mirrors <c>Bff.Api.Features.Baskets.BasketsEndpoints.AddBasketItemRequest</c>.</summary>
    private sealed record AddBasketItemRequest(Guid ProductId, int Quantity);
}
