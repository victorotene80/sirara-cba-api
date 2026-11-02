namespace SiraraCBA.SharedKernel.Primitives;

/// <summary>
/// The outcome of an operation that can fail for an expected business reason.
/// Expected outcomes are results; exceptions are for the genuinely exceptional
/// (coding-standards.md, Application).
/// </summary>
public class Result
{
    protected Result(bool isSuccess, Error error)
    {
        // A success carrying an error, or a failure carrying none, is a
        // programming error — not a business outcome.
        if (isSuccess && error != Error.None)
        {
            throw new InvalidOperationException("A successful result cannot carry an error.");
        }

        if (!isSuccess && error == Error.None)
        {
            throw new InvalidOperationException("A failed result must carry an error.");
        }

        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    public Error Error { get; }

    /// <summary>True when the operation was parked for maker-checker approval.</summary>
    public bool IsPending => Error.Type == ErrorType.Pending;

    public static Result Success() => new(true, Error.None);

    public static Result Failure(Error error) => new(false, error);

    public static Result<TValue> Success<TValue>(TValue value) => new(value, true, Error.None);

    public static Result<TValue> Failure<TValue>(Error error) => new(default, false, error);

    /// <summary>
    /// The command was accepted and parked for approval. Callers handle this
    /// alongside success and failure (maker-checker-spec.md §6).
    /// </summary>
    public static Result Pending(Guid approvalRequestId) =>
        new(false, new Error(PendingCode, approvalRequestId.ToString(), ErrorType.Pending));

    /// <inheritdoc cref="Pending(Guid)"/>
    public static Result<TValue> Pending<TValue>(Guid approvalRequestId) =>
        Failure<TValue>(new Error(PendingCode, approvalRequestId.ToString(), ErrorType.Pending));

    /// <summary>The stable error code identifying a parked, awaiting-approval outcome.</summary>
    public const string PendingCode = "approval.pending";
}

/// <summary>A <see cref="Result"/> carrying a value on success.</summary>
public sealed class Result<TValue> : Result
{
    private readonly TValue? _value;

    internal Result(TValue? value, bool isSuccess, Error error)
        : base(isSuccess, error)
    {
        _value = value;
    }

    /// <summary>
    /// The value. Reading it on a failed result is a programming error — check
    /// <see cref="Result.IsSuccess"/> first.
    /// </summary>
    public TValue Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("The value of a failed result cannot be accessed.");

    public static implicit operator Result<TValue>(TValue value) => Success(value);
}
