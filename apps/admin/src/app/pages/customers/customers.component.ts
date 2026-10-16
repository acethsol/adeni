import { Component, computed, inject, signal } from "@angular/core";
import { JsonPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute, Router } from "@angular/router";
import type { AdminCustomerSummary, CustomerDataExport } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniStaffButtonComponent,
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
import { erasurePill } from "../../shared/admin-pill-maps";

@Component({
  selector: "app-admin-customers",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    JsonPipe,
    AdeniCarbonIconComponent,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
  ],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class AdminCustomersComponent {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  searchInput = "";
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly customers = signal<AdminCustomerSummary[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = ADMIN_LIST_PAGE_SIZE;
  readonly exportData = signal<CustomerDataExport | null>(null);
  readonly busyId = signal<string | null>(null);

  readonly totalPages = computed(() => adminListTotalPages(this.totalCount(), this.pageSize));
  readonly showingRange = computed(() =>
    adminListRangeLabel(this.page(), this.pageSize, this.totalCount()),
  );

  readonly erasurePill = erasurePill;

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const q = (params.get("q") ?? params.get("email") ?? "").trim();
      const page = Number(params.get("page") ?? "1");
      if (q && q !== this.searchInput) {
        this.searchInput = q;
      }
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

  async search(): Promise<void> {
    const q = this.searchInput.trim();
    if (!q) {
      return;
    }
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { q, email: null, page: null },
    });
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
          c.searchAdminCustomers(q, { page: this.page(), pageSize: this.pageSize }),
        );
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
      }, "Searching customers…", q);
    } catch (err) {
      const message = adminLoadErrorMessage("Could not search customers.", err);
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

  async export(customerId: string): Promise<void> {
    this.busyId.set(customerId);
    this.error.set(null);
    try {
      const data = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.exportAdminCustomer(customerId)),
        "Exporting…",
        "Building customer data package",
      );
      this.exportData.set(data);
      this.message.set("Export loaded below — save JSON for your records.");
      this.feedback.success("Customer export is ready below.", "Export ready");
    } catch {
      this.error.set("Export failed.");
      this.feedback.error("Export failed.");
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
    this.feedback.info("JSON download started.", "Download");
  }

  async erase(customer: AdminCustomerSummary): Promise<void> {
    const label = customer.name || customer.email || "this customer";
    const ok = await this.confirmDialog.confirm({
      title: "Initiate erasure?",
      message: `Erase PII for ${label}? This cannot be recovered.`,
      confirmLabel: "Erase",
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
        "Initiating erasure…",
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
      this.message.set("Erasure initiated.");
      this.feedback.success("Customer PII erasure was initiated.", "Erasure started");
    } catch {
      this.error.set("Could not initiate erasure.");
      this.feedback.error("Could not initiate erasure.");
    } finally {
      this.busyId.set(null);
    }
  }
}
