import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  QUOTE_STATUS_LABELS,
  formatPrice,
  formatSlotTime,
  type QuoteRequestResponse,
  type ServiceOffering,
} from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

@Component({
  selector: "app-quotes",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./quotes.component.html",
  styleUrl: "./quotes.component.scss",
})
export class QuotesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly quotes = signal<QuoteRequestResponse[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly selectedId = signal<string | null>(null);
  readonly busy = signal(false);

  amount = "";
  currency = "NGN";
  notes = "";
  serviceOfferingId = "";
  proposedStart = "";
  proposedEnd = "";

  readonly formatSlotTime = formatSlotTime;
  readonly formatPrice = formatPrice;
  readonly statusLabel = (status: string) => QUOTE_STATUS_LABELS[status] ?? status;

  ngOnInit(): void {
    void this.load();
  }

  openQuotes(): QuoteRequestResponse[] {
    return this.quotes().filter((quote) => quote.status === "submitted" || quote.status === "quoted");
  }

  closedQuotes(): QuoteRequestResponse[] {
    return this.quotes().filter((quote) => quote.status !== "submitted" && quote.status !== "quoted");
  }

  selected(): QuoteRequestResponse | null {
    return this.quotes().find((quote) => quote.id === this.selectedId()) ?? null;
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const [quotes, services] = await this.api.withAuthorizedClient((client) =>
        Promise.all([client.listTenantQuotes(), client.getTenantServices()]),
      );
      this.quotes.set(quotes);
      this.services.set(services.filter((service) => service.isActive));
      if (services[0]?.currency) this.currency = services[0].currency;
    } catch {
      this.error.set("Could not load quote requests.");
    } finally {
      this.loading.set(false);
    }
  }

  startOffer(quote: QuoteRequestResponse): void {
    this.selectedId.set(quote.id);
    this.amount = quote.quotedAmount ? String(quote.quotedAmount) : "";
    this.currency = quote.quotedCurrency ?? this.currency;
    this.notes = quote.quoteNotes ?? "";
    this.serviceOfferingId = quote.serviceOfferingId ?? this.services()[0]?.id ?? "";
    const start = quote.proposedStartAt ? new Date(quote.proposedStartAt) : defaultStart();
    const end = quote.proposedEndAt
      ? new Date(quote.proposedEndAt)
      : new Date(start.getTime() + 60 * 60_000);
    this.proposedStart = toLocalInput(start);
    this.proposedEnd = toLocalInput(end);
  }

  cancelOffer(): void {
    this.selectedId.set(null);
  }

  async sendOffer(): Promise<void> {
    const quote = this.selected();
    const amount = Number(this.amount);
    if (!quote || !this.serviceOfferingId || !Number.isFinite(amount) || amount <= 0) {
      this.error.set("Add an amount, a service, and a time window.");
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    try {
      const updated = await this.api.withAuthorizedClient((client) =>
        client.submitQuoteOffer(quote.id, {
          amount,
          currency: this.currency.trim().toUpperCase(),
          notes: this.notes.trim() || undefined,
          serviceOfferingId: this.serviceOfferingId,
          proposedStartAt: new Date(this.proposedStart).toISOString(),
          proposedEndAt: new Date(this.proposedEnd).toISOString(),
        }),
      );
      this.quotes.update((items) => items.map((item) => (item.id === updated.id ? updated : item)));
      this.selectedId.set(null);
    } catch {
      this.error.set("Could not send that quote.");
    } finally {
      this.busy.set(false);
    }
  }
}

function defaultStart(): Date {
  const date = new Date();
  date.setDate(date.getDate() + 2);
  date.setHours(10, 0, 0, 0);
  return date;
}

function toLocalInput(date: Date): string {
  const pad = (value: number) => String(value).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}
