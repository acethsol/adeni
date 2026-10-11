import { Component, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type {
  BookingDeliveryType,
  ServiceMenuGroup,
  ServiceOffering,
  ServiceTemplate,
} from "@adeni/shared";
import { getCategoryLabel } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  PortalPageComponent,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";
import { formatPrice } from "@adeni/shared";

type ServiceDraft = {
  name: string;
  description: string;
  priceAmount: string;
  currency: string;
  durationMinutes: string;
  categorySlug: string;
  catalogServiceId: string;
  bookingDeliveryType: BookingDeliveryType;
  menuGroupId: string;
  sortOrder: string;
  isAddOn: boolean;
};

@Component({
  selector: "app-services",
  standalone: true,
  imports: [PortalPageComponent, FormsModule, AdeniCarbonIconComponent],
  templateUrl: "./services.component.html",
  styleUrl: "./services.component.scss",
})
export class ServicesComponent implements OnInit {
  private readonly api = inject(BusinessApiService);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly services = signal<ServiceOffering[]>([]);
  readonly groups = signal<ServiceMenuGroup[]>([]);
  readonly busy = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly showForm = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogTemplates = signal<ServiceTemplate[]>([]);
  readonly primaryCategorySlug = signal("");
  readonly newGroupName = signal("");

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
      menuGroupId: "",
      sortOrder: "0",
      isAddOn: false,
    };
  }

  groupName(groupId: string | null | undefined): string {
    if (!groupId) {
      return "Ungrouped";
    }
    return this.groups().find((g) => g.id === groupId)?.name ?? "Ungrouped";
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          const [catalog, profile] = await Promise.all([
            c.getTenantServiceCatalog(),
            c.getTenantProfile().catch(() => null),
          ]);
          this.services.set(catalog.items);
          this.groups.set(catalog.groups);
          if (profile?.categorySlug) {
            this.primaryCategorySlug.set(profile.categorySlug);
          }
          const marketId = profile?.locations[0]?.marketId?.toLowerCase();
          if (marketId === "lagos" || marketId === "abuja") this.defaultCurrency = "NGN";
          else if (marketId === "ottawa" || marketId === "toronto") this.defaultCurrency = "CAD";
          else if (catalog.items[0]?.currency) this.defaultCurrency = catalog.items[0].currency;
        });
      }, "Loading services…", "Fetching menu and collections");
    } catch {
      this.error.set("Could not load services.");
      this.feedback.error("Could not load services.");
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
      menuGroupId: service.menuGroupId ?? "",
      sortOrder: String(service.sortOrder ?? 0),
      isAddOn: service.isAddOn === true,
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

  async addGroup(): Promise<void> {
    const name = this.newGroupName().trim();
    if (name.length < 2) {
      this.error.set("Collection name needs at least 2 characters.");
      return;
    }
    this.busy.set("group");
    this.error.set(null);
    try {
      const created = await this.feedback.runLoading(
        () =>
          this.api.withAuthorizedClient((c) =>
            c.createTenantServiceMenuGroup({
              name,
              sortOrder: this.groups().length,
            }),
          ),
        "Adding collection…",
        name,
      );
      this.groups.update((list) => [...list, created].sort((a, b) => a.sortOrder - b.sortOrder));
      this.newGroupName.set("");
      this.feedback.success(`“${created.name}” is ready to assign.`, "Collection added");
    } catch {
      this.error.set("Could not create collection.");
      this.feedback.error("Could not create collection.");
    } finally {
      this.busy.set(null);
    }
  }

  async removeGroup(group: ServiceMenuGroup): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Remove collection?",
      message: `Remove “${group.name}”? Services stay on the menu, ungrouped.`,
      confirmLabel: "Remove",
      danger: true,
    });
    if (!ok) {
      return;
    }
    this.busy.set(group.id);
    this.error.set(null);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.deleteTenantServiceMenuGroup(group.id)),
        "Removing collection…",
        group.name,
      );
      this.groups.update((list) => list.filter((g) => g.id !== group.id));
      this.services.update((list) =>
        list.map((s) => (s.menuGroupId === group.id ? { ...s, menuGroupId: null } : s)),
      );
      this.feedback.success(`“${group.name}” removed. Services are ungrouped.`, "Collection removed");
    } catch {
      this.error.set("Could not remove collection.");
      this.feedback.error("Could not remove collection.");
    } finally {
      this.busy.set(null);
    }
  }

  async save(): Promise<void> {
    const price = Number(this.draft.priceAmount);
    const duration = Number(this.draft.durationMinutes);
    const sortOrder = Number(this.draft.sortOrder);
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
      menuGroupId: this.draft.menuGroupId.trim() || null,
      sortOrder: Number.isNaN(sortOrder) ? 0 : Math.round(sortOrder),
      isAddOn: this.draft.isAddOn,
    };

    try {
      await this.feedback.runLoading(async () => {
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
      }, "Saving service…", createBody.name);
      this.cancelForm();
      this.feedback.success(`“${createBody.name}” is on your menu.`, "Service saved");
    } catch {
      this.error.set("Could not save service.");
      this.feedback.error("Could not save service.");
    } finally {
      this.busy.set(null);
    }
  }

  async deactivate(service: ServiceOffering): Promise<void> {
    const ok = await this.confirmDialog.confirm({
      title: "Deactivate service?",
      message: `Deactivate “${service.name}”? Customers won’t be able to book it.`,
      confirmLabel: "Deactivate",
      danger: true,
    });
    if (!ok) {
      return;
    }
    this.busy.set(service.id);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.deactivateTenantService(service.id)),
        "Deactivating service…",
        service.name,
      );
      this.services.update((list) => list.filter((s) => s.id !== service.id));
      this.feedback.success(`“${service.name}” is no longer bookable.`, "Service deactivated");
    } catch {
      this.error.set("Could not deactivate service.");
      this.feedback.error("Could not deactivate service.");
    } finally {
      this.busy.set(null);
    }
  }
}
