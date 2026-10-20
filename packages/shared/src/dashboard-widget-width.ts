/** Column spans on a 12-column grid: quarter (3), half (6), three-quarter (9), full (12). */
export type DashboardWidgetWidth = 3 | 6 | 9 | 12;

export const DASHBOARD_WIDGET_WIDTHS: readonly DashboardWidgetWidth[] = [3, 6, 9, 12];

export type DashboardWidgetWidthOption = {
  w: DashboardWidgetWidth;
  label: string;
  title: string;
};

export const DASHBOARD_WIDGET_WIDTH_OPTIONS: readonly DashboardWidgetWidthOption[] = [
  { w: 3, label: "S", title: "Quarter width (25% of row)" },
  { w: 6, label: "M", title: "Half width (50% of row)" },
  { w: 9, label: "L", title: "Large width (75% of row)" },
  { w: 12, label: "Full", title: "Full width (100% of row)" },
];

/** Map stored/API widths to quarter / half / three-quarter / full (legacy 4→3, 8→9). */
export function normalizeDashboardWidgetWidth(width: number): DashboardWidgetWidth {
  if (width === 3 || width === 4) {
    return 3;
  }
  if (width === 6) {
    return 6;
  }
  if (width === 8 || width === 9) {
    return 9;
  }
  return 12;
}
