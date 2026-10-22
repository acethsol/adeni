import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import { AdeniApiError } from "@adeni/api-client";
import {
  buildReportExportMenuItems,
  canAddLocationAtCount,
  DEFAULT_PAGE_SIZE,
  LIST_PAGE_SIZE_OPTIONS,
  mergeTenantEntitlements,
  REPORT_KEYS,
  sliceListPage,
  type BusinessLocation,
  type MarketConfig,
  type ReportExportFormat,
  type TenantEntitlements,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListPaginationComponent,
  AdeniListToolbarComponent,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  readPersistedListViewMode,
  sortDataRows,
  type AdeniDataTableColumn,
  type AdeniDataTableSortDirection,
  type AdeniExportMenuItem,
  type AdeniListViewMode,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

/** Apply upsert response; when primary moves, clear stale primary flags on other rows. */
function mergeSavedLocation(
  list: BusinessLocation[],
  saved: BusinessLocation,
  editedId: string | null,
): BusinessLocation[] {
  const merged = editedId
    ? list.map((l) => (l.id === editedId ? saved : l))
    : [...list, saved];
  if (!saved.isPrimary) {
    return merged;
  }
  return merged.map((l) => (l.id === saved.id ? l : { ...l, isPrimary: false }));
}

type LocationDraft = {
  slug: string;
  name: string;
  addressLine: string;
  area: string;
  marketId: string;
  isPrimary: boolean;
};

@Component({
  selector: "app-locations",
  standalone: true,
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniModalComponent,
    AdeniListToolbarComponent,
    AdeniListPaginationComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniPillComponent,
    AdeniTablePersonCellComponent,
  ],
  templateUrl: "./locations.component.html",
  styleUrl: "./locations.component.scss",
})
export class LocationsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly feedback = inject(AdeniFeedbackService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly locations = signal<BusinessLocation[]>([]);
  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly markets = signal<MarketConfig[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly locationSearch = signal("");
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("portal.locations", "cards"),
  );

  readonly exporting = signal(false);

  readonly exportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.entitlements(),
      pdf: { disabled: true, hint: "Locations export is CSV only" },
    }),
  );

  readonly canAddLocation = computed(() =>
    canAddLocationAtCount(this.entitlements(), this.locations().length),
  );

  readonly filteredLocations = computed(() => {
    const query = this.locationSearch().trim().toLowerCase();
    const list = this.locations();
    if (!query) {
      return list;
    }
    return list.filter((loc) => {
      const haystack = [loc.name ?? "", loc.slug, loc.addressLine, loc.area, loc.marketId]
        .join(" ")
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  readonly sortedFilteredLocations = computed(() =>
    sortDataRows(
      this.filteredLocations(),
      this.sortColumnId(),
      this.sortDirection(),
      this.locationColumns,
    ),
  );

  readonly filteredLocationsCount = computed(() => this.filteredLocations().length);

  readonly paginatedLocations = computed(() =>
    sliceListPage(this.sortedFilteredLocations(), this.page(), this.pageSize()),
  );

  readonly locationColumns: AdeniDataTableColumn<BusinessLocation>[] = [
    {
      id: "name",
      header: "Location",
      cell: (l) => l.name || l.slug,
      sortValue: (l) => (l.name || l.slug).toLowerCase(),
    },
    {
      id: "address",
      header: "Address",
      cell: (l) => l.addressLine,
      sortValue: (l) => l.addressLine.toLowerCase(),
    },
    { id: "market", header: "Market", cell: (l) => l.marketId },
    {
      id: "primary",
      header: "Primary",
      cell: () => "",
      sortValue: (l) => (l.isPrimary ? 1 : 0),
    },
  ];

  readonly trackLocationRow = (loc: BusinessLocation): string => loc.id;

  defaultMarketId = "lagos";
  draft: LocationDraft = this.emptyDraft();

  ngOnInit(): void {
    void this.load();
  }

  onLocationSearchChange(value: string): void {
    this.locationSearch.set(value);
    this.page.set(1);
  }

  goToLocationsPage(next: number): void {
    this.page.set(next);
  }

  onLocationsPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  onLocationSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    this.page.set(1);
  }

  onLocationSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    this.page.set(1);
  }

  emptyDraft(): LocationDraft {
    return {
      slug: "",
      name: "",
      addressLine: "",
      area: "",
      marketId: this.defaultMarketId,
      isPrimary: false,
    };
  }

  locationInitials(loc: BusinessLocation): string {
    const label = (loc.name || loc.slug).trim();
    const parts = label.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    return label.slice(0, 2).toUpperCase() || "?";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const publicClient = this.api.createPublicClient();
        const markets = await publicClient.getMarkets();
        this.markets.set(markets);
        this.defaultMarketId = markets[0]?.id ?? "lagos";
        await this.api.withAuthorizedClient(async (c) => {
          const [profile, usage, planEntitlements, items] = await Promise.all([
            c.getTenantProfile().catch(() => null),
            c.getTenantSubscriptionUsage().catch(() => null),
            c.getTenantEntitlements().catch(() => null),
            c.getTenantLocations(),
          ]);
          this.entitlements.set(
            mergeTenantEntitlements(planEntitlements, usage?.entitlements, profile?.entitlements),
          );
          this.locations.set(items);
        });
      }, "Loading locations", "Fetching branches");
    } catch {
      this.error.set("Could not load locations.");
      this.feedback.error("Could not load locations.");
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
    if (!this.canAddLocation()) {
      const message =
        "Your plan includes one location. Upgrade to Business to add more branches.";
      this.error.set(message);
      this.feedback.error(message);
      return;
    }
    this.editingId.set(null);
    this.draft = this.emptyDraft();
    this.showForm.set(true);
  }

  openEdit(loc: BusinessLocation): void {
    this.editingId.set(loc.id);
    this.draft = {
      slug: loc.slug,
      name: loc.name ?? "",
      addressLine: loc.addressLine,
      area: loc.area,
      marketId: loc.marketId,
      isPrimary: loc.isPrimary,
    };
    this.showForm.set(true);
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
  }

  async save(): Promise<void> {
    const slug = this.draft.slug.trim().toLowerCase();
    if (!slug || !SLUG_PATTERN.test(slug) || !this.draft.addressLine.trim() || !this.draft.area.trim()) {
      this.error.set("Check slug, address, and area.");
      this.feedback.error("Check slug, address, and area.");
      return;
    }

    const body = {
      slug,
      name: this.draft.name.trim() || undefined,
      addressLine: this.draft.addressLine.trim(),
      area: this.draft.area.trim(),
      marketId: this.draft.marketId,
      isPrimary: this.draft.isPrimary,
    };

    this.busy.set(this.editingId() ?? "new");
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const id = this.editingId();
        if (id) {
          const updated = await this.api.withAuthorizedClient((c) => c.updateTenantLocation(id, body));
          this.locations.update((list) => mergeSavedLocation(list, updated, id));
        } else {
          const created = await this.api.withAuthorizedClient((c) => c.addTenantLocation(body));
          this.locations.update((list) => mergeSavedLocation(list, created, null));
        }
      }, "Saving location", body.name ?? slug);
      this.cancelForm();
      this.feedback.success("Location saved.");
    } catch (err) {
      const message =
        err instanceof AdeniApiError && err.message
          ? err.message
          : "Could not save location.";
      this.error.set(message);
      this.feedback.error(message);
    } finally {
      this.busy.set(null);
    }
  }

  async remove(loc: BusinessLocation): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Delete location?",
      message: `Delete “${loc.name || loc.slug}”? It will no longer appear on your public page.`,
      confirmLabel: "Delete",
      danger: true,
    });
    if (!ok) {
      return;
    }
    this.busy.set(loc.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.deactivateTenantLocation(loc.id)),
        "Deleting location",
        loc.name || loc.slug,
      );
      if (loc.isPrimary) {
        const items = await this.api.withAuthorizedClient((c) => c.getTenantLocations());
        this.locations.set(items);
      } else {
        this.locations.update((list) => list.filter((l) => l.id !== loc.id));
      }
      this.feedback.success("Location removed.");
    } catch {
      this.error.set("Could not delete location.");
      this.feedback.error("Could not delete location.");
    } finally {
      this.busy.set(null);
    }
  }

  async downloadLocationsExport(format: ReportExportFormat): Promise<void> {
    if (format !== "csv") {
      return;
    }
    this.exporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setFullYear(from.getFullYear() - 1);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.tenantLocations,
          from: from.toISOString(),
          to: to.toISOString(),
          format: "csv",
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `tenant-locations-${to.toISOString().slice(0, 10)}.csv`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.feedback.success("Locations downloaded.");
    } catch {
      this.feedback.error("Could not download locations.");
    } finally {
      this.exporting.set(false);
    }
  }
}
