import { Component, computed, input, output, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { AdminBusinessSummary, SubscriptionSku, TenantBillingStatus } from "@adeni/shared";
import {
  SUBSCRIPTION_SKUS,
  applyTenantSaasDiscount,
  formatPrice,
  formatSubscriptionSku,
  getSkuMonthlyAmount,
  resolveTenantSaasListPrice,
} from "@adeni/shared";
import {
  AdeniModalComponent,
  AdeniStaffButtonComponent,
  AdeniWizardComponent,
  type AdeniWizardStep,
} from "@adeni/ui";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { businessPlanPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import {
  hasCommercialTermsDelta,
  resolveCommercialChangeReason,
  type BusinessSubscriptionDraft,
} from "./business-subscription.model";

const STEPS: AdeniWizardStep[] = [
  {
    id: "plan",
    label: "Plan",
    title: "Product SKU",
    lede: "Starter, Professional, or Business — entitlements and country list pricing. No perpetual free plan; use trial or suspend instead.",
  },
  {
    id: "billing",
    label: "Billing",
    title: "Billing & trial",
    lede: "Subscription billing state and optional trial before the tenant is charged.",
  },
  {
    id: "pricing",
    label: "Pricing",
    title: "Overrides",
    lede: "Discounts, negotiated monthly price, and an audit note for compliance.",
  },
  {
    id: "review",
    label: "Review",
    title: "Review changes",
    lede: "Confirm everything before updating this tenant.",
  },
];

@Component({
  selector: "app-business-subscription-wizard",
  standalone: true,
  imports: [
    FormsModule,
    AdeniStaffButtonComponent,
    AdeniModalComponent,
    AdeniWizardComponent,
    AdminPillComponent,
  ],
  templateUrl: "./business-subscription-wizard.component.html",
  styleUrl: "./business-subscription-wizard.component.scss",
})
export class BusinessSubscriptionWizardComponent {
  readonly open = input(false);
  readonly business = input<AdminBusinessSummary | null>(null);
  readonly draft = input<BusinessSubscriptionDraft | null>(null);
  readonly saving = input(false);

  readonly closed = output<void>();
  readonly save = output<void>();
  readonly draftChange = output<Partial<BusinessSubscriptionDraft>>();

  readonly wizardStep = signal(STEPS[0].id);

  readonly steps = STEPS;
  readonly skus = SUBSCRIPTION_SKUS;
  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly billingStatuses: TenantBillingStatus[] = [
    "trialing",
    "active",
    "past_due",
    "suspended",
    "canceled",
  ];

  readonly stepIndex = computed(() => {
    const idx = STEPS.findIndex((s) => s.id === this.wizardStep());
    return idx >= 0 ? idx : 0;
  });

  readonly isFirstStep = computed(() => this.stepIndex() === 0);
  readonly isLastStep = computed(() => this.stepIndex() === STEPS.length - 1);

  readonly tenantStatusPill = tenantStatusPill;
  readonly businessPlanPill = businessPlanPill;

  readonly skuChanged = computed(() => {
    const d = this.draft();
    return d != null && d.subscriptionSku !== d.initialSubscriptionSku;
  });

  readonly pricingPreview = computed(() => {
    const d = this.draft();
    if (!d) {
      return null;
    }
    const country = d.initialTerms.billingCountryCode ?? "NG";
    const currency = d.initialTerms.currency ?? "NGN";
    const catalogAmount = getSkuMonthlyAmount(country, d.subscriptionSku);
    const override =
      !d.clearCustomSaasPrice && d.customSaasMonthlyAmount != null && d.customSaasCurrency.trim()
        ? {
            customSaasMonthlyAmount: d.customSaasMonthlyAmount,
            customSaasCurrency: d.customSaasCurrency.trim(),
            customSaasSku: d.customSaasSku || null,
          }
        : null;
    const list = resolveTenantSaasListPrice(country, d.subscriptionSku, override);
    if (!list) {
      return { country, currency, catalogAmount, listAmount: null, effectiveAmount: null };
    }
    const discount =
      !d.clearDiscounts
        ? {
            discountPercent: d.discountPercent,
            discountFixedAmount: d.discountFixedAmount,
            discountCurrency: d.discountCurrency.trim() || null,
          }
        : null;
    const effective = applyTenantSaasDiscount(list.amount, discount);
    return {
      country,
      currency: list.currency,
      catalogAmount,
      listAmount: list.amount,
      effectiveAmount: effective,
    };
  });

  readonly formatPrice = formatPrice;

  readonly requiresChangeReason = computed(() => {
    const d = this.draft();
    return d != null && hasCommercialTermsDelta(d) && !resolveCommercialChangeReason(d);
  });

  patch(partial: Partial<BusinessSubscriptionDraft>): void {
    this.draftChange.emit(partial);
  }

  onStepSelect(id: string): void {
    this.wizardStep.set(id);
  }

  back(): void {
    const idx = this.stepIndex();
    if (idx > 0) {
      this.wizardStep.set(STEPS[idx - 1].id);
    }
  }

  next(): void {
    const idx = this.stepIndex();
    if (idx < STEPS.length - 1) {
      this.wizardStep.set(STEPS[idx + 1].id);
    }
  }

  onClose(): void {
    this.wizardStep.set(STEPS[0].id);
    this.closed.emit();
  }
}
