import { DatePipe } from "@angular/common";
import { Component, computed, inject, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { formatSubscriptionSku, formatTenantStatus, type AdminBusinessSummary } from "@adeni/shared";
import {
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniExportMenuItem,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListRangeLabel,
  adminListTotalPages,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { billingStatusPill, businessPlanPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import { BusinessSubscriptionWizardComponent } from "./business-subscription-wizard.component";
import {
  buildCommercialTermsRequest,
  draftFromCommercialTerms,
  type BusinessSubscriptionDraft,
} from "./business-subscription.model";

/** TenantStatus.PendingVerification */
const PENDING_VERIFICATION = 1;

@Component({
  selector: "app-admin-businesses",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    DatePipe,
    AdeniListToolbarComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniListPaginationComponent,
    AdeniPillComponent,
    AdeniPortalEmptyStateComponent,
    AdeniTablePersonCellComponent,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    RouterLink,
    BusinessSubscriptionWizardComponent,
  ],
  templateUrl: "./businesses.component.html",
  styleUrl: "./businesses.component.scss",
})
export class AdminBusinessesComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly businesses = signal<AdminBusinessSummary[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;
  readonly searchText = signal("");
  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize, this.totalCount()),
  );
  readonly isEmptyCatalog = computed(() => !this.loading() && this.totalCount() === 0);

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

  readonly columns: AdeniDataTableColumn<AdminBusinessSummary>[] = [
    { id: "business", header: "Business", cell: (b) => b.name },
    { id: "status", header: "Status", cell: () => "" },
    { id: "plan", header: "Plan", cell: () => "" },
    { id: "billing", header: "Billing", cell: () => "" },
  ];

  readonly trackBusinessRow = (b: AdminBusinessSummary): string => b.id;

  businessInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly subscriptionBusiness = signal<AdminBusinessSummary | null>(null);
  readonly subscriptionDraft = signal<BusinessSubscriptionDraft | null>(null);
  readonly subscriptionSaving = signal(false);
  readonly subscriptionWizardOpen = computed(
    () => this.subscriptionBusiness() != null && this.subscriptionDraft() != null,
  );

  readonly formatTenantStatus = formatTenantStatus;
  readonly pendingVerificationStatus = PENDING_VERIFICATION;
  readonly tenantStatusPill = tenantStatusPill;
  readonly businessPlanPill = businessPlanPill;
  readonly billingStatusPill = billingStatusPill;

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
          c.listAdminBusinesses({
            q: this.searchText().trim() || undefined,
            page: this.page(),
            pageSize: this.pageSize,
          }),
        );
        this.businesses.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Loading businesses…", "Fetching tenant list");
    } catch (err) {
      const message = adminLoadErrorMessage("Could not load businesses.", err);
      this.error.set(message);
      this.feedback.error(message);
      this.businesses.set([]);
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

  async openSubscriptionWizard(business: AdminBusinessSummary): Promise<void> {
    try {
      const terms = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) => c.getAdminTenantCommercialTerms(business.id)),
        "Loading subscription…",
        business.name,
      );
      this.subscriptionBusiness.set(business);
      this.subscriptionDraft.set(draftFromCommercialTerms(terms, business));
    } catch {
      this.feedback.error("Could not load subscription details.");
    }
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

    const body = buildCommercialTermsRequest(draft);
    const skuChanged = draft.subscriptionSku !== draft.initialSubscriptionSku;

    this.subscriptionSaving.set(true);
    try {
      await this.feedback.runLoading(async () => {
        const client = await this.api.createAuthorizedClient();
        await client.setAdminTenantCommercialTerms(business.id, body);
      }, "Saving subscription…", business.name);
      this.closeSubscriptionWizard();
      await this.load();
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
