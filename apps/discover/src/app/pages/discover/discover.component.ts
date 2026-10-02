import { DecimalPipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { FormsModule } from "@angular/forms";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  DISCOVERY_PAGE_SIZE,
  getCategoryLabel,
  resolveBusinessCoverImage,
} from "@adeni/shared";
import { DiscoverApiService } from "../../core/services/discover-api.service";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-discover-page",
  standalone: true,
  imports: [RouterLink, FormsModule, DecimalPipe],
  templateUrl: "./discover.component.html",
  styleUrl: "./discover.component.scss",
})
export class DiscoverComponent implements OnInit {
  private readonly api = inject(DiscoverApiService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly items = signal<DiscoveryBusinessItem[]>([]);
  readonly totalCount = signal(0);

  searchQuery = "";
  selectedCategory = "";
  readonly coverFor = resolveBusinessCoverImage;
  readonly categoryLabel = getCategoryLabel;

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.selectedCategory = params.get("category") ?? "";
      this.searchQuery = params.get("q") ?? "";
      void this.load();
    });
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    const client = this.api.createClient();

    try {
      const [categories, discovery] = await Promise.all([
        client.getCategories().catch(() => [] as Category[]),
        client.searchDiscovery({
          lat: this.config.defaultLocation.lat,
          lng: this.config.defaultLocation.lng,
          market: this.config.defaultMarketId,
          page: 1,
          pageSize: DISCOVERY_PAGE_SIZE,
          category: this.selectedCategory || undefined,
          q: this.searchQuery.trim() || undefined,
        }),
      ]);
      this.categories.set(categories);
      this.items.set(discovery.items);
      this.totalCount.set(discovery.totalCount);
    } catch {
      this.error.set("Could not load discovery results. Is the API running?");
      this.items.set([]);
      this.totalCount.set(0);
    } finally {
      this.loading.set(false);
    }
  }

}
