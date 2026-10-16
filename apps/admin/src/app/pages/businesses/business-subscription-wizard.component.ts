import { DatePipe } from "@angular/common";
import { Component, computed, inject, input, output, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type {
  AdminBusinessSummary,
  SubscriptionSku,
  TenantBillingStatus,
  TenantSubscriptionCharge,
} from "@adeni/shared";
import {
  SUBSCRIPTION_SKUS,
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
  formatTenantBillingStatus,
} from "@adeni/shared";
import {
  AdeniModalComponent,
  AdeniPortalEmptyStateComponent,
  AdeniWizardComponent,
  type AdeniWizardStep,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { billingStatusPill, businessPlanPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import type { BusinessSubscriptionDraft } from "./business-subscription.model";

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
    id: "history",
    label: "History",
    title: "Billing history",
    lede: "Adeni SaaS charges synced from checkout or provider webhooks — what this business has paid or attempted.",
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
    DatePipe,
    AdeniModalComponent,
    AdeniWizardComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
  ],
  templateUrl: "./business-subscription-wizard.component.html",
  styleUrl: "./business-subscription-wizard.component.scss",
})
export class BusinessSubscriptionWizardComponent {
  private readonly api = inject(AdminApiService);

  readonly open = input(false);
  readonly business = input<AdminBusinessSummary | null>(null);
  readonly draft = input<BusinessSubscriptionDraft | null>(null);
  readonly saving = input(false);

  readonly closed = output<void>();
  readonly save = output<void>();
  readonly draftChange = output<Partial<BusinessSubscriptionDraft>>();

  readonly wizardStep = signal(STEPS[0].id);
  readonly charges = signal<TenantSubscriptionCharge[]>([]);
  readonly chargesLoading = signal(false);
  readonly chargesError = signal<string | null>(null);
  private chargesLoadedForId: string | null = null;

  readonly steps = STEPS;
  readonly skus = SUBSCRIPTION_SKUS;
  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatAmountMinor = formatAmountMinor;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;
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
  readonly billingStatusPill = billingStatusPill;

  readonly skuChanged = computed(() => {
    const d = this.draft();
    return d != null && d.subscriptionSku !== d.initialSubscriptionSku;
  });

  readonly billingOwedHint = computed(() => {
    const b = this.business();
    const d = this.draft();
    if (!b || !d) {
      return null;
    }
    if (d.billingStatus === "past_due") {
      return "Billing is past due — collect through the payment provider or update status after payment.";
    }
    if (d.billingStatus === "trialing") {
      return "On trial — no recurring Adeni charge until trial ends or you end trial in Billing.";
    }
    if (d.billingStatus === "suspended" || d.billingStatus === "canceled") {
      return "Not in good standing — restore active billing after payment or commercial agreement.";
    }
    return null;
  });

  patch(partial: Partial<BusinessSubscriptionDraft>): void {
    this.draftChange.emit(partial);
  }

  onStepSelect(id: string): void {
    this.wizardStep.set(id);
    if (id === "history") {
      void this.loadCharges();
    }
  }

  back(): void {
    const idx = this.stepIndex();
    if (idx > 0) {
      const prev = STEPS[idx - 1].id;
      this.wizardStep.set(prev);
      if (prev === "history") {
        void this.loadCharges();
      }
    }
  }

  next(): void {
    const idx = this.stepIndex();
    if (idx < STEPS.length - 1) {
      const nextId = STEPS[idx + 1].id;
      this.wizardStep.set(nextId);
      if (nextId === "history") {
        void this.loadCharges();
      }
    }
  }

  onClose(): void {
    this.wizardStep.set(STEPS[0].id);
    this.charges.set([]);
    this.chargesError.set(null);
    this.chargesLoadedForId = null;
    this.closed.emit();
  }

  formatChargeAmount(row: TenantSubscriptionCharge): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return "—";
  }

  chargeSkuLabel(sku: SubscriptionSku | null | undefined): string {
    return sku ? formatSubscriptionSku(sku) : "—";
  }

  async loadCharges(): Promise<void> {
    const b = this.business();
    if (!b) {
      return;
    }
    if (this.chargesLoadedForId === b.id && !this.chargesError()) {
      return;
    }

    this.chargesLoading.set(true);
    this.chargesError.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) =>
        c.listAdminTenantSubscriptionCharges(b.id, 25),
      );
      this.charges.set(items);
      this.chargesLoadedForId = b.id;
    } catch {
      this.chargesError.set("Could not load billing history.");
      this.charges.set([]);
    } finally {
      this.chargesLoading.set(false);
    }
  }
}
