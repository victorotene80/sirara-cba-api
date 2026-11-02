namespace SiraraCBA.Modules.Ledger.Contracts;

/// <summary>
/// Which side of the books a line falls on.
/// </summary>
/// <remarks>
/// Declared here rather than borrowed from the Domain: ER-5 forbids sharing a
/// domain type across a boundary, not having one's own. It is an enum rather than a
/// string because after extraction this contract becomes a wire format, and
/// <c>Debit</c> against <c>debit</c> against <c>DEBIT</c> is a bug the compiler
/// cannot see. Direction is the only thing carrying sign (I-8), so it is the last
/// field in the ledger that should be ambiguous.
/// </remarks>
public enum PostingDirection
{
    /// <summary>Increases an asset or expense; decreases a liability, equity or income.</summary>
    Debit,

    /// <summary>Increases a liability, equity or income; decreases an asset or expense.</summary>
    Credit,
}

/// <summary>One proposed line of a posting request.</summary>
/// <param name="AccountId">The account to post to.</param>
/// <param name="Direction">Which side the line falls on. Never the sign (I-8).</param>
/// <param name="Amount">Strictly positive. Scale is validated against the currency registry.</param>
public sealed record PostJournalLineRequest(
    Guid AccountId,
    PostingDirection Direction,
    decimal Amount);

/// <summary>
/// A request to post one journal entry.
/// </summary>
/// <remarks>
/// Idempotency is the natural key <see cref="SourceSystem"/> + <see cref="SourceReference"/>
/// (I-5, ER-8): replaying it returns the original id and posts nothing new. Lines are
/// an arbitrary number, deliberately not two — a principal, a fee and an FX margin is
/// four, and the cross-border build must work against this contract unchanged (D-11).
/// </remarks>
/// <param name="SourceSystem">The system originating the entry.</param>
/// <param name="SourceReference">That system's reference. Unique with <paramref name="SourceSystem"/>.</param>
/// <param name="TransactionDate">When the economic event happened.</param>
/// <param name="ValueDate">When it affects the balance.</param>
/// <param name="PostingDate">When it hits the ledger.</param>
/// <param name="Description">Human-readable narrative.</param>
/// <param name="CurrencyCode">ISO 4217 code. Every line shares it (I-2).</param>
/// <param name="Lines">Two or more lines whose debits equal their credits (I-1).</param>
public sealed record PostJournalEntryRequest(
    string SourceSystem,
    string SourceReference,
    DateOnly TransactionDate,
    DateOnly ValueDate,
    DateOnly PostingDate,
    string Description,
    string CurrencyCode,
    IReadOnlyList<PostJournalLineRequest> Lines);

/// <summary>The outcome of a posting.</summary>
/// <param name="JournalId">The entry's id. On a replay this is the original id (I-5).</param>
/// <param name="WasReplay">True when the request had already been posted.</param>
public sealed record PostJournalEntryResponse(Guid JournalId, bool WasReplay);

/// <summary>A request to reverse a posted entry by mirrored entry, never by edit (I-4, D-10).</summary>
/// <param name="JournalId">The entry to reverse.</param>
/// <param name="SourceSystem">The system originating the reversal.</param>
/// <param name="SourceReference">That system's reference for the reversal. Idempotency key (I-5).</param>
/// <param name="Reason">Why it is being reversed.</param>
public sealed record ReverseJournalEntryRequest(
    Guid JournalId,
    string SourceSystem,
    string SourceReference,
    string Reason);

/// <summary>One line as read back from the ledger.</summary>
/// <param name="JournalId">The entry the line belongs to.</param>
/// <param name="AccountId">The account posted to.</param>
/// <param name="Direction">Which side the line fell on.</param>
/// <param name="Amount">The positive amount.</param>
/// <param name="CurrencyCode">ISO 4217 code.</param>
/// <param name="PostingDate">When it hit the ledger.</param>
/// <param name="SequenceNumber">Line order within the entry.</param>
public sealed record JournalLineDto(
    Guid JournalId,
    Guid AccountId,
    PostingDirection Direction,
    decimal Amount,
    string CurrencyCode,
    DateOnly PostingDate,
    int SequenceNumber);

/// <summary>An account's balance on a date.</summary>
/// <param name="AccountId">The account.</param>
/// <param name="AsOf">The date the balance is stated for.</param>
/// <param name="LedgerBalance">Posted and confirmed only.</param>
/// <param name="AvailableBalance">Posted, plus holds and pending debits.</param>
/// <param name="CurrencyCode">ISO 4217 code.</param>
public sealed record AccountBalanceDto(
    Guid AccountId,
    DateOnly AsOf,
    decimal LedgerBalance,
    decimal AvailableBalance,
    string CurrencyCode);

/// <summary>Which statement lines to read.</summary>
/// <param name="AccountId">The account.</param>
/// <param name="From">Inclusive start date.</param>
/// <param name="To">Inclusive end date.</param>
public sealed record StatementQuery(Guid AccountId, DateOnly From, DateOnly To);

/// <summary>
/// The one door through which anything writes to the ledger (I-7, D-8).
/// </summary>
/// <remarks>
/// Written as if remote (ER-4): every method returns a <see cref="Result{TValue}"/>,
/// takes a <see cref="CancellationToken"/>, and is safe to retry.
/// </remarks>
public interface ILedgerPostingService
{
    /// <summary>Posts one entry, or explains why it cannot be posted.</summary>
    /// <param name="request">The entry to post.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The entry's id, or the original id when this is a replay.</returns>
    public Task<Result<PostJournalEntryResponse>> PostAsync(
        PostJournalEntryRequest request,
        CancellationToken cancellationToken);

    /// <summary>Reverses a posted entry by writing its mirror.</summary>
    /// <param name="request">The entry to reverse.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The reversing entry's id.</returns>
    public Task<Result<PostJournalEntryResponse>> ReverseAsync(
        ReverseJournalEntryRequest request,
        CancellationToken cancellationToken);
}

/// <summary>Reads from the ledger. Never writes.</summary>
public interface ILedgerQueryService
{
    /// <summary>Reads an account's balance on a date.</summary>
    /// <param name="accountId">The account.</param>
    /// <param name="asOf">The date.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The balance, or a failure if the account is unknown.</returns>
    public Task<Result<AccountBalanceDto>> GetBalanceAsync(
        Guid accountId,
        DateOnly asOf,
        CancellationToken cancellationToken);

    /// <summary>Reads an account's statement lines.</summary>
    /// <param name="query">Which lines to read.</param>
    /// <param name="cancellationToken">Cancels the operation.</param>
    /// <returns>The lines, oldest first.</returns>
    public Task<Result<IReadOnlyList<JournalLineDto>>> GetStatementAsync(
        StatementQuery query,
        CancellationToken cancellationToken);
}
