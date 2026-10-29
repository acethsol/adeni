import { Component, inject } from "@angular/core";
import { RouterLink } from "@angular/router";
import {
  AdeniCarbonIconComponent,
  PortalPageComponent,
  injectAdeniTranslate,
} from "@adeni/ui";
import { ADENI_DISCOVER_CONFIG } from "../../../core/adeni-config";
import { CustomerAppSessionService } from "../../services/customer-app-session.service";

/**
 * `/app/profile` — read-only identity from Auth0 claims (no profile API exists yet), shortcuts, legal, sign-out.
 * Language switching and notification preferences are deferred (phase E / Notifications module).
 */
@Component({
  selector: "app-customer-profile",
  standalone: true,
  imports: [RouterLink, PortalPageComponent, AdeniCarbonIconComponent],
  template: `
    <app-portal-page [title]="tr('customer.app.nav.profile')" [description]="tr('discover.profile.description')">
      <section class="app-panel app-profile__identity">
        <span class="app-avatar app-avatar--lg" aria-hidden="true">
          @if (session.initials(); as initials) {
            {{ initials }}
          } @else {
            <adeni-carbon-icon name="user--avatar" [size]="28" />
          }
        </span>
        <div>
          <strong>{{ session.displayName() || tr('customer.app.account') }}</strong>
          @if (session.email()) {
            <p class="app-muted">{{ session.email() }}</p>
          } @else if (session.devCustomerMode) {
            <p class="app-muted">{{ tr('discover.layout.devCustomer') }}</p>
          }
        </div>
      </section>

      <ul class="app-list app-profile__links">
        <li>
          <a class="app-row" routerLink="/app/benefits">
            <span class="app-row__main"><adeni-carbon-icon name="gift" [size]="16" />{{ tr('customer.app.nav.benefits') }}</span>
            <adeni-carbon-icon name="chevron--right" [size]="16" />
          </a>
        </li>
        <li>
          <a class="app-row" routerLink="/app/bookings">
            <span class="app-row__main"><adeni-carbon-icon name="calendar" [size]="16" />{{ tr('customer.app.nav.bookings') }}</span>
            <adeni-carbon-icon name="chevron--right" [size]="16" />
          </a>
        </li>
        @if (businessPortalUrl) {
          <li>
            <a class="app-row" [href]="businessPortalUrl" target="_blank" rel="noopener noreferrer">
              <span class="app-row__main"><adeni-carbon-icon name="launch" [size]="16" />{{ tr('common.forBusinesses') }}</span>
              <adeni-carbon-icon name="chevron--right" [size]="16" />
            </a>
          </li>
        }
        <li>
          <a class="app-row" routerLink="/privacy">
            <span class="app-row__main"><adeni-carbon-icon name="security" [size]="16" />{{ tr('footer.links.privacy') }}</span>
            <adeni-carbon-icon name="chevron--right" [size]="16" />
          </a>
        </li>
        <li>
          <a class="app-row" routerLink="/terms">
            <span class="app-row__main"><adeni-carbon-icon name="document" [size]="16" />{{ tr('footer.links.terms') }}</span>
            <adeni-carbon-icon name="chevron--right" [size]="16" />
          </a>
        </li>
      </ul>

      @if (session.auth0Mode) {
        <p class="app-more">
          <button type="button" class="cds-btn cds-btn--tertiary" (click)="session.logout()">
            {{ tr('discover.layout.logOut') }}
          </button>
        </p>
      }
    </app-portal-page>
  `,
})
export class CustomerProfileComponent {
  protected readonly tr = injectAdeniTranslate();
  protected readonly session = inject(CustomerAppSessionService);
  protected readonly businessPortalUrl = inject(ADENI_DISCOVER_CONFIG).portalAppUrl.trim() || null;
}
