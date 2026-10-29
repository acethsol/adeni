import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import { Router } from "@angular/router";
import type { TenantSubscriptionCharge } from "@adeni/shared";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniPillComponent,
  type AdeniDataTableColumn,
  injectAdeniTranslate,
} from "@adeni/ui";
import { billingStatusPill, businessPlanPill } from "../../shared/admin-pill-maps";
import { chargeStatusTone, formatChargeAmount } from "./business-charge-grid";
import { BusinessDetailFacade } from "./business-detail.facade";

@Component({
  selector: "app-business-overview",
  standalone: true,
  imports: [
    DatePipe,
    AdeniCarbonIconComponent,
    AdeniPillComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
  ],
  templateUrl: "./business-overview.component.html",
  styleUrl: "./business-overview.component.scss",
})
export class BusinessOverviewComponent {
  protected readonly tr = injectAdeniTranslate();
  private readonly router = inject(Router);
  readonly facade = inject(BusinessDetailFacade);

  readonly businessPlanPill = businessPlanPill;
  readonly billingStatusPill = billingStatusPill;
  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
  readonly formatChargeAmount = formatChargeAmount;
  readonly chargeStatusTone = chargeStatusTone;

  readonly recentCharges = computed(() => this.facade.charges().slice(0, 5));

  readonly previewColumns = computed<AdeniDataTableColumn<TenantSubscriptionCharge>[]>(() => [
    {
      id: "when",
      header: this.tr("businessApp.plan.date"),
      cell: (row) => row.occurredAt,
      sortable: false,
    },
    {
      id: "amount",
      header: this.tr("businessApp.quotes.amount"),
      align: "right",
      cell: (row) => formatChargeAmount(row),
      sortable: false,
    },
    {
      id: "status",
      header: this.tr("common.status"),
      cell: () => "",
      sortable: false,
    },
  ]);

  readonly trackChargeRow = (row: TenantSubscriptionCharge): string => row.id;

  readonly effectiveMonthlyLabel = computed(() => {
    const t = this.facade.terms();
    if (t?.effectiveMonthlyAmount != null && t.currency) {
      return `${t.effectiveMonthlyAmount} ${t.currency}`;
    }
    const b = this.facade.business();
    return b ? formatSubscriptionSku(b.subscriptionSku) : "—";
  });

  readonly effectiveMonthlyHint = computed(() => {
    const t = this.facade.terms();
    if (!t) {
      return this.tr("admin.businesses.perCommercialTerms");
    }
    if (t.customSaasMonthlyAmount != null) {
      return this.tr("admin.businesses.customSaasOverride");
    }
    if (
      t.agreedMonthlyAmount != null &&
      t.currency &&
      t.effectiveMonthlyAmount != null &&
      t.agreedMonthlyAmount !== t.effectiveMonthlyAmount
    ) {
      return this.tr("admin.businesses.boundMo", { agreedMonthlyAmount: t.agreedMonthlyAmount, currency: t.currency });
    }
    if (
      t.catalogListPrice != null &&
      t.effectiveMonthlyAmount != null &&
      t.catalogListPrice === t.effectiveMonthlyAmount
    ) {
      return this.tr("admin.businesses.matchesCatalogList");
    }
    if (t.pricingCatalogVersion) {
      return t.pricingCatalogVersion;
    }
    return this.tr("admin.businesses.viewCommercialTerms");
  });

  readonly lastChargeHint = computed(() => {
    const latest = this.facade.charges()[0];
    if (!latest) {
      return this.tr("admin.insights.noChargesSynced");
    }
    const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
      new Date(latest.occurredAt),
    );
    return `${formatBillingChargeStatus(latest.status)} · ${when}`;
  });

  readonly auditHint = computed(() => {
    const n = this.facade.commercialAudit().length;
    return n > 0 ? `${n} event${n === 1 ? "" : "s"} on record` : this.tr("admin.businesses.openAuditTabHistory");
  });

  readonly overviewInsight = computed(() => {
    const b = this.facade.business();
    const t = this.facade.terms();
    if (!b || !t) {
      return this.tr("admin.businesses.loadingTenantSaasSnapshot");
    }
    if (b.billingStatus === "past_due") {
      return this.tr("admin.businesses.pastDueAlignPaymentProvider");
    }
    if (b.billingStatus === "trialing" && b.trialEndsAt) {
      return this.tr("admin.businesses.trialProgressConfirmEffectivePricing");
    }
    if (this.facade.charges().length === 0) {
      return this.tr("admin.businesses.healthyMonitorNoAdeniCharges");
    }
    return this.tr("admin.businesses.saasHealthAtGlanceUse");
  });

  navigateTab(segment: "terms" | "billing" | "audit"): void {
    const id = this.facade.business()?.id;
    if (id) {
      void this.router.navigate(["/businesses", id, segment]);
    }
  }
}
