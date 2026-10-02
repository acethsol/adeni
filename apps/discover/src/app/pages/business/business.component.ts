import { DecimalPipe } from "@angular/common";
import { Component, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import type { PublicBusinessProfile, ServiceOffering } from "@adeni/shared";
import {
  getCategoryLabel,
  resolveBusinessCoverImage,
  shouldShowQuoteFlow,
} from "@adeni/shared";
import {
  ADENI_DISCOVER_CONFIG,
  isDiscoverCustomerDevMode,
} from "../../core/adeni-config";
import { buildLocalBusinessJsonLd } from "../../core/seo-jsonld";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { SeoService } from "../../core/services/seo.service";
import { MarketContextService } from "../../core/services/market-context.service";
import { BookingPanelComponent } from "../../shared/booking-panel.component";
import { QuoteRequestPanelComponent } from "../../shared/quote-request-panel.component";

@Component({
  selector: "app-business-profile",
  standalone: true,
  imports: [
    RouterLink,
    DecimalPipe,
    BookingPanelComponent,
    QuoteRequestPanelComponent,
  ],
  templateUrl: "./business.component.html",
  styleUrl: "./business.component.scss",
})
export class BusinessProfileComponent implements OnInit {
  private readonly api = inject(CustomerApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly seo = inject(SeoService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly market = inject(MarketContextService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly profile = signal<PublicBusinessProfile | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly bookingEnabled = signal(false);

  readonly categoryLabel = getCategoryLabel;
  readonly coverFor = resolveBusinessCoverImage;
  readonly showQuoteFlow = shouldShowQuoteFlow;

  ngOnInit(): void {
    this.route.paramMap.subscribe((params) => {
      const slug = params.get("slug");
      if (slug) {
        void this.load(slug);
      }
    });
  }

  supportsDeposits(profile: PublicBusinessProfile): boolean {
    return profile.capabilities?.includes("deposits") ?? false;
  }

  async load(slug: string): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    const devEnabled = isDiscoverCustomerDevMode(this.config);
    const loggedIn = devEnabled || (await this.api.isLoggedIn());
    this.bookingEnabled.set(loggedIn);

    try {
      const [profile, services] = await Promise.all([
        this.api.createPublicClient().getBusinessProfile(slug),
        this.api.createPublicClient().getBusinessServices(slug).catch(() => [] as ServiceOffering[]),
      ]);
      this.profile.set(profile);
      this.services.set(services);
      this.applySeo(profile, slug);
    } catch {
      this.error.set("Business not found or API unavailable.");
      this.profile.set(null);
      this.services.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  private applySeo(profile: PublicBusinessProfile, slug: string): void {
    const marketName = this.market.market()?.name ?? "Adeni";
    const path = `/businesses/${slug}`;
    const cover = this.coverFor(profile.categorySlug, profile.coverImageUrl);
    const description =
      profile.description ||
      `Verified ${getCategoryLabel("en", profile.categorySlug)} in ${profile.area}.`;

    this.seo.update(
      {
        title: `${profile.name} — Adeni ${marketName}`,
        description,
        canonicalPath: path,
        imageUrl: cover,
        jsonLd: buildLocalBusinessJsonLd(
          profile,
          `${this.config.publicAppUrl}${path}`,
          cover,
        ),
      },
      this.config.publicAppUrl,
    );
  }
}
