import { Component } from "@angular/core";
import { RouterLink } from "@angular/router";
import { PortalPageComponent, injectAdeniTranslate } from "@adeni/ui";
import { DiscoverySearchComponent } from "../../../shared/discovery-search.component";

/**
 * `/app/discover` — search entry inside the app shell. Results and business pages are the canonical,
 * SEO/SSR public routes (`/discover`, `/businesses/:slug`), so booking never forks into the app surface
 * (spec §6.4). Featured cards and the "back to app" affordance arrive in phase E.
 */
@Component({
  selector: "app-app-discover",
  standalone: true,
  imports: [RouterLink, PortalPageComponent, DiscoverySearchComponent],
  template: `
    <app-portal-page [title]="tr('discover.app.nav.discover')" [description]="tr('discover.appDiscover.description')">
      <div class="app-discover-search">
        <app-discovery-search variant="default" />
      </div>
      <p class="app-more">
        <a class="cds-btn cds-btn--tertiary cds-btn--sm" routerLink="/discover">{{ tr('discover.appDiscover.browseAll') }}</a>
      </p>
    </app-portal-page>
  `,
})
export class AppDiscoverComponent {
  protected readonly tr = injectAdeniTranslate();
}
