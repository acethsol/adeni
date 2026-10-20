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

  readonly mrrRows = computed(() => {
    const data = this.metrics();
    if (!data) {
      return [];
    }
    return data.mrrSource === "synced_charges"
      ? data.syncedMrrByCurrency
      : data.estimatedMrrByCurrency;
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

  readonly saasWindowLabel = computed(() => formatTotalsMinor(this.windowTotals(), (row) => row.saasAmountMinor));

  readonly feesWindowLabel = computed(() =>
    formatTotalsMajor(this.windowTotals(), (row) => row.platformFeeTotal),
  );

  readonly gmvWindowLabel = computed(() =>
    formatTotalsMajor(this.windowTotals(), (row) => row.commerceVolume),
  );

  readonly earningsHeadline = computed(() => {
    const saas = this.saasWindowLabel();
    const fees = this.feesWindowLabel();
    if (!saas && !fees) {
      return `No synced SaaS or commerce earnings in the ${this.windowLabel().toLowerCase()} yet.`;
    }
    const parts: string[] = [];
    if (saas) {
      parts.push(`${saas} SaaS`);
    }
    if (fees) {
      parts.push(`${fees} platform fees`);
    }
    return `${parts.join(" · ")} (${this.windowLabel().toLowerCase()}).`;
  });

  readonly pageDescription = computed(
    () =>
      "Adeni earnings: subscription charges from businesses and platform fees on completed checkout. Customer GMV is shown for context.",
  );

  readonly formatMoney = formatAmountMinor;

  readonly periodLabel = computed(() =>
    this.granularity() === "year" ? "Yearly" : "Monthly",
  );

  readonly skuLabel = (sku: string) => {
    try {
      return formatSubscriptionSku(sku as "starter" | "professional" | "business");
    } catch {
      return sku;
    }
  };

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
      await this.feedback.runLoading(async () => {
        const [subscription, revenueData] = await this.api.withAuthorizedClient((c) =>
          Promise.all([
            c.getAdminSubscriptionMetrics(),
            c.getAdminRevenueMetrics(this.granularity()),
          ]),
        );
        this.metrics.set(subscription);
        this.revenue.set(revenueData);
      }, "Loading revenue…", "Platform billing");
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
    try {
      const revenueData = await this.api.withAuthorizedClient((c) =>
        c.getAdminRevenueMetrics(this.granularity()),
      );
      this.revenue.set(revenueData);
    } catch {
      this.feedback.error("Could not load period totals.");
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
