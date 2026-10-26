namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Microsoft.Extensions.Logging;

internal static class TimeZoneResolver
{
    public static TimeZoneInfo Resolve(string timeZoneId, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
        {
            return TimeZoneInfo.Utc;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            logger?.LogWarning(
                ex,
                "Time zone {TimeZoneId} was not found on this host. Scheduling slots will not be offered.",
                timeZoneId);

            return TimeZoneInfo.Utc;
        }
    }
}

internal sealed class SchedulingTimeZone(TimeZoneInfo timeZone, string timeZoneId, bool valid = true) : ISchedulingTimeZone
{
    public string TimeZoneId { get; } = timeZoneId;
    public bool IsConfiguredZoneValid => valid;

    public static SchedulingTimeZone FromId(string timeZoneId, ILogger? logger = null)
    {
        var resolved = TimeZoneResolver.Resolve(timeZoneId, logger);
        try
        {
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId.Trim());
            return new SchedulingTimeZone(resolved, timeZoneId.Trim());
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { return new SchedulingTimeZone(resolved, timeZoneId.Trim(), false); }
    }

    public DateTimeOffset ToLocal(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, timeZone);

    public bool IsValidLocalTime(DateTime date, TimeOnly time) => valid && !timeZone.IsInvalidTime(DateTime.SpecifyKind(date.Date + time.ToTimeSpan(), DateTimeKind.Unspecified));

    public DateTimeOffset ToUtc(DateTime date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);
        if (!valid || timeZone.IsInvalidTime(local)) throw new ArgumentException("Invalid local schedule time.");
        // Use standard time once at the fall-back fold; never manufacture a spring-forward instant.
        var offset = timeZone.IsAmbiguousTime(local) ? timeZone.GetAmbiguousTimeOffsets(local).Min() : timeZone.GetUtcOffset(local);
        var withOffset = new DateTimeOffset(local, offset);
        // Persist booking instants as UTC (offset 0) for SQL Server datetimeoffset consistency.
        return withOffset.ToUniversalTime();
    }
}
