import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink, RouterOutlet } from "@angular/router";
import { formatSubscriptionSku, formatTenantStatus } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniStaffButtonComponent,
  AdeniTabsComponent,
  type AdeniTab,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { AdminPillComponent } from "../../shared/admin-pill.component";
import { billingStatusPill, businessPlanPill, tenantStatusPill } from "../../shared/admin-pill-maps";
import { BusinessDetailFacade } from "./business-detail.facade";

@Component({
  selector: "app-business-detail-shell",
  standalone: true,
  providers: [BusinessDetailFacade],
  imports: [
    PortalPageComponent,
    RouterLink,
    RouterOutlet,
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniCarbonIconComponent,
    AdeniTabsComponent,
    AdminPillComponent,
  ],
  templateUrl: "./business-detail-shell.component.html",
  styleUrl: "./business-detail-shell.component.scss",
})
export class BusinessDetailShellComponent {
  protected readonly tr = injectAdeniTranslate();
  private readonly route = inject(ActivatedRoute);
  readonly facade = inject(BusinessDetailFacade);

  readonly tenantStatusPill = tenantStatusPill;
  readonly businessPlanPill = businessPlanPill;
  readonly billingStatusPill = billingStatusPill;
  readonly formatTenantStatus = formatTenantStatus;
  readonly formatSubscriptionSku = formatSubscriptionSku;

  readonly pageDescription = computed(() => {
    const b = this.facade.business();
    if (!b) {
      return "Tenant profile, subscription, and billing history.";
    }
    return `${formatTenantStatus(b.status)} · ${formatSubscriptionSku(b.subscriptionSku)} · Adeni SaaS and commercial terms.`;
  });

  readonly detailTabs = computed((): AdeniTab[] => {
    const charges = this.facade.charges().length;
    const audit = this.facade.commercialAudit().length;
    const billingLabel = charges > 0 ? `Billing (${charges})` : "Billing";
    const auditLabel = audit > 0 ? `Commercial audit (${audit})` : "Commercial audit";
    return [
      { id: "overview", label: "Overview" },
      { id: "terms", label: "Commercial terms" },
      { id: "billing", label: billingLabel },
      { id: "audit", label: auditLabel },
    ];
  });

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get("tenantId");
      if (id) {
        this.facade.ensureLoaded(id);
      }
    });
  }
}
