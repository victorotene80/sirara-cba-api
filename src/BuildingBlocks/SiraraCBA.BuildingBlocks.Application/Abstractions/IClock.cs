namespace SiraraCBA.BuildingBlocks.Application.Abstractions;

/// <summary>
/// The only source of time in Domain and Application code.
/// </summary>
/// <remarks>
/// <c>DateTime.UtcNow</c> makes a value date, an expiry, or a rate lock impossible
/// to test without waiting for real time to pass, and impossible to reproduce
/// afterwards. ER-7 forbids it outside infrastructure.
/// </remarks>
public interface IClock
{
    /// <summary>The current instant, always with an explicit offset.</summary>
    public DateTimeOffset UtcNow { get; }

    /// <summary>Today's date in the institution's configured business time zone.</summary>
    /// <remarks>
    /// Deliberately separate from <see cref="UtcNow"/>: a posting date is a
    /// business day in the bank's own zone, not a UTC calendar day. Deriving one
    /// from the other at each call site is how entries land in the wrong day.
    /// </remarks>
    public DateOnly BusinessToday { get; }
}
