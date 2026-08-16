namespace Tenancy;

/// <summary>
/// The caller resolved for the current request — a request-scoped holder populated by
/// <see cref="CallerContextMiddleware"/> from the <c>X-Subject-Id</c> header the gateway stamped.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately shaped exactly like <see cref="TenantContext"/>, with the same two states —
/// Unresolved (<see cref="SubjectId"/> null or blank) and Resolved — and the same absence of a
/// "resolved to a default" third state. The two answer different questions: the tenant decides
/// which data store a request may reach, the subject decides whose rows inside it
/// (004-minimal-shopping-spa contracts/subject-id-header.md).
/// </para>
/// <para>
/// <see cref="SubjectId"/> is publicly settable for the same reason the tenant's is: test-seeding
/// helpers resolve services from a manually created DI scope with no HTTP request behind it, so no
/// middleware runs and they must prime the caller themselves. A real request only ever has this set
/// by the middleware.
/// </para>
/// </remarks>
public sealed class CallerContext
{
    /// <summary>
    /// The resolved subject identifier, or <see langword="null"/> while unresolved.
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// Returns the resolved subject identifier, or throws if this request has none.
    /// </summary>
    /// <exception cref="MissingCallerContextException">
    /// The caller is unresolved. Blank counts as unresolved: a caller whose identifier is an empty
    /// string is not a caller, and treating it as one would give every such request the same basket.
    /// </exception>
    public string RequireSubjectId() =>
        string.IsNullOrWhiteSpace(SubjectId)
            ? throw new MissingCallerContextException()
            : SubjectId;
}
