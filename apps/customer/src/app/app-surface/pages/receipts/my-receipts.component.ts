import { Component, inject, OnInit, signal } from "@angular/core";
import { RouterLink } from "@angular/router";
import { formatPrice, type BillingDocumentResponse } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniFeedbackService,
  AdeniLocaleService,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { CustomerApiService } from "../../../core/services/customer-api.service";

@Component({
  selector: "app-my-receipts",
  standalone: true,
  imports: [
    RouterLink,
    PortalPageComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
  ],
  templateUrl: "./my-receipts.component.html",
  styleUrl: "./my-receipts.component.scss",
})
export class MyReceiptsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  private readonly api = inject(CustomerApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly locale = inject(AdeniLocaleService).locale;

  readonly loading = signal(true);
  readonly failed = signal(false);
  readonly documents = signal<BillingDocumentResponse[]>([]);

  ngOnInit(): void {
    void this.load();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.failed.set(false);
    try {
      const items = await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.listMyDocuments()),
        this.tr("customer.app.receipts.title"),
        this.tr("toast.loadingDetail"),
      );
      this.documents.set(items);
    } catch {
      this.failed.set(true);
      this.documents.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  kindLabel(doc: BillingDocumentResponse): string {
    return doc.kind === "invoice"
      ? this.tr("discover.document.invoice")
      : this.tr("discover.document.receipt");
  }

  when(iso: string | null | undefined): string {
    if (!iso) {
      return "-";
    }
    return new Intl.DateTimeFormat(this.locale(), {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(iso));
  }

  totalLabel(doc: BillingDocumentResponse): string {
    return formatPrice(doc.total, doc.currency, this.locale());
  }

  documentPath(doc: BillingDocumentResponse): string {
    return doc.publicUrl.startsWith("/") ? doc.publicUrl : `/documents/${doc.publicToken}`;
  }
}
