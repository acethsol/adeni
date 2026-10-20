import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import type { PricingCatalogVersionSummary } from "@adeni/shared";
import { formatPrice, formatSubscriptionSku, type SubscriptionSku } from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";

type PricingCatalogEntryRow = {
  rowKey: string;
  countryCode: string;
  subscriptionSku: SubscriptionSku;
  currencyCode: string;
  monthlyAmountMajor: number | null | undefined;
  platformFeePercent: number;
  isSelfServeEnabled: boolean;
};

@Component({
  selector: "app-admin-pricing-catalog",
  standalone: true,
  imports: [
    PortalPageComponent,
    DatePipe,
    FormsModule,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniPillComponent,
    AdeniIconBtnComponent,
    AdeniRowActionBarComponent,
  ],
  templateUrl: "./pricing-catalog.component.html",
  styleUrl: "./pricing-catalog.component.scss",
})
export class AdminPricingCatalogComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly versions = signal<PricingCatalogVersionSummary[]>([]);
  readonly selectedCode = signal<string | null>(null);
  readonly entryRows = signal<PricingCatalogEntryRow[]>([]);
  readonly selectedStatus = signal<string | null>(null);
  readonly selectedIsActive = signal(false);
  readonly entriesLoading = signal(false);
  readonly searchText = signal("");
  readonly versionSearchText = signal("");

  readonly createOpen = signal(false);
  readonly createSaving = signal(false);
  readonly createCode = signal("");
  readonly createEffectiveFrom = signal("");
  readonly createCloneFromActive = signal(true);

  readonly activeVersion = computed(
    () => this.versions().find((v) => v.isActive) ?? null,
  );

  readonly filteredVersions = computed(() => {
    const query = this.versionSearchText().trim().toLowerCase();
    const items = this.versions();
    if (!query) {
      return items;
    }
    return items.filter(
      (v) =>
        v.code.toLowerCase().includes(query) ||
        v.status.toLowerCase().includes(query),
    );
  });

  readonly filteredEntryRows = computed(() => {
    const query = this.searchText().trim().toLowerCase();
    const items = this.entryRows();
    if (!query) {
      return items;
    }
    return items.filter((row) => {
      const skuLabel = formatSubscriptionSku(row.subscriptionSku).toLowerCase();
      return (
        row.countryCode.toLowerCase().includes(query) ||
        row.subscriptionSku.toLowerCase().includes(query) ||
        skuLabel.includes(query) ||
        row.currencyCode.toLowerCase().includes(query)
      );
    });
  });

  readonly isEmptyVersions = computed(
    () => !this.loading() && this.versions().length === 0 && !this.error(),
  );

  readonly versionColumns: AdeniDataTableColumn<PricingCatalogVersionSummary>[] = [
    { id: "code", header: "Version", cell: (v) => v.code },
    { id: "effective", header: "Effective", cell: () => "" },
    { id: "status", header: "Status", cell: () => "" },
    { id: "active", header: "Live", cell: () => "" },
  ];

  readonly entryColumns: AdeniDataTableColumn<PricingCatalogEntryRow>[] = [
    { id: "country", header: "Country", cell: (row) => row.countryCode },
    { id: "sku", header: "Plan (SKU)", cell: (row) => formatSubscriptionSku(row.subscriptionSku) },
    { id: "price", header: "List price", cell: () => "" },
    { id: "gmv", header: "Platform fee", cell: (row) => `${row.platformFeePercent}%` },
    { id: "selfServe", header: "Self-serve", cell: () => "" },
  ];

  readonly trackVersion = (v: PricingCatalogVersionSummary): string => v.code;
  readonly trackEntryRow = (row: PricingCatalogEntryRow): string => row.rowKey;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const version = params.get("version");
      this.selectedCode.set(version);
      if (version) {
        void this.loadEntries(version);
      } else {
        this.entryRows.set([]);
        this.selectedStatus.set(null);
        this.selectedIsActive.set(false);
      }
      void this.loadVersions();
    });
  }

  async loadVersions(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const list = await this.api.withAuthorizedClient((client) =>
        client.listAdminPricingCatalogVersions(),
      );
      this.versions.set(list);

      const selected = this.selectedCode();
      if (!selected && list.length > 0) {
        const active = list.find((v) => v.isActive) ?? list[0]!;
        void this.selectVersion(active.code);
      }
    } catch (err) {
      this.versions.set([]);
      this.error.set(adminLoadErrorMessage("Could not load catalog versions.", err));
    } finally {
      this.loading.set(false);
    }
  }

  async loadEntries(code: string): Promise<void> {
    this.entriesLoading.set(true);
    try {
      const detail = await this.api.withAuthorizedClient((client) =>
        client.getAdminPricingCatalogVersion(code),
      );
      this.selectedStatus.set(detail.status);
      this.selectedIsActive.set(detail.isActive);
      this.entryRows.set(
        detail.entries.map((entry) => ({
          rowKey: `${code}:${entry.countryCode}:${entry.subscriptionSku}`,
          countryCode: entry.countryCode,
          subscriptionSku: entry.subscriptionSku,
          currencyCode: entry.currencyCode,
          monthlyAmountMajor: entry.monthlyAmountMajor,
          platformFeePercent: entry.platformFeePercent,
          isSelfServeEnabled: entry.isSelfServeEnabled,
        })),
      );
    } catch (err) {
      this.entryRows.set([]);
      this.feedback.error(adminLoadErrorMessage("Could not load catalog entries.", err));
    } finally {
      this.entriesLoading.set(false);
    }
  }

  selectVersion(code: string): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { version: code },
      queryParamsHandling: "merge",
    });
  }

  onVersionSearch(value: string): void {
    this.versionSearchText.set(value);
  }

  onEntrySearch(value: string): void {
    this.searchText.set(value);
  }

  openCreate(): void {
    const today = new Date();
    today.setUTCHours(0, 0, 0, 0);
    this.createCode.set("");
    this.createEffectiveFrom.set(today.toISOString().slice(0, 16));
    this.createCloneFromActive.set(true);
    this.createOpen.set(true);
  }

  closeCreate(): void {
    this.createOpen.set(false);
    this.createSaving.set(false);
  }

  async submitCreate(): Promise<void> {
    const code = this.createCode().trim();
    if (!code) {
      this.feedback.error("Version code is required.");
      return;
    }
    const effectiveRaw = this.createEffectiveFrom().trim();
    if (!effectiveRaw) {
      this.feedback.error("Effective from date is required.");
      return;
    }
    const effectiveFrom = new Date(effectiveRaw).toISOString();

    this.createSaving.set(true);
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((client) =>
            client.createAdminPricingCatalogVersion({
              code,
              effectiveFrom,
              effectiveTo: null,
              cloneFromActive: this.createCloneFromActive(),
            }),
          ),
        "Creating draft…",
        code,
      );
      this.feedback.success("Draft catalog version created.", code);
      this.closeCreate();
      await this.loadVersions();
      this.selectVersion(code);
    } catch {
      this.feedback.error("Could not create catalog version.");
    } finally {
      this.createSaving.set(false);
    }
  }

  async activateSelected(): Promise<void> {
    const code = this.selectedCode();
    if (!code || this.selectedStatus() !== "draft") {
      return;
    }
    const ok = await this.confirmDialog.confirm({
      title: "Activate catalog version?",
      message: `Publish ${code} as the live price book? The current active version will be retired.`,
      confirmLabel: "Activate",
      danger: false,
    });
    if (!ok) {
      return;
    }

    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((client) =>
            client.activateAdminPricingCatalogVersion(code),
          ),
        "Activating…",
        code,
      );
      this.feedback.success(`${code} is now the active catalog.`, "Catalog published");
      await this.loadVersions();
      await this.loadEntries(code);
    } catch {
      this.feedback.error("Could not activate catalog version.");
    }
  }

  formatSku(sku: SubscriptionSku): string {
    return formatSubscriptionSku(sku);
  }

  formatAmount(currency: string, amount: number | null | undefined): string {
    if (amount == null) {
      return "—";
    }
    return formatPrice(amount, currency);
  }

  statusPillTone(status: string): "success" | "info" | "neutral" | "warning" {
    if (status === "active") {
      return "success";
    }
    if (status === "draft") {
      return "warning";
    }
    return "neutral";
  }
}
