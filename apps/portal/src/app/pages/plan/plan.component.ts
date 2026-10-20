import { DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import {
  formatBookingUsage,
  formatAmountMinor,
  formatBillingChargeStatus,
  formatPrice,
  formatTenantBillingStatus,
  getMarketById,
  getPlanDisplayForCountry,
  isNearBookingLimit,
  planComparisonCell,
  PLAN_COMPARISON,
  resolveSubscriptionSku,
  SUBSCRIPTION_SKUS,
  SUBSCRIPTION_SKU_LABELS,
  type PublicCountryPricing,
  type SubscriptionSku,
  type SubscriptionUsage,
  type TenantSubscriptionCharge,
} from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-plan",
  standalone: true,
  imports: [PortalPageComponent, DatePipe],
  templateUrl: "./plan.component.html",
  styleUrl: "./plan.component.scss",
})
export class PlanComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly checkoutBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly effectiveSku = signal<SubscriptionSku>("starter");
  readonly contractSku = signal<SubscriptionSku>("starter");
  readonly countryCode = signal<string | null>(null);
  readonly usage = signal<SubscriptionUsage | null>(null);
  readonly billingHistory = signal<TenantSubscriptionCharge[]>([]);
  readonly apiPricing = signal<PublicCountryPricing | null>(null);
  private tenantId = signal<string | null>(null);

  readonly skus = SUBSCRIPTION_SKUS;
  readonly planComparison = PLAN_COMPARISON;
  readonly formatBookingUsage = formatBookingUsage;
  readonly isNearBookingLimit = isNearBookingLimit;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
  readonly comparisonCell = planComparisonCell;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [context, profile, subscriptionUsage, history] = await Promise.all([
          c.getBusinessContext(),
          c.getTenantProfile(),
          c.getTenantSubscriptionUsage(),
          c.getTenantBillingHistory(10).catch(() => []),
        ]);
        this.tenantId.set(context.tenantId);
        this.effectiveSku.set(resolveSubscriptionSku(subscriptionUsage.subscriptionSku));
        this.contractSku.set(
          resolveSubscriptionSku(
            profile.subscriptionSku ??
              subscriptionUsage.contractSubscriptionSku ??
              subscriptionUsage.subscriptionSku,
          ),
        );
        const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
        const country =
          location ? (getMarketById(location.marketId)?.countryCode ?? null) : null;
        this.countryCode.set(country);
        this.usage.set(subscriptionUsage);
        this.billingHistory.set(history);

        if (country) {
          try {
            const pricing = await this.api.createPublicClient().getPublicPricing(country);
            this.apiPricing.set(pricing);
          } catch {
            this.apiPricing.set(null);
          }
        } else {
          this.apiPricing.set(null);
        }
      });
      await this.maybeCompleteStubCheckout();
    } catch {
      this.error.set("Could not load plan details.");
    } finally {
      this.loading.set(false);
    }
  }

  planDisplay(sku: SubscriptionSku): { name: string; priceLabel: string; description: string } {
    const country = this.countryCode();
    const pricing = this.apiPricing();
    const fallbackDescription =
      getPlanDisplayForCountry(country ?? "", sku)?.description ??
      "Pricing for your market is being configured.";

    if (pricing) {
      if (!pricing.selfServeEnabled) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: "Contact us",
          description: fallbackDescription,
        };
      }

      const row = pricing.skus.find((item) => item.sku === sku);
      const amount = row?.monthlyAmountMajor;
      if (typeof amount === "number" && amount > 0) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: `${formatPrice(amount, pricing.currencyCode)}/mo`,
          description: fallbackDescription,
        };
      }
    }

    if (country) {
      const localized = getPlanDisplayForCountry(country, sku);
      if (localized) {
        return localized;
      }
    }

    return {
      name: SUBSCRIPTION_SKU_LABELS[sku],
      priceLabel: "Contact us",
      description: fallbackDescription,
    };
  }

  isCurrentPlan(sku: SubscriptionSku): boolean {
    return sku === this.effectiveSku();
  }

  canUpgrade(sku: SubscriptionSku): boolean {
    const order: SubscriptionSku[] = ["starter", "professional", "business"];
    return order.indexOf(sku) > order.indexOf(this.contractSku());
  }

  async upgrade(sku: SubscriptionSku): Promise<void> {
    const tenantId = this.tenantId();
    if (!tenantId || !this.canUpgrade(sku)) {
      return;
    }

    this.checkoutBusy.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const origin = window.location.origin;
        const checkout = await c.createSubscriptionCheckout({
          tenantId,
          targetSku: sku,
          customerEmail: "billing@adeni.local",
          successUrl: `${origin}/plan?upgraded=1`,
          cancelUrl: `${origin}/plan`,
        });

        const url = new URL(checkout.checkoutUrl);
        if (url.searchParams.has("stub_checkout")) {
          await c.completeStubSubscriptionCheckout({
            tenantId,
            providerReference: checkout.providerReference,
          });
          await this.router.navigate(["/plan"], { queryParams: { upgraded: "1" }, replaceUrl: true });
          await this.load();
          return;
        }

        window.location.href = checkout.checkoutUrl;
      });
    } catch {
      this.error.set("Checkout could not start. Try again.");
    } finally {
      this.checkoutBusy.set(false);
    }
  }

  formatCharge(row: TenantSubscriptionCharge): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return formatBillingChargeStatus(row.status);
  }

  featureCell(value: string | boolean): string {
    if (value === true) return "Yes";
    if (value === false) return "—";
    return value;
  }

  private async maybeCompleteStubCheckout(): Promise<void> {
    const params = this.route.snapshot.queryParamMap;
    const reference = params.get("reference");
    const stub = params.get("stub_checkout");
    const tenantId = this.tenantId();
    if (!tenantId || !reference || stub !== "1") {
      return;
    }

    await this.api.withAuthorizedClient((c) =>
      c.completeStubSubscriptionCheckout({ tenantId, providerReference: reference }),
    );
    await this.router.navigate(["/plan"], { queryParams: { upgraded: "1" }, replaceUrl: true });
  }
}
