import { DatePipe } from "@angular/common";
import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute } from "@angular/router";
import type {
  AdminBusinessSummary,
  AdminTenantCommercialTerms,
  SetTenantCommercialTermsRequest,
  SubscriptionSku,
  SubscriptionTier,
  TenantBillingStatus,
} from "@adeni/shared";
import { AdeniFeedbackService, PortalPageComponent } from "@adeni/ui";
import { AdminApiService } from "../../core/services/admin-api.service";

type CommercialDraft = {
  trialDays: number | null;
  trialSku: SubscriptionSku;
  endTrial: boolean;
  subscriptionSku: SubscriptionSku;
  billingStatus: TenantBillingStatus;
  discountPercent: number | null;
  discountFixedAmount: number | null;
  discountCurrency: string;
  discountCyclesRemaining: number | null;
  customSaasMonthlyAmount: number | null;
  customSaasCurrency: string;
  customSaasSku: SubscriptionSku | "";
  billingCompReason: string;
  clearDiscounts: boolean;
  clearCustomSaasPrice: boolean;
};

@Component({
  selector: "app-admin-businesses",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, DatePipe],
  templateUrl: "./businesses.component.html",
  styleUrl: "./businesses.component.scss",
})
export class AdminBusinessesComponent implements OnInit {
  private readonly api = inject(AdminApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly businesses = signal<AdminBusinessSummary[]>([]);
  readonly query = signal("");
  readonly visible = computed(() =>
    filterByQuery(this.businesses(), this.query(), (item) => [
      item.name,
      item.slug,
      item.subscriptionTier,
      item.billingStatus,
    ]),
  );
  readonly tiers: SubscriptionTier[] = ["free", "pro", "business"];
  readonly skus: SubscriptionSku[] = ["starter", "professional", "business"];
  readonly billingStatuses: TenantBillingStatus[] = [
    "trialing",
    "active",
    "past_due",
    "suspended",
    "canceled",
  ];

  readonly editingId = signal<string | null>(null);
  readonly commercialDraft = signal<CommercialDraft | null>(null);

  constructor() {
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      this.query.set(params.get("q") ?? "");
    });
  }

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const items = await this.api.withAuthorizedClient((c) => c.getAdminBusinesses());
        this.businesses.set(items);
      }, "Loading businesses…", "Fetching tenant list");
    } catch {
      this.error.set("Could not load businesses.");
      this.feedback.error("Could not load businesses.");
    } finally {
      this.loading.set(false);
    }
  }

  async setTier(business: AdminBusinessSummary, tier: SubscriptionTier): Promise<void> {
    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.setAdminBusinessSubscriptionTier(business.id, tier),
          ),
        "Updating plan…",
        business.name,
      );
      await this.load();
      this.feedback.success(`${business.name} is now on ${tier}.`, "Subscription updated");
    } catch {
      this.error.set("Could not update subscription tier.");
      this.feedback.error("Could not update subscription tier.");
    }
  }

  async openCommercial(business: AdminBusinessSummary): Promise<void> {
    try {
      const terms = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) => c.getAdminTenantCommercialTerms(business.id)),
        "Loading commercial terms…",
        business.name,
      );
      this.editingId.set(business.id);
      this.commercialDraft.set(draftFromTerms(terms));
    } catch {
      this.feedback.error("Could not load commercial terms.");
    }
  }

  closeCommercial(): void {
    this.editingId.set(null);
    this.commercialDraft.set(null);
  }

  async saveCommercial(business: AdminBusinessSummary): Promise<void> {
    const draft = this.commercialDraft();
    if (!draft) {
      return;
    }

    const body: SetTenantCommercialTermsRequest = {
      subscriptionSku: draft.subscriptionSku,
      billingStatus: draft.billingStatus,
      billingCompReason: draft.billingCompReason.trim() || null,
    };

    if (draft.endTrial) {
      body.endTrial = true;
    } else if (draft.trialDays != null) {
      body.trialDays = draft.trialDays;
      body.trialSku = draft.trialSku;
    }

    if (draft.clearDiscounts) {
      body.clearDiscounts = true;
    } else {
      if (draft.discountPercent != null) {
        body.discountPercent = draft.discountPercent;
      }
      if (draft.discountFixedAmount != null) {
        body.discountFixedAmount = draft.discountFixedAmount;
      }
      if (draft.discountCurrency.trim()) {
        body.discountCurrency = draft.discountCurrency.trim().toUpperCase();
      }
      if (draft.discountCyclesRemaining != null) {
        body.discountCyclesRemaining = draft.discountCyclesRemaining;
      }
    }

    if (draft.clearCustomSaasPrice) {
      body.clearCustomSaasPrice = true;
    } else if (draft.customSaasMonthlyAmount != null && draft.customSaasCurrency.trim()) {
      body.customSaasMonthlyAmount = draft.customSaasMonthlyAmount;
      body.customSaasCurrency = draft.customSaasCurrency.trim().toUpperCase();
      if (draft.customSaasSku) {
        body.customSaasSku = draft.customSaasSku;
      }
    }

    try {
      await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.setAdminTenantCommercialTerms(business.id, body),
          ),
        "Saving commercial terms…",
        business.name,
      );
      this.closeCommercial();
      await this.load();
      this.feedback.success("Commercial terms saved.", business.name);
    } catch {
      this.feedback.error("Could not save commercial terms.");
    }
  }

  updateDraft(partial: Partial<CommercialDraft>): void {
    const current = this.commercialDraft();
    if (!current) {
      return;
    }
    this.commercialDraft.set({ ...current, ...partial });
  }
}

function draftFromTerms(terms: AdminTenantCommercialTerms): CommercialDraft {
  return {
    trialDays: 30,
    trialSku: (terms.trialSku as SubscriptionSku | null) ?? "professional",
    endTrial: terms.billingStatus === "active" && !terms.trialEndsAt,
    subscriptionSku: terms.subscriptionSku,
    billingStatus: terms.billingStatus,
    discountPercent: terms.discountPercent ?? null,
    discountFixedAmount: terms.discountFixedAmount ?? null,
    discountCurrency: terms.discountCurrency ?? "",
    discountCyclesRemaining: terms.discountCyclesRemaining ?? null,
    customSaasMonthlyAmount: terms.customSaasMonthlyAmount ?? null,
    customSaasCurrency: terms.customSaasCurrency ?? "",
    customSaasSku: (terms.customSaasSku as SubscriptionSku | null) ?? "",
    billingCompReason: terms.billingCompReason ?? "",
    clearDiscounts: false,
    clearCustomSaasPrice: false,
  };
}

function filterByQuery<T>(
  items: T[],
  query: string,
  parts: (item: T) => Array<string | null | undefined>,
): T[] {
  const needle = query.trim().toLowerCase();
  if (!needle) {
    return items;
  }
  return items.filter((item) =>
    parts(item)
      .filter((part): part is string => Boolean(part))
      .join(" ")
      .toLowerCase()
      .includes(needle),
  );
}
