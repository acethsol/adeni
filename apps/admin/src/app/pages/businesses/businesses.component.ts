import { Component, inject, OnInit, signal } from "@angular/core";
import type { AdminBusinessSummary, SubscriptionTier } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { AdminApiService } from "../../core/services/admin-api.service";

@Component({
  selector: "app-admin-businesses",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./businesses.component.html",
  styleUrl: "./businesses.component.scss",
})
export class AdminBusinessesComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly businesses = signal<AdminBusinessSummary[]>([]);
  readonly tiers: SubscriptionTier[] = ["free", "pro", "business"];

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.getAdminBusinesses());
      this.businesses.set(items);
    } catch {
      this.error.set("Could not load businesses.");
    } finally {
      this.loading.set(false);
    }
  }

  async setTier(business: AdminBusinessSummary, tier: SubscriptionTier): Promise<void> {
    try {
      await this.api.withAuthorizedClient((c) => c.setAdminBusinessSubscriptionTier(business.id, tier));
      await this.load();
    } catch {
      this.error.set("Could not update subscription tier.");
    }
  }
}
