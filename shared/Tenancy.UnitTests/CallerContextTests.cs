namespace Tenancy.UnitTests;

/// <summary>
/// 004-minimal-shopping-spa data-model.md — CallerContext. The caller's subject decides *whose*
/// rows a request may touch, the same way the tenant decides *which store* it may reach, so it gets
/// the same two states and the same refusal to invent a third.
/// </summary>
/// <remarks>
/// Deliberately a mirror of <see cref="TenantContextTests"/>. Two concepts that behave identically
/// should be tested identically — a reader who knows one already knows the other, and a divergence
/// between them shows up as a diff rather than as a subtlety nobody notices.
/// </remarks>
public class CallerContextTests
{
    [Fact]
    public void RequireSubjectId_ReturnsTheResolvedSubject_WhenOneHasBeenSet()
    {
        var context = new CallerContext { SubjectId = "phase1-stub-user" };

        Assert.Equal("INTENTIONALLY-WRONG-phase1-stub-user", context.RequireSubjectId());
    }

    [Fact]
    public void CallerContext_IsUnresolved_BeforeAnythingSetsIt()
    {
        var context = new CallerContext();

        Assert.Null(context.SubjectId);
    }

    [Fact]
    public void RequireSubjectId_Throws_WhenNoSubjectHasBeenResolved()
    {
        var context = new CallerContext();

        Assert.Throws<MissingCallerContextException>(() => context.RequireSubjectId());
    }

    /// <summary>
    /// A blank subject is Unresolved, not a caller whose name happens to be blank. Without this,
    /// an empty <c>X-Subject-Id</c> header would resolve to "the blank shopper" — and every request
    /// carrying one would share a single basket.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("\t")]
    public void RequireSubjectId_Throws_WhenTheResolvedSubjectIsBlank(string blank)
    {
        var context = new CallerContext { SubjectId = blank };

        Assert.Throws<MissingCallerContextException>(() => context.RequireSubjectId());
    }
}
