import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type { BookingDeliveryType, ServiceOffering, ServiceTemplate } from "@adeni/shared";
import { getCategoryLabel } from "@adeni/shared";
import { PortalPageComponent } from "../../shared/portal-page.component";
import { BusinessApiService } from "../../core/services/business-api.service";
import { formatPrice } from "../../shared/portal-format";

type ServiceDraft = {
  name: string;
  description: string;
  priceAmount: string;
  currency: string;
  durationMinutes: string;
  categorySlug: string;
  catalogServiceId: string;
  bookingDeliveryType: BookingDeliveryType;
};

@Component({
  selector: "app-services",
  standalone: true,
  imports: [PortalPageComponent, FormsModule],
  templateUrl: "./services.component.html",
  styleUrl: "./services.component.scss",
})
export class ServicesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogTemplates = signal<ServiceTemplate[]>([]);
  readonly primaryCategorySlug = signal("");

  defaultCurrency = "NGN";
  draft: ServiceDraft = this.emptyDraft();

  readonly formatPrice = formatPrice;
  readonly categoryLabel = getCategoryLabel;

  ngOnInit(): void {
    void this.load();
  }

  emptyDraft(): ServiceDraft {
    return {
      name: "",
      description: "",
      priceAmount: "",
      currency: this.defaultCurrency,
      durationMinutes: "30",
      categorySlug: this.primaryCategorySlug(),
      catalogServiceId: "",
      bookingDeliveryType: "appointment",
    };
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.api.withAuthorizedClient(async (c) => {
        const [items, profile] = await Promise.all([
          c.getTenantServices(),
          c.getTenantProfile().catch(() => null),
        ]);
        this.services.set(items);
        if (profile?.categorySlug) {
          this.primaryCategorySlug.set(profile.categorySlug);
        }
        const marketId = profile?.locations[0]?.marketId?.toLowerCase();
        if (marketId === "lagos" || marketId === "abuja") this.defaultCurrency = "NGN";
        else if (marketId === "ottawa" || marketId === "toronto") this.defaultCurrency = "CAD";
        else if (items[0]?.currency) this.defaultCurrency = items[0].currency;
      });
    } catch {
      this.error.set("Could not load services.");
    } finally {
      this.loading.set(false);
    }
  }

  async loadCatalog(categorySlug: string): Promise<void> {
    this.catalogLoading.set(true);
    try {
      const templates = await this.api.createPublicClient().getCategoryServiceTemplates(categorySlug);
      this.catalogTemplates.set(templates);
    } catch {
      this.catalogTemplates.set([]);
    } finally {
      this.catalogLoading.set(false);
    }
  }

  async openCreate(): Promise<void> {
    this.editingId.set(null);
    this.draft = this.emptyDraft();
    this.draft.categorySlug = this.primaryCategorySlug();
    this.showForm.set(true);
    if (this.primaryCategorySlug()) {
      await this.loadCatalog(this.primaryCategorySlug());
    }
  }

  openEdit(service: ServiceOffering): void {
    this.editingId.set(service.id);
    this.draft = {
      name: service.name,
      description: service.description ?? "",
      priceAmount: String(service.priceAmount),
      currency: service.currency,
      durationMinutes: String(service.durationMinutes),
      categorySlug: service.categorySlug ?? this.primaryCategorySlug(),
      catalogServiceId: service.catalogServiceId ?? "",
      bookingDeliveryType: service.bookingDeliveryType ?? "appointment",
    };
    this.showForm.set(true);
    void this.loadCatalog(this.draft.categorySlug);
  }

  applyTemplate(template: ServiceTemplate): void {
    this.draft.name = template.name;
    this.draft.durationMinutes = String(template.defaultDurationMinutes);
    this.draft.catalogServiceId = template.id;
    this.draft.bookingDeliveryType = template.bookingDeliveryType;
    this.draft.categorySlug = this.draft.categorySlug || this.primaryCategorySlug();
  }

  cancelForm(): void {
    this.showForm.set(false);
    this.editingId.set(null);
    this.catalogTemplates.set([]);
  }

  async save(): Promise<void> {
    const price = Number(this.draft.priceAmount);
    const duration = Number(this.draft.durationMinutes);
    if (!this.draft.name.trim() || Number.isNaN(price) || Number.isNaN(duration)) {
      this.error.set("Fill in name, price, and duration.");
      return;
    }

    this.busy.set(this.editingId() ?? "new");
    this.error.set(null);
    const createBody = {
      name: this.draft.name.trim(),
      description: this.draft.description.trim() || undefined,
      priceAmount: price,
      currency: this.draft.currency.trim().toUpperCase(),
      durationMinutes: Math.round(duration),
      categorySlug: this.draft.categorySlug.trim() || undefined,
      catalogServiceId: this.draft.catalogServiceId.trim() || undefined,
      bookingDeliveryType: this.draft.bookingDeliveryType || "appointment",
    };

    try {
      const id = this.editingId();
      if (id) {
        const updated = await this.api.withAuthorizedClient((c) =>
          c.updateTenantService(id, { ...createBody, isActive: true }),
        );
        this.services.update((list) => list.map((s) => (s.id === id ? updated : s)));
      } else {
        const created = await this.api.withAuthorizedClient((c) => c.createTenantService(createBody));
        this.services.update((list) => [...list, created]);
      }
      this.cancelForm();
    } catch {
      this.error.set("Could not save service.");
    } finally {
      this.busy.set(null);
    }
  }

  async deactivate(service: ServiceOffering): Promise<void> {
    if (!confirm(`Deactivate ${service.name}?`)) return;
    this.busy.set(service.id);
    try {
      await this.api.withAuthorizedClient((c) => c.deactivateTenantService(service.id));
      this.services.update((list) => list.filter((s) => s.id !== service.id));
    } catch {
      this.error.set("Could not deactivate service.");
    } finally {
      this.busy.set(null);
    }
  }
}
