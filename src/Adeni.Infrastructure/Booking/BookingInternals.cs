namespace Adeni.Infrastructure.Booking;

using Adeni.Application.Booking;
using Adeni.Domain.Booking;
using Adeni.Domain.Tenancy;

internal static class SlotGenerator
{
    public static IEnumerable<DateTimeOffset> GenerateSlotStarts(
        ISchedulingTimeZone schedulingTimeZone,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        DateTimeOffset rangeStart,
        DateTimeOffset rangeEnd,
        int durationMinutes)
    {
        if (durationMinutes <= 0 || rangeEnd <= rangeStart)
        {
            yield break;
        }

        var localStartDate = schedulingTimeZone.ToLocal(rangeStart).Date;
        var localEndDate = schedulingTimeZone.ToLocal(rangeEnd).Date;

        for (var day = localStartDate; day <= localEndDate; day = day.AddDays(1))
        {
            var dayRules = rules.Where(r => r.DayOfWeek == day.DayOfWeek).ToArray();
            foreach (var rule in dayRules)
            {
                if (rule.CloseTime <= rule.OpenTime)
                {
                    continue;
                }

                var cursor = rule.OpenTime;
                while (cursor.AddMinutes(durationMinutes) <= rule.CloseTime)
                {
                    var slotStart = schedulingTimeZone.ToUtc(day, cursor);
                    var slotEnd = slotStart.AddMinutes(durationMinutes);

                    if (slotEnd <= rangeStart || slotStart >= rangeEnd)
                    {
                        cursor = cursor.AddMinutes(durationMinutes);
                        continue;
                    }

                    if (slotStart >= rangeStart && slotEnd <= rangeEnd)
                    {
                        yield return slotStart;
                    }

                    cursor = cursor.AddMinutes(durationMinutes);
                }
            }
        }
    }

    public static bool FitsWeeklyRules(
        ISchedulingTimeZone schedulingTimeZone,
        IReadOnlyList<WeeklyAvailabilityRule> rules,
        DateTimeOffset startAt,
        int durationMinutes)
    {
        var endAt = startAt.AddMinutes(durationMinutes);
        var localStart = schedulingTimeZone.ToLocal(startAt);
        var localEnd = schedulingTimeZone.ToLocal(endAt);

        if (localStart.Date != localEnd.Date)
        {
            return false;
        }

        return rules.Any(rule =>
            rule.DayOfWeek == localStart.DayOfWeek
            && localStart.TimeOfDay >= rule.OpenTime.ToTimeSpan()
            && localEnd.TimeOfDay <= rule.CloseTime.ToTimeSpan());
    }
}

internal static class ServiceOfferingMapper
{
    public static ServiceOfferingResponse ToResponse(ServiceOffering entity) =>
        new(
            entity.Id,
            entity.Name,
            entity.Description,
            entity.PriceAmount,
            entity.Currency,
            entity.DurationMinutes,
            entity.IsActive);
}

internal static class BookingMapper
{
    public static BookingResponse ToResponse(BookingRecord entity, string serviceName) =>
        new(
            entity.Id,
            entity.TenantId,
            entity.ServiceOfferingId,
            serviceName,
            entity.CustomerId,
            entity.StartAt,
            entity.EndAt,
            entity.Status,
            entity.CustomerNotes,
            entity.CreatedAt);

    public static CustomerBookingResponse ToCustomerResponse(
        BookingRecord entity,
        string serviceName,
        string businessName,
        string businessSlug,
        bool canReview = false,
        bool hasReview = false,
        byte? reviewRating = null) =>
        new(
            entity.Id,
            entity.TenantId,
            businessName,
            businessSlug,
            entity.ServiceOfferingId,
            serviceName,
            entity.StartAt,
            entity.EndAt,
            entity.Status,
            entity.CustomerNotes,
            entity.CreatedAt,
            canReview,
            hasReview,
            reviewRating);
}

internal static class BookingConflictChecker
{
    public static bool OverlapsExisting(
        IEnumerable<BookingRecord> existing,
        DateTimeOffset startAt,
        DateTimeOffset endAt) =>
        existing.Any(booking =>
            booking.Status is BookingStatus.Pending or BookingStatus.Confirmed
            && booking.StartAt < endAt
            && booking.EndAt > startAt);
}
