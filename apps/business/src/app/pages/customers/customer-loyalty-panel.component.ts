import { Component, computed, inject, input, OnInit, signal } from "@angular/core";
import {
  formatSlotTime,
  hasTenantEntitlement,
  mergeTenantEntitlements,
  type LoyaltyAccount,
  type LoyaltyTransaction,
  type ReferralCode,
  type TenantEntitlements,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
  type AdeniPillTone,
} from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-customer-loyalty-panel",
  standalone: true,
  imports: [AdeniCarbonIconComponent, AdeniPillComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./customer-loyalty-panel.component.html",
  styleUrl: "./customer-loyalty-panel.component.scss",
})
export class CustomerLoyaltyPanelComponent implements OnInit {
  readonly customerId = input.required<string>();
  protected readonly tr = injectAdeniTranslate();
  private readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly portalConfig = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly account = signal<LoyaltyAccount | null>(null);
  readonly transactions = signal<LoyaltyTransaction[]>([]);
  readonly referral = signal<ReferralCode | null>(null);

  protected readonly formatSlotTime = formatSlotTime;

  readonly hasLoyaltyData = computed(() => {
    const acc = this.account();
    if (!acc) {
      return false;
    }
    return acc.pointsBalance > 0 || acc.lifetimeEarned > 0 || this.transactions().length > 0;
  });

  readonly redeemProgress = computed((): { percent: number; hint: string } | null => {
    const acc = this.account();
    if (!acc || acc.lifetimeEarned <= 0) {
      return null;
    }
    const percent = Math.min(100, Math.round((acc.pointsBalance / acc.lifetimeEarned) * 100));
    return {
      percent,
      hint: this.tr("businessApp.loyalty.customerBalanceHint", {
        balance: acc.pointsBalance,
        earned: acc.lifetimeEarned,
      }),
    };
  });

  ngOnInit(): void {
    void this.load();
  }

  protected referralBookingUrl(ref: ReferralCode): string | null {
    if (!ref.sharePath) {
      return null;
    }
    const base = this.portalConfig.discoverWebUrl.replace(/\/$/, "");
    return ref.sharePath.startsWith("http") ? ref.sharePath : `${base}${ref.sharePath}`;
  }

  protected async copyReferralCode(code: string): Promise<void> {
    await this.copyText(code);
  }

  protected async copyText(value: string): Promise<void> {
    if (typeof navigator === "undefined" || !navigator.clipboard) {
      return;
    }
    try {
      await navigator.clipboard.writeText(value);
      this.feedback.success(this.tr("businessApp.loyalty.referralCopied"));
    } catch {
      /* ignore */
    }
  }

  protected kindTone(row: LoyaltyTransaction): AdeniPillTone {
    switch (row.kind) {
      case "earn":
        return "success";
      case "redeem":
        return "warning";
      case "expire":
        return "neutral";
      default:
        return "info";
    }
  }

  protected kindLabel(row: LoyaltyTransaction): string {
    switch (row.kind) {
      case "earn":
        return this.tr("businessApp.loyalty.kindEarn");
      case "redeem":
        return this.tr("businessApp.loyalty.kindRedeem");
      case "adjust":
        return this.tr("businessApp.loyalty.kindAdjust");
      case "expire":
        return this.tr("businessApp.loyalty.kindExpire");
      default:
        return row.kind;
    }
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    this.referral.set(null);
    try {
      const id = this.customerId();
      const [account, ledger, referral] = await this.api.withAuthorizedClient(async (client) => {
        const acc = await client.retention.getLoyaltyCustomerAccount(id);
        const tx = await client.retention.listLoyaltyCustomerTransactions(id, 50);
        const [planEntitlements, profile] = await Promise.all([
          client.getTenantEntitlements().catch(() => null),
          client.getTenantProfile().catch(() => null),
        ]);
        const merged: TenantEntitlements | null = mergeTenantEntitlements(
          planEntitlements,
          null,
          profile?.entitlements ?? null,
        );
        let ref: ReferralCode | null = null;
        if (hasTenantEntitlement(merged, "referrals")) {
          try {
            ref = await client.retention.getCustomerReferralCode(id);
          } catch {
            ref = null;
          }
        }
        return [acc, tx, ref] as const;
      });
      this.account.set(account);
      this.transactions.set(ledger.items);
      this.referral.set(referral);
    } catch (err) {
      this.error.set(this.apiErr(err, "businessApp.loyalty.couldNotLoadCustomer"));
    } finally {
      this.loading.set(false);
    }
  }
}
