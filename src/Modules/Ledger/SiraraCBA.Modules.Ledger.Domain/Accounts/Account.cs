using SiraraCBA.Modules.Ledger.Domain.Identifiers;

namespace SiraraCBA.Modules.Ledger.Domain.Accounts;

/// <summary>An account's lifecycle state (ledger-spec.md §2).</summary>
public enum AccountStatus
{
    Active,
    Dormant,
    Closed,
}

/// <summary>
/// A postable account. Its status decides whether it may take a line, and it says
/// so with a <see cref="Result"/> — a dormant account is an expected outcome, not
/// an exceptional one.
/// </summary>
public sealed class Account : AggregateRoot<AccountId>
{
    private Account(
        AccountId id,
        AccountNumber number,
        Guid? customerId,
        string productType,
        GlCode glCode,
        Currency currency,
        AccountStatus status,
        DateOnly openedAt)
        : base(id)
    {
        Number = number;
        CustomerId = customerId;
        ProductType = productType;
        GlCode = glCode;
        Currency = currency;
        Status = status;
        OpenedAt = openedAt;
    }

    public AccountNumber Number { get; }

    /// <summary>Opaque. No foreign key to any Customers module (ER-2, O-4).</summary>
    public Guid? CustomerId { get; }

    public string ProductType { get; }

    public GlCode GlCode { get; }

    public Currency Currency { get; }

    public AccountStatus Status { get; }

    public DateOnly OpenedAt { get; }

    /// <summary>Builds an account, or explains why it cannot exist.</summary>
    public static Result<Account> Create(
        AccountId id,
        AccountNumber number,
        Guid? customerId,
        string productType,
        GlCode glCode,
        Currency currency,
        AccountStatus status,
        DateOnly openedAt)
    {
        if (string.IsNullOrWhiteSpace(number.Value))
        {
            return Result.Failure<Account>(LedgerErrors.AccountInvalid("An account needs a number."));
        }

        if (string.IsNullOrWhiteSpace(productType))
        {
            return Result.Failure<Account>(
                LedgerErrors.AccountInvalid($"Account '{number.Value}' has no product type."));
        }

        if (string.IsNullOrWhiteSpace(glCode.Value))
        {
            return Result.Failure<Account>(
                LedgerErrors.AccountInvalid($"Account '{number.Value}' has no GL code."));
        }

        ArgumentNullException.ThrowIfNull(currency);

        return new Account(id, number, customerId, productType, glCode, currency, status, openedAt);
    }

    /// <summary>Whether this account may take a journal line right now.</summary>
    public Result CanAcceptPosting() =>
        Status is AccountStatus.Active
            ? Result.Success()
            : Result.Failure(LedgerErrors.AccountNotActive(
                $"Account '{Number.Value}' is {Status.ToString().ToLowerInvariant()}."));
}
