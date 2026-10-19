import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { PricingCatalogVersionDetail, PricingCatalogVersionSummary } from "@adeni/shared";
import { formatPrice, formatSubscriptionSku, type SubscriptionSku } from "@adeni/shared";
import {
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
} from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";
import { adminLoadErrorMessage } from "../../shared/admin-load-error";
import { AdminPillComponent } from "../../shared/admin-pill.component";

@Component({
  selector: "app-admin-pricing-catalog",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniPortalEmptyStateComponent,
    AdminPillComponent,
  ],
  templateUrl: "./pricing-catalog.component.html",
  styleUrl: "./pricing-catalog.component.scss",
})
export class AdminPricingCatalogComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly versions = signal<PricingCatalogVersionSummary[]>([]);
  readonly selectedCode = signal<string | null>(null);
  readonly detail = signal<PricingCatalogVersionDetail | null>(null);

  readonly activeVersion = computed(() => this.versions().find((v) => v.isActive) ?? null);

  async ngOnInit(): Promise<void> {
    await this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((client) =>
        client.listAdminPricingCatalogVersions(),
      );
      this.versions.set(items);
      const active = items.find((v) => v.isActive) ?? items[0] ?? null;
      if (active) {
        await this.selectVersion(active.code);
      } else {
        this.detail.set(null);
        this.selectedCode.set(null);
      }
    } catch (err) {
      this.error.set(adminLoadErrorMessage("Could not load pricing catalog.", err));
    } finally {
      this.loading.set(false);
    }
  }

  async selectVersion(code: string): Promise<void> {
    this.selectedCode.set(code);
    this.error.set(null);
    try {
      const version = await this.api.withAuthorizedClient((client) =>
        client.getAdminPricingCatalogVersion(code),
      );
      this.detail.set(version);
    } catch (err) {
      this.detail.set(null);
      this.error.set(adminLoadErrorMessage("Could not load catalog version.", err));
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
}
