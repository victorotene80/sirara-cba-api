using SiraraCBA.Modules.Ledger.Domain.Accounts;
using SiraraCBA.Modules.Ledger.Domain.ChartOfAccounts;
using SiraraCBA.Modules.Ledger.Domain.Identifiers;

namespace SiraraCBA.Modules.Ledger.Domain.Journals;

/// <summary>Which side a line falls on. Direction is never carried by the sign (I-8).</summary>
public enum DebitOrCredit
{
    Debit,
    Credit,
}

/// <summary>Forward-only lifecycle of an entry (I-3).</summary>
public enum JournalStatus
{
    Pending,
    Posted,
    Reversed,
}

/// <summary>
/// One line of an entry. It has no independent lifecycle and no repository: a line
/// outside its entry cannot be balanced, so it cannot be an aggregate.
/// </summary>
public sealed class JournalLine : Entity<JournalLineId>
{
    /// <summary>
    /// For persistence only. EF cannot bind a complex value through a constructor, so
    /// it rehydrates field by field. Private, so the factory below remains the only
    /// way anything else obtains a line.
    /// </summary>
    private JournalLine()
        : base(default)
    {
    }

    internal JournalLine(
        JournalLineId id,
        AccountId accountId,
        DebitOrCredit direction,
        Money amount,
        int sequenceNumber)
        : base(id)
    {
        AccountId = accountId;
        Direction = direction;
        Amount = amount;
        SequenceNumber = sequenceNumber;
    }

    public AccountId AccountId { get; }

    public DebitOrCredit Direction { get; }

    /// <summary>Strictly positive (I-8). Scale comes from the currency, never from a literal.</summary>
    public Money Amount { get; }

    public int SequenceNumber { get; }
}

/// <summary>
/// What a caller proposes as one line, before the entry exists.
/// </summary>
/// <remarks>
/// Carries the <see cref="Accounts.Account"/> and its chart node rather than bare
/// ids so that postability and account status are decided with zero I/O — the
/// caller has already resolved them. The line that results stores only the id.
/// <para>
/// The line's identifier is supplied, not generated here: identifiers come from
/// <c>IIdGenerator</c> (ER-7), which lives in BuildingBlocks and which Domain may
/// not reference. Same reason the entry's own <c>JournalId</c> is a parameter.
/// </para>
/// </remarks>
public sealed record JournalLineDraft(
    JournalLineId Id,
    Account Account,
    ChartOfAccount GlAccount,
    DebitOrCredit Direction,
    Money Amount);

/// <summary>
/// The aggregate root, owning its lines. The factory is the only way to obtain one,
/// and it validates I-1, I-2, I-8 and minimum-two-lines first — so an unbalanced
/// entry is unrepresentable rather than merely rejected somewhere later.
/// </summary>
public sealed class JournalEntry : AggregateRoot<JournalId>
{
    private readonly List<JournalLine> _lines = [];

    private JournalEntry(
        JournalId id,
        DateOnly transactionDate,
        DateOnly valueDate,
        DateOnly postingDate,
        string description,
        string sourceSystem,
        string sourceReference,
        Currency currency,
        Guid createdBy)
        : base(id)
    {
        TransactionDate = transactionDate;
        ValueDate = valueDate;
        PostingDate = postingDate;
        Description = description;
        SourceSystem = sourceSystem;
        SourceReference = sourceReference;
        Currency = currency;
        Status = JournalStatus.Pending;
        CreatedBy = createdBy;
    }

    public DateOnly TransactionDate { get; }

    public DateOnly ValueDate { get; }

    public DateOnly PostingDate { get; }

    public string Description { get; }

    public string SourceSystem { get; }

    /// <summary>Unique with <see cref="SourceSystem"/> — the natural idempotency key (I-5).</summary>
    public string SourceReference { get; }

    /// <summary>The entry's single currency. Every line shares it (I-2).</summary>
    public Currency Currency { get; }

    public JournalStatus Status { get; }

    public Guid CreatedBy { get; }

