import { DecimalPipe } from "@angular/common";
import { Component, computed, effect, inject, input, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import {
  formatRatingSummary,
  getCategoryLabel,
  getCategoryVisual,
  resolveBusinessImageUrls,
  t,
} from "@adeni/shared";
import { AdeniCarbonIconComponent, AdeniLocaleService } from "@adeni/ui";

@Component({
  selector: "app-discovery-business-card",
  standalone: true,
  imports: [RouterLink, DecimalPipe, AdeniCarbonIconComponent],
  templateUrl: "./discovery-business-card.component.html",
  styleUrl: "./discovery-business-card.component.scss",
})
export class DiscoveryBusinessCardComponent {
  private readonly localeService = inject(AdeniLocaleService);

  readonly item = input.required<DiscoveryBusinessItem>();
  readonly headingLevel = input<"h2" | "h3">("h3");
  readonly layout = input<"tile" | "row">("tile");
  readonly index = input<number | null>(null);
  /** Explore / map-style carousel; leave false on home. */
  readonly imageCarousel = input(false);

  readonly locale = this.localeService.locale;
  readonly activeImageIndex = signal(0);

  constructor() {
    effect(() => {
      this.item();
      this.activeImageIndex.set(0);
    });
  }

  readonly categoryName = computed(() =>
    getCategoryLabel(this.locale(), this.item().categorySlug),
  );

  readonly tone = computed(() => {
    const [g1, g2] = getCategoryVisual(this.item().categorySlug).gradient;
    return { g1, g2 };
  });

  readonly verifiedLabel = computed(() => t(this.locale(), "business.verified"));
  /** "New" when there are no reviews yet. */
  readonly newLabel = computed(() => formatRatingSummary(null, 0));

  readonly isVerified = computed(() => Boolean(this.item().verifiedSince));

  readonly hasReviews = computed(() => {
    const item = this.item();
    return item.ratingAvg != null && (item.reviewCount ?? 0) > 0;
  });

  readonly images = computed(() => {
    const item = this.item();
    return resolveBusinessImageUrls(item.categorySlug, item.coverImageUrl, item.imageUrls);
  });

  readonly showCarousel = computed(() => this.imageCarousel() && this.images().length > 1);

  readonly activeImage = computed(() => {
    const list = this.images();
    const index = Math.min(Math.max(this.activeImageIndex(), 0), list.length - 1);
    return list[index] ?? list[0] ?? "";
  });

  prevImage(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const count = this.images().length;
    if (count < 2) return;
    this.activeImageIndex.update((i) => (i - 1 + count) % count);
  }

  nextImage(event: Event): void {
    event.preventDefault();
    event.stopPropagation();
    const count = this.images().length;
    if (count < 2) return;
    this.activeImageIndex.update((i) => (i + 1) % count);
  }
}
