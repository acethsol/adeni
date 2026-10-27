import { Component, computed, DestroyRef, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
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
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
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
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly operationsRealtime = inject(TenantOperationsRealtimeService);
  private opsSub: Subscription | null = null;
  private readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly route = inject(ActivatedRoute);
  readonly tab = toSignal(this.route.queryParamMap.pipe(map((params) => params.get("tab") ?? "links")), {
    initialValue: this.route.snapshot.queryParamMap.get("tab") ?? "links",
  });
  readonly tabs = computed(() => [
    { id: "links", label: this.tr("portal.payments.links") },
    { id: "ledger", label: this.tr("portal.payments.ledger") },
    { id: "documents", label: this.tr("portal.payments.documents") },
    { id: "export", label: this.tr("portal.payments.exports") },
  ]);

  exportFrom = "";
  exportTo = "";
  compensationExporting = false;
  salesSummaryExporting = false;

  readonly planEntitlements = signal<TenantEntitlements | null>(null);
  readonly salesSummaryExportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.planEntitlements(),
      xlsx: false,
      pdf: salesSummaryPdfMenuOption(this.planEntitlements()),
      labels: { csv: this.tr("portal.payments.exportSalesSummaryCsv"), pdf: this.tr("portal.payments.exportSalesSummaryPdf") },
    }),
  );

  readonly compensationExportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.planEntitlements(),
      xlsx: true,
      csvRequiresAdvancedReports: true,
      labels: { csv: this.tr("portal.payments.exportStaffPayoutsCsv"), xlsx: this.tr("portal.payments.exportStaffPayoutsExcel") },
    }),
  );

  readonly loading = signal(true);
  readonly creating = signal(false);
  readonly error = signal<string | null>(null);
  readonly profile = signal<BusinessProfile | null>(null);
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
    this.activeLocation.selectionChanged$
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => {
        void this.activeLocation.runOnBranchSwitch(() => this.load());
      });
    this.opsSub = this.operationsRealtime.paymentCompleted$.subscribe(() => {
      this.feedback.info(this.tr("portal.payments.paymentCompletedRefreshingLedger"), this.tr("portal.payments.payment"));
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
      await this.feedback.runLoading(async () => {
      await this.api.withAuthorizedClient(async (c) => {
        const profile = await c.getTenantProfile();
        const entitlements = await c.getTenantEntitlements().catch(() => null);
        const mergedEntitlements = mergeTenantEntitlements(
          entitlements,
          profile.entitlements,
          profile.entitlements,
        );
        const locationQueryId = this.activeLocation.tenantApiLocationId(mergedEntitlements);
        const usage = await c.getTenantSubscriptionUsage(locationQueryId).catch(() => null);
        this.profile.set(profile);
        this.planEntitlements.set(
          mergeTenantEntitlements(entitlements, usage?.entitlements, profile.entitlements),
        );
        const ledgerPage = await c.listPaymentLedger(
          profile.tenantId,
          this.ledgerPage(),
          this.ledgerPageSize(),
          locationQueryId,
        );
        this.ledger.set(ledgerPage.items);
        this.ledgerTotalCount.set(ledgerPage.totalCount);
        this.ledgerPage.set(ledgerPage.page);
        const docs = await c.listBillingDocuments();
        this.documents.set(docs);
      });
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.payments.couldNotLoadPayments"));
    } finally {
      this.loading.set(false);
    }
  }

  async createLink(): Promise<void> {
    const profile = this.profile();
    const parsed = Number(this.amount);
    if (!profile || !Number.isFinite(parsed) || parsed <= 0 || !this.description.trim()) {
      this.error.set(this.tr("portal.payments.enterValidAmountDescription"));
      return;
    }

    const confirmed = await this.confirmDialog.confirmKind("create", {
      subject: this.tr("common.confirm.subjects.paymentLink"),
    });
    if (!confirmed) {
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.payments.couldNotCreatePaymentLink"));
    } finally {
      this.creating.set(false);
    }
  }

  whatsAppUrl(link: { checkoutUrl: string; description: string }): string {
    const text = encodeURIComponent(this.tr("portal.payments.payViaAdeni", { description: link.description, checkoutUrl: link.checkoutUrl }));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.payments.couldNotEmailDocumentAdd"));
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.payments.couldNotDownloadDocumentPdf"));
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
      this.error.set(this.tr("portal.payments.chooseDateRangeExport"));
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
          locationId: this.activeLocation.reportExportLocationId(this.planEntitlements()),
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `sales-summary-${this.exportFrom}-${this.exportTo}.${format === "pdf" ? "pdf" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      this.error.set(
        this.apiErr(err, "portal.payments.couldNotDownloadSalesSummary"),
      );
    } finally {
      this.salesSummaryExporting = false;
    }
  }

  async downloadCompensationExport(format: ReportExportFormat): Promise<void> {
    if (format === "pdf") {
      return;
    }
    if (!this.profile() || !this.exportFrom || !this.exportTo) {
      this.error.set(this.tr("portal.payments.chooseDateRangeExport"));
      return;
    }

    this.compensationExporting = true;
    this.error.set(null);
    try {
      const from = new Date(`${this.exportFrom}T00:00:00.000Z`).toISOString();
      const to = new Date(`${this.exportTo}T23:59:59.999Z`).toISOString();
      const blob = await this.api.withAuthorizedClient((c) =>
        c.downloadTenantReportExport({
          reportKey: REPORT_KEYS.compensation,
          from,
          to,
          format,
          locationId: this.activeLocation.reportExportLocationId(this.planEntitlements()),
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `compensation-${this.exportFrom}-${this.exportTo}.${format === "xlsx" ? "xlsx" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (err) {
      this.error.set(
        this.apiErr(err, "portal.payments.couldNotDownloadCompensation"),
      );
    } finally {
      this.compensationExporting = false;
    }
  }

  async refund(entry: PaymentLedgerEntry): Promise<void> {
    const profile = this.profile();
    if (!profile || entry.status !== "completed") return;
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.payments.refundPayment"),
      message: this.tr("portal.payments.refundConfirm", { amount: formatPrice(entry.amount, entry.currency) }),
      confirmLabel: this.tr("portal.payments.refund"),
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
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.payments.refundFailed"));
    }
  }
}
