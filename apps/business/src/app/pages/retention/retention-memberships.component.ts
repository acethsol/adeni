import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  formatPrice,
  hasAdvancedRetentionAnalyticsEntitlement,
  mergeTenantEntitlements,
  REPORT_KEYS,
  type MembershipBillingInterval,
  type MembershipPlan,
  type ServiceOffering,
  type TenantEntitlements,
  type TenantMembershipMrr,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  PortalPageComponent,
  type AdeniDataTableColumn,
  type AdeniListViewMode,
  injectAdeniApiError,
  injectAdeniTranslate,
  readPersistedListViewMode,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { withRetentionPageLoading } from "./retention-page-loading";

const CURRENCIES = ["NGN", "CAD", "USD"] as const;
const INTERVALS: readonly MembershipBillingInterval[] = ["monthly", "quarterly", "annually"];

type CreateDraft = {
  name: string;
  price: number | null;
  currency: string;
  billingInterval: MembershipBillingInterval;
  serviceOfferingId: string;
  quantityPerCycle: number;
  rolloverPolicy: "none" | "carry_over_one_cycle";
  cancellationPolicy: "end_of_cycle" | "immediate";
  allowFreeze: boolean;
  pastDueGraceDays: number;
};

/**
 * Sprint 37.2-A shell: membership plan catalog (list / create / retire). Enrolling customers and recurring
 * billing ship in later slices — this page only defines plans. Credits are service sessions, never money.
 */
