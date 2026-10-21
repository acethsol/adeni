import { DatePipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, Router } from "@angular/router";
import {
  bookingsUsagePercent,
  formatAmountMinor,
  formatBillingChargeStatus,
  formatBookingUsageMetric,
  formatLocationUsageMetric,
  formatPrice,
  formatSubscriptionPlanLabel,
  formatTeamUsageMetric,
  formatTenantBillingStatus,
  getMarketById,
  getPlanDisplayForCountry,
  isPlanUsageNearLimit,
  isSelfServePricingEnabled,
  planComparisonCell,
  PLAN_COMPARISON,
  resolveSubscriptionSku,
  SUBSCRIPTION_SKUS,
  SUBSCRIPTION_SKU_LABELS,
  usagePercentForLimit,
  type PublicCountryPricing,
  type SubscriptionSku,
  type SubscriptionUsage,
  type TenantSubscriptionCharge,
} from "@adeni/shared";

const OUTCOME_TAGLINES: Record<SubscriptionSku, string> = {
  starter: "Get bookable",
  professional: "Run & grow",
  business: "Scale operations",
};
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniPillTone,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const BILLING_EVENT_LABELS: Record<string, string> = {
  "subscription.create": "Plan activated",
  "subscription.charge.success": "Monthly subscription",
  "invoice.payment_failed": "Payment failed",
  "subscription.disable": "Subscription canceled",
};

@Component({
  selector: "app-plan",
  standalone: true,
  imports: [
    PortalPageComponent,
    DatePipe,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniPortalEmptyStateComponent,
    AdeniPillComponent,
  ],
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
  readonly formatSubscriptionPlanLabel = formatSubscriptionPlanLabel;
  readonly formatBookingUsageMetric = formatBookingUsageMetric;
  readonly formatLocationUsageMetric = formatLocationUsageMetric;
  readonly formatTeamUsageMetric = formatTeamUsageMetric;
  readonly bookingsUsagePercent = bookingsUsagePercent;
  readonly usagePercentForLimit = usagePercentForLimit;
  readonly isPlanUsageNearLimit = isPlanUsageNearLimit;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatPrice = formatPrice;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
  readonly comparisonCell = planComparisonCell;

  readonly billingColumns: AdeniDataTableColumn<TenantSubscriptionCharge>[] = [
    {
      id: "occurredAt",
      header: "Date",
      cell: (row) => row.occurredAt,
      sortable: true,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "event",
      header: "Event",
      cell: (row) => this.formatBillingEvent(row.eventType),
      sortable: true,
      sortValue: (row) => this.formatBillingEvent(row.eventType),
    },
    {
      id: "amount",
      header: "Amount",
      align: "right",
      cell: (row) => this.formatChargeAmount(row),
      sortable: true,
      sortValue: (row) => row.amountMinor ?? 0,
    },
    {
      id: "status",
      header: "Status",
      cell: (row) => formatBillingChargeStatus(row.status),
      sortable: true,
      sortValue: (row) => row.status,
    },
  ];

  readonly trackBillingRow = (row: TenantSubscriptionCharge): string => row.id;

  readonly billingSortColumn = signal<string | null>("occurredAt");
  readonly billingSortDirection = signal<AdeniDataTableSortDirection>("desc");

  comparisonCellKind(
    row: (typeof PLAN_COMPARISON)[number],
    sku: SubscriptionSku,
  ): "yes" | "dash" | "text" {
    const value = row[sku];
    if (value === true) {
      return "yes";
    }
    if (value === false) {
      return "dash";
    }
    return "text";
  }

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

  selfServeEnabled(): boolean {
    const pricing = this.apiPricing();
    if (pricing) {
      return pricing.selfServeEnabled;
    }
    const country = this.countryCode();
    return country ? isSelfServePricingEnabled(country) : false;
  }

  planDisplay(sku: SubscriptionSku): {
    name: string;
    priceLabel: string;
    description: string;
    tagline: string;
  } {
    const country = this.countryCode();
    const pricing = this.apiPricing();
    const tagline = OUTCOME_TAGLINES[sku];
    const fallbackDescription =
      getPlanDisplayForCountry(country ?? "", sku)?.description ??
      "Pricing for your market is being configured.";

    if (pricing) {
      if (!pricing.selfServeEnabled) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: "Contact us",
          description: fallbackDescription,
          tagline,
        };
      }

      const row = pricing.skus.find((item) => item.sku === sku);
      const amount = row?.monthlyAmountMajor;
      if (typeof amount === "number" && amount > 0) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: `${formatPrice(amount, pricing.currencyCode)}/mo`,
          description: fallbackDescription,
          tagline,
        };
      }
    }

    if (country) {
      const localized = getPlanDisplayForCountry(country, sku);
      if (localized) {
        return { ...localized, tagline };
      }
    }

    return {
      name: SUBSCRIPTION_SKU_LABELS[sku],
      priceLabel: "Contact us",
      description: fallbackDescription,
      tagline,
    };
  }

  isPopular(sku: SubscriptionSku): boolean {
    return sku === "professional";
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

  formatBillingEvent(eventType: string): string {
    return BILLING_EVENT_LABELS[eventType] ?? eventType;
  }

  formatChargeAmount(row: TenantSubscriptionCharge): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return "—";
  }

  billingStatusPillTone(status: string): AdeniPillTone {
    const normalized = status.trim().toLowerCase();
    if (normalized === "active" || normalized === "paid" || normalized === "success") {
      return "success";
    }
    if (normalized === "past_due" || normalized === "failed") {
      return "danger";
    }
    return "neutral";
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
