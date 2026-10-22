import { Injectable, computed, inject, signal } from "@angular/core";
import { Subject } from "rxjs";
import type { BusinessLocation, BusinessProfile, TenantEntitlements } from "@adeni/shared";
import {
  activeBusinessLocations,
  hasMultiLocationReportingEntitlement,
  resolveReportExportLocationId,
} from "@adeni/shared";
import { AdeniFeedbackService } from "@adeni/ui";
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

export type PortalBranchSwitchDisplay = {
  title: string;
  hint: string | null;
  allLocationsScope: boolean;
  isPrimary: boolean;
};

@Injectable({ providedIn: "root" })
export class PortalActiveLocationService {
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly tenantId = signal<string | null>(null);
  private readonly locations = signal<readonly BusinessLocation[]>([]);
  private readonly selectionChangedSubject = new Subject<string>();
  private branchSwitchActive = false;
  private branchSwitchPending = 0;

  /** Fires when the user picks a branch in the shell (not on profile hydration). */
  readonly selectionChanged$ = this.selectionChangedSubject.asObservable();

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

  readonly switchDisplay = computed((): PortalBranchSwitchDisplay => {
    const allScope = this.selectedLocationId() === PORTAL_ALL_LOCATIONS_SCOPE;
    if (allScope) {
      return {
        title: "All locations",
        hint: `${this.activeLocations().length} branches`,
        allLocationsScope: true,
        isPrimary: false,
      };
    }
    const branch = this.selectedLocation();
    if (!branch) {
      return {
        title: "Location",
        hint: null,
        allLocationsScope: false,
        isPrimary: false,
      };
    }
    const name = branch.name?.trim() || branch.slug || "Branch";
    const hint = branch.area?.trim() || branch.slug || null;
    return {
      title: name,
      hint,
      allLocationsScope: false,
      isPrimary: branch.isPrimary,
    };
  });

  syncFromProfile(profile: BusinessProfile | null | undefined): void {
    if (!profile?.tenantId) {
      this.tenantId.set(null);
      this.locations.set([]);
      this.selectedLocationId.set(PORTAL_ALL_LOCATIONS_SCOPE);
      return;
    }

    const entitlements = profile.entitlements;
    const active = activeBusinessLocations(profile.locations);
    const tenantChanged = this.tenantId() !== profile.tenantId;
    const previousSelection = this.selectedLocationId();

    this.tenantId.set(profile.tenantId);
    this.locations.set(profile.locations ?? []);

    if (
      !tenantChanged
      && this.isSelectionValid(previousSelection, active, entitlements)
    ) {
      return;
    }

    const persisted = readPersistedPortalActiveLocation(profile.tenantId);
    const resolved = this.resolveInitialSelection(active, entitlements, persisted);
    this.selectedLocationId.set(resolved);
  }

  setSelectedLocationId(locationId: string): void {
    const tenantId = this.tenantId();
    const active = this.activeLocations();
    const normalized = locationId.trim();

    let next = normalized;
    if (normalized === PORTAL_ALL_LOCATIONS_SCOPE) {
      next = PORTAL_ALL_LOCATIONS_SCOPE;
    } else if (!active.some((location) => location.id === normalized)) {
      return;
    }

    if (this.selectedLocationId() === next) {
      return;
    }

    this.selectedLocationId.set(next);
    if (tenantId) {
      writePersistedPortalActiveLocation(tenantId, next);
    }

    this.branchSwitchActive = true;
    this.branchSwitchPending = 0;
    this.beginBranchSwitchTransaction();
    this.selectionChangedSubject.next(next);
    queueMicrotask(() => this.tryFinishBranchSwitch());
  }

  beginBranchSwitchTransaction(): void {
    this.feedback.showLoading("Switching branch…", "Updating dashboard, bookings, and calendar");
  }

  endBranchSwitchTransaction(): void {
    this.feedback.hideLoading();
  }

  /** Wrap branch-switch refetches so the global loading panel stays up until work finishes. */
  runOnBranchSwitch<T>(work: () => Promise<T> | T): Promise<T> {
    if (!this.branchSwitchActive) {
      return this.feedback.runLoading(
        () => Promise.resolve(work()),
        "Switching branch…",
        "Updating dashboard, bookings, and calendar",
      );
    }

    this.branchSwitchPending += 1;
    return Promise.resolve(work()).finally(() => {
      this.branchSwitchPending -= 1;
      this.tryFinishBranchSwitch();
    });
  }

  private tryFinishBranchSwitch(): void {
    if (!this.branchSwitchActive || this.branchSwitchPending > 0) {
      return;
    }
    this.branchSwitchActive = false;
    this.endBranchSwitchTransaction();
  }

  /** @deprecated Use switch UI labels from `switchDisplay` or branch rows directly. */
  locationSwitchOptions(
    entitlements: TenantEntitlements | null | undefined,
  ): { value: string; label: string }[] {
    const active = this.activeLocations();
    const branchOptions = active.map((location) => ({
      value: location.id,
      label: location.name?.trim() || location.slug || location.area || "Branch",
    }));

    if (hasMultiLocationReportingEntitlement(entitlements)) {
      return [{ value: PORTAL_ALL_LOCATIONS_SCOPE, label: "All locations" }, ...branchOptions];
    }

    return branchOptions;
  }

  allowsAllLocationsScope(entitlements: TenantEntitlements | null | undefined): boolean {
    return hasMultiLocationReportingEntitlement(entitlements);
  }

  branchRowLabel(location: BusinessLocation): string {
    return location.name?.trim() || location.slug || location.area || "Branch";
  }

  branchRowHint(location: BusinessLocation): string | null {
    const area = location.area?.trim();
    if (area && area !== this.branchRowLabel(location)) {
      return area;
    }
    return location.slug?.trim() || null;
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

  /** Prefer active shell branch; fall back to primary / first location on profile. */
  /** Optional `locationId` query for tenant list/dashboard APIs (same rules as exports). */
  tenantApiLocationId(entitlements: TenantEntitlements | null | undefined): string | undefined {
    return this.reportExportLocationId(entitlements);
  }

  resolveContextLocation(
    profile: BusinessProfile | null | undefined,
  ): BusinessLocation | null {
    const selected = this.selectedLocation();
    if (selected) {
      return selected;
    }
    if (!profile?.locations?.length) {
      return null;
    }
    if (this.selectedLocationId() === PORTAL_ALL_LOCATIONS_SCOPE) {
      return null;
    }
    const active = activeBusinessLocations(profile.locations);
    const primary = active.find((item) => item.isPrimary) ?? active[0];
    return primary ?? null;
  }

  private isSelectionValid(
    selection: string,
    active: BusinessLocation[],
    entitlements: TenantEntitlements | null | undefined,
  ): boolean {
    if (active.length <= 1) {
      return active.length === 0
        ? selection === PORTAL_ALL_LOCATIONS_SCOPE
        : selection === active[0]!.id;
    }
    if (selection === PORTAL_ALL_LOCATIONS_SCOPE) {
      return hasMultiLocationReportingEntitlement(entitlements);
    }
    return active.some((location) => location.id === selection);
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
