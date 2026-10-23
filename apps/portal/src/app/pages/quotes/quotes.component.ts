import { Component, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { Subscription } from "rxjs";
import { FormsModule } from "@angular/forms";
import {
  QUOTE_STATUS_LABELS,
  formatPrice,
  formatSlotTime,
  type QuoteRequestResponse,
  type ServiceOffering,
} from "@adeni/shared";
import {
  AdeniFeedbackService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { TenantOperationsRealtimeService } from "@adeni/ui";

@Component({
  selector: "app-quotes",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniPortalEmptyStateComponent],
  templateUrl: "./quotes.component.html",
  styleUrl: "./quotes.component.scss",
})
export class QuotesComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private readonly feedback = inject(AdeniFeedbackService);
  private opsSub: Subscription | null = null;

  readonly error = signal<string | null>(null);
  readonly quotes = signal<QuoteRequestResponse[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly selectedId = signal<string | null>(null);
  readonly busy = signal(false);
  readonly pdfBusyId = signal<string | null>(null);

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
    this.opsSub = this.operationsRealtime.quotePipelineChanged$.subscribe(() => {
      void this.load();
    });
  }

  ngOnDestroy(): void {
    this.opsSub?.unsubscribe();
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
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const [quotes, services] = await this.api.withAuthorizedClient((client) =>
          Promise.all([client.listTenantQuotes(), client.getTenantServices()]),
        );
        this.quotes.set(quotes);
        this.services.set(services.filter((service) => service.isActive));
        if (services[0]?.currency) this.currency = services[0].currency;
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.quotes.couldNotLoadQuoteRequests"));
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
      this.error.set(this.tr("portal.quotes.addAmountServiceTimeWindow"));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.quotes.couldNotSendQuote"));
    } finally {
      this.busy.set(false);
    }
  }

  canDownloadPdf(quote: QuoteRequestResponse): boolean {
    return quote.status === "quoted" || quote.status === "accepted" || quote.status === "declined";
  }

  async downloadQuotePdf(quote: QuoteRequestResponse): Promise<void> {
    this.pdfBusyId.set(quote.id);
    this.error.set(null);
    try {
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantQuotePdf(quote.id),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `quote-${quote.id}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.quotes.couldNotDownloadQuotePdf"));
    } finally {
      this.pdfBusyId.set(null);
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
