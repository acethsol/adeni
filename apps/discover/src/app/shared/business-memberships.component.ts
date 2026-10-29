import { isPlatformBrowser } from "@angular/common";
import { Component, inject, input, OnInit, PLATFORM_ID, signal } from "@angular/core";
import { formatPrice, resolvePaymentCheckoutUrl, type PublicMembershipPlan } from "@adeni/shared";
import { injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
import { CustomerApiService } from "../core/services/customer-api.service";

function newKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/**
 * Public membership plans on the business profile. Anonymous read; subscribe needs a signed-in customer
 * and hands off to Finance checkout. The membership stays pending until PaymentCompleted.
 */
@Component({
  selector: "app-business-memberships",
  standalone: true,
  template: `
    @if (plans().length > 0) {
      <section id="memberships" class="block memberships">
        <h2>{{ tr("discover.memberships.title") }}</h2>
        <p class="memberships-lede">{{ tr("discover.memberships.description") }}</p>
        @if (error()) {
          <p class="memberships-error" role="alert">{{ error() }}</p>
        }
        <ul class="membership-list">
          @for (plan of plans(); track plan.id) {
            <li class="membership-card">
              <div class="membership-card__head">
                <h3>{{ plan.name }}</h3>
                <strong class="membership-card__price">{{ price(plan) }}</strong>
              </div>
              @if (plan.description) {
                <p class="membership-card__desc">{{ plan.description }}</p>
              }
              <ul class="membership-card__benefits">
                @for (b of plan.benefits; track b.id) {
                  <li>{{ tr("discover.memberships.includes", { count: b.quantityPerCycle, service: b.serviceName }) }}</li>
                }
              </ul>
              @if (onlineSubscribeEnabled()) {
                <button type="button" class="primary" [disabled]="busyId() === plan.id" (click)="subscribe(plan)">
                  {{ busyId() === plan.id ? tr("discover.memberships.subscribing") : tr("discover.memberships.subscribe") }}
                </button>
              } @else {
                <p class="membership-card__meta">{{ tr("discover.memberships.subscribeUnavailable") }}</p>
              }
            </li>
          }
        </ul>
      </section>
    }
  `,
  styles: [
    `
      .memberships-lede, .membership-card__meta, .membership-card__desc { margin: 0.25rem 0; opacity: 0.8; }
      .memberships-error { color: var(--cds-support-error, #da1e28); }
      .membership-list { list-style: none; margin: 1rem 0 0; padding: 0; display: grid; gap: 1rem; grid-template-columns: repeat(auto-fill, minmax(260px, 1fr)); }
      .membership-card { border: 1px solid var(--cds-border-subtle, #e0e0e0); border-radius: 0.75rem; padding: 1rem 1.25rem; display: grid; gap: 0.5rem; align-content: start; }
      .membership-card__head { display: flex; justify-content: space-between; gap: 0.75rem; align-items: baseline; }
      .membership-card__head h3 { margin: 0; font-size: 1.05rem; }
      .membership-card__benefits { margin: 0; padding-left: 1.1rem; }
    `,
  ],
})
export class BusinessMembershipsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  private readonly isBrowser = isPlatformBrowser(inject(PLATFORM_ID));

  readonly slug = input.required<string>();

  readonly plans = signal<PublicMembershipPlan[]>([]);
  readonly onlineSubscribeEnabled = signal(false);
  readonly busyId = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  private readonly keys = new Map<string, string>();

  ngOnInit(): void {
    if (this.isBrowser) {
      void this.load();
    }
  }

  private async load(): Promise<void> {
    try {
      const catalog = await this.api.createPublicClient().retention.publicMembershipPlans(this.slug());
      this.plans.set(catalog.items);
      this.onlineSubscribeEnabled.set(catalog.onlineSubscribeEnabled);
    } catch {
      this.plans.set([]);
    }
  }

  price(plan: PublicMembershipPlan): string {
    const interval = this.tr(`discover.memberships.${plan.billingInterval === "quarterly" || plan.billingInterval === "annually" ? plan.billingInterval : "monthly"}`);
    return this.tr("discover.memberships.perInterval", {
      price: formatPrice(plan.price, plan.currency),
      interval,
    });
  }

  async subscribe(plan: PublicMembershipPlan): Promise<void> {
    if (this.busyId()) return;
    this.error.set(null);
    this.busyId.set(plan.id);
    try {
      if (!(await this.api.isLoggedIn())) {
        await this.api.login(`${window.location.pathname}#memberships`);
        return;
      }
      const key = this.keys.get(plan.id) ?? newKey();
      this.keys.set(plan.id, key);
      const started = await this.api.withAuthorizedClient((c) => c.retention.subscribeMembership(this.slug(), plan.id, key));
      if (!started.checkoutUrl) {
        this.error.set(this.tr("discover.memberships.subscribeFailed"));
        return;
      }
      window.location.href = resolvePaymentCheckoutUrl(started.checkoutUrl, window.location.origin, {
        amount: plan.price,
        currency: plan.currency,
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "discover.memberships.subscribeFailed"));
    } finally {
      this.busyId.set(null);
    }
  }
}
