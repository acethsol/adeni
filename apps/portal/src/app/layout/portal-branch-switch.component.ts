import { Component, computed, HostListener, inject, input, signal } from "@angular/core";
import type { TenantEntitlements } from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniPillComponent, injectAdeniTranslate } from "@adeni/ui";
import { PortalActiveLocationService } from "../core/services/portal-active-location.service";
import { PORTAL_ALL_LOCATIONS_SCOPE } from "../core/services/portal-active-location-store";

@Component({
  selector: "app-portal-branch-switch",
  standalone: true,
  imports: [AdeniCarbonIconComponent, AdeniPillComponent],
  templateUrl: "./portal-branch-switch.component.html",
  styleUrl: "./portal-branch-switch.component.scss",
})
export class PortalBranchSwitchComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly activeLocation = inject(PortalActiveLocationService);

  readonly entitlements = input<TenantEntitlements | null>(null);
  readonly ariaLabel = input.required<string>();
  readonly allLocationsLabel = input("All locations");

  readonly open = signal(false);
  readonly allScopeValue = PORTAL_ALL_LOCATIONS_SCOPE;

  readonly branches = computed(() => this.activeLocation.activeLocations());
  readonly display = computed(() => this.activeLocation.switchDisplay());
  readonly showAllOption = computed(() =>
    this.activeLocation.allowsAllLocationsScope(this.entitlements()),
  );

  readonly selectedId = computed(() => this.activeLocation.selectedLocationId());

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    this.open.update((value) => !value);
  }

  close(): void {
    this.open.set(false);
  }

  select(locationId: string, event: MouseEvent): void {
    event.stopPropagation();
    this.activeLocation.setSelectedLocationId(locationId);
    this.close();
  }

  isSelected(locationId: string): boolean {
    return this.selectedId() === locationId;
  }

  @HostListener("document:click", ["$event"])
  closeOnOutsideClick(event: MouseEvent): void {
    if (!this.open()) {
      return;
    }
    const target = event.target;
    if (target instanceof Element && target.closest("[data-menu-root='branch']")) {
      return;
    }
    this.close();
  }
}
