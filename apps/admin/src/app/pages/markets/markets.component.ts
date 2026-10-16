import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import type { AdminMarket } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";
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
    FormsModule,
    RouterLink,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
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

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.searchText.set(params.get("q") ?? "");
      const page = Number(params.get("page") ?? "1");
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      void this.load();
    });
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

  applySearch(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: this.searchText().trim() || null, page: null },
      queryParamsHandling: "merge",
    });
  }

  goToPage(next: number): void {
    const page = Math.min(Math.max(1, next), this.totalPages());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page === 1 ? null : page },
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
