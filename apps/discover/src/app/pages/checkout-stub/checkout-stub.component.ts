import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { ActivatedRoute, RouterLink } from "@angular/router";
import { AdeniApiClient } from "@adeni/api-client";
import { formatPrice } from "@adeni/shared";
import { ADENI_DISCOVER_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-checkout-stub",
  standalone: true,
  imports: [RouterLink],
  templateUrl: "./checkout-stub.component.html",
  styleUrl: "./checkout-stub.component.scss",
})
export class CheckoutStubComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly config = inject(ADENI_DISCOVER_CONFIG);

  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  readonly paid = signal(false);
  readonly reference = signal("");
  readonly amount = signal<number | null>(null);
  readonly currency = signal("NGN");

  readonly amountLabel = computed(() => {
    const value = this.amount();
    if (value == null) {
      return null;
    }
    return formatPrice(value, this.currency());
  });

  ngOnInit(): void {
    const reference = this.route.snapshot.paramMap.get("reference")?.trim() ?? "";
    this.reference.set(reference);
    const params = this.route.snapshot.queryParamMap;
    const amountRaw = params.get("amount");
    const parsed = amountRaw != null ? Number(amountRaw) : NaN;
    if (Number.isFinite(parsed) && parsed > 0) {
      this.amount.set(parsed);
    }
    const currency = params.get("currency")?.trim();
    if (currency) {
      this.currency.set(currency);
    }
    if (!reference) {
      this.error.set("Payment link is invalid.");
    }
  }

  async confirmPayment(): Promise<void> {
    const reference = this.reference();
    if (!reference || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.error.set(null);
    try {
      const client = new AdeniApiClient({ baseUrl: this.config.apiBaseUrl });
      await client.confirmStubPayment(reference);
      this.paid.set(true);
    } catch {
      this.error.set("Could not complete this payment. Try again or ask the business to resend the link.");
    } finally {
      this.loading.set(false);
    }
  }
}
