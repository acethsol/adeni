import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { PricingCatalogVersionSummary } from "@adeni/shared";
import { formatPrice, formatSubscriptionSku, type SubscriptionSku } from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniExportMenuItem,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { skuPill } from "../../shared/admin-pill-maps";

type PricingCatalogEntryRow = {
  rowKey: string;
  catalogVersionCode: string;
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
    RouterLink,
    DatePipe,
    FormsModule,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniExportMenuComponent,
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

  readonly editOpen = signal(false);
  readonly editSaving = signal(false);
  readonly editCountry = signal("");
  readonly editSku = signal<SubscriptionSku>("starter");
  readonly editCurrency = signal("");
  readonly editMonthlyAmount = signal("");
  readonly editPlatformFee = signal(0);
  readonly editSelfServe = signal(false);

  readonly canEditEntries = computed(() => this.selectedStatus() === "draft");

  readonly exportItems: AdeniExportMenuItem[] = [
    {
      format: "csv",
      label: "CSV (.csv)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "xlsx",
      label: "Excel (.xlsx)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "pdf",
      label: "PDF (.pdf)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
  ];

  readonly skuPill = skuPill;

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
        row.catalogVersionCode.toLowerCase().includes(query) ||
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
    {
      id: "code",
      header: "Version",
      cell: (v) => v.code,
      sortValue: (v) => v.code.toLowerCase(),
    },
    {
      id: "effective",
      header: "Effective",
      cell: () => "",
      sortValue: (v) => v.effectiveFrom,
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortValue: (v) => v.status,
    },
    {
      id: "active",
      header: "Live",
      cell: () => "",
      sortValue: (v) => (v.isActive ? 1 : 0),
    },
  ];

  readonly entryColumns: AdeniDataTableColumn<PricingCatalogEntryRow>[] = [
    {
      id: "version",
      header: "Catalog version",
      cell: (row) => row.catalogVersionCode,
      sortValue: (row) => row.catalogVersionCode.toLowerCase(),
    },
    {
      id: "country",
      header: "Country",
      cell: (row) => row.countryCode,
      sortValue: (row) => row.countryCode,
    },
    {
      id: "sku",
      header: "Plan (SKU)",
      cell: (row) => formatSubscriptionSku(row.subscriptionSku),
      sortValue: (row) => row.subscriptionSku,
    },
    {
      id: "price",
      header: "List price",
      align: "right",
      cell: () => "",
      sortValue: (row) => row.monthlyAmountMajor ?? 0,
    },
    {
      id: "gmv",
      header: "Platform fee",
      cell: (row) => `${row.platformFeePercent}%`,
      sortValue: (row) => row.platformFeePercent,
    },
    {
      id: "selfServe",
      header: "Self-serve",
      cell: () => "",
      sortValue: (row) => (row.isSelfServeEnabled ? 1 : 0),
    },
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
          catalogVersionCode: code,
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
    const effectiveDate = new Date(effectiveRaw);
    if (Number.isNaN(effectiveDate.getTime())) {
      this.feedback.error("Effective from date is not valid.");
      return;
    }
    const effectiveFrom = effectiveDate.toISOString();

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
    } catch (err) {
      this.feedback.error(
        adminLoadErrorMessage("Could not create catalog version.", err),
      );
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
    } catch (err) {
      this.feedback.error(
        adminLoadErrorMessage("Could not activate catalog version.", err),
      );
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

  openEditEntry(row: PricingCatalogEntryRow): void {
    if (!this.canEditEntries()) {
      return;
    }
    this.editCountry.set(row.countryCode);
    this.editSku.set(row.subscriptionSku);
    this.editCurrency.set(row.currencyCode);
    this.editMonthlyAmount.set(
      row.monthlyAmountMajor == null ? "" : String(row.monthlyAmountMajor),
    );
    this.editPlatformFee.set(row.platformFeePercent);
    this.editSelfServe.set(row.isSelfServeEnabled);
    this.editOpen.set(true);
  }

  closeEditEntry(): void {
    this.editOpen.set(false);
    this.editSaving.set(false);
  }

  async submitEditEntry(): Promise<void> {
    const code = this.selectedCode();
    if (!code || !this.canEditEntries()) {
      return;
    }
    const currency = this.editCurrency().trim().toUpperCase();
    if (currency.length !== 3) {
      this.feedback.error("Currency must be a 3-letter ISO code.");
      return;
    }
    const amountRaw = this.editMonthlyAmount().trim();
    let monthlyAmountMajor: number | undefined;
    if (amountRaw !== "") {
      const parsed = Number(amountRaw);
      if (Number.isNaN(parsed) || parsed < 0) {
        this.feedback.error("List price must be a non-negative number.");
        return;
      }
      monthlyAmountMajor = parsed;
    }
    const platformFee = this.editPlatformFee();
    if (platformFee < 0 || platformFee > 100) {
      this.feedback.error("Platform fee must be between 0 and 100.");
      return;
    }

    const countryCode = this.editCountry();
    const subscriptionSku = this.editSku();

    this.editSaving.set(true);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const detail = await client.getAdminPricingCatalogVersion(code);
        const entries = detail.entries.map((entry) => {
          if (
            entry.countryCode !== countryCode ||
            entry.subscriptionSku !== subscriptionSku
          ) {
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
            currencyCode: currency,
            monthlyAmountMajor,
            platformFeePercent: platformFee,
            isSelfServeEnabled: this.editSelfServe(),
          };
        });
        await client.upsertAdminPricingCatalogEntries(code, { entries });
      });
      this.feedback.success("Catalog entry updated.", `${countryCode} · ${subscriptionSku}`);
      this.closeEditEntry();
      await this.loadEntries(code);
    } catch (err) {
      this.feedback.error(
        adminLoadErrorMessage("Could not save catalog entry.", err),
      );
    } finally {
      this.editSaving.set(false);
    }
  }
}
