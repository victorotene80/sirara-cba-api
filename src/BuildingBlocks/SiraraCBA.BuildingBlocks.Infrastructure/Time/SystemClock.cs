using Microsoft.Extensions.Options;
using SiraraCBA.BuildingBlocks.Application.Abstractions;

namespace SiraraCBA.BuildingBlocks.Infrastructure.Time;

/// <summary>
/// The one place in the system permitted to read the machine clock (ER-7).
/// </summary>
public sealed class SystemClock : IClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _businessTimeZone;

    public SystemClock(TimeProvider timeProvider, IOptions<BusinessCalendarOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _timeProvider = timeProvider;

        // A misconfigured time zone must fail at boot, not at the first posting
        // (coding-standards.md, Configuration).
        _businessTimeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateTimeOffset UtcNow => _timeProvider.GetUtcNow();

    public DateOnly BusinessToday =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(UtcNow, _businessTimeZone).DateTime);
}
