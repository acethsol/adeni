import type { DashboardLayoutPresetId } from "./dashboard-layout-presets";
import { t } from "./i18n";
import { getMarketById } from "./markets";
import type { PortalPermissionRole } from "./portal-permissions";

export type DashboardLayoutPresetDisplayCopy = {
  focus: string;
  description: string;
  /** Optional market-specific one-liner (Lagos / Ottawa). */
  marketLine: string | null;
};

function presetMessageKey(presetId: DashboardLayoutPresetId, field: "focus" | "description"): string {
  return `portalDashboard.presets.${presetId}.${field}`;
}

function marketLineKey(presetId: DashboardLayoutPresetId, marketId: string): string {
  return `portalDashboard.presets.${presetId}.market.${marketId}`;
}

export function getDashboardLayoutPresetDisplayCopy(
  locale: string,
  presetId: DashboardLayoutPresetId,
  marketId: string | null | undefined,
  fallback: { focus: string; description: string },
): DashboardLayoutPresetDisplayCopy {
  const focus = t(locale, presetMessageKey(presetId, "focus"));
  const description = t(locale, presetMessageKey(presetId, "description"));
  const resolvedFocus = focus === presetMessageKey(presetId, "focus") ? fallback.focus : focus;
  const resolvedDescription =
    description === presetMessageKey(presetId, "description") ? fallback.description : description;

  const marketKey = marketId?.trim().toLowerCase();
  let marketLine: string | null = null;
  if (marketKey) {
    const line = t(locale, marketLineKey(presetId, marketKey));
    const key = marketLineKey(presetId, marketKey);
    if (line !== key) {
      marketLine = line;
    }
  }

  return {
    focus: resolvedFocus,
    description: resolvedDescription,
    marketLine,
  };
}

export function resolveRecommendedDashboardLayoutPresetId(opts: {
  isPractitionerView: boolean;
  permissionRole: PortalPermissionRole;
}): DashboardLayoutPresetId {
  if (opts.isPractitionerView) {
    return "practitioner";
  }
  switch (opts.permissionRole) {
    case "manager":
      return "manager";
    case "receptionist":
      return "front_desk";
    case "practitioner":
      return "practitioner";
    case "accountant":
    case "ops":
      return "minimal";
    case "owner":
    default:
      return "owner";
  }
}

export function formatDashboardPresetMarketName(marketId: string | null | undefined): string | null {
  if (!marketId?.trim()) {
    return null;
  }
  return getMarketById(marketId)?.name ?? null;
}
