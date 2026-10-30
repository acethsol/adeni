import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import {
  formatPrice,
  hasCrossLocationRetentionEntitlement,
  mergeTenantEntitlements,
  type BusinessLocation,
  type CustomerPackage,
  type PackageDefinition,
  type PackageRedemptionScope,
  type ServiceOffering,
  type TenantCustomerListItem,
  type TenantEntitlements,
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

type CreateDraft = {
  name: string;
  listPrice: number | null;
  currency: string;
  serviceOfferingId: string;
  quantityGranted: number;
  redemptionScope: PackageRedemptionScope;
  redemptionLocationIds: string[];
};

const REDEMPTION_SCOPES: readonly PackageRedemptionScope[] = [
  "purchase_location_only",
  "all_locations",
  "selected_locations",
];

type SellDraft = {
  packageDefinitionId: string;
  businessLocationId: string;
  paymentMethod: "till" | "transfer";
  note: string;
};

function newKey(): string {
  return globalThis.crypto?.randomUUID?.() ?? `${Date.now()}-${Math.random().toString(16).slice(2)}`;
}

/** Retention MVP: package catalog list + in-person sell-to-customer (no online checkout; no wallet). */
@Component({
  selector: "app-retention-packages",
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
  templateUrl: "./retention-packages.component.html",
  styleUrl: "./retention-packages.component.scss",
})
export class RetentionPackagesComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly currencies = CURRENCIES;
  readonly redemptionScopes = REDEMPTION_SCOPES;
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly loading = signal(true);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly packages = signal<PackageDefinition[]>([]);
  readonly services = signal<ServiceOffering[]>([]);
  readonly locations = signal<BusinessLocation[]>([]);
  readonly search = signal("");
  readonly viewMode = signal<AdeniListViewMode>("table");

  readonly showCreate = signal(false);
  readonly createDraft = signal<CreateDraft>(this.emptyCreate());

  readonly sellTarget = signal<PackageDefinition | null>(null);
  readonly sellDraft = signal<SellDraft | null>(null);
  readonly customerQuery = signal("");
  readonly customerResults = signal<TenantCustomerListItem[]>([]);
  readonly selectedCustomer = signal<TenantCustomerListItem | null>(null);
  readonly lastSold = signal<CustomerPackage | null>(null);
  /** Reused for retries of the same sale; regenerated when the form is opened or after success. */
  private sellKey = newKey();
  private customerSearchTimer: ReturnType<typeof setTimeout> | null = null;

  readonly filtered = computed(() => {
    const term = this.search().trim().toLowerCase();
    return term ? this.packages().filter((p) => p.name.toLowerCase().includes(term)) : this.packages();
  });

  readonly columns = computed<AdeniDataTableColumn<PackageDefinition>[]>(() => [
    { id: "name", header: this.tr("businessApp.retention.name"), cell: (row) => row.name, sortValue: (row) => row.name.toLowerCase() },
    {
      id: "price",
      header: this.tr("businessApp.retention.listPrice"),
      align: "right",
      cell: (row) => formatPrice(row.listPrice, row.currency),
      sortValue: (row) => row.listPrice,
    },
    { id: "credits", header: this.tr("businessApp.retention.credits"), cell: (row) => this.creditsSummary(row), sortable: false },
    {
      id: "scope",
      header: this.tr("businessApp.retention.redemptionScope"),
      cell: (row) => this.scopeLabel(row),
      sortValue: (row) => row.redemptionScope,
    },
    {
      id: "expiry",
      header: this.tr("businessApp.retention.expiry"),
      cell: (row) => this.tr("businessApp.retention.months", { count: row.effectiveExpiryMonths }),
      sortValue: (row) => row.effectiveExpiryMonths,
    },
    { id: "status", header: this.tr("businessApp.retention.status"), cell: (row) => this.statusLabel(row), sortValue: (row) => (row.isActive ? 1 : 0) },
  ]);

  readonly trackRow = (row: PackageDefinition): string => row.id;

  ngOnInit(): void {
    void this.load();
  }

  creditsSummary(row: PackageDefinition): string {
    return row.benefits.map((b) => `${b.quantityGranted}× ${b.serviceName}`).join(", ");
  }

  statusLabel(row: PackageDefinition): string {
    return row.isActive ? this.tr("businessApp.retention.active") : this.tr("businessApp.retention.inactive");
  }

  scopeLabel(row: PackageDefinition): string {
    return this.redemptionScopeLabel(row.redemptionScope);
  }

  redemptionScopeLabel(scope: PackageRedemptionScope): string {
    switch (scope) {
      case "purchase_location_only":
        return this.tr("businessApp.retention.scopePurchase");
      case "selected_locations":
        return this.tr("businessApp.retention.scopeSelected");
      default:
        return this.tr("businessApp.retention.scopeAll");
    }
  }

  showCrossLocationPlanHint(scope: PackageRedemptionScope): boolean {
    return scope === "all_locations" && !hasCrossLocationRetentionEntitlement(this.entitlements());
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (client) => {
        const [list, services, locations, planEntitlements, profile] = await Promise.all([
          client.retention.listPackages(false),
          client.getTenantServices().catch(() => [] as ServiceOffering[]),
          client.getTenantLocations().catch(() => [] as BusinessLocation[]),
          client.getTenantEntitlements().catch(() => null),
          client.getTenantProfile().catch(() => null),
        ]);
        this.entitlements.set(
          mergeTenantEntitlements(planEntitlements, null, profile?.entitlements ?? null),
        );
        this.packages.set(list.items);
        this.services.set(services.filter((s) => s.isActive));
        this.locations.set(locations.filter((l) => l.isActive !== false));
      });
    } catch (caught) {
      this.error.set(this.apiErr(caught, "businessApp.retention.couldNotLoad"));
      this.packages.set([]);
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Create ----

  private emptyCreate(): CreateDraft {
    return {
      name: "",
      listPrice: null,
      currency: "NGN",
      serviceOfferingId: "",
      quantityGranted: 3,
      redemptionScope: "purchase_location_only",
      redemptionLocationIds: [],
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
    this.createDraft.update((d) => {
      const next = { ...d, ...patch };
      if (patch.redemptionScope && patch.redemptionScope !== "selected_locations") {
        next.redemptionLocationIds = [];
      }
      return next;
    });
  }

  toggleRedemptionLocation(locationId: string): void {
    this.createDraft.update((d) => {
      const set = new Set(d.redemptionLocationIds);
      if (set.has(locationId)) {
        set.delete(locationId);
      } else {
        set.add(locationId);
      }
      return { ...d, redemptionLocationIds: [...set] };
    });
  }

  isRedemptionLocationSelected(locationId: string): boolean {
    return this.createDraft().redemptionLocationIds.includes(locationId);
  }

  canCreate(): boolean {
    const d = this.createDraft();
    return (
      d.name.trim().length > 0
      && d.listPrice !== null
      && d.listPrice >= 0
      && !!d.serviceOfferingId
      && d.quantityGranted >= 1
      && (d.redemptionScope !== "selected_locations" || d.redemptionLocationIds.length > 0)
    );
  }

  async saveCreate(): Promise<void> {
    if (!this.canCreate() || this.busy()) return;
    const d = this.createDraft();
    this.busy.set(true);
    try {
      const created = await this.api.withAuthorizedClient((client) =>
        client.retention.createPackage({
          name: d.name.trim(),
          listPrice: d.listPrice ?? 0,
          currency: d.currency,
          benefits: [{ serviceOfferingId: d.serviceOfferingId, quantityGranted: d.quantityGranted }],
          redemptionScope: d.redemptionScope,
          ...(d.redemptionScope === "selected_locations"
            ? { redemptionLocationIds: d.redemptionLocationIds }
            : {}),
        }),
      );
      this.packages.update((list) => [created, ...list]);
      this.showCreate.set(false);
      this.feedback.success(this.tr("businessApp.retention.created"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.retention.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }

  async deactivate(row: PackageDefinition): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: this.tr("businessApp.retention.deactivateTitle"),
      message: this.tr("businessApp.retention.deactivateMessage", { name: row.name }),
      confirmLabel: this.tr("businessApp.retention.deactivate"),
      danger: true,
    });
    if (!ok) return;
    this.busy.set(true);
    try {
      const updated = await this.api.withAuthorizedClient((client) => client.retention.deactivatePackage(row.id));
      this.packages.update((list) => list.map((p) => (p.id === updated.id ? updated : p)));
      this.feedback.success(this.tr("businessApp.retention.deactivated"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "businessApp.retention.couldNotSave"));
    } finally {
      this.busy.set(false);
    }
  }

  // ---- Sell ----

  openSell(row: PackageDefinition): void {
    this.sellKey = newKey();
    this.selectedCustomer.set(null);
    this.customerQuery.set("");
    this.customerResults.set([]);
    this.lastSold.set(null);
    this.sellTarget.set(row);
    this.sellDraft.set({
      packageDefinitionId: row.id,
      businessLocationId: this.locations().find((l) => l.isPrimary)?.id ?? this.locations()[0]?.id ?? "",
      paymentMethod: "till",
      note: "",
    });
  }

  closeSell(): void {
    this.sellTarget.set(null);
    this.sellDraft.set(null);
    if (this.customerSearchTimer) clearTimeout(this.customerSearchTimer);
  }

  patchSell(patch: Partial<SellDraft>): void {
    this.sellDraft.update((d) => (d ? { ...d, ...patch } : d));
  }

  onCustomerQuery(value: string): void {
    this.customerQuery.set(value);
    this.selectedCustomer.set(null);
    if (this.customerSearchTimer) clearTimeout(this.customerSearchTimer);
    const term = value.trim();
    if (term.length < 2) {
      this.customerResults.set([]);
      return;
    }
    this.customerSearchTimer = setTimeout(() => void this.searchCustomers(term), 300);
  }

  private async searchCustomers(term: string): Promise<void> {
    try {
      const result = await this.api.withAuthorizedClient((client) =>
        client.listTenantCustomers({ search: term, page: 1, pageSize: 8 }),
      );
      this.customerResults.set(result.items);
    } catch {
      this.customerResults.set([]);
    }
  }

  pickCustomer(customer: TenantCustomerListItem): void {
    this.selectedCustomer.set(customer);
    this.customerQuery.set(customer.displayName);
    this.customerResults.set([]);
  }

  canSell(): boolean {
    const d = this.sellDraft();
    return !!d && !!this.selectedCustomer() && !!d.businessLocationId && !this.busy();
  }

  async confirmSell(): Promise<void> {
    const draft = this.sellDraft();
    const customer = this.selectedCustomer();
    if (!draft || !customer || !this.canSell()) return;
    this.busy.set(true);
    try {
      const sold = await this.api.withAuthorizedClient((client) =>
        client.retention.sellPackage(
          customer.id,
          {
            packageDefinitionId: draft.packageDefinitionId,
            businessLocationId: draft.businessLocationId,
            paymentMethod: draft.paymentMethod,
            note: draft.note.trim() || undefined,
          },
          this.sellKey,
        ),
      );
      this.lastSold.set(sold);
      this.sellKey = newKey();
      this.feedback.success(this.tr("businessApp.retention.sold", { name: sold.name, customer: customer.displayName }));
      this.closeSell();
    } catch (caught) {
      // Keep the same Idempotency-Key so a retry cannot double-sell.
      this.feedback.error(this.apiErr(caught, "businessApp.retention.couldNotSell"));
    } finally {
      this.busy.set(false);
    }
  }
}
