import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { PricingCatalogVersionSummary } from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

type GmvCountryRow = {
  countryCode: string;
  platformFeePercent: number;
  isSelfServeEnabled: boolean;
};

@Component({
  selector: "app-admin-pricing-gmv",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./pricing-gmv.component.html",
  styleUrl: "./pricing-gmv.component.scss",
})
export class AdminPricingGmvComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(AdminApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly versions = signal<PricingCatalogVersionSummary[]>([]);
  readonly selectedCode = signal<string | null>(null);
  readonly rows = signal<GmvCountryRow[]>([]);
  readonly canEdit = computed(() => this.selectedStatus() === "draft");
  readonly selectedStatus = signal<string | null>(null);

  constructor() {
    void this.loadVersions();
  }

  async loadVersions(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.listAdminPricingCatalogVersions());
      this.versions.set(items);
      const draft = items.find((v) => v.status === "draft") ?? items.find((v) => v.isActive);
      if (draft) {
        await this.selectVersion(draft.code);
      }
    } catch (err) {
      this.error.set(adminLoadErrorMessage(this.tr("admin.pricing.couldNotLoadPricingVersions"), err));
    } finally {
      this.loading.set(false);
    }
  }

  async selectVersion(code: string): Promise<void> {
    this.selectedCode.set(code);
    try {
      const detail = await this.api.withAuthorizedClient((c) =>
        c.getAdminPricingCatalogVersion(code),
      );
      this.selectedStatus.set(detail.status);
      const byCountry = new Map<string, GmvCountryRow>();
      for (const entry of detail.entries) {
        const existing = byCountry.get(entry.countryCode);
        if (!existing || entry.subscriptionSku === "starter") {
          byCountry.set(entry.countryCode, {
            countryCode: entry.countryCode,
            platformFeePercent: entry.platformFeePercent,
            isSelfServeEnabled: entry.isSelfServeEnabled,
          });
        }
      }
      this.rows.set([...byCountry.values()].sort((a, b) => a.countryCode.localeCompare(b.countryCode)));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "admin.pricing.couldNotLoadGmvSettings"));
      this.rows.set([]);
    }
  }

  patchRow(countryCode: string, patch: Partial<GmvCountryRow>): void {
    this.rows.update((current) =>
      current.map((row) => (row.countryCode === countryCode ? { ...row, ...patch } : row)),
    );
  }

  async saveDraft(): Promise<void> {
    const code = this.selectedCode();
    if (!code || !this.canEdit()) {
      return;
    }
    if (!(await this.confirmDialog.confirmKind("save", { subject: code }))) {
      return;
    }
    this.saving.set(true);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const detail = await c.getAdminPricingCatalogVersion(code);
        const entries = detail.entries.map((entry) => {
          const gmv = this.rows().find((r) => r.countryCode === entry.countryCode);
          if (!gmv) {
            return {
              countryCode: entry.countryCode,
              subscriptionSku: entry.subscriptionSku,
              currencyCode: entry.currencyCode,
              monthlyAmountMajor: entry.monthlyAmountMajor ?? undefined,
              platformFeePercent: entry.platformFeePercent,
              isSelfServeEnabled: entry.isSelfServeEnabled,
            };
          }
          return {
            countryCode: entry.countryCode,
            subscriptionSku: entry.subscriptionSku,
            currencyCode: entry.currencyCode,
            monthlyAmountMajor: entry.monthlyAmountMajor ?? undefined,
            platformFeePercent: gmv.platformFeePercent,
            isSelfServeEnabled: gmv.isSelfServeEnabled,
          };
        });
        await c.upsertAdminPricingCatalogEntries(code, { entries });
      });
      this.feedback.success("GMV settings saved on draft catalog.");
      await this.selectVersion(code);
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "admin.pricing.couldNotSaveGmvSettings"));
    } finally {
      this.saving.set(false);
    }
  }
}
