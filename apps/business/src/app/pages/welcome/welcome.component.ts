import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { Router } from "@angular/router";
import { AuthService } from "@auth0/auth0-angular";
import {
  formatPrice,
  getPlanDisplayForCountry,
  isSelfServePricingEnabled,
  SUBSCRIPTION_SKUS,
  SUBSCRIPTION_SKU_LABELS,
  type PublicCountryPricing,
  type SubscriptionSku,
} from "@adeni/shared";
import { AdeniBrandLockupComponent, AdeniLocaleService, injectAdeniTranslate } from "@adeni/ui";
import {
  ADENI_PORTAL_CONFIG,
  canAccessBusinessPortal,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../../core/adeni-config";
import { auth0FirstValueFrom } from "../../core/auth0-rxjs";
import { isPortalDevSignedOut, signInPortalDev } from "../../core/business-dev-session";
import { BusinessApiService } from "../../core/services/business-api.service";

const TAGLINE_KEYS: Record<SubscriptionSku, string> = {
  starter: "businessApp.plan.taglineStarter",
  professional: "businessApp.plan.taglineProfessional",
  business: "businessApp.plan.taglineBusiness",
};

/** Public supply landing — no auth guard. */
@Component({
  selector: "app-welcome",
  standalone: true,
  imports: [AdeniBrandLockupComponent],
  templateUrl: "./welcome.component.html",
  styleUrl: "./welcome.component.scss",
})
export class WelcomeComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly router = inject(Router);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly api = inject(BusinessApiService);
  private readonly localeService = inject(AdeniLocaleService);
  private readonly auth = inject(AuthService, { optional: true });

  readonly skus = SUBSCRIPTION_SKUS;
  readonly loading = signal(true);
  readonly apiPricing = signal<PublicCountryPricing | null>(null);

  /** Lagos-first pricing until portal market selector exists. */
  readonly countryCode = "NG";

  readonly discoverUrl = this.config.discoverWebUrl;
  readonly pricingOnDiscoverUrl = `${this.config.discoverWebUrl}/plan?from=business-welcome`;

  readonly portalAccessReady = canAccessBusinessPortal(this.config);
  readonly devSignedOut =
    isBusinessPortalDevMode(this.config) && isPortalDevSignedOut();

  readonly selfServeEnabled = computed(() => {
    const pricing = this.apiPricing();
    if (pricing) {
      return pricing.selfServeEnabled;
    }
    return isSelfServePricingEnabled(this.countryCode);
  });

  ngOnInit(): void {
    void this.loadPricing();
  }

  async loadPricing(): Promise<void> {
    this.loading.set(true);
    try {
      const pricing = await this.api.createPublicClient().getPublicPricing(this.countryCode);
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
    const pricing = this.apiPricing();
    const tagline = this.tr(TAGLINE_KEYS[sku]);

    if (pricing) {
      if (!pricing.selfServeEnabled) {
        return {
          name: SUBSCRIPTION_SKU_LABELS[sku],
          priceLabel: this.tr("businessApp.plan.contactUs"),
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

    const fallback = getPlanDisplayForCountry(this.countryCode, sku);
    if (fallback) {
      return {
        name: fallback.name,
        priceLabel: fallback.priceLabel,
        tagline,
      };
    }

    return {
      name: SUBSCRIPTION_SKU_LABELS[sku],
      priceLabel: this.tr("businessApp.plan.contactUs"),
      tagline,
    };
  }

  isPopular(sku: SubscriptionSku): boolean {
    return sku === "professional";
  }

  async startFree(): Promise<void> {
    if (!this.portalAccessReady) {
      return;
    }

    if (isBusinessPortalDevMode(this.config)) {
      if (isPortalDevSignedOut()) {
        signInPortalDev();
      }
      await this.router.navigateByUrl("/register");
      return;
    }

    if (isAuth0Configured(this.config) && this.auth) {
      await auth0FirstValueFrom(
        this.auth.loginWithRedirect({
          appState: { target: "/register" },
          authorizationParams: { screen_hint: "signup" },
        }),
      );
    }
  }

  async signIn(): Promise<void> {
    if (!this.portalAccessReady) {
      return;
    }

    if (isBusinessPortalDevMode(this.config)) {
      if (isPortalDevSignedOut()) {
        signInPortalDev();
      }
      await this.router.navigateByUrl("/dashboard");
      return;
    }

    if (isAuth0Configured(this.config) && this.auth) {
      await auth0FirstValueFrom(
        this.auth.loginWithRedirect({
          appState: { target: "/dashboard" },
        }),
      );
    }
  }
}
