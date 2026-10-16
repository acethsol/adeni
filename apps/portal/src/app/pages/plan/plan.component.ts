import { DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import {
  formatBookingUsage,
  formatAmountMinor,
  formatBillingChargeStatus,
  formatTenantBillingStatus,
  getMarketById,
  getPlanDisplayForCountry,
  isNearBookingLimit,
  PLAN_COMPARISON,
  type PlanFeature,
  resolveSubscriptionSku,
  SUBSCRIPTION_SKUS,
  skuToSubscriptionTier,
  type SubscriptionSku,
  type SubscriptionTier,
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
  readonly currentTier = signal<SubscriptionTier>("free");
  readonly currentSku = signal<SubscriptionSku>("starter");
  readonly countryCode = signal<string | null>(null);
  readonly usage = signal<SubscriptionUsage | null>(null);
  readonly billingHistory = signal<TenantSubscriptionCharge[]>([]);
  private tenantId = signal<string | null>(null);

  readonly skus = SUBSCRIPTION_SKUS;
  readonly planComparison = PLAN_COMPARISON;
  readonly skuTierColumn = {
    starter: "free",
    professional: "pro",
    business: "business",
  };
  readonly formatBookingUsage = formatBookingUsage;
  readonly isNearBookingLimit = isNearBookingLimit;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;

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
        const tier = profile.subscriptionTier ?? subscriptionUsage.tier;
        const sku = resolveSubscriptionSku(
          profile.subscriptionSku ?? subscriptionUsage.subscriptionSku,
          tier,
        );
        this.currentTier.set(tier);
        this.currentSku.set(sku);
        const location = profile.locations.find((item) => item.isPrimary) ?? profile.locations[0];
        this.countryCode.set(
          location ? (getMarketById(location.marketId)?.countryCode ?? null) : null,
        );
        this.usage.set(subscriptionUsage);
        this.billingHistory.set(history);
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
    if (country) {
      const localized = getPlanDisplayForCountry(country, sku);
      if (localized) {
        return localized;
      }
    }
    const tier = this.skuTierColumn[sku];
    const fallbackName = sku.charAt(0).toUpperCase() + sku.slice(1);
    return {
      name: fallbackName,
      priceLabel: "Contact us",
      description: "Pricing for your market is being configured.",
    };
  }

  canUpgrade(sku: SubscriptionSku): boolean {
    const order: SubscriptionSku[] = ["starter", "professional", "business"];
    return order.indexOf(sku) > order.indexOf(this.currentSku());
  }

  async upgrade(sku: SubscriptionSku): Promise<void> {
    const tenantId = this.tenantId();
    if (!tenantId || !this.canUpgrade(sku)) {
      return;
    }
    const targetTier = skuToSubscriptionTier(sku);

    this.checkoutBusy.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const origin = window.location.origin;
        const checkout = await c.createSubscriptionCheckout({
          tenantId,
          targetTier,
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

  comparisonCell(row: PlanFeature, sku: SubscriptionSku): string {
    const tierKey = this.skuTierColumn[sku] as keyof Omit<PlanFeature, "label">;
    return this.featureCell(row[tierKey]);
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
