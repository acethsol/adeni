import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BusinessLocation, MarketConfig } from "@adeni/shared";
import { PortalPageComponent } from "@adeni/ui";
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
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./locations.component.html",
  styleUrl: "./locations.component.scss",
})
export class LocationsComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly locations = signal<BusinessLocation[]>([]);
  readonly markets = signal<MarketConfig[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);

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

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      const publicClient = this.api.createPublicClient();
      const markets = await publicClient.getMarkets();
      this.markets.set(markets);
      this.defaultMarketId = markets[0]?.id ?? "lagos";

      const items = await this.api.withAuthorizedClient((c) => c.getTenantLocations());
      this.locations.set(items);
    } catch {
      this.error.set("Could not load locations.");
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
  }

  async save(): Promise<void> {
    const slug = this.draft.slug.trim().toLowerCase();
    if (!slug || !SLUG_PATTERN.test(slug) || !this.draft.addressLine.trim() || !this.draft.area.trim()) {
      this.error.set("Check slug, address, and area.");
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
      const id = this.editingId();
      if (id) {
        const updated = await this.api.withAuthorizedClient((c) => c.updateTenantLocation(id, body));
        this.locations.update((list) => list.map((l) => (l.id === id ? updated : l)));
      } else {
        const created = await this.api.withAuthorizedClient((c) => c.addTenantLocation(body));
        this.locations.update((list) => [...list, created]);
      }
      this.cancelForm();
    } catch {
      this.error.set("Could not save location.");
    } finally {
      this.busy.set(null);
    }
  }

  async remove(loc: BusinessLocation): Promise<void> {
    if (!confirm(`Delete location ${loc.slug}?`)) return;
    this.busy.set(loc.id);
    try {
      await this.api.withAuthorizedClient((c) => c.deactivateTenantLocation(loc.id));
      this.locations.update((list) => list.filter((l) => l.id !== loc.id));
    } catch {
      this.error.set("Could not delete location.");
    } finally {
      this.busy.set(null);
    }
  }
}
