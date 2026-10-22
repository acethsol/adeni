import { Injectable, computed, signal } from "@angular/core";
import type { BusinessLocation, BusinessProfile, TenantEntitlements } from "@adeni/shared";
import {
  activeBusinessLocations,
  hasMultiLocationReportingEntitlement,
  resolveReportExportLocationId,
} from "@adeni/shared";
import type { AdeniFilterSelectOption } from "@adeni/ui";
import {
  PORTAL_ALL_LOCATIONS_SCOPE,
  readPersistedPortalActiveLocation,
  writePersistedPortalActiveLocation,
} from "./portal-active-location-store";

function defaultBranchLocationId(active: BusinessLocation[]): string | undefined {
  if (active.length === 0) {
    return undefined;
  }
  const primary = active.find((location) => location.isPrimary);
  return (primary ?? active[0])!.id;
}

@Injectable({ providedIn: "root" })
export class PortalActiveLocationService {
  private readonly tenantId = signal<string | null>(null);
  private readonly locations = signal<readonly BusinessLocation[]>([]);

  /** Selected branch id, or `PORTAL_ALL_LOCATIONS_SCOPE` when cross-branch scope is allowed. */
  readonly selectedLocationId = signal<string>(PORTAL_ALL_LOCATIONS_SCOPE);

  readonly activeLocations = computed(() => activeBusinessLocations(this.locations()));

  readonly showLocationSwitch = computed(() => this.activeLocations().length > 1);

  readonly selectedLocation = computed(() => {
    const id = this.selectedLocationId();
    if (!id || id === PORTAL_ALL_LOCATIONS_SCOPE) {
      return null;
    }
    return this.activeLocations().find((location) => location.id === id) ?? null;
  });

  syncFromProfile(profile: BusinessProfile | null | undefined): void {
    if (!profile?.tenantId) {
      this.tenantId.set(null);
      this.locations.set([]);
      this.selectedLocationId.set(PORTAL_ALL_LOCATIONS_SCOPE);
      return;
    }

    const active = activeBusinessLocations(profile.locations);
    const entitlements = profile.entitlements;
    const persisted = readPersistedPortalActiveLocation(profile.tenantId);
    const resolved = this.resolveInitialSelection(active, entitlements, persisted);

    this.tenantId.set(profile.tenantId);
    this.locations.set(profile.locations ?? []);
    this.selectedLocationId.set(resolved);
  }

  setSelectedLocationId(locationId: string): void {
    const tenantId = this.tenantId();
    const active = this.activeLocations();
    const normalized = locationId.trim();

    if (normalized === PORTAL_ALL_LOCATIONS_SCOPE) {
      this.selectedLocationId.set(PORTAL_ALL_LOCATIONS_SCOPE);
      if (tenantId) {
        writePersistedPortalActiveLocation(tenantId, PORTAL_ALL_LOCATIONS_SCOPE);
      }
      return;
    }

    if (!active.some((location) => location.id === normalized)) {
      return;
    }

    this.selectedLocationId.set(normalized);
    if (tenantId) {
      writePersistedPortalActiveLocation(tenantId, normalized);
    }
  }

  locationSwitchOptions(
    entitlements: TenantEntitlements | null | undefined,
  ): AdeniFilterSelectOption[] {
    const active = this.activeLocations();
    const branchOptions: AdeniFilterSelectOption[] = active.map((location) => ({
      value: location.id,
      label: location.name?.trim() || location.slug || location.area || "Branch",
    }));

    if (hasMultiLocationReportingEntitlement(entitlements)) {
      return [{ value: PORTAL_ALL_LOCATIONS_SCOPE, label: "All locations" }, ...branchOptions];
    }

    return branchOptions;
  }

  /** Resolves `locationId` for tenant report export API calls (global branch scope). */
  reportExportLocationId(entitlements: TenantEntitlements | null | undefined): string | undefined {
    const selected = this.selectedLocationId();
    return resolveReportExportLocationId({
      locations: this.locations(),
      entitlements,
      selectedLocationId:
        selected === PORTAL_ALL_LOCATIONS_SCOPE ? null : selected || null,
    });
  }

  private resolveInitialSelection(
    active: BusinessLocation[],
    entitlements: TenantEntitlements | null | undefined,
    persisted: string | null,
  ): string {
    if (active.length <= 1) {
      return active[0]?.id ?? PORTAL_ALL_LOCATIONS_SCOPE;
    }

    const crossBranchAllowed = hasMultiLocationReportingEntitlement(entitlements);

    if (persisted !== null) {
      if (persisted === PORTAL_ALL_LOCATIONS_SCOPE && crossBranchAllowed) {
        return PORTAL_ALL_LOCATIONS_SCOPE;
      }
      if (active.some((location) => location.id === persisted)) {
        return persisted;
      }
    }

    if (crossBranchAllowed) {
      return PORTAL_ALL_LOCATIONS_SCOPE;
    }

    return defaultBranchLocationId(active) ?? PORTAL_ALL_LOCATIONS_SCOPE;
  }
}
