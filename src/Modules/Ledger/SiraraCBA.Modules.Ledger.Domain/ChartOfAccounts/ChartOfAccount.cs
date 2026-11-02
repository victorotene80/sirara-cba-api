using SiraraCBA.Modules.Ledger.Domain.Identifiers;

namespace SiraraCBA.Modules.Ledger.Domain.ChartOfAccounts;

/// <summary>Which side of the books an account belongs to (ledger-spec.md §2).</summary>
public enum AccountType
{
    Asset,
    Liability,
    Equity,
    Income,
    Expense,
}

/// <summary>The side that increases an account.</summary>
public enum NormalBalance
{
    Debit,
    Credit,
}

/// <summary>
/// A node in the chart of accounts. Only leaves are postable — posting to a
/// roll-up node would let the same money be counted at two levels of the hierarchy.
/// </summary>
public sealed class ChartOfAccount : AggregateRoot<GlCode>
{
    private ChartOfAccount(
        GlCode id,
        string name,
        AccountType type,
        NormalBalance normalBalance,
        GlCode? parentGlCode,
        int level,
        bool isPostable)
        : base(id)
    {
        Name = name;
        Type = type;
        NormalBalance = normalBalance;
        ParentGlCode = parentGlCode;
        Level = level;
        IsPostable = isPostable;
    }

    public string Name { get; }

    public AccountType Type { get; }

    public NormalBalance NormalBalance { get; }

    public GlCode? ParentGlCode { get; }

    public int Level { get; }

    public bool IsPostable { get; }

    /// <summary>Builds a chart node, or explains why it cannot exist.</summary>
    public static Result<ChartOfAccount> Create(
        GlCode glCode,
        string name,
        AccountType type,
        NormalBalance normalBalance,
        GlCode? parentGlCode,
        int level,
        bool isPostable)
    {
        if (string.IsNullOrWhiteSpace(glCode.Value))
        {
            return Result.Failure<ChartOfAccount>(
                LedgerErrors.ChartNodeInvalid("A chart node needs a GL code."));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            return Result.Failure<ChartOfAccount>(
                LedgerErrors.ChartNodeInvalid($"GL code '{glCode.Value}' has no name."));
        }

        if (level < RootLevel)
        {
            return Result.Failure<ChartOfAccount>(
                LedgerErrors.ChartNodeInvalid($"GL code '{glCode.Value}' has a negative level."));
        }

        bool isRoot = parentGlCode is null;

        // The hierarchy is described twice — by the parent link and by the level —
        // and the two must agree, or a roll-up total depends on which one is read.
        if (isRoot != (level == RootLevel))
        {
            return Result.Failure<ChartOfAccount>(LedgerErrors.ChartNodeInvalid(
                $"GL code '{glCode.Value}' has level {level} but "
                + (isRoot ? "no parent." : $"parent '{parentGlCode!.Value.Value}'.")));
        }

        // A root is never a leaf, so it can never be postable.
        if (isPostable && isRoot)
        {
            return Result.Failure<ChartOfAccount>(LedgerErrors.ChartNodeInvalid(
                $"GL code '{glCode.Value}' is a root and cannot be postable. "
                + "Only leaves are postable."));
        }

        return new ChartOfAccount(glCode, name, type, normalBalance, parentGlCode, level, isPostable);
    }

    /// <summary>The level of a node with no parent. Structural, not a business threshold.</summary>
    private const int RootLevel = 0;
}
