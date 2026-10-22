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

  readonly previewColumns: AdeniDataTableColumn<TenantSubscriptionCharge>[] = [
    {
      id: "when",
      header: "Date",
      cell: (row) => row.occurredAt,
      sortable: false,
    },
    {
      id: "amount",
      header: "Amount",
      align: "right",
      cell: (row) => formatChargeAmount(row),
      sortable: false,
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortable: false,
    },
  ];

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
      return "Per commercial terms";
    }
    if (t.customSaasMonthlyAmount != null) {
      return "Custom SaaS override";
    }
    if (
      t.agreedMonthlyAmount != null &&
      t.currency &&
      t.effectiveMonthlyAmount != null &&
      t.agreedMonthlyAmount !== t.effectiveMonthlyAmount
    ) {
      return `Bound ${t.agreedMonthlyAmount} ${t.currency}/mo`;
    }
    if (
      t.catalogListPrice != null &&
      t.effectiveMonthlyAmount != null &&
      t.catalogListPrice === t.effectiveMonthlyAmount
    ) {
      return "Matches catalog list";
    }
    if (t.pricingCatalogVersion) {
      return t.pricingCatalogVersion;
    }
    return "View commercial terms";
  });

  readonly lastChargeHint = computed(() => {
    const latest = this.facade.charges()[0];
    if (!latest) {
      return "No charges synced";
    }
    const when = new Intl.DateTimeFormat(undefined, { dateStyle: "medium" }).format(
      new Date(latest.occurredAt),
    );
    return `${formatBillingChargeStatus(latest.status)} · ${when}`;
  });

  readonly auditHint = computed(() => {
    const n = this.facade.commercialAudit().length;
    return n > 0 ? `${n} event${n === 1 ? "" : "s"} on record` : "Open audit tab for history";
  });

  readonly overviewInsight = computed(() => {
    const b = this.facade.business();
    const t = this.facade.terms();
    if (!b || !t) {
      return "Loading tenant SaaS snapshot…";
    }
    if (b.billingStatus === "past_due") {
      return "Past due — align payment provider status with commercial terms before the next billing cycle.";
    }
    if (b.billingStatus === "trialing" && b.trialEndsAt) {
      return "Trial in progress — confirm effective pricing before trial converts to paid.";
    }
    if (this.facade.charges().length === 0) {
      return "Healthy to monitor — no Adeni charges yet; stub checkout or live billing will populate Billing.";
    }
    return "SaaS health at a glance — use the cards below or open Billing / Commercial audit for full grids.";
  });

  navigateTab(segment: "terms" | "billing" | "audit"): void {
    const id = this.facade.business()?.id;
    if (id) {
      void this.router.navigate(["/businesses", id, segment]);
    }
  }
}