    /// <summary>
    /// The entry's lines, owned by it. Exposed read-only over a private list so the
    /// collection cannot be added to from outside the factory — and so persistence
    /// has a mutable field to rehydrate into without the aggregate offering a way to
    /// mutate it.
    /// </summary>
    public IReadOnlyList<JournalLine> Lines => _lines;

    /// <summary>
    /// Builds an entry, or explains why it cannot exist. Takes an arbitrary number
    /// of lines, never two — a fee plus an FX margin is four (D-11).
    /// </summary>
    /// <remarks>
    /// Dates are parameters, not read from a clock: Domain never touches
    /// <c>IClock</c> or ambient time (ER-7).
    /// </remarks>
    public static Result<JournalEntry> Create(
        JournalId id,
        DateOnly transactionDate,
        DateOnly valueDate,
        DateOnly postingDate,
        string description,
        string sourceSystem,
        string sourceReference,
        Guid createdBy,
        IReadOnlyList<JournalLineDraft> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        if (lines.Count < MinimumLinesPerEntry)
        {
            return Result.Failure<JournalEntry>(LedgerErrors.TooFewLines(
                $"An entry needs at least {MinimumLinesPerEntry} lines; {lines.Count} were given."));
        }

        // I-8 before anything else. A "negative debit" is a credit, and allowing both
        // spellings makes every downstream aggregate ambiguous.
        foreach (JournalLineDraft line in lines)
        {
            if (!line.Amount.IsPositive)
            {
                return Result.Failure<JournalEntry>(LedgerErrors.AmountNotPositive(
                    $"Line for account '{line.Account.Number.Value}' has amount {line.Amount}. "
                    + "Amounts are strictly positive; direction is carried by debit or credit."));
            }
        }

        // I-2 before any arithmetic. Money's operators throw across currencies, and a
        // mixed-currency entry is an expected business outcome that must come back as
        // a Result — so the currencies are agreed before a single sum is taken.
        Currency currency = lines[0].Amount.Currency;

        foreach (JournalLineDraft line in lines)
        {
            if (line.Amount.Currency != currency)
            {
                return Result.Failure<JournalEntry>(LedgerErrors.MixedCurrency(
                    $"Entry mixes {currency.Code} and {line.Amount.Currency.Code}. A cross-currency "
                    + "event is two linked entries sharing a source reference, not one mixed entry."));
            }
        }

        foreach (JournalLineDraft line in lines)
        {
            if (!line.GlAccount.IsPostable)
            {
                return Result.Failure<JournalEntry>(LedgerErrors.AccountNotPostable(
                    $"GL code '{line.GlAccount.Id.Value}' is a roll-up node. Only leaves are postable."));
            }

            Result postable = line.Account.CanAcceptPosting();

            if (postable.IsFailure)
            {
                return Result.Failure<JournalEntry>(postable.Error);
            }
        }

        // I-1. Every amount is now known to share one currency, so this arithmetic
        // cannot throw.
        Money debits = Total(lines, DebitOrCredit.Debit, currency);
        Money credits = Total(lines, DebitOrCredit.Credit, currency);

        if (debits != credits)
        {
            return Result.Failure<JournalEntry>(LedgerErrors.Unbalanced(
                $"Debits are {debits} and credits are {credits}."));
        }

        List<JournalLine> entryLines = [.. lines.Select((line, index) => new JournalLine(
            line.Id,
            line.Account.Id,
            line.Direction,
            line.Amount,
            index + FirstSequenceNumber))];

        JournalEntry entry = new(
            id,
            transactionDate,
            valueDate,
            postingDate,
            description,
            sourceSystem,
            sourceReference,
            currency,
            createdBy);

        entry._lines.AddRange(entryLines);

        return entry;
    }

    private static Money Total(
        IReadOnlyList<JournalLineDraft> lines,
        DebitOrCredit direction,
        Currency currency) =>
        lines
            .Where(line => line.Direction == direction)
            .Aggregate(Money.Zero(currency), (running, line) => running + line.Amount);

    /// <summary>Double entry needs two sides. Structural, not a business threshold.</summary>
    private const int MinimumLinesPerEntry = 2;

    /// <summary>Lines are numbered from one for audit and display, not from zero.</summary>
    private const int FirstSequenceNumber = 1;
}
