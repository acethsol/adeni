import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { formatPrice, type LoyaltyProgram, type LoyaltyReward, type LoyaltyTier, type ReferralProgram } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  PortalPageComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { BusinessSessionService } from "../../core/services/business-session.service";

const CURRENCIES = ["NGN", "CAD", "USD"] as const;

type Draft = {
  isEnabled: boolean;
  currency: string;
  earnPoints: number | null;
  earnPerAmount: number | null;
  redeemPoints: number | null;
  redeemValue: number | null;
  minRedeemPoints: number | null;
  neverExpires: boolean;
  pointsExpiryMonths: number | null;
};

type TierDraft = { name: string; minLifetimeEarned: number; sortOrder: number };

type NewRewardDraft = { name: string; pointsCost: number | null; sortOrder: number };

type ReferralDraft = {
  isEnabled: boolean;
  referrerRewardPoints: number | null;
  refereeRewardPoints: number | null;
  maxRewardsPerReferrer: number | null;
  unlimitedCap: boolean;
};

/**
 * Loyalty program settings (37.3-A–C) plus tiers and reward catalog when `loyaltyTiers` is entitled (37.3-D).
 */
@Component({
  selector: "app-retention-loyalty",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./retention-loyalty.component.html",
  styleUrl: "./retention-loyalty.component.scss",
})
export class RetentionLoyaltyComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly session = inject(BusinessSessionService);

  readonly currencies = CURRENCIES;
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly tiersBusy = signal(false);
  readonly rewardsBusy = signal(false);
  readonly referralBusy = signal(false);
  readonly error = signal<string | null>(null);
  readonly configured = signal(false);
  readonly draft = signal<Draft>(this.toDraft(null));
  readonly loyaltyTiersEntitled = signal(false);
  readonly referralsEntitled = signal(false);
  readonly referralConfigured = signal(false);
  readonly referralDraft = signal<ReferralDraft>(this.toReferralDraft(null));
  readonly tierDrafts = signal<TierDraft[]>([]);
  readonly rewards = signal<LoyaltyReward[]>([]);
  readonly newReward = signal<NewRewardDraft>({ name: "", pointsCost: 100, sortOrder: 0 });

  readonly isOwner = computed(() => this.session.permissionRole() === "owner");

  readonly summaryEarn = computed(() => {
    const d = this.draft();
    if (!d.earnPoints || !d.earnPerAmount) return "";
    return this.tr("businessApp.loyalty.summaryEarn", {
      points: String(d.earnPoints),
      amount: formatPrice(d.earnPerAmount, d.currency),
    });
  });

  readonly summaryRedeem = computed(() => {
    const d = this.draft();
    if (!d.redeemPoints || !d.redeemValue) return "";
    return this.tr("businessApp.loyalty.summaryRedeem", {
      points: String(d.redeemPoints),
      value: formatPrice(d.redeemValue, d.currency),
    });
  });

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const program = await client.retention.getLoyaltyProgram();
        this.configured.set(program.configured);
        this.draft.set(this.toDraft(program));

        const entitlements = await client.getTenantEntitlements().catch(() => null);
        const tiersOn = entitlements?.loyaltyTiers === true;
        this.loyaltyTiersEntitled.set(tiersOn);
        if (tiersOn) {
          const tierList = await client.retention.listLoyaltyTiers();
          this.tierDrafts.set(tierList.items.map((t) => this.tierToDraft(t)));
          const rewardList = await client.retention.listLoyaltyRewards(false);
          this.rewards.set(rewardList.items);
        }

        const referralsOn = entitlements?.referrals === true;
        this.referralsEntitled.set(referralsOn);
        if (referralsOn) {
          const referral = await client.retention.getReferralProgram();
          this.referralConfigured.set(referral.configured);
          this.referralDraft.set(this.toReferralDraft(referral));
        }
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.loyalty.couldNotLoad"));
    } finally {
      this.loading.set(false);
    }
  }

  patch(patch: Partial<Draft>): void {
    this.draft.update((d) => ({ ...d, ...patch }));
  }

  patchTier(index: number, patch: Partial<TierDraft>): void {
    this.tierDrafts.update((rows) => rows.map((row, i) => (i === index ? { ...row, ...patch } : row)));
  }

  addTierRow(): void {
    this.tierDrafts.update((rows) => [...rows, { name: "", minLifetimeEarned: 0, sortOrder: rows.length }]);
  }

  removeTierRow(index: number): void {
    this.tierDrafts.update((rows) => rows.filter((_, i) => i !== index));
  }

  patchNewReward(patch: Partial<NewRewardDraft>): void {
    this.newReward.update((d) => ({ ...d, ...patch }));
  }

  canSave(): boolean {
    const d = this.draft();
    return (
      this.isOwner() &&
      !!d.earnPoints && d.earnPoints >= 1 && d.earnPoints <= 1000 &&
      !!d.earnPerAmount && d.earnPerAmount > 0 &&
      !!d.redeemPoints && d.redeemPoints >= 1 &&
      !!d.redeemValue && d.redeemValue > 0 &&
      d.minRedeemPoints !== null && d.minRedeemPoints >= 0 &&
      (d.neverExpires || (!!d.pointsExpiryMonths && d.pointsExpiryMonths >= 1 && d.pointsExpiryMonths <= 60))
    );
  }

  canSaveTiers(): boolean {
    if (!this.isOwner() || !this.loyaltyTiersEntitled()) return false;
    const rows = this.tierDrafts();
    if (rows.length === 0) return true;
    const thresholds = new Set<number>();
    return rows.every((r) => {
      if (r.name.trim().length === 0 || r.minLifetimeEarned < 0) return false;
      if (thresholds.has(r.minLifetimeEarned)) return false;
      thresholds.add(r.minLifetimeEarned);
      return true;
    });
  }

  canCreateReward(): boolean {
    const r = this.newReward();
    return this.isOwner() && this.loyaltyTiersEntitled() && r.name.trim().length > 0 && !!r.pointsCost && r.pointsCost >= 1;
  }

  async save(): Promise<void> {
    if (!this.canSave() || this.busy()) return;
    const d = this.draft();
    this.busy.set(true);
    try {
      const saved = await this.api.withAuthorizedClient((client) =>
        client.retention.saveLoyaltyProgram({
          isEnabled: d.isEnabled,
          currency: d.currency,
          earnPoints: d.earnPoints ?? 1,
          earnPerAmount: d.earnPerAmount ?? 1,
          redeemPoints: d.redeemPoints ?? 1,
          redeemValue: d.redeemValue ?? 1,
          minRedeemPoints: d.minRedeemPoints ?? 0,
          pointsExpiryMonths: d.neverExpires ? null : d.pointsExpiryMonths,
        }),
      );
      this.configured.set(saved.configured);
      this.draft.set(this.toDraft(saved));
      this.feedback.success(this.tr("businessApp.loyalty.saved"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.loyalty.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }

  async saveTiers(): Promise<void> {
    if (!this.canSaveTiers() || this.tiersBusy()) return;
    this.tiersBusy.set(true);
    try {
      const saved = await this.api.withAuthorizedClient((client) =>
        client.retention.saveLoyaltyTiers({
          tiers: this.tierDrafts().map((t) => ({
            name: t.name.trim(),
            minLifetimeEarned: t.minLifetimeEarned,
            sortOrder: t.sortOrder,
          })),
        }),
      );
      this.tierDrafts.set(saved.items.map((t) => this.tierToDraft(t)));
      this.feedback.success(this.tr("businessApp.loyalty.tiersSaved"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.loyalty.couldNotSaveTiers"));
    } finally {
      this.tiersBusy.set(false);
    }
  }

  async createReward(): Promise<void> {
    if (!this.canCreateReward() || this.rewardsBusy()) return;
    const r = this.newReward();
    this.rewardsBusy.set(true);
    try {
      const created = await this.api.withAuthorizedClient((client) =>
        client.retention.createLoyaltyReward({
          name: r.name.trim(),
          kind: "free_service",
          pointsCost: r.pointsCost ?? 1,
          sortOrder: r.sortOrder,
        }),
      );
      this.rewards.update((list) => [...list, created].sort((a, b) => a.sortOrder - b.sortOrder));
      this.newReward.set({ name: "", pointsCost: 100, sortOrder: this.rewards().length });
      this.feedback.success(this.tr("businessApp.loyalty.rewardCreated"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.loyalty.couldNotCreateReward"));
    } finally {
      this.rewardsBusy.set(false);
    }
  }

  async toggleRewardActive(reward: LoyaltyReward): Promise<void> {
    if (!this.isOwner() || this.rewardsBusy()) return;
    this.rewardsBusy.set(true);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.retention.updateLoyaltyReward(reward.id, {
          name: reward.name,
          description: reward.description,
          pointsCost: reward.pointsCost,
          serviceOfferingId: reward.serviceOfferingId,
          isActive: !reward.isActive,
          sortOrder: reward.sortOrder,
        }),
      );
      this.rewards.update((list) => list.map((r) => (r.id === updated.id ? updated : r)));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.loyalty.couldNotUpdateReward"));
    } finally {
      this.rewardsBusy.set(false);
    }
  }

  private toDraft(program: LoyaltyProgram | null): Draft {
    return {
      isEnabled: program?.isEnabled ?? false,
      currency: program?.currency ?? "NGN",
      earnPoints: program?.earnPoints ?? 1,
      earnPerAmount: program?.earnPerAmount ?? 100,
      redeemPoints: program?.redeemPoints ?? 100,
      redeemValue: program?.redeemValue ?? 500,
      minRedeemPoints: program?.minRedeemPoints ?? 100,
      neverExpires: program ? program.pointsExpiryMonths === null : false,
      pointsExpiryMonths: program?.pointsExpiryMonths ?? 12,
    };
  }

  private tierToDraft(tier: LoyaltyTier): TierDraft {
    return { name: tier.name, minLifetimeEarned: tier.minLifetimeEarned, sortOrder: tier.sortOrder };
  }

  patchReferral(patch: Partial<ReferralDraft>): void {
    this.referralDraft.update((d) => ({ ...d, ...patch }));
  }

  canSaveReferral(): boolean {
    const d = this.referralDraft();
    return (
      this.isOwner() &&
      this.referralsEntitled() &&
      d.referrerRewardPoints !== null &&
      d.refereeRewardPoints !== null &&
      d.referrerRewardPoints >= 0 &&
      d.refereeRewardPoints >= 0 &&
      (d.unlimitedCap || (d.maxRewardsPerReferrer !== null && d.maxRewardsPerReferrer >= 0)) &&
      (!d.isEnabled || (d.referrerRewardPoints ?? 0) > 0 || (d.refereeRewardPoints ?? 0) > 0)
    );
  }

  async saveReferral(): Promise<void> {
    if (!this.canSaveReferral() || this.referralBusy()) return;
    const d = this.referralDraft();
    this.referralBusy.set(true);
    try {
      const saved = await this.api.withAuthorizedClient((client) =>
        client.retention.saveReferralProgram({
          isEnabled: d.isEnabled,
          referrerRewardPoints: d.referrerRewardPoints ?? 0,
          refereeRewardPoints: d.refereeRewardPoints ?? 0,
          maxRewardsPerReferrer: d.unlimitedCap ? null : d.maxRewardsPerReferrer,
        }),
      );
      this.referralConfigured.set(saved.configured);
      this.referralDraft.set(this.toReferralDraft(saved));
      this.feedback.success(this.tr("businessApp.loyalty.referralSaved"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.loyalty.couldNotSaveReferral"));
    } finally {
      this.referralBusy.set(false);
    }
  }

  private toReferralDraft(program: ReferralProgram | null): ReferralDraft {
    return {
      isEnabled: program?.isEnabled ?? false,
      referrerRewardPoints: program?.referrerRewardPoints ?? 100,
      refereeRewardPoints: program?.refereeRewardPoints ?? 50,
      maxRewardsPerReferrer: program?.maxRewardsPerReferrer ?? 10,
      unlimitedCap: program ? program.maxRewardsPerReferrer === null : false,
    };
  }
}
