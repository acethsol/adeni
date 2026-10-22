import { DatePipe } from "@angular/common";
import { Component, computed, inject } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { ActivatedRoute, RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import { formatSubscriptionSku, formatTenantStatus } from "@adeni/shared";
import {
  AdeniCarbonIconComponent,
  AdeniStaffButtonComponent,
  PortalPageComponent,
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
    RouterLinkActive,
    RouterOutlet,
    DatePipe,
    AdeniStaffButtonComponent,
    AdeniCarbonIconComponent,
    AdminPillComponent,
  ],
  templateUrl: "./business-detail-shell.component.html",
  styleUrl: "./business-detail-shell.component.scss",
})
export class BusinessDetailShellComponent {
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

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get("tenantId");
      if (id) {
        this.facade.ensureLoaded(id);
      }
    });
  }
}
