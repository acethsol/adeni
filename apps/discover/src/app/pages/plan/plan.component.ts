import { Component, computed, inject, OnInit, signal } from "@angular/core";
import {
  formatPrice,
  getMarketById,
  getPlanDisplayForCountry,
  isSelfServePricingEnabled,
  PLAN_COMPARISON,
  planComparisonCell,
  SUBSCRIPTION_SKUS,
  SUBSCRIPTION_SKU_LABELS,
  type PublicCountryPricing,
  type SubscriptionSku,
} from "@adeni/shared";
import { AdeniLocaleService } from "@adeni/ui";
import {
  ADENI_DISCOVER_CONFIG,
} from "../../core/adeni-config";
import { CustomerApiService } from "../../core/services/customer-api.service";
import { MarketContextService } from "../../core/services/market-context.service";

const OUTCOME_TAGLINES: Record<SubscriptionSku, string> = {
  starter: "Get bookable",
  professional: "Run & grow",
  business: "Scale operations",
};

@Component({
  selector: "app-plan-page",
  standalone: true,
  imports: [],
  templateUrl: "./plan.component.html",
  styleUrl: "./plan.component.scss",
})
export class PlanPageComponent implements OnInit {
  private readonly api = inject(CustomerApiService);
  private readonly marketContext = inject(MarketContextService);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);
  private readonly localeService = inject(AdeniLocaleService);

  readonly loading = signal(true);
  readonly apiPricing = signal<PublicCountryPricing | null>(null);

  readonly skus = SUBSCRIPTION_SKUS;
  readonly planComparison = PLAN_COMPARISON;
  readonly comparisonCell = planComparisonCell;

  readonly countryCode = computed(() => {
    const market = this.marketContext.market();
    if (market) {
      return market.countryCode;
    }
    return getMarketById(this.config.defaultMarketId)?.countryCode ?? "NG";
  });

  readonly registerUrl = computed(() => {
    const portal = this.config.portalAppUrl.trim();
    return portal ? `${portal.replace(/\/$/, "")}/register` : null;
  });

  readonly selfServeEnabled = computed(() => {
    const pricing = this.apiPricing();
    if (pricing) {
      return pricing.selfServeEnabled;
    }
    return isSelfServePricingEnabled(this.countryCode());
  });

  readonly platformFeePercent = computed(() => {
    const pricing = this.apiPricing();
    if (pricing) {
      return pricing.platformFeePercent;
    }
    return null;
  });

  ngOnInit(): void {
    void this.loadPricing();
  }

  async loadPricing(): Promise<void> {
    this.loading.set(true);
    const country = this.countryCode();
    try {
      const pricing = await this.api.createPublicClient().getPublicPricing(country);
      this.apiPricing.set(pricing);
    } catch {
      this.apiPricing.set(null);
    } finally {
      this.loading.set(false);
    }
  }

  planDisplay(sku: SubscriptionSku): {
    name: string;
    priceLabel: string;
    tagline: string;
  } {
    const country = this.countryCode();
    const pricing = this.apiPricing();
    const tagline = OUTCOME_TAGLINES[sku];

    if (pricing) {
      if (!pricing.selfServeEnabled) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: "Contact us",
          tagline,
        };
      }

      const row = pricing.skus.find((item) => item.sku === sku);
      const amount = row?.monthlyAmountMajor;
      if (typeof amount === "number" && amount > 0) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: `${formatPrice(amount, pricing.currencyCode, this.localeService.locale())}/mo`,
          tagline,
        };
      }
    }

    const fallback = getPlanDisplayForCountry(country, sku);
    if (fallback) {
      return {
        name: fallback.name,
        priceLabel: fallback.priceLabel,
        tagline,
      };
    }

    return {
      name: SUBSCRIPTION_SKU_LABELS[sku],
      priceLabel: "Contact us",
      tagline,
    };
  }

  isPopular(sku: SubscriptionSku): boolean {
    return sku === "professional";
  }
}
