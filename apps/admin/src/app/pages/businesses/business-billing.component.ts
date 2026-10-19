import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type {
  AdminBusinessSummary,
  AdminTenantCommercialTerms,
  TenantSubscriptionCharge,
} from "@adeni/shared";
import {
  formatAmountMinor,
  formatBillingChargeStatus,
  formatSubscriptionSku,
  formatTenantBillingStatus,
} from "@adeni/shared";
import {
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { billingStatusPill, businessPlanPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import { BusinessSubscriptionWizardComponent } from "./business-subscription-wizard.component";
import {
  buildCommercialTermsRequest,
  draftFromCommercialTerms,
  hasCommercialTermsDelta,
  resolveCommercialChangeReason,
  type BusinessSubscriptionDraft,
} from "./business-subscription.model";

@Component({
  selector: "app-admin-business-billing",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
    BusinessSubscriptionWizardComponent,
  ],
  templateUrl: "./business-billing.component.html",
  styleUrl: "./business-billing.component.scss",
})
export class BusinessBillingComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly business = signal<AdminBusinessSummary | null>(null);
  readonly terms = signal<AdminTenantCommercialTerms | null>(null);
  readonly charges = signal<TenantSubscriptionCharge[]>([]);

  readonly subscriptionDraft = signal<BusinessSubscriptionDraft | null>(null);
  readonly subscriptionSaving = signal(false);
  readonly subscriptionWizardOpen = computed(
    () => this.subscriptionBusiness() != null && this.subscriptionDraft() != null,
  );
  readonly subscriptionBusiness = signal<AdminBusinessSummary | null>(null);

  readonly tenantStatusPill = tenantStatusPill;
  readonly businessPlanPill = businessPlanPill;
  readonly billingStatusPill = billingStatusPill;
  readonly formatSubscriptionSku = formatSubscriptionSku;
  readonly formatTenantBillingStatus = formatTenantBillingStatus;
  readonly formatBillingChargeStatus = formatBillingChargeStatus;

  readonly billingOwedHint = computed(() => {
    const b = this.business();
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

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get("tenantId");
      if (id) {
        void this.load(id);
      }
    });
  }

  async load(tenantId: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const data = await this.api.withAuthorizedClient(async (c) => {
          const [business, terms, chargeItems] = await Promise.all([
            c.getAdminBusiness(tenantId),
            c.getAdminTenantCommercialTerms(tenantId),
            c.listAdminTenantSubscriptionCharges(tenantId, 50),
          ]);
          return { business, terms, chargeItems };
        });
        this.business.set(data.business);
        this.terms.set(data.terms);
        this.charges.set(data.chargeItems);
      }, "Loading billing…");
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load billing for this business.", err);
      this.error.set(message);
      this.feedback.error(message);
      this.business.set(null);
      this.terms.set(null);
      this.charges.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  formatChargeAmount(row: TenantSubscriptionCharge): string {
    if (row.amountMinor != null && row.currency) {
      return formatAmountMinor(row.amountMinor, row.currency);
    }
    return "—";
  }

  async openSubscriptionWizard(): Promise<void> {
    const b = this.business();
    const t = this.terms();
    if (!b || !t) {
      return;
    }
    this.subscriptionBusiness.set(b);
    this.subscriptionDraft.set(draftFromCommercialTerms(t, b));
  }

  closeSubscriptionWizard(): void {
    this.subscriptionBusiness.set(null);
    this.subscriptionDraft.set(null);
    this.subscriptionSaving.set(false);
  }

  patchSubscriptionDraft(partial: Partial<BusinessSubscriptionDraft>): void {
    const current = this.subscriptionDraft();
    if (!current) {
      return;
    }
    this.subscriptionDraft.set({ ...current, ...partial });
  }

  async saveSubscription(): Promise<void> {
    const business = this.subscriptionBusiness();
    const draft = this.subscriptionDraft();
    if (!business || !draft) {
      return;
    }

    if (hasCommercialTermsDelta(draft) && !resolveCommercialChangeReason(draft)) {
      this.feedback.error("Add an audit note or change reason before saving commercial updates.");
      return;
    }

    const body = buildCommercialTermsRequest(draft);
    const skuChanged = draft.subscriptionSku !== draft.initialSubscriptionSku;

    this.subscriptionSaving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const client = await this.api.createAuthorizedClient();
        await client.setAdminTenantCommercialTerms(business.id, body);
      }, "Saving subscription…", business.name);
      this.closeSubscriptionWizard();
      await this.load(business.id);
      this.feedback.success(
        skuChanged
          ? `Subscription updated (${formatSubscriptionSku(draft.subscriptionSku)}).`
          : "Subscription updated.",
        business.name,
      );
    } catch {
      this.feedback.error("Could not save subscription.");
    } finally {
      this.subscriptionSaving.set(false);
    }
  }
}
