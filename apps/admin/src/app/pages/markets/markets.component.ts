import { Component, computed, inject, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { listSortParamsFromColumn, type AdminMarket } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniStaffButtonComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { marketLivePill } from "../../shared/admin-pill-maps";
import {
  MarketFormModalComponent,
  type MarketFormMode,
  type MarketFormValue,
} from "./market-form-modal.component";

@Component({
  selector: "app-admin-markets",
  standalone: true,
  imports: [
    PortalPageComponent,
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListPaginationComponent,
    AdeniPillComponent,
    AdeniTablePersonCellComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    MarketFormModalComponent,
  ],
  templateUrl: "./markets.component.html",
  styleUrl: "./markets.component.scss",
})
export class AdminMarketsComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly markets = signal<AdminMarket[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;
  readonly searchText = signal("");
  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");
  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize, this.totalCount()),
  );
  readonly isEmptyCatalog = computed(() => !this.loading() && this.totalCount() === 0);

  readonly formOpen = signal(false);
  readonly formMode = signal<MarketFormMode>("create");
  readonly editingMarket = signal<AdminMarket | null>(null);
  readonly formSaving = signal(false);

  readonly marketLivePill = marketLivePill;

  readonly columns: AdeniDataTableColumn<AdminMarket>[] = [
    {
      id: "market",
      header: "Market",
      cell: (m) => m.name,
      sortValue: (m) => m.name.toLowerCase(),
    },
    {
      id: "country",
      header: "Country",
      cell: (m) => m.countryCode,
      sortValue: (m) => m.countryCode,
    },
    {
      id: "currency",
      header: "Currency",
      cell: (m) => m.currency,
      sortValue: (m) => m.currency,
    },
    {
      id: "businesses",
      header: "Businesses",
      align: "right",
      cell: (m) => String(m.businessCount ?? 0),
      sortValue: (m) => m.businessCount ?? 0,
    },
    {
      id: "status",
      header: "Status",
      cell: () => "",
      sortValue: (m) => (m.isLive ? 1 : 0),
    },
  ];

  readonly trackMarketRow = (market: AdminMarket): string => market.id;

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.searchText.set(params.get("q") ?? "");
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
  }

  marketInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((c) =>
          c.listAdminMarkets({
            q: this.searchText().trim() || undefined,
            page: this.page(),
            pageSize: this.pageSize,
            sort: listSortParamsFromColumn(this.sortColumnId(), this.sortDirection()),
          }),
        );
        this.markets.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Loading markets…", "Fetching market catalog");
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load markets.", err);
      this.error.set(message);
      this.feedback.error(message);
      this.markets.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  onToolbarSearch(value: string): void {
    this.searchText.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { q: value.trim() || null, page: null },
        queryParamsHandling: "merge",
      });
    }, 350);
  }

  goToPage(next: number): void {
    const page = Math.min(Math.max(1, next), this.totalPages());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page === 1 ? null : page },
      queryParamsHandling: "merge",
    });
  }

  onSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  onSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  openCreate(): void {
    this.formMode.set("create");
    this.editingMarket.set(null);
    this.formOpen.set(true);
  }

  openEdit(market: AdminMarket): void {
    this.formMode.set("edit");
    this.editingMarket.set(market);
    this.formOpen.set(true);
  }

  closeForm(): void {
    this.formOpen.set(false);
    this.editingMarket.set(null);
    this.formSaving.set(false);
  }

  async saveForm(value: MarketFormValue): Promise<void> {
    const languages: string[] = [];
    if (value.languagesEn) {
      languages.push("en");
    }
    if (value.languagesFr) {
      languages.push("fr");
    }
    if (languages.length === 0) {
      this.feedback.error("Select at least one language.");
      return;
    }

    this.formSaving.set(true);
    try {
      if (this.formMode() === "create") {
        await this.feedback.runLoading(
          () =>
            this.api.withAuthorizedClient((c) =>
              c.createAdminMarket({
                id: value.id.trim().toLowerCase(),
                name: value.name.trim(),
                countryCode: value.countryCode.trim().toUpperCase(),
                currency: value.currency.trim().toUpperCase(),
                timeZoneId: value.timeZoneId.trim(),
                defaultLat: value.defaultLat,
                defaultLng: value.defaultLng,
                languages,
                isLive: value.isLive,
                launchNote: value.launchNote.trim() || null,
              }),
            ),
          "Creating market…",
          value.name,
        );
        this.feedback.success("Market created.", value.name);
      } else {
        const id = this.editingMarket()?.id;
        if (!id) {
          return;
        }
        await this.feedback.runLoading(
          () =>
            this.api.withAuthorizedClient((c) =>
              c.updateAdminMarket(id, {
                name: value.name.trim(),
                countryCode: value.countryCode.trim().toUpperCase(),
                currency: value.currency.trim().toUpperCase(),
                timeZoneId: value.timeZoneId.trim(),
                defaultLat: value.defaultLat,
                defaultLng: value.defaultLng,
                languages,
                launchNote: value.launchNote.trim() || null,
              }),
            ),
          "Saving market…",
          value.name,
        );
        this.feedback.success("Market updated.", value.name);
      }
      this.closeForm();
      await this.load();
    } catch {
      this.feedback.error("Could not save market.");
    } finally {
      this.formSaving.set(false);
    }
  }

  async toggleLive(market: AdminMarket): Promise<void> {
    const nextLive = !market.isLive;
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.setAdminMarketLive(market.id, nextLive)),
        nextLive ? "Going live…" : "Taking offline…",
        market.name,
      );
      await this.load();
      this.feedback.success(
        `${market.name} is now ${nextLive ? "live" : "offline"}.`,
        "Market updated",
      );
    } catch {
      this.error.set("Could not update market.");
      this.feedback.error("Could not update market.");
    }
  }
}