@Component({
  selector: "app-retention-memberships",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniModalComponent,
    AdeniListToolbarComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniPillComponent,
  ],
  templateUrl: "./retention-memberships.component.html",
  styleUrl: "./retention-memberships.component.scss",
})
export class RetentionMembershipsComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly formatPrice = formatPrice;
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly currencies = CURRENCIES;
  readonly intervals = INTERVALS;
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly plans = signal<MembershipPlan[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly mrrItems = signal<TenantMembershipMrr[]>([]);
  readonly mrrLoading = signal(false);
  readonly mrrExporting = signal(false);
  readonly mrrUpgradeHint = computed(
    () => !hasAdvancedRetentionAnalyticsEntitlement(this.entitlements()) && !this.loading(),
  );
  readonly showMrrPanel = computed(
    () => hasAdvancedRetentionAnalyticsEntitlement(this.entitlements()) && !this.loading(),
  );
  readonly mrrRows = computed(() =>
    this.mrrItems().map((row) => ({
      currency: row.currency,
      memberCount: row.memberCount,
      amountLabel: formatPrice(row.monthlyRecurringRevenue, row.currency),
    })),
  );
  readonly search = signal("");
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("businessApp.retention.memberships", "cards"),
  );

  readonly showCreate = signal(false);
  readonly createDraft = signal<CreateDraft>(this.emptyCreate());

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return term ? this.plans().filter((p) => p.name.toLowerCase().includes(term)) : this.plans();
  });

  readonly columns = computed<AdeniDataTableColumn<MembershipPlan>[]>(() => [
    { id: "name", header: this.tr("businessApp.memberships.name"), cell: (row) => row.name, sortValue: (row) => row.name.toLowerCase() },
    {
      id: "price",
      header: this.tr("businessApp.memberships.price"),
      align: "right",
      cell: (row) => formatPrice(row.price, row.currency),
      sortValue: (row) => row.price,
    },
    { id: "interval", header: this.tr("businessApp.memberships.interval"), cell: (row) => this.intervalLabel(row.billingInterval), sortValue: (row) => row.billingInterval },
    { id: "credits", header: this.tr("businessApp.memberships.credits"), cell: (row) => this.creditsSummary(row), sortable: false },
    { id: "status", header: this.tr("businessApp.memberships.status"), cell: (row) => this.statusLabel(row), sortValue: (row) => (row.isActive ? 1 : 0) },
  ]);

  readonly trackRow = (row: MembershipPlan): string => row.id;

  ngOnInit(): void {
    void this.load();
  }

  intervalLabel(interval: MembershipBillingInterval): string {
    switch (interval) {
      case "quarterly":
        return this.tr("businessApp.memberships.intervalQuarterly");
      case "annually":
        return this.tr("businessApp.memberships.intervalAnnually");
      default:
        return this.tr("businessApp.memberships.intervalMonthly");
    }
  }

  creditsSummary(row: MembershipPlan): string {
    return row.benefits.map((b) => `${b.quantityPerCycle}× ${b.serviceName}`).join(", ");
  }

  protected catalogInitials(name: string): string {
    const parts = name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0].charAt(0) + parts[1].charAt(0)).toUpperCase();
    }
    return name.trim().slice(0, 2).toUpperCase() || "MB";
  }

  statusLabel(row: MembershipPlan): string {
    return row.isActive ? this.tr("businessApp.memberships.active") : this.tr("businessApp.memberships.inactive");
  }

  async downloadMrrCsv(): Promise<void> {
    if (this.mrrExporting() || !this.showMrrPanel()) {
      return;
    }
    this.mrrExporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setUTCDate(from.getUTCDate() - 1);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.retentionMembershipMrr,
          from: from.toISOString(),
          to: to.toISOString(),
          format: "csv",
        }),
      );
      const stamp = to.toISOString().slice(0, 10);
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `membership-mrr-${stamp}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.memberships.couldNotDownloadMrrExport"));
    } finally {
      this.mrrExporting.set(false);
    }
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await withRetentionPageLoading(
        this.feedback,
        this.tr("toast.loading"),
        this.tr("businessApp.retention.loadingSection"),
        () =>
          this.api.withAuthorizedClient(async (client) => {
            const [list, services, planEntitlements, profile] = await Promise.all([
              client.retention.listMembershipPlans(false),
              client.getTenantServices().catch(() => [] as ServiceOffering[]),
              client.getTenantEntitlements().catch(() => null),
              client.getTenantProfile().catch(() => null),
            ]);
            const merged = mergeTenantEntitlements(planEntitlements, null, profile?.entitlements ?? null);
            this.entitlements.set(merged);
            this.plans.set(list.items);
            this.services.set(services.filter((s) => s.isActive));
            if (hasAdvancedRetentionAnalyticsEntitlement(merged)) {
              this.mrrLoading.set(true);
              try {
                const mrr = await client.retention.membershipMrr();
                this.mrrItems.set(mrr.items);
              } catch {
                this.mrrItems.set([]);
              } finally {
                this.mrrLoading.set(false);
              }
            } else {
              this.mrrItems.set([]);
            }
          }),
      );
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.memberships.couldNotLoad"));
      this.plans.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Create ----

  private emptyCreate(): CreateDraft {
    return {
      name: "",
      price: null,
      currency: "NGN",
      billingInterval: "monthly",
      serviceOfferingId: "",
      quantityPerCycle: 2,
      rolloverPolicy: "none",
      cancellationPolicy: "end_of_cycle",
      allowFreeze: false,
      pastDueGraceDays: 7,
    };
  }

  openCreate(): void {
    const draft = this.emptyCreate();
    const first = this.services()[0];
    if (first) {
      draft.serviceOfferingId = first.id;
      draft.currency = CURRENCIES.includes(first.currency as (typeof CURRENCIES)[number]) ? first.currency : draft.currency;
    }
    this.createDraft.set(draft);
    this.showCreate.set(true);
  }

  patchCreate(patch: Partial<CreateDraft>): void {
    this.createDraft.update((d) => ({ ...d, ...patch }));
  }

  canCreate(): boolean {
    const d = this.createDraft();
    return (
      d.name.trim().length > 0 &&
      d.price !== null &&
      d.price > 0 &&
      !!d.serviceOfferingId &&
      d.quantityPerCycle >= 1 &&
      d.pastDueGraceDays >= 0 &&
      d.pastDueGraceDays <= 30
    );
  }

  async saveCreate(): Promise<void> {
    if (!this.canCreate() || this.busy()) return;
    const d = this.createDraft();
    if (!(await this.confirmDialog.confirmKind("create", { subject: d.name.trim() }))) {
      return;
    }
    this.busy.set(true);
    try {
      const created = await this.api.withAuthorizedClient((client) =>
        client.retention.createMembershipPlan({
          name: d.name.trim(),
          price: d.price ?? 0,
          currency: d.currency,
          billingInterval: d.billingInterval,
          rolloverPolicy: d.rolloverPolicy,
          cancellationPolicy: d.cancellationPolicy,
          allowFreeze: d.allowFreeze,
          pastDueGraceDays: d.pastDueGraceDays,
          benefits: [{ serviceOfferingId: d.serviceOfferingId, quantityPerCycle: d.quantityPerCycle }],
        }),
      );
      this.plans.update((list) => [created, ...list]);
      this.showCreate.set(false);
      this.feedback.success(this.tr("businessApp.memberships.created"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.memberships.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(row: MembershipPlan): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.tr("businessApp.memberships.deactivateTitle"),
      message: this.tr("businessApp.memberships.deactivateMessage", { name: row.name }),
      confirmLabel: this.tr("businessApp.memberships.deactivate"),
      danger: true,
    });
    if (!ok) return;
    this.busy.set(true);
    try {
      const updated = await this.api.withAuthorizedClient((client) => client.retention.deactivateMembershipPlan(row.id));
      this.plans.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      this.feedback.success(this.tr("businessApp.memberships.deactivated"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.memberships.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }
}
