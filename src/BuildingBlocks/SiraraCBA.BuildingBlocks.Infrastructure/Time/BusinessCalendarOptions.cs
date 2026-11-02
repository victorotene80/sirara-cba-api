using System.ComponentModel.DataAnnotations;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Time;

/// <summary>
/// The institution's business time zone — the zone a posting date belongs to.
/// </summary>
/// <remarks>
/// Configuration rather than a constant: the same codebase runs for institutions
/// in different zones, and end-of-day is a business fact, not a UTC one (ER-9).
/// </remarks>
public sealed class BusinessCalendarOptions
{
    public const string SectionName = "BusinessCalendar";

    /// <summary>An IANA or Windows time zone id, e.g. <c>Africa/Lagos</c>.</summary>
    [Required(AllowEmptyStrings = false)]
    public string TimeZoneId { get; init; } = string.Empty;
}
