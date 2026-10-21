import { DatePipe, LowerCasePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
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
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly metrics = signal<AdminSubscriptionMetrics | null>(null);
  readonly revenue = signal<AdminRevenueMetrics | null>(null);
  readonly granularity = signal<RevenueGranularity>("month");

  readonly windowLabel = computed(() =>
    this.granularity() === "year" ? "Last 5 years" : "Last 12 months",
  );

  readonly granularityLabel = computed(() =>
    this.granularity() === "year" ? "Yearly" : "Monthly",
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
      ? "From synced subscription charges this month"
      : "Estimated from configured list prices";
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
      return `No platform earnings in the ${this.windowLabel().toLowerCase()} yet.`;
    }
    if (saas && fees) {
      return `${saas} subscription revenue · ${fees} in platform fees.`;
    }
    if (saas) {
      return `${saas} in subscription revenue (${this.windowLabel().toLowerCase()}).`;
    }
    return `${fees} in platform fees (${this.windowLabel().toLowerCase()}).`;
  });

  readonly revenueInsight = computed(() => {
    const billing = this.metrics();
    if (!billing) {
      return "";
    }
    if (billing.pastDueTenantCount > 0) {
      return `${billing.pastDueTenantCount} tenant(s) past due — follow up in billing.`;
    }
    if (billing.paidTenantCount === 0 && billing.trialingTenantCount > 0) {
      return `${billing.trialingTenantCount} on trial; no paying Pro/Business tenants yet.`;
    }
    if (!this.hasCommerceActivity()) {
      return "Commerce GMV is quiet — SaaS subscription sync is carrying platform revenue.";
    }
    return `${billing.paidTenantCount} paying tenant(s) · commerce checkouts included below.`;
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
      "Platform-side subscription revenue and commerce fees — not customer wallet balances.",
  );

  readonly formatMoney = formatAmountMinor;

  readonly periodLabel = computed(() =>
    this.granularity() === "year" ? "Yearly" : "Monthly",
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
      const [subscription, revenueData] = await this.api.withAuthorizedClient((c) =>
        Promise.all([
          c.getAdminSubscriptionMetrics(),
          c.getAdminRevenueMetrics(this.granularity()),
        ]),
      );
      this.metrics.set(subscription);
      this.revenue.set(revenueData);
    } catch (err) {
      let message = adminLoadErrorMessage("Could not load revenue metrics.", err);
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
      const revenueData = await this.api.withAuthorizedClient((c) =>
        c.getAdminRevenueMetrics(this.granularity()),
      );
      this.revenue.set(revenueData);
    } catch {
      this.feedback.error("Could not load period totals.");
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
