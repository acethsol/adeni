import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import { Router, RouterLink } from "@angular/router";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
  formatTenantBillingStatus,
} from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniStaffButtonComponent } from "@adeni/ui";
import { BusinessDetailFacade } from "./business-detail.facade";
import { BusinessSubscriptionWizardHostComponent } from "./business-subscription-wizard-host.component";

@Component({
  selector: "app-business-overview",
  standalone: true,
  imports: [
    RouterLink,
    DatePipe,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    BusinessSubscriptionWizardHostComponent,
  ],
  templateUrl: "./business-overview.component.html",
  styleUrl: "./business-overview.component.scss",
})
export class BusinessOverviewComponent {
  private readonly router = inject(Router);
  readonly facade = inject(BusinessDetailFacade);

  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;

  readonly recentCharges = computed(() => this.facade.charges().slice(0, 5));

  readonly overviewInsight = computed(() => {
    const b = this.facade.business();
    const t = this.facade.terms();
    if (!b || !t) {
      return "";
    }
    if (b.billingStatus === "past_due") {
      return "Billing is past due — review subscription and payment provider.";
    }
    if (b.billingStatus === "trialing" && b.trialEndsAt) {
      return `Trial active — recurring charges expected after trial ends.`;
    }
    if (this.facade.charges().length === 0) {
      return "No Adeni charges synced yet — stub checkout or live billing will appear under Billing.";
    }
    return `${this.facade.charges().length} charge(s) on record · latest terms in Billing tab.`;
  });

  formatChargeAmount(row: { amountMinor?: number | null; currency?: string | null }): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return "—";
  }

  openBillingTab(): void {
    const id = this.facade.business()?.id;
    if (id) {
      void this.router.navigate(["/businesses", id, "billing"]);
    }
  }
}
