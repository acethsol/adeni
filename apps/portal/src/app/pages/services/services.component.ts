import { Component, computed, inject, OnInit, signal } from "@angular/core";
import { FormsModule } from "@angular/forms";
import type {
  BookingDeliveryType,
  ServiceMenuGroup,
  ServiceOffering,
  ServiceTemplate,
} from "@adeni/shared";
import {
  buildReportExportMenuItems,
  DEFAULT_PAGE_SIZE,
  formatPrice,
  getCategoryLabel,
  LIST_PAGE_SIZE_OPTIONS,
  mergeTenantEntitlements,
  REPORT_KEYS,
  sliceListPage,
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
  AdeniFilterSelectComponent,
  AdeniIconBtnComponent,
  AdeniListFilterRowComponent,
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
  type AdeniFilterSelectOption,
  type AdeniListViewMode,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { BusinessApiService } from "../../core/services/business-api.service";

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
  imports: [
    PortalPageComponent,
    FormsModule,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniListToolbarComponent,
    AdeniListPaginationComponent,
    AdeniListFilterRowComponent,
    AdeniFilterSelectComponent,
    AdeniExportMenuComponent,
    AdeniDataTableComponent,
    AdeniDataTableCellDirective,
    AdeniDataTableActionsDirective,
    AdeniRowActionBarComponent,
    AdeniIconBtnComponent,
    AdeniPillComponent,
    AdeniModalComponent,
    AdeniTablePersonCellComponent,
  ],
  templateUrl: "./services.component.html",
  styleUrl: "./services.component.scss",
})
export class ServicesComponent implements OnInit {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
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
  readonly showCollectionsManager = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogTemplates = signal<ServiceTemplate[]>([]);
  readonly primaryCategorySlug = signal("");
  readonly newGroupName = signal("");
  readonly menuSearch = signal("");
  readonly groupFilter = signal("all");
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  readonly listPageSizeOptions = LIST_PAGE_SIZE_OPTIONS;
  readonly viewMode = signal<AdeniListViewMode>(
    readPersistedListViewMode("portal.services", "cards"),
  );

  readonly entitlements = signal<TenantEntitlements | null>(null);
  readonly exporting = signal(false);

  readonly exportItems = computed((): AdeniExportMenuItem[] =>
    buildReportExportMenuItems({
      entitlements: this.entitlements(),
      xlsx: true,
    }),
  );

  readonly groupFilterOptions = computed((): AdeniFilterSelectOption[] => [
    { value: "all", label: "All sections" },
    { value: "none", label: "No section" },
    ...this.groups().map((g) => ({ value: g.id, label: g.name })),
  ]);

  readonly sortColumnId = signal<string | null>(null);
  readonly sortDirection = signal<AdeniDataTableSortDirection>("asc");

  readonly filteredServices = computed(() => {
    let list = this.services();
    switch (this.groupFilter()) {
      case "none":
        list = list.filter((s) => !s.menuGroupId);
        break;
      case "all":
        break;
      default:
        list = list.filter((s) => s.menuGroupId === this.groupFilter());
        break;
    }
    const query = this.menuSearch().trim().toLowerCase();
    if (!query) {
      return list;
    }
    return list.filter((service) => {
      const haystack = [
        service.name,
        service.description ?? "",
        this.groupName(service.menuGroupId),
        service.categorySlug ? getCategoryLabel("en", service.categorySlug) : "",
      ]
        .join(" ")
        .toLowerCase();
      return haystack.includes(query);
    });
  });

  readonly sortedFilteredServices = computed(() =>
    sortDataRows(
      this.filteredServices(),
      this.sortColumnId(),
      this.sortDirection(),
      this.serviceTableColumns,
    ),
  );

  readonly filteredServicesCount = computed(() => this.filteredServices().length);

  readonly paginatedServices = computed(() =>
    sliceListPage(this.sortedFilteredServices(), this.page(), this.pageSize()),
  );

  readonly serviceTableColumns: AdeniDataTableColumn<ServiceOffering>[] = [
    {
      id: "name",
      header: "Service",
      cell: (s) => s.name,
      sortValue: (s) => s.name.toLowerCase(),
    },
    {
      id: "collection",
      header: "Section",
      cell: (s) => this.groupName(s.menuGroupId),
      sortValue: (s) => this.groupName(s.menuGroupId).toLowerCase(),
    },
    {
      id: "tags",
      header: "Details",
      cell: () => "",
      sortValue: (s) => s.priceAmount,
    },
  ];

