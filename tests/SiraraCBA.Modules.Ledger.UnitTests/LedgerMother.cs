namespace SiraraCBA.Modules.Ledger.UnitTests;

/// <summary>
/// Object mothers for the ledger domain. Every test builds from these rather than
/// repeating a setup block (coding-standards.md, Testing).
/// </summary>
internal static class LedgerMother
{
    // Two currencies with different minor units, so no test can accidentally pass
    // by assuming 2 (ER-9).
    public static Currency Ngn { get; } = Currency.Create("NGN", 2).Value;

    public static Currency Jpy { get; } = Currency.Create("JPY", 0).Value;

    public static ChartOfAccount PostableLeaf(string glCode = "10101") =>
        ChartOfAccount.Create(
            new GlCode(glCode),
            "Customer deposits",
            AccountType.Liability,
            NormalBalance.Credit,
            new GlCode("101"),
            level: 2,
            isPostable: true).Value;

    public static ChartOfAccount RollUpNode(string glCode = "101") =>
        ChartOfAccount.Create(
            new GlCode(glCode),
            "Deposits",
            AccountType.Liability,
            NormalBalance.Credit,
            new GlCode("1"),
            level: 1,
            isPostable: false).Value;

    public static Account ActiveAccount(Currency? currency = null, string number = "0000000001") =>
        AccountWith(AccountStatus.Active, currency, number);

    public static Account AccountWith(
        AccountStatus status,
        Currency? currency = null,
        string number = "0000000001") =>
        Account.Create(
            new AccountId(Guid.CreateVersion7()),
            new AccountNumber(number),
            customerId: null,
            productType: "current",
            glCode: new GlCode("10101"),
            currency: currency ?? Ngn,
            status: status,
            openedAt: new DateOnly(2026, 1, 1)).Value;

    public static Money Amount(decimal value, Currency? currency = null) =>
        Money.Create(value, currency ?? Ngn).Value;

    public static JournalLineDraft Line(
        Account account,
        DebitOrCredit direction,
        decimal amount,
        Currency? currency = null,
        ChartOfAccount? glAccount = null) =>
        new(new JournalLineId(Guid.CreateVersion7()),
            account,
            glAccount ?? PostableLeaf(),
            direction,
            Amount(amount, currency));

    /// <summary>A balanced two-line entry: the shape everything else deviates from.</summary>
    public static Result<JournalEntry> Entry(params JournalLineDraft[] lines) =>
        JournalEntry.Create(
            new JournalId(Guid.CreateVersion7()),
            transactionDate: new DateOnly(2026, 3, 1),
            valueDate: new DateOnly(2026, 3, 1),
            postingDate: new DateOnly(2026, 3, 1),
            description: "test entry",
            sourceSystem: "transfers",
            sourceReference: "REF-1",
            createdBy: Guid.CreateVersion7(),
            lines: lines);

    public static Result<JournalEntry> BalancedPair(Currency? currency = null)
    {
        Currency ccy = currency ?? Ngn;
        return Entry(
            Line(ActiveAccount(ccy, "0000000001"), DebitOrCredit.Debit, 100m, ccy),
            Line(ActiveAccount(ccy, "0000000002"), DebitOrCredit.Credit, 100m, ccy));
    }
}
