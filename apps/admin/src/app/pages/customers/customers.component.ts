import { Component, inject, signal } from "@angular/core";
import { JsonPipe } from "@angular/common";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute } from "@angular/router";
import type { AdminCustomerSummary, CustomerDataExport } from "@adeni/shared";
import { AdeniConfirmService, AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
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
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  email = "";
  readonly searching = signal(false);
  readonly hasSearched = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly customers = signal<AdminCustomerSummary[]>([]);
  readonly exportData = signal<CustomerDataExport | null>(null);
  readonly busyId = signal<string | null>(null);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const email = params.get("email")?.trim() ?? "";
      if (!email || (email === this.email && this.hasSearched())) {
        return;
      }
      this.email = email;
      void this.search();
    });
  }

  async search(): Promise<void> {
    this.searching.set(true);
    this.hasSearched.set(true);
    this.error.set(null);
    this.message.set(null);
    this.exportData.set(null);

    try {
      await this.feedback.runLoading(async () => {
        const items = await this.api.withAuthorizedClient((c) =>
          c.searchAdminCustomers(this.email.trim()),
        );
        this.customers.set(items);
      }, "Searching customers…", this.email.trim() || "All matches");
    } catch {
      this.error.set("Could not search customers.");
      this.feedback.error("Could not search customers.");
      this.customers.set([]);
    } finally {
      this.searching.set(false);
    }
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
