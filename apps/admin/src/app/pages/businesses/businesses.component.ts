import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import type { AdminBusinessSummary, SubscriptionTier } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
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
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly businesses = signal<AdminBusinessSummary[]>([]);
  readonly query = signal("");
  readonly visible = computed(() => filterByQuery(this.businesses(), this.query(), (item) => [item.name, item.slug, item.subscriptionTier]));
  readonly tiers: SubscriptionTier[] = ["free", "pro", "business"];

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

function filterByQuery<T>(items: T[], query: string, parts: (item: T) => Array<string | null | undefined>): T[] {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return items;
  }
  return items.filter((item) =>
    parts(item)
      .filter((part): part is string => Boolean(part))
      .join(" ")
      .toLowerCase()
      .includes(needle),
  );
}
