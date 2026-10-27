import { Component, computed, DestroyRef, inject, OnDestroy, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed, toSignal } from "@angular/core/rxjs-interop";
import { ActivatedRoute, Router, RouterLink } from "@angular/router";
import { distinctUntilChanged, map } from "rxjs";
import type { ServiceMenuGroup, ServiceOffering, ServiceTemplate } from "@adeni/shared";
import {
  formatPrice,
  getCategoryLabel,
  mergeTenantEntitlements,
} from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniConfirmService,
  AdeniFeedbackService,
  AdeniPillComponent,
  AdeniPortalEmptyStateComponent,
  AdeniStaffButtonComponent,
  AdeniTablePersonCellComponent,
  PortalPageComponent,
  AdeniTabsComponent,
  type AdeniPillTone,
  type AdeniTab,
  injectAdeniTranslate,
  injectAdeniApiError,
} from "@adeni/ui";
import { AdeniApiError } from "@adeni/api-client";
import { BusinessApiService } from "../../core/services/business-api.service";
import { PortalActiveLocationService } from "../../core/services/portal-active-location.service";
import { PortalSessionService } from "../../core/services/portal-session.service";
import { Subscription } from "rxjs";
import { ServiceEditFormComponent } from "./service-edit-form.component";
import { ServiceIntakePanelComponent } from "./service-intake-panel.component";
import { emptyServiceDraft, type ServiceDraft } from "./service-draft";
import { draftFromService, serviceDraftToUpdateBody } from "./service-save.util";

const SERVICE_DETAIL_TABS = new Set(["details", "intake"]);

function normalizeServiceDetailTab(raw: string | null): string {
  return raw && SERVICE_DETAIL_TABS.has(raw) ? raw : "details";
}

