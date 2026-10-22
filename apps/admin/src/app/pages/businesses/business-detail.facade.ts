import { inject, Injectable, signal } from "@angular/core";
import type {
  AdminBusinessSummary,
  AdminTenantCommercialTerms,
  TenantSubscriptionCharge,
} from "@adeni/shared";
import { AdeniFeedbackService } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

export type CommercialAuditEntry = {
  id: string;
  actorId: string;
  action: string;
  occurredAt: string;
  metadataJson?: string | null;
};

@Injectable()
export class BusinessDetailFacade {
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly business = signal<AdminBusinessSummary | null>(null);
  readonly terms = signal<AdminTenantCommercialTerms | null>(null);
  readonly charges = signal<TenantSubscriptionCharge[]>([]);
  readonly commercialAudit = signal<CommercialAuditEntry[]>([]);

  private loadedTenantId: string | null = null;

  async load(tenantId: string, options?: { silent?: boolean }): Promise<void> {
    if (!options?.silent) {
      this.loading.set(true);
    }
    this.error.set(null);
    try {
      const data = await this.api.withAuthorizedClient(async (c) => {
        const [business, terms, chargeItems, auditItems] = await Promise.all([
          c.getAdminBusiness(tenantId),
          c.getAdminTenantCommercialTerms(tenantId),
          c.listAdminTenantSubscriptionCharges(tenantId, 50),
          c.listAdminTenantCommercialAudit(tenantId, 100),
        ]);
        return { business, terms, chargeItems, auditItems };
      });
      this.loadedTenantId = tenantId;
      this.business.set(data.business);
      this.terms.set(data.terms);
      this.charges.set(data.chargeItems);
      this.commercialAudit.set(data.auditItems);
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load this business.", err);
      this.error.set(message);
      if (!options?.silent) {
        this.feedback.error(message);
      }
      this.business.set(null);
      this.terms.set(null);
      this.charges.set([]);
      this.commercialAudit.set([]);
      this.loadedTenantId = null;
    } finally {
      this.loading.set(false);
    }
  }

  ensureLoaded(tenantId: string): void {
    if (this.loadedTenantId === tenantId && this.business()) {
      return;
    }
    void this.load(tenantId);
  }

  async refresh(): Promise<void> {
    const id = this.business()?.id ?? this.loadedTenantId;
    if (!id) {
      return;
    }
    await this.load(id, { silent: true });
  }
}
