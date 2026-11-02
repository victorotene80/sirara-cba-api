namespace SiraraCBA.Modules.Ledger.UnitTests;

/// <summary>
/// The invariants are the whole point of the module (ledger-spec.md §1). Each is
/// written as an attempt that is *refused*, because "an unbalanced entry must be
/// unrepresentable" is a claim about what cannot be constructed, not about what is
/// reported later.
/// </summary>
public sealed class JournalEntryTests
{
    [Fact]
    public void Create_WhenDebitsDoNotEqualCredits_IsRefused()
    {
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(), DebitOrCredit.Debit, 100m),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000002"), DebitOrCredit.Credit, 99m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.entry.unbalanced");
    }

    [Fact]
    public void Create_WhenLinesSpanTwoCurrencies_IsRefused()
    {
        // A cross-currency event is two linked entries sharing a source_reference,
        // never one mixed entry (I-2, handoff §8.1).
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(LedgerMother.Ngn), DebitOrCredit.Debit, 100m, LedgerMother.Ngn),
            LedgerMother.Line(LedgerMother.ActiveAccount(LedgerMother.Jpy, "0000000002"), DebitOrCredit.Credit, 100m, LedgerMother.Jpy));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.entry.mixed_currency");
    }

    [Fact]
    public void Create_WhenLinesSpanTwoCurrencies_FailsAsAResult_NotAnException()
    {
        // Money's operators throw on a currency mismatch. A mixed-currency entry is
        // an *expected* business outcome, so the factory must decide I-2 before it
        // does any arithmetic — otherwise the invariant arrives as an exception and
        // has to be converted back across a layer boundary (architecture.md §3).
        Should.NotThrow(() => LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(LedgerMother.Ngn), DebitOrCredit.Debit, 100m, LedgerMother.Ngn),
            LedgerMother.Line(LedgerMother.ActiveAccount(LedgerMother.Jpy, "0000000002"), DebitOrCredit.Credit, 100m, LedgerMother.Jpy)));
    }

    [Fact]
    public void Create_WithASingleLine_IsRefused()
    {
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(), DebitOrCredit.Debit, 100m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.entry.too_few_lines");
    }

    [Fact]
    public void Create_WithNoLines_IsRefused()
    {
        Result<JournalEntry> result = LedgerMother.Entry();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.entry.too_few_lines");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100.50)]
    public void Create_WhenALineAmountIsNotStrictlyPositive_IsRefused(decimal amount)
    {
        // A "negative debit" is a credit. Allowing both spellings makes every
        // downstream aggregate ambiguous (I-8).
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(), DebitOrCredit.Debit, amount),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000002"), DebitOrCredit.Credit, amount));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.line.amount_not_positive");
    }

    // C5 — "a line whose amount exceeds the currency's minor units is rejected" — has
    // no test here on purpose. Money.Create returns a Result, so an over-precise Money
    // cannot be constructed at all, so a JournalLineDraft cannot carry one. The
    // invariant holds by construction rather than by a check, which is stronger; and
    // MoneyTests already proves it for NGN, JPY and KWD. A test here would be testing
    // the SharedKernel (coding-standards.md, Testing: do not test the framework) and
    // would have passed before this slice existed.

    [Fact]
    public void Create_WhenAnAccountIsNotPostable_IsRefused()
    {
        // Only leaves are postable; posting to a roll-up node makes the hierarchy's
        // totals meaningless (ledger-spec.md §2).
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(), DebitOrCredit.Debit, 100m,
                glAccount: LedgerMother.RollUpNode()),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000002"), DebitOrCredit.Credit, 100m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.line.account_not_postable");
    }

    [Theory]
    [InlineData(AccountStatus.Closed)]
    [InlineData(AccountStatus.Dormant)]
    public void Create_WhenAnAccountCannotAcceptPosting_IsRefused(AccountStatus status)
    {
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.AccountWith(status), DebitOrCredit.Debit, 100m),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000002"), DebitOrCredit.Credit, 100m));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.line.account_not_active");
    }

    [Fact]
    public void Create_WithABalancedPair_Succeeds()
    {
        Result<JournalEntry> result = LedgerMother.BalancedPair();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Lines.Count.ShouldBe(2);
        result.Value.Currency.ShouldBe(LedgerMother.Ngn);
        result.Value.Status.ShouldBe(JournalStatus.Pending);
    }

    [Fact]
    public void Create_WithFourLines_PrincipalPlusFeePlusMargin_Succeeds()
    {
        // D-11: the contract must not assume two lines. The cross-border build posts
        // principal, fee and margin against this shape unchanged.
        Result<JournalEntry> result = LedgerMother.Entry(
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000001"), DebitOrCredit.Debit, 1_000m),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000002"), DebitOrCredit.Credit, 950m),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000003"), DebitOrCredit.Credit, 30m),
            LedgerMother.Line(LedgerMother.ActiveAccount(number: "0000000004"), DebitOrCredit.Credit, 20m));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Lines.Count.ShouldBe(4);
    }

    [Fact]
    public void Create_NumbersItsLines_InTheOrderTheyWereGiven()
    {
        // sequence_number preserves line order for audit and display (ledger-spec.md §2).
        Result<JournalEntry> result = LedgerMother.BalancedPair();

        result.IsSuccess.ShouldBeTrue();
        result.Value.Lines.Select(line => line.SequenceNumber).ShouldBe([1, 2]);
    }
}
