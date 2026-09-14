namespace CriticalPathLoadTests;

/// <summary>
/// Creates the one <see cref="HttpClient"/> every step of the critical path calls through — the
/// gateway, exactly the surface a real shopper's browser addresses (constitution Principle IX:
/// "Frontends talk to the BFF only").
/// </summary>
/// <remarks>
/// <para>
/// Attaches no bearer token — NOT because none is needed (an earlier version of this comment
/// assumed that; it was wrong), but because there is currently no valid, safe way for an
/// out-of-process HTTP client to obtain one. Confirmed by actually running this test against a live
/// <c>docker-compose.yml</c> + <c>docker-compose.demo.yml</c> stack (tasks.md T017): the gateway's
/// <c>StubIdentityAuthenticationHandler</c> only forwards <c>X-Tenant-Id</c>/<c>X-Subject-Id</c>
/// headers downstream — never a token — while the BFF's <c>AddIdentityValidation()</c>
/// (<c>Program.cs</c>) validates a real JWT unconditionally, with no stub bypass of its own. A
/// caller with no token is therefore rejected by the BFF with 401 regardless of the gateway's
/// toggle state.
/// </para>
/// <para>
/// Minting a real token via <c>/connect/token</c> (Resource Owner Password grant, client
/// <c>integration-test-ropc</c> — see
/// <c>services/identity/tests/Identity.Api.IntegrationTests/LoginIssuesTokenTests.cs</c>) needs an
/// existing user, and <c>services/identity/src/Identity.Api/Data/SeedData.cs</c> deliberately seeds
/// none. Building a way to obtain one is a platform-level decision outside this load test's
/// authority — see research.md Quyết định 6 for the full account and the follow-up task tracking it.
/// Until that exists, a run against a freshly started stack correctly FAILS on 401 (spec FR-004: a
/// real problem — here, "cannot authenticate" — must fail the run, not pass silently).
/// </para>
/// </remarks>
public static class GatewayClient
{
    /// <summary>Same environment variable and default as <c>walkthrough.spec.ts</c>'s <c>GATEWAY_ORIGIN</c>.</summary>
    public static Uri ResolveGatewayOrigin() =>
        new(Environment.GetEnvironmentVariable("GATEWAY_ORIGIN") ?? "http://localhost:5300");

    public static HttpClient Create() => new() { BaseAddress = ResolveGatewayOrigin() };
}
