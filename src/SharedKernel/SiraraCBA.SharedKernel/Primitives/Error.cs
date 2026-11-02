namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// A named, machine-readable failure. <c>Code</c> is stable and safe to branch on;
/// <c>Message</c> is for humans and may change.
/// </summary>
public sealed record Error(string Code, string Message, ErrorType Type)
{
    /// <summary>The absence of an error. Never carried by a failed result.</summary>
    public static readonly Error None = new(string.Empty, string.Empty, ErrorType.Failure);

    public static Error Validation(string code, string message) => new(code, message, ErrorType.Validation);

    public static Error Conflict(string code, string message) => new(code, message, ErrorType.Conflict);

    public static Error NotFound(string code, string message) => new(code, message, ErrorType.NotFound);

    public static Error Forbidden(string code, string message) => new(code, message, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string message) => new(code, message, ErrorType.Unauthorized);

    public static Error Failure(string code, string message) => new(code, message, ErrorType.Failure);

    public override string ToString() => $"{Code}: {Message}";
}
