namespace Adeni.Application.Catalog;

using Adeni.Domain.Tenancy;

public static class BusinessTypeMapping
{
    public static string ToApiValue(BusinessType businessType) =>
        businessType switch
        {
            BusinessType.QuoteRequest => "quote_request",
            _ => "scheduled_appointment",
        };

    public static BusinessType FromApiValue(string? value) =>
        string.Equals(value, "quote_request", StringComparison.OrdinalIgnoreCase)
            ? BusinessType.QuoteRequest
            : BusinessType.ScheduledAppointment;
}
