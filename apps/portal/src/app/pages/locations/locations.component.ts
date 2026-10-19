import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniDataTableActionsDirective,
  AdeniDataTableCellDirective,
  AdeniDataTableComponent,
  AdeniExportMenuComponent,
  AdeniFeedbackService,
  AdeniIconBtnComponent,
  AdeniListToolbarComponent,
  AdeniModalComponent,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniRowActionBarComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  readPersistedListViewMode,
  type AdeniDataTableColumn,
  type AdeniExportMenuItem,
  type AdeniListViewMode,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;

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
  readonly markets = signal<MarketConfig[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly locationSearch = signal("");
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("portal.locations", "cards"),
  );

  readonly exportItems: AdeniExportMenuItem[] = [
    {
      format: "csv",
      label: "CSV (.csv)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "xlsx",
      label: "Excel (.xlsx)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
    {
      format: "pdf",
      label: "PDF (.pdf)",
      disabled: true,
      hint: "Reporting export — coming soon",
    },
  ];

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

  readonly locationColumns: AdeniDataTableColumn<BusinessLocation>[] = [
    { id: "name", header: "Location", cell: (l) => l.name || l.slug },
    { id: "address", header: "Address", cell: (l) => l.addressLine },
    { id: "market", header: "Market", cell: (l) => l.marketId },
    { id: "primary", header: "Primary", cell: () => "" },
  ];

  readonly trackLocationRow = (loc: BusinessLocation): string => loc.id;

  defaultMarketId = "lagos";
  draft: LocationDraft = this.emptyDraft();

  ngOnInit(): void {
    void this.load();
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
        const items = await this.api.withAuthorizedClient((c) => c.getTenantLocations());
        this.locations.set(items);
      }, "Loading locations", "Fetching branches");
    } catch {
      this.error.set("Could not load locations.");
      this.feedback.error("Could not load locations.");
    } finally {
      this.loading.set(false);
    }
  }

  openCreate(): void {
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
          this.locations.update((list) => list.map((l) => (l.id === id ? updated : l)));
        } else {
          const created = await this.api.withAuthorizedClient((c) => c.addTenantLocation(body));
          this.locations.update((list) => [...list, created]);
        }
      }, "Saving location", body.name ?? slug);
      this.cancelForm();
      this.feedback.success("Location saved.");
    } catch {
      this.error.set("Could not save location.");
      this.feedback.error("Could not save location.");
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
      this.locations.update((list) => list.filter((l) => l.id !== loc.id));
      this.feedback.success("Location removed.");
    } catch {
      this.error.set("Could not delete location.");
      this.feedback.error("Could not delete location.");
    } finally {
      this.busy.set(null);
    }
  }
}
