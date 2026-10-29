import {
  Component,
  computed,
  ElementRef,
  HostListener,
  inject,
  input,
  signal,
} from "@angular/core";
import type { TenantEntitlements } from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniPillComponent, injectAdeniTranslate } from "@adeni/ui";
import { BusinessActiveLocationService } from "../core/services/business-active-location.service";
import { PORTAL_ALL_LOCATIONS_SCOPE } from "../core/services/business-active-location-store";

@Component({
  selector: "app-business-branch-switch",
  standalone: true,
  imports: [AdeniCarbonIconComponent, AdeniPillComponent],
  templateUrl: "./business-branch-switch.component.html",
  styleUrl: "./business-branch-switch.component.scss",
})
export class BusinessBranchSwitchComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly activeLocation = inject(BusinessActiveLocationService);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly entitlements = input<TenantEntitlements | null>(null);
  readonly ariaLabel = input.required<string>();
  readonly allLocationsLabel = input("All locations");

  readonly open = signal(false);
  readonly allScopeValue = PORTAL_ALL_LOCATIONS_SCOPE;
  readonly menuId = `portal-branch-menu-${Math.random().toString(36).slice(2, 9)}`;

  readonly branches = computed(() => this.activeLocation.activeLocations());
  readonly display = computed(() => this.activeLocation.switchDisplay());
  readonly showAllOption = computed(() =>
    this.activeLocation.allowsAllLocationsScope(this.entitlements()),
  );

  readonly selectedId = computed(() => this.activeLocation.selectedLocationId());

  toggle(event: MouseEvent): void {
    event.stopPropagation();
    const next = !this.open();
    this.open.set(next);
    if (next) {
      this.focusMenuItem(0);
    }
  }

  close(): void {
    this.open.set(false);
  }

  select(locationId: string, event: MouseEvent): void {
    event.stopPropagation();
    this.activeLocation.setSelectedLocationId(locationId);
    this.close();
    queueMicrotask(() => {
      (this.host.nativeElement.querySelector(".branch-trigger") as HTMLButtonElement | null)?.focus();
    });
  }

  isSelected(locationId: string): boolean {
    return this.selectedId() === locationId;
  }

  onRootKeydown(event: KeyboardEvent): void {
    if (event.key === "Escape" && this.open()) {
      event.preventDefault();
      event.stopPropagation();
      this.close();
      (this.host.nativeElement.querySelector(".branch-trigger") as HTMLButtonElement | null)?.focus();
      return;
    }

    if (!this.open()) {
      const onTrigger =
        event.target instanceof Element && event.target.closest(".branch-trigger");
      if (onTrigger && (event.key === "ArrowDown" || event.key === "ArrowUp")) {
        event.preventDefault();
        this.open.set(true);
        this.focusMenuItem(event.key === "ArrowUp" ? -1 : 0);
      }
      return;
    }

    const items = this.menuItems();
    if (items.length === 0) {
      return;
    }

    const active = document.activeElement;
    let index = items.findIndex((item) => item === active);
    if (index < 0) {
      index = 0;
    }

    switch (event.key) {
      case "ArrowDown":
        event.preventDefault();
        this.focusMenuItem((index + 1) % items.length);
        break;
      case "ArrowUp":
        event.preventDefault();
        this.focusMenuItem((index - 1 + items.length) % items.length);
        break;
      case "Home":
        event.preventDefault();
        this.focusMenuItem(0);
        break;
      case "End":
        event.preventDefault();
        this.focusMenuItem(items.length - 1);
        break;
      default:
        break;
    }
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

  private menuItems(): HTMLButtonElement[] {
    return Array.from(this.host.nativeElement.querySelectorAll(".branch-item")) as HTMLButtonElement[];
  }

  private focusMenuItem(index: number): void {
    queueMicrotask(() => {
      const items = this.menuItems();
      if (items.length === 0) {
        return;
      }
      const resolvedIndex = index < 0 ? items.length - 1 : Math.min(index, items.length - 1);
      items[resolvedIndex]?.focus();
    });
  }
}
