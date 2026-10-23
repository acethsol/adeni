import { Component, computed, inject, signal } from "@angular/core";
import { JsonPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router } from "@angular/router";
import type { AdminCustomerSummary, CustomerDataExport } from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { ADENI_ADMIN_CONFIG, isAdminPortalDevMode } from "../../core/adeni-config";
import { AdminApiService } from "../../core/services/admin-api.service";
import {
  ADMIN_LIST_PAGE_SIZE,
  adminListTotalPages,
  LIST_PAGE_SIZE_OPTIONS,
} from "../../shared/admin-list-paging";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { erasurePill } from "../../shared/admin-pill-maps";

@Component({
  selector: "app-admin-customers",
  standalone: true,
  imports: [
    PortalPageComponent,
    JsonPipe,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniTablePersonCellComponent,
    AdeniStaffButtonComponent,
    AdeniListPaginationComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
  ],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class AdminCustomersComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  protected readonly devMode = isAdminPortalDevMode(inject(ADENI_ADMIN_CONFIG));
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly searchQuery = signal("");
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly customers = signal<AdminCustomerSummary[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(ADMIN_LIST_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly exportData = signal<CustomerDataExport | null>(null);
  readonly busyId = signal<string | null>(null);

  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize()));

  readonly erasurePill = erasurePill;

  readonly columns = computed<AdeniDataTableColumn<AdminCustomerSummary>[]>(() => [
    {
      id: "customer",
      header: this.tr("common.customer"),
      cell: (row) => row.name,
      sortValue: (row) => row.name.toLowerCase(),
    },
    {
      id: "auth0Sub",
      header: this.tr("admin.customers.auth0Sub"),
      cell: (row) => row.auth0Sub,
      sortValue: (row) => row.auth0Sub,
    },
  ]);

  readonly trackRow = (row: AdminCustomerSummary): string => row.id;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = (params.get("q") ?? params.get("email") ?? "").trim();
      const page = Number(params.get("page") ?? "1");
      this.searchQuery.set(q);
      this.page.set(Number.isFinite(page) && page >= 1 ? page : 1);
      if (q) {
        void this.runSearch(q);
      } else {
        this.hasSearched.set(false);
        this.customers.set([]);
        this.totalCount.set(0);
      }
    });
  }

  loadDevSampleCustomers(): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q: "dev-privacy", email: null, page: null },
    });
  }

  onSearchInput(value: string): void {
    this.searchQuery.set(value);
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      const q = value.trim();
      void this.router.navigate([], {
        relativeTo: this.route,
        queryParams: { q: q || null, email: null, page: null },
      });
    }, 400);
  }

  customerInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return "?";
    }
    if (parts.length === 1) {
      return parts[0]!.slice(0, 2).toUpperCase();
    }
    return `${parts[0]![0] ?? ""}${parts[1]![0] ?? ""}`.toUpperCase();
  }

  private async runSearch(q: string): Promise<void> {
    this.searching.set(true);
    this.hasSearched.set(true);
    this.error.set(null);
    this.message.set(null);
    this.exportData.set(null);

    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient((c) =>
          c.searchAdminCustomers(q, { page: this.page(), pageSize: this.pageSize() }),
        );
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
      }, this.tr("admin.customers.searchingCustomers"), q);
    } catch (err) {
      const message = adminLoadErrorMessage(this.tr("admin.customers.couldNotSearchCustomers"), err);
      this.error.set(message);
      this.feedback.error(message);
      this.customers.set([]);
      this.totalCount.set(0);
    } finally {
      this.searching.set(false);
    }
  }

  goToPage(next: number): void {
    const page = Math.min(Math.max(1, next), this.totalPages());
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: page === 1 ? null : page },
      queryParamsHandling: "merge",
    });
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { page: null },
      queryParamsHandling: "merge",
    });
  }

  async export(customerId: string): Promise<void> {
    this.busyId.set(customerId);
    this.error.set(null);
    try {
      const data = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.exportAdminCustomer(customerId)),
        this.tr("admin.customers.exporting"),
        this.tr("admin.customers.buildingCustomerDataPackage"),
      );
      this.exportData.set(data);
      this.message.set(this.tr("admin.customers.exportLoadedBelowSaveJson"));
      this.feedback.success(this.tr("admin.customers.customerExportReadyBelow"), this.tr("admin.customers.exportReady"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "common.exportFailed"));
      this.feedback.error(this.apiErr(caught, "common.exportFailed"));
    } finally {
      this.busyId.set(null);
    }
  }

  downloadExport(): void {
    const data = this.exportData();
    if (!data) return;
    const blob = new Blob([JSON.stringify(data, null, 2)], { type: "application/json" });
    const url = URL.createObjectURL(blob);
    const a = document.createElement("a");
    a.href = url;
    a.download = `adeni-customer-${data.customerId}.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.feedback.info("JSON download started.", this.tr("admin.customers.download"));
  }

  async erase(customer: AdminCustomerSummary): Promise<void> {
    const label = customer.name || customer.email || "this customer";
    const ok = await this.confirmDialog.confirm({
      title: this.tr("admin.customers.initiateErasure"),
      message: this.tr("admin.customers.erasePiiCannotBeRecovered", { label: label }),
      confirmLabel: this.tr("admin.customers.erase"),
      danger: true,
    });
    if (!ok) {
      return;
    }

    this.busyId.set(customer.id);
    this.error.set(null);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.initiateAdminCustomerDelete(customer.id)),
        this.tr("admin.customers.initiatingErasure"),
        customer.email ?? customer.name ?? customer.id,
      );
      this.customers.update((list) =>
        list.map((item) =>
          item.id === customer.id
            ? {
                ...item,
                name: "[erased]",
                email: null,
                erasureRequestedAt: new Date().toISOString(),
              }
            : item,
        ),
      );
      this.message.set(this.tr("admin.customers.erasureInitiated"));
      this.feedback.success(this.tr("admin.customers.customerPiiErasureWasInitiated"), this.tr("admin.customers.erasureStarted"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "admin.customers.couldNotInitiateErasure"));
      this.feedback.error(this.apiErr(caught, "admin.customers.couldNotInitiateErasure"));
    } finally {
      this.busyId.set(null);
    }
  }
}
