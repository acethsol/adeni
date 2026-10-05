import { Component, inject, signal } from "@angular/core";
import { JsonPipe } from "@angular/common";
import { FormsModule } from "@angular/forms";
import type { AdminCustomerSummary, CustomerDataExport } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

@Component({
  selector: "app-admin-customers",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, JsonPipe],
  templateUrl: "./customers.component.html",
  styleUrl: "./customers.component.scss",
})
export class AdminCustomersComponent {
  private readonly api = inject(AdminApiService);

  email = "";
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly customers = signal<AdminCustomerSummary[]>([]);
  readonly exportData = signal<CustomerDataExport | null>(null);
  readonly busyId = signal<string | null>(null);

  async search(): Promise<void> {
    this.searching.set(true);
    this.hasSearched.set(true);
    this.error.set(null);
    this.message.set(null);
    this.exportData.set(null);

    try {
      const items = await this.api.withAuthorizedClient((c) =>
        c.searchAdminCustomers(this.email.trim()),
      );
      this.customers.set(items);
    } catch {
      this.error.set("Could not search customers.");
      this.customers.set([]);
    } finally {
      this.searching.set(false);
    }
  }

  async export(customerId: string): Promise<void> {
    this.busyId.set(customerId);
    this.error.set(null);
    try {
      const data = await this.api.withAuthorizedClient((c) => c.exportAdminCustomer(customerId));
      this.exportData.set(data);
      this.message.set("Export loaded below — save JSON for your records.");
    } catch {
      this.error.set("Export failed.");
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
  }

  async erase(customer: AdminCustomerSummary): Promise<void> {
    if (
      !confirm(
        `Initiate erasure for ${customer.name || customer.email || "this customer"}? PII will be cleared and cannot be recovered.`,
      )
    ) {
      return;
    }

    this.busyId.set(customer.id);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((c) => c.initiateAdminCustomerDelete(customer.id));
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
    } catch {
      this.error.set("Could not initiate erasure.");
    } finally {
      this.busyId.set(null);
    }
  }
}
