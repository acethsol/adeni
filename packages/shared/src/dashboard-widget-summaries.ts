import type { DashboardWidgetId } from "./dashboard-layout";

/** Picker tooltips — kept separate so widget definitions stay minimal for API/normalizer parity. */
export const DASHBOARD_WIDGET_SUMMARIES: Record<DashboardWidgetId, string> = {
  plan_usage: "Your SaaS tier, booking limit, and usage this month.",
  kpis_row: "Headline KPIs for the dashboard date range you selected.",
  today_schedule: "Today’s appointments on the calendar, in visit order.",
  pending_inbox: "Booking requests waiting for you to confirm or decline.",
  week_appointments: "Chart of visits across the current dashboard range.",
  booking_mix: "How bookings split by status (scheduled, completed, cancelled, etc.).",
  collected_revenue: "Deposits collected online in this period (not cash in the drawer).",
  booked_value: "List value of scheduled and in-progress visits in this range.",
  service_mix: "Which services drive the most bookings in this range.",
  staff_leaderboard: "Completed bookings by team member for the selected range.",
  rating_summary: "Average rating and how many reviews you have.",
  queue_now: "Guests waiting in the walk-in queue right now.",
  messages_unread: "Unread client messages plus a quick inbox preview.",
  quote_pipeline: "Quote requests still waiting for a price or reply.",
  verification_status: "Discovery verification status and what to do next.",
  quick_actions: "One-tap links to common portal pages.",
};

export function dashboardWidgetSummary(widgetId: DashboardWidgetId): string {
  return DASHBOARD_WIDGET_SUMMARIES[widgetId] ?? "";
}
