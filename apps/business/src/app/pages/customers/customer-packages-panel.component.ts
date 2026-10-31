import { Component, computed, inject, input, OnInit, signal } from "@angular/core";
import {
  formatPrice,
  formatSlotTime,
  hasCrossLocationRetentionEntitlement,
  mergeTenantEntitlements,
  type BusinessLocation,
  type CustomerPackage,
  type TenantEntitlements,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

/** Customer detail → Packages tab: sold location/channel, redemption scope, balances and expiry (read-only). */
@Component({
  selector: "app-customer-packages-panel",
  standalone: true,
  imports: [AdeniCarbonIconComponent, AdeniPillComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./customer-packages-panel.component.html",
  styleUrl: "./customer-packages-panel.component.scss",
})
export class CustomerPackagesPanelComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);

  readonly customerId = input.required<string>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly packages = signal<CustomerPackage[]>([]);
  private readonly entitlements = signal<TenantEntitlements | null>(null);
  private readonly locations = signal<BusinessLocation[]>([]);
  private readonly locationNames = computed(() => new Map(this.locations().map((l) => [l.id, l.name])));

  readonly activePackageCount = computed(
    () => this.packages().filter((p) => p.status === "active").length,
  );

  readonly totalSessionsLeft = computed(() =>
    this.packages().reduce(
      (sum, p) => sum + p.balances.reduce((line, b) => line + b.remaining, 0),
      0,
    ),
  );

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [list, locations, planEntitlements, profile] = await Promise.all([
          client.retention.listCustomerPackages(this.customerId()),
          client.getTenantLocations().catch(() => [] as BusinessLocation[]),
          client.getTenantEntitlements().catch(() => null),
          client.getTenantProfile().catch(() => null),
        ]);
        this.entitlements.set(
          mergeTenantEntitlements(planEntitlements, null, profile?.entitlements ?? null),
        );
        this.packages.set(list.items);
        this.locations.set(locations);
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.retention.customerCouldNotLoad"));
      this.packages.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  packageProgress(p: CustomerPackage): { remaining: number; granted: number; percent: number; deg: string } | null {
    const granted = p.balances.reduce((sum, b) => sum + b.granted, 0);
    const remaining = p.balances.reduce((sum, b) => sum + b.remaining, 0);
    if (granted <= 0) {
      return null;
    }
    const percent = Math.max(0, Math.min(100, Math.round((remaining / granted) * 100)));
    return { remaining, granted, percent, deg: `${(remaining / granted) * 360}deg` };
  }

  balancePercent(b: CustomerPackage["balances"][number]): number {
    if (b.granted <= 0) {
      return 0;
    }
    return Math.max(0, Math.min(100, Math.round((b.remaining / b.granted) * 100)));
  }

  locationName(id: string): string {
    return this.locationNames().get(id) ?? "—";
  }

  price(p: CustomerPackage): string {
    return formatPrice(p.listPrice, p.currency);
  }

  when(iso: string): string {
    return formatSlotTime(iso);
  }

  channelLabel(p: CustomerPackage): string {
    switch (p.soldChannel) {
      case "in_person_till":
        return this.tr("businessApp.retention.channelTill");
      case "in_person_transfer":
        return this.tr("businessApp.retention.channelTransfer");
      default:
        return this.tr("businessApp.retention.channelOnline");
    }
  }

  showCrossLocationFootnote(p: CustomerPackage): boolean {
    return p.redemptionScope === "all_locations" && !hasCrossLocationRetentionEntitlement(this.entitlements());
  }

  scopeLabel(p: CustomerPackage): string {
    switch (p.redemptionScope) {
      case "purchase_location_only":
        return this.tr("businessApp.retention.redeemableAtPurchase", {
          location: p.soldAtLocationId ? this.locationName(p.soldAtLocationId) : "—",
        });
      case "selected_locations": {
        const names = p.redemptionLocationIds.map((id) => this.locationName(id));
        return names.length > 0
          ? names.join(", ")
          : this.tr("businessApp.retention.redeemableAtSelected", { count: 0 });
      }
      default:
        return this.tr("businessApp.retention.scopeAll");
    }
  }

  statusLabel(p: CustomerPackage): string {
    switch (p.status) {
      case "pending_payment":
        return this.tr("businessApp.retention.statusPending");
      case "active":
        return this.tr("businessApp.retention.statusActive");
      case "expired":
        return this.tr("businessApp.retention.statusExpired");
      default:
        return this.tr("businessApp.retention.statusVoided");
    }
  }

  statusTone(p: CustomerPackage): AdeniPillTone {
    switch (p.status) {
      case "active":
        return "success";
      case "pending_payment":
        return "warning";
      case "voided":
        return "danger";
      default:
        return "neutral";
    }
  }
}
