import { Component, computed } from "@angular/core";
import { RouterOutlet } from "@angular/router";
import { AdeniTabsComponent, PortalPageComponent, injectAdeniTranslate, type AdeniTab } from "@adeni/ui";

/**
 * `/app/benefits` — hub shell for prepaid packages and purchased gift cards.
 * Reuses the existing page components as child routes (packages, memberships, gift cards).
 */
@Component({
  selector: "app-benefits-hub",
  standalone: true,
  imports: [RouterOutlet, PortalPageComponent, AdeniTabsComponent],
  template: `
    <app-portal-page [title]="tr('customer.app.nav.benefits')" [description]="tr('discover.benefits.description')">
      <adeni-tabs [tabs]="tabs()" linkMode="path" />
      <div class="app-benefits__outlet">
        <router-outlet />
      </div>
    </app-portal-page>
  `,
})
export class BenefitsHubComponent {
  protected readonly tr = injectAdeniTranslate();

  readonly tabs = computed<AdeniTab[]>(() => [
    { id: "packages", label: this.tr("discover.packages.myLink") },
    { id: "memberships", label: this.tr("discover.memberships.myLink") },
    { id: "gift-cards", label: this.tr("discover.packages.giftCardsLink") },
  ]);
}
