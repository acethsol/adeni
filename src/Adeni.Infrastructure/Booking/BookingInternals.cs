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
            PricingTypeMapping.ToApiValue(entity.PricingType),
            entity.DurationMinutes,
            entity.IsActive,
            entity.CategorySlug,
            entity.CatalogServiceId,
            BookingDeliveryTypeMapping.ToApiValue(entity.BookingDeliveryType),
            entity.MenuGroupId,
            entity.SortOrder,
            entity.IsAddOn);
}

internal static class BookingDeliveryTypeMapping
{
    public static string ToApiValue(BookingDeliveryType value) =>
        value switch
        {
            BookingDeliveryType.Class => "class",
            BookingDeliveryType.Session => "session",
            BookingDeliveryType.Experience => "experience",
            BookingDeliveryType.MobileAppointment => "mobile_appointment",
            _ => "appointment",
        };

    public static BookingDeliveryType FromApiValue(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "class" => BookingDeliveryType.Class,
            "session" => BookingDeliveryType.Session,
            "experience" => BookingDeliveryType.Experience,
            "mobile_appointment" => BookingDeliveryType.MobileAppointment,
            _ => BookingDeliveryType.Appointment,
        };
}

internal static class PricingTypeMapping
{
    public static string ToApiValue(PricingType pricingType) =>
        pricingType switch
        {
            PricingType.Fixed => "fixed",
            PricingType.QuoteRequest => "quote_request",
            PricingType.Hourly => "hourly",
            _ => "fixed",
        };

    public static PricingType FromApiValue(string value) =>
        value.ToLowerInvariant() switch
        {
            "fixed" => PricingType.Fixed,
            "quote_request" => PricingType.QuoteRequest,
            "hourly" => PricingType.Hourly,
            _ => PricingType.Fixed,
        };
}

internal static class BookingMapper
{
    public static BookingResponse ToResponse(
        BookingRecord entity,
        string serviceName,
        string? staffDisplayName = null,
        string? currency = null) =>
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
            entity.CreatedAt,
            entity.StaffMemberId,
            staffDisplayName,
            entity.GuestCount,
            MapLines(entity, currency),
            MapGuests(entity),
            SumLinePrices(entity),
            currency);

    public static CustomerBookingResponse ToCustomerResponse(
        BookingRecord entity,
        string serviceName,
        string businessName,
        string businessSlug,
        bool canReview = false,
        bool hasReview = false,
        byte? reviewRating = null,
        string? staffDisplayName = null,
        string? currency = null) =>
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
            reviewRating,
            entity.StaffMemberId,
            staffDisplayName,
            entity.GuestCount,
            MapLines(entity, currency),
            MapGuests(entity),
            SumLinePrices(entity),
            currency);

    private static IReadOnlyList<BookingLineResponse>? MapLines(BookingRecord entity, string? currency)
    {
        if (entity.Lines is null || entity.Lines.Count == 0)
        {
            return null;
        }

        var cur = currency ?? "NGN";
        return entity.Lines
            .OrderBy(x => x.SortOrder)
            .Select(x => new BookingLineResponse(
                x.ServiceOfferingId,
                x.ServiceName,
                x.PriceAmount,
                cur,
                x.DurationMinutes,
                x.SortOrder,
                x.IsAddOn))
            .ToArray();
    }

    private static IReadOnlyList<BookingGuestResponse>? MapGuests(BookingRecord entity)
    {
        if (entity.Guests is null || entity.Guests.Count == 0)
        {
            return null;
        }

        return entity.Guests
            .OrderBy(x => x.SortOrder)
            .Select(x => new BookingGuestResponse(x.DisplayName, x.SortOrder))
            .ToArray();
    }

    private static decimal? SumLinePrices(BookingRecord entity)
    {
        if (entity.Lines is null || entity.Lines.Count == 0)
        {
            return null;
        }

        return entity.Lines.Sum(x => x.PriceAmount) * Math.Max(1, entity.GuestCount);
    }
}

public static class BookingConflictChecker
{
    public static bool OverlapsExisting(
        IEnumerable<BookingRecord> existing,
        DateTimeOffset startAt,
        DateTimeOffset endAt) =>
        existing.Any(booking =>
            booking.Status is BookingStatus.Pending or BookingStatus.Confirmed
            && booking.StartAt < endAt
            && booking.EndAt > startAt);

    /// <summary>
    /// When the tenant has active staff for the service, capacity is per roster.
    /// Assigned bookings occupy that staff; unassigned bookings occupy anonymous capacity.
    /// With no staff roster, falls back to a single shared calendar.
    /// </summary>
    public static bool IsStaffSlotAvailable(
        IReadOnlyList<BookingRecord> existing,
        IReadOnlyList<Guid> eligibleStaffIds,
        DateTimeOffset startAt,
        DateTimeOffset endAt,
        Guid? requestedStaffMemberId)
    {
        if (eligibleStaffIds.Count == 0)
        {
            return !OverlapsExisting(existing, startAt, endAt);
        }

        var overlapping = existing
            .Where(booking =>
                booking.Status is BookingStatus.Pending or BookingStatus.Confirmed
                && booking.StartAt < endAt
                && booking.EndAt > startAt)
            .ToArray();

        var busyStaffIds = overlapping
            .Where(b => b.StaffMemberId is Guid id && eligibleStaffIds.Contains(id))
            .Select(b => b.StaffMemberId!.Value)
            .Distinct()
            .ToHashSet();

        var nullOverlapping = overlapping.Count(b => b.StaffMemberId is null);
        var used = busyStaffIds.Count + nullOverlapping;
        var capacity = eligibleStaffIds.Count;

        if (used >= capacity)
        {
            return false;
        }

        if (requestedStaffMemberId is Guid staffId)
        {
            if (!eligibleStaffIds.Contains(staffId))
            {
                return false;
            }

            return !busyStaffIds.Contains(staffId);
        }

        return true;
    }
}
