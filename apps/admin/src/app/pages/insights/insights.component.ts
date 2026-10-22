import { DatePipe, LowerCasePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  type AdminGmvSummary,
  type AdminPlatformSubscriptionMrr,
  type AdminRevenueMetrics,
  type AdminRevenuePeriodBucket,
  type AdminSubscriptionMetrics,
  formatAmountMinor,
  formatBillingChargeStatus,
  formatPrice,
  formatSubscriptionSku,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

type RevenueGranularity = "month" | "year";

type CurrencyWindowTotals = {
  currency: string;
  saasAmountMinor: number;
  commerceVolume: number;
  platformFeeTotal: number;
};

type SkuMixRow = {
  key: string;
  label: string;
  count: number;
  tone: string;
};

@Component({
  selector: "app-admin-insights",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniCarbonIconComponent,
    DatePipe,
    LowerCasePipe,
  ],
  templateUrl: "./insights.component.html",
  styleUrl: "./insights.component.scss",
})
export class AdminInsightsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly metrics = signal<AdminSubscriptionMetrics | null>(null);
  readonly revenue = signal<AdminRevenueMetrics | null>(null);
  readonly granularity = signal<RevenueGranularity>("month");

  readonly windowLabel = computed(() =>
    this.tr(this.granularity() === "year" ? "admin.insights.window5Years" : "admin.insights.window12Months"),
  );

  readonly granularityLabel = computed(() =>
    this.tr(this.granularity() === "year" ? "admin.insights.yearly" : "admin.insights.monthly"),
  );

  readonly mrrRows = computed(() => {
    const data = this.metrics();
    if (!data) {
      return [];
    }
    return data.mrrSource === "synced_charges"
      ? data.syncedMrrByCurrency
      : data.estimatedMrrByCurrency;
  });

  readonly mrrPrimaryLabel = computed(() => {
    const rows = this.mrrRows();
    if (rows.length === 0) {
      return "—";
    }
    return rows.map((row) => formatAmountMinor(row.amountMinor, row.currency)).join(" · ");
  });

  readonly mrrHint = computed(() => {
    const data = this.metrics();
    if (!data) {
      return "";
    }
    return data.mrrSource === "synced_charges"
      ? this.tr("admin.insights.fromSyncedSubscriptionChargesMonth")
      : this.tr("admin.insights.estimatedFromConfiguredListPrices");
  });

  readonly windowTotals = computed((): CurrencyWindowTotals[] => {
    const rev = this.revenue();
    if (!rev) {
      return [];
    }
    const byCurrency = new Map<string, CurrencyWindowTotals>();
    for (const row of rev.buckets) {
      const existing = byCurrency.get(row.currency) ?? {
        currency: row.currency,
        saasAmountMinor: 0,
        commerceVolume: 0,
        platformFeeTotal: 0,
      };
      existing.saasAmountMinor += row.saasAmountMinor;
      existing.commerceVolume += row.commerceVolume;
      existing.platformFeeTotal += row.platformFeeTotal;
      byCurrency.set(row.currency, existing);
    }
    return [...byCurrency.values()].sort((a, b) => a.currency.localeCompare(b.currency));
  });

  readonly saasWindowLabel = computed(() =>
    formatTotalsMinor(this.windowTotals(), (row) => row.saasAmountMinor),
  );

  readonly feesWindowLabel = computed(() =>
    formatTotalsMajor(this.windowTotals(), (row) => row.platformFeeTotal),
  );

  readonly gmvWindowLabel = computed(() =>
    formatTotalsMajor(this.windowTotals(), (row) => row.commerceVolume),
  );

  readonly hasCommerceActivity = computed(() => {
    const rev = this.revenue();
    if (!rev) {
      return false;
    }
    return rev.recentCommercePayments.length > 0 || rev.buckets.some((row) => row.commerceVolume > 0);
  });

  readonly earningsHeadline = computed(() => {
    const saas = this.saasWindowLabel();
    const fees = this.feesWindowLabel();
    if (!saas && !fees) {
      return this.tr("admin.insights.noEarningsInWindow", { window: this.windowLabel().toLowerCase() });
    }
    if (saas && fees) {
      return this.tr("admin.insights.earningsBoth", { saas, fees });
    }
    if (saas) {
      return this.tr("admin.insights.earningsSaasOnly", { saas: saas ?? "", window: this.windowLabel().toLowerCase() });
    }
    return this.tr("admin.insights.earningsFeesOnly", { fees: fees ?? "", window: this.windowLabel().toLowerCase() });
  });

  readonly revenueInsight = computed(() => {
    const billing = this.metrics();
    if (!billing) {
      return "";
    }
    if (billing.pastDueTenantCount > 0) {
      return this.tr("admin.insights.tenantsPastDue", { count: billing.pastDueTenantCount });
    }
    if (billing.paidTenantCount === 0 && billing.trialingTenantCount > 0) {
      return this.tr("admin.insights.tenantsOnTrial", { count: billing.trialingTenantCount });
    }
    if (!this.hasCommerceActivity()) {
      return this.tr("admin.insights.commerceGmvQuietSaasSubscription");
    }
    return this.tr("admin.insights.payingTenants", { count: billing.paidTenantCount });
  });

  readonly skuMix = computed((): SkuMixRow[] => {
    const billing = this.metrics();
    const bySku = new Map((billing?.skuMix ?? []).map((row) => [row.sku, row.count]));
    return (["starter", "professional", "business"] as const).map((sku) => ({
      key: sku,
      label: formatSubscriptionSku(sku),
      count: bySku.get(sku) ?? 0,
      tone: `sku-${sku}`,
    }));
  });

  readonly skuTotal = computed(() => this.skuMix().reduce((sum, row) => sum + row.count, 0));

  readonly pageDescription = computed(
    () =>
      this.tr("admin.insights.pageDescription"),
  );

  readonly formatMoney = formatAmountMinor;

  readonly periodLabel = computed(() =>
    this.tr(this.granularity() === "year" ? "admin.insights.yearly" : "admin.insights.monthly"),
  );

  readonly formatPeriod = (period: string) => formatRevenuePeriod(period, this.granularity());

  ngOnInit(): void {
    void this.load();
  }

  setGranularity(next: RevenueGranularity): void {
    if (this.granularity() === next) {
      return;
    }
    this.granularity.set(next);
    void this.loadRevenue();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [platformMrr, gmv, skuMixSource] = await this.api.withAuthorizedClient((c) =>
        Promise.all([
          c.getAdminPlatformSubscriptionMrr(),
          c.getAdminReportingGmv(this.granularity()),
          c.getAdminSubscriptionMetrics().catch(() => null),
        ]),
      );
      this.metrics.set(mapPlatformMrrToMetrics(platformMrr, skuMixSource));
      this.revenue.set(mapGmvToRevenueMetrics(gmv));
    } catch (err) {
      let message = adminLoadErrorMessage(this.tr("admin.insights.couldNotLoadRevenueMetrics"), err);
      if (message.includes("tenant_subscription_charges")) {
        message +=
          " Apply db/scripts/tenant-subscription-charges-sprint33.sql (or ./scripts/apply-tenant-subscription-charges.ps1).";
      }
      this.error.set(message);
      this.feedback.error(message);
      this.metrics.set(null);
      this.revenue.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  async loadRevenue(): Promise<void> {
    this.loading.set(true);
    try {
      const gmv = await this.api.withAuthorizedClient((c) =>
        c.getAdminReportingGmv(this.granularity()),
      );
      this.revenue.set(mapGmvToRevenueMetrics(gmv));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "admin.insights.couldNotLoadPeriodTotals"));
    } finally {
      this.loading.set(false);
    }
  }

  formatSaasBucket(row: AdminRevenuePeriodBucket): string {
    return formatAmountMinor(row.saasAmountMinor, row.currency);
  }

  formatCommerceFee(row: AdminRevenuePeriodBucket): string {
    return formatPrice(row.platformFeeTotal, row.currency);
  }

  formatCommerceVolume(row: AdminRevenuePeriodBucket): string {
    return formatPrice(row.commerceVolume, row.currency);
  }

  formatSaasCharge(row: {
    amountMinor?: number | null;
    currency?: string | null;
    status: string;
  }): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return formatBillingChargeStatus(row.status);
  }

  formatCommercePayment(row: {
    amount: number;
    platformFeeAmount: number;
    currency: string;
  }): string {
    return `${formatPrice(row.amount, row.currency)} · fee ${formatPrice(row.platformFeeAmount, row.currency)}`;
  }
}

