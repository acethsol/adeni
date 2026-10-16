namespace Adeni.Application.Dashboard;

using Adeni.Application.Subscriptions;

public static class DashboardLayoutNormalizer
{
    private static readonly HashSet<string> WidgetIds =
    [
        "plan_usage",
        "kpis_row",
        "today_schedule",
        "pending_inbox",
        "week_appointments",
        "booking_mix",
        "collected_revenue",
        "booked_value",
        "service_mix",
        "rating_summary",
        "queue_now",
        "messages_unread",
        "quote_pipeline",
        "staff_leaderboard",
        "verification_status",
        "quick_actions",
    ];

    private static readonly HashSet<string> AnalyticsWidgets =
    [
        "collected_revenue",
        "booked_value",
        "service_mix",
        "staff_leaderboard",
    ];

    private static readonly HashSet<string> MessagingWidgets = ["messages_unread"];

    public static TenantDashboardLayoutDto Normalize(
        TenantDashboardLayoutDto? raw,
        TenantDashboardLayoutDto fallback,
        TenantEntitlements entitlements,
        bool practitionerView)
    {
        var source = raw ?? fallback;
        if (source.Version != 1 || source.Widgets.Count == 0)
        {
            source = fallback;
        }

        var range = NormalizeRange(source.Range, fallback.Range);
        var widgets = new List<TenantDashboardLayoutWidgetDto>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var widget in source.Widgets)
        {
            if (string.IsNullOrWhiteSpace(widget.Id) || !WidgetIds.Contains(widget.Id))
            {
                continue;
            }

            if (seen.Contains(widget.Id))
            {
                continue;
            }

            if (!IsWidgetAllowed(widget.Id, entitlements, practitionerView))
            {
                continue;
            }

            seen.Add(widget.Id);
            widgets.Add(new TenantDashboardLayoutWidgetDto(widget.Id, NormalizeWidth(widget.W)));
        }

        if (widgets.Count == 0)
        {
            return Normalize(fallback, fallback, entitlements, practitionerView);
        }

        return new TenantDashboardLayoutDto(1, range, widgets);
    }

    public static bool IsWidgetAllowed(string widgetId, TenantEntitlements entitlements, bool practitionerView)
    {
        if (AnalyticsWidgets.Contains(widgetId) && !entitlements.Analytics)
        {
            return false;
        }

        if (MessagingWidgets.Contains(widgetId) && !entitlements.Messaging)
        {
            return false;
        }

        if (practitionerView && widgetId is
            "plan_usage"
            or "collected_revenue"
            or "booked_value"
            or "service_mix"
            or "staff_leaderboard"
            or "verification_status")
        {
            return false;
        }

        return true;
    }

    private static string NormalizeRange(string? range, string fallback) =>
        range is "today" or "7d" or "30d" or "mtd" ? range : fallback;

    private static int NormalizeWidth(int width) =>
        width is 4 or 6 or 8 ? width : 12;
}
