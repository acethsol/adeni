import { DecimalPipe } from "@angular/common";
import { Component, computed, inject, input } from "@angular/core";
import { RouterLink } from "@angular/router";
import type { DiscoveryBusinessItem } from "@adeni/shared";
import {
  formatRatingSummary,
  getCategoryLabel,
  getCategoryVisual,
  resolveBusinessCoverImage,
  t,
} from "@adeni/shared";
import { AdeniLocaleService } from "@adeni/ui";

@Component({
  selector: "app-discovery-business-card",
  standalone: true,
  imports: [RouterLink, DecimalPipe],
  templateUrl: "./discovery-business-card.component.html",
  styleUrl: "./discovery-business-card.component.scss",
})
export class DiscoveryBusinessCardComponent {
  private readonly localeService = inject(AdeniLocaleService);

  readonly item = input.required<DiscoveryBusinessItem>();
  readonly headingLevel = input<"h2" | "h3">("h3");

  readonly coverFor = resolveBusinessCoverImage;
  readonly locale = this.localeService.locale;

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
}
