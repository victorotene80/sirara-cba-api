namespace SiraraCBA.BuildingBlocks.Application.Abstractions;

/// <summary>
/// Who is making the current request, and how strongly they proved it.
/// </summary>
/// <remarks>
/// Modules depend on this, never on <c>HttpContext</c> or <c>ClaimsPrincipal</c>.
/// That is what lets the same command run identically from an HTTP request, a
/// background job, and a maker-checker approval execution (auth-spec.md §4).
/// </remarks>
public interface ICurrentUser
{
    /// <summary>The authenticated user, or <see langword="null"/> when the caller is anonymous or a system process.</summary>
    public Guid? UserId { get; }

    public bool IsAuthenticated { get; }

    /// <summary>Permissions granted for this request. Enforcement is always on a permission, never a role name (D-5).</summary>
    public IReadOnlySet<string> Permissions { get; }

    /// <summary>When this session's authentication was established, for step-up age checks (auth-spec.md §5).</summary>
    public DateTimeOffset? AuthenticatedAt { get; }

    /// <summary>How strongly the user authenticated, for step-up level checks.</summary>
    public string? AuthenticationLevel { get; }

    /// <summary>Correlates every log, audit row and outbox message produced by this request.</summary>
    public string CorrelationId { get; }

    public bool HasPermission(string permission);
}
