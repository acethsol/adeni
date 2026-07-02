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
                "Time zone {TimeZoneId} was not found on this host. Falling back to UTC.",
                timeZoneId);

            return TimeZoneInfo.Utc;
        }
    }
}

internal sealed class SchedulingTimeZone(TimeZoneInfo timeZone, string timeZoneId) : ISchedulingTimeZone
{
    public string TimeZoneId { get; } = timeZoneId;

    public static SchedulingTimeZone FromId(string timeZoneId, ILogger? logger = null)
    {
        var resolved = TimeZoneResolver.Resolve(timeZoneId, logger);
        return new SchedulingTimeZone(resolved, timeZoneId.Trim());
    }

    public DateTimeOffset ToLocal(DateTimeOffset instant) =>
        TimeZoneInfo.ConvertTime(instant, timeZone);

    public DateTimeOffset ToUtc(DateTime date, TimeOnly time)
    {
        var local = DateTime.SpecifyKind(date.Date + time.ToTimeSpan(), DateTimeKind.Unspecified);
        return new DateTimeOffset(local, timeZone.GetUtcOffset(local));
    }
}
