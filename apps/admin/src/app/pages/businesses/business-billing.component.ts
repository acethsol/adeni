import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
  formatTenantBillingStatus,
} from "@adeni/shared";
import {
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
} from "@adeni/ui";
import { BusinessDetailFacade } from "./business-detail.facade";
import { BusinessSubscriptionWizardHostComponent } from "./business-subscription-wizard-host.component";

@Component({
  selector: "app-admin-business-billing",
  standalone: true,
  imports: [
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    BusinessSubscriptionWizardHostComponent,
  ],
  templateUrl: "./business-billing.component.html",
  styleUrl: "./business-billing.component.scss",
})
export class BusinessBillingComponent {
  readonly facade = inject(BusinessDetailFacade);

  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;

  readonly billingOwedHint = computed(() => {
    const b = this.facade.business();
    if (!b?.billingStatus) {
      return null;
    }
    if (b.billingStatus === "past_due") {
      return "Past due — collect through the payment provider or update billing status after payment.";
    }
    if (b.billingStatus === "trialing") {
      return "On trial — recurring Adeni charges start when trial ends unless you change terms.";
    }
    if (b.billingStatus === "suspended" || b.billingStatus === "canceled") {
      return "Not in good standing — restore active billing after payment or agreement.";
    }
    return null;
  });

  formatChargeAmount(row: { amountMinor?: number | null; currency?: string | null }): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return "—";
  }
}
