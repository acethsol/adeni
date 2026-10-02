import { Component, inject, OnInit, signal } from "@angular/core";
import type { AdminMarket } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { AdminApiService } from "../../core/services/admin-api.service";

@Component({
  selector: "app-admin-markets",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./markets.component.html",
  styleUrl: "./markets.component.scss",
})
export class AdminMarketsComponent implements OnInit {
  private readonly api = inject(AdminApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly markets = signal<AdminMarket[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const items = await this.api.withAuthorizedClient((c) => c.getAdminMarkets());
      this.markets.set(items);
    } catch {
      this.error.set("Could not load markets.");
    } finally {
      this.loading.set(false);
    }
  }

  async toggleLive(market: AdminMarket): Promise<void> {
    try {
      await this.api.withAuthorizedClient((c) =>
        c.setAdminMarketLive(market.id, !market.isLive),
      );
      await this.load();
    } catch {
      this.error.set("Could not update market.");
    }
  }
}
