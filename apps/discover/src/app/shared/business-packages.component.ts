import { isPlatformBrowser } from "@angular/common";
import { Component, inject, input, OnInit, PLATFORM_ID, signal } from "@angular/core";
import { formatPrice, resolvePaymentCheckoutUrl, type PublicPackage } from "@adeni/shared";
import { injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import { CustomerApiService } from "../core/services/customer-api.service";

function newKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Public package catalog on the business profile. Anonymous read; purchase needs a signed-in customer and
 * hands off to Finance checkout — credits are granted by the API only after PaymentCompleted.
 */
@Component({
  selector: "app-business-packages",
  standalone: true,
  template: `
    @if (packages().length > 0) {
      <section id="packages" class="block packages">
        <h2>{{ tr("discover.packages.title") }}</h2>
        <p class="packages-lede">{{ tr("discover.packages.description") }}</p>
        @if (error()) {
          <p class="packages-error" role="alert">{{ error() }}</p>
        }
        <ul class="package-list">
          @for (p of packages(); track p.id) {
            <li class="package-card">
              <div class="package-card__head">
                <h3>{{ p.name }}</h3>
                <strong class="package-card__price">{{ price(p) }}</strong>
              </div>
              @if (p.description) {
                <p class="package-card__desc">{{ p.description }}</p>
              }
              <ul class="package-card__benefits">
                @for (b of p.benefits; track b.id) {
                  <li>{{ tr("discover.packages.includes", { count: b.quantityGranted, service: b.serviceName }) }}</li>
                }
              </ul>
              <p class="package-card__meta">
                {{ tr("discover.packages.validFor", { count: p.expiryMonths }) }} · {{ scopeLabel(p) }}
              </p>
              @if (onlinePurchaseEnabled()) {
                <button type="button" class="primary" [disabled]="busyId() === p.id" (click)="buy(p)">
                  {{ busyId() === p.id ? tr("discover.packages.buying") : tr("discover.packages.buy") }}
                </button>
              } @else {
                <p class="package-card__meta">{{ tr("discover.packages.purchaseUnavailable") }}</p>
              }
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: [
    `
      .packages-lede, .package-card__meta, .package-card__desc { margin: 0.25rem 0; opacity: 0.8; }
      .packages-error { color: var(--cds-support-error, #da1e28); }
      .package-list { list-style: none; margin: 1rem 0 0; padding: 0; display: grid; gap: 1rem; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); }
      .package-card { border: 1px solid var(--cds-border-subtle, #e0e0e0); border-radius: 0.75rem; padding: 1rem 1.25rem; display: grid; gap: 0.5rem; align-content: start; }
      .package-card__head { display: flex; justify-content: space-between; gap: 0.75rem; align-items: baseline; }
      .package-card__head h3 { margin: 0; font-size: 1.05rem; }
      .package-card__benefits { margin: 0; padding-left: 1.1rem; }
    `,
  ],
})
export class BusinessPackagesComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly slug = input.required<string>();

  readonly packages = signal<PublicPackage[]>([]);
  readonly onlinePurchaseEnabled = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  /** One Idempotency-Key per package so a retry/double-click maps to the same checkout. */
  private readonly keys = new Map<string, string>();

  ngOnInit(): void {
    if (this.isBrowser) {
      void this.load();
    }
  }

  private async load(): Promise<void> {
    try {
      const catalog = await this.api.createPublicClient().retention.publicCatalog(this.slug());
      this.packages.set(catalog.items);
      this.onlinePurchaseEnabled.set(catalog.onlinePurchaseEnabled);
    } catch {
      this.packages.set([]); // Packages are optional on the profile.
    }
  }

  price(p: PublicPackage): string {
    return formatPrice(p.listPrice, p.currency);
  }

  scopeLabel(p: PublicPackage): string {
    switch (p.redemptionScope) {
      case "purchase_location_only":
        return this.tr("discover.packages.scopePurchase");
      case "selected_locations":
        return this.tr("discover.packages.scopeSelected");
      default:
        return this.tr("discover.packages.scopeAll");
    }
  }

  async buy(p: PublicPackage): Promise<void> {
    if (this.busyId()) return;
    this.error.set(null);
    this.busyId.set(p.id);
    try {
      if (!(await this.api.isLoggedIn())) {
        await this.api.login(`${window.location.pathname}#packages`);
        return;
      }
      const key = this.keys.get(p.id) ?? newKey();
      this.keys.set(p.id, key);
      const purchase = await this.api.withAuthorizedClient((c) => c.retention.purchasePackage(this.slug(), p.id, key));
      if (!purchase.checkoutUrl) {
        this.error.set(this.tr("discover.packages.purchaseFailed"));
        return;
      }
      window.location.href = resolvePaymentCheckoutUrl(purchase.checkoutUrl, window.location.origin, {
        amount: p.listPrice,
        currency: p.currency,
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "discover.packages.purchaseFailed"));
    } finally {
      this.busyId.set(null);
    }
  }
}
