import { Component, inject, OnInit, signal, computed } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import type { BusinessProfile, Category, MarketConfig } from "@adeni/shared";
import { getCategoryLabel } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const PHONE_PATTERN = /^\+?[0-9\s-]{7,20}$/;
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

@Component({
  selector: "app-register",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, RouterLink],
  templateUrl: "./register.component.html",
  styleUrl: "./register.component.scss",
})
export class RegisterComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly categories = signal<Category[]>([]);
  readonly markets = signal<MarketConfig[]>([]);

  businessName = "";
  categorySlug = "";
  additionalCategorySlugs: string[] = [];
  phone = "";
  description = "";
  slug = "";
  addressLine = "";
  area = "";
  marketId = "lagos";

  readonly categoryLabel = getCategoryLabel;

  readonly additionalOptions = computed(() =>
    this.categories().filter((c) => c.slug !== this.categorySlug),
  );

  ngOnInit(): void {
    void this.load();
  }

  async loadCategoriesForMarket(): Promise<void> {
    const publicClient = this.api.createPublicClient();
    const categories = await publicClient.getCategories({
      market: this.marketId,
      wellness: true,
    });
    this.categories.set(categories);
    if (!categories.some((c) => c.slug === this.categorySlug)) {
      this.categorySlug = categories[0]?.slug ?? "hair-grooming";
      this.additionalCategorySlugs = this.additionalCategorySlugs.filter((s) => s !== this.categorySlug);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const publicClient = this.api.createPublicClient();
      const markets = await publicClient.getMarkets();
      this.markets.set(markets);
      this.marketId = markets[0]?.id ?? "lagos";
      await this.loadCategoriesForMarket();
      this.categorySlug = this.categories()[0]?.slug ?? "hair-grooming";

      try {
        const profile = await this.api.withAuthorizedClient((c) => c.getTenantProfile());
        this.profile.set(profile);
      } catch {
        this.profile.set(null);
      }
    } catch {
      this.error.set("Could not load registration form.");
    } finally {
      this.loading.set(false);
    }
  }

  async onMarketChange(): Promise<void> {
    try {
      await this.loadCategoriesForMarket();
    } catch {
      this.error.set("Could not reload categories for this market.");
    }
  }

  onPrimaryCategoryChange(): void {
    this.additionalCategorySlugs = this.additionalCategorySlugs.filter((s) => s !== this.categorySlug);
  }

  isAdditionalSelected(slug: string): boolean {
    return this.additionalCategorySlugs.includes(slug);
  }

  toggleAdditional(slug: string): void {
    if (slug === this.categorySlug) return;
    if (this.isAdditionalSelected(slug)) {
      this.additionalCategorySlugs = this.additionalCategorySlugs.filter((s) => s !== slug);
    } else {
      this.additionalCategorySlugs = [...this.additionalCategorySlugs, slug];
    }
  }

  suggestSlug(): void {
    if (this.slug.trim()) return;
    this.slug = this.businessName
      .trim()
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, "-")
      .replace(/^-|-$/g, "");
  }

  async submit(): Promise<void> {
    this.suggestSlug();
    const slug = this.slug.trim().toLowerCase();
    if (
      !this.businessName.trim() ||
      !PHONE_PATTERN.test(this.phone.trim()) ||
      !SLUG_PATTERN.test(slug) ||
      !this.addressLine.trim() ||
      !this.area.trim()
    ) {
      this.error.set("Fill in all required fields with valid formats.");
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((c) =>
        c.registerBusiness({
          businessName: this.businessName.trim(),
          categorySlug: this.categorySlug,
          additionalCategorySlugs:
            this.additionalCategorySlugs.length > 0 ? this.additionalCategorySlugs : undefined,
          phone: this.phone.trim(),
          description: this.description.trim() || undefined,
          location: {
            slug,
            addressLine: this.addressLine.trim(),
            area: this.area.trim(),
            marketId: this.marketId,
          },
        }),
      );
      await this.load();
    } catch {
      this.error.set("Registration failed. Slug may already be taken.");
    } finally {
      this.submitting.set(false);
    }
  }
}
