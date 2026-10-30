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
  imports: [AdeniPillComponent, AdeniPortalEmptyStateComponent],
  template: `
    @if (loading()) {
      <p class="muted" role="status">{{ tr('toast.loading') }}</p>
    } @else if (error()) {
      <p class="error-banner" role="alert">{{ error() }}</p>
    } @else if (packages().length === 0) {
      <adeni-portal-empty-state
        icon="user--filled"
        [title]="tr('businessApp.retention.customerEmptyTitle')"
        [description]="tr('businessApp.retention.customerEmptyDescription')"
      />
    } @else {
      <p class="muted note">{{ tr('businessApp.retention.creditsAreSessions') }}</p>
      <ul class="packages">
        @for (p of packages(); track p.id) {
          <li class="card package">
            <div class="package__head">
              <h3 class="package__name">{{ p.name }}</h3>
              <adeni-pill [tone]="statusTone(p)" [compact]="true">{{ statusLabel(p) }}</adeni-pill>
            </div>
            <dl class="package__meta muted">
              <div><dt>{{ tr('businessApp.retention.soldVia') }}</dt><dd>{{ channelLabel(p) }}</dd></div>
              @if (p.soldAtLocationId) {
                <div><dt>{{ tr('businessApp.retention.soldAt') }}</dt><dd>{{ locationName(p.soldAtLocationId) }}</dd></div>
              }
              <div><dt>{{ tr('businessApp.retention.redeemableAt') }}</dt><dd>{{ scopeLabel(p) }}</dd></div>
              <div><dt>{{ tr('businessApp.retention.listPrice') }}</dt><dd>{{ price(p) }}</dd></div>
              <div>
                <dt>{{ tr('businessApp.retention.expiry') }}</dt>
                <dd>{{ p.expiresAt ? tr('businessApp.retention.expires', { date: when(p.expiresAt) }) : tr('businessApp.retention.noExpiry') }}</dd>
              </div>
            </dl>
            @if (showCrossLocationFootnote(p)) {
              <p class="scope-footnote muted">{{ tr('businessApp.retention.crossLocationScopeFootnote') }}</p>
            }
            <table class="balances">
              <thead>
                <tr>
                  <th scope="col">{{ tr('businessApp.retention.service') }}</th>
                  <th scope="col">{{ tr('businessApp.retention.granted') }}</th>
                  <th scope="col">{{ tr('businessApp.retention.used') }}</th>
                  <th scope="col">{{ tr('businessApp.retention.reserved') }}</th>
                  <th scope="col">{{ tr('businessApp.retention.remaining') }}</th>
                </tr>
              </thead>
              <tbody>
                @for (b of p.balances; track b.id) {
                  <tr>
                    <th scope="row">{{ b.serviceName }}</th>
                    <td>{{ b.granted }}</td>
                    <td>{{ b.used }}</td>
                    <td>{{ b.reserved }}</td>
                    <td><strong>{{ b.remaining }}</strong></td>
                  </tr>
                }
              </tbody>
            </table>
          </li>
        }
      </ul>
    }
  `,
  styles: [
    `
      .packages { list-style: none; margin: 0; padding: 0; display: grid; gap: 1rem; }
      .note { margin: 0 0 1rem; }
      .package { padding: 1rem 1.25rem; }
      .package__head { display: flex; align-items: center; justify-content: space-between; gap: 0.75rem; }
      .package__name { margin: 0; font-size: 1rem; }
      .package__meta { display: flex; flex-wrap: wrap; gap: 0.5rem 1.5rem; margin: 0.75rem 0; }
      .package__meta div { display: flex; gap: 0.35rem; }
      .package__meta dt, .package__meta dd { margin: 0; }
      .package__meta dt::after { content: ":"; }
      .scope-footnote { margin: 0.25rem 0 0; width: 100%; }
      .balances { width: 100%; border-collapse: collapse; font-size: 0.875rem; }
      .balances th, .balances td { padding: 0.35rem 0.5rem; text-align: right; border-top: 1px solid var(--cds-border-subtle, #e0e0e0); }
      .balances th:first-child { text-align: left; font-weight: 500; }
    `,
  ],
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
