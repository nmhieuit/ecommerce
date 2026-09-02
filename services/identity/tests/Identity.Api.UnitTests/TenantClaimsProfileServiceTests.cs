using System.Security.Claims;
using Duende.IdentityServer.Models;
using Identity.Api.Data;
using Identity.Api.HostedIdentity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Identity.Api.UnitTests;

/// <summary>
/// tasks.md T045: <see cref="TenantClaimsProfileService"/> issues the correct <c>tenant_id</c>
/// claim (data-model.md — Token) for each shape of Identity User, isolated from the full login
/// flow T017 already covers end-to-end via <c>Identity.Api.IntegrationTests.LoginIssuesTokenTests</c>.
/// </summary>
/// <remarks>
/// <see cref="UserManager{TUser}"/> is not an interface, so isolating from the full ASP.NET Core
/// Identity + EF stack means giving it a minimal in-memory <see cref="IUserStore{TUser}"/> rather
/// than a mocking framework this repository does not otherwise depend on (no Moq/NSubstitute
/// package is referenced anywhere) — the real <see cref="UserManager{TUser}"/> runs against a fake
/// store, rather than faking <see cref="UserManager{TUser}"/> itself.
/// </remarks>
public class TenantClaimsProfileServiceTests
{
    private const string ExistingUserId = "9f8d6b1e-user-0001";
    private const string TenantId = "contoso";

    /// <summary>data-model.md validation rule: a login-eligible user has exactly one non-empty TenantId.</summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesTenantIdClaim_ForAUserWithAValidTenantId()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = TenantId });
        var context = CreateRequestContext(ExistingUserId);

        await service.GetProfileDataAsync(context);

        var claim = Assert.Single(context.IssuedClaims);
        Assert.Equal(TenantClaimsProfileService.TenantClaimType, claim.Type);
        Assert.Equal(TenantId, claim.Value);
    }

    /// <summary>
    /// data-model.md line 48/51: a token is only ever as trustworthy as the claim it carries — every
    /// consuming service must independently check the claim is non-empty, precisely because this
    /// service does not gatekeep it. A user record with an empty <see cref="ApplicationUser.TenantId"/>
    /// should never exist per the validation rule above, but <c>required string</c> is a compile-time
    /// guarantee only, not a non-empty guarantee — this test documents what actually happens if that
    /// rule is ever violated upstream, rather than assuming it silently self-corrects here.
    /// </summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesTheClaimAsIs_ForAUserWithAnEmptyTenantId()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = string.Empty });
        var context = CreateRequestContext(ExistingUserId);

        await service.GetProfileDataAsync(context);

        var claim = Assert.Single(context.IssuedClaims);
        Assert.Equal(TenantClaimsProfileService.TenantClaimType, claim.Type);
        Assert.Equal(string.Empty, claim.Value);
    }

    /// <summary>
    /// No user behind the subject — deleted after the token's session started, or a subject this
    /// profile service was never meant to resolve — issues nothing rather than guessing a tenant.
    /// </summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesNoClaims_WhenNoUserExistsForTheSubject()
    {
        var service = CreateService();
        var context = CreateRequestContext("no-such-user");

        await service.GetProfileDataAsync(context);

        Assert.Empty(context.IssuedClaims);
    }

    [Fact]
    public async Task IsActiveAsync_ReturnsTrue_WhenTheUserExists()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = TenantId });
        var context = CreateIsActiveContext(ExistingUserId);

        await service.IsActiveAsync(context);

        Assert.True(context.IsActive);
    }

    [Fact]
    public async Task IsActiveAsync_ReturnsFalse_WhenNoUserExistsForTheSubject()
    {
        var service = CreateService();
        var context = CreateIsActiveContext("no-such-user");

        await service.IsActiveAsync(context);

        Assert.False(context.IsActive);
    }

    private static TenantClaimsProfileService CreateService(params ApplicationUser[] seededUsers) =>
        new(CreateUserManager(new FakeUserStore(seededUsers)));

    private static readonly Client TestClient = new() { ClientId = "test-client" };

    private static ProfileDataRequestContext CreateRequestContext(string subjectId) =>
        new(SubjectFor(subjectId), TestClient, caller: "test", requestedClaimTypes: [])
        {
            IssuedClaims = [],
        };

    private static IsActiveContext CreateIsActiveContext(string subjectId) =>
        new(SubjectFor(subjectId), TestClient, caller: "test");

    private static ClaimsPrincipal SubjectFor(string subjectId) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, subjectId)], "test"));

    private static UserManager<ApplicationUser> CreateUserManager(IUserStore<ApplicationUser> store) =>
        new(
            store,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            services: null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

    /// <summary>
    /// The minimum <see cref="IUserStore{TUser}"/> surface <see cref="UserManager{TUser}.GetUserAsync(ClaimsPrincipal)"/>
    /// needs — it resolves the <c>NameIdentifier</c> claim to a user id, then calls <see cref="FindByIdAsync"/>.
    /// </summary>
    private sealed class FakeUserStore(IReadOnlyCollection<ApplicationUser> users) : IUserStore<ApplicationUser>
    {
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(users.SingleOrDefault(u => u.Id == userId));

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Not needed by GetUserAsync(ClaimsPrincipal).");

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id);

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Read-only fake — this suite never writes through the store.");

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Read-only fake — this suite never writes through the store.");

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Read-only fake — this suite never writes through the store.");

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Read-only fake — this suite never writes through the store.");

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Read-only fake — this suite never writes through the store.");

        public void Dispose()
        {
        }
    }
}
