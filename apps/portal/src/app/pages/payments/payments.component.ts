import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessProfile, PaymentLedgerEntry } from "@adeni/shared";
import { hasCapability } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { formatPrice } from "@adeni/shared";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-payments",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./payments.component.html",
  styleUrl: "./payments.component.scss",
})
export class PaymentsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly ledger = signal<PaymentLedgerEntry[]>([]);
  readonly lastLink = signal<{ checkoutUrl: string; description: string } | null>(null);

  amount = "";
  description = "";
  readonly formatPrice = formatPrice;

  ngOnInit(): void {
    void this.load();
  }

  supportsPayments(): boolean {
    return hasCapability(this.profile()?.capabilities, "deposits");
  }

  defaultCurrency(): string {
    const market = this.profile()?.locations[0]?.marketId?.toLowerCase();
    if (market === "ottawa" || market === "toronto") return "CAD";
    if (market === "houston" || market === "dallas") return "USD";
    return "NGN";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const profile = await c.getTenantProfile();
        this.profile.set(profile);
        const items = await c.listPaymentLedger(profile.tenantId);
        this.ledger.set(items);
      });
    } catch {
      this.error.set("Could not load payments.");
    } finally {
      this.loading.set(false);
    }
  }

  async createLink(): Promise<void> {
    const profile = this.profile();
    const parsed = Number(this.amount);
    if (!profile || !Number.isFinite(parsed) || parsed <= 0 || !this.description.trim()) {
      this.error.set("Enter a valid amount and description.");
      return;
    }

    this.creating.set(true);
    this.error.set(null);
    try {
      const intent = await this.api.withAuthorizedClient((c) =>
        c.createPaymentLink({
          tenantId: profile.tenantId,
          amount: parsed,
          currency: this.defaultCurrency(),
          description: this.description.trim(),
        }),
      );
      const checkoutUrl = intent.checkoutUrl.startsWith("http")
        ? intent.checkoutUrl
        : `${this.config.discoverWebUrl}${intent.checkoutUrl}`;
      this.lastLink.set({ checkoutUrl, description: this.description.trim() });
      this.amount = "";
      this.description = "";
      await this.load();
    } catch {
      this.error.set("Could not create payment link.");
    } finally {
      this.creating.set(false);
    }
  }

  whatsAppUrl(link: { checkoutUrl: string; description: string }): string {
    const text = encodeURIComponent(`Pay ${link.description} via Adeni: ${link.checkoutUrl}`);
    return `https://wa.me/?text=${text}`;
  }

  async refund(entry: PaymentLedgerEntry): Promise<void> {
    const profile = this.profile();
    if (!profile || entry.status !== "completed") return;
    if (!confirm(`Refund ${formatPrice(entry.amount, entry.currency)}?`)) return;

    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((c) =>
        c.refundPayment(entry.id, { tenantId: profile.tenantId }),
      );
      await this.load();
    } catch {
      this.error.set("Refund failed.");
    }
  }
}
