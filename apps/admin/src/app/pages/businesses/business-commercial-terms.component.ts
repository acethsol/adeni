import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import { formatSubscriptionSku, formatTenantBillingStatus } from "@adeni/shared";
import { AdeniPillComponent, AdeniStaffButtonComponent, injectAdeniTranslate } from "@adeni/ui";
import { billingStatusPill, businessPlanPill } from "../../shared/admin-pill-maps";
import { BusinessDetailFacade } from "./business-detail.facade";
import { BusinessSubscriptionWizardHostComponent } from "./business-subscription-wizard-host.component";

@Component({
  selector: "app-admin-business-commercial-terms",
  standalone: true,
  imports: [
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniPillComponent,
    BusinessSubscriptionWizardHostComponent,
  ],
  templateUrl: "./business-commercial-terms.component.html",
  styleUrl: "./business-commercial-terms.component.scss",
})
export class BusinessCommercialTermsComponent {
  protected readonly tr = injectAdeniTranslate();
  readonly facade = inject(BusinessDetailFacade);

  readonly businessPlanPill = businessPlanPill;
  readonly billingStatusPill = billingStatusPill;
  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;

  readonly heroAmount = computed(() => {
    const t = this.facade.terms();
    if (t?.effectiveMonthlyAmount != null && t.currency) {
      return `${t.effectiveMonthlyAmount} ${t.currency}`;
    }
    if (t?.agreedMonthlyAmount != null && t.currency) {
      return `${t.agreedMonthlyAmount} ${t.currency}`;
    }
    const b = this.facade.business();
    return b ? formatSubscriptionSku(b.subscriptionSku) : "—";
  });

  readonly heroSubline = computed(() => {
    const t = this.facade.terms();
    if (!t) {
      return "Effective monthly revenue to Adeni";
    }
    if (t.customSaasMonthlyAmount != null) {
      return "Includes custom SaaS override on catalog pricing";
    }
    if (t.agreedMonthlyAmount != null) {
      return "Price-bound agreement in effect";
    }
    return "Catalog list price unless overridden";
  });

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
}
