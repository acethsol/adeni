import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import type { TenantCustomerListItem } from "@adeni/shared";
import { formatSlotTime } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-customers",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    FormsModule,
    RouterLink,
  ],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class CustomersComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<TenantCustomerListItem[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 25;
  searchInput = "";
  private searchDebounce: ReturnType<typeof setTimeout> | null = null;

  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
  }

  onSearchInput(): void {
    if (this.searchDebounce) {
      clearTimeout(this.searchDebounce);
    }
    this.searchDebounce = setTimeout(() => {
      this.page.set(1);
      void this.load();
    }, 300);
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const term = this.searchInput.trim();
      const result = await this.api.withAuthorizedClient((client) =>
        client.listTenantCustomers({
          search: term.length > 0 ? term : undefined,
          page: this.page(),
          pageSize: this.pageSize,
        }),
      );
      this.items.set(result.items);
      this.totalCount.set(result.totalCount);
    } catch {
      this.error.set("Could not load customers. Try again.");
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

  prevPage(): void {
    if (this.page() <= 1) {
      return;
    }
    this.page.update((p) => p - 1);
    void this.load();
  }

  nextPage(): void {
    const maxPage = Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
    if (this.page() >= maxPage) {
      return;
    }
    this.page.update((p) => p + 1);
    void this.load();
  }

  pageLabel(): string {
    const maxPage = Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
    return `Page ${this.page()} of ${maxPage}`;
  }

  canNextPage(): boolean {
    return this.page() < Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }
}
