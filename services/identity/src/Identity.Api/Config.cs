using Duende.IdentityServer;
using Duende.IdentityServer.Models;

namespace Identity.Api;

/// <summary>
/// The resources, scopes, and clients this identity server issues tokens for (data-model.md —
/// Client Application). Seeded into the configuration store by <see cref="Data.SeedData"/>.
/// </summary>
public static class Config
{
    /// <summary>The API scope every downstream service (BFF, domain services) requires a token to carry.</summary>
    public const string ApiScopeName = "ecommerce-api";

    public static IEnumerable<IdentityResource> IdentityResources =>
        [new IdentityResources.OpenId(), new IdentityResources.Profile()];

    public static IEnumerable<ApiScope> ApiScopes => [new ApiScope(ApiScopeName, "Ecommerce Platform API")];

    public static IEnumerable<ApiResource> ApiResources =>
        [new ApiResource(ApiScopeName, "Ecommerce Platform API") { Scopes = { ApiScopeName } }];

    /// <summary>
    /// Client applications. Only one production client exists: the web storefront SPA, using
    /// Authorization Code + PKCE (research.md Decision 9) — the only grant type a browser-based
    /// public client should use. It has no interactive login UI wired up yet in this phase (Duende's
    /// own Razor Pages quickstart UI is a separate, explicitly deferred piece of work — see the
    /// spawned follow-up task), so it cannot complete a real browser login end to end today; its
    /// registration exists so that work is additive, not a redesign, when the UI lands.
    /// </summary>
    public static IEnumerable<Client> Clients =>
        [
            new Client
            {
                ClientId = "ecommerce-web-spa",
                ClientName = "Ecommerce Web Storefront",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false, // public client (browser SPA) — PKCE replaces the secret
                RedirectUris = { "http://localhost:5173/callback", "http://localhost:4173/callback" },
                PostLogoutRedirectUris = { "http://localhost:5173", "http://localhost:4173" },
                AllowedScopes =
                {
                    IdentityServerConstants.StandardScopes.OpenId,
                    IdentityServerConstants.StandardScopes.Profile,
                    ApiScopeName,
                },
            },
            IntegrationTestClient,
        ];

    /// <summary>
    /// A second, explicitly non-production client using the Resource Owner Password grant —
    /// <em>not</em> a contradiction of research.md Decision 9 (which scopes the SPA client to
    /// Authorization Code + PKCE only). It exists solely so tasks.md T017's integration test can
    /// exercise real login → token issuance over HTTP without first building the interactive login
    /// UI the Authorization Code flow needs (a browser redirect to a login page) — building that UI
    /// is out of this phase's scope (spec Assumptions: account/login UX is a separate concern; see
    /// the spawned follow-up task for the Razor Pages quickstart UI). This client must never be
    /// deployed with the SPA's real user base reachable through it in a production environment.
    /// </summary>
    private static Client IntegrationTestClient =>
        new()
        {
            ClientId = "integration-test-ropc",
            ClientName = "Integration test client (Resource Owner Password — not used by any real client)",
            AllowedGrantTypes = GrantTypes.ResourceOwnerPassword,
            ClientSecrets = { new Secret("integration-test-secret".Sha256()) },
            AllowedScopes =
            {
                IdentityServerConstants.StandardScopes.OpenId,
                IdentityServerConstants.StandardScopes.Profile,
                ApiScopeName,
            },
        };
}
