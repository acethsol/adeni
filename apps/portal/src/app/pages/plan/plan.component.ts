import { Component, inject, OnInit, signal } from "@angular/core";
import {
  formatBookingUsage,
  isNearBookingLimit,
  PLAN_COMPARISON,
  PLAN_PRICING,
  type SubscriptionTier,
  type SubscriptionUsage,
} from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-plan",
  standalone: true,
  imports: [PortalPageComponent],
  templateUrl: "./plan.component.html",
  styleUrl: "./plan.component.scss",
})
export class PlanComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly currentTier = signal<SubscriptionTier>("free");
  readonly usage = signal<SubscriptionUsage | null>(null);

  readonly tiers: SubscriptionTier[] = ["free", "pro", "business"];
  readonly planPricing = PLAN_PRICING;
  readonly planComparison = PLAN_COMPARISON;
  readonly formatBookingUsage = formatBookingUsage;
  readonly isNearBookingLimit = isNearBookingLimit;

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [profile, subscriptionUsage] = await Promise.all([
          c.getTenantProfile(),
          c.getTenantSubscriptionUsage(),
        ]);
        this.currentTier.set(profile.subscriptionTier ?? subscriptionUsage.tier);
        this.usage.set(subscriptionUsage);
      });
    } catch {
      this.error.set("Could not load plan details.");
    } finally {
      this.loading.set(false);
    }
  }

  featureCell(value: string | boolean): string {
    if (value === true) return "Yes";
    if (value === false) return "—";
    return value;
  }
}
