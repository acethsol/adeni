import { Component, inject, input, OnInit, signal } from "@angular/core";
import { formatSlotTime, type LoyaltyAccount, type LoyaltyTransaction } from "@adeni/shared";
import { AdeniPortalEmptyStateComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";
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
    } @else if (!account() || account()!.pointsBalance === 0 && transactions().length === 0) {
      <adeni-portal-empty-state
        icon="star"
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
  `,
  styles: `
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

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly account = signal<LoyaltyAccount | null>(null);
  readonly transactions = signal<LoyaltyTransaction[]>([]);

  protected readonly formatSlotTime = formatSlotTime;

  ngOnInit(): void {
    void this.load();
  }

  private async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const id = this.customerId();
      const [account, ledger] = await this.api.withAuthorizedClient(async (client) => {
        const acc = await client.retention.getLoyaltyCustomerAccount(id);
        const tx = await client.retention.listLoyaltyCustomerTransactions(id, 50);
        return [acc, tx] as const;
      });
      this.account.set(account);
      this.transactions.set(ledger.items);
    } catch (err) {
      this.error.set(this.apiErr.message(err, "businessApp.loyalty.couldNotLoadCustomer"));
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
