namespace Adeni.Application.Dashboard;

public static class DashboardLayoutDefaults
{
    public static TenantDashboardLayoutDto ForPractitionerView(bool practitionerView) =>
        practitionerView ? Practitioner : Owner;

    public static TenantDashboardLayoutDto Owner { get; } = new(
        1,
        "7d",
        [
            new("plan_usage", 12),
            new("kpis_row", 12),
            new("week_appointments", 6),
            new("today_schedule", 6),
            new("booking_mix", 12),
            new("service_mix", 12),
            new("quick_actions", 12),
        ]);

    public static TenantDashboardLayoutDto Practitioner { get; } = new(
        1,
        "today",
        [
            new("kpis_row", 12),
            new("today_schedule", 12),
            new("week_appointments", 12),
            new("quick_actions", 12),
        ]);
}
