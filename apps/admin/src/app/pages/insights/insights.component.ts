import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { type AdminSubscriptionMetrics, formatAmountMinor } from "@adeni/shared";
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

@Component({
  selector: "app-admin-insights",
  standalone: true,
  imports: [
    PortalPageComponent,
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

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const data = await this.api.withAuthorizedClient((c) => c.getAdminSubscriptionMetrics());
        this.metrics.set(data);
      }, "Loading subscription metrics…", "SaaS billing");
    } catch (err) {
      let message = adminLoadErrorMessage("Could not load subscription metrics.", err);
      if (message.includes("tenant_subscription_charges")) {
        message +=
          " Apply db/scripts/tenant-subscription-charges-sprint33.sql (or ./scripts/apply-tenant-subscription-charges.ps1).";
      }
      this.error.set(message);
      this.feedback.error(message);
      this.metrics.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
