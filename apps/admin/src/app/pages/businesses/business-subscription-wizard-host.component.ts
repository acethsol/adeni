import { Component, inject, signal } from "@angular/core";
import type { AdminBusinessSummary } from "@adeni/shared";
import { formatSubscriptionSku } from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniFeedbackService,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { BusinessDetailFacade } from "./business-detail.facade";
import { BusinessSubscriptionWizardComponent } from "./business-subscription-wizard.component";
import {
  buildCommercialTermsRequest,
  draftFromCommercialTerms,
  hasCommercialTermsDelta,
  resolveCommercialChangeReason,
  type BusinessSubscriptionDraft,
} from "./business-subscription.model";

@Component({
  selector: "app-business-subscription-wizard-host",
  standalone: true,
  imports: [BusinessSubscriptionWizardComponent],
  template: `
    <app-business-subscription-wizard
      [open]="subscriptionWizardOpen()"
      [business]="subscriptionBusiness()"
      [draft]="subscriptionDraft()"
      [saving]="subscriptionSaving()"
      (closed)="close()"
      (save)="save()"
      (draftChange)="patchDraft($event)"
    />
  `,
})
export class BusinessSubscriptionWizardHostComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly facade = inject(BusinessDetailFacade);
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly subscriptionDraft = signal<BusinessSubscriptionDraft | null>(null);
  readonly subscriptionSaving = signal(false);
  readonly subscriptionBusiness = signal<AdminBusinessSummary | null>(null);
  readonly subscriptionWizardOpen = signal(false);

  open(): void {
    const b = this.facade.business();
    const t = this.facade.terms();
    if (!b || !t) {
      return;
    }
    this.subscriptionBusiness.set(b);
    this.subscriptionDraft.set(draftFromCommercialTerms(t, b));
    this.subscriptionWizardOpen.set(true);
  }

  close(): void {
    this.subscriptionBusiness.set(null);
    this.subscriptionDraft.set(null);
    this.subscriptionSaving.set(false);
    this.subscriptionWizardOpen.set(false);
  }

  patchDraft(partial: Partial<BusinessSubscriptionDraft>): void {
    const current = this.subscriptionDraft();
    if (!current) {
      return;
    }
    this.subscriptionDraft.set({ ...current, ...partial });
  }

  async save(): Promise<void> {
    const business = this.subscriptionBusiness();
    const draft = this.subscriptionDraft();
    if (!business || !draft) {
      return;
    }

    if (hasCommercialTermsDelta(draft) && !resolveCommercialChangeReason(draft)) {
      this.feedback.error(this.tr("admin.businesses.addAuditNoteChangeReason"));
      return;
    }

    const body = buildCommercialTermsRequest(draft);
    const skuChanged = draft.subscriptionSku !== draft.initialSubscriptionSku;

    if (!(await this.confirmDialog.confirmKind("save", { subject: business.name }))) {
      return;
    }

    this.subscriptionSaving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const client = await this.api.createAuthorizedClient();
        await client.setAdminTenantCommercialTerms(business.id, body);
      }, this.tr("admin.businesses.savingSubscription"), business.name);
      this.close();
      await this.facade.refresh();
      this.feedback.success(
        skuChanged
          ? this.tr("admin.businesses.subscriptionUpdatedSku", { sku: formatSubscriptionSku(draft.subscriptionSku) })
          : this.tr("admin.businesses.subscriptionUpdated"),
        business.name,
      );
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "common.couldNotSaveSubscription"));
    } finally {
      this.subscriptionSaving.set(false);
    }
  }
}
