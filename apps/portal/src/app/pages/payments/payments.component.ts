import { Component, computed, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { Subscription } from "rxjs";
import { toSignal } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { ActivatedRoute } from "@angular/router";
import { map } from "rxjs";
import type {
  BillingDocumentResponse,
  BusinessProfile,
  PaymentLedgerEntry,
  TenantEntitlements,
} from "@adeni/shared";
import {
  DEFAULT_PAGE_SIZE,
  formatPrice,
  hasBasicExportsEntitlement,
  hasCapability,
  LIST_PAGE_SIZE_OPTIONS,
  REPORT_KEYS,
  buildReportExportMenuItems,
  resolvePaymentCheckoutUrl,
  mergeTenantEntitlements,
  salesSummaryPdfMenuOption,
  type ReportExportFormat,
} from "@adeni/shared";
import {
  AdeniConfirmService,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniListPaginationComponent,
  AdeniPortalEmptyStateComponent,
  PortalPageComponent,
  AdeniTabsComponent,
  TenantOperationsRealtimeService,
  type AdeniExportMenuItem,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { ADENI_PORTAL_CONFIG } from "../../core/adeni-config";

@Component({
  selector: "app-payments",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniTabsComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListPaginationComponent,
    AdeniExportMenuComponent,
  ],
  templateUrl: "./payments.component.html",
  styleUrl: "./payments.component.scss",
})
export class PaymentsComponent implements OnInit, OnDestroy {
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private opsSub: Subscription | null = null;
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "links")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "links",
  });
  readonly tabs = [
    { id: "links", label: "Links" },
    { id: "ledger", label: "Ledger" },
    { id: "documents", label: "Documents" },
    { id: "export", label: "Exports" },
  ];

  exportFrom = "";
  exportTo = "";
  exportFormat: "csv" | "json" = "csv";
  exporting = false;
  salesSummaryExporting = false;

  readonly planEntitlements = signal<TenantEntitlements | null>(null);
  readonly salesSummaryExportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.planEntitlements(),
      xlsx: false,
      pdf: salesSummaryPdfMenuOption(this.planEntitlements()),
      labels: { csv: "Sales summary CSV", pdf: "Sales summary PDF" },
    }),
  );

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
  readonly exportEntitled = signal(true);
  readonly ledger = signal<PaymentLedgerEntry[]>([]);
  readonly ledgerTotalCount = signal(0);
  readonly ledgerPage = signal(1);
  readonly ledgerPageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly documents = signal<BillingDocumentResponse[]>([]);
  readonly lastLink = signal<{ checkoutUrl: string; description: string } | null>(null);
  readonly actionId = signal<string | null>(null);
  readonly pdfBusyId = signal<string | null>(null);

  amount = "";
  description = "";
  readonly formatPrice = formatPrice;

  ngOnInit(): void {
    const today = new Date();
    const monthAgo = new Date(today);
    monthAgo.setDate(today.getDate() - 30);
    this.exportTo = today.toISOString().slice(0, 10);
    this.exportFrom = monthAgo.toISOString().slice(0, 10);
    void this.load();
    this.opsSub = this.operationsRealtime.paymentCompleted$.subscribe(() => {
      this.feedback.info("Payment completed — refreshing ledger.", "Payment");
      void this.load();
    });
  }

  ngOnDestroy(): void {
    this.opsSub?.unsubscribe();
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
        const [profile, usage, entitlements] = await Promise.all([
          c.getTenantProfile(),
          c.getTenantSubscriptionUsage().catch(() => null),
          c.getTenantEntitlements().catch(() => null),
        ]);
        this.profile.set(profile);
        const mergedEntitlements = mergeTenantEntitlements(
          entitlements,
          usage?.entitlements,
          profile.entitlements,
        );
        this.planEntitlements.set(mergedEntitlements);
        this.exportEntitled.set(hasBasicExportsEntitlement(mergedEntitlements));
        const ledgerPage = await c.listPaymentLedger(
          profile.tenantId,
          this.ledgerPage(),
          this.ledgerPageSize(),
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

  async downloadDocumentPdf(doc: BillingDocumentResponse): Promise<void> {
    this.pdfBusyId.set(doc.id);
    this.error.set(null);
    try {
      const blob = await this.api.withAuthorizedClient((c) => c.downloadBillingDocumentPdf(doc.id));
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `${doc.documentNumber || doc.kind}.pdf`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set("Could not download this document PDF.");
    } finally {
      this.pdfBusyId.set(null);
    }
  }

  goToLedgerPage(next: number): void {
    if (next === this.ledgerPage()) {
      return;
    }
    this.ledgerPage.set(next);
    void this.load();
  }

  onLedgerPageSizeChange(size: number): void {
    this.ledgerPageSize.set(size);
    this.ledgerPage.set(1);
    void this.load();
  }

  async downloadSalesSummaryExport(format: ReportExportFormat): Promise<void> {
    if (!this.exportFrom || !this.exportTo) {
      this.error.set("Choose a date range for the export.");
      return;
    }

    this.salesSummaryExporting = true;
    this.error.set(null);
    try {
      const from = new Date(`${this.exportFrom}T00:00:00.000Z`).toISOString();
      const to = new Date(`${this.exportTo}T23:59:59.999Z`).toISOString();
      const blob = await this.api.withAuthorizedClient((c) =>
        c.downloadTenantReportExport({
          reportKey: REPORT_KEYS.salesSummary,
          from,
          to,
          format,
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `sales-summary-${this.exportFrom}-${this.exportTo}.${format === "pdf" ? "pdf" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set("Could not download sales summary export.");
    } finally {
      this.salesSummaryExporting = false;
    }
  }

  async downloadCompensationExport(): Promise<void> {
    const profile = this.profile();
    if (!profile || !this.exportFrom || !this.exportTo) {
      this.error.set("Choose a date range for the export.");
      return;
    }

    this.exporting = true;
    this.error.set(null);
    try {
      const from = new Date(`${this.exportFrom}T00:00:00.000Z`).toISOString();
      const to = new Date(`${this.exportTo}T23:59:59.999Z`).toISOString();
      const blob = await this.api.withAuthorizedClient((c) =>
        c.downloadTenantReportExport({
          reportKey: "compensation",
          from,
          to,
          format: this.exportFormat === "json" ? "csv" : this.exportFormat,
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `compensation-${this.exportFrom}-${this.exportTo}.${this.exportFormat}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch {
      this.error.set("Could not download compensation export.");
    } finally {
      this.exporting = false;
    }
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
