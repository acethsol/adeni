import type { DashboardWidgetId } from "../dashboard-layout";
import { dashboardWidgetTitle } from "../dashboard-layout";
import { dashboardWidgetSummary } from "../dashboard-widget-summaries";
import { t } from "./index";
import { defaultLocale } from "./types";

function resolve(locale: string | undefined): string {
  return locale ?? defaultLocale;
}

export function formatDashboardWidgetTitle(
  locale: string | undefined,
  widgetId: DashboardWidgetId,
): string {
  const loc = resolve(locale);
  const key = `portal.dashboard.widgetTitle.${widgetId}`;
  const translated = t(loc, key);
  if (translated !== key) {
    return translated;
  }
  return dashboardWidgetTitle(widgetId);
}

export function formatDashboardWidgetSummary(
  locale: string | undefined,
  widgetId: DashboardWidgetId,
): string {
  const loc = resolve(locale);
  const key = `portal.dashboard.widgetSummary.${widgetId}`;
  const translated = t(loc, key);
  if (translated !== key) {
    return translated;
  }
  return dashboardWidgetSummary(widgetId);
}
