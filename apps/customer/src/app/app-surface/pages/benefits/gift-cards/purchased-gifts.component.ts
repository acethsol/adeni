import { Component, DestroyRef, PLATFORM_ID, inject, signal } from "@angular/core";
import { isPlatformBrowser } from "@angular/common";
import { RouterLink } from "@angular/router";
import { CustomerApiService } from "../../../../core/services/customer-api.service";
import { formatPrice, type BusinessGiftCard } from "@adeni/shared";
import { AdeniCarbonIconComponent, injectAdeniApiError, injectAdeniTranslate } from "@adeni/ui";

type GiftTone = "pending" | "active" | "inactive";

/** Gift cards the signed-in customer purchased. Codes are fetched on demand and never kept after hide/destroy. */
@Component({
  selector: "app-purchased-gifts",
  standalone: true,
  imports: [RouterLink, AdeniCarbonIconComponent],
  templateUrl: "./purchased-gifts.component.html",
  styleUrl: "./purchased-gifts.component.scss",
})
export class PurchasedGiftsComponent {
  readonly tr = injectAdeniTranslate();
  readonly apiErr = injectAdeniApiError();
  private readonly api = inject(CustomerApiService);
  readonly cards = signal<BusinessGiftCard[]>([]);
  readonly loading = signal(true);
  readonly loadFailed = signal(false);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);
  readonly code = signal("");
  readonly revealedId = signal("");
  readonly formatPrice = formatPrice;
  private readonly abort = new AbortController();

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      this.abort.abort();
      this.hide();
      this.cards.set([]);
    });
    if (isPlatformBrowser(inject(PLATFORM_ID))) void this.load();
  }

  tone(card: BusinessGiftCard): GiftTone {
    if (card.state === "Active") return "active";
    if (card.state === "PendingPayment") return "pending";
    return "inactive";
  }

  hide(): void {
    this.code.set("");
    this.revealedId.set("");
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadFailed.set(false);
    this.error.set(null);
    try {
      const rows = await this.api.withAuthorizedClient((c) => c.commerce.purchasedCards(this.abort.signal));
      if (!this.abort.signal.aborted) this.cards.set(rows);
    } catch (e) {
      if (!this.abort.signal.aborted) {
        this.loadFailed.set(true);
        this.error.set(this.apiErr(e, "commerce.error"));
      }
    } finally {
      if (!this.abort.signal.aborted) this.loading.set(false);
    }
  }

  async reveal(id: string): Promise<void> {
    if (this.busy()) return;
    this.hide();
    this.error.set(null);
    this.busy.set(true);
    try {
      const result = await this.api.withAuthorizedClient((c) => c.commerce.reveal(id, this.abort.signal));
      if (!this.abort.signal.aborted) {
        this.code.set(result.code);
        this.revealedId.set(id);
      }
    } catch (e) {
      if (!this.abort.signal.aborted) this.error.set(this.apiErr(e, "commerce.error"));
    } finally {
      if (!this.abort.signal.aborted) this.busy.set(false);
    }
  }
}
