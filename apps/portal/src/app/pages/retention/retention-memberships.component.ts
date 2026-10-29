import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  formatPrice,
  type MembershipBillingInterval,
  type MembershipPlan,
  type ServiceOffering,
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
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

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
  readonly search = signal("");
  readonly viewMode = signal<AdeniListViewMode>("table");

  readonly showCreate = signal(false);
  readonly createDraft = signal<CreateDraft>(this.emptyCreate());

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return term ? this.plans().filter((p) => p.name.toLowerCase().includes(term)) : this.plans();
  });

  readonly columns = computed<AdeniDataTableColumn<MembershipPlan>[]>(() => [
    { id: "name", header: this.tr("portal.memberships.name"), cell: (row) => row.name, sortValue: (row) => row.name.toLowerCase() },
    {
      id: "price",
      header: this.tr("portal.memberships.price"),
      align: "right",
      cell: (row) => formatPrice(row.price, row.currency),
      sortValue: (row) => row.price,
    },
    { id: "interval", header: this.tr("portal.memberships.interval"), cell: (row) => this.intervalLabel(row.billingInterval), sortValue: (row) => row.billingInterval },
    { id: "credits", header: this.tr("portal.memberships.credits"), cell: (row) => this.creditsSummary(row), sortable: false },
    { id: "status", header: this.tr("portal.memberships.status"), cell: (row) => this.statusLabel(row), sortValue: (row) => (row.isActive ? 1 : 0) },
  ]);

  readonly trackRow = (row: MembershipPlan): string => row.id;

  ngOnInit(): void {
    void this.load();
  }

  intervalLabel(interval: MembershipBillingInterval): string {
    switch (interval) {
      case "quarterly":
        return this.tr("portal.memberships.intervalQuarterly");
      case "annually":
        return this.tr("portal.memberships.intervalAnnually");
      default:
        return this.tr("portal.memberships.intervalMonthly");
    }
  }

  creditsSummary(row: MembershipPlan): string {
    return row.benefits.map((b) => `${b.quantityPerCycle}× ${b.serviceName}`).join(", ");
  }

  statusLabel(row: MembershipPlan): string {
    return row.isActive ? this.tr("portal.memberships.active") : this.tr("portal.memberships.inactive");
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [list, services] = await Promise.all([
          client.retention.listMembershipPlans(false),
          client.getTenantServices().catch(() => [] as ServiceOffering[]),
        ]);
        this.plans.set(list.items);
        this.services.set(services.filter((s) => s.isActive));
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.memberships.couldNotLoad"));
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
      this.feedback.success(this.tr("portal.memberships.created"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "portal.memberships.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(row: MembershipPlan): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.memberships.deactivateTitle"),
      message: this.tr("portal.memberships.deactivateMessage", { name: row.name }),
      confirmLabel: this.tr("portal.memberships.deactivate"),
      danger: true,
    });
    if (!ok) return;
    this.busy.set(true);
    try {
      const updated = await this.api.withAuthorizedClient((client) => client.retention.deactivateMembershipPlan(row.id));
      this.plans.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      this.feedback.success(this.tr("portal.memberships.deactivated"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "portal.memberships.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }
}
