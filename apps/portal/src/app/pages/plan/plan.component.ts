import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import { map } from "rxjs";
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

const TAGLINE_KEYS: Record<SubscriptionSku, string> = {
  starter: "portal.plan.taglineStarter",
  professional: "portal.plan.taglineProfessional",
  business: "portal.plan.taglineBusiness",
};
import {
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  AdeniTabsComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniPillTone,
  type AdeniTab,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const BILLING_EVENT_KEYS: Record<string, string> = {
  "subscription.create": "portal.plan.billingEventPlanActivated",
  "subscription.charge.success": "portal.plan.billingEventMonthlySubscription",
  "invoice.payment_failed": "portal.plan.billingEventPaymentFailed",
  "subscription.disable": "portal.plan.billingEventSubscriptionCanceled",
};

@Component({
  selector: "app-plan",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniTabsComponent,
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
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

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

  readonly billingColumns = computed<AdeniDataTableColumn<TenantSubscriptionCharge>[]>(() => [
    {
      id: "occurredAt",
      header: this.tr("portal.plan.date"),
      cell: (row) => row.occurredAt,
      sortable: true,
      sortValue: (row) => row.occurredAt,
    },
    {
      id: "event",
      header: this.tr("portal.plan.event"),
      cell: (row) => this.formatBillingEvent(row.eventType),
      sortable: true,
      sortValue: (row) => this.formatBillingEvent(row.eventType),
    },
    {
      id: "amount",
      header: this.tr("portal.quotes.amount"),
      align: "right",
      cell: (row) => this.formatChargeAmount(row),
      sortable: true,
      sortValue: (row) => row.amountMinor ?? 0,
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: (row) => formatBillingChargeStatus(row.status),
      sortable: true,
      sortValue: (row) => row.status,
    },
  ]);

  readonly trackBillingRow = (row: TenantSubscriptionCharge): string => row.id;

  readonly billingSortColumn = signal<string | null>("occurredAt");
  readonly billingSortDirection = signal<AdeniDataTableSortDirection>("desc");
  readonly billingSearchQuery = signal("");

  readonly planTabs = computed<AdeniTab[]>(() => [
    { id: "plan", label: this.tr("portal.plan.planBilling") },
    { id: "compare", label: this.tr("common.comparePlans") },
  ]);

  readonly planTab = toSignal(
    this.route.queryParamMap.pipe(
      map((params) => (params.get("tab") === "compare" ? "compare" : "plan")),
    ),
    {
      initialValue:
        this.route.snapshot.queryParamMap.get("tab") === "compare" ? "compare" : "plan",
    },
  );

  readonly filteredBillingHistory = computed(() => {
    const term = this.billingSearchQuery().trim().toLowerCase();
    const rows = this.billingHistory();
    if (!term) {
      return rows;
    }
    return rows.filter((row) => this.billingRowMatchesSearch(row, term));
  });

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
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
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
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.plan.couldNotLoadPlanDetails"));
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
    const tagline = this.tr(TAGLINE_KEYS[sku]);
    const fallbackDescription =
      getPlanDisplayForCountry(country ?? "", sku)?.description ??
      this.tr("portal.plan.pricingMarketBeingConfigured");

    if (pricing) {
      if (!pricing.selfServeEnabled) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: this.tr("portal.plan.contactUs"),
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
      priceLabel: this.tr("portal.plan.contactUs"),
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.plan.checkoutCouldNotStartTry"));
    } finally {
      this.checkoutBusy.set(false);
    }
  }

  formatBillingEvent(eventType: string): string {
    const key = BILLING_EVENT_KEYS[eventType];
    return key ? this.tr(key) : eventType;
  }

  private billingRowMatchesSearch(row: TenantSubscriptionCharge, term: string): boolean {
    const haystack = [
      this.formatBillingEvent(row.eventType),
      row.eventType,
      formatBillingChargeStatus(row.status),
      row.status,
      row.providerReference,
      this.formatChargeAmount(row),
    ]
      .join(" ")
      .toLowerCase();
    return haystack.includes(term);
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
    if (value === true) return this.tr("admin.pricing.yes");
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
