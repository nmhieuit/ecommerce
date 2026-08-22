using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Api.ContractTests;

/// <summary>
/// The provider state a Pact interaction was recorded under, as the verifier reports it.
/// </summary>
/// <param name="State">The state description the consumer wrote in its <c>Given(...)</c>.</param>
/// <param name="Action">
/// <c>setup</c> before the interaction is replayed, <c>teardown</c> after. Only setup does
/// anything here: each setup states the whole database it needs, so there is nothing left over for
/// a teardown to undo.
/// </param>
/// <param name="Params">The values the consumer attached to the state, if any.</param>
public sealed record ProviderState(string State, string Action, IReadOnlyDictionary<string, string> Params)
{
    public string Require(string key) =>
        Params.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"Provider state '{State}' was replayed without the '{key}' parameter the consumer pact records.");
}

/// <summary>
/// Adds the endpoint Pact's verifier calls to put this service's database into the state an
/// interaction was recorded under, ahead of everything the service itself maps.
/// </summary>
/// <remarks>
/// <para>
/// Test-only, and deliberately so: it is registered by the contract-test host, never by
/// <c>Program.cs</c>, so nothing about it can reach a deployed service. Registering it as a
/// startup filter rather than through <c>Configure</c> is what keeps the service's own pipeline
/// intact — the point of a provider verification is that the real routes answer, so replacing the
/// pipeline would defeat it.
/// </para>
/// <para>
/// The route sits ahead of the tenancy middleware and creates its own scope with the tenant
/// primed, the same way the integration suites seed. Without it the gated <c>DbContext</c>
/// registration refuses, which is the behaviour under test elsewhere and merely in the way here.
/// </para>
/// </remarks>
internal sealed class ProviderStateStartupFilter(
    string tenantId,
    Func<ProviderState, IServiceProvider, Task> applyAsync) : IStartupFilter
{
    /// <summary>The path the verifier is pointed at. Nothing else answers on it.</summary>
    public const string Path = "/_pact/provider-states";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, proceed) =>
        {
            if (!context.Request.Path.Equals(Path, StringComparison.OrdinalIgnoreCase) ||
                !HttpMethods.IsPost(context.Request.Method))
            {
                await proceed();
                return;
            }

            var state = await JsonSerializer.DeserializeAsync<ProviderStateRequest>(
                context.Request.Body,
                JsonOptions,
                context.RequestAborted);

            if (state is null)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }

            if (string.Equals(state.Action, "setup", StringComparison.OrdinalIgnoreCase))
            {
                using var scope = context.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateScope();
                scope.ServiceProvider.GetRequiredService<Tenancy.TenantContext>().TenantId = tenantId;

                await applyAsync(
                    new ProviderState(state.State ?? string.Empty, state.Action, state.Params ?? new Dictionary<string, string>()),
                    scope.ServiceProvider);
            }

            context.Response.StatusCode = StatusCodes.Status200OK;
        });

        next(app);
    };

    /// <summary>The body pact-reference posts. Its shape is the verifier's, not ours.</summary>
    private sealed record ProviderStateRequest(string? State, string Action, Dictionary<string, string>? Params);
}
