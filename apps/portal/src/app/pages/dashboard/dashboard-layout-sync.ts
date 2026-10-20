import {
  cloneDashboardLayout,
  coerceDashboardLayout,
  normalizeDashboardWidgetWidth,
  resolveDashboardLayout,
  type DashboardLayout,
  type TenantDashboardLayout,
} from "@adeni/shared";
import {
  clearDashboardLayout,
  loadDashboardLayout as loadLegacyDashboardLayout,
} from "./dashboard-layout-store";

export function apiLayoutToDashboardLayout(layout: TenantDashboardLayout): DashboardLayout {
  return {
    version: 1,
    range: layout.range,
    widgets: layout.widgets.map((widget) => ({ id: widget.id, w: widget.w })),
  };
}

export function dashboardLayoutToApi(layout: DashboardLayout): TenantDashboardLayout {
  return {
    version: 1,
    range: layout.range,
    widgets: layout.widgets.map((widget) => ({
      id: widget.id,
      w: normalizeDashboardWidgetWidth(widget.w),
    })),
  };
}

function layoutsEqual(a: DashboardLayout, b: DashboardLayout): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/** Load layout from API; one-time migrate legacy localStorage when API still has role default. */
export async function resolveDashboardLayoutFromApi(
  fetchLayout: () => Promise<TenantDashboardLayout>,
  saveLayout: (layout: TenantDashboardLayout) => Promise<TenantDashboardLayout>,
  tenantId: string,
  practitioner: boolean,
): Promise<DashboardLayout> {
  const fallback = resolveDashboardLayout(practitioner);
  let layout = fallback;
  try {
    const fromApi = apiLayoutToDashboardLayout(await fetchLayout());
    layout = coerceDashboardLayout(fromApi, fallback);
  } catch {
    layout = cloneDashboardLayout(fallback);
  }

  const legacy = loadLegacyDashboardLayout(tenantId, practitioner);
  if (!layoutsEqual(legacy, fallback) && layoutsEqual(layout, fallback)) {
    layout = coerceDashboardLayout(legacy, fallback);
    await saveLayout(dashboardLayoutToApi(layout));
    clearDashboardLayout(tenantId, practitioner);
  }

  return cloneDashboardLayout(layout);
}
