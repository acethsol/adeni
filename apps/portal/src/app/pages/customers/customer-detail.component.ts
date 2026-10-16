import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { TenantCustomerDetail } from "@adeni/shared";
import { formatSlotTime } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-customer-detail",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    RouterLink,
  ],
  templateUrl: "./customer-detail.component.html",
  styleUrl: "./customer-detail.component.scss",
})
export class CustomerDetailComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly customer = signal<TenantCustomerDetail | null>(null);

  readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      this.error.set("Customer not found.");
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const detail = await this.api.withAuthorizedClient((client) =>
        client.getTenantCustomer(id),
      );
      this.customer.set(detail);
    } catch {
      this.error.set("Could not load this customer.");
      this.customer.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  bookingQueryParams(bookingId: string): { tab: string; focus: string } {
    return { tab: "upcoming", focus: bookingId };
  }
}