function formatTotalsMinor(
  rows: CurrencyWindowTotals[],
  pick: (row: CurrencyWindowTotals) => number,
): string | null {
  const parts = rows
    .map((row) => {
      const amount = pick(row);
      if (amount <= 0) {
        return null;
      }
      return formatAmountMinor(amount, row.currency);
    })
    .filter((part): part is string => Boolean(part));
  return parts.length > 0 ? parts.join(" · ") : null;
}

function formatTotalsMajor(
  rows: CurrencyWindowTotals[],
  pick: (row: CurrencyWindowTotals) => number,
): string | null {
  const parts = rows
    .map((row) => {
      const amount = pick(row);
      if (amount <= 0) {
        return null;
      }
      return formatPrice(amount, row.currency);
    })
    .filter((part): part is string => Boolean(part));
  return parts.length > 0 ? parts.join(" · ") : null;
}

function mapPlatformMrrToMetrics(
  mrr: AdminPlatformSubscriptionMrr,
  skuMixSource: AdminSubscriptionMetrics | null,
): AdminSubscriptionMetrics {
  return {
    paidTenantCount: mrr.paidTenantCount,
    pastDueTenantCount: mrr.pastDueTenantCount,
    trialingTenantCount: mrr.trialingTenantCount,
    skuMix: skuMixSource?.skuMix ?? [],
    syncedMrrByCurrency: mrr.syncedMrrByCurrency,
    estimatedMrrByCurrency: mrr.estimatedMrrByCurrency,
    mrrSource: mrr.mrrSource,
    asOf: mrr.asOf,
  };
}

function mapGmvToRevenueMetrics(gmv: AdminGmvSummary): AdminRevenueMetrics {
  return {
    granularity: gmv.granularity,
    asOf: gmv.asOf,
    buckets: gmv.buckets.map((row) => ({
      period: row.period,
      currency: row.currency,
      saasAmountMinor: 0,
      commerceVolume: row.commerceVolume,
      platformFeeTotal: row.platformFeeTotal,
    })),
    recentSaasCharges: [],
    recentCommercePayments: [],
  };
}

function formatRevenuePeriod(period: string, granularity: RevenueGranularity): string {
  if (granularity === "year") {
    return period;
  }
  const match = /^(\d{4})-(\d{2})$/.exec(period.trim());
  if (!match) {
    return period;
  }
  const year = Number(match[1]);
  const month = Number(match[2]) - 1;
  if (Number.isNaN(year) || Number.isNaN(month) || month < 0 || month > 11) {
    return period;
  }
  return new Intl.DateTimeFormat("en", { month: "short", year: "numeric", timeZone: "UTC" }).format(
    new Date(Date.UTC(year, month, 1)),
  );
}
