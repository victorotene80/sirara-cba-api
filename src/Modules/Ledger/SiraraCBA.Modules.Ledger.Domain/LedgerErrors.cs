namespace SiraraCBA.Modules.Ledger.Domain;

/// <summary>
/// Every failure this module can return, named once. Codes are stable and safe to
/// branch on; messages are for humans (SharedKernel <c>Error</c>).
/// </summary>
public static class LedgerErrors
{
    public static Error Unbalanced(string detail) =>
        Error.Validation("ledger.entry.unbalanced", detail);

    public static Error MixedCurrency(string detail) =>
        Error.Validation("ledger.entry.mixed_currency", detail);

    public static Error TooFewLines(string detail) =>
        Error.Validation("ledger.entry.too_few_lines", detail);

    public static Error AmountNotPositive(string detail) =>
        Error.Validation("ledger.line.amount_not_positive", detail);

    public static Error AccountNotPostable(string detail) =>
        Error.Validation("ledger.line.account_not_postable", detail);

    public static Error AccountNotActive(string detail) =>
        Error.Conflict("ledger.line.account_not_active", detail);

    public static Error ChartNodeInvalid(string detail) =>
        Error.Validation("ledger.chart_node.invalid", detail);

    public static Error AccountInvalid(string detail) =>
        Error.Validation("ledger.account.invalid", detail);
}
