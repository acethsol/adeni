import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  type AdminRevenueMetrics,
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
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

const SKU_LABELS: Record<string, string> = {
  starter: "Starter",
  professional: "Professional",
  business: "Business",
};

type RevenueGranularity = "month" | "year";

@Component({
  selector: "app-admin-insights",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    DatePipe,
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

  readonly skuLabel = (sku: string) => SKU_LABELS[sku] ?? sku;

  readonly formatMoney = formatAmountMinor;

  readonly periodLabel = computed(() =>
    this.granularity() === "year" ? "Yearly" : "Monthly",
  );

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

  formatSaasBucket(row: { saasAmountMinor: number; currency: string }): string {
    return formatAmountMinor(row.saasAmountMinor, row.currency);
  }

  formatCommerceFee(row: { platformFeeTotal: number; currency: string }): string {
    return formatPrice(row.platformFeeTotal, row.currency);
  }

  formatCommerceVolume(row: { commerceVolume: number; currency: string }): string {
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

  readonly formatSubscriptionSku = formatSubscriptionSku;
}
