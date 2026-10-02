import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { RouterLink } from "@angular/router";
import type { BusinessProfile, Category, MarketConfig } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
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
  phone = "";
  description = "";
  slug = "";
  addressLine = "";
  area = "";
  marketId = "lagos";

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    try {
      const publicClient = this.api.createPublicClient();
      const [categories, markets] = await Promise.all([
        publicClient.getCategories(),
        publicClient.getMarkets(),
      ]);
      this.categories.set(categories);
      this.markets.set(markets);
      this.categorySlug = categories[0]?.slug ?? "barbers";
      this.marketId = markets[0]?.id ?? "lagos";

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
