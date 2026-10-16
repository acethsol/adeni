import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute } from "@angular/router";
import { map } from "rxjs";
import type { BillingDocumentResponse, BusinessProfile, PaymentLedgerEntry } from "@adeni/shared";
import {
  formatPrice,
  hasCapability,
  pageRangeLabel,
  resolvePaymentCheckoutUrl,
  totalPages as computeTotalPages,
} from "@adeni/shared";
import { AdeniConfirmService, AdeniPortalEmptyStateComponent, PortalPageComponent } from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";
import { PortalTabsComponent } from "../../shared/portal-tabs.component";

@Component({
  selector: "app-payments",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, PortalTabsComponent, AdeniPortalEmptyStateComponent],
  templateUrl: "./payments.component.html",
  styleUrl: "./payments.component.scss",
})
export class PaymentsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "links")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "links",
  });
  readonly tabs = [
    { id: "links", label: "Links" },
    { id: "ledger", label: "Ledger" },
    { id: "documents", label: "Documents" },
  ];

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly ledger = signal<PaymentLedgerEntry[]>([]);
  readonly ledgerTotalCount = signal(0);
  readonly ledgerPage = signal(1);
  readonly ledgerPageSize = 25;
  readonly ledgerTotalPageCount = computed(() =>
    computeTotalPages(this.ledgerTotalCount(), this.ledgerPageSize),
  );
  readonly ledgerPageLabel = computed(() =>
    pageRangeLabel(this.ledgerPage(), this.ledgerPageSize, this.ledgerTotalCount()),
  );
  readonly documents = signal<BillingDocumentResponse[]>([]);
  readonly lastLink = signal<{ checkoutUrl: string; description: string } | null>(null);
  readonly actionId = signal<string | null>(null);

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
        const ledgerPage = await c.listPaymentLedger(
          profile.tenantId,
          this.ledgerPage(),
          this.ledgerPageSize,
        );
        this.ledger.set(ledgerPage.items);
        this.ledgerTotalCount.set(ledgerPage.totalCount);
        this.ledgerPage.set(ledgerPage.page);
        const docs = await c.listBillingDocuments();
        this.documents.set(docs);
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
      const checkoutUrl = resolvePaymentCheckoutUrl(intent.checkoutUrl, this.config.discoverWebUrl, {
        amount: intent.amount,
        currency: intent.currency,
      });
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

  documentPublicUrl(doc: BillingDocumentResponse): string {
    const path = doc.publicUrl.startsWith("/") ? doc.publicUrl : `/${doc.publicUrl}`;
    return `${this.config.discoverWebUrl.replace(/\/$/, "")}${path}`;
  }

  async emailDocument(doc: BillingDocumentResponse): Promise<void> {
    this.actionId.set(doc.id);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient((c) => c.emailBillingDocument(doc.id));
    } catch {
      this.error.set("Could not email this document. Add a customer email and try again.");
    } finally {
      this.actionId.set(null);
    }
  }

  canNextLedgerPage(): boolean {
    return this.ledgerPage() < this.ledgerTotalPageCount();
  }

  prevLedgerPage(): void {
    if (this.ledgerPage() <= 1) {
      return;
    }
    this.ledgerPage.update((p) => p - 1);
    void this.load();
  }

  nextLedgerPage(): void {
    if (!this.canNextLedgerPage()) {
      return;
    }
    this.ledgerPage.update((p) => p + 1);
    void this.load();
  }

  async refund(entry: PaymentLedgerEntry): Promise<void> {
    const profile = this.profile();
    if (!profile || entry.status !== "completed") return;
    const ok = await this.confirmDialog.confirm({
      title: "Refund payment?",
      message: `Refund ${formatPrice(entry.amount, entry.currency)}? This cannot be undone from Adeni.`,
      confirmLabel: "Refund",
      danger: true,
    });
    if (!ok) {
      return;
    }

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
