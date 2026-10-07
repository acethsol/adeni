import { DecimalPipe } from "@angular/common";
import {
  Component,
  computed,
  DestroyRef,
  inject,
  input,
  OnInit,
  signal,
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { RouterLink } from "@angular/router";
import type { Category, DiscoveryBusinessItem } from "@adeni/shared";
import {
  formatRatingSummary,
  getCategoryLabel,
  getCategoryVisual,
  resolveBusinessCoverImage,
  t,
} from "@adeni/shared";
import { AdeniBrandLockupComponent, AdeniLocaleService } from "@adeni/ui";
import { interval } from "rxjs";
import { ADENI_DISCOVER_CONFIG } from "../core/adeni-config";
import { HERO_SEARCH_ANCHOR_ID } from "../core/services/hero-search-pin.service";
import { DiscoverySearchComponent } from "./discovery-search.component";

/** All enabled beauty/wellness categories (hero chips). */
const HERO_CHIP_ORDER = [
  "hair-grooming",
  "nails",
  "spa-relaxation",
  "skincare-aesthetics",
  "massage-bodywork",
  "fitness",
  "yoga-pilates",
] as const;

const SHORT_LABELS: Record<string, string> = {
  "massage-bodywork": "Massage",
  "spa-relaxation": "Spa",
  "hair-grooming": "Hair",
  nails: "Nails",
  "skincare-aesthetics": "Skincare",
  fitness: "Fitness",
  "yoga-pilates": "Yoga",
};

const ROTATE_MS = 3200;
const FEATURED_ROTATE_MS = 4800;

type HeroCategoryChip = {
  slug: string;
  label: string;
  icon: string;
  tone: string;
  imageUrl: string;
  gradient: [string, string];
};

@Component({
  selector: "app-hero-category-stage",
  standalone: true,
  imports: [
    RouterLink,
    DecimalPipe,
    DiscoverySearchComponent,
    AdeniBrandLockupComponent,
  ],
  templateUrl: "./hero-category-stage.component.html",
  styleUrl: "./hero-category-stage.component.scss",
})
export class HeroCategoryStageComponent implements OnInit {
  private readonly localeService = inject(AdeniLocaleService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly categories = input<Category[]>([]);
  /** Top featured discovery results for this market (API `sort=featured`). */
  readonly featured = input<DiscoveryBusinessItem[]>([]);

  readonly heroSearchAnchorId = HERO_SEARCH_ANCHOR_ID;
  readonly activeIndex = signal(0);
  readonly featuredIndex = signal(0);
  readonly coverFor = resolveBusinessCoverImage;

  readonly locale = this.localeService.locale;

  readonly headlineLead = computed(() => t(this.locale(), "home.headlineLead"));
  readonly lead = computed(() => t(this.locale(), "home.heroLead"));
  readonly browseServicesLabel = computed(() => t(this.locale(), "home.browseServices"));
  readonly listBusinessLabel = computed(() => t(this.locale(), "home.listBusiness"));
  readonly bookNowLabel = computed(() => t(this.locale(), "home.bookNow"));
  readonly featuredLabel = computed(() => t(this.locale(), "home.featuredNearYou"));
  readonly verifiedLabel = computed(() => t(this.locale(), "business.verified"));
  readonly brandPill = computed(() => t(this.locale(), "home.brandPill"));

  readonly portalUrl = computed(() => {
    const origin = this.config.portalAppUrl.trim();
    return origin ? origin.replace(/\/$/, "") : null;
  });

  readonly chips = computed<HeroCategoryChip[]>(() => {
    const locale = this.locale();
    const fromApi = this.categories();
    const available = new Set(fromApi.map((category) => category.slug.trim().toLowerCase()));

    const ordered =
      available.size === 0
        ? [...HERO_CHIP_ORDER]
        : HERO_CHIP_ORDER.filter((slug) => available.has(slug));

    if (available.has("nails") && !ordered.includes("nails")) {
      ordered.splice(1, 0, "nails");
    }

    return ordered.map((slug, index) => {
      const visual = getCategoryVisual(slug);
      const full = getCategoryLabel(locale, slug, visual.label);
      return {
        slug,
        label: SHORT_LABELS[slug] ?? full.split(" ")[0] ?? full,
        icon: visual.icon,
        tone: `tone-${(index % 7) + 1}`,
        imageUrl: visual.imageUrl,
        gradient: visual.gradient,
      };
    });
  });

  readonly activeChip = computed(() => {
    const list = this.chips();
    if (list.length === 0) {
      return null;
    }
    return list[this.activeIndex() % list.length] ?? list[0]!;
  });

  /** Cycles the top featured listings — not random; ranked by discovery `featured` sort. */
  readonly spotlight = computed(() => {
    const items = this.featured().slice(0, 3);
    if (items.length === 0) {
      return null;
    }
    return items[this.featuredIndex() % items.length] ?? null;
  });

  readonly stageImage = computed(() => {
    const biz = this.spotlight();
    if (biz) {
      return this.coverFor(biz.categorySlug, biz.coverImageUrl);
    }
    return this.activeChip()?.imageUrl ?? "";
  });

  readonly stageTone = computed(() => {
    const chip = this.activeChip();
    return {
      g1: chip?.gradient[0] ?? "#1b4332",
      g2: chip?.gradient[1] ?? "#13c2a3",
    };
  });

  ngOnInit(): void {
    if (
      typeof window !== "undefined" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    ) {
      return;
    }

    interval(ROTATE_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const count = this.chips().length;
        if (count <= 1) {
          return;
        }
        this.activeIndex.update((index) => (index + 1) % count);
      });

    interval(FEATURED_ROTATE_MS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        const count = Math.min(this.featured().length, 3);
        if (count <= 1) {
          return;
        }
        this.featuredIndex.update((index) => (index + 1) % count);
      });
  }

  select(index: number): void {
    this.activeIndex.set(index);
  }

  categoryLabel(slug: string): string {
    return getCategoryLabel(this.locale(), slug);
  }

  ratingSummary(biz: DiscoveryBusinessItem): string {
    return formatRatingSummary(biz.ratingAvg, biz.reviewCount);
  }
}
