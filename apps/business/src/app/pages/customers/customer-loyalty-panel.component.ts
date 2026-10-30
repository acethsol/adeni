import { Component, inject, input, OnInit, signal } from "@angular/core";
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
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  injectAdeniApiError,
  injectAdeniTranslate,
} from "@adeni/ui";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-customer-loyalty-panel",
  standalone: true,
  imports: [AdeniPortalEmptyStateComponent],
  template: `
    @if (loading()) {
      <p class="muted" role="status">{{ tr("toast.loading") }}</p>
    } @else if (error()) {
      <p class="error-banner" role="alert">{{ error() }}</p>
    } @else {
      @if (referral(); as ref) {
        <section class="referral-block" aria-labelledby="customer-referral-heading">
          <h3 id="customer-referral-heading">{{ tr("businessApp.loyalty.customerReferralHeading") }}</h3>
          <p class="muted">{{ tr("businessApp.loyalty.customerReferralHint") }}</p>
          <p class="referral-block__code">{{ ref.code }}</p>
          <div class="referral-block__actions">
            <button type="button" class="btn btn-secondary btn-sm" (click)="copyReferralCode(ref.code)">
              {{ tr("businessApp.loyalty.copyReferralCode") }}
            </button>
            @if (referralBookingUrl(ref); as bookingUrl) {
              <button type="button" class="btn btn-secondary btn-sm" (click)="copyText(bookingUrl)">
                {{ tr("businessApp.loyalty.copyReferralLink") }}
              </button>
            }
          </div>
        </section>
      }

      @if (!account() || (account()!.pointsBalance === 0 && transactions().length === 0)) {
        <adeni-portal-empty-state
          icon="user--filled"
          [title]="tr('businessApp.loyalty.customerEmptyTitle')"
          [description]="tr('businessApp.loyalty.customerEmptyDescription')"
        />
      } @else {
        <dl class="loyalty-summary">
          <div>
            <dt>{{ tr("businessApp.loyalty.pointsBalance") }}</dt>
            <dd class="loyalty-summary__balance">{{ account()!.pointsBalance }}</dd>
          </div>
          <div>
            <dt>{{ tr("businessApp.loyalty.lifetimeEarned") }}</dt>
            <dd>{{ account()!.lifetimeEarned }}</dd>
          </div>
          <div>
            <dt>{{ tr("businessApp.loyalty.lifetimeRedeemed") }}</dt>
            <dd>{{ account()!.lifetimeRedeemed }}</dd>
          </div>
        </dl>
        @if (transactions().length > 0) {
          <table class="loyalty-ledger">
            <thead>
              <tr>
                <th scope="col">{{ tr("businessApp.loyalty.when") }}</th>
                <th scope="col">{{ tr("businessApp.loyalty.kind") }}</th>
                <th scope="col">{{ tr("businessApp.loyalty.points") }}</th>
                <th scope="col">{{ tr("businessApp.loyalty.balanceAfter") }}</th>
              </tr>
            </thead>
            <tbody>
              @for (row of transactions(); track row.id) {
                <tr>
                  <td>{{ formatSlotTime(row.occurredAt) }}</td>
                  <td>{{ kindLabel(row) }}</td>
                  <td>{{ row.points > 0 ? "+" + row.points : row.points }}</td>
                  <td>{{ row.balanceAfter }}</td>
                </tr>
              }
            </tbody>
          </table>
        }
      }
    }
  `,
  styles: `
    .referral-block {
      margin-bottom: 1.25rem;
      padding: 1rem;
      border: 1px solid var(--cds-border-subtle);
      border-radius: 0.25rem;
    }
    .referral-block__code {
      font-family: var(--cds-code-01-font-family, monospace);
      font-size: 1.125rem;
      font-weight: 600;
      margin: 0.5rem 0;
    }
    .referral-block__actions {
      display: flex;
      flex-wrap: wrap;
      gap: 0.5rem;
    }
    .loyalty-summary {
      display: grid;
      gap: 1rem;
      grid-template-columns: repeat(auto-fit, minmax(8rem, 1fr));
      margin-bottom: 1.25rem;
    }
    .loyalty-summary__balance {
      font-size: 1.5rem;
      font-weight: 600;
    }
    .loyalty-ledger {
      width: 100%;
      border-collapse: collapse;
    }
    .loyalty-ledger th,
    .loyalty-ledger td {
      padding: 0.5rem 0.75rem;
      text-align: left;
      border-bottom: 1px solid var(--cds-border-subtle);
    }
  `,
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
}
