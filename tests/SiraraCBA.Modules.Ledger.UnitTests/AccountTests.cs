namespace SiraraCBA.Modules.Ledger.UnitTests;

public sealed class AccountTests
{
    [Fact]
    public void CanAcceptPosting_WhenActive_Succeeds()
    {
        LedgerMother.AccountWith(AccountStatus.Active).CanAcceptPosting().IsSuccess.ShouldBeTrue();
    }

    [Theory]
    [InlineData(AccountStatus.Dormant)]
    [InlineData(AccountStatus.Closed)]
    public void CanAcceptPosting_WhenNotActive_FailsWithAResult(AccountStatus status)
    {
        // A closed or dormant account rejects posting with a Result error, not an
        // exception (ledger-spec.md §3). Being closed is expected, not exceptional.
        Result result = LedgerMother.AccountWith(status).CanAcceptPosting();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("ledger.line.account_not_active");
    }

    [Fact]
    public void Create_HoldsCustomerIdAsAnOpaqueValue()
    {
        // Opaque, no foreign key to any future Customers module (ER-2, O-4).
        Guid customerId = Guid.CreateVersion7();

        Result<Account> result = Account.Create(
            new AccountId(Guid.CreateVersion7()),
            new AccountNumber("0000000009"),
            customerId,
            "savings",
            new GlCode("10101"),
            LedgerMother.Ngn,
            AccountStatus.Active,
            new DateOnly(2026, 1, 1));

        result.IsSuccess.ShouldBeTrue();
        result.Value.CustomerId.ShouldBe(customerId);
    }

    [Fact]
    public void Create_WithNoCustomer_Succeeds_ForAnInternalAccount()
    {
        LedgerMother.ActiveAccount().CustomerId.ShouldBeNull();
    }
}

public sealed class ChartOfAccountTests
{
    [Fact]
    public void Create_ALeaf_IsPostable()
    {
        LedgerMother.PostableLeaf().IsPostable.ShouldBeTrue();
    }

    [Fact]
    public void Create_ARollUpNode_IsNotPostable()
    {
        LedgerMother.RollUpNode().IsPostable.ShouldBeFalse();
    }

    [Fact]
    public void Create_WhenARootNodeIsMarkedPostable_IsRefused()
    {
        // Renamed from ...WhenAPostableNodeIsGivenAChild: the name described a case
        // the data did not set up. A root is never a leaf, and posting to a non-leaf
        // would let the same money be counted at two levels of the hierarchy.
        Result<ChartOfAccount> result = ChartOfAccount.Create(
            new GlCode("101"),
            "Deposits",
            AccountType.Liability,
            NormalBalance.Credit,
            parentGlCode: null,
            level: 0,
            isPostable: true);

        result.IsFailure.ShouldBeTrue();
    }
}
