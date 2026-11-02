using System.ComponentModel.DataAnnotations;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Monetary;

/// <summary>
/// The currencies this deployment handles, and the minor units of each.
/// </summary>
/// <remarks>
/// Data, not code. Adding a currency is a configuration change, and no part of the
/// system ever assumes two decimal places (ER-9).
/// </remarks>
public sealed class CurrencyOptions
{
    public const string SectionName = "Currencies";

    [Required]
    [MinLength(1)]
    public IReadOnlyList<CurrencyDefinition> Supported { get; init; } = [];

    public sealed class CurrencyDefinition
    {
        [Required(AllowEmptyStrings = false)]
        [StringLength(3, MinimumLength = 3)]
        public string Code { get; init; } = string.Empty;

        [Range(0, 4)]
        public int MinorUnits { get; init; }
    }
}
