namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// The category of an <see cref="Error"/>. Transport layers map these to status
/// codes; nothing in Domain or Application branches on them.
/// </summary>
public enum ErrorType
{
    /// <summary>An unexpected or unclassified failure.</summary>
    Failure = 0,

    /// <summary>The request was structurally invalid.</summary>
    Validation = 1,

    /// <summary>A business invariant refused the operation.</summary>
    Conflict = 2,

    /// <summary>The addressed thing does not exist.</summary>
    NotFound = 3,

    /// <summary>The caller is not authenticated.</summary>
    Unauthorized = 4,

    /// <summary>The caller is authenticated but lacks the required permission.</summary>
    Forbidden = 5,

    /// <summary>
    /// The command was accepted but parked for maker-checker approval and has not
    /// executed. A first-class outcome, not an error (maker-checker-spec.md §6).
    /// </summary>
    Pending = 6,

    /// <summary>
    /// Authentication is valid but not strong or recent enough for this operation
    /// (auth-spec.md §5).
    /// </summary>
    StepUpRequired = 7,
}
