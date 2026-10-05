import { Component, inject, Injector, OnInit, signal } from "@angular/core";
import { RouterLink, RouterLinkActive, RouterOutlet } from "@angular/router";
import type { AuthService } from "@auth0/auth0-angular";
import { AsyncPipe } from "@angular/common";
import type { BusinessProfile } from "@adeni/shared";
import { PORTAL_NAV } from "../core/portal-nav";
import { BusinessApiService } from "../core/services/business-api.service";
import {
  ADENI_PORTAL_CONFIG,
  isAuth0Configured,
  isBusinessPortalDevMode,
} from "../core/adeni-config";
import { PendingBookingsBellComponent } from "../shared/pending-bookings-bell.component";
import { resolveAuthService } from "../core/auth0-rxjs";
import { AdeniStaffSidebarBrandComponent } from "@adeni/ui";

@Component({
  selector: "app-portal-shell",
  standalone: true,
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    AsyncPipe,
    PendingBookingsBellComponent,
    AdeniStaffSidebarBrandComponent,
  ],
  templateUrl: "./portal-shell.component.html",
  styleUrl: "./portal-shell.component.scss",
})
export class PortalShellComponent implements OnInit {
  readonly nav = PORTAL_NAV;
  readonly config = inject(ADENI_PORTAL_CONFIG);
  private readonly injector = inject(Injector);
  readonly auth: AuthService | null = isAuth0Configured(this.config)
    ? resolveAuthService(this.injector)
    : null;
  private readonly businessApi = inject(BusinessApiService);

  readonly profile = signal<BusinessProfile | null>(null);
  readonly devMode = isBusinessPortalDevMode(this.config);
  readonly auth0Mode = isAuth0Configured(this.config);

  ngOnInit(): void {
    void this.loadProfile();
  }

  async loadProfile(): Promise<void> {
    this.profile.set(await this.businessApi.getTenantProfile());
  }

  login(): void {
    this.auth?.loginWithRedirect({
      appState: { target: window.location.pathname },
    });
  }

  logout(): void {
    this.auth?.logout({
      logoutParams: {
        returnTo: window.location.origin,
      },
    });
  }

  discoverUrl(): string {
    return this.config.discoverWebUrl;
  }
}