  readonly trackServiceRow = (service: ServiceOffering): string => service.id;

  defaultCurrency = "NGN";
  draft: ServiceDraft = this.emptyDraft();

  readonly formatPrice = formatPrice;
  readonly categoryLabel = getCategoryLabel;

  ngOnInit(): void {
    void this.load();
  }

  onMenuSearchChange(value: string): void {
    this.menuSearch.set(value);
    this.page.set(1);
  }

  onGroupFilterChange(value: string): void {
    this.groupFilter.set(value);
    this.page.set(1);
  }

  goToServicesPage(next: number): void {
    this.page.set(next);
  }

  onServicesPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.page.set(1);
  }

  onServiceSortColumnChange(columnId: string | null): void {
    this.sortColumnId.set(columnId);
    this.page.set(1);
  }

  onServiceSortDirectionChange(direction: AdeniDataTableSortDirection): void {
    this.sortDirection.set(direction);
    this.page.set(1);
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
      return "No section";
    }
    return this.groups().find((g) => g.id === groupId)?.name ?? "No section";
  }

  serviceInitials(service: ServiceOffering): string {
    const parts = service.name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        await this.api.withAuthorizedClient(async (c) => {
          const [catalog, profile, usage, planEntitlements] = await Promise.all([
            c.getTenantServiceCatalog(),
            c.getTenantProfile().catch(() => null),
            c.getTenantSubscriptionUsage().catch(() => null),
            c.getTenantEntitlements().catch(() => null),
          ]);
          this.services.set(catalog.items);
          this.groups.set(catalog.groups);
          this.entitlements.set(
            mergeTenantEntitlements(planEntitlements, usage?.entitlements, profile?.entitlements),
          );
          if (profile?.categorySlug) {
            this.primaryCategorySlug.set(profile.categorySlug);
          }
          const marketId = profile?.locations[0]?.marketId?.toLowerCase();
          if (marketId === "lagos" || marketId === "abuja") this.defaultCurrency = "NGN";
          else if (marketId === "ottawa" || marketId === "toronto") this.defaultCurrency = "CAD";
          else if (catalog.items[0]?.currency) this.defaultCurrency = catalog.items[0].currency;
        });
      }, "Loading services…", "Fetching menu and collections");
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotLoadServices"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotLoadServices"));
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
      this.error.set(this.tr("portal.services.collectionNameNeedsAtLeast"));
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
      this.feedback.success(`“${created.name}” is ready to assign.`, this.tr("portal.services.collectionAdded"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotCreateCollection"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotCreateCollection"));
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
      this.feedback.success(`“${group.name}” removed. Services are ungrouped.`, this.tr("portal.services.collectionRemoved"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotRemoveCollection"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotRemoveCollection"));
    } finally {
      this.busy.set(null);
    }
  }

  async save(): Promise<void> {
    const price = Number(this.draft.priceAmount);
    const duration = Number(this.draft.durationMinutes);
    const sortOrder = Number(this.draft.sortOrder);
    if (!this.draft.name.trim() || Number.isNaN(price) || Number.isNaN(duration)) {
      this.error.set(this.tr("portal.services.fillNamePriceDuration"));
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
      this.feedback.success(`“${createBody.name}” is on your menu.`, this.tr("portal.services.serviceSaved"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotSaveService"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotSaveService"));
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
      this.feedback.success(`“${service.name}” is no longer bookable.`, this.tr("portal.services.serviceDeactivated"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotDeactivateService"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotDeactivateService"));
    } finally {
      this.busy.set(null);
    }
  }

  async downloadServicesExport(format: ReportExportFormat): Promise<void> {
    this.exporting.set(true);
    try {
      const to = new Date();
      const from = new Date(to);
      from.setFullYear(from.getFullYear() - 1);
      const blob = await this.api.withAuthorizedClient((client) =>
        client.downloadTenantReportExport({
          reportKey: REPORT_KEYS.tenantServices,
          from: from.toISOString(),
          to: to.toISOString(),
          format,
        }),
      );
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = url;
      anchor.download = `tenant-services-${to.toISOString().slice(0, 10)}.${format === "xlsx" ? "xlsx" : "csv"}`;
      anchor.click();
      URL.revokeObjectURL(url);
      this.feedback.success(this.tr("portal.services.serviceMenuDownloaded"));
    } catch (caught) {
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotDownloadServiceMenu"));
    } finally {
      this.exporting.set(false);
    }
  }
}