@Component({
  selector: "app-service-detail",
  standalone: true,
  imports: [
    PortalPageComponent,
    AdeniTabsComponent,
    AdeniCarbonIconComponent,
    AdeniPortalEmptyStateComponent,
    AdeniTablePersonCellComponent,
    AdeniPillComponent,
    AdeniStaffButtonComponent,
    RouterLink,
    ServiceEditFormComponent,
    ServiceIntakePanelComponent,
  ],
  templateUrl: "./service-detail.component.html",
  styleUrl: "./service-detail.component.scss",
})
export class ServiceDetailComponent implements OnInit, OnDestroy {
  protected readonly tr = injectAdeniTranslate();
  protected readonly apiErr = injectAdeniApiError();
  private readonly api = inject(BusinessApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly feedback = inject(AdeniFeedbackService);
  private readonly confirmDialog = inject(AdeniConfirmService);
  private readonly activeLocation = inject(PortalActiveLocationService);
  private readonly destroyRef = inject(DestroyRef);
  readonly session = inject(PortalSessionService);

  private loadVersion = 0;
  private branchSub: Subscription | null = null;

  readonly tab = toSignal(
    this.route.queryParamMap.pipe(map((params) => normalizeServiceDetailTab(params.get("tab")))),
    {
      initialValue: normalizeServiceDetailTab(this.route.snapshot.queryParamMap.get("tab")),
    },
  );

  readonly error = signal<string | null>(null);
  readonly service = signal<ServiceOffering | null>(null);
  readonly groups = signal<ServiceMenuGroup[]>([]);
  readonly busy = signal(false);
  readonly catalogLoading = signal(false);
  readonly catalogTemplates = signal<ServiceTemplate[]>([]);
  readonly primaryCategorySlug = signal("");

  defaultCurrency = "NGN";
  draft: ServiceDraft = emptyServiceDraft({ currency: "NGN", categorySlug: "" });

  readonly formatPrice = formatPrice;
  readonly categoryLabel = getCategoryLabel;

  readonly canManageIntake = computed(
    () =>
      this.session.loaded() &&
      (this.session.permissionRole() === "owner" || this.session.permissionRole() === "manager"),
  );

  readonly tabs = computed((): AdeniTab[] => {
    const base: AdeniTab[] = [
      { id: "details", label: this.tr("portal.services.details") },
      { id: "intake", label: this.tr("portal.services.consultationFormTab") },
    ];
    return base;
  });

  ngOnInit(): void {
    this.route.paramMap
      .pipe(
        map((params) => params.get("id")),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe(() => {
        void this.load();
      });
    this.branchSub = this.activeLocation.selectionChanged$.subscribe(() => {
      void this.activeLocation.runOnBranchSwitch(() => this.load());
    });
  }

  ngOnDestroy(): void {
    this.loadVersion++;
    this.branchSub?.unsubscribe();
  }

  async load(): Promise<void> {
    const version = ++this.loadVersion;
    const id = this.route.snapshot.paramMap.get("id");
    if (!id) {
      this.error.set(this.tr("portal.services.serviceNotFound"));
      return;
    }

    this.error.set(null);
    this.service.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const result = await this.api.withAuthorizedClient(async (client) => {
          const profile = await client.getTenantProfile().catch(() => null);
          const planEntitlements = await client.getTenantEntitlements().catch(() => null);
          const mergedForLocation = mergeTenantEntitlements(
            planEntitlements,
            profile?.entitlements,
            profile?.entitlements,
          );
          this.activeLocation.tenantApiLocationId(mergedForLocation);
          const catalog = await client.getTenantServiceCatalog();
          return { catalog, profile };
        });
        if (version !== this.loadVersion) {
          return;
        }
        const found = result.catalog.items.find((s) => s.id === id) ?? null;
        this.groups.set(result.catalog.groups);
        if (result.profile?.categorySlug) {
          this.primaryCategorySlug.set(result.profile.categorySlug);
        }
        const marketId = result.profile?.locations[0]?.marketId?.toLowerCase();
        if (marketId === "lagos" || marketId === "abuja") {
          this.defaultCurrency = "NGN";
        } else if (marketId === "ottawa" || marketId === "toronto") {
          this.defaultCurrency = "CAD";
        } else if (result.catalog.items[0]?.currency) {
          this.defaultCurrency = result.catalog.items[0].currency;
        }
        if (!found) {
          this.service.set(null);
          return;
        }
        this.service.set(found);
        this.draft = draftFromService(found, this.primaryCategorySlug());
        void this.loadCatalog(this.draft.categorySlug);
      });
    } catch (err) {
      if (version !== this.loadVersion) {
        return;
      }
      if (err instanceof AdeniApiError && err.statusCode === 404) {
        this.error.set(null);
      } else {
        this.error.set(this.apiErr(err, "portal.services.couldNotLoadService"));
      }
      this.service.set(null);
    }
  }

  async loadCatalog(categorySlug: string): Promise<void> {
    if (!categorySlug) {
      this.catalogTemplates.set([]);
      return;
    }
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

  serviceInitials(service: ServiceOffering): string {
    const parts = service.name.trim().split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return (parts[0]!.charAt(0) + parts[1]!.charAt(0)).toUpperCase();
    }
    const word = parts[0] ?? "?";
    return word.slice(0, 2).toUpperCase();
  }

  groupName(groupId: string | null | undefined): string {
    if (!groupId) {
      return this.tr("portal.services.noSection");
    }
    return this.groups().find((g) => g.id === groupId)?.name ?? this.tr("portal.services.noSection");
  }

  sectionPillTone(service: ServiceOffering): AdeniPillTone {
    return service.menuGroupId ? "brand" : "neutral";
  }

  applyTemplate(template: ServiceTemplate): void {
    this.draft.name = template.name;
    this.draft.durationMinutes = String(template.defaultDurationMinutes);
    this.draft.catalogServiceId = template.id;
    this.draft.bookingDeliveryType = template.bookingDeliveryType;
    this.draft.categorySlug = this.draft.categorySlug || this.primaryCategorySlug();
  }

  async save(): Promise<void> {
    const service = this.service();
    if (!service) {
      return;
    }
    const body = serviceDraftToUpdateBody(this.draft);
    if (!body) {
      this.error.set(this.tr("portal.services.fillNamePriceDuration"));
      return;
    }

    const confirmed = await this.confirmDialog.confirmKind("save", {
      subject: body.name,
    });
    if (!confirmed) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    try {
      await this.feedback.runLoading(async () => {
        const updated = await this.api.withAuthorizedClient((c) =>
          c.updateTenantService(service.id, { ...body, isActive: true }),
        );
        this.service.set(updated);
        this.draft = draftFromService(updated, this.primaryCategorySlug());
      }, this.tr("portal.services.savingService"), body.name);
      this.feedback.success(`"${body.name}" is on your menu.`, this.tr("portal.services.serviceSaved"));
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotSaveService"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotSaveService"));
    } finally {
      this.busy.set(false);
    }
  }

  onCancelDetails(): void {
    const service = this.service();
    if (service) {
      this.draft = draftFromService(service, this.primaryCategorySlug());
    }
  }

  async deactivate(): Promise<void> {
    const service = this.service();
    if (!service) {
      return;
    }
    const ok = await this.confirmDialog.confirm({
      title: this.tr("portal.services.deactivateService2"),
      message: this.tr("portal.services.deactivateCustomersWonTBe", { name: service.name }),
      confirmLabel: this.tr("common.deactivate"),
      danger: true,
    });
    if (!ok) {
      return;
    }
    this.busy.set(true);
    try {
      await this.feedback.runLoading(
        () => this.api.withAuthorizedClient((c) => c.deactivateTenantService(service.id)),
        this.tr("portal.services.deactivatingService"),
        service.name,
      );
      this.feedback.success(`"${service.name}" is no longer bookable.`, this.tr("portal.services.serviceDeactivated"));
      await this.router.navigate(["/services"]);
    } catch (caught) {
      this.error.set(this.apiErr(caught, "portal.services.couldNotDeactivateService"));
      this.feedback.error(this.apiErr(caught, "portal.services.couldNotDeactivateService"));
    } finally {
      this.busy.set(false);
    }
  }
}
