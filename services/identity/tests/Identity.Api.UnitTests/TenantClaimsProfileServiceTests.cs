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

    /// <summary>
    /// Kiểm tra: với 1 user có `TenantId` hợp lệ ("contoso"), `GetProfileDataAsync` phát hành đúng 1
    /// claim `tenant_id` mang giá trị đó.
    /// Lý do: `TenantClaimsProfileService` là nguồn phát hành `tenant_id` DUY NHẤT của token — nếu
    /// nó phát sai/thiếu, mọi service phía sau sẽ nhận nhầm tenant.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — T045, US1 (FR-002).
    /// </summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesTenantIdClaim_ForAUserWithAValidTenantId()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = TenantId });
        var context = CreateRequestContext(ExistingUserId);

        await service.GetProfileDataAsync(context);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử (trả phần tử đó ra), đỏ khi 0 hoặc
        // nhiều hơn.
        var claim = Assert.Single(context.IssuedClaims);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — cho cả tên claim
        // ("tenant_id") lẫn giá trị ("contoso").
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
    /// <summary>
    /// Kiểm tra: với 1 user có `TenantId` rỗng (trường hợp lẽ ra không nên tồn tại, nhưng `required
    /// string` chỉ đảm bảo có mặt lúc biên dịch, không đảm bảo khác rỗng), hàm vẫn phát hành đúng 1
    /// claim, mang giá trị rỗng y nguyên — không tự "sửa"/bỏ qua.
    /// Lý do: service này không gác cổng nội dung claim — mọi service tiêu thụ token phải tự kiểm
    /// tra claim rỗng hay không; test này ghi lại đúng hành vi thật nếu quy tắc validate ở nơi khác
    /// bị vi phạm, thay vì giả định nó tự sửa đúng ở đây.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — T045, US1 (FR-010).
    /// </summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesTheClaimAsIs_ForAUserWithAnEmptyTenantId()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = string.Empty });
        var context = CreateRequestContext(ExistingUserId);

        await service.GetProfileDataAsync(context);

        // Assert.Single(tập hợp): xanh khi có đúng 1 phần tử, đỏ khi 0 hoặc nhiều hơn.
        var claim = Assert.Single(context.IssuedClaims);
        // Assert.Equal(kỳ vọng, thực tế): xanh khi bằng nhau, đỏ khi khác — claim vẫn được phát
        // hành (tên đúng), giá trị đúng bằng chuỗi rỗng, không bị lặng lẽ đổi thành 1 tenant khác.
        Assert.Equal(TenantClaimsProfileService.TenantClaimType, claim.Type);
        Assert.Equal(string.Empty, claim.Value);
    }

    /// <summary>
    /// No user behind the subject — deleted after the token's session started, or a subject this
    /// profile service was never meant to resolve — issues nothing rather than guessing a tenant.
    /// </summary>
    /// <summary>
    /// Kiểm tra: khi subject không ứng với user nào (đã bị xoá, hoặc subject không hợp lệ),
    /// `GetProfileDataAsync` không phát hành claim nào cả.
    /// Lý do: không được đoán mò 1 tenant cho subject không xác định — phát claim rỗng còn hơn phát
    /// nhầm 1 tenant nào đó.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — T045, US1 (FR-010).
    /// </summary>
    [Fact]
    public async Task GetProfileDataAsync_IssuesNoClaims_WhenNoUserExistsForTheSubject()
    {
        var service = CreateService();
        var context = CreateRequestContext("no-such-user");

        await service.GetProfileDataAsync(context);

        // Assert.Empty(tập hợp): xanh khi không có phần tử nào, đỏ khi có.
        Assert.Empty(context.IssuedClaims);
    }

    /// <summary>
    /// Kiểm tra: với subject ứng đúng 1 user tồn tại, `IsActiveAsync` đặt `IsActive = true`.
    /// Lý do: Duende dùng `IsActiveAsync` để quyết định token còn hiệu lực hay không dựa trên tài
    /// khoản đứng sau nó, không chỉ dựa vào chữ ký/hạn dùng của token.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — T045, US1.
    /// </summary>
    [Fact]
    public async Task IsActiveAsync_ReturnsTrue_WhenTheUserExists()
    {
        var service = CreateService(new ApplicationUser { Id = ExistingUserId, UserName = "shopper", TenantId = TenantId });
        var context = CreateIsActiveContext(ExistingUserId);

        await service.IsActiveAsync(context);

        // Assert.True(điều kiện): xanh khi điều kiện đúng, đỏ khi sai.
        Assert.True(context.IsActive);
    }

    /// <summary>
    /// Kiểm tra: với subject không ứng user nào, `IsActiveAsync` đặt `IsActive = false`.
    /// Lý do: user bị xoá sau khi token đã phát hành thì token đó phải bị coi là không còn hiệu lực
    /// — đây là cơ chế Duende dùng để thu hồi hiệu lực mà không cần chờ token tự hết hạn.
    /// Task nguồn: spec 014 (máy chủ định danh thật) — T045, US1.
    /// </summary>
    [Fact]
    public async Task IsActiveAsync_ReturnsFalse_WhenNoUserExistsForTheSubject()
    {
        var service = CreateService();
        var context = CreateIsActiveContext("no-such-user");

        await service.IsActiveAsync(context);

        // Assert.False(điều kiện): xanh khi điều kiện sai, đỏ khi đúng.
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
