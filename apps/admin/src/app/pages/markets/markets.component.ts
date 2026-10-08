import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import type { AdminMarket } from "@adeni/shared";
import { AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
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
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly markets = signal<AdminMarket[]>([]);
  readonly query = signal("");
  readonly visible = computed(() =>
    this.markets().filter((item) => {
      const needle = this.query().trim().toLowerCase();
      if (!needle) {
        return true;
      }
      return `${item.name} ${item.id} ${item.countryCode} ${item.currency}`.toLowerCase().includes(needle);
    }),
  );

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.query.set(params.get("q") ?? "");
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const items = await this.api.withAuthorizedClient((c) => c.getAdminMarkets());
        this.markets.set(items);
      }, "Loading markets…", "Fetching market catalog");
    } catch {
      this.error.set("Could not load markets.");
      this.feedback.error("Could not load markets.");
    } finally {
      this.loading.set(false);
    }
  }

  async toggleLive(market: AdminMarket): Promise<void> {
    const nextLive = !market.isLive;
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.setAdminMarketLive(market.id, nextLive)),
        nextLive ? "Going live…" : "Taking offline…",
        market.name,
      );
      await this.load();
      this.feedback.success(
        `${market.name} is now ${nextLive ? "live" : "offline"}.`,
        "Market updated",
      );
    } catch {
      this.error.set("Could not update market.");
      this.feedback.error("Could not update market.");
    }
  }
}
