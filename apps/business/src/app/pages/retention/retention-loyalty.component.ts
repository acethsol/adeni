import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { formatPrice, type LoyaltyProgram } from "@adeni/shared";
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

/**
 * Sprint 37.3-A shell: loyalty program settings. Slice A only stores the settings — nothing earns or redeems
 * points yet (slices B-C). Points are program credits for this business, never cash.
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
  readonly error = signal<string | null>(null);
  readonly configured = signal(false);
  readonly draft = signal<Draft>(this.toDraft(null));

  /** Only the owner may change what a point is worth; everyone else sees the settings read-only. */
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
      const program = await this.api.withAuthorizedClient((client) => client.retention.getLoyaltyProgram());
      this.configured.set(program.configured);
      this.draft.set(this.toDraft(program));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.loyalty.couldNotLoad"));
    } finally {
      this.loading.set(false);
    }
  }

  patch(patch: Partial<Draft>): void {
    this.draft.update((d) => ({ ...d, ...patch }));
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
}
